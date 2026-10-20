import {height} from './terrain-legacy.js';import {boxes} from './story-plan.js';
export const floor=seed=>Math.max(44,height(0,0,seed));
const region=(x,z)=>Math.min(Math.max(Math.abs(x),Math.abs(z-8))-9,Math.max(Math.abs(x+32),Math.abs(z+32))-12,Math.max(Math.abs(x-96),Math.abs(z-48))-24,Math.max(Math.abs(x-48),Math.abs(z+96))-13,Math.max(Math.abs(x-176),Math.abs(z+112))-17);
function segment(x,z,ax,az,bx,bz,r){let dx=bx-ax,dz=bz-az,px=x-ax,pz=z-az,len=dx*dx+dz*dz,t=px*dx+pz*dz;if(t<0)return px*px+pz*pz<=r*r;if(t>len){px=x-bx;pz=z-bz;return px*px+pz*pz<=r*r;}let cross=px*dz-pz*dx;return cross*cross<=r*r*len;}
const road=(x,z,r)=>segment(x,z,-32,-32,0,8,r)||segment(x,z,0,8,96,48,r)||segment(x,z,96,48,48,-96,r)||segment(x,z,48,-96,176,-112,r)||segment(x,z,176,-112,96,48,r);
export const protectedSite=(x,z)=>region(x,z)<=0;
const streetLampsV8=[[88,48,56],[105,48,56],[96,26,56],[96,73,56],[-39,-24,54],[43,-86,54],[80,48,56],[112,48,56],[96,38,56],[96,58,56]];
const villageHousesV8=[[82,36],[109,35],[82,61],[110,62],[96,67],[48,-96]];
function applyV8StoryUpgrades(x,ry,z,id){
  for(const [lx,lz,light] of streetLampsV8){
    if(x===lx&&z===lz){if(ry>=1&&ry<=3)return 4;if(ry===4)return 58;if(ry===5)return light;}
    if(Math.abs(x-lx)===1&&z===lz&&ry===4)return 58;
    if(Math.abs(x-lx)===1&&z===lz&&ry===3&&light===56)return 56;
  }
  for(const [hx,hz] of villageHousesV8){
    let dx=x-hx,dz=z-hz;if(Math.abs(dx)>4||Math.abs(dz)>4)continue;
    if(id===46&&ry>=4)id=59;
    if(ry===1&&dx===-2&&dz===2)return 55;
    if(ry>=1&&ry<=2&&dx===2&&dz===-2)return 57;
    if(ry===2&&dx===0&&dz===-4)return 54;
  }
  return id;
}
export function site(x,y,z,seed,contentVersion=7){if(y<=0||y>=256)return null;let d=region(x,z),r=road(x,z,2);if(d>6&&!road(x,z,5))return null;let base=floor(seed),natural=height(x,z,seed),top=base;if(d>0&&!r){let blend=Math.min(6,d);top=Math.trunc((base*(6-blend)+natural*blend)/6);}if(y<top-3&&y<=natural)return null;if(y<=top){let id=y===top?(r?32:d<=0?3:2):y<top-2?1:2;if(y===base&&d<=0)for(let i=0;i<boxes.length;i+=7)if(x>=boxes[i]&&x<=boxes[i+1]&&boxes[i+2]<=0&&boxes[i+3]>=0&&z>=boxes[i+4]&&z<=boxes[i+5])id=boxes[i+6];return id;}if(y>base+18)return 0;if(d>0)return contentVersion>=8&&d<=2?applyV8StoryUpgrades(x,y-base,z,0):0;let ry=y-base,id=0;for(let i=0;i<boxes.length;i+=7)if(x>=boxes[i]&&x<=boxes[i+1]&&ry>=boxes[i+2]&&ry<=boxes[i+3]&&z>=boxes[i+4]&&z<=boxes[i+5])id=boxes[i+6];return contentVersion>=8?applyV8StoryUpgrades(x,ry,z,id):id;}
