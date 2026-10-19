import {test} from 'node:test';import assert from 'node:assert/strict';
import {validateEdit,validatePlayerSave} from '../src/protocol.js';
// Stage 1.3 space stations 49..53 (Assets/Scripts/Gameplay/FuelStations.cs, ExpandedContent.MaxBlock 53).
const empty=()=>({item:0,count:0,durability:0});
const save=()=>({inventory:{selected:0,slots:Array.from({length:45},empty),equipment:Array.from({length:8},empty)},survival:{health:100,hunger:80,stamina:100,temperature:37,wetness:0}});
const peer=()=>({positioned:true,lastEdit:0,x:0.5,y:50.5,z:0.5});
test('station items 49..53 are accepted only in expanded worlds',()=>{
  for(let id=49;id<=53;id++){
    const s=save();s.inventory.slots[2]={item:id,count:1,durability:0};
    assert.equal(validatePlayerSave(s,true,false,false).inventory.slots[2].item,id);
    const rejected=save();rejected.inventory.slots[2]={item:id,count:1,durability:0};
    assert.throws(()=>validatePlayerSave(rejected,false,false,false));
  }
});
test('station ids just outside 49..53 stay rejected',()=>{
  for(const id of [48,54]){const s=save();s.inventory.slots[0]={item:id,count:1,durability:0};assert.throws(()=>validatePlayerSave(s,true,false,false));}
});
test('room content 7 accepts placing block 53, room 5/6 limit (maxBlock 48) rejects it',()=>{
  const edit={x:0,y:50,z:0,block:53};
  assert.equal(validateEdit(edit,peer(),new Map(),1000,53,64).block,53);
  assert.throws(()=>validateEdit({x:0,y:50,z:0,block:49},peer(),new Map(),1000,48,64));
  assert.throws(()=>validateEdit({x:0,y:50,z:0,block:54},peer(),new Map(),1000,53,64));
});
