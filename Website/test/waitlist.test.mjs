import test from 'node:test';import assert from 'node:assert/strict';
import {handleWaitlist,validate,cleanNote,normalizeEmail} from '../src/waitlist.mjs';
import worker from '../src/worker.mjs';

const kv=()=>{const m=new Map();return {m,get:async k=>m.has(k)?m.get(k):null,put:async(k,v)=>{m.set(k,v);}};};
const post=(body,headers={'Content-Type':'application/json'})=>new Request('https://example.test/api/waitlist',{method:'POST',headers,body:typeof body==='string'?body:JSON.stringify(body)});
const good={email:'Player@Example.com ',consent:true,website:'',note:'',locale:'ru-RU'};

test('without a WAITLIST binding the endpoint says 503 and stores nothing',async()=>{
  const res=await handleWaitlist(post(good),{});
  assert.equal(res.status,503);
  assert.equal((await res.json()).ok,false);
});

test('valid submission is stored under the normalized address',async()=>{
  const store=kv();
  const res=await handleWaitlist(post(good),{WAITLIST:store});
  assert.equal(res.status,200);
  assert.equal(res.headers.get('Cache-Control'),'no-store');
  const saved=JSON.parse(store.m.get('email:player@example.com'));
  assert.equal(saved.email,'player@example.com');
  assert.equal(saved.locale,'ru-RU');
  assert.ok(!Number.isNaN(Date.parse(saved.createdAt)));
});

test('repeat address answers the same way and is not overwritten',async()=>{
  const store=kv();
  await handleWaitlist(post(good),{WAITLIST:store});
  const first=store.m.get('email:player@example.com');
  const res=await handleWaitlist(post({...good,note:'другая заметка'}),{WAITLIST:store});
  assert.equal(res.status,200);
  assert.equal(store.m.get('email:player@example.com'),first);
});

test('honeypot filled: success answer, nothing stored',async()=>{
  const store=kv();
  const res=await handleWaitlist(post({...good,website:'http://spam.example'}),{WAITLIST:store});
  assert.equal(res.status,200);
  assert.equal(store.m.size,0);
});

test('invalid addresses are rejected',async()=>{
  for(const email of ['', 'no-at-sign', 'a@b', 'a b@example.com', 'x@example.com<script>', 'a'.repeat(250)+'@example.com'])
    assert.equal((await handleWaitlist(post({...good,email}),{WAITLIST:kv()})).status,400,email);
});

test('consent must be exactly true',async()=>{
  for(const consent of [false, 'true', 1, undefined])
    assert.equal((await handleWaitlist(post({...good,consent}),{WAITLIST:kv()})).status,400,String(consent));
});

test('wrong content type, bad JSON, oversized body and wrong method are refused',async()=>{
  assert.equal((await handleWaitlist(post(JSON.stringify(good),{'Content-Type':'text/plain'}),{WAITLIST:kv()})).status,415);
  assert.equal((await handleWaitlist(post('{not json'),{WAITLIST:kv()})).status,400);
  assert.equal((await handleWaitlist(post('x'.repeat(3000)),{WAITLIST:kv()})).status,413);
  assert.equal((await handleWaitlist(new Request('https://example.test/api/waitlist',{method:'GET'}),{WAITLIST:kv()})).status,405);
  assert.equal((await handleWaitlist(new Request('https://example.test/api/waitlist',{method:'PUT',headers:{'Content-Type':'application/json'},body:'{}'}),{WAITLIST:kv()})).status,405);
});

test('note is single-line, trimmed and capped at 500 characters',async()=>{
  assert.equal(cleanNote('  Пресса\n\tтест\u0007 '),'Пресса тест');
  assert.equal(cleanNote('я'.repeat(900)).length,500);
  const store=kv();
  await handleWaitlist(post({...good,note:'Удаление\u0000'.repeat(1)}),{WAITLIST:store});
  assert.equal(JSON.parse(store.m.get('email:player@example.com')).note,'Удаление');
});

test('validate and normalizeEmail basics',async()=>{
  assert.equal(normalizeEmail('  A@B.RU '),'a@b.ru');
  assert.equal(validate(null).error !== undefined,true);
  assert.equal(validate({...good,email:'ok@mail.ru'}).error,undefined);
});

test('worker routes /api/waitlist to the handler and keeps other API paths unchanged',async()=>{
  const res=await worker.fetch(new Request('https://example.test/api/waitlist',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(good)}),{});
  assert.equal(res.status,503);
  const other=await worker.fetch(new Request('https://example.test/api/unknown',{method:'POST'}),{});
  assert.equal(other.status,405);
});
