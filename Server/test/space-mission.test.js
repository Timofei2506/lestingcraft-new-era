import {test} from 'node:test';
import assert from 'node:assert/strict';
import {beginMoonMission,advanceMission,setCruiseAutopilot,landMission,missionView,MOON_SECONDS,COUNTDOWN_MS,MOON_DISTANCE,EARTH_X} from '../src/space-mission.js';
const t=1000000;
const start=()=>beginMoonMission({crew:['a','b'],boarded:['a','b'],pilot:'a',now:t});
const ignition=t+COUNTDOWN_MS;

test('requires two unique members of the same boarded crew, not just a pilot',()=>{
  assert.throws(()=>beginMoonMission({crew:['a','b'],boarded:['a'],pilot:'a',now:t}),/boarded/);
  assert.throws(()=>beginMoonMission({crew:['a','a'],boarded:['a'],pilot:'a',now:t}),/distinct/);
  assert.throws(()=>beginMoonMission({crew:['a'],boarded:['a'],pilot:'a',now:t}),/two/);
  assert.throws(()=>beginMoonMission({crew:['a','b'],boarded:['a','b'],pilot:'z',now:t}),/pilot/);
  assert.equal(missionView(start(),t).phase,'countdown');
});

test('countdown, 630-second ascent, 990-second cruise and 180-second approach are server-timed',()=>{
  let m=start();
  assert.equal(advanceMission(m,ignition-1).position[0],EARTH_X);
  m=advanceMission(m,ignition+629000);
  assert.equal(missionView(m,ignition+629000).phase,'ascent');
  assert.ok(m.position[0]>EARTH_X && m.position[0]<EARTH_X+MOON_DISTANCE);
  m=advanceMission(m,ignition+630000);
  assert.equal(missionView(m,ignition+630000).phase,'cruise');
  m=advanceMission(m,ignition+1619999);
  assert.equal(missionView(m,ignition+1619999).phase,'cruise');
  m=advanceMission(m,ignition+1620000);
  assert.equal(missionView(m,ignition+1620000).phase,'approach');
  m=advanceMission(m,ignition+MOON_SECONDS*1000);
  assert.equal(missionView(m,ignition+MOON_SECONDS*1000).phase,'landing-ready');
  assert.ok(Math.abs(m.position[0]-(EARTH_X+MOON_DISTANCE))<1);
  assert.throws(()=>landMission(m,'a',ignition+MOON_SECONDS*1000),/landing/);
  assert.equal(m.landingBurn,0);
});

test('only the pilot changes cruise mode or steers; no bypass of time or landing checks',()=>{
  let m=advanceMission(start(),ignition+630000);
  assert.throws(()=>setCruiseAutopilot(m,'b',true,ignition+630000),/Pilot/);
  assert.throws(()=>advanceMission(m,ignition+631000,{player:'b',heading:[0,0,1]}),/pilot/);
  assert.throws(()=>advanceMission(m,ignition+631000,{player:'b',landingThrottle:1}),/pilot/);
  assert.throws(()=>advanceMission(m,ignition+629000),/Invalid/);
  assert.throws(()=>advanceMission(m,ignition+631000,{player:'a',heading:[Number.NaN,0,1]}),/Invalid/);
  m=advanceMission(m,ignition+631000,{player:'a',heading:[0,0,1]});
  assert.ok(m.position[2]>0,'manual flight changes the actual position');
  m=advanceMission(m,ignition+632000);
  assert.ok(m.position[2]>0,'coasting does not secretly engage guidance');
  const manualOffset=Math.abs(m.position[2]);
  m=setCruiseAutopilot(m,'a',true,ignition+632000);
  assert.equal(m.autopilot,true);
  m=advanceMission(m,ignition+1620000);
  assert.ok(Math.abs(m.position[2])<manualOffset,'autopilot is correcting the course');
  assert.throws(()=>landMission(m,'a',ignition+1620000),/landing/);
});

test('a single huge update cannot counterfeit 180 seconds of manual braking',()=>{
  const m=advanceMission(start(),ignition+1800000,{player:'a',landingThrottle:1});
  assert.equal(m.landingBurn,0);
  assert.throws(()=>landMission(m,'a',ignition+1800000),/landing/);
});

test('manual braking at the right time allows only the pilot to land after half an hour',()=>{
  let m=advanceMission(start(),ignition+1620000);
  for(let n=1;n<=180;n++){
    m=advanceMission(m,ignition+(1620+n)*1000,{player:'a',landingThrottle:1});
    if(n<180)assert.throws(()=>landMission(m,'a',ignition+(1620+n)*1000),/landing/);
  }
  assert.equal(m.landingBurn,180);
  assert.throws(()=>landMission(m,'b',ignition+1800000),/landing/);
  m=landMission(m,'a',ignition+1800000);
  assert.equal(missionView(m,ignition+1800000).phase,'landed');
  assert.deepEqual(m.crew,['a','b']);
  assert.throws(()=>landMission(m,'a',ignition+1800001),/landing/);
});

test('saved mission resumes without advancing on restart or losing its shared crew',()=>{
  let m=advanceMission(start(),ignition+700000);
  const persisted=JSON.parse(JSON.stringify(m));
  assert.deepEqual(missionView(persisted,ignition+700000).crew,['a','b']);
  m=advanceMission(persisted,ignition+800000);
  assert.equal(missionView(m,ignition+800000).phase,'cruise');
  assert.ok(m.position[0]>persisted.position[0]);
  assert.equal(missionView(m,ignition+800000).elapsed,800);
});
