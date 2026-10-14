export const VERSION = 1;
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
  return { version: VERSION, seed: value.seed, dimension: value.dimension, rev: 0, maxPlayers: MAX_PLAYERS };
}
export function newPeer(id, name) {return {id,name:safeName(name),x:0,y:90,z:0,yaw:0,positioned:false,windowStart:0,count:0,lastPosition:0,lastEdit:0};}
export function publicPeer(p) {return {id:p.id,name:p.name,x:p.x,y:p.y,z:p.z,yaw:p.yaw};}
export function consumeRate(peer, now) {
  if (now - peer.windowStart >= 1000) {peer.windowStart=now;peer.count=0;}
  return ++peer.count <= 15;
}
export function parseMessage(raw) {
  if (typeof raw !== 'string' || raw.length > 2048) throw new Error('Text message must be at most 2048 bytes');
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
  if(!integer(p.x)||!integer(p.y)||!integer(p.z)||!integer(p.block)||p.x < -64||p.x>=64||p.z < -64||p.z>=64||p.y<=0||p.y>=256||p.block<0||p.block>14||p.block===9)throw new Error('Invalid block edit (bedrock and y=0 are protected)');
  if(!peer.positioned)throw new Error('Send position before editing');
  if(now-peer.lastEdit<100)throw new Error('Block update too frequent');
  if(Math.hypot(p.x+.5-peer.x,p.y+.5-peer.y,p.z+.5-peer.z)>8)throw new Error('Block out of reach');
  const key=`${p.x},${p.y},${p.z}`;
  if(!edits.has(key)&&edits.size>=MAX_EDITS)throw new Error('Room edit limit reached');
  return {key,x:p.x,y:p.y,z:p.z,block:p.block};
}
export class RoomState {
  constructor(config,edits=[]) {this.config=config;this.edits=new Map(edits.map(e=>[`${e.x},${e.y},${e.z}`,e]));}
  welcome(peer,peers) {return {type:'welcome',...this.config,id:peer.id,edits:[...this.edits.values()],players:peers.filter(p=>p.id!==peer.id&&p.positioned).map(publicPeer)};}
  handle(peer,raw,now,persistEdit) {
    if(!consumeRate(peer,now))throw new Error('Rate limit');
    const p=parseMessage(raw);
    if(p.type==='position') {const message=validatePosition(p,peer,now);Object.assign(peer,message,{positioned:true,lastPosition:now});return message;}
    if(p.type==='edit') {
      const edit=validateEdit(p,peer,this.edits,now),record={x:edit.x,y:edit.y,z:edit.z,block:edit.block,rev:this.config.rev+1};
      // Persistence succeeds before acknowledgement/broadcast and memory mutation.
      persistEdit(record);this.config.rev=record.rev;this.edits.set(edit.key,record);peer.lastEdit=now;
      return {type:'edit',...record,id:peer.id};
    }
    throw new Error('Unknown packet type');
  }
}
