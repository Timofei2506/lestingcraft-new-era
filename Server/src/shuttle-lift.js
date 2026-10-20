// One room-owned crew platform, shared by both clients. Physical motion is continuous;
// the approved pad top is 25 m above its ground walkway. Stable player keys stay private.
export const LIFT_TRAVEL=25,LIFT_MS=6000,EYE_HEIGHT=1.58;
const finite=n=>typeof n==='number'&&Number.isFinite(n);
export const emptyLift=()=>({from:0,to:0,startedAt:0,riderKey:null});
export function liftAt(state,now){
  if(!state||![0,1].includes(state.from)||![0,1].includes(state.to)||!finite(state.startedAt)||!finite(now))throw Error('Invalid lift state');
  if(state.from===state.to)return {...state,progress:state.to,moving:false};
  const fraction=Math.max(0,Math.min(1,(now-state.startedAt)/LIFT_MS));
  if(fraction>=1)return {...state,from:state.to,progress:state.to,moving:false};
  return {...state,progress:state.from+(state.to-state.from)*fraction,moving:true};
}
export function liftSnapshot(state,now,peers=[]){
  const v=liftAt(state,now),rider=peers.find(p=>p.playerKey===v.riderKey);
  return {active:true,progress:v.progress,height:v.progress*LIFT_TRAVEL,moving:v.moving,
    direction:v.moving?(v.to>v.from?'up':'down'):'',
    occupied:!!v.riderKey,riderId:rider?.id||'',serverNow:now};
}
function near(peer,site,progress,wide=false){
  if(!peer?.positioned||!site?.active)return false;
  const span=wide?2:1;
  return Math.abs(peer.x-(site.x-19.8))<=span && Math.abs(peer.z-(site.z+3))<=span &&
    Math.abs(peer.y-(site.y+5+progress*LIFT_TRAVEL+EYE_HEIGHT))<=1.15;
}
export function operateLift(state,site,peer,action,now){
  if(!finite(now)||!['up','down','exit','recall'].includes(action)||!peer?.playerKey||
     now-(peer.lastPosition||0)>1500)throw Error('Lift needs recent physical position');
  const v=liftAt(state,now);
  if(v.moving)throw Error('Lift is in motion');
  if(action==='up'||action==='down'){
    if(v.riderKey&&v.riderKey!==peer.playerKey)throw Error('Lift belongs to the other rider');
    if(v.progress!==(action==='up'?0:1)||!near(peer,site,v.progress))throw Error('Board the stationary lift first');
    return {from:v.progress,to:action==='up'?1:0,startedAt:now,riderKey:peer.playerKey};
  }
  if(action==='exit'){
    if(v.riderKey!==peer.playerKey||!near(peer,site,v.progress,true))throw Error('Only the stopped rider can step off');
    return {from:v.progress,to:v.progress,startedAt:now,riderKey:null};
  }
  if(v.progress!==1||v.riderKey||!near(peer,site,0,true))throw Error('Recall needs an empty platform and player at ground');
  return {from:1,to:0,startedAt:now,riderKey:null};
}
