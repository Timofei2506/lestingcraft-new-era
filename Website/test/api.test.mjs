import {test} from 'node:test';import assert from 'node:assert/strict';import {readyReleases,REPO} from '../src/api.mjs';
const asset=name=>({name,state:'uploaded',size:1024,browser_download_url:`https://github.com/${REPO}/releases/download/v1/${name}`});
const release=()=>({draft:false,prerelease:false,tag_name:'v1',name:'Version 1',assets:[asset('build-StandaloneWindows64.zip'),asset('build-WebGL.zip')]});
test('only stable complete downloadable builds are shown',()=>{assert.equal(readyReleases([release()]).length,1);for(const override of [{draft:true},{prerelease:true},{assets:[]},{assets:[asset('source.zip')]}])assert.equal(readyReleases([{...release(),...override}]).length,0);});
test('no executable download when one platform is missing',()=>assert.equal(readyReleases([{...release(),assets:[asset('build-WebGL.zip')]}]).length,0));
test('rejects foreign or empty assets',()=>{const r=release();r.assets[0].browser_download_url='https://malicious.example/file.zip';assert.equal(readyReleases([r]).length,0);r.assets[0]=asset('build-StandaloneWindows64.zip');r.assets[0].size=0;assert.equal(readyReleases([r]).length,0);});
test('no releases is an honest empty state',()=>assert.deepEqual(readyReleases([]),[]));
test('invalid upstream response throws instead of pretending no releases',()=>assert.throws(()=>readyReleases({error:'rate limit'})));
