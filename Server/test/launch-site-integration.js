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
    wait:(filter,timeout=5000,after=0)=>new Promise((resolve,reject)=>{
      const found=messages.slice(after).find(filter);if(found)return resolve(found);
      const w={filter,resolve,timer:setTimeout(()=>{waiters.splice(waiters.indexOf(w),1);
        reject(Error('Missing network response; recent packets: '+JSON.stringify(messages.slice(-5))));},timeout)};
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
  // Exercise the rest of the boarding route with TWO real network connections.
  // Unity collision and actual Windows input still require the owner's playthrough.
  const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
  async function step(client,id,x,y,z){
    await delay(105); // don't collide with the previous movement sample
    const after=client.messages.length;
    client.send({type:'position',x,y,z,yaw:0});
    await client.wait(v=>v.type==='player'&&v.id===id&&Math.abs(v.x-x)<.01&&
      Math.abs(v.y-y)<.01&&Math.abs(v.z-z)<.01,5000,after);
  }
  async function walk(client,id,from,to,y,z,segments){
    for(let i=1;i<=segments;i++)await step(client,id,from+(to-from)*i/segments,y,z);
  }
  const deck=site.y+30+1.58;
  await step(p,wp.id,site.x-19.8,deck,site.z+3);
  await walk(p,wp.id,site.x-19.8,site.x-9,deck,site.z+3,20);
  p.send({type:'space_hatch',open:true});
  assert.equal((await p.wait(v=>v.type==='space_hatch'&&v.hatchOpen)).hatchOpen,true);
  await walk(p,wp.id,site.x-9,site.x+.5,deck,site.z+3,18);
  for(let i=1;i<=56;i++)await step(p,wp.id,site.x+.5,deck+14*i/56,site.z+3);
  const seatY=site.y+44+1.58;
  for(let i=1;i<=7;i++)await step(p,wp.id,site.x+.5-.9*i/7,seatY,site.z+3+2.35*i/7);
  p.send({type:'space_seat',action:'sit',index:0});
  assert.deepEqual((await p.wait(v=>v.type==='space_seat'&&v.seats?.[0]===wp.id)).seats,[wp.id,'']);
  const heartbeat=setInterval(()=>p.send({type:'position',x:site.x-.4,y:seatY,z:site.z+5.35,yaw:0}),520);
  try{
    p.send({type:'space_preflight'});
    assert.equal((await p.wait(v=>v.type==='space_preflight'&&v.id===wp.id)).ready,false);
    while(Date.now()<recall.lift.serverNow+6150)await delay(150);
    await step(q,wq.id,site.x-19.8,site.y+5+1.58,site.z+3);
    q.send({type:'space_lift',action:'up'});
    const second=await q.wait(v=>v.type==='space_lift'&&v.lift?.riderId===wq.id);
    assert.equal(second.lift.progress,0);
    while(Date.now()<second.lift.serverNow+6150){
      await delay(210);
      const progress=Math.max(0,Math.min(1,(Date.now()-second.lift.serverNow)/6000));
      q.send(foot(progress));
    }
    await q.wait(v=>v.type==='player'&&v.id===wq.id&&v.lift?.height===25);
    q.send({type:'space_lift',action:'exit'});
    assert.equal((await p.wait(v=>v.type==='space_lift'&&v.id===wq.id&&v.lift?.occupied===false)).lift.height,25);
    await step(q,wq.id,site.x-19.8,deck,site.z+3);
    await walk(q,wq.id,site.x-19.8,site.x-9,deck,site.z+3,20);
    await walk(q,wq.id,site.x-9,site.x-7.4,deck,site.z+3,4);
    q.send({type:'space_hatch',open:false});
    assert.equal((await q.wait(v=>v.type==='space_hatch'&&v.hatchOpen===false)).hatchOpen,false);
    await walk(q,wq.id,site.x-7.4,site.x+.5,deck,site.z+3,15);
    for(let i=1;i<=56;i++)await step(q,wq.id,site.x+.5,deck+14*i/56,site.z+3);
    for(let i=1;i<=7;i++)await step(q,wq.id,site.x+.5+.9*i/7,seatY,site.z+3+2.35*i/7);
    q.send({type:'space_seat',action:'sit',index:1});
    assert.deepEqual((await q.wait(v=>v.type==='space_seat'&&v.seats?.[1]===wq.id)).seats,[wp.id,wq.id]);
    q.send({type:'space_preflight'});
    await q.wait(v=>v.type==='error'&&v.code==='space_preflight');
    const after=p.messages.length;
    p.send({type:'space_preflight'});
    const checked=await p.wait(v=>v.type==='space_preflight'&&v.id===wp.id,5000,after);
    assert.equal(checked.ready,true,JSON.stringify(checked.issues));
    assert.equal(checked.crewCount,2);
    assert(!JSON.stringify(checked).includes(a));assert(!JSON.stringify(checked).includes(b));
    p.send({type:'space_launch'});
    await p.wait(v=>v.type==='error'&&v.code==='space_launch');
    console.log('PASS: two WebSocket clients ride, walk through hatch, climb, claim both seats and pass read-only preflight');
  }finally{clearInterval(heartbeat);}
}finally{for(const c of sockets)c.ws.close();}
