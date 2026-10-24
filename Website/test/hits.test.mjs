// The visit counter must stay honest: no per-visitor storage, junk input ignored,
// and it must answer 503 instead of pretending when KV is missing.
import test from 'node:test';
import assert from 'node:assert/strict';
import {handleHit, handleStats, dayKey, sanitizeSource} from '../src/hits.mjs';

class FakeKV {
  constructor(seed = {}) { this.map = new Map(Object.entries(seed)); this.puts = 0; }
  async get(key) { return this.map.get(key) ?? null; }
  async put(key, value) { this.puts++; this.map.set(key, value); }
}

const post = (body = '', headers = {}) => new Request('https://site.test/api/hit', {
  method: 'POST', headers: { 'Content-Type': 'application/json', ...headers }, body: body || undefined,
});

test('dayKey is a UTC date', () => {
  assert.equal(dayKey(new Date('2026-10-24T23:59:59Z')), '2026-10-24');
});

test('sanitizeSource keeps only known sources', () => {
  assert.equal(sanitizeSource('friend'), 'friend');
  assert.equal(sanitizeSource('FRIEND'), 'friend');
  assert.equal(sanitizeSource('<script>'), '');
  assert.equal(sanitizeSource('../../etc/passwd'), '');
  assert.equal(sanitizeSource(undefined), '');
});

test('a hit increments total and the day counter', async () => {
  const kv = new FakeKV({ 'hits:total': '41', 'hits:day:2026-10-24': '7' });
  const response = await handleHit(post(JSON.stringify({ source: 'friend' })), { WAITLIST: kv }, new Date('2026-10-24T10:00:00Z'));
  assert.equal(response.status, 200);
  assert.equal(await kv.get('hits:total'), '42');
  assert.equal(await kv.get('hits:day:2026-10-24'), '8');
  assert.equal(await kv.get('hits:src:friend'), '1');
});

test('malformed body still counts the visit and stores nothing personal', async () => {
  const kv = new FakeKV();
  const response = await handleHit(post('{not json'), { WAITLIST: kv }, new Date('2026-10-24T10:00:00Z'));
  assert.equal(response.status, 200);
  assert.equal(await kv.get('hits:total'), '1');
  assert.deepEqual([...kv.map.keys()].sort(), ['hits:day:2026-10-24', 'hits:total']);
});

test('a body with an IP or user agent is not stored', async () => {
  const kv = new FakeKV();
  await handleHit(post(JSON.stringify({ source: 'search', ip: '1.2.3.4', ua: 'UA' })), { WAITLIST: kv }, new Date('2026-10-24T10:00:00Z'));
  const stored = [...kv.map.values()].join('|');
  assert.ok(!stored.includes('1.2.3.4'));
  assert.ok(!stored.includes('UA'));
});

test('oversized bodies are rejected', async () => {
  const kv = new FakeKV();
  const response = await handleHit(post('x'.repeat(600), { 'Content-Length': '600' }), { WAITLIST: kv });
  assert.equal(response.status, 413);
});

test('GET on /api/hit is refused', async () => {
  const response = await handleHit(new Request('https://site.test/api/hit'), { WAITLIST: new FakeKV() });
  assert.equal(response.status, 405);
});

test('without KV the counter says so instead of faking a number', async () => {
  assert.equal((await handleHit(post(''), {})).status, 503);
  assert.equal((await handleStats(new Request('https://site.test/api/stats'), {})).status, 503);
});

test('stats returns aggregate totals only', async () => {
  const kv = new FakeKV({ 'hits:total': '120', 'hits:day:2026-10-24': '9' });
  const response = await handleStats(new Request('https://site.test/api/stats'), { WAITLIST: kv }, new Date('2026-10-24T12:00:00Z'));
  assert.deepEqual(await response.json(), { ok: true, total: 120, today: 9, day: '2026-10-24' });
});
