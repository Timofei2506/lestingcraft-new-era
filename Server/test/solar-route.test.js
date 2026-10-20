import {test} from 'node:test';
import assert from 'node:assert/strict';
import {BODY_POSITIONS,MOON_SECONDS,COUNTDOWN_MS,beginSolarMission,beginReturnLeg,
    advanceMission,landMission,missionView} from '../src/space-mission.js';
const now=1_000_000;
const crew=['captain','friend'];
const boarded=[...crew];
const launch=to=>beginSolarMission({crew,boarded,pilot:'captain',now,from:'earth',to});
const distance=(a,b)=>Math.hypot(...a.map((n,i)=>n-b[i]));

test('each approved first-release destination has one continuous route, never a sector jump',()=>{
    for(const destination of ['moon','mars','venus','mercury']){
        const m=launch(destination),ignition=m.ignitionAt;
        assert.deepEqual(m.crew,crew);
        assert.deepEqual(m.position,BODY_POSITIONS.earth);
        assert.deepEqual(m.target,BODY_POSITIONS[destination]);
        assert.equal(m.to,destination);
        assert.ok(m.seconds>=MOON_SECONDS);
        assert.equal(destination==='moon',m.seconds===MOON_SECONDS);
        const before=advanceMission(m,ignition-1);
        assert.deepEqual(before.position,BODY_POSITIONS.earth);
        const mid=advanceMission(before,ignition+Math.floor(m.seconds/2)*1000);
        assert.ok(distance(mid.position,m.origin)>0);
        assert.ok(distance(mid.position,m.target)>0);
        assert.ok(distance(mid.position,m.target)<distance(m.origin,m.target));
        assert.deepEqual(missionView(mid,mid.lastAt).crew,crew);
        const end=advanceMission(mid,ignition+m.seconds*1000);
        assert.ok(distance(end.position,end.target)<1,`${destination} end is continuous`);
        assert.throws(()=>landMission(end,'captain',end.lastAt),/Manual landing/);
        assert.deepEqual(m.position,m.origin,'mission state is immutable');
    }
});

test('landing and return require the same boarded crew and retain the actual ship position',()=>{
    let m=launch('moon');const ignition=m.ignitionAt;
    m=advanceMission(m,ignition+(m.seconds-180)*1000);
    for(let i=1;i<=20;i++)m=advanceMission(m,ignition+(m.seconds-180+i)*1000,{player:'captain',landingThrottle:1});
    m=advanceMission(m,ignition+m.seconds*1000);
    assert.throws(()=>landMission(m,'friend',m.lastAt),/Manual landing/);
    m=landMission(m,'captain',m.lastAt);
    assert.throws(()=>beginReturnLeg(m,{boarded:['captain'],pilot:'captain',now:m.lastAt+1000,to:'earth'}),/boarded/);
    assert.throws(()=>beginReturnLeg(m,{boarded,pilot:'captain',now:m.lastAt+1000,to:'moon'}),/route/);
    const returnLeg=beginReturnLeg(m,{boarded,pilot:'captain',now:m.lastAt+1000,to:'earth'});
    assert.deepEqual(returnLeg.position,m.position);
    assert.deepEqual(returnLeg.origin,m.position);
    assert.deepEqual(returnLeg.target,BODY_POSITIONS.earth);
    assert.equal(returnLeg.seconds,MOON_SECONDS);
    assert.deepEqual(returnLeg.crew,crew);
    assert.throws(()=>beginReturnLeg(returnLeg,{boarded,pilot:'captain',now:returnLeg.lastAt+1000,to:'earth'}),/landed/);
});

test('invalid destinations, early clock, reboarding gaps and unlanded return are rejected',()=>{
    assert.throws(()=>launch('jupiter'),/route/);
    assert.throws(()=>beginSolarMission({crew,boarded,pilot:'captain',now,from:'earth',to:'earth'}),/route/);
    assert.throws(()=>beginSolarMission({crew,boarded:['friend'],pilot:'captain',now,from:'earth',to:'mars'}),/boarded/);
    const m=launch('mercury');
    assert.throws(()=>beginReturnLeg(m,{boarded,pilot:'captain',now,to:'earth'}),/landed/);
    assert.throws(()=>advanceMission(m,m.lastAt-1),/Invalid/);
});
