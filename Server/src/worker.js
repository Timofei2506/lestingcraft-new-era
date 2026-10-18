export {AccountDirectory} from './accounts-worker.js';
import {publicConfig,RoomState,createConfig,validCode,randomCode,newPeer,MAX_PLAYERS,RULES_VERSION,acceptsClient,packetError} from './protocol.js';
const cors={'Access-Control-Allow-Origin':'*','Access-Control-Allow-Methods':'GET,POST,OPTIONS','Access-Control-Allow-Headers':'Content-Type,Authorization'};
const json=(data,status=200)=>new Response(JSON.stringify(data),{status,headers:{...cors,'Content-Type':'application/json','Cache-Control':'no-store'}});
const error=(message,status=400)=>json({type:'error',message},status);
export default {
  async fetch(request,env) {
    const url=new URL(request.url);
    if(request.method==='OPTIONS')return new Response(null,{status:204,headers:cors});
    if(/^\/api\/accounts\/(register|login|recover|me|logout|password|delete|profile|progress)$/.test(url.pathname)){const response=await env.ACCOUNTS.get(env.ACCOUNTS.idFromName('accounts-v1')).fetch(request);return new Response(response.body,{status:response.status,headers:{...cors,'Content-Type':'application/json','Cache-Control':'no-store'}});}
    if(url.pathname==='/health')return json({ok:true,game:'LestingCraft',version:1,rulesVersion:RULES_VERSION,transport:'websocket',maxPlayers:8,storage:'durable-object-sqlite'});
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
    if(!this.room)return error('Room not found',404);
    if(this.ctx.getWebSockets().length>=MAX_PLAYERS)return error('Room full',409);
    const url=new URL(request.url),key=url.searchParams.get('key');
    if(!acceptsClient(this.room.config,url.searchParams.get('clientRules')))return error('Client update required for expanded room',409);
    if(this.room.config.rulesVersion>=2&&(!validCode(key)||this.ctx.getWebSockets().some(s=>s.deserializeAttachment()?.playerKey===key)))return error('Valid unique player key required',409);
    const pair=new WebSocketPair(),[client,server]=Object.values(pair),peer=newPeer(crypto.randomUUID(),url.searchParams.get('name'),key);
    server.serializeAttachment(peer);this.ctx.acceptWebSocket(server);
    server.send(JSON.stringify(this.room.welcome(peer,this.ctx.getWebSockets().map(s=>s.deserializeAttachment()))));if(this.room.config.lobby)this.broadcast(this.room.lobbyState());
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
      socket.serializeAttachment(peer);if(message.privateSave){socket.send(JSON.stringify({type:"living_saved",requestId:message.requestId,save:message.privateSave}));delete message.privateSave;}this.broadcast(message,message.type==='player'&&!message.animals&&!message.story&&!message.living?socket:null);
    }catch(e){socket.serializeAttachment(peer);if(peer.count>30)socket.close(1008,'Rate limit');else try{socket.send(JSON.stringify(packetError(raw,e)));}catch{}}
  }
  broadcast(message,exclude=null) {const raw=JSON.stringify(message);for(const socket of this.ctx.getWebSockets())if(socket!==exclude)try{socket.send(raw);}catch{}}
  webSocketClose(socket) {const p=socket.deserializeAttachment();if(this.room){this.room.setMembers(this.ctx.getWebSockets().filter(s=>s!==socket).map(s=>s.deserializeAttachment()));this.room.disconnect(p,config=>this.ctx.storage.sql.exec('UPDATE meta SET json=? WHERE id=1',JSON.stringify(config)));if(this.room.config.lobby)this.broadcast(this.room.lobbyState(),socket);}try{socket.close(1000,'Disconnected');}catch{}this.broadcast({type:'leave',id:p.id},socket);}
  webSocketError(socket) {try{socket.close(1011,'Socket error');}catch{}}
}
