export {AccountDirectory} from './accounts-worker.js';
import {publicConfig,RoomState,createConfig,validCode,randomCode,newPeer,MAX_PLAYERS,RULES_VERSION,CONTENT_VERSION,acceptsClient,packetError} from './protocol.js';
import {MAX_SERVERS,serverRecord,publicServer,sortServers,serverName} from './server-registry.js';
import {CloudService,handleCloudRequest} from './cloud.js';

// One client per isolate: the service key never leaves this module.
let cloudService=null;
function cloud(env){
  if(!env.SUPABASE_URL||!env.SUPABASE_SERVICE_KEY)return null;
  if(!cloudService||cloudService.base!==String(env.SUPABASE_URL).replace(/\/+$/,'')||cloudService.key!==env.SUPABASE_SERVICE_KEY)
    cloudService=new CloudService(env.SUPABASE_URL,env.SUPABASE_SERVICE_KEY);
  return cloudService;
}

// The game account token is validated by the accounts Durable Object; its uid is
// the player_key the economy tables use.
async function resolvePlayer(request,env,token){
  if(!/^[a-f0-9]{64}$/.test(token||''))return null;
  const response=await env.ACCOUNTS.get(env.ACCOUNTS.idFromName('accounts-v1')).fetch('https://accounts/api/accounts/me',{
    method:'GET',headers:{Authorization:`Bearer ${token}`,'CF-Connecting-IP':request.headers.get('CF-Connecting-IP')||'cloud'}});
  if(!response.ok)return null;
  const body=await response.json().catch(()=>null);
  return body?.ok&&typeof body.profile?.uid==='string'?body.profile.uid.toLowerCase():null;
}
const cors={'Access-Control-Allow-Origin':'*','Access-Control-Allow-Methods':'GET,POST,DELETE,OPTIONS','Access-Control-Allow-Headers':'Content-Type,Authorization'};
const json=(data,status=200)=>new Response(JSON.stringify(data),{status,headers:{...cors,'Content-Type':'application/json','Cache-Control':'no-store'}});
const error=(message,status=400)=>json({type:'error',message},status);
export default {
  async fetch(request,env,ctx) {
    const url=new URL(request.url);
    if(request.method==='OPTIONS')return new Response(null,{status:204,headers:cors});
    if(/^\/api\/accounts\/(register|login|recover|me|logout|password|delete|profile|progress)$/.test(url.pathname)){
      const response=await env.ACCOUNTS.get(env.ACCOUNTS.idFromName('accounts-v1')).fetch(request);
      // Best effort: give the new account an economy profile right away. Without it
      // a friend request to a player who never opened the wallet is dropped silently.
      if(/\/(register|login)$/.test(url.pathname)&&response.ok){
        // waitUntil keeps the isolate alive until the write finishes: a plain
        // .then() was silently cancelled on some responses and the profile stayed missing.
        const provisioning=(async()=>{
          const body=await response.clone().json();
          const uid=String(body?.profile?.uid||'').toLowerCase();
          const service=cloud(env);
          if(service&&/^[a-f0-9]{32}$/.test(uid))await service.ensureProfile(uid,body?.profile?.username);
        })().catch(()=>{});
        if(ctx&&typeof ctx.waitUntil==='function')ctx.waitUntil(provisioning);
        else await provisioning;
      }
      return new Response(response.body,{status:response.status,headers:{...cors,'Content-Type':'application/json','Cache-Control':'no-store'}});
    }
    if(url.pathname.startsWith('/api/cloud/')) {
      const service=cloud(env);
      if(!service)return error('Экономика не настроена: нужны SUPABASE_URL и SUPABASE_SERVICE_KEY',503);
      return handleCloudRequest(request,{service,resolvePlayer:token=>resolvePlayer(request,env,token),cors});
    }
    if(url.pathname==='/health')return json({ok:true,game:'LestingCraft',version:1,rulesVersion:RULES_VERSION,contentVersion:CONTENT_VERSION,transport:'websocket',maxPlayers:8,storage:'durable-object-sqlite',commit:env.GIT_SHA||'dev',serverNow:Date.now()});
    if(url.pathname==='/api/rooms'&&request.method==='POST') {
      try {
        if(Number(request.headers.get('Content-Length'))>1024)return error('Body too large',413);
        const text=await request.text();if(text.length>1024)return error('Body too large',413);
        const config=createConfig(JSON.parse(text));
        const gate=env.DIRECTORY.get(env.DIRECTORY.idFromName('creation-limits-v1'));
        const reserve=await gate.fetch('https://directory/reserve',{method:'POST',body:request.headers.get('CF-Connecting-IP')||'unknown'});
        if(!reserve.ok)return error('Room creation quota reached; retry later',429);
        const {code}=await reserve.json();
        const room=env.ROOMS.get(env.ROOMS.idFromName(code));
        const created=await room.fetch('https://room/create',{method:'POST',body:JSON.stringify(config)});
        if(!created.ok)return error('Room could not be created',503);
        return json({type:'created',code,...publicConfig(config)},201);
      }catch(e){return error(e.message);}
    }
    if(url.pathname==='/api/servers'&&request.method==='POST') {
      try {
        const text=await request.text();if(text.length>2048)return error('Body too large',413);
        const body=JSON.parse(text),config=createConfig({version:1,...body});
        const gate=env.DIRECTORY.get(env.DIRECTORY.idFromName('creation-limits-v1'));
        const reserve=await gate.fetch('https://directory/reserve',{method:'POST',body:request.headers.get('CF-Connecting-IP')||'unknown'});
        if(!reserve.ok)return error('Server creation quota reached; retry later',429);
        const {code}=await reserve.json();
        const created=await env.ROOMS.get(env.ROOMS.idFromName(code)).fetch('https://room/create',{method:'POST',body:JSON.stringify(config)});
        if(!created.ok)return error('Server could not be created',503);
        const record=serverRecord(config,body.name,Date.now(),config.ownerKey);
        const registry=env.SERVERS.get(env.SERVERS.idFromName('servers-v1'));
        await registry.fetch('https://registry/add',{method:'POST',body:JSON.stringify({code,record})});
        return json({type:'created',code,server:publicServer(code,record,{players:0,maxPlayers:MAX_PLAYERS,started:config.started===true}),...publicConfig(config)},201);
      }catch(e){return error(e.message);}
    }
    if(url.pathname==='/api/servers'&&request.method==='GET') {
      const registry=env.SERVERS.get(env.SERVERS.idFromName('servers-v1'));
      const listed=await registry.fetch('https://registry/list');
      const rows=listed.ok?await listed.json():[];
      const servers=await Promise.all(rows.map(async row=>{
        let status=null;
        try {
          const response=await env.ROOMS.get(env.ROOMS.idFromName(row.code)).fetch('https://room/status');
          if(response.ok)status=await response.json();
        } catch {}
        return publicServer(row.code,row,status);
      }));
      return json({type:'servers',servers:sortServers(servers)});
    }
    const serverMatch=url.pathname.match(/^\/api\/servers\/([a-f0-9]{32})$/);
    if(serverMatch&&validCode(serverMatch[1])) {
      const registry=env.SERVERS.get(env.SERVERS.idFromName('servers-v1'));
      if(request.method==='DELETE') {
        const response=await registry.fetch('https://registry/remove',{method:'POST',body:JSON.stringify({code:serverMatch[1],owner:url.searchParams.get('owner')||''})});
        if(!response.ok)return error(await response.text()||'Server not found',response.status);
        return json({type:'removed',code:serverMatch[1]});
      }
      const listed=await registry.fetch('https://registry/list');
      const rows=listed.ok?await listed.json():[];
      const row=rows.find(r=>r.code===serverMatch[1]);
      if(!row)return error('Server not found',404);
      let status=null;
      try {
        const response=await env.ROOMS.get(env.ROOMS.idFromName(row.code)).fetch('https://room/status');
        if(response.ok)status=await response.json();
      } catch {}
      return json({type:'server',server:publicServer(row.code,row,status)});
    }
    const match=url.pathname.match(/^\/api\/rooms\/([a-f0-9]{32})\/ws$/);
    if(match&&validCode(match[1])&&request.method==='GET') {
      if(request.headers.get('Upgrade')?.toLowerCase()!=='websocket')return error('WebSocket upgrade required',426);
      return env.ROOMS.get(env.ROOMS.idFromName(match[1])).fetch(request);
    }
    return error('Not found',404);
  }
};
export class RoomDirectory {
  constructor(ctx) {this.ctx=ctx;ctx.storage.sql.exec('CREATE TABLE IF NOT EXISTS quota (key TEXT PRIMARY KEY,count INTEGER NOT NULL)');}
  async fetch(request) {
    const ip=await request.text();
    // No IP addresses in storage: only a digest and expiring quota counters.
    const digest=[...new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(ip)))].map(x=>x.toString(16).padStart(2,'0')).join('');
    const hour=Math.floor(Date.now()/3600000),day=Math.floor(hour/24),dayKey=`day:${day}`,ipKey=`ip:${hour}:${digest}`;
    const count=key=>this.ctx.storage.sql.exec('SELECT count FROM quota WHERE key=?',key).toArray()[0]?.count||0;
    if(count(dayKey)>=32||count(ipKey)>=4)return new Response('Quota',{status:429});
    this.ctx.storage.transactionSync(()=>{
      for(const key of [dayKey,ipKey])this.ctx.storage.sql.exec('INSERT INTO quota VALUES (?,1) ON CONFLICT(key) DO UPDATE SET count=count+1',key);
      this.ctx.storage.sql.exec("DELETE FROM quota WHERE key LIKE 'day:%' AND key != ?",dayKey);
      this.ctx.storage.sql.exec("DELETE FROM quota WHERE key LIKE 'ip:%' AND key NOT LIKE ?",`ip:${hour}:%`);
    });
    return json({code:randomCode()});
  }
}
export class GameRoom {
  constructor(ctx) {
    this.ctx=ctx;const sql=ctx.storage.sql;
    sql.exec('CREATE TABLE IF NOT EXISTS meta (id INTEGER PRIMARY KEY,json TEXT NOT NULL)');
    sql.exec('CREATE TABLE IF NOT EXISTS edits (x INTEGER,y INTEGER,z INTEGER,block INTEGER NOT NULL,rev INTEGER NOT NULL,PRIMARY KEY(x,y,z))');
    sql.exec('CREATE TABLE IF NOT EXISTS player_saves (key TEXT PRIMARY KEY,json TEXT NOT NULL)');
    const row=sql.exec('SELECT json FROM meta WHERE id=1').toArray()[0];
    this.room=row?new RoomState(JSON.parse(row.json),sql.exec('SELECT x,y,z,block,rev FROM edits').toArray(),sql.exec('SELECT key,json FROM player_saves').toArray().map(p=>({key:p.key,save:JSON.parse(p.json)}))):null;
  }
  async fetch(request) {
    if(new URL(request.url).pathname==='/create'&&request.method==='POST') {
      const config=JSON.parse(await request.text());if(this.room)return new Response('Already created',{status:409});
      this.ctx.storage.sql.exec('INSERT INTO meta VALUES (1,?)',JSON.stringify(config));this.room=new RoomState(config);return new Response('Created',{status:201});
    }
    if(new URL(request.url).pathname==='/status') {
      if(!this.room)return error('Room not found',404);
      return json({players:this.ctx.getWebSockets().length,maxPlayers:MAX_PLAYERS,started:this.room.config.started===true,rulesVersion:this.room.config.rulesVersion,contentVersion:this.room.config.contentVersion,rev:this.room.config.rev||0});
    }
    if(!this.room)return error('Room not found',404);
    if(this.ctx.getWebSockets().length>=MAX_PLAYERS)return error('Room full',409);
    const url=new URL(request.url),key=url.searchParams.get('key');
    if(!acceptsClient(this.room.config,url.searchParams.get('clientRules')))return error('Client update required for expanded room',409);
    if(this.room.config.rulesVersion>=2&&(!validCode(key)||this.ctx.getWebSockets().some(s=>s.deserializeAttachment()?.playerKey===key)))return error('Valid unique player key required',409);
    const pair=new WebSocketPair(),[client,server]=Object.values(pair),peer=newPeer(crypto.randomUUID(),url.searchParams.get('name'),key);
    server.serializeAttachment(peer);this.ctx.acceptWebSocket(server);
    server.send(JSON.stringify(this.room.welcome(peer,this.ctx.getWebSockets().map(s=>s.deserializeAttachment()))));
    const seatIndex=this.room.config.spaceMission?.crew?.indexOf(key)??-1;
    if(seatIndex>=0)this.broadcast({type:'space_crew_join',id:peer.id,name:peer.name,seatIndex},server);
    if(this.room.config.lobby)this.broadcast(this.room.lobbyState());
    return new Response(null,{status:101,webSocket:client});
  }
  webSocketMessage(socket,raw) {
    const peer=socket.deserializeAttachment();this.room.setMembers(this.ctx.getWebSockets().map(s=>s.deserializeAttachment()));
    try {
      const message=this.room.handle(peer,raw,Date.now(),(record,nextConfig)=>{
        this.ctx.storage.transactionSync(()=>{
          this.ctx.storage.sql.exec('INSERT INTO edits VALUES (?,?,?,?,?) ON CONFLICT(x,y,z) DO UPDATE SET block=excluded.block,rev=excluded.rev',record.x,record.y,record.z,record.block,record.rev);
          this.ctx.storage.sql.exec('UPDATE meta SET json=? WHERE id=1',JSON.stringify(nextConfig||{...this.room.config,rev:record.rev}));
        });
      },(key,save,config)=>{
        this.ctx.storage.transactionSync(()=>{
          this.ctx.storage.sql.exec('INSERT INTO player_saves VALUES (?,?) ON CONFLICT(key) DO UPDATE SET json=excluded.json',key,JSON.stringify(save));
          this.ctx.storage.sql.exec('UPDATE meta SET json=? WHERE id=1',JSON.stringify(config));
        });
      },config=>this.ctx.storage.sql.exec('UPDATE meta SET json=? WHERE id=1',JSON.stringify(config)));
      socket.serializeAttachment(peer);if(message.privateSave){socket.send(JSON.stringify({type:message.type==='station_trade'?'station_trade_saved':'living_saved',requestId:message.requestId,save:message.privateSave}));delete message.privateSave;}if(message.type==='dream_woke'){socket.send(JSON.stringify(this.room.welcome(peer,this.ctx.getWebSockets().map(s=>s.deserializeAttachment()))));return;}this.broadcast(message,message.type==='player'&&!message.animals&&!message.story&&!message.living&&!message.lift?socket:message.dreamIsolated?socket:null);
    }catch(e){socket.serializeAttachment(peer);if(peer.count>30)socket.close(1008,'Rate limit');else try{socket.send(JSON.stringify(packetError(raw,e)));}catch{}}
  }
  broadcast(message,exclude=null) {const raw=JSON.stringify(message);for(const socket of this.ctx.getWebSockets())if(socket!==exclude&&!socket.deserializeAttachment()?.inDream)try{socket.send(raw);}catch{}}
  webSocketClose(socket) {const p=socket.deserializeAttachment();if(this.room){this.room.setMembers(this.ctx.getWebSockets().filter(s=>s!==socket).map(s=>s.deserializeAttachment()));this.room.disconnect(p,config=>this.ctx.storage.sql.exec('UPDATE meta SET json=? WHERE id=1',JSON.stringify(config)));if(this.room.config.lobby)this.broadcast(this.room.lobbyState(),socket);}try{socket.close(1000,'Disconnected');}catch{}this.broadcast({type:'leave',id:p.id},socket);}
  webSocketError(socket) {try{socket.close(1011,'Socket error');}catch{}}
}

export class ServerRegistry {
  constructor(ctx) {
    this.ctx=ctx;
    ctx.storage.sql.exec('CREATE TABLE IF NOT EXISTS servers (code TEXT PRIMARY KEY,name TEXT NOT NULL,mode INTEGER NOT NULL,seed INTEGER NOT NULL,dimension INTEGER NOT NULL,rulesVersion INTEGER NOT NULL,contentVersion INTEGER NOT NULL,createdAt INTEGER NOT NULL,owner TEXT)');
  }
  rows() {return this.ctx.storage.sql.exec('SELECT code,name,mode,seed,dimension,rulesVersion,contentVersion,createdAt,owner FROM servers ORDER BY createdAt DESC').toArray();}
  async fetch(request) {
    const url=new URL(request.url),route=url.pathname;
    if(route==='/list')return json(this.rows());
    if(route==='/add'&&request.method==='POST') {
      const {code,record}=JSON.parse(await request.text());
      if(!validCode(code))return error('Invalid server code',400);
      if(!record||typeof record.name!=='string')return error('Server name required',400);
      try {record.name=serverName(record.name,'Мир');} catch(e) {return error(e.message,400);}
      this.ctx.storage.sql.exec('INSERT INTO servers VALUES (?,?,?,?,?,?,?,?,?) ON CONFLICT(code) DO UPDATE SET name=excluded.name,mode=excluded.mode,seed=excluded.seed,contentVersion=excluded.contentVersion',
        code,record.name,record.mode,record.seed,record.dimension,record.rulesVersion,record.contentVersion,record.createdAt,record.owner||null);
      this.ctx.storage.sql.exec('DELETE FROM servers WHERE code NOT IN (SELECT code FROM servers ORDER BY createdAt DESC LIMIT ?)',MAX_SERVERS);
      return json({ok:true});
    }
    if(route==='/remove'&&request.method==='POST') {
      const {code,owner}=JSON.parse(await request.text());
      const row=this.ctx.storage.sql.exec('SELECT code,owner FROM servers WHERE code=?',code).toArray()[0];
      if(!row)return new Response('Server not found',{status:404});
      if(row.owner&&row.owner!==owner)return new Response('Only the creator can remove this server',{status:403});
      this.ctx.storage.sql.exec('DELETE FROM servers WHERE code=?',code);
      return json({ok:true});
    }
    return error('Not found',404);
  }
}
