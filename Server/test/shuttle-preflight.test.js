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

test('trusted ignition transition persists a unique two-person lunar mission only after fresh preflight',()=>{
  const {room,pilot,copilot,seat}=crew();
  assert.throws(()=>room.igniteVerifiedMoon(copilot,t+400),/pilot/);
  room.config.launchHatch=true;
  assert.throws(()=>room.igniteVerifiedMoon(pilot,t+400),/Preflight refused/);
  room.config.launchHatch=false;
  let commits=0;
  assert.throws(()=>room.igniteVerifiedMoon(pilot,t+400,()=>{throw Error('storage unavailable');}),/storage unavailable/);
  assert.equal(room.config.spaceMission,undefined,'failed storage must not strand the room');
  const started=room.igniteVerifiedMoon(pilot,t+500,next=>{
    assert.equal(next.spaceMission.crew.length,2);
    assert.deepEqual(next.spaceMission.crew,[a,b]);
    commits++;
  });
  assert.equal(commits,1);
  assert.equal(started.space.phase,'countdown');
  assert.equal(started.space.seconds,1800);
  assert.deepEqual(started.space.position,[149597870700,0,0]);
  assert(!JSON.stringify(started).includes(a));assert(!JSON.stringify(started).includes(b));
  assert.throws(()=>room.igniteVerifiedMoon(pilot,t+600),/Preflight refused/);
  assert.equal(room.config.spaceMission.ignitionAt,t+500+10000);
  // Normal ground packets are refused for crew while the server owns the mission.
  assert.throws(()=>seat(pilot,0,t+700),/In-flight crew/);
  const restarted=new RoomState(JSON.parse(JSON.stringify(room.config)));
  assert.equal(restarted.config.spaceMission.crew.length,2);
  const welcome=restarted.welcome(newPeer('new-pilot','Pilot',a),[]);
  assert.equal(welcome.space.active,true);
  assert.equal(welcome.space.seatIndex,0);
  assert.equal(welcome.space.ignitionAt,t+500+10000);
  assert(!JSON.stringify(welcome).includes(a));assert(!JSON.stringify(welcome).includes(b));
});

test('trusted ignition fails when seat claims are stale, a crew member leaves or a rider remains',()=>{
  const {room,pilot,copilot,send,seat}=crew();
  assert.throws(()=>room.igniteVerifiedMoon(pilot,t+2000),/Preflight refused/);
  seat(pilot,0,t+2100);seat(copilot,1,t+2200);
  room.config.launchLift={from:1,to:1,startedAt:t,riderKey:b};
  assert.throws(()=>room.igniteVerifiedMoon(pilot,t+2300),/Preflight refused/);
  room.config.launchLift={from:1,to:1,startedAt:t,riderKey:null};
  send(copilot,'space_seat',t+2300,{action:'stand',index:1});
  assert.throws(()=>room.igniteVerifiedMoon(pilot,t+2400),/Preflight refused/);
  seat(copilot,1,t+2500);send(copilot,'space_seat',t+2600,{action:'sit',index:1});
  room.disconnect(copilot,()=>{});
  assert.throws(()=>room.igniteVerifiedMoon(pilot,t+2700),/Preflight refused/);
  assert.equal(room.config.spaceMission,undefined);
});

test('network ignition fails closed until BOTH current seated clients negotiate a released flight protocol',()=>{
  const {room,pilot,copilot,send,seat}=crew();
  assert.throws(()=>send(pilot,'space_ignite',t+400),/not released/);
  assert.equal(room.config.spaceMission,undefined);
  room.config.flightEnabled=true; // trusted server-only feature gate, never accepted in createConfig
  assert.throws(()=>send(pilot,'space_ignite',t+400),/Both crew clients/);
  assert.throws(()=>send(copilot,'space_flight_ready',t+400,{protocol:22}),/Flight client/);
  assert.equal(send(pilot,'space_flight_ready',t+500,{protocol:1}).seatIndex,0);
  assert.throws(()=>send(pilot,'space_ignite',t+600),/Both crew clients/);
  assert.equal(send(copilot,'space_flight_ready',t+600,{protocol:1}).seatIndex,1);
  assert.throws(()=>send(copilot,'space_ignite',t+700),/pilot/);
  const result=send(pilot,'space_ignite',t+800);
  assert.equal(result.space.phase,'countdown');assert.equal(result.space.crewCount,2);
  assert.deepEqual(room.config.spaceMission.crew,[a,b]);
  assert(!JSON.stringify(result).includes(a));assert(!JSON.stringify(result).includes(b));
  assert.throws(()=>send(pilot,'space_ignite',t+900),/Both crew clients|not released|Preflight refused/);
});

test('reconnect, missing second client, out-of-seat position and stale capability never ignite',()=>{
  const {room,pilot,copilot,send,seat}=crew();room.config.flightEnabled=true;
  send(pilot,'space_flight_ready',t+400,{protocol:1});
  send(copilot,'space_flight_ready',t+500,{protocol:1});
  assert.throws(()=>send(pilot,'space_ignite',t+2100),/Both crew clients/);
  seat(pilot,0,t+2200);seat(copilot,1,t+2300);
  send(pilot,'space_flight_ready',t+2400,{protocol:1});
  send(copilot,'position',t+2500,{x:room.config.launchSite.x+3,y:room.config.launchSite.y+45.58,z:room.config.launchSite.z+5.35,yaw:0});
  send(copilot,'space_flight_ready',t+2600,{protocol:1});
  assert.throws(()=>send(pilot,'space_ignite',t+2700),/Preflight refused/);
  seat(copilot,1,t+2800);room.disconnect(copilot,()=>{});
  assert.throws(()=>send(pilot,'space_ignite',t+2900),/Both crew clients/);
  assert.equal(room.config.spaceMission,undefined);
});
