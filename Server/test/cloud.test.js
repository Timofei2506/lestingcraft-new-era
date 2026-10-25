import test from 'node:test';
import assert from 'node:assert/strict';
import {CloudService,CloudError,handleCloudRequest,explain,CLOUD_ACTIONS} from '../src/cloud.js';

const KEY = 'f'.repeat(32);
const LISTING = '0b6d5c1e-1a1a-4a4a-9a9a-123456789abc';

/** Records calls and answers like PostgREST. */
function fakeSupabase(handler) {
  const calls = [];
  const fetch = async (url, init) => {
    const name = url.split('/').pop();
    const args = JSON.parse(init.body);
    calls.push({ name, args, headers: init.headers });
    const result = handler(name, args);
    if (result instanceof Error) return new Response(JSON.stringify({ message: result.message }), { status: 400 });
    return new Response(JSON.stringify(result ?? null), { status: 200 });
  };
  return { fetch, calls };
}

test('the fetch wrapper survives a receiver change (Workers "Illegal invocation")', async () => {
  // A Worker's global fetch rejects being called as a method of another object.
  const strictFetch = function (url, init) {
    if (this !== undefined && this !== globalThis) throw new TypeError('Illegal invocation');
    return Promise.resolve(new Response('{"balance":7}', { status: 200 }));
  };
  const service = new CloudService('https://example.supabase.co', 'k', strictFetch);
  assert.deepEqual(await service.state(KEY), { balance: 7 });
});

test('a transport failure carries an operator-facing code but a calm message', async () => {
  const service = new CloudService('https://example.supabase.co', 'k', async () => { throw new TypeError('Illegal invocation'); });
  await assert.rejects(() => service.state(KEY), (error) => {
    assert.equal(error.status, 503);
    assert.ok(error.message.includes('недоступен'));
    assert.ok(error.code.startsWith('unreachable:'));
    return true;
  });
});

test('the service sends the service key and never exposes it to callers', async () => {
  const { fetch, calls } = fakeSupabase(() => ({ balance: 0 }));
  const service = new CloudService('https://example.supabase.co/', 'secret-key', fetch);
  const state = await service.state(KEY);
  assert.deepEqual(state, { balance: 0 });
  assert.equal(calls[0].name, 'svc_state');
  assert.equal(calls[0].args.p_player_key, KEY);
  assert.equal(calls[0].headers.apikey, 'secret-key');
  assert.equal(calls[0].headers.Authorization, 'Bearer secret-key');
  assert.ok(!JSON.stringify(state).includes('secret-key'));
});

test('database errors are translated, unknown ones are hidden', () => {
  assert.equal(explain('insufficient coins'), 'Не хватает монет.');
  assert.equal(explain('cannot buy your own listing'), 'Нельзя купить собственный лот.');
  assert.equal(explain('duplicate key value violates unique constraint "x"'), null);
});

test('an unknown database error becomes a generic message, not a leak', async () => {
  const service = new CloudService('https://example.supabase.co', 'k', fakeSupabase(() => new Error('relation "secret_table" does not exist')).fetch);
  await assert.rejects(() => service.state(KEY), (error) => {
    assert.ok(error instanceof CloudError);
    assert.equal(error.status, 502);
    assert.ok(!/secret_table/.test(error.message));
    return true;
  });
});

test('a known refusal reaches the player in Russian', async () => {
  const service = new CloudService('https://example.supabase.co', 'k', fakeSupabase(() => new Error('insufficient coins')).fetch);
  await assert.rejects(() => service.buy(KEY, 'skin_ember'), /Не хватает монет/);
});

test('a network failure is a 503, not a crash', async () => {
  const service = new CloudService('https://example.supabase.co', 'k', async () => { throw new TypeError('fetch failed'); });
  await assert.rejects(() => service.state(KEY), (error) => error.status === 503);
});

const call = (path, { token = 'a'.repeat(64), method = 'POST', body } = {}) => new Request(`https://game.example${path}`, {
  method,
  headers: token ? { Authorization: `Bearer ${token}` } : {},
  body: method === 'POST' ? JSON.stringify(body ?? {}) : undefined,
});

function gateway(playerKey = KEY) {
  const seen = [];
  const service = new Proxy({}, {
    get: (_, method) => async (...args) => { seen.push([method, ...args]); return { echo: [method, ...args] }; },
  });
  const resolvePlayer = async (token) => (token === 'a'.repeat(64) ? playerKey : null);
  return { service, resolvePlayer, seen, run: (request) => handleCloudRequest(request, { service, resolvePlayer }) };
}

test('the gateway refuses anonymous callers before touching the economy', async () => {
  const g = gateway();
  const response = await g.run(call('/api/cloud/state', { token: '' }));
  assert.equal(response.status, 401);
  assert.equal(g.seen.length, 0);
});

test('the gateway refuses accounts without a player key', async () => {
  const g = gateway(null);
  const response = await g.run(call('/api/cloud/buy', { body: { code: 'skin_ember' } }));
  assert.equal(response.status, 401);
  assert.equal(g.seen.length, 0);
});

test('every action reaches the right RPC with validated arguments', async () => {
  const g = gateway();
  const cases = [
    ['/api/cloud/state', {}, 'state', [KEY]],
    ['/api/cloud/buy', { code: 'skin_ember' }, 'buy', [KEY, 'skin_ember']],
    ['/api/cloud/equip', { code: 'cape_aurora' }, 'equip', [KEY, 'cape_aurora']],
    ['/api/cloud/market', {}, 'board', [KEY]],
    ['/api/cloud/list', { code: 'skin_tide', price: 120 }, 'list', [KEY, 'skin_tide', 120]],
    ['/api/cloud/cancel', { id: LISTING }, 'cancel', [KEY, LISTING]],
    ['/api/cloud/purchase', { id: LISTING }, 'purchase', [KEY, LISTING]],
    ['/api/cloud/friends', {}, 'friends', [KEY]],
    ['/api/cloud/friend/request', { key: 'b'.repeat(32), message: 'привет' }, 'friendRequest', [KEY, 'b'.repeat(32), 'привет']],
    ['/api/cloud/friend/accept', { id: 7 }, 'friendAccept', [KEY, 7]],
    ['/api/cloud/friend/decline', { id: 8 }, 'friendDecline', [KEY, 8]],
    ['/api/cloud/friend/remove', { key: 'b'.repeat(32) }, 'friendRemove', [KEY, 'b'.repeat(32)]],
  ];
  for (const [path, body, method, args] of cases) {
    const response = await g.run(call(path, { body }));
    assert.equal(response.status, 200, path);
    const payload = await response.json();
    assert.equal(payload.ok, true, path);
    assert.deepEqual(g.seen.at(-1), [method, ...args], path);
  }
});

test('bad arguments never reach the database', async () => {
  const g = gateway();
  const bad = [
    ['/api/cloud/buy', { code: '../../etc/passwd' }],
    ['/api/cloud/buy', {}],
    ['/api/cloud/equip', { code: 'Skin Ember' }],
    ['/api/cloud/list', { code: 'skin_tide', price: 0 }],
    ['/api/cloud/list', { code: 'skin_tide', price: 1.5 }],
    ['/api/cloud/list', { code: 'skin_tide', price: 10_000_000 }],
    ['/api/cloud/cancel', { id: 'not-a-uuid' }],
    ['/api/cloud/purchase', { id: 42 }],
    ['/api/cloud/friend/request', { key: 'short' }],
    ['/api/cloud/friend/accept', { id: -1 }],
    ['/api/cloud/friend/accept', { id: '3' }],
  ];
  for (const [path, body] of bad) {
    const response = await g.run(call(path, { body }));
    assert.equal(response.status, 400, path + ' ' + JSON.stringify(body));
    const payload = await response.json();
    assert.equal(payload.ok, false);
    assert.ok(payload.message.length > 0);
  }
  assert.equal(g.seen.length, 0, 'no RPC may run for a rejected request');
});

test('unknown actions and wrong methods are 404/405', async () => {
  const g = gateway();
  assert.equal((await g.run(call('/api/cloud/sql', { body: {} }))).status, 404);
  assert.equal((await g.run(call('/api/cloud/state', { method: 'GET' }))).status, 405);
  assert.equal((await g.run(call('/api/cloud/', { body: {} }))).status, 404);
});

test('an oversized body is refused', async () => {
  const g = gateway();
  const big = { code: 'x'.repeat(4000) };
  const response = await g.run(call('/api/cloud/buy', { body: big }));
  assert.equal(response.status, 413);
});

test('the action table covers exactly the documented endpoints', () => {
  assert.deepEqual(Object.keys(CLOUD_ACTIONS).sort(), [
    'buy', 'cancel', 'equip', 'friend/accept', 'friend/decline', 'friend/remove', 'friend/request',
    'friends', 'list', 'market', 'purchase', 'state',
  ]);
});

// === An account that never opened the wallet must still be reachable for friends ===
test('ensureProfile creates the row so a friend request is not dropped', async () => {
  const calls = [];
  const service = new CloudService('https://example.supabase.co', 'service-key', async (url, init) => {
    calls.push({ url: String(url), body: init?.body });
    return new Response(JSON.stringify({ id: 'uuid-1' }), { status: 200, headers: { 'Content-Type': 'application/json' } });
  });
  const key = 'a'.repeat(32);
  await service.ensureProfile(key, 'Игрок');
  assert.equal(calls.length, 1);
  assert.match(calls[0].url, /rpc\/svc_ensure_profile$/);
  assert.deepEqual(JSON.parse(calls[0].body), { p_player_key: key, p_display_name: 'Игрок' });
});

test('ensureProfile without a name still sends null, not "undefined"', async () => {
  let body = null;
  const service = new CloudService('https://example.supabase.co', 'service-key', async (url, init) => {
    body = JSON.parse(init.body);
    return new Response('{}', { status: 200 });
  });
  await service.ensureProfile('b'.repeat(32));
  assert.equal(body.p_display_name, null);
});

// === Friends by nickname: the UI shows names, not keys ===
test('friend request by nickname resolves the name and creates the profile with it', async () => {
  const calls = [];
  const service = new CloudService('https://example.supabase.co', 'service-key', async (url, init) => {
    calls.push({ url: String(url), body: init?.body ? JSON.parse(init.body) : null });
    return new Response(JSON.stringify({ result: 'sent' }), { status: 200, headers: { 'Content-Type': 'application/json' } });
  });
  const me = 'a'.repeat(32);
  const target = 'b'.repeat(32);
  const result = { ok: true };
  const body = { username: 'EHOT_Night', message: 'привет' };
  const action = CLOUD_ACTIONS['friend/request'];
  const out = await action(service, me, body, { resolveUsername: async name => ({ uid: target, username: name.toLowerCase() }) });
  assert.equal(out.result, 'sent');
  assert.match(calls[0].url, /rpc\/svc_ensure_profile$/);
  assert.equal(calls[0].body.p_display_name, 'ehot_night');
  assert.match(calls[1].url, /rpc\/svc_friend_request$/);
  assert.equal(calls[1].body.p_target_key, target);

});

test('an unknown nickname says so instead of pretending the request was sent', async () => {
  const service = new CloudService('https://example.supabase.co', 'service-key', async () => new Response('{}', { status: 200 }));
  const action = CLOUD_ACTIONS['friend/request'];
  await assert.rejects(
    async () => action(service, 'a'.repeat(32), { username: 'nobody_here' }, { resolveUsername: async () => null }),
    error => error instanceof CloudError && error.status === 404 && /не найден/.test(error.message) && error.code === 'not_found',
  );
});

test('a nickname with forbidden characters is rejected before any lookup', async () => {
  const service = new CloudService('https://example.supabase.co', 'service-key', async () => new Response('{}', { status: 200 }));
  const action = CLOUD_ACTIONS['friend/request'];
  await assert.rejects(
    async () => action(service, 'a'.repeat(32), { username: '<script>' }, { resolveUsername: async () => ({ uid: 'x' }) }),
    error => error instanceof CloudError && error.status === 400,
  );
});
