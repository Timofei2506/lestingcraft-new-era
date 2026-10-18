export const VERSION = 1;
export const RULES_VERSION = 4;
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
  if(value.rulesVersion!==undefined&&![2,3,RULES_VERSION].includes(value.rulesVersion))throw new Error('Unsupported game rules');
  if([2,3,RULES_VERSION].includes(value.rulesVersion)&&(!integer(value.mode)||value.mode<0||value.mode>3||value.dimension!==0))throw new Error('Choose mode 0..3; new campaigns must start in Lestingia');
  return { version: VERSION, rulesVersion:value.rulesVersion||1, mode:[2,3,RULES_VERSION].includes(value.rulesVersion)?value.mode:2,seed:value.seed,dimension:value.dimension,rev:0,maxPlayers:MAX_PLAYERS,startedAt:Date.now(),runEnded:false,campaign:{chapter:0,unlockedMask:1,milestones:0,runEnded:false} };
}
export function newPeer(id, name,playerKey=null) {return {id,playerKey,name:safeName(name),lastState:0,lastForcedSave:0,x:0,y:90,z:0,yaw:0,positioned:false,windowStart:0,count:0,lastPosition:0,lastEdit:0,lastChat:-10000};}
export function publicPeer(p) {return {id:p.id,name:p.name,x:p.x,y:p.y,z:p.z,yaw:p.yaw,equipment:p.equipment||Array(8).fill(0)};}
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
  const equipment=validateEquipmentIds(p.equipment===undefined?(peer.equipment||Array(8).fill(0)):p.equipment);
  return {type:'player',id:peer.id,name:peer.name,x:p.x,y:p.y,z:p.z,yaw:((p.yaw%360)+360)%360,equipment};
}
export function validateEdit(p,peer,edits,now) {
  if(!integer(p.x)||!integer(p.y)||!integer(p.z)||!integer(p.block)||p.x < -64||p.x>=64||p.z < -64||p.z>=64||p.y<=0||p.y>=256||p.block<0||p.block>18||p.block===9)throw new Error('Invalid block edit (bedrock and y=0 are protected)');
  if(!peer.positioned)throw new Error('Send position before editing');
  if(now-peer.lastEdit<100)throw new Error('Block update too frequent');
  if(Math.hypot(p.x+.5-peer.x,p.y+.5-peer.y,p.z+.5-peer.z)>8)throw new Error('Block out of reach');
  const key=`${p.x},${p.y},${p.z}`;
  if(!edits.has(key)&&edits.size>=MAX_EDITS)throw new Error('Room edit limit reached');
  return {key,x:p.x,y:p.y,z:p.z,block:p.block};
}
export class RoomState {
  constructor(config,edits=[],savedPlayers=[]) {this.config=config;this.savedPlayers=new Map(savedPlayers.map(s=>[s.key,s.save]));this.edits=new Map(edits.map(e=>[`${e.x},${e.y},${e.z}`,e]));}
  welcome(peer,peers) {return {type:'welcome',serverRulesVersion:RULES_VERSION,clock:420+Math.max(0,(Date.now()-(this.config.startedAt||Date.now()))/1000),...this.config,id:peer.id,edits:[...this.edits.values()],save:this.savedPlayers.get(peer.playerKey)||null,players:peers.filter(p=>p.id!==peer.id&&p.positioned).map(publicPeer)};}
  handle(peer,raw,now,persistEdit,persistPlayer=()=>{}) {
    if(!consumeRate(peer,now))throw new Error('Rate limit');
    const p=parseMessage(raw);
    if(p.type==='position') {const message=validatePosition(p,peer,now);Object.assign(peer,message,{positioned:true,lastPosition:now});return message;}
    if(p.type==='chat') {
      if(typeof p.message!=='string'||p.message.length>240)throw new Error('Chat must be at most 240 characters');
      const message=p.message.replace(/[<>\x00-\x1f\x7f]/g,'').trim();
      if(!message)throw new Error('Empty chat message');
      if(now-(peer.lastChat??-10000)<1000)throw new Error('Chat cooldown');
      peer.lastChat=now;
      return {type:'chat',id:peer.id,name:peer.name,message};
    }
    if(p.type==='edit') {
      if(this.config.runEnded)throw new Error('Hardcore run has ended');
      const edit=validateEdit(p,peer,this.edits,now),record={x:edit.x,y:edit.y,z:edit.z,block:edit.block,rev:this.config.rev+1};
      // Persistence succeeds before acknowledgement/broadcast and memory mutation.
      persistEdit(record);this.config.rev=record.rev;this.edits.set(edit.key,record);peer.lastEdit=now;
      return {type:'edit',...record,id:peer.id};
    }
    if(p.type==='state') {
      if(![2,3,RULES_VERSION].includes(this.config.rulesVersion)||!validCode(peer.playerKey))throw new Error('Persistent player key required');
      if(this.config.runEnded)throw new Error('Hardcore run has ended');
      const save=validatePlayerSave(p.save),dead=save.survival.health<=0,previous=this.savedPlayers.get(peer.playerKey),firstDeath=dead&&(!previous||previous.survival.health>0);
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

export function validatePlayerSave(save) {
  if(!save||typeof save!=='object'||!save.inventory||!save.survival)throw new Error('Invalid player save');
  const maxStack=id=>((id>=100&&id<=108)||(id>=130&&id<=132))?1:[110,111,112].includes(id)?32:64;
  const validItem=id=>integer(id)&&(id===0||(id>=1&&id<=17)||(id>=100&&id<=108)||(id>=130&&id<=132)||(id>=110&&id<=114)||(id>=120&&id<=123));
  const cleanStack=s=>{
    if(!s||!validItem(s.item)||!integer(s.count)||s.count<0||s.count>maxStack(s.item)||!integer(s.durability)||s.durability<0||s.durability>260||(s.item===0)!==(s.count===0))throw new Error('Invalid item stack');
    const life={100:60,101:140,102:120,103:160,105:180,106:260,107:220,108:180};if(s.durability>(life[s.item]||0))throw new Error('Invalid tool durability');
    return {item:s.item,count:s.count,durability:s.durability};
  };
  const empty=()=>({item:0,count:0,durability:0});
  const extras=(value,length)=>{if(value===undefined)return Array.from({length},empty);if(!Array.isArray(value)||value.length!==length)throw new Error('Invalid crafting/storage size');return value.map(cleanStack);};
  const inv=save.inventory;
  if(!Array.isArray(inv.slots)||inv.slots.length!==45||!integer(inv.selected)||inv.selected<0||inv.selected>8)throw new Error('Expected 45 inventory slots and selected hotbar 0..8');
  const equipment=extras(inv.equipment,8);validateEquipmentIds(equipment.map(s=>s.count?s.item:0));
  const slots=inv.slots.map(cleanStack),grid=extras(inv.grid,9),cursor=cleanStack(inv.cursor||empty());
  const raw=inv.furnace||{},furnace={input:cleanStack(raw.input||empty()),fuel:cleanStack(raw.fuel||empty()),output:cleanStack(raw.output||empty())};
  for(const [key,min,max] of [['burn',0,40],['progress',0,8],['recipe',0,123],['x',-64,63],['y',0,255],['z',-64,63],['realm',0,4]]){
    const value=raw[key]===undefined?0:raw[key];if(!finite(value)||value<min||value>max||(!['burn','progress'].includes(key)&&!integer(value)))throw new Error('Invalid furnace '+key);furnace[key]=value;
  }
  let grave=[],graveExtras=[];
  if(save.grave?.length){
    if(!Array.isArray(save.grave)||save.grave.length!==45)throw new Error('Invalid grave inventory');
    grave=save.grave.map(cleanStack);if(save.graveExtras?.length&&![13,21].includes(save.graveExtras.length))throw new Error('Invalid grave equipment size');graveExtras=save.graveExtras?.length?extras(save.graveExtras,save.graveExtras.length):[];
    for(const key of ['graveX','graveY','graveZ'])if(!finite(save[key])||Math.abs(save[key])>32000)throw new Error('Invalid grave position');
  }
  const v=save.survival;
  for(const [key,min,max] of [['health',0,100],['hunger',0,100],['stamina',0,100],['temperature',25,43],['wetness',0,1]])if(!finite(v[key])||v[key]<min||v[key]>max)throw new Error('Invalid survival '+key);
  const air=v.air===undefined?100:v.air;if(!finite(air)||air<0||air>100)throw new Error('Invalid air');
  return {grave,graveExtras,graveX:grave.length?save.graveX:0,graveY:grave.length?save.graveY:0,graveZ:grave.length?save.graveZ:0,milestones:integer(save.milestones)?save.milestones&3:0,inventory:{slots,grid,cursor,furnace,equipment,selected:inv.selected,revision:integer(inv.revision)?inv.revision:0},survival:{health:v.health,hunger:v.hunger,stamina:v.stamina,temperature:v.temperature,wetness:v.wetness,air,dead:v.health<=0},equippedCoat:save.equippedCoat===true&&slots.some(s=>s.item===104&&s.count===1)};
}

export function validateEquipmentIds(ids){
 const allowed=[130,104,131,132,105,106,107,108];
 if(!Array.isArray(ids)||ids.length!==8||ids.some((id,i)=>!Number.isInteger(id)||(id!==0&&id!==allowed[i])))throw new Error('Invalid equipment slot');return [...ids];
}
