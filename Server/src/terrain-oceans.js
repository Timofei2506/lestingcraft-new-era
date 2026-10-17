// Content 8 only. Integer basins/coast falloff must match OceanGenerator.cs.
import {hash,height} from './terrain-legacy.js';
import {sampleBiomes,biomeAt,biomeTree} from './terrain-biomes.js';
import {cave} from './terrain-nature.js';
import {floor} from './story-world.js';
const reserved=(x,z)=>x>=-110&&x<=270&&z>=-190&&z<=158;
export function oceanWeight(x,z,seed){if(reserved(x,z))return 0;let cx=Math.floor(x/512),cz=Math.floor(z/512),weight=0;for(let dz=-1;dz<=1;dz++)for(let dx=-1;dx<=1;dx++){let a=cx+dx,b=cz+dz,h=hash(a,0,b,seed^90317),ax=a*512+112+h%288,az=b*512+112+(h>>>9)%288,r=152+(h>>>20)%72,rx=x-ax,rz=z-az,rr=r*r,d=rx*rx+rz*rz;if(d>=rr)continue;let t=Math.trunc((rr-d)*1024/rr),smooth=Math.trunc(Math.trunc(t*t/1024)*(3072-2*t)/1024);weight=Math.max(weight,smooth);}let margin=Math.max(-110-x,x-270,-190-z,z-158);return Math.min(weight,Math.max(0,Math.min(1024,margin*32)));}
export function oceanHeight(x,z,seed){let h=height(x,z,seed),w=oceanWeight(x,z,seed);return Math.trunc((h*(1024-w)+19*w)/1024);}
export function oceanBiome(x,z,seed){let h=oceanHeight(x,z,seed);return h<40?6:h<=43&&oceanWeight(x,z,seed)>0?7:biomeAt(x,z,seed);}
export const isStreetLamp=(x,z)=>x===-39&&z===-24||x===43&&z===-86||x===88&&z===48||x===105&&z===48||x===96&&(z===26||z===72);
export const streetLightBlock=(x,y,z,seed,block)=>{if(!isStreetLamp(x,z))return block;let dy=y-floor(seed);return dy>=1&&dy<=3?4:dy===4?18:dy===5?0:block;};
export function sampleOceans(x,y,z,seed){if(y<0||y>=256)return 0;if(y===0)return 9;let h=oceanHeight(x,z,seed),kind=oceanBiome(x,z,seed);if(kind===6||kind===7){if(y>h)return y<=40?7:biomeTree(x,y,z,seed,h,oceanHeight);if(cave(x,y,z,seed,h))return 0;if(y>=h-3)return y>=h-1?6:43;}return sampleBiomes(x,y,z,seed,oceanHeight);}
