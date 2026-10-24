// World bosses for LestingCraft: New Era (content 10+).
// Deterministic arenas, bounded active set, no timers outside the tick that
// already exists for wildlife. A boss is a room entity, not a scripted event:
// it spawns when players come close, fights with phases, dies once and leaves
// world drops in the living layer.
import {hash, height} from './terrain-legacy.js';

export const BOSS_KINDS = [
    {name: 'Тлеющий колосс', health: 900, height: 4.2, radius: 1.6, damage: [12, 18, 26], speed: 1.9, reach: 5.5, cooldown: 2200, aggro: 30, phaseShield: 3000, loot: [{item: 70, count: 6}, {item: 135, count: 8}]},
    {name: 'Ледяная матрона', health: 760, height: 3.6, radius: 1.3, damage: [10, 16, 22], speed: 2.4, reach: 4.8, cooldown: 1900, aggro: 34, phaseShield: 2600, loot: [{item: 54, count: 5}, {item: 136, count: 6}]},
    {name: 'Курганный владыка', health: 1180, height: 4.8, radius: 1.8, damage: [14, 21, 30], speed: 1.5, reach: 6.2, cooldown: 2500, aggro: 28, phaseShield: 3400, loot: [{item: 47, count: 3}, {item: 138, count: 6}]}
];

export const MAX_ACTIVE = 3;
const SITE_CELL = 160;
const WORLD_BOUND = 380; // rooms are limited to +-384 blocks
const SPAWN_DISTANCE = 96;   // players closer than this wake the boss
const SLEEP_DISTANCE = 190;  // every player further than this and it sleeps
const SLEEP_DELAY = 90000;   // ...after this long without contact
const REPEAT_SUPPRESS = 1800000; // a defeated boss stays down for 30 minutes

const finite = n => Number.isFinite(n) && Math.abs(n) < 32000;
const siteHash = (cx, cz, seed, salt) => hash(cx, salt, cz, seed);

// Arena centres on a 384-block grid, rotated away from the spawn point so the
// first boss is never on top of the player. Same seed => same arenas forever.
export function bossSites(seed, gridRadius = 4) {
    const sites = [];
    for (let cz = -gridRadius; cz <= gridRadius; cz++) {
        for (let cx = -gridRadius; cx <= gridRadius; cx++) {
            const h = siteHash(cx, cz, seed, 90421);
            if (h % 3 !== 0) continue;
            const x = cx * SITE_CELL + 48 + h % 96;
            const z = cz * SITE_CELL + 48 + (h >>> 8) % 96;
            const kind = (h >>> 16) % BOSS_KINDS.length;
            const y = height(x, z, seed) + 1;
            if (!Number.isFinite(y) || y < 42 || y > 110) continue;
            // Rooms end at +-384 blocks and the spawn point must stay safe.
            if (Math.abs(x) > WORLD_BOUND || Math.abs(z) > WORLD_BOUND) continue;
            if (x * x + z * z < 150 * 150) continue;
            sites.push({id: `b:${cx}:${cz}`, kind, x: x + .5, y, z: z + .5});
        }
    }
    return sites.sort((a, b) => a.id < b.id ? -1 : 1);
}

const phaseOf = (boss, kind) => boss.health > kind.health * 2 / 3 ? 1 : boss.health > kind.health / 3 ? 2 : 3;
const snapshot = b => ({id: b.id, kind: b.kind, name: BOSS_KINDS[b.kind].name, health: b.health,
    maxHealth: BOSS_KINDS[b.kind].health, phase: b.phase, shielded: b.shieldUntil > b.now,
    x: b.x, y: b.y, z: b.z, yaw: b.yaw, dead: b.dead});

export class WorldBosses {
    constructor(seed, saved = {}, contentVersion = 10, edits = new Map()) {
        this.seed = seed;
        this.contentVersion = contentVersion;
        this.enabled = contentVersion >= 10;
        this.edits = edits;
        this.sites = this.enabled ? bossSites(seed) : [];
        this.active = [];
        this.defeated = new Map();
        this.lastTick = 0;
        this.now = 0;
        const store = saved || {};
        for (const entry of Array.isArray(store.defeated) ? store.defeated.slice(0, 64) : []) {
            // Timestamps are epoch milliseconds, far outside the coordinate bound.
            if (!entry || typeof entry.id !== 'string' || !Number.isFinite(entry.at) || entry.at < 0) continue;
            this.defeated.set(entry.id, entry.at);
        }
        // A boss that was alive in a saved room keeps its health across restarts.
        for (const entry of Array.isArray(store.active) ? store.active.slice(0, MAX_ACTIVE) : []) {
            if (!entry || typeof entry.id !== 'string' || !Number.isInteger(entry.kind)) continue;
            const kind = BOSS_KINDS[entry.kind];
            if (!kind || !finite(entry.x) || !finite(entry.y) || !finite(entry.z)) continue;
            this.active.push({
                id: entry.id, kind: entry.kind, x: entry.x, y: entry.y, z: entry.z, yaw: 0,
                health: Math.max(0, Math.min(kind.health, Number.isInteger(entry.health) ? entry.health : kind.health)),
                phase: 1, shieldUntil: 0, lastAttack: 0, lastContact: 0, homeY: entry.y, dead: false
            });
        }
        for (const boss of this.active) boss.phase = phaseOf(boss, BOSS_KINDS[boss.kind]);
    }

    save() {
        return {
            defeated: [...this.defeated.entries()].slice(-64).map(([id, at]) => ({id, at})),
            active: this.active.filter(b => !b.dead).map(b => ({id: b.id, kind: b.kind, x: b.x, y: b.y, z: b.z, health: b.health}))
        };
    }

    snapshots() {
        return this.enabled ? this.active.filter(b => !b.dead).map(b => snapshot({...b, now: this.now})) : [];
    }

    // Ground level for a boss: the terrain surface, unless a player dug it out.
    groundAt(x, z, kind, fallback) {
        const ix = Math.floor(x), iz = Math.floor(z);
        const edited = this.edits.get(`${ix},${Math.floor(fallback)},${iz}`);
        if (edited && edited.block === 0) return fallback;
        const surface = height(ix, iz, this.seed) + 1;
        return Number.isFinite(surface) ? Math.max(fallback - 3, Math.min(fallback + 3, surface)) : fallback;
    }

    admit(now, observers) {
        if (!this.enabled) return;
        for (const [id, at] of this.defeated) if (now - at > REPEAT_SUPPRESS) this.defeated.delete(id);
        if (this.active.length >= MAX_ACTIVE) return;
        const live = this.active.map(b => b.id);
        for (const site of this.sites) {
            if (this.active.length >= MAX_ACTIVE) break;
            if (live.includes(site.id) || this.defeated.has(site.id)) continue;
            const near = observers.some(p => (p.x - site.x) ** 2 + (p.z - site.z) ** 2 < SPAWN_DISTANCE ** 2);
            if (!near) continue;
            const kind = BOSS_KINDS[site.kind];
            this.active.push({
                id: site.id, kind: site.kind, x: site.x, y: site.y, z: site.z, yaw: 0,
                health: kind.health, phase: 1, shieldUntil: 0, lastAttack: 0, lastContact: now,
                homeY: site.y, dead: false
            });
        }
        this.active = this.active.filter(b => !b.dead);
    }

    tick(now, observers, creative = false) {
        this.now = now;
        if (!this.enabled) return null;
        if (now - this.lastTick < 350) return null;
        const dt = this.lastTick ? Math.max(0, Math.min(.5, (now - this.lastTick) / 1000)) : .25;
        this.lastTick = now;
        this.admit(now, observers);

        const damageEvents = [];
        const defeated = [];
        for (const boss of this.active) {
            if (boss.dead) continue;
            const kind = BOSS_KINDS[boss.kind];
            let nearest = null, best = Infinity;
            for (const p of observers) {
                if (p.alive === false) continue;
                const d = (p.x - boss.x) ** 2 + (p.z - boss.z) ** 2;
                if (d < best) { best = d; nearest = p; }
            }
            if (!nearest || best > SLEEP_DISTANCE ** 2) {
                if (now - (boss.lastContact || 0) > SLEEP_DELAY) { boss.dead = true; continue; }
                continue;
            }
            boss.lastContact = now;
            const reachable = Math.abs(nearest.y - boss.y) < kind.height + 4;
            if (!reachable) continue;
            const dist = Math.sqrt(best);
            const attackRange = kind.reach + kind.radius;

            if (best > (attackRange + 1) ** 2 && dist > 1.2) {
                const step = Math.min(dist - attackRange, kind.speed * (1 + (boss.phase - 1) * .18) * dt);
                const nx = boss.x + (nearest.x - boss.x) / dist * step;
                const nz = boss.z + (nearest.z - boss.z) / dist * step;
                boss.x = nx; boss.z = nz;
                boss.y = this.groundAt(nx, nz, boss.kind, boss.homeY);
                boss.yaw = Math.atan2(nearest.x - boss.x, nearest.z - boss.z) * 180 / Math.PI;
            } else if (best <= attackRange ** 2 && now - boss.lastAttack >= kind.cooldown && now >= boss.shieldUntil) {
                boss.lastAttack = now;
                damageEvents.push({
                    id: nearest.id,
                    seq: now * 64 + damageEvents.length,
                    amount: kind.damage[boss.phase - 1],
                    part: 4,
                    boss: boss.id
                });
            }
        }
        this.active = this.active.filter(b => !b.dead);
        return {bosses: this.snapshots(), damageEvents, defeated};
    }

    hit(peer, id, weaponId, damage, now, reach, creative = false, lineClear = () => true) {
        if (!this.enabled) throw Error('World bosses are unavailable in this world');
        const boss = this.active.find(b => b.id === id && !b.dead);
        if (!boss) throw Error('Boss is no longer here');
        if (!peer.positioned) throw Error('Position required');
        const kind = BOSS_KINDS[boss.kind];
        const dy = boss.y + kind.height * .5 - (peer.y + 1.62);
        const distance = Math.hypot(boss.x - peer.x, dy, boss.z - peer.z);
        if (distance > reach + kind.radius) throw Error('Boss out of reach');
        if (!lineClear(peer.x, peer.y + 1.62, peer.z, boss)) throw Error('Boss behind a block');
        if (now < boss.shieldUntil) return {type: 'boss_hit', id: peer.id, boss: snapshot({...boss, now}), blocked: true, damage: 0};

        const previous = boss.phase;
        boss.health = Math.max(0, boss.health - Math.max(1, Math.trunc(damage)));
        boss.phase = phaseOf(boss, kind);
        if (boss.phase !== previous) boss.shieldUntil = now + kind.phaseShield;

        let loot = [];
        if (boss.health === 0) {
            boss.dead = true;
            this.defeated.set(boss.id, now);
            if (creative) loot = [];
            else loot = kind.loot.map(entry => ({...entry}));
        }
        return {
            type: 'boss_hit', id: peer.id, boss: snapshot({...boss, now}), damage: Math.max(1, Math.trunc(damage)),
            defeated: boss.dead, loot
        };
    }

    // Finds the boss a peer is aiming at, used by the server packet handler.
    nearest(x, y, z, reach) {
        let best = null, bestDistance = reach;
        for (const boss of this.active) {
            if (boss.dead) continue;
            const kind = BOSS_KINDS[boss.kind];
            const d = Math.hypot(boss.x - x, boss.y + kind.height * .5 - y, boss.z - z) - kind.radius;
            if (d < bestDistance) { bestDistance = d; best = boss; }
        }
        return best;
    }
}
