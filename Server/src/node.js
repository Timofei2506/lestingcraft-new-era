import {AccountService,MemoryAccountStore} from './accounts.js';
import http from 'node:http';
import {WebSocketServer} from 'ws';
import fs from 'node:fs';
import path from 'node:path';
import {publicConfig,RoomState,createConfig,randomCode,newPeer,MAX_PLAYERS,validCode,RULES_VERSION,MAX_MESSAGE,acceptsClient,packetError} from './protocol.js';
const port=Number(process.env.PORT||8787),dataDir=path.resolve(process.env.DATA_DIR||'./data');fs.mkdirSync(dataDir,{recursive:true});
class FileAccountStore extends MemoryAccountStore {constructor(file){super(fs.existsSync(file)?JSON.parse(fs.readFileSync(file,'utf8')):{});this.file=file;this.lastGood=JSON.stringify(this.data);}async flush(){const raw=JSON.stringify(this.data);try{fs.writeFileSync(this.file+'.tmp',raw,{mode:0o600});fs.renameSync(this.file+'.tmp',this.file);this.lastGood=raw;}catch(e){this.data=JSON.parse(this.lastGood);throw e;}}}
const accountStore=new FileAccountStore(path.join(dataDir,'accounts.private.json')),accounts=new AccountService(accountStore);
const rooms=new Map(),sockets=new Map(),quota=new Map();
function load(code) {
  if(rooms.has(code))return rooms.get(code);const file=path.join(dataDir,code+'.json');if(!fs.existsSync(file))return null;
  const stored=JSON.parse(fs.readFileSync(file,'utf8')),room=new RoomState(stored.config,stored.edits,stored.players||[]);rooms.set(code,room);return room;
}
function save(code,config,edits,players=[]) {const file=path.join(dataDir,code+'.json'),temp=file+'.tmp';fs.writeFileSync(temp,JSON.stringify({config,edits,players}));fs.renameSync(temp,file);}
function reply(res,status,value) {res.writeHead(status,{'Content-Type':'application/json','Access-Control-Allow-Origin':'*','Cache-Control':'no-store'});res.end(JSON.stringify(value));}
function peers(code) {return [...(sockets.get(code)||[])].filter(s=>s.readyState===1);}
function broadcast(code,message,except) {const raw=JSON.stringify(message);for(const s of peers(code))if(s!==except&&!s.peer?.inDream)s.send(raw);}
const server=http.createServer(async(req,res)=>{
  const url=new URL(req.url,'http://server');
  if(req.method==='OPTIONS'){res.writeHead(204,{'Access-Control-Allow-Origin':'*','Access-Control-Allow-Methods':'GET,POST,OPTIONS','Access-Control-Allow-Headers':'Content-Type,Authorization'});res.end();return;}
  if(/^\/api\/accounts\/(register|login|recover|me|logout|password|delete|profile|progress)$/.test(url.pathname)){try{let parts=[],bytes=0;for await(const part of req){bytes+=part.length;if(bytes>24576)return reply(res,413,{message:'Body too large'});parts.push(part);}const raw=Buffer.concat(parts).toString('utf8');const result=await accounts.handle(url.pathname.split('/').pop(),req.method,raw?JSON.parse(raw):{},req.headers.authorization?.replace(/^Bearer /,'')||'',req.socket.remoteAddress);return reply(res,200,result);}catch(e){await accountStore.flush().catch(()=>{});return reply(res,e.status||503,{ok:false,message:e.status?e.message:'Сервис аккаунтов временно недоступен.'});}}
  if(url.pathname==='/health')return reply(res,200,{ok:true,game:'LestingCraft',version:1,rulesVersion:RULES_VERSION,transport:'websocket',storage:'local-json',maxPlayers:8});
  if(req.method==='POST'&&url.pathname==='/api/rooms') {
    try {
      let raw='';for await(const chunk of req){raw+=chunk;if(raw.length>1024){reply(res,413,{type:'error',message:'Body too large'});return;}}
      const config=createConfig(JSON.parse(raw)),hour=Math.floor(Date.now()/3600000),ip=req.socket.remoteAddress,key=`${hour}:${ip}`;
      for(const k of quota.keys())if(!k.startsWith(hour+':'))quota.delete(k);
      if((quota.get(key)||0)>=4||rooms.size>=32)return reply(res,429,{type:'error',message:'Room quota reached (4/hour/IP,32 loaded rooms)'});
      quota.set(key,(quota.get(key)||0)+1);const code=randomCode();save(code,config,[]);rooms.set(code,new RoomState(config));return reply(res,201,{type:'created',code,...publicConfig(config)});
    }catch(e){return reply(res,400,{type:'error',message:e.message});}
  }
  reply(res,404,{type:'error',message:'Not found'});
});
const wss=new WebSocketServer({noServer:true,maxPayload:MAX_MESSAGE});
server.on('upgrade',(req,socket,head)=>{
  const url=new URL(req.url,'http://server'),match=url.pathname.match(/^\/api\/rooms\/([a-f0-9]{32})\/ws$/),code=match?.[1];let room;
  try{room=validCode(code)?load(code):null;}catch{}
  const reject=(status,reason)=>{socket.write(`HTTP/1.1 ${status} ${reason}\r\nConnection: close\r\n\r\n`);socket.destroy();};
  if(!room)return reject(404,'Not Found');if(peers(code).length>=MAX_PLAYERS)return reject(409,'Room Full');
  if(!acceptsClient(room.config,url.searchParams.get('clientRules')))return reject(409,'Client Update Required');
  const key=url.searchParams.get('key');
  if(room.config.rulesVersion>=2&&(!validCode(key)||peers(code).some(p=>p.peer.playerKey===key)))return reject(409,'Invalid Player Key');
  wss.handleUpgrade(req,socket,head,ws=>{
    ws.peer=newPeer(crypto.randomUUID(),url.searchParams.get('name'),key);if(!sockets.has(code))sockets.set(code,new Set());sockets.get(code).add(ws);
    ws.send(JSON.stringify(room.welcome(ws.peer,peers(code).map(s=>s.peer))));
    const seatIndex=room.config.spaceMission?.crew?.indexOf(key)??-1;
    if(seatIndex>=0)broadcast(code,{type:'space_crew_join',id:ws.peer.id,name:ws.peer.name,seatIndex},ws);
    if(room.config.lobby)broadcast(code,room.lobbyState());
    ws.on('message',(buffer,binary)=>{
      try {
        const message=room.handle(ws.peer,binary?buffer:buffer.toString(),Date.now(),(record,nextConfig)=>{
          const records=new Map(room.edits);records.set(`${record.x},${record.y},${record.z}`,record);save(code,nextConfig||{...room.config,rev:record.rev},[...records.values()],[...room.savedPlayers].map(([key,save])=>({key,save})));
        },(key,state,config)=>{
          const saved=new Map(room.savedPlayers);saved.set(key,state);save(code,config,[...room.edits.values()],[...saved].map(([key,save])=>({key,save})));
        },config=>save(code,config,[...room.edits.values()],[...room.savedPlayers].map(([key,save])=>({key,save}))));if(message.privateSave){ws.send(JSON.stringify({type:message.type==='station_trade'?'station_trade_saved':'living_saved',requestId:message.requestId,save:message.privateSave}));delete message.privateSave;}if(message.type==='dream_woke'){ws.send(JSON.stringify(room.welcome(ws.peer,peers(code).map(s=>s.peer))));return;}broadcast(code,message,message.type==='player'&&!message.animals&&!message.story&&!message.living&&!message.lift?ws:message.dreamIsolated?ws:null);
      }catch(e){if(ws.peer.count>30)ws.close(1008,'Rate limit');else if(ws.readyState===1)ws.send(JSON.stringify(packetError(binary?null:buffer.toString(),e)));}
    });
    ws.on('close',()=>{sockets.get(code)?.delete(ws);room.disconnect(ws.peer,config=>save(code,config,[...room.edits.values()],[...room.savedPlayers].map(([key,save])=>({key,save}))));if(room.config.lobby)broadcast(code,room.lobbyState());broadcast(code,{type:'leave',id:ws.peer.id},ws);});ws.on('error',()=>{});
  });
});
server.listen(port,'0.0.0.0',()=>console.log(`LestingCraft standalone server on port ${port};storage ${dataDir}`));
for(const signal of ['SIGINT','SIGTERM'])process.on(signal,()=>{for(const set of sockets.values())for(const ws of set)ws.close(1001,'Shutdown');server.close(()=>process.exit(0));setTimeout(()=>process.exit(0),2000).unref();});
