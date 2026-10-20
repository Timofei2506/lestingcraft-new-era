// A deterministic, terrestrial pad with an approach at a server-validated position.
// Selection alone cannot create a mission or board anybody.
import {height} from './terrain-legacy.js';
export const PAD_X=168,PAD_Z=168;
export const RAMP_START_Z=PAD_Z-110;
export const LIFT_X=PAD_X-20;
const cached=new Map();
export function launchSite(seed,x=PAD_X,z=PAD_Z){
  if(!Number.isInteger(seed)||seed < -2147483648 || seed > 2147483647)throw Error('Invalid site seed');
  // The approach is 110 m long and the 94 x 90 m pad must fit inside
  // the 384 m room. Only the server calculates altitude from this geometry.
  if(!Number.isInteger(x)||!Number.isInteger(z)||x < -320||x > 320||z < -260||z > 320)throw Error('Launch site outside room');
  const key=`${seed}:${x}:${z}`;
  if(cached.has(key))return {...cached.get(key)};
  const liftX=x-20,rampStartZ=z-110;
  let highest=0;
  // The approved pad is 94 x 90 m. Allow eight metres for trees/foliage and
  // lift the entire foundation above terrain; do not modify the authored model.
  for(let px=x-47;px<=x+46;px++)for(let pz=z-45;pz<=z+44;pz++)
    highest=Math.max(highest,height(px,pz,seed));
  // The approach from existing terrain must not lead into a higher hill.
  for(let px=liftX-2;px<=liftX+2;px++)for(let pz=rampStartZ;pz<=z+3;pz++)
    highest=Math.max(highest,height(px,pz,seed));
  const site={active:true,x,y:highest+4,z}; // deck top=site.y+30; lift foot level=site.y+5
  if(cached.size>=32)cached.delete(cached.keys().next().value);
  cached.set(key,site);return {...site};
}
