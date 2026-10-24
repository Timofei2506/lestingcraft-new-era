// Public server directory: the Minecraft style "address" a player types in is
// the endpoint (IP or domain plus ports). Every hosted world published on that
// endpoint is listed here, so a player can pick a server by name instead of
// copying a 32 character room code.
export const SERVER_NAME_MAX = 32;
export const SERVER_NAME_MIN = 2;
export const MAX_SERVERS = 40;

const CONTROL = /[\u0000-\u001f\u007f]/g;

/** Player facing server name: printable, single spaced and short. */
export function serverName(value, fallback = 'Новый мир') {
  const raw = typeof value === 'string' ? value : '';
  const clean = raw.replace(CONTROL, ' ').replace(/\s+/g, ' ').trim();
  const name = clean.length ? clean : fallback;
  if ([...name].length < SERVER_NAME_MIN || [...name].length > SERVER_NAME_MAX)
    throw new Error(`Server name must be ${SERVER_NAME_MIN}-${SERVER_NAME_MAX} characters`);
  return name;
}

/** Shape stored next to the room code. */
export function serverRecord(config, name, now = Date.now(), owner = null) {
  return {
    name: serverName(name, 'Мир'),
    mode: config.mode,
    seed: config.seed,
    dimension: config.dimension,
    rulesVersion: config.rulesVersion,
    contentVersion: config.contentVersion,
    createdAt: now,
    owner: owner || null,
  };
}

/** One row of GET /api/servers. `status` comes from the room itself. */
export function publicServer(code, record, status = null) {
  const live = status && typeof status === 'object' ? status : {};
  return {
    code,
    name: record.name,
    mode: record.mode ?? 0,
    seed: record.seed ?? 0,
    dimension: record.dimension ?? 0,
    rulesVersion: record.rulesVersion ?? 0,
    contentVersion: record.contentVersion ?? 0,
    createdAt: record.createdAt ?? 0,
    players: Number.isFinite(live.players) ? Math.max(0, live.players) : 0,
    maxPlayers: Number.isFinite(live.maxPlayers) ? live.maxPlayers : 8,
    started: live.started === true,
    offline: status === null,
  };
}

/** Sort: worlds with people inside first, then newest. */
export function sortServers(list) {
  return [...list].sort((a, b) => (b.players - a.players) || (b.createdAt - a.createdAt));
}

/**
 * Directory storage that both the standalone node server and the worker can
 * share. `entries` is an ordered list so eviction (oldest first) is stable.
 */
export class ServerDirectory {
  constructor(entries = []) {
    this.entries = entries.filter((e) => e && typeof e.code === 'string' && e.code.length === 32);
  }
  get(code) { return this.entries.find((e) => e.code === code) || null; }
  add(code, record) {
    this.entries = this.entries.filter((e) => e.code !== code);
    this.entries.push({ code, ...record });
    while (this.entries.length > MAX_SERVERS) this.entries.shift();
    return this.get(code);
  }
  remove(code, owner) {
    const entry = this.get(code);
    if (!entry) return false;
    if (entry.owner && owner && entry.owner !== owner) throw new Error('Only the creator can remove this server');
    this.entries = this.entries.filter((e) => e.code !== code);
    return true;
  }
  toJSON() { return this.entries.map((e) => ({ ...e })); }
}
