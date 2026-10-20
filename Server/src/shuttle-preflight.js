// Safe, read-only server gate. It verifies two physically present people in the
// approved cockpit, but intentionally DOES NOT ignite or install a mission until
// a shared in-flight client can preserve their local positions and restore them.
import {liftAt} from './shuttle-lift.js';
export function preflight(config,peers,now){
  const issues=[],site=config.launchSite,seats=config.shuttleSeats||[];
  if(!site?.active)issues.push('нет космодрома');
  if(config.spaceMission||config.spaceVisit||config.runEnded)issues.push('комната уже в полёте или завершена');
  if(config.launchHatch)issues.push('закройте люк');
  const lift=config.launchLift&&liftAt(config.launchLift,now);
  if(!lift||lift.moving||lift.riderKey)issues.push('освободите и остановите лифт');
  if(seats.length!==2||!seats[0]||!seats[1]||seats[0]===seats[1])issues.push('два разных игрока должны занять места');
  for(let i=0;i<2;i++){
    const key=seats[i];if(!key)continue;
    const peer=peers.find(p=>p.playerKey===key);
    if(!config.padVisited?.includes(key)||!peer?.positioned||now-peer.lastPosition>1500){issues.push('член экипажа не находится рядом');continue;}
    if(!site?.active)continue;
    const x=site.x+(i===0?-.4:1.4),y=site.y+44+1.58,z=site.z+5.35;
    if(Math.abs(peer.x-x)>1.1||Math.abs(peer.y-y)>1.4||Math.abs(peer.z-z)>1.1)
      issues.push('член экипажа покинул кресло');
  }
  return {ready:issues.length===0,issues,crewCount:seats.filter(Boolean).length};
}
