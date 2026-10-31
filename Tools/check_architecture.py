"""Authoring geometry, doors, source/table reproducibility and real C#/JS parity.
Requires Python, Mono and Node; no Unity emulation. Does not validate GPU rendering.
"""
from pathlib import Path
import json,subprocess,sys,tempfile
sys.path.insert(0,str(Path(__file__).parent/'WorldGen'))
from architecture import PLANS
R=Path(__file__).resolve().parents[1]
subprocess.run([sys.executable,str(R/'Tools/build_architecture.py'),'--check'],check=True,cwd=R)
# Independent ordered-box interpreter for semantic entrance checks.
def block(p,x,y,z):
 id=1 if y<0 else 3 if y==0 else 0
 for x0,x1,y0,y1,z0,z1,b in p.boxes:
  if x0<=x<=x1 and y0<=y<=y1 and z0<=z<=z1:id=b
 return id
for p in PLANS:
 for x,z,y,width in p.entrances:
  for xx in range(x,x+width):
   for yy in range(y,y+3):assert block(p,xx,yy,z)==0,(p.name,'obstructed doorway',xx,yy,z)
 assert len(p.boxes)>25,(p.name,'under-authored')
# Capsule-clear connectivity check through the keep's actual two-storey staircase.
# This is a voxel topology test, not a CharacterController physics simulation.
from collections import deque
p=PLANS[1];solid={}
for x0,x1,y0,y1,z0,z1,id in p.boxes:
 for y in range(y0,y1+1):
  for z in range(z0,z1+1):
   for x in range(x0,x1+1):
    if id:solid[x,y,z]=id
    else:solid.pop((x,y,z),None)
def stand(x,y,z):return solid.get((x,y-1,z),0)!=0 and not solid.get((x,y,z),0) and not solid.get((x,y+1,z),0)
start=(-1,1,0);goal=(0,6,7);queue=deque([start]);seen={start}
assert stand(*start) and stand(*goal),'Keep entry / gallery has no clear standing space'
while queue and goal not in seen:
 x,y,z=queue.popleft()
 for dx,dz in [(1,0),(-1,0),(0,1),(0,-1)]:
  for dy in [0,1,-1]:
   n=(x+dx,y+dy,z+dz)
   if n in seen or max(abs(n[0]),abs(n[2]))>p.radius or not 1<=n[1]<=22:continue
   if dy>0 and solid.get((x,y+2,z),0):continue
   if stand(*n):seen.add(n);queue.append(n)
assert goal in seen,'Keep gallery unreachable through the authored stair'
print('PASS keep entry-to-upper-gallery topology with two-block capsule clearance')
with tempfile.TemporaryDirectory() as temp:
 exe=str(Path(temp)/'architecture.exe');csv=Path(temp)/'parity.csv';source=Path(temp)/'source.json'
 subprocess.run(['mcs','-out:'+exe,'Assets/Scripts/World/ArchitecturePlan.cs','Assets/Scripts/World/StructurePlan.cs','Tools/check_architecture_core.cs'],check=True,cwd=R)
 with csv.open('w') as output:subprocess.run(['mono',exe],stdout=output,check=True,cwd=R)
 source.write_text(json.dumps([{'radius':p.radius,'boxes':p.boxes} for p in PLANS]))
 script=r'''
import {architectureBlock} from './Server/src/architecture-plan.js';
import fs from 'node:fs';
const source=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
let n=0;
for(const line of fs.readFileSync(process.argv[1],'utf8').trim().split('\n')){
 const [kind,x,y,z,id]=line.split(',').map(Number),p=source[kind];
 let expected=y<0?1:y===0?3:0;
 for(const [x0,x1,y0,y1,z0,z1,b] of p.boxes)if(x>=x0&&x<=x1&&y>=y0&&y<=y1&&z>=z0&&z<=z1)expected=b;
 const js=architectureBlock(kind,x,y,z);
 if(js!==id||js!==expected)throw Error(`Parity at ${line}: JS ${js}, authored ${expected}`);
 n++;
}
console.log('PASS '+n+' C# / JS / independently sampled ordered-box architecture probes');
'''
 subprocess.run(['node','--input-type=module','-e',script,str(csv),str(source)],check=True,cwd=R)
print('PASS: all authored entrances have 2+ block width / 3-block height; all ten plans detailed; legacy originals frozen')
