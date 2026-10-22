import {test} from 'node:test';
import assert from 'node:assert/strict';
import {api,readyReleases,REPO} from '../src/api.mjs';

const asset=name=>({name,state:'uploaded',size:1024,browser_download_url:`https://github.com/${REPO}/releases/download/v1/${name}`,digest:'sha256:abc123'});
const release=()=>({draft:false,prerelease:false,tag_name:'v1',name:'Version 1',published_at:'2026-10-22T00:00:00Z',assets:[asset('build-StandaloneWindows64.zip'),asset('LestingCraft-Android-arm64.apk')]});

test('only published releases with both direct-download platforms are shown',()=>{
  assert.equal(readyReleases([release()]).length,1);
  for(const override of [
    {draft:true},
    {assets:[]},
    {assets:[asset('build-StandaloneWindows64.zip')]},
    {assets:[asset('LestingCraft-Android-arm64.apk')]},
    {assets:[asset('source.zip')]}
  ]) assert.equal(readyReleases([{...release(),...override}]).length,0);
});

test('pre-release builds are visible and marked as such',()=>{
  const [ready]=readyReleases([{...release(),prerelease:true}]);
  assert.equal(ready.prerelease,true);
  assert.equal(ready.windows.name,'build-StandaloneWindows64.zip');
  assert.equal(ready.android.name,'LestingCraft-Android-arm64.apk');
});

test('unrelated assets are ignored and both expected assets remain present',()=>{
  const [ready]=readyReleases([{...release(),assets:[...release().assets,asset('build-WebGL.zip')]}]);
  assert.ok(ready.windows);
  assert.ok(ready.android);
  assert.equal(ready.webgl,undefined);
});

test('rejects foreign, empty, or unuploaded platform assets',()=>{
  const r=release();
  r.assets[0].browser_download_url='https://malicious.example/file.zip';
  assert.equal(readyReleases([r]).length,0);
  const sizeZero=release();sizeZero.assets[1].size=0;
  assert.equal(readyReleases([sizeZero]).length,0);
  const notUploaded=release();notUploaded.assets[1].state='new';
  assert.equal(readyReleases([notUploaded]).length,0);
});

test('no releases is an honest empty state',()=>assert.deepEqual(readyReleases([]),[]));
test('invalid upstream response throws instead of pretending no releases',()=>assert.throws(()=>readyReleases({error:'rate limit'})));

test('public cooperative status omits the server URL',async()=>{
  const result=await api('/api/status',{COOP:{fetch:async()=>Response.json({ok:true,version:1})}});
  assert.deepEqual(Object.keys(result).sort(),['checkedAt','online']);
  assert.equal(result.online,true);
  assert.doesNotMatch(JSON.stringify(result),/workers\.dev|lestingcraft-coop/i);
});
