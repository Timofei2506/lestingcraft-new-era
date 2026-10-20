// Server-owned cooperative journey core. Not connected to a room yet: no client command
// may create a mission until boarding, cabin physics, persistence and landing are wired.
// Distances are double-precision metres. The 30-minute trip uses fictional propulsion.
export const MOON_SECONDS = 1800;
export const ASCENT_SECONDS = 630;
export const APPROACH_SECONDS = 180;
export const COUNTDOWN_MS = 10000;
export const MOON_DISTANCE = 384400000;
export const EARTH_X = 149597870700;

const finite = n => typeof n === 'number' && Number.isFinite(n);
const validVec = p => Array.isArray(p) && p.length === 3 && p.every(finite);
const sub = (a,b) => a.map((v,i) => v-b[i]);
const add = (a,b) => a.map((v,i) => v+b[i]);
const scale = (a,s) => a.map(v => v*s);
const norm = v => {const m = Math.hypot(...v); return m > 0 ? scale(v,1/m) : [0,0,0];};
const distance = (a,b) => Math.hypot(...sub(a,b));
const phase = (m,t) => t<m.ignitionAt ? 'countdown' : t<m.ignitionAt+ASCENT_SECONDS*1000 ? 'ascent' :
    t<m.ignitionAt+(m.seconds-APPROACH_SECONDS)*1000 ? 'cruise' :
    t<m.ignitionAt+m.seconds*1000 ? 'approach' : 'landing-ready';

export function beginMoonMission({crew,boarded,pilot,now,origin=[EARTH_X,0,0]}) {
    if(!finite(now) || now<0 || !validVec(origin) || !Array.isArray(crew) || crew.length<2 || crew.length>8 ||
        new Set(crew).size!==crew.length || !crew.every(id=>typeof id==='string' && id.length>0) ||
        !Array.isArray(boarded) || !crew.every(id=>boarded.includes(id)) || !crew.includes(pilot))
        throw Error('Launch requires two to eight distinct, physically boarded crew members and a pilot');
    const target=[EARTH_X+MOON_DISTANCE,0,0];
    if(distance(origin,target)<1000)throw Error('Launch site cannot be destination');
    return {crew:[...crew],pilot,origin:[...origin],target,position:[...origin],heading:norm(sub(target,origin)),
        ignitionAt:now+COUNTDOWN_MS,lastAt:now,seconds:MOON_SECONDS,autopilot:false,landingBurn:0,landed:false};
}

// Only the seated commander can engage/disengage guidance during cruise.
export function setCruiseAutopilot(m,player,on,now) {
    if(!m.crew.includes(player) || player!==m.pilot || typeof on!=='boolean' || phase(m,now)!=='cruise' || m.landed)
        throw Error('Pilot can change cruise autopilot only in cruise');
    const next=advanceMission(m,now);
    return {...next,autopilot:on};
}

// Advance by server time, not time or destination supplied by a client. A single call is
// segmented at ignition/ascent/approach boundaries so 1800 seconds in one tick cannot
// accidentally credit a 180-second manual landing burn.
export function advanceMission(m,now,{player,heading,landingThrottle=0}={}) {
    if(!finite(now) || now<m.lastAt || !validVec(m.position) || !validVec(m.target) || !finite(landingThrottle) ||
        landingThrottle<0 || landingThrottle>1 || (heading!==undefined && !validVec(heading)))throw Error('Invalid flight update');
    if((heading!==undefined || landingThrottle!==0) && (player!==m.pilot || !m.crew.includes(player)))
        throw Error('Only the pilot can steer or fire landing engines');
    const state={...m,position:[...m.position],heading:[...m.heading]};
    if(state.landed)return state;
    // Controls expire after a short outage; a late packet cannot retroactively steer or
    // claim minutes of braking during a sleeping/restarting server.
    const liveInput=now-m.lastAt<=1500;
    const end=Math.min(now,m.ignitionAt+m.seconds*1000);
    while(state.lastAt<end) {
        const stage=phase(state,state.lastAt);
        const boundary=stage==='countdown'?state.ignitionAt:stage==='ascent'?state.ignitionAt+ASCENT_SECONDS*1000:
            stage==='cruise'?state.ignitionAt+(state.seconds-APPROACH_SECONDS)*1000:state.ignitionAt+state.seconds*1000;
        const next=Math.min(boundary,end);
        if(stage!=='countdown') {
            if(stage==='ascent' || state.autopilot)state.heading=norm(sub(state.target,state.position));
            else if(liveInput && heading!==undefined && distance(heading,[0,0,0])>.01)state.heading=norm(heading);
            const remaining=(state.ignitionAt+state.seconds*1000-state.lastAt)/1000;
            const dt=(next-state.lastAt)/1000;
            // The position is integrated in one coordinate frame; never replaced with target.
            state.position=add(state.position,scale(state.heading,distance(state.position,state.target)*dt/remaining));
            if(stage==='approach' && liveInput)state.landingBurn+=dt*landingThrottle;
        }
        state.lastAt=next;
    }
    // Late join/restart cannot reverse the journey. Preserve server clock even after arrival.
    state.lastAt=now;
    return state;
}

export function landMission(m,player,now) {
    if(!m.crew.includes(player) || player!==m.pilot || m.landed || phase(m,now)!=='landing-ready' ||
        m.landingBurn<20 || distance(m.position,m.target)>1000000)throw Error('Manual landing checks not satisfied');
    return {...m,landed:true,lastAt:now};
}

export function missionView(m,now) {
    if(!finite(now))throw Error('Invalid server clock');
    return {crew:[...m.crew],pilot:m.pilot,phase:m.landed?'landed':phase(m,now),
        elapsed:Math.max(0,Math.min(m.seconds,(now-m.ignitionAt)/1000)),
        position:[...m.position],autopilot:m.autopilot,landingBurn:m.landingBurn};
}
