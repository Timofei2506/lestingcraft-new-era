import {test} from 'node:test';
import assert from 'node:assert/strict';
import {architectureBlock,architectureSummary} from '../src/architecture-plan.js';
import {applyExpedition,expeditionColumn} from '../src/terrain-expedition.js';
import {plans} from '../src/structure-plan.js';

test('all ten new blueprints have authored entrances, bounded geometry and substantial detail',()=>{
 assert.equal(architectureSummary.length,10);
 for(const [kind,p] of architectureSummary.entries()){
  assert.equal(p.name,plans[kind].name);
  assert.equal(p.radius,plans[kind].radius);
  assert.ok(p.boxes>25,p.name);
  assert.ok(p.top<49);
  for(const [x,z,y,width] of p.entrances){
   assert.ok(width>=2);
   for(let dx=0;dx<width;dx++)for(let dy=0;dy<3;dy++)assert.equal(architectureBlock(kind,x+dx,y+dy,z),0,`${p.name} doorway ${x+dx}/${y+dy}/${z}`);
  }
 }
});
test('new architecture is gated at 11; every <=10 ordered box stays authoritative',()=>{
 for(let kind=0;kind<10;kind++)for(let n=0;n<3000;n++){
  const p=plans[kind],x=(n*7)%(2*p.radius+1)-p.radius,z=(n*13)%(2*p.radius+1)-p.radius,y=n%40-5;
  const c={surface:true,kind,base:60,rx:x,rz:z};
  let expected=y<=0?(y===0?3:1):0;
  for(let i=0;i<p.boxes.length;i+=7){const b=p.boxes;if(x>=b[i]&&x<=b[i+1]&&y>=b[i+2]&&y<=b[i+3]&&z>=b[i+4]&&z<=b[i+5])expected=b[i+6];}
  // Content 7 predates the existing v8 furniture transformations.
  assert.equal(applyExpedition(c,60+y,2506,1,7),expected);
  assert.equal(applyExpedition(c,60+y,2506,1,11),architectureBlock(kind,x,y,z));
 }
});
test('content-11 placement is deterministic and leaves content-9 placement frozen',()=>{
 const kinds=new Set();
 for(let n=0;n<12000;n++){
  const x=n*13%6000-3000,z=n*29%6000-3000;
  const old=expeditionColumn(x,z,2506,9),fresh=expeditionColumn(x,z,2506,11);
  assert.ok(!old.surface||old.kind<5);
  assert.deepEqual(fresh,expeditionColumn(x,z,2506,11));
  if(fresh.surface)kinds.add(fresh.kind);
 }
 assert.equal(kinds.size,10);
});
test('new mines have railway floors, lit work alcoves and retain legacy supports',()=>{
 const c={mine:true,mb:20,mx:6,mz:5};
 assert.equal(applyExpedition(c,21,2506,1,11),16);
 assert.equal(applyExpedition(c,22,2506,1,11),56);
 assert.equal(applyExpedition(c,21,2506,1,10),1,'old solid wall unchanged');
 c.mx=9;c.mz=0;
 assert.equal(applyExpedition(c,21,2506,1,11),85);
 assert.equal(applyExpedition(c,21,2506,1,10),0);
 c.mx=12;c.mz=2;
 assert.equal(applyExpedition(c,24,2506,1,11),4);
});
