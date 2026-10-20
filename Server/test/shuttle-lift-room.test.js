import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createConfig,newPeer,RoomState,publicConfig} from '../src/protocol.js';
import {liftAt,liftSnapshot,LIFT_MS,LIFT_TRAVEL} from '../src/shuttle-lift.js';
const captain='a'.repeat(32),friend='b'.repeat(32),intruder='c'.repeat(32),t=10000;
function setup(){
  const config=createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:11,ownerKey:captain});
  config.started=true;
  const room=new RoomState(config),p=newPeer('one','Captain',captain),q=newPeer('two','Friend',friend);
  return {room,p,q,site:config.launchSite};
}
const send=(room,peer,packet,now,persist=()=>{})=>room.handle(peer,JSON.stringify(packet),now,()=>{},()=>{},persist);
const foot=(s,progress)=>({x:s.x-19.8,y:s.y+5+progress*LIFT_TRAVEL+1.58,z:s.z+3,yaw:0});
const position=(r,p,s,time,progress=0)=>send(r,p,{type:'position',...foot(s,progress)},time);
const lift=(r,p,action,time,persist)=>send(r,p,{type:'space_lift',action,height:9999,riderKey:intruder},time,persist);

test('two people share one server-owned lift, empty recall and 25 m collision route',()=>{
  const {room,p,q,site}=setup();
  assert.equal(room.welcome(p,[p]).lift.progress,0);
  assert(!JSON.stringify(room.welcome(p,[p])).includes(captain));
  assert.equal(publicConfig(room.config).launchLift,undefined);
  assert.throws(()=>lift(room,p,'up',t+100),/position/);
  position(room,p,site,t);
  let saves=0;
  let out=lift(room,p,'up',t+100,()=>saves++);
  assert.equal(saves,1);assert.equal(out.lift.riderId,p.id);
  assert.equal(out.lift.height,0);assert.equal(room.config.launchLift.riderKey,captain);
  assert.throws(()=>lift(room,p,'exit',t+900),/motion/);
  position(room,q,site,t+1000);
  assert.throws(()=>lift(room,q,'recall',t+1050),/motion/);
  assert.equal(liftAt(room.config.launchLift,t+100+LIFT_MS/2).progress,.5);
  out=position(room,p,site,t+100+LIFT_MS/2,.5);
  assert.equal(out.lift.height,12.5);assert.equal(out.lift.riderId,p.id);
  position(room,q,site,t+4000);
  assert.throws(()=>lift(room,q,'up',t+4100),/motion/);
  position(room,p,site,t+100+LIFT_MS,1);
  out=lift(room,p,'exit',t+100+LIFT_MS+100);
  assert.equal(out.lift.height,25);assert.equal(out.lift.occupied,false);
  assert.deepEqual(room.config.padVisited,[captain]);
  assert(!JSON.stringify(room.welcome(q,[p,q])).includes(captain));
  position(room,q,site,t+100+LIFT_MS+120);
  assert.throws(()=>lift(room,q,'up',t+100+LIFT_MS+150),/stationary/);
  position(room,q,site,t+100+LIFT_MS+220);
  out=lift(room,q,'recall',t+100+LIFT_MS+300);assert.equal(out.lift.riderId,'');
  assert.equal(out.lift.moving,true);
  out=position(room,q,site,t+100+2*LIFT_MS+400);
  assert.equal(out.lift.height,0);
  out=lift(room,q,'up',t+100+2*LIFT_MS+500);
  assert.equal(out.lift.riderId,q.id);
  assert.equal(room.config.launchLift.riderKey,friend);
  const copy=new RoomState(JSON.parse(JSON.stringify(room.config)));
  const rejoined=newPeer('friend-reconnected','Friend',friend);
  assert.equal(copy.welcome(rejoined,[rejoined]).lift.riderId,rejoined.id);
  assert(!JSON.stringify(copy.welcome(rejoined,[rejoined])).includes(friend));
});

test('only a rider who exited at the top may physically open the shared hatch',()=>{
  const {room,p,q,site}=setup();position(room,p,site,t);
  position(room,q,site,t+100);
  assert.throws(()=>send(room,q,{type:'space_hatch',open:true},t+200),/physically/);
  lift(room,p,'up',t+300);
  position(room,p,site,t+300+LIFT_MS,1);
  lift(room,p,'exit',t+300+LIFT_MS+100);
  const topY=site.y+30+1.58,z=site.z+3;
  assert.throws(()=>send(room,p,{type:'position',x:site.x-9,y:topY,z,yaw:0},t+300+LIFT_MS+200),/Unphysical step/);
  let at=t+300+LIFT_MS+100;
  for(let x=site.x-19.3;x<=site.x-9.3;x+=.5)
    send(room,p,{type:'position',x,y:topY,z,yaw:0},at+=100);
  assert.throws(()=>send(room,q,{type:'space_hatch',open:true},at+100),/physically/);
  assert.throws(()=>send(room,p,{type:'space_hatch',open:true},at+100,()=>{throw Error('disk failed');}),/disk failed/);
  assert.equal(room.config.launchHatch,false);
  const open=send(room,p,{type:'space_hatch',open:true},at+200);
  assert.equal(open.hatchOpen,true);
  assert.equal(room.welcome(q,[p,q]).hatchOpen,true);
  assert.throws(()=>send(room,q,{type:'space_hatch',open:false},at+300),/physically/);
  const close=send(room,p,{type:'space_hatch',open:false},at+400);
  assert.equal(close.hatchOpen,false);
});

test('two room members claim two different cockpit seats only after boarding on foot',()=>{
  const {room,p,q,site}=setup();
  room.config.padVisited=[captain,friend]; // prior lift traversal is tested above
  const seat=(who,index,at)=>send(room,who,{type:'position',
    x:site.x+(index===0?-.4:1.4),y:site.y+44+1.58,z:site.z+5.35,yaw:0},at);
  seat(p,0,t);seat(q,1,t+100);
  assert.throws(()=>send(room,p,{type:'space_seat',action:'sit',index:1},t+200),/real cockpit/);
  assert.deepEqual(send(room,p,{type:'space_seat',action:'sit',index:0},t+300).seats,[p.id,'']);
  assert.deepEqual(send(room,q,{type:'space_seat',action:'sit',index:1},t+400).seats,[p.id,q.id]);
  assert.equal(publicConfig(room.config).shuttleSeats,undefined);
  assert(!JSON.stringify(room.welcome(q,[p,q])).includes(captain));
  assert.deepEqual(room.welcome(q,[p,q]).seats,[p.id,q.id]);
  assert.throws(()=>send(room,q,{type:'space_seat',action:'sit',index:0},t+500),/real cockpit|occupied/);
  assert.throws(()=>send(room,p,{type:'space_seat',action:'sit',index:0},t+600),/occupied/);
  assert.deepEqual(send(room,p,{type:'space_seat',action:'stand',index:0},t+700).seats,['',q.id]);
  room.disconnect(q,()=>{});
  assert.deepEqual(room.config.shuttleSeats,[null,null]);
});

test('disconnect releases the rider without moving a player or stranding the platform',()=>{
  const {room,p,q,site}=setup();position(room,p,site,t);
  lift(room,p,'up',t+100);
  const before={...room.config.launchLift};
  assert.throws(()=>room.disconnect(p,()=>{throw Error('disk unavailable');}),/disk unavailable/);
  assert.equal(room.config.launchLift.riderKey,captain,'failed write cannot change lift');
  room.disconnect(p,()=>{});
  assert.equal(room.config.launchLift.riderKey,null);
  assert.equal(room.config.launchLift.startedAt,before.startedAt,'motion stays continuous');
  position(room,q,site,t+100+LIFT_MS+100);
  assert.equal(lift(room,q,'recall',t+100+LIFT_MS+200).lift.direction,'down');
});

test('stale, distant, forged and failed commands never move the lift',()=>{
  const {room,p,site}=setup(),before=JSON.stringify(room.config.launchLift);
  position(room,p,site,t);
  assert.throws(()=>lift(room,p,'down',t+100),/stationary/);
  assert.throws(()=>lift(room,p,'up',t+1600),/position/);
  assert.equal(JSON.stringify(room.config.launchLift),before);
  position(room,p,site,t+1800);
  assert.throws(()=>lift(room,p,'up',t+1900,()=>{throw Error('disk error');}),/disk error/);
  assert.equal(JSON.stringify(room.config.launchLift),before);
  let out=lift(room,p,'up',t+2000);
  assert.equal(out.lift.riderId,p.id);
  assert.equal(out.lift.height,0);
  assert.throws(()=>lift(room,p,'up',t+2150),/unavailable/);
  const spy=newPeer('spy','Spy',intruder);
  position(room,spy,site,t+2200);
  assert.throws(()=>lift(room,spy,'exit',t+2300),/motion/);
  assert.throws(()=>send(room,spy,{type:'space_lift',action:'warp',height:25},t+2400),/position|motion|unavailable/);
  assert.equal(liftSnapshot(room.config.launchLift,t+2000+LIFT_MS).height,25);
});
