import {test} from 'node:test';import assert from 'node:assert/strict';import {floor,site} from '../src/story-world.js';import {sampleNature} from '../src/terrain-nature.js';
// Voxel clearance / connectivity only. This does NOT simulate CharacterController or native colliders.
test('house, supplies, village, mill, smith and shrine have connected two-block-clear approaches',()=>{
 const seed=2506,H=floor(seed),cache=new Map(),sample=(x,y,z)=>site(x,y,z,seed)??sampleNature(x,y,z,seed),solid=id=>![0,7,8,11].includes(id);
 function feet(x,z){const key=x+','+z;if(cache.has(key))return cache.get(key);let ys=[];for(let y=H+1;y<=H+4;y++)if(solid(sample(x,y-1,z))&&!solid(sample(x,y,z))&&!solid(sample(x,y+1,z)))ys.push(y);cache.set(key,ys);return ys;}
 const queue=[[-32,H+1,-30]],seen=new Set(['-32,'+(H+1)+',-30']);for(let i=0;i<queue.length;i++){const [x,y,z]=queue[i];for(const [dx,dz] of [[1,0],[-1,0],[0,1],[0,-1]]){const X=x+dx,Z=z+dz;if(X< -45||X>196||Z< -131||Z>79)continue;for(const Y of feet(X,Z)){if(Y-y>1||y-Y>2)continue;const key=X+','+Y+','+Z;if(!seen.has(key)){seen.add(key);queue.push([X,Y,Z]);}}}}
 for(const [name,x,z] of [['crate',-35,-29],['satchel',-28,-33],['witnesses',-34,-34],['weights',0,8],['Vera',96,50],['Mira at mill',48,-90],['raven',42,-87],['sun',54,-87],['smith',108,60],['altar',176,-112],['brazier1',168,-112],['brazier2',184,-112],['brazier3',176,-104]])assert.ok(queue.some(([X,Y,Z])=>Math.hypot(X-x,Z-z)<2.5),name+' is unreachable');
});
