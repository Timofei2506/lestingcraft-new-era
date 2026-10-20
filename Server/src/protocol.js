import {initialStory,StoryEngine} from './story.js';
import {protectedSite} from './story-world.js';
import {weapons} from './weapons.js';
import {Fauna} from './wildlife.js';
import {Living,ownerTag,transact,toolDrop} from './living.js';
import {stationWalk,stationTrade} from './station-visit.js';
import {advanceMission,setCruiseAutopilot,landMission,missionView} from './space-mission.js';
import {launchSite} from './launch-site.js';
import {emptyLift,liftAt,liftSnapshot,operateLift} from './shuttle-lift.js';
import {preflight} from './shuttle-preflight.js';
export const VERSION = 1;
export const RULES_VERSION = 11;
export const MAX_MESSAGE = 16384;
export const MAX_PLAYERS = 8;
export const MAX_EDITS = 4096;
export const BOUNDS = { min: -64, max: 64, height: 256 };
export const validCode = code => typeof code === 'string' && /^[a-f0-9]{32}$/.test(code);
export const safeName = name => String(name || 'Player').replace(/[<>\x00-\x1f\x7f\u202a-\u202e\u2066-\u2069]/g, '').slice(0, 24) || 'Player';
export const randomCode = () => crypto.randomUUID().replaceAll('-', '');
const integer = v => typeof v === 'number' && Number.isInteger(v);
const finite = v => typeof v === 'number' && Number.isFinite(v);
export function createConfig(value) {
  if (!value || value.version !== VERSION || !integer(value.seed) || value.seed < -2147483648 || value.seed > 2147483647 || !integer(value.dimension) || value.dimension < 0 || value.dimension > 4)
    throw new Error('Expected version=1, signed 32-bit seed, dimension=0..4');
  if(value.rulesVersion!==undefined&&![2,3,4,5,6,7,8,9,10,RULES_VERSION].includes(value.rulesVersion))throw new Error('Unsupported game rules');
  if([2,3,4,5,6,7,8,9,10,RULES_VERSION].includes(value.rulesVersion)&&(!integer(value.mode)||value.mode<0||value.mode>3||value.dimension!==0))throw new Error('Choose mode 0..3; new campaigns must start in Lestingia');
  if(value.rulesVersion>=7&&!validCode(value.ownerKey))throw Error('Creator identity required');
  return {launchSite:value.rulesVersion>=RULES_VERSION?launchSite(value.seed):undefined,launchLift:value.rulesVersion>=RULES_VERSION?emptyLift():undefined,padVisited:[],shuttleSeats:[null,null],launchHatch:false, lobby:value.rulesVersion>=7,started:value.rulesVersion<7,ownerKey:value.rulesVersion>=7?value.ownerKey:undefined,story:value.rulesVersion>=8&&value.mode!==2||value.rulesVersion>=7&&value.mode===3?initialStory():null,contentVersion:value.rulesVersion>=10?7:value.rulesVersion>=9?6:value.rulesVersion>=8?5:value.rulesVersion>=7?4:value.rulesVersion>=6?3:value.rulesVersion===5?2:1,version: VERSION, rulesVersion:value.rulesVersion||1, mode:[2,3,4,5,6,7,8,9,10,RULES_VERSION].includes(value.rulesVersion)?value.mode:2,seed:value.seed,dimension:value.dimension,rev:0,maxPlayers:MAX_PLAYERS,startedAt:Date.now(),runEnded:false,campaign:{chapter:0,unlockedMask:1,milestones:0,runEnded:false} };
}
const safeMission=(m,now)=>{const v=missionView(m,now);return {active:true,from:v.from,to:v.to,seconds:v.seconds,phase:v.phase,elapsed:v.elapsed,position:v.position,autopilot:v.autopilot,landingBurn:v.landingBurn,crewCount:v.crew.length};};
export function newPeer(id, name,playerKey=null) {return {id,playerKey,name:safeName(name),lastState:0,lastForcedSave:0,x:0,y:90,z:0,yaw:0,positioned:false,windowStart:0,count:0,lastPosition:0,lastEdit:0,lastChat:-10000};}
export function publicPeer(p) {return {id:p.id,name:p.name,x:p.x,y:p.y,z:p.z,yaw:p.yaw,weapon:p.weapon||0,offhand:p.offhand||0,equipment:p.equipment||Array(8).fill(0)};}
export function consumeRate(peer, now) {
  if (now - peer.windowStart >= 1000) {peer.windowStart=now;peer.count=0;}
  return ++peer.count <= 15;
}
export function parseMessage(raw) {
  if (typeof raw !== 'string' || raw.length > MAX_MESSAGE) throw new Error('Text message must be at most 16384 bytes');
  const message=JSON.parse(raw);
  if (!message || typeof message !== 'object' || Array.isArray(message)) throw new Error('Invalid packet');
  return message;
}
export function validatePosition(p,peer,now,bound=64) {
  if (!finite(p.x)||!finite(p.y)||!finite(p.z)||!finite(p.yaw)||p.x < -bound||p.x>=bound||p.z < -bound||p.z>=bound||p.y<0||p.y>256||Math.abs(p.yaw)>360000)throw new Error('Position outside room bounds');
  if(now-peer.lastPosition<80)throw new Error('Position update too frequent');
  // Client-authoritative movement: reach validation is not an anti-cheat.
  const equipment=validateEquipmentIds(p.equipment===undefined?(peer.equipment||Array(8).fill(0)):p.equipment);
  if(p.weapon!==undefined&&p.weapon!==0&&![100,101,102,103,150,151].includes(p.weapon)&&!Object.hasOwn(weapons,p.weapon))throw Error('Invalid visible weapon');
  if(p.offhand!==undefined&&p.offhand!==0&&p.offhand!==148)throw Error('Invalid offhand');
  return {weapon:p.weapon||0,offhand:p.offhand||0,type:'player',id:peer.id,name:peer.name,x:p.x,y:p.y,z:p.z,yaw:((p.yaw%360)+360)%360,equipment};
}
export function validateEdit(p,peer,edits,now,maxBlock=18,bound=64) {
  if(!integer(p.x)||!integer(p.y)||!integer(p.z)||!integer(p.block)||p.x < -bound||p.x>=bound||p.z < -bound||p.z>=bound||p.y<=0||p.y>=256||p.block<0||p.block>maxBlock||p.block===9)throw new Error('Invalid block edit (bedrock and y=0 are protected)');
  if(!peer.positioned)throw new Error('Send position before editing');
  if(now-peer.lastEdit<100)throw new Error('Block update too frequent');
  if(Math.hypot(p.x+.5-peer.x,p.y+.5-peer.y,p.z+.5-peer.z)>8)throw new Error('Block out of reach');
  const key=`${p.x},${p.y},${p.z}`;
  if(!edits.has(key)&&edits.size>=MAX_EDITS)throw new Error('Room edit limit reached');
  return {key,x:p.x,y:p.y,z:p.z,block:p.block};
}
export class RoomState {
  constructor(config,edits=[],savedPlayers=[]) {this.config={...config,contentVersion:config.contentVersion??(config.rulesVersion>=10?7:config.rulesVersion>=9?6:config.rulesVersion>=8?5:config.rulesVersion>=7?4:config.rulesVersion>=6?3:config.rulesVersion>=5?2:1)};this.members=new Map();this.storyEngine=new StoryEngine();this.observers=new Map();this.savedPlayers=new Map(savedPlayers.map(s=>[s.key,s.save]));this.edits=new Map(edits.map(e=>[`${e.x},${e.y},${e.z}`,e]));this.fauna=new Fauna(config.seed,config.wildlife||{},this.edits,this.config.contentVersion);this.living=new Living(config.seed,config.living||{},this.edits,this.config.contentVersion);this.living.restorePending();this.fauna.dynamicCells=this.living.cells;}
  setMembers(peers){this.members=new Map(peers.map(p=>[p.id,p]));const connected=new Set(peers.map(p=>p.playerKey)),ackKey=this.config.story?`${this.config.story.stage}:${this.config.story.line}`:'';for(const key of this.storyEngine.ready)if(!connected.has(key))this.storyEngine.ready.delete(key);this.storyEngine.acks=new Set(peers.filter(p=>p.storyAckKey===ackKey&&ackKey!=='').map(p=>p.playerKey));for(const p of peers){if(p.storyReady)this.storyEngine.ready.add(p.playerKey);if(p.lastStoryHit)this.storyEngine.hits.set(p.playerKey,p.lastStoryHit);}}
  clock(now=Date.now()){return 420+Math.max(0,(now-(this.config.startedAt||now))/1000);}
  lobbyState(){return {clock:this.clock(),type:'lobby',started:!!this.config.started,hostId:[...this.members.values()].find(p=>p.playerKey===this.config.ownerKey)?.id||'',players:[...this.members.values()].map(publicPeer),story:this.config.story,serverNow:Date.now()};}
  disconnect(peer,persist=()=>{}){this.members.delete(peer.id);this.observers.delete(peer.id);this.storyEngine.ready.delete(peer.playerKey);this.storyEngine.acks.delete(peer.playerKey);this.storyEngine.hits.delete(peer.playerKey);let next=this.config;
    // A disconnected rider cannot strand the only platform forever; it continues its
    // current path empty. No person is moved by this cleanup or by a new connection.
    if(next.launchLift?.riderKey===peer.playerKey)next={...next,launchLift:{...next.launchLift,riderKey:null}};
    if(next.shuttleSeats?.includes(peer.playerKey))next={...next,shuttleSeats:next.shuttleSeats.map(k=>k===peer.playerKey?null:k)};
    if(next.lobby&&next.ownerKey===peer.playerKey&&this.members.size)next={...next,ownerKey:this.members.values().next().value.playerKey};
    if(next!==this.config){persist(next);this.config=next;}}
  welcome(peer,peers) {
    this.setMembers([...peers.filter(p=>p.id!==peer.id),peer]);
    const {wildlife,ownerKey,living,spaceVisit,spaceMission,launchLift,padVisited,shuttleSeats,...publicConfig}=this.config,save=this.savedPlayers.get(peer.playerKey)||null;
    return {seats:(shuttleSeats||[null,null]).map(key=>[...this.members.values()].find(m=>m.playerKey===key)?.id||''),hatchOpen:!!this.config.launchHatch,lift:this.config.launchSite?.active?liftSnapshot(launchLift||emptyLift(),Date.now(),[...this.members.values()]):undefined,space:spaceMission?.crew?.includes(peer.playerKey)?{...safeMission(advanceMission(spaceMission,Math.max(Date.now(),spaceMission.lastAt)),Date.now()),pilot:spaceMission.pilot===peer.playerKey}:undefined,station:spaceVisit?.dock?.latched&&spaceVisit.crew?.includes(peer.playerKey)&&Array.isArray(spaceVisit.positions?.[peer.playerKey])?{active:true,dock:{...spaceVisit.dock},local:[...spaceVisit.positions[peer.playerKey]],crewCount:spaceVisit.crew.length}:undefined,stationRequestId:spaceVisit?.crew?.includes(peer.playerKey)?spaceVisit.tradeRequests?.[peer.playerKey]||0:0,living:this.config.contentVersion>=6?this.living.save():undefined,ownerTag:ownerTag(peer.playerKey||peer.id),host:peer.playerKey===ownerKey,serverNow:Date.now(),type:'welcome',serverRulesVersion:RULES_VERSION,clock:420+Math.max(0,(Date.now()-(this.config.startedAt||Date.now()))/1000),...publicConfig,id:peer.id,edits:[...this.edits.values()],save,players:peers.filter(p=>p.id!==peer.id&&p.positioned).map(publicPeer),animals:this.config.contentVersion>=2?this.fauna.snapshots():[],loot:this.fauna.pending(peer.playerKey,save?.claimedAnimalLoot||[])};
  }
  handle(peer,raw,now,persistEdit,persistPlayer=()=>{},persistFauna=()=>{}) {
    if(!consumeRate(peer,now))throw new Error('Rate limit');
    const p=parseMessage(raw);this.members.set(peer.id,peer);
    if(p.type==='lobby_start'){
      if(!this.config.lobby||this.config.started||this.config.ownerKey!==peer.playerKey)throw Error('Only host may start the waiting lobby');
      const next={...this.config,started:true,startedAt:now,story:this.config.story?{...this.config.story,bossMax:180+90*Math.max(0,this.members.size-1)}:null};persistFauna(next);this.config=next;return {...this.lobbyState(),type:'lobby_start',serverNow:now};
    }
    if(this.config.lobby&&!this.config.started)throw Error('Wait for host to start the lobby');
    // A room owns the lift. The command supplies an action, never a height, crew key,
    // timestamp or teleported position. Position is the latest server-recorded sample.
    if(p.type==='space_lift'){
      if(!this.config.launchSite?.active||!this.config.launchLift||this.config.runEnded||
         this.config.spaceMission||this.config.spaceVisit||now-(peer.lastLiftAction||0)<350)
        throw Error('Crew lift unavailable');
      const previous=liftAt(this.config.launchLift,now);
      const nextLift=operateLift(this.config.launchLift,this.config.launchSite,peer,p.action,now);
      const walkedOut=p.action==='exit'&&previous.progress===1&&previous.riderKey===peer.playerKey;
      const visited=this.config.padVisited||[];
      const next={...this.config,launchLift:nextLift,
        padVisited:walkedOut&&!visited.includes(peer.playerKey)?[...visited,peer.playerKey]:visited};
      persistFauna(next);this.config=next;peer.lastLiftAction=now;
      return {type:'space_lift',id:peer.id,lift:liftSnapshot(nextLift,now,[...this.members.values()])};
    }
    if(p.type==='space_hatch'){
      const site=this.config.launchSite;
      if(!site?.active||this.config.runEnded||this.config.spaceMission||this.config.spaceVisit||
         !this.config.padVisited?.includes(peer.playerKey)||
         typeof p.open!=='boolean'||!peer.positioned||now-peer.lastPosition>1500||
         Math.abs(peer.x-(site.x-9))>3.5||Math.abs(peer.z-(site.z+3))>1.5||
         Math.abs(peer.y-(site.y+30+1.58))>2)throw Error('Hatch needs crew physically at the upper deck');
      if(p.open===!!this.config.launchHatch)return {type:'space_hatch',hatchOpen:p.open,id:peer.id};
      const next={...this.config,launchHatch:p.open};persistFauna(next);this.config=next;
      return {type:'space_hatch',hatchOpen:p.open,id:peer.id};
    }
    if(p.type==='space_seat'){
      const site=this.config.launchSite,key=peer.playerKey,seats=this.config.shuttleSeats||[null,null];
      if(!site?.active||this.config.runEnded||this.config.spaceMission||this.config.spaceVisit||
         !this.config.padVisited?.includes(key)||!peer.positioned||now-peer.lastPosition>1500||
         !['sit','stand'].includes(p.action)||!integer(p.index)||p.index<0||p.index>1||
         Math.abs(peer.x-(site.x+(p.index===0?-.4:1.4)))>1.1||
         Math.abs(peer.z-(site.z+5.35))>1.1||
         Math.abs(peer.y-(site.y+44+1.58))>1.4)throw Error('Crew must walk to a real cockpit seat');
      if(p.action==='sit'&&(seats[p.index]||seats.includes(key))||
         p.action==='stand'&&seats[p.index]!==key)throw Error('Crew seat occupied or unavailable');
      const nextSeats=[...seats];nextSeats[p.index]=p.action==='sit'?key:null;
      const next={...this.config,shuttleSeats:nextSeats};persistFauna(next);this.config=next;
      return {type:'space_seat',id:peer.id,seats:nextSeats.map(k=>[...this.members.values()].find(m=>m.playerKey===k)?.id||'')};
    }
    // Pilot can ask the server for a read-only check, not an ignition command.
    // Never install a mission until the Unity client implements shared ship flight.
    if(p.type==='space_preflight'){
      if(!this.config.launchSite?.active||this.config.shuttleSeats?.[0]!==peer.playerKey)
        throw Error('Only the seated pilot can request preflight');
      return {type:'space_preflight',id:peer.id,...preflight(this.config,[...this.members.values()],now)};
    }
    // Only trusted physical-boarding code may install a mission; no network launch packet.
    // All flight updates below use the server clock and stable crew identities.
    if(['space_heartbeat','space_control','space_autopilot','space_land'].includes(p.type)) {
      const m=this.config.spaceMission,key=peer.playerKey;
      if(!m||!m.crew?.includes(key))throw Error('No authenticated shared mission');
      let nextMission;
      if(p.type==='space_heartbeat')nextMission=advanceMission(m,now);
      else if(p.type==='space_control') {
        if(m.pilot!==key)throw Error('Only the pilot steers');
        nextMission=advanceMission(m,now,{player:key,heading:p.heading,landingThrottle:p.landingThrottle??0});
      } else if(p.type==='space_autopilot')nextMission=setCruiseAutopilot(m,key,p.on,now);
      else nextMission=landMission(advanceMission(m,now),key,now);
      const next={...this.config,spaceMission:nextMission};
      persistFauna(next);this.config=next;
      return {type:'space_mission',id:peer.id,space:safeMission(nextMission,now)};
    }
    // A docked crew is not simultaneously standing in the terrestrial voxel world.
    // Old client packets cannot mine, trade with villagers or overwrite station inventory.
    if((this.config.spaceVisit?.dock?.latched&&this.config.spaceVisit.crew?.includes(peer.playerKey)||
        this.config.spaceMission&&!this.config.spaceMission.landed&&this.config.spaceMission.crew?.includes(peer.playerKey))&&
       ['position','edit','living_action','animal_hit','arrow_shot','state'].includes(p.type))
      throw Error('In-flight crew must use the shared ship or station, not the terrestrial world');
    if(p.type==='story_ready'||p.type==='story_action'){
      if(!this.config.story||this.config.runEnded)throw Error('Story unavailable');
      if(p.action==='hit'&&((p.weapon!==103&&!(this.config.contentVersion>=5&&weapons[p.weapon]))||!(this.config.story.rewards&4)))throw Error('A sword is required');
      const result=this.storyEngine.process(this.config,p,peer,[...this.members.values()],now),next={...this.config,story:result.story};
      if(result.story.stage===14)next.campaign={...next.campaign,chapter:1,unlockedMask:3};
      persistFauna(next);this.config=next;this.storyEngine=result.engine;if(p.type==='story_ready')peer.storyReady=true;if(p.action==='ack')peer.storyAckKey=`${p.stage}:${p.line}`;if(p.action==='hit')peer.lastStoryHit=now;return {clock:this.clock(now),type:'story',story:result.story,damageEvents:result.damageEvents,serverNow:now,campaign:this.config.campaign};
    }
    if(p.type==='position') {
      const site=this.config.launchSite;
      if(site?.active&&peer.positioned&&now-peer.lastPosition>0&&now-peer.lastPosition<5000&&
         Math.abs(peer.x-site.x)<70&&Math.abs(peer.z-site.z)<70&&
         finite(p.x)&&finite(p.y)&&finite(p.z)&&Math.abs(p.x-site.x)<70&&Math.abs(p.z-site.z)<70){
        const dt=(now-peer.lastPosition)/1000;
        if(Math.hypot(p.x-peer.x,p.z-peer.z)>11*dt+.4||Math.abs(p.y-peer.y)>35*dt+.4)
          throw Error('Unphysical step near launch site');
      }
      const message=validatePosition(p,peer,now,this.config.contentVersion>=4?384:64);Object.assign(peer,message,{positioned:true,lastPosition:now});message.clock=this.clock(now);this.observers.set(peer.id,{...peer,seen:now});
      for(const [id,observer] of this.observers)if(now-observer.seen>5000)this.observers.delete(id);
      if(this.config.contentVersion>=2&&!this.config.runEnded){
        const active=[...this.observers.values()].filter(p=>this.savedPlayers.get(p.playerKey)?.survival?.health!==0);
        const tick=this.fauna.tick(now,active);if(tick){Object.assign(message,tick);if(this.config.mode===2)message.damageEvents=[];if(now-this.fauna.lastPersist>=5000){const next={...this.config,wildlife:this.fauna.save()};persistFauna(next);this.config=next;this.fauna.lastPersist=now;}}
      }
      if(this.config.contentVersion>=6&&!this.config.runEnded){const candidate=new Living(this.config.seed,this.living.save(),this.edits,this.config.contentVersion);candidate.last=this.living.last;candidate.flow=this.living.flow;candidate.fluidPass=this.living.fluidPass;candidate.queue=this.living.queue.map(p=>({...p}));candidate.queued=new Set(this.living.queued);candidate.paths=new Map(this.living.paths);if(candidate.tick(now,this.clock(now),[...this.observers.values()].filter(p=>this.savedPlayers.get(p.playerKey)?.survival?.health!==0))){const next={...this.config,living:candidate.save()};persistFauna(next);this.config=next;this.living=candidate;this.fauna.dynamicCells=candidate.cells;message.living=candidate.save();if(this.config.mode!==2)message.damageEvents=[...(message.damageEvents||[]),...candidate.impacts];}}
      if(this.config.story){const result=this.storyEngine.process(this.config,{type:'tick'},peer,[...this.members.values()],now);if(result.changed){const next={...this.config,story:result.story};persistFauna(next);this.config=next;this.storyEngine=result.engine;message.story=result.story;message.serverNow=now;message.damageEvents=[...(message.damageEvents||[]),...result.damageEvents];}}
      if(this.config.launchSite?.active&&this.config.launchLift&&
         Math.abs(peer.x-this.config.launchSite.x)<60&&Math.abs(peer.z-this.config.launchSite.z)<60)
        message.lift=liftSnapshot(this.config.launchLift,now,[...this.members.values()]);
      return message;
    }
    if(p.type==='arrow_shot') {if(this.config.contentVersion<5||this.config.runEnded||!peer.positioned||this.savedPlayers.get(peer.playerKey)?.survival?.health===0||now-(peer.lastBowShot||0)<1300||!Number.isInteger(p.requestId)||p.requestId<1||p.requestId>2147483647)throw Error('Invalid bow shot / cooldown');peer.bowShots=Object.fromEntries(Object.entries(peer.bowShots||{}).filter(([id,s])=>s.until>now));if(Object.keys(peer.bowShots).length>=4)throw Error('Too many flying arrows');peer.bowShots[p.requestId]={x:peer.x,y:peer.y,z:peer.z,until:now+2500};peer.lastBowShot=now;return {type:'arrow_shot',id:peer.id,requestId:p.requestId};}
    if(p.type==='animal_hit') {
      if(this.config.contentVersion<2||this.config.runEnded||this.savedPlayers.get(peer.playerKey)?.survival?.health===0)throw Error('Wildlife interactions unavailable');
      if(!Number.isInteger(p.requestId)||p.requestId<0||p.requestId>2147483647)throw Error('Invalid attack request ID');
      const candidate=new Fauna(this.config.seed,this.fauna.save(),this.edits,this.config.contentVersion);candidate.dynamicCells=this.living.cells;const message=candidate.hit(peer,p.animal,p.weapon,now,this.config.mode===2,p.requestId),next={...this.config,wildlife:candidate.save()};
      // Death, tombstone and pending loot commit atomically BEFORE health mutation / acknowledgement.
      persistFauna(next);candidate.lastTick=this.fauna.lastTick;candidate.lastPersist=now;this.fauna=candidate;this.config=next;peer.lastAnimalHit=now;if(p.weapon===164)delete peer.bowShots[p.requestId];return message;
    }
    // Station packets are impossible in ordinary rooms. Only server-trusted flight code may
    // install spaceVisit after physically verified docking. Never accept docking, crew lists,
    // initial walking positions or player inventory from a network packet.
    if(p.type==='station_walk'||p.type==='station_trade') {
      const visit=this.config.spaceVisit,key=peer.playerKey;
      if(this.config.runEnded||!visit?.dock?.latched||!Array.isArray(visit.crew)||
         !visit.crew.includes(key)||!Array.isArray(visit.positions?.[key]))throw Error('No authenticated docked crew');
      if(p.type==='station_walk') {
        if(now-(peer.lastStationWalk||0)<60||!stationWalk(visit.dock,visit.positions[key],p.local))
          throw Error('Station step or hatch invalid');
        const next={...this.config,spaceVisit:{...visit,positions:{...visit.positions,[key]:[...p.local]}}};
        persistFauna(next);this.config=next;peer.lastStationWalk=now;
        return {type:'station_position',id:peer.id,local:[...p.local]};
      }
      if(!Number.isInteger(p.requestId)||p.requestId<1||p.requestId<= Math.max(peer.lastStationTradeRequest||0,visit.tradeRequests?.[key]||0)||
         now-(peer.lastStationTrade||0)<350)throw Error('Station trade replay or cooldown');
      const previous=this.savedPlayers.get(key);
      if(!previous||previous.survival?.health<=0)throw Error('No healthy saved player');
      const save=validatePlayerSave(previous,true,true,true);
      const updated=stationTrade(visit.dock,{crewVerified:true,local:visit.positions[key],role:p.role,save});
      // Persist replay marker and inventory atomically before acknowledging. A reconnect
      // cannot resubmit the same request, and a full bag never consumes payment.
      const next={...this.config,spaceVisit:{...visit,tradeRequests:{...visit.tradeRequests,[key]:p.requestId}}};
      persistPlayer(key,updated,next);this.config=next;this.savedPlayers.set(key,updated);
      peer.lastStationTradeRequest=p.requestId;peer.lastStationTrade=now;
      return {type:'station_trade',id:peer.id,requestId:p.requestId,role:p.role,privateSave:updated};
    }
    if(p.type==='living_action') {
      if(this.config.contentVersion<6||this.config.runEnded||!Number.isInteger(p.requestId)||p.requestId<1||p.requestId<= (peer.lastLivingRequest||0)||now-(peer.lastLivingAction||0)<350)throw Error('Living interaction unavailable / cooldown');
      const save=validatePlayerSave(p.save,true,true,true),previous=this.savedPlayers.get(peer.playerKey);
      if(previous&&save.inventory.revision<previous.inventory.revision)throw Error('Stale inventory');
      if((save.claimedStoryRewards&~(this.config.story?.rewards||0))!==0||((previous?.claimedStoryRewards||0)&~save.claimedStoryRewards)!==0)throw Error('Invalid story claims');
      for(const id of save.claimedAnimalLoot)if(!this.fauna.loot.some(l=>l.id===id&&l.key===peer.playerKey))throw Error('Unknown loot claim');
      for(const id of previous?.claimedAnimalLoot||[])if(!save.claimedAnimalLoot.includes(id))throw Error('Loot rollback');
      const candidate=new Living(this.config.seed,this.living.save(),this.edits,this.config.contentVersion);candidate.last=this.living.last;candidate.flow=this.living.flow;candidate.fluidPass=this.living.fluidPass;candidate.queue=this.living.queue.map(p=>({...p}));candidate.queued=new Set(this.living.queued);candidate.paths=new Map(this.living.paths);
      transact(candidate,p,peer,save);const next={...this.config,living:candidate.save()};persistPlayer(peer.playerKey,save,next);this.config=next;this.living=candidate;this.fauna.dynamicCells=candidate.cells;this.savedPlayers.set(peer.playerKey,save);peer.lastLivingRequest=p.requestId;peer.lastLivingAction=now;
      return {type:'living',id:peer.id,requestId:p.requestId,living:candidate.save(),privateSave:save};
    }
    if(p.type==='chat') {
      if(typeof p.message!=='string'||p.message.length>240)throw new Error('Chat must be at most 240 characters');
      const message=p.message.replace(/[<>\x00-\x1f\x7f]/g,'').trim();
      if(!message)throw new Error('Empty chat message');
      if(now-(peer.lastChat??-10000)<1000)throw new Error('Chat cooldown');
      peer.lastChat=now;
      return {type:'chat',id:peer.id,name:peer.name,message};
    }
    if(p.type==='edit') {
      if(this.config.story&&protectedSite(p.x,p.z))throw Error('This story landmark is protected');
      if(this.config.runEnded)throw new Error('Hardcore run has ended');
      const edit=validateEdit(p,peer,this.edits,now,this.config.contentVersion>=7?53:this.config.contentVersion>=5?48:this.config.contentVersion>=2?46:18,this.config.contentVersion>=4?384:64),record={x:edit.x,y:edit.y,z:edit.z,block:edit.block,rev:this.config.rev+1};
      // Persistence succeeds before acknowledgement/broadcast and memory mutation.
      let candidate=null,next={...this.config,rev:record.rev};if(this.config.contentVersion>=6){candidate=new Living(this.config.seed,this.living.save(),this.edits,this.config.contentVersion);candidate.queue=this.living.queue.map(p=>({...p}));candidate.queued=new Set(this.living.queued);candidate.last=this.living.last;candidate.flow=this.living.flow;candidate.fluidPass=this.living.fluidPass;const before=this.living.block(edit.x,edit.y,edit.z);candidate.edited(edit);if(edit.block===0&&before>0&&before!==7&&before!==8&&this.config.mode!==2&&!candidate.drop(toolDrop(before),1,0,edit.x+.5,edit.y+.5,edit.z+.5))throw Error('Dropped item limit');next.living=candidate.save();}persistEdit(record,next);this.config=next;if(candidate){this.living=candidate;this.fauna.dynamicCells=candidate.cells;}this.config.rev=record.rev;this.edits.set(edit.key,record);peer.lastEdit=now;
      return {type:'edit',...record,id:peer.id,living:candidate?candidate.save():undefined};
    }
    if(p.type==='state') {
      if(![2,3,4,5,6,7,8,9,10,RULES_VERSION].includes(this.config.rulesVersion)||!validCode(peer.playerKey))throw new Error('Persistent player key required');
      if(this.config.runEnded)throw new Error('Hardcore run has ended');
      const save=validatePlayerSave(p.save,this.config.contentVersion>=2,this.config.contentVersion>=4,this.config.contentVersion>=5);if((save.claimedStoryRewards&~(this.config.story?.rewards||0))!==0||((this.savedPlayers.get(peer.playerKey)?.claimedStoryRewards||0)&~save.claimedStoryRewards)!==0)throw Error("Invalid story rewards / rollback");const dead=save.survival.health<=0,previous=this.savedPlayers.get(peer.playerKey),firstDeath=dead&&(!previous||previous.survival.health>0);
      if(previous&&save.inventory.revision<previous.inventory.revision)throw Error('Stale inventory after confirmed transaction');
      for(const id of save.claimedAnimalLoot){if(!this.fauna.loot.some(l=>l.id===id&&l.key===peer.playerKey))throw Error('Unknown animal loot claim');}
      for(const id of previous?.claimedAnimalLoot||[])if(!save.claimedAnimalLoot.includes(id))throw Error('Animal loot claims cannot be rolled back');
      const forced=p.force===true;
      if(peer.lastState>0&&now-peer.lastState<19000&&!firstDeath&&!(forced&&(peer.lastForcedSave===0||now-peer.lastForcedSave>=60000)))throw new Error('Save update too frequent');
      if(!this.savedPlayers.has(peer.playerKey)&&this.savedPlayers.size>=64)throw new Error('Saved player limit reached');
      const ended=this.config.mode===1&&dead,next={...this.config,runEnded:ended||this.config.runEnded,campaign:{...this.config.campaign,milestones:(this.config.campaign?.milestones||0)|save.milestones,runEnded:ended||this.config.runEnded}};
      // Stats/inventory are trusted for friends; this is validation and persistence, not anti-cheat.
      persistPlayer(peer.playerKey,save,next);
      this.savedPlayers.set(peer.playerKey,save);this.config=next;peer.lastState=now;if(forced)peer.lastForcedSave=now;
      return ended?{type:'run_ended',id:peer.id,message:'A teammate died. The entire hardcore run is over.',runEnded:true}:{type:'state_saved',id:peer.id,campaign:next.campaign};
    }
    throw new Error('Unknown packet type');
  }
}

export function validatePlayerSave(save,expanded=true,chapter=false,expedition=false) {
  if(!save||typeof save!=='object'||!save.inventory||!save.survival)throw new Error('Invalid player save');
  const maxStack=id=>(id>=150&&id<=164)?1:((id>=100&&id<=108)||(id>=130&&id<=132))?1:[110,111,112,135,138].includes(id)?32:64;
  const validItem=id=>integer(id)&&(id===0||(expedition&&((id>=149&&id<=165)||id===47||id===48))||(chapter&&id===148)||(id>=1&&id<=17)||(expanded&&id>=19&&id<=46)||(expanded&&id>=49&&id<=53)||(id>=100&&id<=108)||(id>=130&&id<=132)||(id>=110&&id<=114)||(id>=120&&id<=123)||(expanded&&[135,136,138,139,140,141,142,143,144,145,146,147].includes(id))||(id>=170&&id<=177));
  const cleanStack=s=>{
    if(!s||!validItem(s.item)||!integer(s.count)||s.count<0||s.count>maxStack(s.item)||!integer(s.durability)||s.durability<0||s.durability>1500||(s.item===0)!==(s.count===0))throw new Error('Invalid item stack');
    const life={150:450,151:1500,...Object.fromEntries(Object.entries(weapons).map(([id])=>[id,({152:380,153:1100,154:260,155:800,156:340,157:1050,158:420,159:1200,160:500,161:1400,162:450,163:1300,164:400})[id]])),100:60,101:140,102:120,103:160,105:180,106:260,107:220,108:180};if(s.durability>(life[s.item]||0))throw new Error('Invalid tool durability');
    return {item:s.item,count:s.count,durability:s.durability};
  };
  const empty=()=>({item:0,count:0,durability:0});
  const extras=(value,length)=>{if(value===undefined)return Array.from({length},empty);if(!Array.isArray(value)||value.length!==length)throw new Error('Invalid crafting/storage size');return value.map(cleanStack);};
  const inv=save.inventory;
  const offhand=cleanStack(inv.offhand||empty());if(offhand.count&&(offhand.item!==148||offhand.count!==1||!chapter))throw Error("Invalid offhand slot");
  const claimedStoryRewards=save.claimedStoryRewards||0;if(!integer(claimedStoryRewards)||claimedStoryRewards<0||claimedStoryRewards>15)throw Error("Invalid story claims");
  if(!Array.isArray(inv.slots)||inv.slots.length!==45||!integer(inv.selected)||inv.selected<0||inv.selected>8)throw new Error('Expected 45 inventory slots and selected hotbar 0..8');
  const equipment=extras(inv.equipment,8);validateEquipmentIds(equipment.map(s=>s.count?s.item:0));
  const slots=inv.slots.map(cleanStack),grid=extras(inv.grid,9),cursor=cleanStack(inv.cursor||empty());
  const raw=inv.furnace||{},furnace={input:cleanStack(raw.input||empty()),fuel:cleanStack(raw.fuel||empty()),output:cleanStack(raw.output||empty())};
  for(const [key,min,max] of [['burn',0,40],['progress',0,8],['recipe',0,147],['x',chapter?-384:-64,chapter?383:63],['y',0,255],['z',chapter?-384:-64,chapter?383:63],['realm',0,4]]){
    const value=raw[key]===undefined?0:raw[key];if(!finite(value)||value<min||value>max||(!['burn','progress'].includes(key)&&!integer(value)))throw new Error('Invalid furnace '+key);furnace[key]=value;
  }
  let grave=[],graveExtras=[];
  if(save.grave?.length){
    if(!Array.isArray(save.grave)||save.grave.length!==45)throw new Error('Invalid grave inventory');
    grave=save.grave.map(cleanStack);if(save.graveExtras?.length&&![13,21,22].includes(save.graveExtras.length))throw new Error('Invalid grave equipment size');graveExtras=save.graveExtras?.length?extras(save.graveExtras,save.graveExtras.length):[];
    for(const key of ['graveX','graveY','graveZ'])if(!finite(save[key])||Math.abs(save[key])>32000)throw new Error('Invalid grave position');
  }
  const claimedAnimalLoot=save.claimedAnimalLoot||[];
  if(!Array.isArray(claimedAnimalLoot)||claimedAnimalLoot.length>256||new Set(claimedAnimalLoot).size!==claimedAnimalLoot.length||claimedAnimalLoot.some(id=>typeof id!=='string'||!/^a:-?\d{1,5}:-?\d{1,5}$/.test(id)))throw Error('Invalid animal loot claims');
  const v=save.survival;
  for(const [key,min,max] of [['health',0,100],['hunger',0,100],['stamina',0,100],['temperature',25,43],['wetness',0,1]])if(!finite(v[key])||v[key]<min||v[key]>max)throw new Error('Invalid survival '+key);
  const air=v.air===undefined?100:v.air;if(!finite(air)||air<0||air>100)throw new Error('Invalid air');
  const body=v.body===undefined?{hp:Array(6).fill(v.health),trauma:0}:v.body;
  if(!body||!Array.isArray(body.hp)||body.hp.length!==6||body.hp.some(h=>!finite(h)||h<0||h>100)||!finite(body.trauma)||body.trauma<0||body.trauma>120)throw Error('Invalid body injuries');
  if(v.health>0&&(body.hp[0]===0||body.hp[1]===0))throw Error('Vital injury must be fatal');
  return {claimedStoryRewards,claimedAnimalLoot:[...claimedAnimalLoot],grave,graveExtras,graveX:grave.length?save.graveX:0,graveY:grave.length?save.graveY:0,graveZ:grave.length?save.graveZ:0,milestones:integer(save.milestones)?save.milestones&3:0,inventory:{slots,grid,cursor,furnace,equipment,offhand,selected:inv.selected,revision:integer(inv.revision)?inv.revision:0},survival:{body:{hp:[...body.hp],trauma:body.trauma},health:v.health,hunger:v.hunger,stamina:v.stamina,temperature:v.temperature,wetness:v.wetness,air,dead:v.health<=0},equippedCoat:save.equippedCoat===true&&slots.some(s=>s.item===104&&s.count===1)};
}

export function validateEquipmentIds(ids){
 const allowed=[130,104,131,132,105,106,107,108];
 if(!Array.isArray(ids)||ids.length!==8||ids.some((id,i)=>!Number.isInteger(id)||(id!==0&&id!==allowed[i])))throw new Error('Invalid equipment slot');return [...ids];
}

// New content must never reach a pre-expansion client (unknown IDs would become air).
export function acceptsClient(config,clientRules){const n=Number(clientRules),min=config.launchSite?.active?11:config.contentVersion>=7?10:config.contentVersion>=6?9:config.contentVersion>=5?8:config.contentVersion>=4?7:config.contentVersion>=3?6:config.contentVersion>=2?5:2;return config.contentVersion<2&&(clientRules===null||clientRules===undefined)||Number.isInteger(n)&&n>=min&&n<=RULES_VERSION;}

export function packetError(raw,error){let p={};try{if(typeof raw==='string'&&raw.length<=MAX_MESSAGE)p=JSON.parse(raw)||{};}catch{}return {type:'error',message:error.message,code:typeof p.type==='string'?p.type:'',requestId:Number.isInteger(p.requestId)?p.requestId:0};}

export function publicConfig(config){const {ownerKey,wildlife,living,spaceVisit,spaceMission,launchLift,padVisited,shuttleSeats,...safe}=config;return safe;}
