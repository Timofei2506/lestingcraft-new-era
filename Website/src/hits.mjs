// Honest, cookie-free visit counter.
// The site has no third-party analytics, so without this there is no way to tell
// "nobody came" from "people came and did not download". We store only aggregate
// counters in KV: no IP, no user agent, no cookies, no per-visitor keys.
// One page view = one POST /api/hit; repeat views from the same tab within the
// same day are counted by the client only once (sessionStorage).
const HEADERS = {'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff'};
const json = (status, body) => Response.json(body, {status, headers: HEADERS});
const MAX_BODY = 512;

export function dayKey(date = new Date()) {
  return date.toISOString().slice(0, 10);
}

export function sanitizeSource(value) {
  const allowed = new Set(['', 'direct', 'github', 'share', 'search', 'friend', 'press']);
  const source = String(value ?? '').toLowerCase().replace(/[^a-z_-]/g, '').slice(0, 16);
  return allowed.has(source) ? source : '';
}

export async function handleHit(request, env = {}, now = new Date()) {
  if (request.method !== 'POST') return json(405, {ok: false, message: 'Метод не поддерживается.'});
  const length = Number(request.headers.get('Content-Length') || 0);
  if (length > MAX_BODY) return json(413, {ok: false, message: 'Слишком большой запрос.'});
  let source = '';
  try {
    const raw = await request.text();
    if (raw) source = sanitizeSource(JSON.parse(raw)?.source);
  } catch { source = ''; }

  if (!env.WAITLIST) return json(503, {ok: false, message: 'Счётчик не подключён.'});

  // KV has no atomic increment, so counters are best-effort: a lost update under
  // heavy concurrency costs one visit, it never inflates the number. Reads happen
  // with a short cache so a popular page does not hammer one key.
  const day = dayKey(now);
  const keys = {
    [`hits:day:${day}`]: 1,
    'hits:total': 1,
  };
  if (source) keys[`hits:src:${source}`] = 1;
  await Promise.all(Object.keys(keys).map(async key => {
    const current = Number(await env.WAITLIST.get(key)) || 0;
    await env.WAITLIST.put(key, String(current + 1));
  }));
  return json(200, {ok: true});
}

export async function handleStats(request, env = {}, now = new Date()) {
  if (request.method !== 'GET') return json(405, {ok: false, message: 'Метод не поддерживается.'});
  if (!env.WAITLIST) return json(503, {ok: false, message: 'Счётчик не подключён.'});
  const day = dayKey(now);
  const [total, today] = await Promise.all([
    env.WAITLIST.get('hits:total'),
    env.WAITLIST.get(`hits:day:${day}`),
  ]);
  return json(200, {ok: true, total: Number(total) || 0, today: Number(today) || 0, day});
}
