// Two actual WebSocket clients exercise one room-owned platform over its six real
// seconds. This checks transport/persistence, NOT a Unity/Windows physical playthrough.
import assert from 'node:assert/strict';
import WebSocket from 'ws';
const endpoint=process.env.TEST_ENDPOINT||'http://127.0.0.1:8787';
const a='a'.repeat(32),b='b'.repeat(32);
const create=await fetch(endpoint+'/api/rooms',{method:'POST',headers:{'Content-Type':'application/json'},
  body:JSON.stringify({version:1,rulesVersion:11,seed:2506,dimension:0,mode:2,ownerKey:a})});
assert.equal(create.status,201);
const {code}=await create.json();
const sockets=[];
function connect(key,name){return new Promise((resolve,reject)=>{
  const ws=new WebSocket(endpoint.replace(/^http/,'ws')+'/api/rooms/'+code+'/ws?name='+name+'&key='+key+'&clientRules=11');
  const messages=[],waiters=[];
  ws.on('error',reject);
  ws.on('message',buf=>{const packet=JSON.parse(buf.toString());messages.push(packet);
    for(const w of [...waiters])if(w.filter(packet)){waiters.splice(waiters.indexOf(w),1);clearTimeout(w.timer);w.resolve(packet);}});
  ws.on('open',()=>{const client={ws,messages,send:v=>ws.send(JSON.stringify(v)),
    wait:(filter,timeout=5000)=>new Promise((resolve,reject)=>{
      const found=messages.find(filter);if(found)return resolve(found);
      const w={filter,resolve,timer:setTimeout(()=>{waiters.splice(waiters.indexOf(w),1);reject(Error('Missing network response'));},timeout)};
      waiters.push(w);
    })};sockets.push(client);resolve(client);});
});}
const p=await connect(a,'Captain'),q=await connect(b,'Friend');
try{
  const wp=await p.wait(v=>v.type==='welcome'),wq=await q.wait(v=>v.type==='welcome');
  assert.deepEqual(wp.launchSite,wq.launchSite);
  assert.equal(wp.launchSite.active,true);
  assert(!JSON.stringify(wq).includes(a));
  p.send({type:'lobby_start'});
  await p.wait(v=>v.type==='lobby_start');
  await q.wait(v=>v.type==='lobby_start');
  const site=wp.launchSite,foot=progress=>({type:'position',
    x:site.x-19.8,y:site.y+5+progress*25+1.58,z:site.z+3,yaw:0});
  p.send(foot(0));await p.wait(v=>v.type==='player'&&v.id===wp.id&&v.lift?.active);
  q.send(foot(0));await q.wait(v=>v.type==='player'&&v.id===wq.id&&v.lift?.active);
  p.send({type:'space_lift',action:'up'});
  const lift=await p.wait(v=>v.type==='space_lift'&&v.lift?.riderId===wp.id);
  const other=await q.wait(v=>v.type==='space_lift'&&v.lift?.riderId===wp.id);
  assert.equal(lift.lift.serverNow,other.lift.serverNow,'both see the same authoritative start');
  assert.equal(lift.lift.height,0);
  let counter=0;
  while(Date.now()<lift.lift.serverNow+6150){
    await new Promise(r=>setTimeout(r,210));
    const progress=Math.max(0,Math.min(1,(Date.now()-lift.lift.serverNow)/6000));
    p.send(foot(progress));counter++;
  }
  assert.ok(counter>=20);
  const top=await p.wait(v=>v.type==='player'&&v.id===wp.id&&v.lift?.height===25);
  assert.equal(top.lift.height,25);
  p.send({type:'space_lift',action:'exit'});
  const exited=await q.wait(v=>v.type==='space_lift'&&v.lift?.occupied===false);
  assert.equal(exited.lift.height,25);
  q.send(foot(0));await q.wait(v=>v.type==='player'&&v.id===wq.id&&v.lift?.height===25);
  q.send({type:'space_lift',action:'recall'});
  const recall=await p.wait(v=>v.type==='space_lift'&&v.lift?.direction==='down');
  assert.equal(recall.lift.occupied,false);
  console.log('PASS: two real WebSocket clients share server-timed lift ascent and empty recall');
}finally{for(const c of sockets)c.ws.close();}
