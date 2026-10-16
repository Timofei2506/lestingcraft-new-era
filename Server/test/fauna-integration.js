// Actual WebSocket / filesystem integration, NOT a Unity graphical test.
import assert from 'node:assert/strict';import fs from 'node:fs';import WebSocket from 'ws';
import {animalHeight} from '../src/wildlife.js';
const rules=Number(process.env.TEST_RULES||5);
const endpoint=process.env.TEST_ENDPOINT||'http://127.0.0.1:8798',receipt=process.env.TEST_RECEIPT||'/tmp/lc-fauna-receipt.json';
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
for(let i=0;;i++){try{if((await fetch(endpoint+'/health')).ok)break;}catch{}if(i>=50)throw Error('Server did not start');await delay(100);}
function connect(code,name,key,rulesVersion=rules){
 const ws=new WebSocket(endpoint.replace(/^http/,'ws')+`/api/rooms/${code}/ws?name=${name}&key=${key}&clientRules=${rulesVersion}`),queue=[],waiters=[];
 ws.on('error',()=>{});ws.on('message',raw=>{const packet=JSON.parse(raw),i=waiters.findIndex(w=>w.predicate(packet));if(i<0)queue.push(packet);else{const [w]=waiters.splice(i,1);clearTimeout(w.timer);w.resolve(packet);}});
 return {ws,next(test){const predicate=typeof test==='string'?p=>p.type===test:test,i=queue.findIndex(predicate);if(i>=0)return Promise.resolve(queue.splice(i,1)[0]);return new Promise((resolve,reject)=>waiters.push({predicate,resolve,timer:setTimeout(()=>reject(Error('Packet timeout')),5000)}));},send(packet){ws.send(JSON.stringify(packet));},close(){if(ws.readyState===3)return Promise.resolve();return new Promise(resolve=>{const timer=setTimeout(()=>ws.terminate(),1000);ws.once('close',()=>{clearTimeout(timer);resolve();});ws.close();});}};
}
if(process.argv.includes('--reconnect')){
 const {code,key,animal}=JSON.parse(fs.readFileSync(receipt)),a=connect(code,'Alice',key);
 try{const w=await a.next('welcome');assert.equal(w.loot.length,0);assert.equal(w.animals.some(q=>q.id===animal),false);assert.equal(w.save.inventory.slots[0].item,135);assert.equal(w.save.inventory.slots[0].count,1);assert.deepEqual(w.save.claimedAnimalLoot,[animal]);console.log('PASS: full server process restart restores dead animal, private inventory and committed loot claim without respawn or duplicate reward');}finally{await a.close();}
}else{
 const response=await fetch(endpoint+'/api/rooms',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({version:1,rulesVersion:rules,mode:0,seed:2506,dimension:0})});assert.equal(response.status,201);const {code}=await response.json(),key='a'.repeat(32),bobKey='b'.repeat(32);let a=connect(code,'Alice',key),b=connect(code,'Bob',bobKey);
 try{
  const wa=await a.next('welcome');await b.next('welcome');assert.equal(wa.contentVersion,rules>=6?3:2);
  const old=connect(code,'Old','c'.repeat(32),rules>=6?5:4);await new Promise((resolve,reject)=>{old.ws.once('unexpected-response',(req,res)=>{try{assert.equal(res.statusCode,409);res.resume();req.destroy();resolve();}catch(e){reject(e);}});old.ws.once('open',()=>reject(Error('Old client admitted')));});
  a.send({type:'position',x:0,y:46.62,z:0,yaw:0});const snapshot=await a.next(p=>p.type==='player'&&p.id===wa.id&&p.animals);assert.deepEqual(new Set(snapshot.animals.map(a=>a.species)),new Set([0,1,2]));
  const hare=snapshot.animals.find(a=>a.species===1),position={type:'position',x:hare.x,y:hare.y+animalHeight(1)*.5+.7,z:hare.z,yaw:0};await delay(100);a.send(position);await b.next(p=>p.type==='player'&&p.id===wa.id&&p.y===position.y);
  a.send({type:'animal_hit',animal:hare.id,weapon:103,requestId:71});const hit=await a.next('animal_hit');assert.equal(hit.requestId,71);assert.equal(hit.loot.length,1);assert.equal(hit.loot[0].meat,1);assert.equal(hit.animals.some(a=>a.id===hare.id),false);await b.next('animal_hit');
  b.send(position);await delay(100);b.send({type:'animal_hit',animal:hare.id,weapon:103,requestId:72});assert.match((await b.next('error')).message,/available/);
  await a.close();a=connect(code,'Alice',key);const pending=await a.next('welcome');assert.equal(pending.loot.length,1);assert.equal(pending.loot[0].id,hare.id);assert.equal(pending.loot[0].meat,1);assert.equal('wildlife' in pending,false);assert.ok(!JSON.stringify(pending).includes(bobKey));
  await b.close();b=connect(code,'Bob',bobKey);assert.equal((await b.next('welcome')).loot.length,0);
  const save={claimedAnimalLoot:[hare.id],inventory:{selected:0,slots:Array.from({length:45},(_,i)=>({item:i===0?135:i===1?136:0,count:i<2?1:0,durability:0}))},survival:{health:100,hunger:90,stamina:100,temperature:37,wetness:0}};
  a.send({type:'state',save});await a.next('state_saved');await a.close();a=connect(code,'Alice',key);const committed=await a.next('welcome');assert.equal(committed.loot.length,0);assert.equal(committed.save.inventory.slots[0].count,1);fs.writeFileSync(receipt,JSON.stringify({code,key,animal:hare.id}));
  console.log('PASS rules '+rules+': actual two-client WebSocket fauna sync including sender, older-client rejection, authoritative single kill, contested hit rejection, pending loot replay before save, private committed loot/inventory reconnect; receipt ready for process-restart check');
 }finally{await Promise.all([a.close(),b.close()]);}
}
