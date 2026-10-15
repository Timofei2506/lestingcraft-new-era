import test from 'node:test';import assert from 'node:assert/strict';import {launcherFeed,archiveHost} from '../src/launcher.mjs';
const env={LAUNCHER_READER:'server-only-secret',LAUNCHER_ACCESS_KEY:'invite'};
const request=key=>new Request('https://example.test/api/launcher/latest',{headers:key?{Authorization:'Bearer '+key}:{}});
test('launcher fails closed without configuration',async()=>assert.equal((await launcherFeed(request(),{})).status,503));
test('launcher denies absent and wrong invitation before touching GitHub',async()=>{for(const key of [null,'wrong'])assert.equal((await launcherFeed(request(key),env,()=>{throw Error('must not fetch');})).status,401);});
test('launcher returns no source tokens; only successful non-expired smoke build',async()=>{
 const fetcher=async(url,opt)=>{
  assert.equal(opt.headers.Authorization,'Bearer server-only-secret');
  if(url.includes('/workflows/'))return Response.json({workflow_runs:[{id:42,head_sha:'a'.repeat(40),head_branch:'main',conclusion:'success',event:'workflow_dispatch'}]});
  if(url.includes('/runs/'))return Response.json({artifacts:[{name:'build-StandaloneWindows64',id:1,size_in_bytes:8,expired:false},{name:'launcher-manifest',id:2,size_in_bytes:8,expired:false}]});
  return new Response(null,{status:302,headers:{Location:'https://fixture.blob.core.windows.net/archive?sig=short-lived'}});
 };
 const response=await launcherFeed(request('invite'),env,fetcher);assert.equal(response.status,200);const text=await response.text();assert.ok(!text.includes('server-only-secret'));assert.ok(!text.includes('Bearer'));assert.equal(JSON.parse(text).build,'a'.repeat(40));assert.equal(response.headers.get('Cache-Control'),'no-store');
});
test('launcher does not publish builds lacking a verification manifest',async()=>{
 const f=async url=>Response.json(url.includes('/workflows/')?{workflow_runs:[{id:1,head_sha:'a'.repeat(40),head_branch:'main',conclusion:'success',event:'workflow_dispatch'}]}:{artifacts:[]});assert.equal((await launcherFeed(request('invite'),env,f)).status,503);
});
test('artifact URLs must use HTTPS and trusted Azure/GitHub artifact hosts',()=>{assert.ok(archiveHost('https://x.blob.core.windows.net/y'));for(const bad of ['http://x.blob.core.windows.net/y','https://x.blob.core.windows.net.evil.test','https://user@x.blob.core.windows.net/y','https://evil.test/y'])assert.equal(archiveHost(bad),false);});
