import {test} from 'node:test';
import assert from 'node:assert/strict';
import {RoomState,createConfig,newPeer,publicConfig} from '../src/protocol.js';
import {beginMoonMission,MOON_SECONDS,COUNTDOWN_MS,EARTH_X} from '../src/space-mission.js';
const pilot='a'.repeat(32),passenger='b'.repeat(32),outsider='c'.repeat(32),start=1000000,ignition=start+COUNTDOWN_MS;
function fixture(){
 const config=createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:10,ownerKey:pilot});config.started=true;
 config.spaceMission=beginMoonMission({crew:[pilot,passenger],boarded:[pilot,passenger],pilot,now:start});
 return {room:new RoomState(config),p:newPeer('captain','Pilot',pilot),q:newPeer('crew','Passenger',passenger)};
}
const send=(room,peer,packet,now,persist=()=>{})=>room.handle(peer,JSON.stringify(packet),now,()=>{},()=>{},persist);

test('public room creation cannot create a flight or reveal passenger keys',()=>{
 const c=createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:10,ownerKey:pilot,spaceMission:fixture().room.config.spaceMission});
 c.started=true;const r=new RoomState(c);
 assert.equal(c.spaceMission,undefined);
 assert.equal(publicConfig(fixture().room.config).spaceMission,undefined);
 assert.throws(()=>send(r,newPeer('intruder','Intruder',outsider),{type:'space_heartbeat'},start+1000),/authenticated/);
 const {room,p,q}=fixture();
 assert(!JSON.stringify(room.welcome(q,[q])).includes(pilot));
 assert.throws(()=>send(room,newPeer('intruder','Intruder',outsider),{type:'space_control',heading:[0,1,0]},start+1000),/authenticated/);
});
test('two members see one server-timed position, pilot alone steers; terrain packets blocked midflight',()=>{
 const {room,p,q}=fixture();let writes=0;
 let msg=send(room,q,{type:'space_heartbeat',now:ignition+900000},ignition+630000,()=>{writes++;});
 assert.equal(msg.space.phase,'cruise');assert.equal(msg.space.crewCount,2);assert(!JSON.stringify(msg).includes(passenger));
 assert.equal(writes,1);assert.equal(room.config.spaceMission.position[0],msg.space.position[0]);
 assert.throws(()=>send(room,q,{type:'space_control',heading:[0,0,1]},ignition+631000),/pilot/);
 assert.throws(()=>send(room,p,{type:'position',x:0,y:52,z:0,yaw:0},ignition+631000),/In-flight crew/);
 msg=send(room,p,{type:'space_control',heading:[0,0,1]},ignition+631000);
 assert.ok(msg.space.position[2]>0);
 const copy=new RoomState(room.config);
 assert.deepEqual(copy.config.spaceMission.position,room.config.spaceMission.position);
 assert.throws(()=>send(copy,newPeer('spy','X',outsider),{type:'space_heartbeat'},ignition+632000),/authenticated/);
});
test('only pilot can enable cruise autopilot or land after real thirty minutes and manual braking',()=>{
 const {room,p,q}=fixture();send(room,p,{type:'space_heartbeat'},ignition+630000);
 assert.throws(()=>send(room,q,{type:'space_autopilot',on:true},ignition+630001),/Pilot/);
 let r=send(room,p,{type:'space_autopilot',on:true},ignition+630001);assert.equal(r.space.autopilot,true);
 r=send(room,q,{type:'space_heartbeat'},ignition+1620000);assert.equal(r.space.phase,'approach');
 assert.throws(()=>send(room,p,{type:'space_land'},ignition+1620000),/landing/);
 // Three seconds of fresh manual throttle, not a client-provided elapsed time.
 for(let n=1;n<=25;n++)r=send(room,p,{type:'space_control',landingThrottle:1},ignition+1620000+n*1000);
 assert.ok(r.space.landingBurn>=20);
 assert.throws(()=>send(room,p,{type:'space_land'},ignition+1645000),/landing/);
 r=send(room,q,{type:'space_heartbeat'},ignition+MOON_SECONDS*1000);
 assert.equal(r.space.phase,'landing-ready');assert.ok(Math.abs(r.space.position[0]-(EARTH_X+384400000))<1);
 assert.throws(()=>send(room,q,{type:'space_land'},ignition+MOON_SECONDS*1000),/landing/);
 r=send(room,p,{type:'space_land'},ignition+MOON_SECONDS*1000);
 assert.equal(r.space.phase,'landed');assert.equal(room.config.spaceMission.landed,true);
});
test('disk failure cannot advance a shared mission or change crew state',()=>{
 const {room,p}=fixture(),before=JSON.stringify(room.config.spaceMission);
 assert.throws(()=>send(room,p,{type:'space_heartbeat'},ignition+630000,()=>{throw Error('disk error');}),/disk error/);
 assert.equal(JSON.stringify(room.config.spaceMission),before);
});
