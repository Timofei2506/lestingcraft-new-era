import {test} from 'node:test';
import assert from 'node:assert/strict';
import {launchSite,PAD_X,PAD_Z,RAMP_START_Z,LIFT_X} from '../src/launch-site.js';
import {height} from '../src/terrain-legacy.js';
import {createConfig,RoomState,publicConfig,newPeer,acceptsClient} from '../src/protocol.js';
const owner='a'.repeat(32);

test('approved pad is anchored above the entire deterministic ground and approach',()=>{
  for(const seed of [0,1,2506,-7712]){
    const site=launchSite(seed);
    assert.deepEqual(site,{active:true,x:PAD_X,y:site.y,z:PAD_Z});
    assert.ok(site.y>height(LIFT_X,PAD_Z+3,seed));
    assert.ok(site.y+5<256 && site.y+80<240,'full stack fits the playable room height');
    for(let x=PAD_X-47;x<=PAD_X+46;x++)for(let z=PAD_Z-45;z<=PAD_Z+44;z++)
      assert.ok(height(x,z,seed)+8<=site.y+4,'vegetation cannot pierce pad ground');
    for(let z=RAMP_START_Z;z<=PAD_Z+3;z++)for(let x=LIFT_X-2;x<=LIFT_X+2;x++)
      assert.ok(height(x,z,seed)+8<=site.y+4,'approach elevation fits terrain');
    site.y=-1;
    assert.notEqual(launchSite(seed).y,-1,'callers cannot change cached site');
  }
});

test('room chooses site, ignores client supplied coordinates and preserves it on reconnect',()=>{
  const config=createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:11,
    ownerKey:owner,launchSite:{x:0,y:1,z:0}});
  assert.deepEqual(config.launchSite,launchSite(2506));
  const peer=newPeer('one','Player',owner),room=new RoomState(config);
  assert.deepEqual(room.welcome(peer,[]).launchSite,config.launchSite);
  assert.deepEqual(publicConfig(config).launchSite,config.launchSite);
  assert.deepEqual(new RoomState(JSON.parse(JSON.stringify(config))).welcome(peer,[]).launchSite,config.launchSite);
  const old=createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:10,ownerKey:owner});
  assert.deepEqual(createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:11,ownerKey:owner}).launchSite,config.launchSite,'old ground rooms keep their fixed pad');
  assert.equal(old.launchSite,undefined,'old rules10 room topology is unchanged');
  assert.equal(acceptsClient(config,10),false,'older clients cannot join a visible site they cannot render');
  assert.equal(acceptsClient(config,11),true);
  assert.equal(acceptsClient(old,10),true,'old rooms remain joinable');
});

test('creative ground blueprint relocates one real pad and shuttle, persists atomically for both peers',()=>{
  const config=createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:12,ownerKey:owner});
  config.started=true;
  const room=new RoomState(config),now=100000;
  const ground=height(0,0,2506),one=newPeer('one','Builder',owner),two=newPeer('two','Friend','b'.repeat(32));
  Object.assign(one,{x:.5,y:ground+2.58,z:.5,positioned:true,lastPosition:now-100});
  room.setMembers([one,two]);
  let saved;
  const result=room.handle(one,JSON.stringify({type:'space_site_place',action:'pad',x:0,y:ground,z:0}),now,()=>{},()=>{},value=>{saved=JSON.parse(JSON.stringify(value));});
  assert.deepEqual(result.launchSite,launchSite(2506,20,110));
  assert.deepEqual(result.launchSite,room.config.launchSite);
  assert.deepEqual(saved.launchSite,result.launchSite);
  assert.deepEqual(room.welcome(two,[one]).launchSite,result.launchSite);
  assert.deepEqual(new RoomState(saved).welcome(two,[one]).launchSite,result.launchSite,'reconnect and durable rehydration use the same approved location');
  assert.equal(result.lift.height,0);
  assert.equal(acceptsClient(room.config,11),false,'ground-only Windows 11 build must not see a relocated pad');
  assert.equal(acceptsClient(room.config,12),true);
});

test('creative placement rejects forged altitude, distance, mode and occupied relocation',()=>{
  const config=createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:12,ownerKey:owner});
  config.started=true;
  const room=new RoomState(config),now=100000,ground=height(0,0,2506),peer=newPeer('one','Builder',owner);
  Object.assign(peer,{x:.5,y:ground+2.58,z:.5,positioned:true,lastPosition:now-100});room.setMembers([peer]);
  const send=(p,t=now)=>room.handle(peer,JSON.stringify({type:'space_site_place',action:'shuttle',x:0,y:ground,z:0,...p}),t,()=>{},()=>{},()=>{});
  assert.throws(()=>send({x:100}),/nearby ground/);
  assert.throws(()=>send({y:ground+15}),/natural ground/);
  assert.throws(()=>send({action:'launch'}),/nearby ground/);
  assert.throws(()=>send({z:300}),/nearby ground/);
  assert.throws(()=>send({},now+2000),/nearby ground/);
  assert.deepEqual(room.config.launchSite,launchSite(2506),'failed requests cannot move the existing stack');
  const survival=createConfig({version:1,seed:2506,dimension:0,mode:0,rulesVersion:12,ownerKey:owner});survival.started=true;
  assert.throws(()=>new RoomState(survival).handle(peer,JSON.stringify({type:'space_site_place',action:'pad',x:0,y:ground,z:0}),now,()=>{},()=>{},()=>{}),/nearby ground/);
  room.config.padVisited=['another'];
  assert.throws(()=>send({}),/relocation is locked/);
  assert.deepEqual(room.config.launchSite,launchSite(2506));
  assert.throws(()=>launchSite(2506,321,110),/outside room/);
  const original=room.config.launchSite;
  room.config.padVisited=[];
  assert.throws(()=>room.handle(peer,JSON.stringify({type:'space_site_place',action:'pad',x:0,y:ground,z:0}),now,()=>{},()=>{},()=>{throw Error('storage unavailable');}),/storage unavailable/);
  assert.deepEqual(room.config.launchSite,original,'failed persistence cannot relocate an occupied client');
});
