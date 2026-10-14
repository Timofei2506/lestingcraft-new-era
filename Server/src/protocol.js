export const VERSION = 1;
export const RULES_VERSION = 2;
export const MAX_MESSAGE = 16384;
export const MAX_PLAYERS = 8;
export const MAX_EDITS = 4096;
export const BOUNDS = { min: -64, max: 64, height: 256 };
export const validCode = code => typeof code === 'string' && /^[a-f0-9]{32}$/.test(code);
export const safeName = name => String(name || 'Player').replace(/[<>\x00-\x1f]/g, '').slice(0, 24) || 'Player';
export const randomCode = () => crypto.randomUUID().replaceAll('-', '');
const integer = v => typeof v === 'number' && Number.isInteger(v);
const finite = v => typeof v === 'number' && Number.isFinite(v);
export function createConfig(value) {
  if (!value || value.version !== VERSION || !integer(value.seed) || value.seed < -2147483648 || value.seed > 2147483647 || !integer(value.dimension) || value.dimension < 0 || value.dimension > 4)
    throw new Error('Expected version=1, signed 32-bit seed, dimension=0..4');
  if(value.rulesVersion!==undefined&&value.rulesVersion!==RULES_VERSION)throw new Error('Unsupported game rules');
  if(value.rulesVersion===RULES_VERSION&&(!integer(value.mode)||value.mode<0||value.mode>3||value.dimension!==0))throw new Error('Choose mode 0..3; new campaigns must start in Lestingia');
  return { version: VERSION, rulesVersion:value.rulesVersion||1, mode:value.rulesVersion===RULES_VERSION?value.mode:2,seed:value.seed,dimension:value.dimension,rev:0,maxPlayers:MAX_PLAYERS,startedAt:Date.now(),runEnded:false,campaign:{chapter:0,unlockedMask:1,milestones:0,runEnded:false} };
}
export function newPeer(id, name,playerKey=null) {return {id,playerKey,name:safeName(name),lastState:0,x:0,y:90,z:0,yaw:0,positioned:false,windowStart:0,count:0,lastPosition:0,lastEdit:0};}
export function publicPeer(p) {return {id:p.id,name:p.name,x:p.x,y:p.y,z:p.z,yaw:p.yaw};}
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
export function validatePosition(p,peer,now) {
  if (!finite(p.x)||!finite(p.y)||!finite(p.z)||!finite(p.yaw)||p.x < -64||p.x>=64||p.z < -64||p.z>=64||p.y<0||p.y>256||Math.abs(p.yaw)>360000)throw new Error('Position outside room bounds');
  if(now-peer.lastPosition<80)throw new Error('Position update too frequent');
  // Client-authoritative movement: reach validation is not an anti-cheat.
  return {type:'player',id:peer.id,name:peer.name,x:p.x,y:p.y,z:p.z,yaw:((p.yaw%360)+360)%360};
}
export function validateEdit(p,peer,edits,now) {
  if(!integer(p.x)||!integer(p.y)||!integer(p.z)||!integer(p.block)||p.x < -64||p.x>=64||p.z < -64||p.z>=64||p.y<=0||p.y>=256||p.block<0||p.block>15||p.block===9)throw new Error('Invalid block edit (bedrock and y=0 are protected)');
  if(!peer.positioned)throw new Error('Send position before editing');
  if(now-peer.lastEdit<100)throw new Error('Block update too frequent');
  if(Math.hypot(p.x+.5-peer.x,p.y+.5-peer.y,p.z+.5-peer.z)>8)throw new Error('Block out of reach');
  const key=`${p.x},${p.y},${p.z}`;
  if(!edits.has(key)&&edits.size>=MAX_EDITS)throw new Error('Room edit limit reached');
  return {key,x:p.x,y:p.y,z:p.z,block:p.block};
}
export class RoomState {
  constructor(config,edits=[],savedPlayers=[]) {this.config=config;this.savedPlayers=new Map(savedPlayers.map(s=>[s.key,s.save]));this.edits=new Map(edits.map(e=>[`${e.x},${e.y},${e.z}`,e]));}
  welcome(peer,peers) {return {type:'welcome',clock:420+Math.max(0,(Date.now()-(this.config.startedAt||Date.now()))/1000),...this.config,id:peer.id,edits:[...this.edits.values()],save:this.savedPlayers.get(peer.playerKey)||null,players:peers.filter(p=>p.id!==peer.id&&p.positioned).map(publicPeer)};}
  handle(peer,raw,now,persistEdit,persistPlayer=()=>{}) {
    if(!consumeRate(peer,now))throw new Error('Rate limit');
    const p=parseMessage(raw);
    if(p.type==='position') {const message=validatePosition(p,peer,now);Object.assign(peer,message,{positioned:true,lastPosition:now});return message;}
    if(p.type==='edit') {
      if(this.config.runEnded)throw new Error('Hardcore run has ended');
      const edit=validateEdit(p,peer,this.edits,now),record={x:edit.x,y:edit.y,z:edit.z,block:edit.block,rev:this.config.rev+1};
      // Persistence succeeds before acknowledgement/broadcast and memory mutation.
      persistEdit(record);this.config.rev=record.rev;this.edits.set(edit.key,record);peer.lastEdit=now;
      return {type:'edit',...record,id:peer.id};
    }
    if(p.type==='state') {
      if(this.config.rulesVersion!==RULES_VERSION||!validCode(peer.playerKey))throw new Error('Persistent player key required');
      if(this.config.runEnded)throw new Error('Hardcore run has ended');
      const save=validatePlayerSave(p.save),dead=save.survival.health<=0;
      if(now-peer.lastState<9000&&!dead)throw new Error('Save update too frequent');
      if(!this.savedPlayers.has(peer.playerKey)&&this.savedPlayers.size>=64)throw new Error('Saved player limit reached');
      const ended=this.config.mode===1&&dead,next={...this.config,runEnded:ended||this.config.runEnded,campaign:{...this.config.campaign,milestones:(this.config.campaign?.milestones||0)|save.milestones,runEnded:ended||this.config.runEnded}};
      // Stats/inventory are trusted for friends; this is validation and persistence, not anti-cheat.
      persistPlayer(peer.playerKey,save,next);
      this.savedPlayers.set(peer.playerKey,save);this.config=next;peer.lastState=now;
      return ended?{type:'run_ended',id:peer.id,message:'A teammate died. The entire hardcore run is over.',runEnded:true}:{type:'state_saved',id:peer.id,campaign:next.campaign};
    }
    throw new Error('Unknown packet type');
  }
}

export function validatePlayerSave(save) {
  if(!save||typeof save!=='object'||!save.inventory||!save.survival)throw new Error('Invalid player save');
  const maxStack=id=>[100,101,102,103,104].includes(id)?1:[110,111].includes(id)?32:64;
  const validItem=id=>integer(id)&&(id===0||(id>=1&&id<=15)||(id>=100&&id<=104)||id===110||id===111||(id>=120&&id<=122));
  const inv=save.inventory;
  if(!Array.isArray(inv.slots)||inv.slots.length!==45||!integer(inv.selected)||inv.selected<0||inv.selected>8)throw new Error('Expected 45 inventory slots and selected hotbar 0..8');
  const slots=inv.slots.map(s=>{
    if(!s||!validItem(s.item)||!integer(s.count)||s.count<0||s.count>maxStack(s.item)||!integer(s.durability)||s.durability<0||s.durability>160||(s.item===0)!==(s.count===0))throw new Error('Invalid item stack');
    return {item:s.item,count:s.count,durability:s.durability};
  });
  let grave=[];
  if(save.grave?.length){
    if(!Array.isArray(save.grave)||save.grave.length!==45)throw new Error('Invalid grave inventory');
    grave=validatePlayerSave({...save,grave:[],inventory:{...inv,slots:save.grave}}).inventory.slots;
    for(const key of ['graveX','graveY','graveZ'])if(!finite(save[key])||Math.abs(save[key])>32000)throw new Error('Invalid grave position');
  }
  const v=save.survival;
  for(const [key,min,max] of [['health',0,100],['hunger',0,100],['stamina',0,100],['temperature',25,43],['wetness',0,1]])if(!finite(v[key])||v[key]<min||v[key]>max)throw new Error('Invalid survival '+key);
  return {grave,graveX:grave.length?save.graveX:0,graveY:grave.length?save.graveY:0,graveZ:grave.length?save.graveZ:0,milestones:integer(save.milestones)?save.milestones&3:0,inventory:{slots,selected:inv.selected,revision:integer(inv.revision)?inv.revision:0},survival:{health:v.health,hunger:v.hunger,stamina:v.stamina,temperature:v.temperature,wetness:v.wetness,dead:v.health<=0},equippedCoat:save.equippedCoat===true&&slots.some(s=>s.item===104&&s.count===1)};
}
