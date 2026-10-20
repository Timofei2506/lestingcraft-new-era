// Server-side station visit rules. This is a domain module: room commands must NOT expose
// it until the room has authoritative craft motion, physical walking and crew membership.
import {EARTH_X} from './space-mission.js';
export const CENTER=[EARTH_X+6771044,0,0];
export const PORT=[CENTER[0]-44,21,0]; // exterior model local collar (-44,21,0)
export const PRESSURE_MS=8000;
export const CREW=[
  {title:'Бортинженер',position:[5,0,0],offer:{input:141,cost:6,output:177,amount:4}},
  {title:'Специалист по кислороду',position:[11,0,0],offer:{input:177,cost:9,output:174,amount:2}},
  {title:'Квартирмейстер',position:[17,0,0],offer:{input:140,cost:4,output:111,amount:4}},
];
const finite=n=>typeof n==='number'&&Number.isFinite(n);
const vec=v=>Array.isArray(v)&&v.length===3&&v.every(finite);
const length=v=>Math.hypot(...v);
const subtract=(a,b)=>a.map((n,i)=>n-b[i]);
const distance=(a,b)=>length(subtract(a,b));
const dot=(a,b)=>a.reduce((n,v,i)=>n+v*b[i],0);
const inside=p=>vec(p)&&p[0]>=-44&&p[0]<=20&&p[1]>=0&&p[1]<=2.05&&Math.abs(p[2])<=1.15;

// shipNose/velocity/facing must be computed from room-owned physics, never accepted as client payload.
export function latchStation(dock,{shipNose,relativeVelocity,forward,portNormal,shipHatchSealed,pilotAuthorized,now}) {
  if(dock?.latched||!finite(now)||now<0||!vec(shipNose)||!vec(relativeVelocity)||!vec(forward)||!vec(portNormal)||
     distance(shipNose,PORT)>2||length(relativeVelocity)>.35||length(forward)<.99||length(portNormal)<.99||
     dot(forward,portNormal)/(length(forward)*length(portNormal))>-.96||!shipHatchSealed||!pilotAuthorized)
    throw Error('Docking alignment, speed, seal or pilot authorization failed');
  return {latched:true,hatchOpen:false,latchedAt:now}; // no position reset, relocation or scene change
}
export function setStationHatch(dock,open,now) {
  if(!dock?.latched||typeof open!=='boolean'||!finite(now)||now<dock.latchedAt||open&&now-dock.latchedAt<PRESSURE_MS)
    throw Error('Port not pressurized');
  return {...dock,hatchOpen:open};
}
export function undockStation(dock,{allCrewAboard,pilotAuthorized}) {
  if(!dock?.latched||dock.hatchOpen||!allCrewAboard||!pilotAuthorized)throw Error('Undocking blocked by crew or hatch');
  return {latched:false,hatchOpen:false,latchedAt:0};
}
// The movement system supplies its own next position, and must sample often enough for .55 m steps.
export function stationWalk(dock,from,to) {
  if(!dock?.latched||!inside(from)||!inside(to)||distance(from,to)>.55||
     !dock.hatchOpen&&((from[0]<-42&&to[0]>=-42)||(from[0]>=-42&&to[0]<-42)))return false;
  for(let i=1;i<=4;i++)if(!inside(from.map((v,k)=>v+(to[k]-v)*i/4)))return false;
  return true;
}
// Atomic, server-owned inventory transfer. local is the current server-verified station walking position.
export function stationTrade(dock,{crewVerified,local,role,save}) {
  if(!dock?.latched||!dock.hatchOpen||!crewVerified||!inside(local)||!Number.isInteger(role)||
     role<0||role>=CREW.length||distance(local,CREW[role].position)>4||
     !save?.inventory?.slots)throw Error('Astronaut not available');
  const inv=structuredClone(save.inventory),slots=inv.slots,{input,cost,output,amount}=CREW[role].offer;
  if(!Array.isArray(slots)||slots.length!==45||slots.some(s=>!s||!Number.isInteger(s.item)||!Number.isInteger(s.count)||s.count<0))
    throw Error('Invalid inventory');
  if(slots.reduce((n,s)=>n+(s.item===input?s.count:0),0)<cost)throw Error('Недостаточно ресурсов');
  let left=cost;
  for(const s of slots)if(s.item===input&&left){const n=Math.min(left,s.count);s.count-=n;left-=n;if(!s.count){s.item=0;s.durability=0;}}
  const max=output===111?32:64;
  const available=slots.reduce((n,s)=>n+(s.item===output?max-s.count:s.count===0?max:0),0);
  if(available<amount)throw Error('Инвентарь заполнен');
  left=amount;
  for(const s of slots)if(s.item===output&&s.count<max&&left){const n=Math.min(left,max-s.count);s.count+=n;left-=n;}
  for(const s of slots)if(!s.count&&left){const n=Math.min(left,max);Object.assign(s,{item:output,count:n,durability:0});left-=n;}
  inv.revision=(inv.revision||0)+1;
  return {...save,inventory:inv};
}
