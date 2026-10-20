import {test} from 'node:test';
import assert from 'node:assert/strict';
import {RoomState,createConfig,newPeer,publicConfig,validatePlayerSave} from '../src/protocol.js';
const a='a'.repeat(32),b='b'.repeat(32),outsider='c'.repeat(32);
const empty=()=>({item:0,count:0,durability:0});
function playerSave(){const save={inventory:{selected:0,revision:0,slots:Array.from({length:45},empty)},survival:{health:100,hunger:80,stamina:100,temperature:37,wetness:0,air:100}};save.inventory.slots[0]={item:141,count:12,durability:0};return validatePlayerSave(save,true,true,true);}
function setup(){
 const config=createConfig({version:1,rulesVersion:10,mode:2,seed:2506,dimension:0,ownerKey:a});config.started=true;
 // Only trusted flight code may install this after actual docking; room creation ignores it.
 config.spaceVisit={crew:[a,b],dock:{latched:true,hatchOpen:true,latchedAt:10000},positions:{[a]:[-44,0,0],[b]:[-44,0,0]}};
 const room=new RoomState(config,[],[{key:a,save:playerSave()},{key:b,save:playerSave()}]);
 return {room,p:newPeer('peer-a','Alice',a),q:newPeer('peer-b','Bob',b)};
}
const send=(room,peer,packet,now,savePlayer=()=>{},saveConfig=()=>{})=>room.handle(peer,JSON.stringify(packet),now,()=>{},savePlayer,saveConfig);

test('ordinary room has no station capability; client cannot create or reveal private crew keys',()=>{
 const cfg=createConfig({version:1,rulesVersion:10,mode:2,seed:2506,dimension:0,ownerKey:a,spaceVisit:setup().room.config.spaceVisit});
 cfg.started=true;assert.equal(cfg.spaceVisit,undefined);
 const room=new RoomState(cfg),peer=newPeer('stranger','X',outsider);
 assert.throws(()=>send(room,peer,{type:'station_walk',local:[-43.5,0,0]},12000),/authenticated/);
 assert.throws(()=>send(room,peer,{type:'station_trade',role:0,requestId:1,spaceVisit:setup().room.config.spaceVisit},12100),/authenticated/);
 const trusted=setup();assert(!JSON.stringify(trusted.room.welcome(trusted.p,[trusted.p])).includes(b));
 assert.deepEqual(trusted.room.welcome(trusted.p,[trusted.p]).station.local,[-44,0,0]);
 assert.equal(trusted.room.welcome(peer,[]).station,undefined);
 assert.equal(publicConfig(trusted.room.config).spaceVisit,undefined);
 assert.equal(trusted.room.config.spaceVisit.positions[b][0],-44);
});
test('two real identities walk on a shared persisted corridor; no leap, closed hatch, other crew or client inventory',()=>{
 const {room,p,q}=setup();let writes=0;
 assert.throws(()=>send(room,p,{type:'station_walk',local:[-43,0,0]},11000,()=>{},()=>{}),/invalid/);
 assert.throws(()=>send(room,newPeer('spy','Spy',outsider),{type:'station_walk',local:[-43.5,0,0]},11000),/authenticated/);
 const step=send(room,p,{type:'station_walk',local:[-43.5,0,0],crew:[outsider]},11100,()=>{},()=>{writes++});
 assert.equal(step.id,'peer-a');assert.deepEqual(step.local,[-43.5,0,0]);assert.equal(writes,1);
 assert.equal(room.config.spaceVisit.positions[a][0],-43.5);assert.equal(room.config.spaceVisit.positions[b][0],-44);
 assert.throws(()=>send(room,q,{type:'station_trade',role:0,requestId:1,save:playerSave()},11200),/available/);
 assert.throws(()=>send(room,p,{type:'station_walk',local:[-43,0,0]},11120),/invalid/);
 room.config.spaceVisit.dock.hatchOpen=false;
 let before=JSON.stringify(room.config.spaceVisit);
 for(const who of [p,q])assert.throws(()=>send(room,who,{type:'station_walk',local:[-41.8,0,0]},11300),/invalid/);
 assert.equal(JSON.stringify(room.config.spaceVisit),before);
});
test('astronaut trading is server-sourced, atomic, replay-safe and survives reconstruction',()=>{
 const {room,p,q}=setup();
 let now=20000;
 for(let x=-43.5;x<=5;x+=.5){send(room,p,{type:'station_walk',local:[x,0,0]},now,()=>{},()=>{});now+=100;}
 assert.equal(room.config.spaceVisit.positions[a][0],5);
 assert.equal(room.config.spaceVisit.positions[b][0],-44);
 assert.throws(()=>send(room,q,{type:'station_trade',role:0,requestId:1},now),/available/);
 let writes=0;
 const out=send(room,p,{type:'station_trade',role:0,requestId:1,save:{inventory:{slots:[]}}},now,(key,save,config)=>{
  writes++;assert.equal(key,a);assert.equal(save.inventory.slots[0].count,6);assert.equal(config.spaceVisit.positions[a][0],5);assert.equal(config.spaceVisit.tradeRequests[a],1);
 });
 assert.equal(writes,1);assert.equal(out.type,'station_trade');assert.equal(out.privateSave.inventory.revision,1);
 assert.equal(room.savedPlayers.get(a).inventory.slots[0].count,6);
 assert.equal(room.savedPlayers.get(b).inventory.slots[0].count,12);
 assert(!JSON.stringify({...out,privateSave:undefined}).includes(a));
 assert.throws(()=>send(room,p,{type:'station_trade',role:0,requestId:1},now+100),/replay/);
 assert.throws(()=>send(room,p,{type:'state',save:playerSave()},now+150),/In-flight crew/);
 assert.throws(()=>send(room,p,{type:'living_action',action:'trade',requestId:1,save:playerSave()},now+200),/In-flight crew/);
 assert.throws(()=>send(room,p,{type:'position',x:5,y:55,z:5,yaw:0},now+250),/In-flight crew/);
 const copy=new RoomState(room.config,[],[...room.savedPlayers].map(([key,save])=>({key,save})));
 assert.equal(copy.config.spaceVisit.positions[a][0],5);
 assert.equal(copy.welcome(newPeer('new','Reconnect',a),[]).save.inventory.slots[0].count,6);
 assert.equal(copy.welcome(newPeer('new','Reconnect',a),[]).stationRequestId,1);
 assert.throws(()=>send(copy,newPeer('new','Reconnect',a),{type:'station_trade',role:0,requestId:1},now+1000),/replay/);
 assert.throws(()=>send(copy,newPeer('spy','Spy',outsider),{type:'station_trade',role:0,requestId:1},now+1000),/authenticated/);
});
test('a failed disk write never changes walking position or consumes resources',()=>{
 const {room,p}=setup(),before=JSON.stringify(room.config);
 assert.throws(()=>send(room,p,{type:'station_walk',local:[-43.5,0,0]},12000,()=>{},()=>{throw Error('disk write failed');}),/disk write/);
 assert.equal(JSON.stringify(room.config),before);assert.equal(p.lastStationWalk,undefined);
 room.config.spaceVisit.positions[a]=[5,0,0];let saved=JSON.stringify(room.savedPlayers.get(a));
 assert.throws(()=>send(room,p,{type:'station_trade',role:0,requestId:1},13000,()=>{throw Error('disk write failed');}),/disk write/);
 assert.equal(JSON.stringify(room.savedPlayers.get(a)),saved);assert.equal(p.lastStationTradeRequest,undefined);
});
