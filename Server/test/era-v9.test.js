import {test} from 'node:test';
import assert from 'node:assert/strict';
import {
  RULES_VERSION, createConfig, acceptsClient, validateEdit, validatePlayerSave
} from '../src/protocol.js';
import {applyExpedition} from '../src/terrain-expedition.js';
import {Fauna, wildlifeHabitat, animalHeight} from '../src/wildlife.js';

const KEY = 'e'.repeat(32);

test('ERA-V9 rules14 / contentVersion 9 supports blocks 70..89, items 190..195 + secret 666, uranium veins and Radia mutants 20..29', () => {
  assert.equal(RULES_VERSION, 15);
  const cfg9 = createConfig({version: 1, ownerKey: KEY, seed: 2506, dimension: 0, mode: 0, rulesVersion: 14});
  assert.equal(cfg9.contentVersion, 9);
  assert.equal(acceptsClient(cfg9, 14), true);
  assert.equal(acceptsClient(cfg9, 13), false);

  // Legacy v13 rooms remain untouched at contentVersion 8 and still accept v14 clients
  const cfg8 = createConfig({version: 1, ownerKey: KEY, seed: 2506, dimension: 0, mode: 0, rulesVersion: 13});
  assert.equal(cfg8.contentVersion, 8);
  assert.equal(acceptsClient(cfg8, 14), true);

  const peer = {positioned: true, lastEdit: 0, x: 0.5, y: 50.5, z: 0.5};
  for (let id = 70; id <= 89; id++) {
    assert.equal(validateEdit({x: 0, y: 50, z: 0, block: id}, peer, new Map(), 1000, 89, 64).block, id);
  }
  assert.throws(() => validateEdit({x: 0, y: 50, z: 0, block: 90}, peer, new Map(), 1000, 89, 64));

  const empty = () => ({item: 0, count: 0, durability: 0});
  const save = {
    inventory: {selected: 0, slots: Array.from({length: 45}, empty), equipment: Array.from({length: 8}, empty)},
    survival: {health: 100, hunger: 85, stamina: 100, temperature: 37, wetness: 0}
  };
  save.inventory.slots[0] = {item: 70, count: 12, durability: 0};  // Uranium Ore
  save.inventory.slots[1] = {item: 190, count: 4, durability: 0};  // Uranium Rod
  save.inventory.slots[2] = {item: 192, count: 1, durability: 0};  // Electric Locomotive
  save.inventory.slots[3] = {item: 194, count: 1, durability: 0};  // Quad Drone
  save.inventory.slots[4] = {item: 195, count: 1, durability: 0};  // Industrial Robot
  save.inventory.slots[5] = {item: 666, count: 1, durability: 0};  // Secret 666 Warhead
  assert.equal(validatePlayerSave(save, true, true, true, true, true).inventory.slots[5].item, 666);
  assert.throws(() => validatePlayerSave(save, true, true, true, true, false), 'v8 rooms reject v9 items cleanly');

  // Deep Uranium Ore (70) generates deterministically in contentVersion >= 9 without touching contentVersion 8
  let foundUranium = false;
  for (let x = -180; x <= 180 && !foundUranium; x += 3) {
    for (let z = -180; z <= 180 && !foundUranium; z += 3) {
      for (let y = 7; y <= 27; y += 3) {
        const col = {x, z, surface: false, mine: false};
        const v9 = applyExpedition(col, y, 2506, 1, 9);
        const v8 = applyExpedition(col, y, 2506, 1, 8);
        if (v9 === 70) {
          assert.notEqual(v8, 70, 'v8 must stay bit-identical');
          foundUranium = true;
          break;
        }
      }
    }
  }
  assert.ok(foundUranium, 'Expected Uranium Ore (70) veins in v9 underground');

  // Radia mutant species 20..29 all have physical heights and map to habitat 6
  assert.equal(wildlifeHabitat(79, 0, 0, 2506), 6);
  for (let s = 20; s <= 29; s++) {
    assert.ok(animalHeight(s) > 1.0, `Radia mutant ${s} must have valid physical height`);
  }
  const savedMutants = {
    animals: Array.from({length: 10}, (_, i) => ({
      id: `a:${i}:99`, species: 20 + i, health: 60, x: i * 2, y: 50, z: 4, yaw: 0
    }))
  };
  const faunaV9 = new Fauna(2506, savedMutants, new Map(), 9);
  assert.equal(faunaV9.snapshots().length, 10);
});
