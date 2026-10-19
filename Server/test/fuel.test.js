import {test} from 'node:test';import assert from 'node:assert/strict';
import {validatePlayerSave} from '../src/protocol.js';
// Stage-1 fuel items (170..177, see Assets/Scripts/Gameplay/FuelChain.cs). Save validation must accept them and reject neighbours.
const empty=()=>({item:0,count:0,durability:0});
const save=()=>({inventory:{selected:0,slots:Array.from({length:45},empty),equipment:Array.from({length:8},empty)},survival:{health:100,hunger:80,stamina:100,temperature:37,wetness:0}});
test('fuel chain items 170..177 survive save validation with their counts',()=>{
  for(let id=170;id<=177;id++){const s=save();s.inventory.slots[3]={item:id,count:7,durability:0};const out=validatePlayerSave(s,true,false,false);assert.equal(out.inventory.slots[3].item,id);assert.equal(out.inventory.slots[3].count,7);}
});
test('ids just outside the fuel range are still rejected',()=>{
  for(const id of [169,178]){const s=save();s.inventory.slots[0]={item:id,count:1,durability:0};assert.throws(()=>validatePlayerSave(s,true,false,false));}
});
