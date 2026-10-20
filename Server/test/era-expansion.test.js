import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {WebSocket} from 'ws';

import {
  RULES_VERSION, createConfig, acceptsClient, validateEdit, validatePlayerSave
} from '../src/protocol.js';
import {biomeNames, biomeNamesV8, biomeAt, sampleBiomes} from '../src/terrain-biomes.js';
import {floor as storyFloor, site as storySite} from '../src/story-world.js';
import {applyExpedition} from '../src/terrain-expedition.js';
import {Fauna, animalHeight} from '../src/wildlife.js';
import {Living, transact} from '../src/living.js';
import {
  BODY_POSITIONS, PLANET_SECONDS, MOON_SECONDS,
  beginSolarMission, setCruiseAutopilot, advanceMission, landMission, missionView
} from '../src/space-mission.js';

const KEY_A = 'a'.repeat(32);
const KEY_B = 'b'.repeat(32);

test('Era expansion protocol v13 / contentVersion 8 preserves full legacy compatibility', () => {
  assert.equal(RULES_VERSION, 13);
  const cfg = createConfig({version: 1, ownerKey: KEY_A, seed: 2506, dimension: 0, mode: 0, rulesVersion: 13});
  assert.equal(cfg.rulesVersion, 13);
  assert.equal(cfg.contentVersion, 8);
  assert.equal(acceptsClient(cfg, 13), true);
  assert.equal(acceptsClient(cfg, 12), false);

  const legacyV7 = createConfig({version: 1, ownerKey: KEY_A, seed: 2506, dimension: 0, mode: 0, rulesVersion: 12});
  assert.equal(legacyV7.contentVersion, 7);
  assert.equal(acceptsClient(legacyV7, 12), true);
  assert.equal(acceptsClient(legacyV7, 13), true);

  const p = {positioned: true, lastEdit: 0, x: 0.5, y: 50.5, z: 0.5};
  for (let id = 54; id <= 69; id++) {
    assert.equal(validateEdit({x: 0, y: 50, z: 0, block: id}, p, new Map(), 1000, 69, 64).block, id);
  }
  assert.throws(() => validateEdit({x: 0, y: 50, z: 0, block: 70}, p, new Map(), 1000, 69, 64));
  assert.throws(() => validateEdit({x: 0, y: 50, z: 0, block: 54}, p, new Map(), 1000, 53, 64));

  const empty = () => ({item: 0, count: 0, durability: 0});
  const save = {
    inventory: {selected: 0, slots: Array.from({length: 45}, empty), equipment: Array.from({length: 8}, empty)},
    survival: {health: 100, hunger: 85, stamina: 100, temperature: 37, wetness: 0}
  };
  save.inventory.slots[0] = {item: 55, count: 1, durability: 0}; // Bed
  save.inventory.slots[1] = {item: 56, count: 8, durability: 0}; // Lantern
  save.inventory.slots[2] = {item: 67, count: 4, durability: 0}; // Andromeda Crystal
  assert.equal(validatePlayerSave(save, true, true, true, true).inventory.slots[2].item, 67);
  assert.throws(() => validatePlayerSave(save, true, true, true, false));
});

test('All 12 v8 surface biomes, new biome blocks 60..65, and immutable v7 biomes work deterministically', () => {
  assert.equal(biomeNames.length, 6);
  assert.equal(biomeNamesV8.length, 12);

  const seenV8 = new Set();
  const seenBlocks = new Set();
  for (let x = -2400; x <= 2400; x += 96) {
    for (let z = -2400; z <= 2400; z += 96) {
      if (x >= -110 && x <= 270 && z >= -190 && z <= 158) continue;
      const b7 = biomeAt(x, z, 2506, 7);
      const b8 = biomeAt(x, z, 2506, 8);
      assert.ok(b7 >= 0 && b7 < 6);
      assert.ok(b8 >= 0 && b8 < 12);
      seenV8.add(b8);
    }
  }
  assert.equal(seenV8.size, 12, 'All 12 surface biomes must generate across world space');

  for (let x = -1600; x <= 1600 && seenBlocks.size < 6; x += 29) {
    for (let z = -1600; z <= 1600 && seenBlocks.size < 6; z += 29) {
      if (x >= -110 && x <= 270 && z >= -190 && z <= 158) continue;
      const k = biomeAt(x, z, 2506, 8);
      if (k < 6) continue;
      for (let y = 39; y <= 66; y++) {
        const blk = sampleBiomes(x, y, z, 2506, 8);
        if (blk >= 60 && blk <= 65) seenBlocks.add(blk);
      }
    }
  }
  for (const id of [60, 61, 62, 63, 64, 65]) {
    assert.ok(seenBlocks.has(id), `Expected biome block ${id} to appear naturally in v8 world`);
  }
});

test('Upgraded v8 Story Village & Expedition Villages replace lamp-post campfires with Lanterns/Torches and furnish Beds/Bookshelves/Shingles', () => {
  const realBase = storyFloor(2506);
  // All 5 in-region original poles had raw campfires (15) at ry = 5; v8 upgrades all 6 village/ford poles!
  for (const [lx, lz, expectedLight] of [
    [88, 48, 56], [105, 48, 56], [96, 26, 56], [-39, -24, 54], [43, -86, 54]
  ]) {
    assert.equal(storySite(lx, realBase + 5, lz, 2506, 7), 15, `Legacy v7 keeps campfire at (${lx},${lz})`);
    assert.equal(storySite(lx, realBase + 5, lz, 2506, 8), expectedLight, `v8 replaces pole campfire at (${lx},${lz}) with ${expectedLight}`);
    assert.equal(storySite(lx, realBase + 4, lz, 2506, 8), 58, `v8 places carved oak head at (${lx},${lz})`);
  }
  // North gate pole (96, 73) in dist <= 2 also gets full Lantern pole in v8
  assert.equal(storySite(96, realBase + 5, 73, 2506, 8), 56);
  assert.equal(storySite(96, realBase + 4, 73, 2506, 8), 58);

  assert.equal(storySite(80, realBase + 1, 38, 2506, 8), 55, 'Cottage contains real Bed (55)');
  assert.equal(storySite(84, realBase + 1, 34, 2506, 8), 57, 'Cottage contains Bookshelf (57)');
  assert.equal(storySite(82, realBase + 2, 32, 2506, 8), 54, 'Cottage doorway lit by real Torch (54)');

  const fakeVillageCol = {x: 400, z: 400, surface: true, mine: false, base: 50, kind: 0, rx: -16, rz: -13};
  assert.equal(applyExpedition(fakeVillageCol, 51, 2506, 0, 8), 55);
  assert.equal(applyExpedition(fakeVillageCol, 51, 2506, 0, 7), 0);
});

test('All 13 cosmic bodies across Solar System, Extrasolar Stars & Andromeda M31 fly and land authoritatively', () => {
  const expectedKeys = [
    'earth', 'moon', 'mars', 'venus', 'mercury',
    'europa', 'titan', 'pluto',
    'proxima_b', 'trappist_1e', 'kepler_452b',
    'andromeda_prime', 'somnia'
  ];
  assert.equal(MOON_SECONDS, 1800);
  for (const key of expectedKeys) {
    assert.ok(BODY_POSITIONS[key], `Missing cosmic destination ${key}`);
    if (key !== 'earth' && key !== 'moon') {
      assert.ok(PLANET_SECONDS[key] >= 5400, `Missing trip duration for ${key}`);
    }
  }
  let m = beginSolarMission({crew: [KEY_A, KEY_B], boarded: [KEY_A, KEY_B], pilot: KEY_A, now: 1000, from: 'earth', to: 'andromeda_prime'});
  assert.equal(m.to, 'andromeda_prime');
  const t = m.ignitionAt;
  m = advanceMission(m, t + 631000, {player: KEY_A}); // ascent -> cruise
  m = setCruiseAutopilot(m, KEY_A, true, t + 631000);
  const approachStart = t + (m.seconds - 180) * 1000;
  m = advanceMission(m, approachStart, {player: KEY_A}); // cruise -> approach
  for (let step = 1000; step <= 180000; step += 1000) {
    m = advanceMission(m, approachStart + step, {player: KEY_A, landingThrottle: 1});
  }
  const landed = landMission(m, KEY_A, t + m.seconds * 1000);
  assert.equal(missionView(landed, t + m.seconds * 1000).phase, 'landed');
});

test('20 wildlife species and 10 diverse villager trade professions are active and authoritative', () => {
  for (let s = 0; s < 20; s++) {
    assert.ok(animalHeight(s) > 0.2, `Species ${s} must have valid physical height`);
  }
  const fauna = new Fauna(2506, null, new Map(), 8);
  for (let gx = -320; gx <= 320; gx += 48) {
    for (let gz = -320; gz <= 320; gz += 48) {
      fauna.spawn({id: 'p1', x: gx, y: 55, z: gz, alive: true});
    }
  }
  const seenSpecies = new Set(fauna.snapshots().map(a => a.species));
  assert.ok(seenSpecies.size >= 8, `Expected diverse wildlife species, got ${seenSpecies.size}`);
  assert.ok([...seenSpecies].some(s => s >= 12), 'Expanded species 12..19 must spawn naturally in v8 worlds');

  const liv = new Living(2506, {}, new Map(), 8);
  liv.actors.push({id: 'v-lamp', kind: 0, job: 6, x: 96.5, y: 45, z: 48.5, yaw: 0, health: 20, seed: 77, homeX: 96, homeZ: 48});
  const empty = () => ({item: 0, count: 0, durability: 0});
  const playerSave = {
    inventory: {selected: 0, revision: 1, slots: Array.from({length: 45}, empty), equipment: Array.from({length: 8}, empty)},
    survival: {health: 100, hunger: 90, stamina: 100, temperature: 37, wetness: 0}
  };
  playerSave.inventory.slots[0] = {item: 147, count: 20, durability: 0}; // Coal -> 2 Wrought Lanterns (56)
  const traded = transact(
    liv,
    {action: 'trade', animal: 'v-lamp'},
    {positioned: true, x: 96.5, y: 45.8, z: 48.5, playerKey: KEY_A},
    playerSave
  );
  assert.ok(traded.inventory.slots.some(s => s.item === 56 && s.count === 2), 'Miner trade yields 2 Wrought Lanterns (56)');
});

test('Live multiplayer WebSocket server enforces 100% Dream isolation & world lockout, then resyncs on wake', async (t) => {
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'lc-dream-ws-'));
  const port = 29400 + Math.floor(Math.random() * 1500);
  const proc = spawn(process.execPath, ['src/node.js'], {
    cwd: path.resolve(import.meta.dirname, '..'),
    env: {...process.env, PORT: String(port), DATA_DIR: tmpDir},
    stdio: ['ignore', 'pipe', 'pipe']
  });
  t.after(() => {
    proc.kill('SIGTERM');
    fs.rmSync(tmpDir, {recursive: true, force: true});
  });

  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Server startup timeout')), 8000);
    proc.stdout.on('data', chunk => {
      if (String(chunk).includes('LestingCraft standalone server')) {
        clearTimeout(timer);
        resolve();
      }
    });
  });

  const createRes = await fetch(`http://127.0.0.1:${port}/api/rooms`, {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({version: 1, ownerKey: KEY_A, seed: 2506, dimension: 0, mode: 0, rulesVersion: 13})
  });
  const created = await createRes.json();
  assert.equal(created.rulesVersion, 13);
  assert.equal(created.contentVersion, 8);

  const connectPeer = (name, key) => new Promise((resolve, reject) => {
    const inbox = [];
    const ws = new WebSocket(`ws://127.0.0.1:${port}/api/rooms/${created.code}/ws?name=${name}&key=${key}&clientRules=13`);
    ws.on('message', raw => inbox.push(JSON.parse(String(raw))));
    ws.on('error', reject);
    const waitWelcome = setInterval(() => {
      if (inbox.some(m => m.type === 'welcome')) {
        clearInterval(waitWelcome);
        resolve({ws, inbox});
      }
    }, 15);
  });

  const alice = await connectPeer('Alice', KEY_A);
  const bob = await connectPeer('Bob', KEY_B);
  t.after(() => { alice.ws.close(); bob.ws.close(); });

  const waitMsg = (peerObj, pred, ms = 2500) => new Promise((resolve, reject) => {
    const start = Date.now();
    const iv = setInterval(() => {
      const hit = peerObj.inbox.find(pred);
      if (hit) { clearInterval(iv); resolve(hit); }
      else if (Date.now() - start > ms) {
        clearInterval(iv);
        reject(new Error('Timeout waiting for message; inbox=' + JSON.stringify(peerObj.inbox.map(x => ({t: x.type, c: x.code, m: x.message})))));
      }
    }, 15);
  });

  alice.ws.send(JSON.stringify({type: 'lobby_start'}));
  await waitMsg(alice, m => m.type === 'lobby_start');
  await waitMsg(bob, m => m.type === 'lobby_start');

  // Position both players outside story protected zone (Region(32, 32) = 16 > 0)
  alice.ws.send(JSON.stringify({type: 'position', x: 32.5, y: 60.6, z: 32.5, yaw: 0}));
  bob.ws.send(JSON.stringify({type: 'position', x: 34.5, y: 60.6, z: 32.5, yaw: 90}));
  await waitMsg(bob, m => m.type === 'player' && m.name === 'Alice');

  // 1. Alice falls into the creepy Dream dimension!
  alice.inbox.length = 0;
  bob.inbox.length = 0;
  alice.ws.send(JSON.stringify({type: 'dream_state', on: true, dream: true}));
  await waitMsg(bob, m => m.type === 'leave');

  // 2. While in Dream, EVERY world/chat/movement interaction from Alice is strictly rejected!
  alice.ws.send(JSON.stringify({type: 'edit', x: 33, y: 60, z: 32, block: 56}));
  const editErr = await waitMsg(alice, m => m.type === 'error' && m.code === 'edit');
  assert.match(editErr.message, /Dream/i);

  alice.ws.send(JSON.stringify({type: 'chat', message: 'Кто-нибудь слышит меня из Сна?!'}));
  const chatErr = await waitMsg(alice, m => m.type === 'error' && m.code === 'chat');
  assert.match(chatErr.message, /Dream/i);

  // 3. Meanwhile Bob chats and places a real Lantern (56) — Alice in Dream receives ZERO packets!
  bob.ws.send(JSON.stringify({type: 'chat', message: 'Я ставлю фонарь на улице'}));
  bob.ws.send(JSON.stringify({type: 'edit', x: 35, y: 60, z: 32, block: 56}));
  await waitMsg(bob, m => m.type === 'edit' && m.block === 56);
  await new Promise(r => setTimeout(r, 120));

  assert.equal(alice.inbox.some(m => m.type === 'chat'), false, 'Alice in Dream must NOT receive waking players chat');
  assert.equal(alice.inbox.some(m => m.type === 'edit'), false, 'Alice in Dream must NOT receive waking world edits');
  assert.equal(bob.inbox.some(m => m.type === 'chat' && m.message.includes('Сна')), false, 'Bob must NOT hear Alice from Dream');

  // 4. Alice reaches the Pale Arch and wakes up — server sends full authoritative resync including Bob's Lantern (56) & Bob's avatar!
  alice.inbox.length = 0;
  alice.ws.send(JSON.stringify({type: 'dream_state', on: false, dream: false}));
  const wokeResync = await waitMsg(alice, m => m.type === 'welcome');
  assert.ok(wokeResync.edits.some(e => e.x === 35 && e.y === 60 && e.z === 32 && e.block === 56), 'Waking up resyncs missed world edits');
  assert.ok(wokeResync.players.some(p => p.name === 'Bob'), 'Waking up restores visible co-op peers');
});
