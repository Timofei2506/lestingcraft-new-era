// Server-authoritative wildlife. No timers, external services or paid storage.
// Only position heartbeats advance AI; both populations and active work are bounded.
import {site} from './story-world.js';
import {sampleLegacy, height, hash} from './terrain-legacy.js';
import {weapon, weapons} from './weapons.js';
import {sampleExpedition} from './terrain-expedition.js';
import {sampleBiomes} from './terrain-biomes.js';
import {sampleNature} from './terrain-nature.js';
export {f, hash, height} from './terrain-legacy.js';

export function sample(x, y, z, seed, edits, contentVersion = 2) {
    const edit = edits.get(`${x},${y},${z}`);
    if (edit) return edit.block;
    if (contentVersion >= 4) {
        const placed = site(x, y, z, seed, contentVersion);
        if (placed !== null) return placed;
    }
    const raw = contentVersion >= 7 ? sampleBiomes(x, y, z, seed, contentVersion)
        : contentVersion >= 3 ? sampleNature(x, y, z, seed)
        : sampleLegacy(x, y, z, seed, edits);
    return contentVersion >= 5 ? sampleExpedition(x, y, z, seed, raw, contentVersion) : raw;
}

const HEALTH = [30, 8, 40, 34, 85, 14, 95, 26, 30, 18, 10, 20, 16, 18, 68, 22, 32, 110, 28, 55];
const HEIGHT = [2.2, .8, .95, 1.25, 1.95, .9, 2.05, 1.65, 1.05, .7, .55, .9, .75, .95, 2.35, .55, .62, 2.45, .82, .85];
const RADIUS = [.43, .21, .45, .38, .72, .27, .76, .32, .34, .35, .38, .36, .30, .28, .58, .36, .40, .74, .36, .55];
const MEAT = [3, 1, 4, 2, 6, 1, 7, 3, 2, 2, 1, 2, 1, 2, 5, 1, 2, 8, 3, 2];
const HIDE = [2, 1, 2, 2, 4, 2, 4, 2, 3, 2, 1, 1, 2, 2, 3, 2, 3, 5, 2, 3];
const ATTACK = [0, 0, 8, 6, 12, 0, 10, 0, 7, 0, 0, 0, 0, 0, 0, 9, 0, 11, 0, 0];
const WALK = [.65, .75, .65, 1.15, .65, 1.25, .55, .9, 1.05, .55, .5, 2.8, 2.4, .62, .72, .95, .35, .60, .58, 2.6];
const RUN = [3.8, 4.2, 3.8, 4.6, 3.3, 4.8, 3.4, 3.6, 4.4, 2.1, 2.7, 4.5, 4.1, 2.4, 3.5, 3.6, 1.2, 3.9, 2.8, 4.6];
const HABITATS = [
    [0, 0, 1, 1, 5, 6, 6, 11],
    [0, 0, 1, 2, 2, 3, 4, 5, 8, 11],
    [0, 1, 3, 3, 4, 4, 6, 8, 11],
    [1, 1, 5, 5, 7, 11],
    [2, 2, 9, 9, 9, 10, 10, 10, 11],
    [1, 3, 7, 7, 7, 8, 11, 11]
];
const HABITATS_V8 = [
    [0, 1, 5, 6, 11, 12, 16, 18],
    [0, 1, 2, 3, 4, 5, 8, 11, 12, 17],
    [0, 1, 3, 4, 6, 8, 12, 13, 13, 17, 17],
    [1, 5, 7, 11, 14, 14, 15, 15],
    [2, 9, 10, 11, 16, 16, 18, 18, 19],
    [1, 3, 7, 8, 11, 12, 15, 17, 19]
];
const EMPTY_EDITS = new Map();
const solid = id => id > 0 && id !== 7 && id !== 8;
const index = s => Number.isInteger(s) && s >= 0 && s < 20 ? s : 2;
const flyingSpecies = s => s === 11 || s === 12 || s === 19;
const health = s => HEALTH[index(s)];
export const animalHeight = s => HEIGHT[index(s)];
const radius = s => RADIUS[index(s)];
const speciesHash = (x, z, seed) => hash(x, 0, z, seed);
const finite = n => Number.isFinite(n) && Math.abs(n) < 32000;
const snapshot = a => ({
    id: a.id, species: a.species, health: a.health,
    x: a.x, y: a.y, z: a.z, yaw: a.yaw, dead: a.dead
});

// Must match WildlifeSimulation.Habitat. Substrate is sampled from the actual
// biome-authored world, including edits. Meadow and forest share grassy ground.
export function wildlifeHabitat(surface, x, z, seed) {
    if (surface === 33 || surface === 34 || surface === 7 || surface === 61) return 4;
    if (surface === 6 || surface === 43 || surface === 62) return 3;
    if (surface === 27 || surface === 32 || surface === 1 || surface === 10 || surface === 31 || surface === 64 || surface === 65) return 5;
    if (surface === 14 || surface === 63) return 2;
    return speciesHash(Math.floor(x / 160), Math.floor(z / 160), seed ^ 81473) % 2;
}

export class Fauna {
    constructor(seed, saved = {}, edits = new Map(), contentVersion = 2) {
        saved = saved || {};
        this.contentVersion = contentVersion;
        this.seed = seed;
        this.edits = edits;
        this.animals = [];
        this.sites = new Set();
        this.loot = [];
        this.lastTick = 0;
        this.lastPersist = 0;
        this.cursor = 0;
        this.blockCache = new Map();
        const ids = new Set();
        for (const key of Array.isArray(saved.sites) ? saved.sites : []) {
            if (this.sites.size >= 4096) break;
            if (typeof key === 'string') this.sites.add(key);
        }
        for (const source of Array.isArray(saved.animals) ? saved.animals : []) {
            if (this.animals.length >= 256) break;
            if (!source || typeof source.id !== 'string' || ids.has(source.id) ||
                !Number.isInteger(source.species) || source.species < 0 ||
                source.species >= (contentVersion >= 8 ? 20 : contentVersion >= 7 ? 12 : 3) ||
                ![source.x, source.y, source.z, source.yaw].every(finite)) continue;
            const a = {...source};
            ids.add(a.id);
            a.health = Math.max(0, Math.min(health(a.species), Number.isFinite(a.health) ? Math.trunc(a.health) : 0));
            a.dead = Boolean(a.dead || a.health === 0);
            a.homeX = finite(a.homeX) ? a.homeX : a.x;
            a.homeZ = finite(a.homeZ) ? a.homeZ : a.z;
            a.goalX = a.x;
            a.goalZ = a.z;
            a.timer = a.anger = a.bite = a.fallSpeed = a.impulseX = a.impulseZ = 0;
            a.wander = Number.isInteger(a.wander) ? a.wander | 0 : 0;
            a.flightPhase = finite(a.flightPhase) ? a.flightPhase : 0;
            a.flightHeight = flyingSpecies(a.species)
                ? finite(a.flightHeight) && a.flightHeight >= 3.5 && a.flightHeight <= 6 ? a.flightHeight : 4.75
                : 0;
            this.animals.push(a);
        }
        const lootIds = new Set();
        for (const l of Array.isArray(saved.loot) ? saved.loot : []) {
            if (this.loot.length >= 256) break;
            if (!l || typeof l.id !== 'string' || typeof l.key !== 'string' || lootIds.has(l.id) ||
                !Number.isInteger(l.meat) || l.meat < 1 || l.meat > 8 ||
                !Number.isInteger(l.hide) || l.hide < 1 || l.hide > 5) continue;
            lootIds.add(l.id);
            this.loot.push({...l});
        }
    }

    block(x, y, z) {
        const key = `${x},${y},${z}`;
        const moving = this.dynamicCells?.get(key);
        if (moving) return moving.block;
        const edit = this.edits.get(key);
        if (edit) return edit.block;
        if (this.blockCache.has(key)) return this.blockCache.get(key);
        if (this.blockCache.size >= 8192) this.blockCache.clear();
        const id = sample(x, y, z, this.seed, EMPTY_EDITS, this.contentVersion);
        this.blockCache.set(key, id);
        return id;
    }

    save() {
        return {
            animals: this.animals.map(a => ({...a})),
            sites: [...this.sites].sort(),
            loot: this.loot.map(l => ({...l}))
        };
    }

    snapshots() { return this.animals.filter(a => !a.dead).map(snapshot); }

    pending(key, claims = []) {
        return this.loot.filter(l => l.key === key && !claims.includes(l.id))
            .map(({id, meat, hide}) => ({id, meat, hide}));
    }

    ground(x, z, previous, s) {
        const r = radius(s), h = animalHeight(s) + (flyingSpecies(s) ? 6 : 0);
        const natural = height(Math.floor(x), Math.floor(z), this.seed) + 1;
        const hi = previous < 0 ? natural + 1 : Math.floor(previous) + 1;
        const lo = previous < 0 ? natural - 2 : Math.floor(previous) - 1;
        const extent = this.contentVersion >= 4 ? 384 : 64;
        for (let y = hi; y >= lo; y--) {
            let valid = true, supported = false;
            for (let k = 0; k < 5 && valid; k++) {
                const ax = Math.floor(x + (k === 0 ? 0 : k % 2 === 0 ? r : -r));
                const az = Math.floor(z + (k === 0 ? 0 : k < 3 ? -r : r));
                if (ax < -extent || ax >= extent || az < -extent || az >= extent) {
                    valid = false; break;
                }
                const below = this.block(ax, y - 1, az), support = solid(below);
                supported ||= support;
                if (!support && (previous < 0 || below === 7 || below === 8 || !solid(this.block(ax, y - 2, az)))) {
                    valid = false; break;
                }
                if (previous < 0 && ![3, 2, 14].includes(below) &&
                    !(this.contentVersion >= 7 && [1, 6, 27, 32, 33, 34, 43].includes(below)) &&
                    !(this.contentVersion >= 8 && [10, 29, 60, 61, 62, 63, 64, 65].includes(below))) {
                    valid = false; break;
                }
                for (let cy = y; cy < Math.ceil(y + h); cy++) {
                    if (this.block(ax, cy, az) !== 0) { valid = false; break; }
                }
            }
            if (valid && supported && y > 41 && y < 100) return y;
        }
        return NaN;
    }

    foot(a, x, z, previous) {
        const offset = flyingSpecies(a.species) ? a.flightHeight : 0;
        let y = this.ground(x, z, previous - offset, a.species);
        if (!Number.isFinite(y) && a.species === 7) y = this.ground(x, z, previous + 1, a.species);
        return Number.isFinite(y) ? y + offset : NaN;
    }

    spawn(p) {
        if (p.alive === false || !finite(p.x) || !finite(p.z)) return;
        const cx = Math.floor(p.x / 24), cz = Math.floor(p.z / 24);
        for (let dz = -2; dz <= 2; dz++) {
            for (let dx = -2; dx <= 2; dx++) {
                const sx = cx + dx, sz = cz + dz, key = `${sx}:${sz}`;
                if (this.sites.has(key) || this.sites.size >= 4096 || this.animals.length >= 256) continue;
                // Immutable legacy hash, positions, rejection and species formula.
                const h = speciesHash(sx, sz, this.seed ^ 2581);
                const x = sx * 24 + 4 + h % 16 + .5, z = sz * 24 + 4 + (h >>> 8) % 16 + .5;
                const d = (x - p.x) ** 2 + (z - p.z) ** 2;
                if (d < 144 || d > 3600) continue;
                this.sites.add(key);
                if (h % 2) continue;
                let species = (h >>> 16) % 3;
                if (this.contentVersion >= 7) {
                    const ix = Math.floor(x), iz = Math.floor(z);
                    const surface = this.block(ix, height(ix, iz, this.seed), iz);
                    const pool = this.contentVersion >= 8 ? HABITATS_V8 : HABITATS;
                    const choices = pool[wildlifeHabitat(surface, ix, iz, this.seed)];
                    species = choices[(h >>> 16) % choices.length];
                }
                let y = this.ground(x, z, -1, species);
                if (!Number.isFinite(y)) continue;
                const flightHeight = flyingSpecies(species) ? 4.75 : 0;
                y += flightHeight;
                this.animals.push({
                    id: 'a:' + key, species, health: health(species), x, y, z, yaw: h % 360,
                    homeX: x, homeZ: z, goalX: x, goalZ: z, timer: 1,
                    anger: 0, bite: 0, wander: 0, impulseX: 0, impulseZ: 0, fallSpeed: 0,
                    flightHeight, flightPhase: 0, dead: false
                });
            }
        }
    }

    tick(now, observers) {
        if (now - this.lastTick < 400) return null;
        let dt = this.lastTick ? Math.max(0, Math.min(.5, (now - this.lastTick) / 1000)) : .25;
        const steps = Math.max(1, Math.ceil(dt / .1));
        dt /= steps;
        this.lastTick = now;
        for (const p of observers) this.spawn(p);
        const damageEvents = [];
        for (let sub = 0; sub < steps; sub++) {
            const count = this.animals.length;
            if (!count) break;
            const start = this.contentVersion >= 7 ? this.cursor % count : 0;
            let active = 0, visited = 0;
            for (; visited < count; visited++) {
                const a = this.animals[(start + visited) % count];
                if (a.dead) continue;
                let nearest = null, best = 3600;
                for (const p of observers) {
                    if (p.alive === false) continue;
                    const d = (a.x - p.x) ** 2 + (a.z - p.z) ** 2;
                    if (d < best) { best = d; nearest = p; }
                }
                if (!nearest) continue;
                if (active++ >= 32) break;
                const s = a.species;
                if (flyingSpecies(s)) {
                    const old = a.flightHeight;
                    a.flightPhase = (a.flightPhase + dt * .8) % (Math.PI * 2);
                    a.flightHeight = 4.75 + 1.25 * Math.sin(a.flightPhase);
                    a.y += a.flightHeight - old;
                }
                if (Math.abs(a.impulseX || 0) + Math.abs(a.impulseZ || 0) > .01) {
                    const nx = a.x + a.impulseX * dt, nz = a.z + a.impulseZ * dt;
                    const ny = this.foot(a, nx, nz, a.y);
                    if (Number.isFinite(ny)) { a.x = nx; a.z = nz; a.y = ny; }
                    a.impulseX *= Math.exp(-dt * 8);
                    a.impulseZ *= Math.exp(-dt * 8);
                }
                const footing = this.foot(a, a.x, a.z, a.y);
                if (!Number.isFinite(footing)) {
                    a.fallSpeed = Math.max(-24, (a.fallSpeed || 0) - dt * 20);
                    let remaining = -a.fallSpeed * dt;
                    while (remaining > 0) {
                        const down = Math.min(.2, remaining);
                        a.y = Math.max(0, a.y - down);
                        remaining -= down;
                        const landed = this.foot(a, a.x, a.z, a.y);
                        if (!Number.isFinite(landed)) continue;
                        a.y = landed;
                        if (a.fallSpeed < -11) a.health = Math.max(0, a.health - Math.trunc((-a.fallSpeed - 11) * 3));
                        a.fallSpeed = 0;
                        break;
                    }
                    if (a.y <= 0 || a.health <= 0) { a.dead = true; a.health = 0; }
                    continue;
                }
                a.fallSpeed = 0;
                a.y = footing;
                a.anger = Math.max(0, (a.anger || 0) - dt);
                a.bite = Math.max(0, (a.bite || 0) - dt);
                a.timer -= dt;
                const hostile = (s === 2 || s === 6 || s === 8 || s === 19) && a.anger > 0 ||
                    (s === 3 || s === 12) && (a.anger > 0 || best < 81) ||
                    (s === 4 || s === 15) && (a.anger > 0 || best < 36);
                const defending = hostile && best < 196 && Math.abs(a.y - nearest.y) < 8;
                const flee = best < (s === 1 ? 100 : 64) && !defending &&
                    Math.abs(a.y - nearest.y) < 8 && !flyingSpecies(s);
                let speed = WALK[s];
                if (defending) {
                    a.goalX = nearest.x; a.goalZ = nearest.z;
                    speed = s === 2 ? 3.1 : RUN[s];
                    if (best < (s === 4 || s === 6 ? 4 : 2.4) &&
                        Math.abs(nearest.y - (a.y + 1.5)) < 2.3 && a.bite <= 0 &&
                        this.lineClear(nearest.x, nearest.y, nearest.z, a)) {
                        damageEvents.push({id: nearest.id, seq: now * 64 + damageEvents.length, amount: ATTACK[s], part: 4});
                        a.bite = 1.5;
                    }
                } else if (flee) {
                    const inv = 1 / Math.sqrt(Math.max(.01, best));
                    a.goalX = a.x + (a.x - nearest.x) * inv * 10;
                    a.goalZ = a.z + (a.z - nearest.z) * inv * 10;
                    speed = RUN[s];
                } else if (a.timer <= 0) {
                    a.wander = ((a.wander || 0) + 1) | 0;
                    const h = speciesHash(Math.trunc(a.x * 7 + a.yaw), Math.trunc(a.z * 7),
                        this.seed ^ a.health ^ Math.imul(a.wander, 1597334677));
                    const angle = h % 6283 * .001, range = 2 + (h >>> 8) % 9 + (s === 11 ? 8 : 0);
                    a.goalX = a.homeX + Math.sin(angle) * range;
                    a.goalZ = a.homeZ + Math.cos(angle) * range;
                    a.timer = 3 + (h >>> 16) % 5;
                }
                const vx = a.goalX - a.x, vz = a.goalZ - a.z, len = Math.hypot(vx, vz);
                if (len < .25) continue;
                const step = Math.min(len, speed * dt);
                let nx = a.x + vx / len * step, nz = a.z + vz / len * step;
                let ny = this.foot(a, nx, nz, a.y);
                if (!Number.isFinite(ny)) {
                    for (let turn = 0; turn < 4; turn++) {
                        const angle = (turn % 2 === 0 ? 1 : -1) * (turn < 2 ? .8 : 1.55);
                        const rx = vx * Math.cos(angle) - vz * Math.sin(angle);
                        const rz = vx * Math.sin(angle) + vz * Math.cos(angle);
                        nx = a.x + rx / len * step; nz = a.z + rz / len * step;
                        ny = this.foot(a, nx, nz, a.y);
                        if (Number.isFinite(ny)) break;
                    }
                }
                if (!Number.isFinite(ny)) { a.timer = 0; a.goalX = a.x; a.goalZ = a.z; continue; }
                a.yaw = Math.atan2(nx - a.x, nz - a.z) * 180 / Math.PI;
                a.x = nx; a.z = nz; a.y = ny;
            }
            if (this.contentVersion >= 7) this.cursor = (start + Math.max(1, visited)) % count;
        }
        return {animals: this.snapshots(), damageEvents};
    }

    lineClear(x, y, z, a) {
        const dx = a.x - x, dy = a.y + animalHeight(a.species) * .5 - y, dz = a.z - z;
        const n = Math.ceil(Math.hypot(dx, dy, dz) / .12);
        for (let i = 1; i < n; i++) {
            const t = i / n;
            if (solid(this.block(Math.floor(x + dx * t), Math.floor(y + dy * t), Math.floor(z + dz * t)))) return false;
        }
        return true;
    }

    hit(peer, id, weaponId, now, creative, requestId = 0) {
        if (!peer.positioned || weaponId !== 164 && now - (peer.lastAnimalHit || 0) < weapon(weaponId).cooldown)
            throw Error('Animal attack cooldown / position required');
        if (![0, 100, 101, 102, 103].includes(weaponId) && !(this.contentVersion >= 5 && weapons[weaponId]) ||
            typeof id !== 'string') throw Error('Invalid animal attack');
        const a = this.animals.find(animal => animal.id === id && !animal.dead);
        if (!a) throw Error('Animal no longer available');
        const origin = weaponId === 164 ? peer.bowShots?.[requestId] : peer;
        if (!origin || weaponId === 164 && origin.until < now) throw Error('Bow shot expired / already consumed');
        const dy = a.y + animalHeight(a.species) * .5 - origin.y;
        if ((a.x - origin.x) ** 2 + dy * dy + (a.z - origin.z) ** 2 > weapon(weaponId).reach ** 2 ||
            !this.lineClear(origin.x, origin.y, origin.z, a)) throw Error('Animal out of reach or behind a block');
        const push = Math.hypot(a.x - origin.x, a.z - origin.z);
        if (push > .01) {
            a.impulseX = (a.x - origin.x) / push * 3.5;
            a.impulseZ = (a.z - origin.z) / push * 3.5;
        }
        a.health = Math.max(0, a.health - weapon(weaponId).damage);
        a.anger = 8; a.timer = 0;
        let loot = [];
        if (a.health === 0) {
            a.dead = true;
            if (!creative) {
                const l = {id: a.id, key: peer.playerKey, meat: MEAT[a.species], hide: HIDE[a.species]};
                this.loot.push(l);
                loot = [{id: l.id, meat: l.meat, hide: l.hide}];
            }
        }
        return {type: 'animal_hit', id: peer.id, requestId, weapon: weaponId, animals: this.snapshots(), loot};
    }
}
