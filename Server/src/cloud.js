// Supabase bridge for the game servers (Cloudflare Worker and standalone Node).
//
// The game client never sees Supabase and never holds a key: it talks to the
// game server with its account token, the server resolves the token to a
// player_key and calls a service_role-only RPC. Every RPC is guarded in SQL by
// assert_service_role(), so a leaked client cannot reach the economy at all.

/** Messages the database is allowed to surface to a player, already localized. */
export const SUPABASE_MESSAGES = [
  [/^insufficient coins/, 'Не хватает монет.'],
  [/^already owned/, 'Этот предмет уже куплен.'],
  [/^unknown item/, 'Такого предмета нет в магазине.'],
  [/^skin not owned/, 'Сначала купите этот скин.'],
  [/^cannot buy your own listing/, 'Нельзя купить собственный лот.'],
  [/^listing is not active/, 'Лот больше не продаётся.'],
  [/^this item is already listed/, 'Этот предмет уже выставлен на продажу.'],
  [/^item not owned/, 'Этого предмета нет в инвентаре.'],
  [/^trade blocked/, 'Обмен с этим игроком заблокирован.'],
  [/^request not found/, 'Заявка не найдена.'],
  [/^price must be positive/, 'Цена должна быть больше нуля.'],
  [/^unknown profile/, 'Профиль не найден.'],
];

export function explain(message) {
  const text = String(message ?? '');
  for (const [pattern, russian] of SUPABASE_MESSAGES) if (pattern.test(text)) return russian;
  return null;
}

export class CloudError extends Error {
  constructor(message, status = 400, code = 'cloud') { super(message); this.status = status; this.code = code; }
}

/**
 * Thin REST client for the svc_* RPCs. `fetchImpl` is injected so tests can run
 * without the network; `key` stays inside this object.
 */
export class CloudService {
  constructor(url, key, fetchImpl = globalThis.fetch) {
    if (!url || !key) throw new CloudError('Cloud is not configured', 503, 'not_configured');
    this.base = String(url).replace(/\/+$/, '');
    this.key = key;
    // Wrapped in an arrow on purpose: a Worker's global fetch throws
    // "Illegal invocation" when it is called with any other receiver.
    this.fetch = (target, init) => fetchImpl(target, init);
  }

  async rpc(name, args = {}) {
    let response;
    try {
      response = await this.fetch(`${this.base}/rest/v1/rpc/${name}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          apikey: this.key,
          Authorization: `Bearer ${this.key}`,
          Prefer: 'return=representation',
        },
        body: JSON.stringify(args),
      });
    } catch (error) {
      // The message is deliberately generic for players; the code tells the
      // server operator what actually broke.
      throw new CloudError('Сервис экономики недоступен. Попробуйте позже.', 503, `unreachable:${error?.message ?? error}`);
    }
    const text = await response.text();
    let payload = null;
    try { payload = text ? JSON.parse(text) : null; } catch { payload = null; }
    if (!response.ok) {
      const raw = payload?.message ?? text ?? '';
      const friendly = explain(raw);
      // Internal SQL errors are logged nowhere near the client: only known,
      // player-facing messages are relayed.
      throw new CloudError(friendly || 'Сервис экономики недоступен. Попробуйте позже.', friendly ? 400 : 502, 'supabase');
    }
    return payload;
  }

  state(playerKey) { return this.rpc('svc_state', { p_player_key: playerKey }); }
  buy(playerKey, code) { return this.rpc('svc_store_buy', { p_player_key: playerKey, p_item_code: code }); }
  equip(playerKey, code) { return this.rpc('svc_equip_skin', { p_player_key: playerKey, p_item_code: code }); }
  board(playerKey) { return this.rpc('svc_market_board', { p_player_key: playerKey }); }
  list(playerKey, code, price) { return this.rpc('svc_market_list', { p_player_key: playerKey, p_item_code: code, p_price: price }); }
  cancel(playerKey, id) { return this.rpc('svc_market_cancel', { p_player_key: playerKey, p_listing: id }); }
  purchase(playerKey, id) { return this.rpc('svc_market_buy', { p_player_key: playerKey, p_listing: id }); }
  friends(playerKey) { return this.rpc('svc_friends', { p_player_key: playerKey }); }
  friendRequest(playerKey, target, message) { return this.rpc('svc_friend_request', { p_player_key: playerKey, p_target_key: target, p_message: message ?? null }); }
  friendAccept(playerKey, id) { return this.rpc('svc_friend_accept', { p_player_key: playerKey, p_request: id }); }
  friendDecline(playerKey, id) { return this.rpc('svc_friend_decline', { p_player_key: playerKey, p_request: id }); }
  friendRemove(playerKey, target) { return this.rpc('svc_friend_remove', { p_player_key: playerKey, p_other_key: target }); }
}

const KEY = /^[a-f0-9]{32}$/;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const CODE = /^[a-z0-9_]{2,40}$/;

/** Validated action table. Anything not listed here is a 404. */
export const CLOUD_ACTIONS = {
  state: (s, p) => s.state(p),
  buy: (s, p, b) => { if (!CODE.test(b?.code ?? '')) throw new CloudError('Неизвестный предмет', 400, 'item'); return s.buy(p, b.code); },
  equip: (s, p, b) => { if (!CODE.test(b?.code ?? '')) throw new CloudError('Неизвестный скин', 400, 'item'); return s.equip(p, b.code); },
  market: (s, p) => s.board(p),
  list: (s, p, b) => {
    if (!CODE.test(b?.code ?? '')) throw new CloudError('Неизвестный предмет', 400, 'item');
    const price = Number(b?.price);
    if (!Number.isSafeInteger(price) || price <= 0 || price > 1_000_000) throw new CloudError('Цена должна быть от 1 до 1000000 монет', 400, 'price');
    return s.list(p, b.code, price);
  },
  cancel: (s, p, b) => { if (!UUID.test(b?.id ?? '')) throw new CloudError('Лот не найден', 400, 'listing'); return s.cancel(p, b.id); },
  purchase: (s, p, b) => { if (!UUID.test(b?.id ?? '')) throw new CloudError('Лот не найден', 400, 'listing'); return s.purchase(p, b.id); },
  friends: (s, p) => s.friends(p),
  'friend/request': (s, p, b) => { if (!KEY.test(b?.key ?? '')) throw new CloudError('Нужен ключ игрока из 32 символов', 400, 'key'); return s.friendRequest(p, b.key, typeof b?.message === 'string' ? b.message : null); },
  'friend/accept': (s, p, b) => { if (!Number.isSafeInteger(b?.id) || b.id <= 0) throw new CloudError('Заявка не найдена', 400, 'request'); return s.friendAccept(p, b.id); },
  'friend/decline': (s, p, b) => { if (!Number.isSafeInteger(b?.id) || b.id <= 0) throw new CloudError('Заявка не найдена', 400, 'request'); return s.friendDecline(p, b.id); },
  'friend/remove': (s, p, b) => { if (!KEY.test(b?.key ?? '')) throw new CloudError('Нужен ключ игрока из 32 символов', 400, 'key'); return s.friendRemove(p, b.key); },
};

/**
 * POST /api/cloud/<action>, authenticated by the game account token.
 * `resolvePlayer(token)` returns a 32 hex player_key or null.
 */
export async function handleCloudRequest(request, { service, resolvePlayer, cors = {} }) {
  const json = (data, status = 200) => new Response(JSON.stringify(data), {
    status, headers: { ...cors, 'Content-Type': 'application/json', 'Cache-Control': 'no-store' },
  });
  const url = new URL(request.url);
  const match = url.pathname.match(/^\/api\/cloud\/([a-z/]+)$/);
  if (!match) return json({ ok: false, message: 'Not found' }, 404);
  const action = CLOUD_ACTIONS[match[1]];
  if (!action) return json({ ok: false, message: 'Not found' }, 404);
  if (request.method !== 'POST') return json({ ok: false, message: 'Method not allowed' }, 405);

  const token = (request.headers.get('Authorization') || '').replace(/^Bearer /i, '');
  let playerKey = null;
  try { playerKey = await resolvePlayer(token); } catch { playerKey = null; }
  if (!KEY.test(playerKey ?? '')) return json({ ok: false, message: 'Войдите в аккаунт, чтобы открыть кошелёк и друзей.' }, 401);

  let body = {};
  if (request.body) {
    const text = await request.text();
    if (text.length > 2048) return json({ ok: false, message: 'Body too large' }, 413);
    if (text) { try { body = JSON.parse(text); } catch { return json({ ok: false, message: 'Неверный запрос' }, 400); } }
  }

  try {
    const result = await action(service, playerKey, body);
    // `ok` is written last: a database payload must never be able to mask it.
    return json({ ...(result && typeof result === 'object' ? result : { value: result }), ok: true });
  } catch (error) {
    const status = error instanceof CloudError ? error.status : 502;
    const message = error instanceof CloudError ? error.message : 'Сервис экономики недоступен. Попробуйте позже.';
    // "code" never contains player data: it is a short operator-facing tag.
    return json({ ok: false, message, code: error instanceof CloudError ? error.code : `internal:${error?.name ?? 'Error'}` }, status);
  }
}
