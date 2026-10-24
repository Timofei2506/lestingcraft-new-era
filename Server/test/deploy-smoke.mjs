// Live deployment smoke test for the Cloudflare Worker.
// NOT a Unity, graphics or gameplay test: it proves that the code that is
// actually deployed on workers.dev speaks the same protocol version as this
// commit, can create a room and can accept a real WebSocket client.
//
// Usage:
//   TEST_ENDPOINT=https://lestingcraft-coop.example.workers.dev node test/deploy-smoke.mjs
//   EXPECTED_COMMIT=<git sha>   — fails when /health reports another build
//
// Exit codes: 0 = verified (or skipped because of the room-creation quota),
//             1 = the deployment is stale or broken.
import WebSocket from 'ws';
import {RULES_VERSION, CONTENT_VERSION} from '../src/protocol.js';

const endpoint = (process.env.TEST_ENDPOINT || 'https://lestingcraft-coop.kroterallitir.workers.dev').replace(/\/+$/, '');
const expectedCommit = process.env.EXPECTED_COMMIT || '';
const attempts = Number(process.env.HEALTH_ATTEMPTS || 20);
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const say = line => console.log(line);
const fail = message => { console.log(`::error::${message}`); console.error(`FAIL: ${message}`); };

let failed = false;
const check = (condition, message) => {
  if (condition) say(`PASS: ${message}`);
  else { failed = true; fail(message); }
};

// 1. Edge propagation: a fresh deploy can answer with the previous script for a
//    few seconds, so poll instead of failing on the first response.
let health = null;
for (let attempt = 1; attempt <= attempts; attempt++) {
  try {
    const response = await fetch(`${endpoint}/health`, {headers: {'Cache-Control': 'no-cache'}});
    if (!response.ok) throw Error(`HTTP ${response.status}`);
    health = await response.json();
    if (health.rulesVersion === RULES_VERSION && (!expectedCommit || health.commit === expectedCommit)) break;
  } catch (error) {
    health = health || null;
    say(`health attempt ${attempt}: ${error.message}`);
  }
  if (attempt < attempts) await delay(3000);
}

if (!health) {
  fail(`${endpoint}/health is unreachable`);
  process.exit(1);
}

say(`live health: ${JSON.stringify(health)}`);
check(health.ok === true, 'health answers ok');
check(health.game === 'LestingCraft', 'health identifies the game');
check(health.rulesVersion === RULES_VERSION, `live rulesVersion ${health.rulesVersion} matches repo ${RULES_VERSION}`);
check(health.contentVersion === CONTENT_VERSION, `live contentVersion ${health.contentVersion} matches repo ${CONTENT_VERSION}`);
if (expectedCommit) check(health.commit === expectedCommit, `live commit ${health.commit?.slice(0, 12)} matches deployed ${expectedCommit.slice(0, 12)}`);
else if (health.commit === 'dev') say('NOTE: commit marker is "dev" — deploy was run without GIT_SHA');

if (failed) process.exit(1);

// 2. A room created by the current protocol generation must be accepted.
const ownerKey = 'e' + '0'.repeat(30) + '7';
const created = await fetch(`${endpoint}/api/rooms`, {
  method: 'POST',
  headers: {'Content-Type': 'application/json'},
  body: JSON.stringify({version: 1, rulesVersion: RULES_VERSION, mode: 2, dimension: 0, seed: 2506, ownerKey})
});

if (created.status === 429) {
  say('SKIP: room creation quota reached for this runner IP — health already verified, live protocol smoke postponed');
  process.exit(0);
}
check(created.status === 201, `POST /api/rooms -> ${created.status}`);
if (created.status !== 201) {
  console.log(`::error::${await created.text()}`);
  process.exit(1);
}

const room = await created.json();
check(room.type === 'created', 'room creation packet');
check(room.rulesVersion === RULES_VERSION, `new room rulesVersion ${room.rulesVersion}`);
check(room.contentVersion === CONTENT_VERSION, `new room contentVersion ${room.contentVersion}`);
check(!('ownerKey' in room), 'create response does not leak the private owner key');

// 3. Real WebSocket handshake and one trusted round-trip.
//    A `position` packet is deliberately not echoed to its own sender, so the
//    round-trip uses `chat`, which the server echoes back to everyone including
//    the author after validating it.
const chatText = `deploy-smoke ${Date.now()}`;
const socketUrl = `${endpoint.replace(/^http/, 'ws')}/api/rooms/${room.code}/ws?key=${ownerKey}&clientRules=${RULES_VERSION}&name=${encodeURIComponent('deploy-smoke')}`;
const result = await new Promise(resolve => {
  const socket = new WebSocket(socketUrl);
  let welcome = null;
  let started = false;
  let settled = false;
  const finish = value => {
    if (settled) return;
    settled = true;
    clearTimeout(timer);
    try { socket.terminate(); } catch {}
    resolve(value);
  };
  const timer = setTimeout(() => finish({error: 'timeout waiting for the server', welcome}), 20000);
  socket.on('open', () => say('PASS: websocket upgrade accepted'));
  socket.on('error', error => finish({error: error.message, welcome}));
  socket.on('message', raw => {
    let packet;
    try { packet = JSON.parse(raw.toString()); } catch { return; }
    if (packet.type === 'welcome' && !welcome) {
      welcome = packet;
      // Rooms created by rules >= 7 wait in a host-started lobby; gameplay
      // packets such as chat are refused until this host-only packet lands.
      if (packet.lobby && packet.host) socket.send(JSON.stringify({type: 'lobby_start'}));
      else socket.send(JSON.stringify({type: 'chat', message: chatText}));
      return;
    }
    if (packet.type === 'lobby_start') {
      started = true;
      socket.send(JSON.stringify({type: 'chat', message: chatText}));
      return;
    }
    if (packet.type === 'chat' && packet.message === chatText) finish({welcome, chat: packet});
    else if (packet.type === 'error') finish({error: packet.message, welcome});
  });
});

if (result.error) {
  fail(`live websocket: ${result.error}`);
  process.exit(1);
}
const welcome = result.welcome || {};
check(welcome.type === 'welcome', 'server sent a welcome packet');
check(welcome.rulesVersion === RULES_VERSION, `welcome rulesVersion ${welcome.rulesVersion}`);
check(welcome.contentVersion === CONTENT_VERSION, `welcome contentVersion ${welcome.contentVersion}`);
check(!('ownerKey' in welcome), 'welcome does not leak the private owner key');
check(!!result.chat, `lobby start + chat round-trip (${result.chat?.message})`);
check(!failed, 'deployed server matches this commit');

say(failed ? 'LIVE SMOKE FAILED' : 'LIVE SMOKE OK');
process.exit(failed ? 1 : 0);
