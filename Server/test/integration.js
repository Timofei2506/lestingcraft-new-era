import assert from 'node:assert/strict';
import WebSocket from 'ws';
const endpoint=process.env.TEST_ENDPOINT||'http://127.0.0.1:8787';
const health=await(await fetch(endpoint+'/health')).json();assert.equal(health.version,1);
const response=await fetch(endpoint+'/api/rooms',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({version:1,seed:2506,dimension:0})});
assert.equal(response.status,201);const {code}=await response.json();assert.equal(code.length,32);
const wsUrl=endpoint.replace(/^http/,'ws')+`/api/rooms/${code}/ws`;
function connect(name) {
 const ws=new WebSocket(wsUrl+'?name='+name),queue=[],waiters=[];
 ws.on('message',raw=>{const p=JSON.parse(raw),i=waiters.findIndex(w=>w.type===p.type);if(i>=0){const[w]=waiters.splice(i,1);clearTimeout(w.timer);w.resolve(p);}else queue.push(p);});ws.on('error',()=>{});
 function next(type){const i=queue.findIndex(p=>p.type===type);if(i>=0)return Promise.resolve(queue.splice(i,1)[0]);return new Promise((resolve,reject)=>{waiters.push({type,resolve,reject,timer:setTimeout(()=>reject(new Error('Timeout waiting for '+type)),10000)});});}
 return {ws,next};
}
const a=connect('Alice'),b=connect('Bob');
try {
 const[wa,wb]=await Promise.all([a.next('welcome'),b.next('welcome')]);assert.equal(wa.seed,2506);assert.equal(wa.players.length,0);assert.notEqual(wa.id,wb.id);
 a.ws.send(JSON.stringify({type:'position',x:1,y:42,z:1,yaw:10}));assert.equal((await b.next('player')).id,wa.id);
 a.ws.send(JSON.stringify({type:'edit',x:1,y:41,z:1,block:4}));const[ea,eb]=await Promise.all([a.next('edit'),b.next('edit')]);assert.equal(ea.rev,1);assert.equal(eb.block,4);
 const c=connect('Charlie');try{const wc=await c.next('welcome');assert.equal(wc.edits[0].block,4);assert.equal(wc.rev,1);assert.equal(wc.players[0].name,'Alice');}finally{c.ws.close();}
 a.ws.send(JSON.stringify({type:'edit',x:1,y:0,z:1,block:0}));assert.equal((await a.next('error')).type,'error');
 b.ws.close();let left;do {left=await a.next('leave');} while(left.id!==wb.id);assert.equal(left.id,wb.id);
 console.log('PASS:health,create,two peers,positions,block broadcast,reconnect snapshot,invalid edit,leave');
}finally{a.ws.close();b.ws.close();}
