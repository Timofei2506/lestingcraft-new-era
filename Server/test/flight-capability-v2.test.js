import test from 'node:test';
import assert from 'node:assert/strict';
import {RoomState,newPeer,RULES_VERSION,createConfig} from '../src/protocol.js';

const NOW=100000;
const PILOT_KEY='a'.repeat(32);
const COPILOT_KEY='b'.repeat(32);
const noop=()=>{};

// These tests isolate capability negotiation and route dispatch. Successful
// dispatch is deliberately intercepted before physical preflight: these are
// not substitutes for shuttle-preflight or launch-site integration tests.
function fixture() {
  const config={
    seed:2506,
    dimension:0,
    version:1,
    rulesVersion:12,
    contentVersion:7,
    mode:2,
    rev:0,
    lobby:false,
    started:true,
    startedAt:NOW,
    runEnded:false,
    story:null,
    launchSite:{active:true,x:168,y:80,z:168},
    launchHatch:false,
    shuttleSeats:[PILOT_KEY,COPILOT_KEY],
    padVisited:[PILOT_KEY,COPILOT_KEY],
    campaign:{chapter:0,unlockedMask:1,milestones:0,runEnded:false}
  };
  const room=new RoomState(config);
  const pilot=newPeer('pilot','Pilot',PILOT_KEY);
  const copilot=newPeer('copilot','Copilot',COPILOT_KEY);
  for(const peer of [pilot,copilot]) {
    peer.positioned=true;
    peer.lastPosition=NOW;
  }
  room.setMembers([pilot,copilot]);

  const calls=[];
  room.igniteVerifiedMoon=(peer,now,persist)=>{
    calls.push({kind:'moon',peer,now,persist});
    return {type:'test_dispatch',to:'moon'};
  };
  room.igniteVerifiedSolar=(peer,now,to,persist)=>{
    calls.push({kind:'solar',peer,now,to,persist});
    return {type:'test_dispatch',to};
  };

  function send(peer,packet,now=NOW,persist=noop) {
    return room.handle(peer,JSON.stringify(packet),now,noop,noop,persist);
  }
  function ready(peer,protocol,now=NOW) {
    return send(peer,{type:'space_flight_ready',protocol},now);
  }
  function ignite(to,now=NOW,persist=noop) {
    const packet={type:'space_ignite'};
    if(to!==undefined)packet.to=to;
    return send(pilot,packet,now,persist);
  }
  return {room,pilot,copilot,calls,send,ready,ignite};
}

test('flight negotiation does not increment rules version',()=>{
  assert.equal(RULES_VERSION,15);
});

test('createConfig cannot enable flight through client configuration',()=>{
  const config=createConfig({
    version:1,rulesVersion:12,seed:2506,dimension:0,mode:2,
    ownerKey:PILOT_KEY,flightEnabled:true
  });
  assert.notEqual(config.flightEnabled,true);
});

test('missing capability acknowledgements retain the legacy release error',()=>{
  const f=fixture();
  assert.throws(()=>f.ignite(),/not released/);
  assert.equal(f.calls.length,0);
});

test('protocol 1 remains gated even when both crew acknowledge',()=>{
  const f=fixture();
  f.ready(f.pilot,1);
  f.ready(f.copilot,1);
  assert.equal(f.pilot.flightProtocol,1);
  assert.equal(f.copilot.flightProtocol,1);
  assert.throws(()=>f.ignite(),/not released/);
  assert.equal(f.calls.length,0);
});

test('protocol 2 records the server timestamp and negotiated version',()=>{
  const f=fixture();
  const packet=f.ready(f.pilot,2);
  assert.equal(packet.type,'space_flight_ready');
  assert.equal(packet.seatIndex,0);
  assert.equal(f.pilot.flightReadyAt,NOW);
  assert.equal(f.pilot.flightProtocol,2);
});

test('both fresh protocol-2 crew release the default-off gate',()=>{
  const f=fixture();
  f.ready(f.pilot,2);
  f.ready(f.copilot,2);
  const persist=()=>{};
  const result=f.ignite(undefined,NOW,persist);
  assert.equal(result.to,'moon');
  assert.equal(f.calls.length,1);
  assert.equal(f.calls[0].kind,'moon');
  assert.equal(f.calls[0].peer,f.pilot);
  assert.equal(f.calls[0].now,NOW);
  assert.equal(f.calls[0].persist,persist);
  assert.notEqual(f.room.config.flightEnabled,true);
});

test('mixed capabilities cannot release a default-off room',()=>{
  const f=fixture();
  f.ready(f.pilot,2);
  f.ready(f.copilot,1);
  assert.throws(()=>f.ignite(),/not released/);
  assert.equal(f.calls.length,0);
});

test('a single acknowledgement cannot release a default-off room',()=>{
  const f=fixture();
  f.ready(f.pilot,2);
  assert.throws(()=>f.ignite(),/not released/);
  assert.equal(f.calls.length,0);
});

test('capability freshness includes the 1500 ms boundary',()=>{
  const f=fixture();
  f.ready(f.pilot,2);
  f.ready(f.copilot,2);
  assert.equal(f.ignite(undefined,NOW+1500).to,'moon');
});

test('capability acknowledgement expires after 1500 ms',()=>{
  const f=fixture();
  f.ready(f.pilot,2);
  f.ready(f.copilot,2);
  assert.throws(()=>f.ignite(undefined,NOW+1501),/not released/);
  assert.equal(f.calls.length,0);
});

test('future capability timestamps cannot release a default-off room',()=>{
  const f=fixture();
  f.ready(f.pilot,2);
  f.ready(f.copilot,2);
  f.copilot.flightReadyAt=NOW+1;
  assert.throws(()=>f.ignite(),/not released/);
  assert.equal(f.calls.length,0);
});

test('missing or nonfinite capability timestamps cannot release a room',()=>{
  for(const timestamp of [undefined,NaN,Infinity,-Infinity]) {
    const f=fixture();
    f.ready(f.pilot,2);
    f.ready(f.copilot,2);
    f.copilot.flightReadyAt=timestamp;
    assert.throws(()=>f.ignite(),/not released/);
    assert.equal(f.calls.length,0);
  }
});

test('disconnected seated crew cannot contribute an acknowledgement',()=>{
  const f=fixture();
  f.ready(f.pilot,2);
  f.ready(f.copilot,2);
  f.room.setMembers([f.pilot]);
  assert.throws(()=>f.ignite(),/not released/);
  assert.equal(f.calls.length,0);
});

test('an empty second seat cannot release a default-off room',()=>{
  const f=fixture();
  f.ready(f.pilot,2);
  f.ready(f.copilot,2);
  f.room.config.shuttleSeats=[PILOT_KEY,null];
  assert.throws(()=>f.ignite(),/not released/);
  assert.equal(f.calls.length,0);
});

test('explicitly enabled rooms retain protocol-1 dispatch',()=>{
  const f=fixture();
  f.room.config.flightEnabled=true;
  f.ready(f.pilot,1);
  f.ready(f.copilot,1);
  assert.equal(f.ignite().to,'moon');
  assert.equal(f.calls[0].kind,'moon');
});

test('explicitly enabled rooms still require fresh acknowledgements',()=>{
  const f=fixture();
  f.room.config.flightEnabled=true;
  f.ready(f.pilot,1);
  f.ready(f.copilot,1);
  assert.throws(
    ()=>f.ignite(undefined,NOW+1501),
    /Both crew clients must support and acknowledge shared flight/
  );
  assert.equal(f.calls.length,0);
});

test('unsupported negotiation versions do not overwrite an accepted version',()=>{
  for(const protocol of [0,3,-1,1.5,'2',null]) {
    const f=fixture();
    f.ready(f.pilot,2);
    assert.throws(
      ()=>f.ready(f.pilot,protocol),
      /Flight client must be seated with a recent position/
    );
    assert.equal(f.pilot.flightProtocol,2);
    assert.equal(f.pilot.flightReadyAt,NOW);
  }
});

test('negotiation cannot use a client-supplied timestamp',()=>{
  const f=fixture();
  f.send(f.pilot,{
    type:'space_flight_ready',
    protocol:2,
    flightReadyAt:NOW+1000000,
    now:NOW+1000000
  });
  assert.equal(f.pilot.flightReadyAt,NOW);
});

test('unseated peers cannot negotiate flight readiness',()=>{
  const f=fixture();
  f.room.config.shuttleSeats=[null,COPILOT_KEY];
  assert.throws(
    ()=>f.ready(f.pilot,2),
    /Flight client must be seated with a recent position/
  );
  assert.equal(f.pilot.flightProtocol,undefined);
});

test('stale position prevents negotiation',()=>{
  const f=fixture();
  f.pilot.lastPosition=NOW-1501;
  assert.throws(
    ()=>f.ready(f.pilot,2),
    /Flight client must be seated with a recent position/
  );
  assert.equal(f.pilot.flightProtocol,undefined);
});

test('a running mission prevents another readiness negotiation',()=>{
  const f=fixture();
  f.room.config.spaceMission={};
  assert.throws(
    ()=>f.ready(f.pilot,2),
    /Flight client must be seated with a recent position/
  );
});

test('explicit Moon retains the established lunar dispatch',()=>{
  const f=fixture();
  f.ready(f.pilot,2);
  f.ready(f.copilot,2);
  assert.equal(f.ignite('moon').to,'moon');
  assert.equal(f.calls[0].kind,'moon');
});

test('planet targets dispatch to the verified solar transition',()=>{
  for(const to of ['mars','venus','mercury']) {
    const f=fixture();
    f.ready(f.pilot,2);
    f.ready(f.copilot,2);
    const persist=()=>{};
    assert.equal(f.ignite(to,NOW,persist).to,to);
    assert.equal(f.calls.length,1);
    assert.equal(f.calls[0].kind,'solar');
    assert.equal(f.calls[0].to,to);
    assert.equal(f.calls[0].persist,persist);
  }
});

test('Earth is a solar target and the route transition validates its origin',()=>{
  const f=fixture();
  f.room.config.dimension=1;
  f.ready(f.pilot,2);
  f.ready(f.copilot,2);
  assert.equal(f.ignite('earth').to,'earth');
  assert.equal(f.calls[0].kind,'solar');
  assert.equal(f.calls[0].to,'earth');
});

test('a lunar target from another body uses the solar transition',()=>{
  const f=fixture();
  f.room.config.dimension=2;
  f.ready(f.pilot,2);
  f.ready(f.copilot,2);
  assert.equal(f.ignite('moon').to,'moon');
  assert.equal(f.calls[0].kind,'solar');
});

test('unsupported targets are rejected before route dispatch',()=>{
  for(const to of ['station','jupiter','Moon','',null,2,{},[]]) {
    const f=fixture();
    f.ready(f.pilot,2);
    f.ready(f.copilot,2);
    assert.throws(()=>f.ignite(to),/Invalid solar route/);
    assert.equal(f.calls.length,0);
  }
});

test('invalid targets do not change legacy release-error ordering',()=>{
  const f=fixture();
  assert.throws(()=>f.ignite('invalid'),/not released/);
});

test('solar transition rejects an unauthorized pilot before preflight',()=>{
  const f=fixture();
  assert.throws(
    ()=>RoomState.prototype.igniteVerifiedSolar.call(
      f.room,f.copilot,NOW,'mars',noop
    ),
    /Only the connected seated pilot may ignite/
  );
  assert.equal(f.room.config.spaceMission,undefined);
});

test('solar transition rejects a destination equal to the server-owned origin',()=>{
  const f=fixture();
  assert.throws(
    ()=>RoomState.prototype.igniteVerifiedSolar.call(
      f.room,f.pilot,NOW,'earth',noop
    ),
    /Invalid solar route/
  );
  assert.equal(f.room.config.spaceMission,undefined);
});

test('solar transition rejects invalid server dimension state',()=>{
  for(const dimension of [-1,5,1.5,'1',undefined]) {
    const f=fixture();
    f.room.config.dimension=dimension;
    assert.throws(
      ()=>RoomState.prototype.igniteVerifiedSolar.call(
        f.room,f.pilot,NOW,'mars',noop
      ),
      /Invalid solar route/
    );
    assert.equal(f.room.config.spaceMission,undefined);
  }
});
