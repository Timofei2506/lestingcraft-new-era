import {test} from 'node:test';
import assert from 'node:assert/strict';
import {PORT,latchStation,setStationHatch,undockStation,stationWalk,stationTrade,CREW} from '../src/station-visit.js';
const approach={shipNose:PORT,relativeVelocity:[0,.1,0],forward:[-1,0,0],portNormal:[1,0,0],shipHatchSealed:true,pilotAuthorized:true,now:10000};
const docked=()=>latchStation(null,approach);
const open=()=>setStationHatch(docked(),true,18000);
const bag=()=>({inventory:{revision:0,slots:Array.from({length:45},()=>({item:0,count:0,durability:0}))}});

test('close, slow, sealed and pilot-authorized approach is required; never snap craft to port',()=>{
 for(const patch of [
   {shipNose:[PORT[0]+3,0,0]},{relativeVelocity:[.36,0,0]},{forward:[1,0,0]},
   {shipHatchSealed:false},{pilotAuthorized:false},{shipNose:[NaN,0,0]}])
   assert.throws(()=>latchStation(null,{...approach,...patch}),/Docking/);
 assert.deepEqual(docked(),{latched:true,hatchOpen:false,latchedAt:10000});
 assert.throws(()=>latchStation(docked(),approach),/Docking/);
});
test('pressure and all crew must be verified before passage and undocking',()=>{
 const d=docked();
 assert.throws(()=>setStationHatch(d,true,17999),/pressurized/);
 const o=open();assert.equal(stationWalk(o,[-42.2,1,0],[-41.8,1,0]),true);
 assert.equal(stationWalk(d,[-42.2,1,0],[-41.8,1,0]),false);
 assert.equal(stationWalk(o,[-42.2,1,0],[17,1,0]),false);
 assert.equal(stationWalk(o,[6,1,0],[6,1,3]),false);
 assert.throws(()=>undockStation(o,{allCrewAboard:true,pilotAuthorized:true}),/blocked/);
 const shut=setStationHatch(o,false,18001);
 assert.throws(()=>undockStation(shut,{allCrewAboard:false,pilotAuthorized:true}),/blocked/);
 assert.deepEqual(undockStation(shut,{allCrewAboard:true,pilotAuthorized:true}).latched,false);
});
test('all three astronaut roles have distinct goods and proximity-gated atomic exchanges',()=>{
 assert.equal(new Set(CREW.map(c=>c.offer.output)).size,3);
 const d=open(),save=bag();save.inventory.slots[0]={item:141,count:6,durability:0};
 for(const patch of [{crewVerified:false},{local:[17,0,0]},{role:3}])
   assert.throws(()=>stationTrade(d,{crewVerified:true,local:[5,0,0],role:0,save,...patch}),/not available/);
 const next=stationTrade(d,{crewVerified:true,local:[5,0,0],role:0,save});
 assert.equal(save.inventory.slots[0].count,6,'source bag is not mutated');
 assert.equal(next.inventory.slots[0].item,177);assert.equal(next.inventory.slots[0].count,4);
 assert.equal(next.inventory.revision,1);
 assert.throws(()=>stationTrade(d,{crewVerified:true,local:[5,0,0],role:0,save:next}),/ресурсов/);
});
test('a full bag or closed hatch cannot mint goods or consume payment',()=>{
 const d=open(),save=bag();save.inventory.slots.fill(null);for(let i=0;i<45;i++)save.inventory.slots[i]={item:140,count:64,durability:0};
 save.inventory.slots[0]={item:141,count:7,durability:0};
 const original=JSON.stringify(save);
 assert.throws(()=>stationTrade(d,{crewVerified:true,local:[5,0,0],role:0,save}),/заполнен/);
 assert.equal(JSON.stringify(save),original);
 assert.throws(()=>stationTrade(docked(),{crewVerified:true,local:[5,0,0],role:0,save}),/not available/);
});
