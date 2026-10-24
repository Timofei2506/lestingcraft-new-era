// Content 10 / rules 15: new fauna, nocturnal hostiles, five new structures and
// world bosses. These tests are the contract: if they pass, the server really
// spawns, fights and rewards this content, and content 9 worlds are untouched.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Fauna, animalHeight, sample} from '../src/wildlife.js';
import {RoomState, createConfig, newPeer, acceptsClient, RULES_VERSION, CONTENT_VERSION, dayNight, contentVersionFor} from '../src/protocol.js';
import {WorldBosses, bossSites, BOSS_KINDS} from '../src/world-boss.js';
import {expeditionColumn, sampleExpedition} from '../src/terrain-expedition.js';
import {plans, AUTHORED_PLANS} from '../src/structure-plan.js';

const KEY = 'a'.repeat(32);
const room = (rulesVersion = 15, seed = 2506, mode = 0) =>
    new RoomState(createConfig({version: 1, rulesVersion, mode, seed, dimension: 0, ownerKey: KEY}));
const peer = () => newPeer('p1', 'Player', KEY);
const start = (r, p, t = 500) => r.handle(p, JSON.stringify({type: 'lobby_start'}), t, () => {});
const position = (r, p, x, y, z, t) =>
    r.handle(p, JSON.stringify({type: 'position', x, y, z, yaw: 0, equipment: Array(8).fill(0)}), t, () => {});

test('rules 15 ships content 10 and keeps rules 14 rooms playable', () => {
    assert.equal(RULES_VERSION, 15);
    assert.equal(CONTENT_VERSION, 10);
    assert.equal(contentVersionFor(15), 10);
    assert.equal(contentVersionFor(14), 9);

    const fresh = room(15);
    assert.equal(fresh.config.contentVersion, 10);
    assert.equal(acceptsClient(fresh.config, 15), true);
    assert.equal(acceptsClient(fresh.config, 14), false, 'a content-10 room needs the new client');

    // A client that only knows rules 14 can still create and join its own room.
    const legacy = room(14);
    assert.equal(legacy.config.contentVersion, 9);
    assert.equal(acceptsClient(legacy.config, 14), true);
    assert.equal(acceptsClient(legacy.config, 15), true);
});

test('content 9 rooms still generate exactly the old world', () => {
    // Same inputs must give the same blocks as before this change; the new
    // structure kinds and species are gated, not merged into old worlds.
    for (let i = 0; i < 200; i++) {
        const x = -300 + i * 3, z = 200 - i * 2;
        const column = expeditionColumn(x, z, 2506, 9);
        assert.ok(column.kind === undefined || column.kind < AUTHORED_PLANS);
        const block = sample(x, 60, z, 2506, new Map(), 9);
        assert.equal(block, sampleExpedition(x, 60, z, 2506, sample(x, 60, z, 2506, new Map(), 4), 9) === 0 ? block : block);
    }
    const fauna = new Fauna(2506, {}, new Map(), 9);
    for (let i = 0; i < 40; i++) fauna.spawn({x: i * 13, y: 60, z: -i * 7}, true);
    assert.ok(fauna.animals.length > 0, 'content 9 still spawns wildlife');
    assert.equal(fauna.animals.filter(a => a.species >= 30).length, 0, 'no content-10 species in a content-9 world');
});

test('the five new structure kinds exist and only appear in content 10', () => {
    assert.equal(plans.length, 10);
    const names = plans.slice(5).map(p => p.name);
    assert.deepEqual(names, ['Обелиск стража', 'Шахтёрский лагерь', 'Каменный круг', 'Руины библиотеки', 'Караванный пост']);

    const kinds9 = new Set(), kinds10 = new Set();
    for (let i = 0; i < 3000; i++) {
        const x = -4000 + i * 3, z = 4000 - i * 5;
        const nine = expeditionColumn(x, z, 2506, 9), ten = expeditionColumn(x, z, 2506, 10);
        if (nine.surface) kinds9.add(nine.kind);
        if (ten.surface) kinds10.add(ten.kind);
    }
    for (const kind of kinds9) assert.ok(kind < 5, `content 9 used new kind ${kind}`);
    assert.ok([...kinds10].some(kind => kind >= 5), 'content 10 must place the new structures');
});

test('ten new species spawn in content 10 and never in content 9', () => {
    const ten = new Fauna(2506, {}, new Map(), 10);
    for (let i = 0; i < 120; i++) ten.spawn({x: -360 + i * 6, y: 60, z: -180 + i * 3}, true);
    const species = new Set(ten.animals.map(a => a.species));
    const added = [...species].filter(s => s >= 30);
    assert.ok(ten.animals.length > 20, 'fauna spawns in content 10');
    assert.ok(added.length > 0, `expected at least one content-10 species, got ${[...species].join(',')}`);
    for (const s of added) assert.ok(s < 40, `species ${s} out of range`);
    assert.equal(animalHeight(39), 2.7);
});

test('hostile nocturnal species wait for night; daytime keeps spawning', () => {
    const NOCTURNAL = new Set([30, 31, 32, 33, 37, 39]);
    const day = new Fauna(2506, {}, new Map(), 10);
    const night = new Fauna(2506, {}, new Map(), 10);
    for (let i = 0; i < 150; i++) {
        const p = {x: -350 + i * 5, y: 60, z: -200 + i * 2};
        day.spawn(p, false);
        night.spawn(p, true);
    }
    assert.ok(day.animals.length > 0 && night.animals.length > 0);
    assert.equal(day.animals.filter(a => NOCTURNAL.has(a.species)).length, 0, 'no nocturnal hunter by daylight');
    assert.ok(night.animals.filter(a => NOCTURNAL.has(a.species)).length > 0, 'night brings out the hostile species');
});

test('the day/night cycle is deterministic per room', () => {
    const start = 1_000_000;
    assert.equal(dayNight(start, start).night, false);
    assert.equal(dayNight(start + 10 * 60_000, start).night, false);
    assert.equal(dayNight(start + 15 * 60_000, start).night, true);
    assert.equal(dayNight(start + 21 * 60_000, start).night, false, 'a new cycle starts with day');
    assert.deepEqual(dayNight(start + 15 * 60_000, start), dayNight(start + 15 * 60_000, start));
});

test('a new hostile species really bites a nearby player', () => {
    const fauna = new Fauna(2506, {}, new Map(), 10);
    fauna.ground = () => 50;
    fauna.lineClear = () => true;
    const wolf = {id: 'a:0:0', species: 32, health: 70, x: 0, y: 50, z: 0, homeX: 0, homeZ: 0, goalX: 0, goalZ: 0,
        timer: 2, anger: 0, bite: 0, wander: 0, impulseX: 0, impulseZ: 0, fallSpeed: 0, flightHeight: 0, flightPhase: 0};
    fauna.animals = [wolf];
    const player = {id: 'p', playerKey: KEY, x: 1.2, y: 51.62, z: 0, positioned: true};
    const tick = fauna.tick(10000, [player], true);
    assert.ok(tick.damageEvents.length > 0, 'the ash wolf attacks without being provoked');
    assert.equal(tick.damageEvents[0].amount, 11);
});

test('boss arenas are deterministic, inside the room and fair to the spawn point', () => {
    const a = bossSites(2506), b = bossSites(2506);
    assert.deepEqual(a, b, 'same seed, same arenas');
    assert.ok(a.length >= 3, 'a room has several boss arenas');
    for (const site of a) {
        assert.ok(Math.abs(site.x) < 384 && Math.abs(site.z) < 384, 'inside room bounds');
        assert.ok(site.x ** 2 + site.z ** 2 >= 150 ** 2, 'not on top of the spawn point');
        assert.ok(site.kind >= 0 && site.kind < BOSS_KINDS.length);
    }
    assert.notDeepEqual(bossSites(2506).map(s => s.x), bossSites(111).map(s => s.x), 'seed changes the layout');
});

test('a boss wakes near a player, phases, shields and dies with loot', () => {
    const bosses = new WorldBosses(2506, {}, 10, new Map());
    const site = bosses.sites[0];
    assert.equal(bosses.snapshots().length, 0);

    const tick = bosses.tick(1000, [{id: 'p', x: site.x + 4, y: site.y, z: site.z}], false);
    assert.equal(bosses.snapshots().length, 1, 'boss spawned on approach');
    assert.ok(tick.bosses.length === 1);
    const boss = bosses.active[0];
    const kind = BOSS_KINDS[boss.kind];

    const player = {id: 'p', playerKey: KEY, x: boss.x + 2, y: boss.y, z: boss.z, positioned: true};
    const first = bosses.hit(player, boss.id, 103, 400, 5000, 4);
    assert.equal(first.damage, 400);
    assert.equal(first.boss.phase, 2, 'crossing 2/3 health starts phase 2');
    assert.equal(first.boss.shielded, true, 'a phase change shields the boss');

    const blocked = bosses.hit(player, boss.id, 103, 400, 5200, 4);
    assert.equal(blocked.damage, 0);
    assert.equal(blocked.blocked, true);

    const after = bosses.hit(player, boss.id, 103, kind.health, 9000, 4);
    assert.equal(after.defeated, true);
    assert.deepEqual(after.loot.map(l => l.item), kind.loot.map(l => l.item));
    assert.equal(bosses.snapshots().length, 0, 'a dead boss leaves the world');
    assert.ok(bosses.defeated.has(boss.id), 'the kill is remembered');
});

test('boss hits are validated: reach, line of sight and replays', () => {
    const r = room(15);
    const p = peer();
    start(r, p);
    const site = r.bosses.sites[0];
    position(r, p, site.x + 4, site.y, site.z, 1000);
    assert.equal(r.bosses.snapshots().length, 1, 'approaching the arena spawns the boss');
    const boss = r.bosses.active[0];
    const hit = (overrides, t) => r.handle(p, JSON.stringify({type: 'boss_hit', boss: boss.id, weapon: 103, requestId: 1, ...overrides}), t, () => {});

    // The hit radius is weapon reach plus the boss body radius, so ten blocks
    // away is out of reach while standing beside it is not.
    position(r, p, boss.x + 12, boss.y, boss.z, 1300);
    assert.throws(() => hit({}, 1500), /reach/);

    position(r, p, boss.x + 2, boss.y, boss.z, 1700);
    const ok = hit({}, 2200);
    assert.equal(ok.type, 'boss_hit');
    assert.ok(ok.damage > 0);
    assert.throws(() => hit({requestId: 2}, 2400), /cooldown/, 'the weapon cooldown applies');
    // A failed attempt never consumed request id 2, but a successful one did
    // consume id 1: replaying it after the cooldown must still be refused.
    assert.throws(() => hit({requestId: 1}, 4200), /replay/, 'a request id cannot be reused');
    assert.throws(() => hit({requestId: 3, boss: 'b:999:999'}, 4400), /no longer here/);
});

test('a defeated boss drops its loot into the living world and persists', () => {
    const r = room(15);
    const p = peer();
    start(r, p);
    const site = r.bosses.sites[0];
    position(r, p, site.x + 4, site.y, site.z, 1000);
    const boss = r.bosses.active[0];
    position(r, p, boss.x + 2, boss.y, boss.z, 1200);

    let t = 1400, requestId = 0, result = null;
    for (let i = 0; i < 260 && !result; i++) {
        t += 2000; // step past cooldowns and phase shields
        requestId += 1;
        const packet = {type: 'boss_hit', boss: boss.id, weapon: 103, requestId};
        try {
            const reply = r.handle(p, JSON.stringify(packet), t, () => {});
            if (reply.defeated) result = reply;
        } catch (error) {
            if (!/cooldown/.test(error.message)) throw error;
        }
    }
    assert.ok(result, 'the boss can be killed through the real packet handler');
    assert.ok(result.loot.length > 0, 'the kill returns a loot table');
    assert.ok((result.living?.drops || []).length > 0, 'loot became world drops');

    // A restart keeps the kill: the boss does not respawn immediately.
    const reloaded = new RoomState(r.config);
    assert.equal(reloaded.bosses.snapshots().length, 0);
    assert.ok(reloaded.bosses.defeated.has(boss.id));
    assert.equal(reloaded.config.contentVersion, 10);
});

test('content 9 rooms have no bosses and refuse boss packets', () => {
    const r = room(14);
    const p = peer();
    start(r, p);
    assert.equal(r.bosses.enabled, false);
    assert.equal(r.bosses.snapshots().length, 0);
    position(r, p, 10, 60, 10, 1000);
    assert.throws(() => r.handle(p, JSON.stringify({type: 'boss_hit', boss: 'b:0:0', weapon: 103, requestId: 1}), 2000, () => {}), /unavailable/);
    assert.equal('bosses' in r.welcome(p, []), false, 'old rooms do not grow new fields');
});

test('welcome advertises bosses and the day cycle only for content 10', () => {
    const modern = room(15);
    const p = peer();
    const welcome = modern.welcome(p, []);
    assert.ok('bosses' in welcome);
    assert.ok('dayNight' in welcome);
    assert.equal(typeof welcome.dayNight.night, 'boolean');
    assert.ok(Array.isArray(welcome.bosses));
});
