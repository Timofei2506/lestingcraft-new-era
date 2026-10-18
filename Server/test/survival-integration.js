import assert from 'node:assert/strict';import WebSocket from 'ws';
const endpoint=process.env.TEST_ENDPOINT||'http://127.0.0.1:8787';
function connect(code,name,key){
 const ws=new WebSocket(endpoint.replace(/^http/,'ws')+`/api/rooms/${code}/ws?name=${name}&key=${key}`),queue=[],waiters=[];
 ws.on('message',raw=>{let packet=JSON.parse(raw),i=waiters.findIndex(w=>w.type===packet.type);if(i<0)queue.push(packet);else{const [w]=waiters.splice(i,1);clearTimeout(w.timer);w.resolve(packet);}});ws.on('error',()=>{});
 return {ws,next(type){let i=queue.findIndex(p=>p.type===type);if(i>=0)return Promise.resolve(queue.splice(i,1)[0]);return new Promise((resolve,reject)=>waiters.push({type,resolve,timer:setTimeout(()=>reject(Error('Timeout '+type)),10000)}));},close(){if(ws.readyState===WebSocket.CLOSED)return Promise.resolve();return new Promise(resolve=>{const timer=setTimeout(()=>ws.terminate(),3000);ws.once('close',()=>{clearTimeout(timer);resolve();});ws.close();});}};
}
const makeSave=()=>({milestones:1,inventory:{selected:0,revision:1,slots:Array.from({length:45},(_,i)=>({item:i===0?4:0,count:i===0?8:0,durability:0}))},survival:{health:100,hunger:61,stamina:85,temperature:36,wetness:.5},equippedCoat:false,grave:[]});
async function create(mode){const r=await fetch(endpoint+'/api/rooms',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({version:1,rulesVersion:2,mode,seed:2506,dimension:0})});assert.equal(r.status,201);return (await r.json()).code;}
const code=await create(1),key='a'.repeat(32),bkey='b'.repeat(32),b=connect(code,'Bob',bkey);let a=connect(code,'Alice',key);
try{
 const wa=await a.next('welcome');await b.next('welcome');assert.equal(wa.mode,1);assert.equal(wa.dimension,0);
 a.ws.send(JSON.stringify({type:'state',save:makeSave()}));assert.equal((await a.next('state_saved')).campaign.milestones,1);const notice=await b.next('state_saved');assert.equal(notice.save,undefined);
 await a.close();a=connect(code,'Alice',key);const restored=await a.next('welcome');assert.equal(restored.save.inventory.slots[0].count,8);assert.equal(restored.save.survival.hunger,61);assert.equal(restored.campaign.milestones,1);
 const death=makeSave();death.survival.health=0;a.ws.send(JSON.stringify({type:'state',save:death}));assert.equal((await a.next('run_ended')).runEnded,true);assert.equal((await b.next('run_ended')).runEnded,true);
 a.ws.send(JSON.stringify({type:'edit',x:0,y:40,z:0,block:1}));assert.match((await a.next('error')).message,/ended/);
 await a.close();a=connect(code,'Alice',key);assert.equal((await a.next('welcome')).runEnded,true);
 console.log('PASS: rules v2, modes, inventory/stats reconnect, private save, shared milestones, team hardcore end and reconnect lock');
}finally{await Promise.all([a.close(),b.close()]);}
const ordinary=await create(0),c=connect(ordinary,'Charlie','c'.repeat(32));
try{await c.next('welcome');const death=makeSave();death.survival.health=0;death.grave=death.inventory.slots;death.graveX=1;death.graveY=42;death.graveZ=1;death.inventory={selected:0,revision:2,slots:Array.from({length:45},()=>({item:0,count:0,durability:0}))};c.ws.send(JSON.stringify({type:'state',save:death}));assert.equal((await c.next('state_saved')).campaign.runEnded,false);await c.close();const d=connect(ordinary,'Charlie','c'.repeat(32));try{const w=await d.next('welcome');assert.equal(w.save.grave[0].count,8);assert.equal(w.save.survival.dead,true);assert.equal(w.runEnded,false);}finally{await d.close();}console.log('PASS: ordinary survival death and persistent personal grave');}finally{if(c.ws.readyState<2)await c.close();}
