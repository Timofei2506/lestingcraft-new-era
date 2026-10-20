import {test} from 'node:test';
import assert from 'node:assert/strict';
import {launchSite,PAD_X,PAD_Z,RAMP_START_Z,LIFT_X} from '../src/launch-site.js';
import {height} from '../src/terrain-legacy.js';
import {createConfig,RoomState,publicConfig,newPeer} from '../src/protocol.js';
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
  const config=createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:10,
    ownerKey:owner,launchSite:{x:0,y:1,z:0}});
  assert.deepEqual(config.launchSite,launchSite(2506));
  const peer=newPeer('one','Player',owner),room=new RoomState(config);
  assert.deepEqual(room.welcome(peer,[]).launchSite,config.launchSite);
  assert.deepEqual(publicConfig(config).launchSite,config.launchSite);
  assert.deepEqual(new RoomState(JSON.parse(JSON.stringify(config))).welcome(peer,[]).launchSite,config.launchSite);
  const old=createConfig({version:1,seed:2506,dimension:0,mode:2,rulesVersion:9,ownerKey:owner});
  assert.equal(old.launchSite,undefined,'old content topology is unchanged');
});
