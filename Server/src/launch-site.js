// A single deterministic, terrestrial pad in the existing shared room. This only
// selects a common physical location; it cannot create a mission or board anybody.
import {height} from './terrain-legacy.js';
export const PAD_X=168,PAD_Z=168;
export const RAMP_START_Z=PAD_Z-110;
export const LIFT_X=PAD_X-20;
const cached=new Map();
export function launchSite(seed){
  if(!Number.isInteger(seed)||seed < -2147483648 || seed > 2147483647)throw Error('Invalid site seed');
  if(cached.has(seed))return {...cached.get(seed)};
  let highest=0;
  // The approved pad is 94 x 90 m. Allow eight metres for trees/foliage and
  // lift the entire foundation above terrain; do not modify the authored model.
  for(let x=PAD_X-47;x<=PAD_X+46;x++)for(let z=PAD_Z-45;z<=PAD_Z+44;z++)
    highest=Math.max(highest,height(x,z,seed));
  // The approach from existing terrain must not lead into a higher hill.
  for(let x=LIFT_X-2;x<=LIFT_X+2;x++)for(let z=RAMP_START_Z;z<=PAD_Z+3;z++)
    highest=Math.max(highest,height(x,z,seed));
  const site={active:true,x:PAD_X,y:highest+4,z:PAD_Z}; // deck top=site.y+30; lift foot level=site.y+5
  if(cached.size>=32)cached.delete(cached.keys().next().value);
  cached.set(seed,site);return {...site};
}
