import test from 'node:test';
import assert from 'node:assert/strict';
import { createConfig, newPeer, RoomState } from '../src/protocol.js';

function createSeatedTwoCrewRoom(seed = 2506) {
  const ownerKey = 'a'.repeat(32);
  const mateKey = 'b'.repeat(32);
  const config = createConfig({ version: 1, seed, dimension: 0, rulesVersion: 12, mode: 0, ownerKey });
  config.lobby = false;
  config.started = true;
  config.padVisited = [ownerKey, mateKey];
  config.shuttleSeats = [ownerKey, mateKey];

  const room = new RoomState(config);
  const pilot = newPeer('p1', 'Commander', ownerKey);
  const copilot = newPeer('p2', 'Engineer', mateKey);
  const site = room.config.launchSite;
  // Exact seat 0 (-0.4, 42.96, 4.95) and seat 1 (1.4, 42.96, 4.95) local to orbiter at site.y + 2
  Object.assign(pilot, { positioned: true, x: site.x - 0.4, y: site.y + 44.96, z: site.z + 4.95, lastPosition: 1500 });
  Object.assign(copilot, { positioned: true, x: site.x + 1.4, y: site.y + 44.96, z: site.z + 4.95, lastPosition: 1500 });
  room.setMembers([pilot, copilot]);
  return { room, pilot, copilot, ownerKey, mateKey };
}

const send = (room, peer, msg, now) => room.handle(peer, JSON.stringify(msg), now);

test('full co-op Mars expedition: protocol-2 launch, warp, recoverable retro-burn, Mars landing, and return launch to Earth', () => {
  const { room, pilot, copilot } = createSeatedTwoCrewRoom(4242);
  let now = 2000;

  send(room, pilot, { type: 'space_flight_ready', protocol: 2 }, now);
  send(room, copilot, { type: 'space_flight_ready', protocol: 2 }, now);

  const launch = send(room, pilot, { type: 'space_ignite', target: 'mars' }, now);
  assert.equal(launch.type, 'space_mission');
  assert.equal(launch.space.from, 'earth');
  assert.equal(launch.space.to, 'mars');

  now += 15000;
  for (let i = 0; i < 45; i++) {
    now += 100;
    send(room, pilot, { type: 'space_warp', warp: true }, now);
  }

  now += 200000;
  send(room, pilot, { type: 'space_heartbeat' }, now);
  for (let step = 0; step < 25; step++) {
    now += 1000;
    send(room, pilot, { type: 'space_control', heading: [1, 0, 0], landingThrottle: 1 }, now);
  }

  const landed = send(room, pilot, { type: 'space_land' }, now);
  assert.equal(landed.space.phase, 'landed');
  assert.ok(landed.space.landingBurn >= 20);

  const surface = send(room, pilot, { type: 'space_complete_landing' }, now);
  assert.equal(surface.type, 'space_landed');
  assert.equal(surface.planet, 'mars');
  assert.equal(room.config.dimension, 2);

  // Refresh seat positions at now and launch return leg from Mars back to Earth!
  pilot.lastPosition = now;
  copilot.lastPosition = now;
  now += 500;
  send(room, pilot, { type: 'space_flight_ready', protocol: 2 }, now);
  send(room, copilot, { type: 'space_flight_ready', protocol: 2 }, now);
  const returnLeg = send(room, pilot, { type: 'space_ignite', target: 'earth' }, now);
  assert.equal(returnLeg.space.from, 'mars');
  assert.equal(returnLeg.space.to, 'earth');
});

test('full co-op Orbital Station docking, astronaut trade, and departure back to shuttle', () => {
  const { room, pilot, copilot, ownerKey } = createSeatedTwoCrewRoom(9999);
  const emptyStack = () => ({ item: 0, count: 0, durability: 0 });
  const slots = Array.from({ length: 45 }, emptyStack);
  slots[0] = { item: 141, count: 12, durability: 0 };
  room.savedPlayers.set(ownerKey, {
    claimedStoryRewards: 0,
    claimedAnimalLoot: [],
    milestones: 0,
    equippedCoat: false,
    grave: [],
    graveExtras: [],
    graveX: 0,
    graveY: 0,
    graveZ: 0,
    inventory: {
      slots,
      grid: Array.from({ length: 9 }, emptyStack),
      cursor: emptyStack(),
      equipment: Array.from({ length: 8 }, emptyStack),
      offhand: emptyStack(),
      selected: 0,
      revision: 1,
      furnace: { input: emptyStack(), fuel: emptyStack(), output: emptyStack(), burn: 0, progress: 0, recipe: 0, x: 0, y: 0, z: 0, realm: 0 },
    },
    survival: { health: 100, hunger: 100, stamina: 100, temperature: 37, wetness: 0, air: 100, dead: false, body: { hp: [100, 100, 100, 100, 100, 100], trauma: 0 } },
  });

  let now = 2000;
  send(room, pilot, { type: 'space_flight_ready', protocol: 2 }, now);
  send(room, copilot, { type: 'space_flight_ready', protocol: 2 }, now);

  const dockMsg = send(room, pilot, { type: 'space_dock_station' }, now);
  assert.equal(dockMsg.type, 'space_station_docked');
  assert.equal(dockMsg.station.active, true);

  now += 500;
  const tradeMsg = send(room, pilot, { type: 'station_trade', role: 0, requestId: 1 }, now);
  assert.equal(tradeMsg.type, 'station_trade');
  assert.equal(tradeMsg.privateSave.inventory.slots[0].count, 6);
  assert.ok(tradeMsg.privateSave.inventory.slots.some(s => s.item === 177 && s.count === 4));

  now += 500;
  const departMsg = send(room, pilot, { type: 'space_depart_station' }, now);
  assert.equal(departMsg.type, 'space_station_departed');
  assert.equal(room.config.spaceVisit, undefined);
});
