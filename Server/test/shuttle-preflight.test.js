import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createConfig,newPeer,RoomState} from '../src/protocol.js';
const a='a'.repeat(32),b='b'.repeat(32),t=20000;
function crew(){
  const config=createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:11,ownerKey:a});
  config.started=true;
  const room=new RoomState(config),pilot=newPeer('pilot','Pilot',a),copilot=newPeer('copilot','Copilot',b);
  let writes=0;
  const send=(who,type,time,fields={})=>room.handle(who,JSON.stringify({type,...fields}),time,()=>{},()=>{},()=>{writes++;});
  const seat=(who,index,time)=>send(who,'position',time,{x:config.launchSite.x+(index===0?-.4:1.4),y:config.launchSite.y+45.58,z:config.launchSite.z+5.35,yaw:0});
  room.config.padVisited=[a,b]; // actual lift ascent + exit is tested by shuttle-lift-room.test.js
  seat(pilot,0,t);seat(copilot,1,t+100);
  send(pilot,'space_seat',t+200,{action:'sit',index:0});
  send(copilot,'space_seat',t+300,{action:'sit',index:1});
  return {room,pilot,copilot,send,seat,writes:()=>writes};
}
test('read-only pilot preflight verifies two connected cockpit occupants without igniting',()=>{
  const {room,pilot,copilot,send,writes}=crew();
  assert.throws(()=>send(copilot,'space_preflight',t+400),/pilot/);
  const before=JSON.stringify(room.config),previousWrites=writes(),result=send(pilot,'space_preflight',t+500);
  assert.equal(result.type,'space_preflight');assert.equal(result.id,pilot.id);
  assert.equal(result.crewCount,2);assert.equal(result.ready,true);assert.deepEqual(result.issues,[]);
  assert.equal(JSON.stringify(room.config),before);assert.equal(writes(),previousWrites);
  assert.equal(room.config.spaceMission,undefined,'preflight must not launch');
  assert(!JSON.stringify(result).includes(a));assert(!JSON.stringify(result).includes(b));
  assert.throws(()=>send(pilot,'space_launch',t+600),/Unknown packet type/);
  assert.equal(room.config.spaceMission,undefined);
});
test('preflight fails closed for hatch, moving lift, disconnected crew, stale or displaced position',()=>{
  const {room,pilot,copilot,send,seat}=crew();
  room.config.launchHatch=true;
  assert.equal(send(pilot,'space_preflight',t+400).ready,false);
  room.config.launchHatch=false;
  room.config.launchLift={from:0,to:1,startedAt:t,riderKey:null};
  assert.equal(send(pilot,'space_preflight',t+400).ready,false);
  room.config.launchLift={from:1,to:1,startedAt:t,riderKey:a};
  assert.equal(send(pilot,'space_preflight',t+500).ready,false);
  room.config.launchLift={from:1,to:1,startedAt:t,riderKey:null};
  assert.equal(send(pilot,'space_preflight',t+2000).ready,false);
  seat(pilot,0,t+2100);seat(copilot,1,t+2200);
  assert.equal(send(pilot,'space_preflight',t+2300).ready,true);
  send(copilot,'position',t+2500,{x:room.config.launchSite.x+3,y:room.config.launchSite.y+45.58,z:room.config.launchSite.z+5.35,yaw:0});
  assert.equal(send(pilot,'space_preflight',t+2600).ready,false);
  seat(copilot,1,t+2700);
  room.disconnect(copilot,()=>{});
  assert.equal(send(pilot,'space_preflight',t+2800).ready,false);
  assert.equal(room.config.spaceMission,undefined);
});
