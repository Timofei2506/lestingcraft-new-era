"""Author three articulated planetary boss VOXEL assets for Unity (not AI concept art).
The exact JSON cells are loaded by PlanetBossVisual, and previewed by preview_planet_boss_models.py.
Assets are models only: no bosses spawn until their planets, combat and animations are implemented.
"""
import json,math
from pathlib import Path
R=Path(__file__).resolve().parents[1]
OUT=R/'Assets/Resources/Models'

class Sculpture:
 def __init__(self,slug,name,planet,unit,palette):
  self.slug,self.name,self.planet,self.unit,self.palette=slug,name,planet,unit,palette
  self.parts={};self.pivots={};self.moves={}
 def part(self,name,pivot,move='fixed'):
  self.parts[name]={};self.pivots[name]=tuple(pivot);self.moves[name]=move;return self
 def put(self,x,y,z,m):
  if y<0:return
  self.parts[next(reversed(self.parts))][(int(x),int(y),int(z))]=m
 def box(self,x0,x1,y0,y1,z0,z1,m):
  for x in range(x0,x1):
   for y in range(y0,y1):
    for z in range(z0,z1):self.put(x,y,z,m)
 def ball(self,x,y,z,rx,ry,rz,m,pattern=None):
  for a in range(math.floor(x-rx),math.ceil(x+rx)+1):
   for b in range(max(0,math.floor(y-ry)),math.ceil(y+ry)+1):
    for c in range(math.floor(z-rz),math.ceil(z+rz)+1):
     d=((a-x)/rx)**2+((b-y)/ry)**2+((c-z)/rz)**2
     if d<=1.05:
      chosen=m
      if pattern and .73<d<1.06:chosen=pattern(a,b,c,m)
      self.put(a,b,c,chosen)
 def stroke(self,points,radii,m):
  for p,q,r0,r1 in zip(points,points[1:],radii,radii[1:]):
   n=max(2,math.ceil(math.dist(p,q)*2.2))
   for k in range(n+1):
    t=k/n; pos=[p[j]*(1-t)+q[j]*t for j in range(3)];r=r0*(1-t)+r1*t
    self.ball(*pos,r,r,r,m)
 def export(self):
  parts=[]
  for name,cells in self.parts.items():
   # Sub-voxel brush tips occasionally make isolated single cubes. They are
   # never part of a physical rig, so keep the largest 6-connected solid.
   pending=set(cells);regions=[]
   while pending:
    region=set();queue=[pending.pop()]
    while queue:
     p=queue.pop();region.add(p)
     x,y,z=p
     for q in ((x+1,y,z),(x-1,y,z),(x,y+1,z),(x,y-1,z),(x,y,z+1),(x,y,z-1)):
      if q in pending:pending.remove(q);queue.append(q)
    regions.append(region)
   main=max(regions,key=len)
   cells={q:cells[q] for q in main}
   pivot=self.pivots[name];flat=[]
   for (x,y,z),m in sorted(cells.items()):flat.extend((x-pivot[0],y-pivot[1],z-pivot[2],m))
   assert flat and len(flat)//4<=15000
   parts.append(dict(name=name,pivot=list(pivot),motion=self.moves[name],cells=flat))
  out=dict(version=1,slug=self.slug,name=self.name,planet=self.planet,unit=self.unit,
           palette=self.palette,parts=parts)
  path=OUT/('PlanetBoss'+self.slug+'.json')
  path.write_text(json.dumps(out,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
  print(self.slug,len(parts),'parts',sum(len(p['cells'])//4 for p in parts),'voxels',path.stat().st_size,'bytes')
  return out

def pal(name,layer,tint,emission=0):return dict(name=name,layer=layer,tint=tint,emission=emission)

def mars():
 p=[pal('charred basalt armor',11,[.46,.35,.32]),pal('obsidian edge',35,[.31,.27,.32]),
    pal('rust strata',31,[.69,.32,.21]),pal('molten heart',9,[1,.39,.05],2.8),
    pal('lava cracks',9,[1,.28,.03],1.8),pal('ember eye',9,[1,.62,.08],3),
    pal('stone joint',11,[.30,.27,.30]),pal('furnace core',9,[1,.75,.16],3.4)]
 s=Sculpture('Mars','Кузнец разлома','Mars',.26,p)
 def rock(a,b,c,m):return 2 if (a*3+b*5+c*7)%19<3 else 1 if (a-b+c)%11==0 else m
 s.part('torso',(0,25,0),'breathe')
 s.ball(0,25,0,7.3,10,5.5,0,rock);s.ball(0,16,0,6,4.2,4.5,6,rock)
 for side in (-1,1):
  s.stroke([(side*3,18,4),(side*5,28,5),(side*5,34,2)],[1.7,2.0,1.5],1)
  for i in range(3):s.stroke([(side*3,26-i*4,4),(side*5,27-i*4,5)],[1.1,.5],2)
 # glowing heart OPEN to front with branching lava traces
 s.ball(0,26,5.0,3,4.2,1.8,3);s.ball(0,26,6.8,1.25,1.5,.75,7)
 for side in (-1,1):
  s.stroke([(side*2,25,6),(side*4,22,5.5),(side*5,17,4.2)],[.55,.4,.18],4)
  s.stroke([(side*1.5,28,6),(side*4,31,5)],[.32,.13],4)
 s.part('head',(0,35,0),'look');s.ball(0,38,0,4.4,4.5,3.8,0,rock)
 for side in (-1,1):
  s.box(side*2 if side>0 else -4,side*4 if side>0 else -2,36,38,3,5,1)
  s.ball(side*1.9,39,3.7,.8,.45,.7,5)
  for spike in range(3):
   x=side*(1+spike*1.2);s.stroke([(x,41,0),(x+side*.5,45+spike%2*2,-.4)],[1.15,.12],1)
 s.stroke([(0,35,3),(0,33,4.3)],[1.5,.6],2)
 for side in (-1,1):
  s.part(('left' if side<0 else 'right')+'-leg',(side*5,16,0),'stride')
  s.stroke([(side*5,17,0),(side*6,10,0),(side*5.3,4,2),(side*5.8,1.5,4)],[3.0,3.4,2.6,2.1],0)
  for t in (-1,0,1):s.stroke([(side*(5.8+t*.8),2,4),(side*(6.2+t),.8,7.5)],[1.15,.7],1)
  for y in (6,11):s.stroke([(side*7,y,2),(side*6,y-2,3)],[.5,.2],4)
  for extra in (0,1):
   shoulder=(10 if extra==0 else 7);high=(32 if extra==0 else 22)
   s.part(('left' if side<0 else 'right')+'-arm-'+str(extra),(side*shoulder,high,0),'arm')
   s.stroke([(side*(6.5 if extra==0 else 5),high,0),(side*(shoulder+1),high-3,0),(side*(shoulder+2),high-10,2)],[3 if extra==0 else 2.2,3.1 if extra==0 else 2.1,2.8 if extra==0 else 2],0)
   s.ball(side*(shoulder+2),high-11,2,3.6 if extra==0 else 2.5,3.1,3.4,1,rock)
   for finger in (-1,0,1):
    s.stroke([(side*(shoulder+2+finger*.85),high-13,4),(side*(shoulder+2.5+finger),high-15.5,5.5)],[.85,.22],1)
   s.stroke([(side*(shoulder+1),high-6,3),(side*(shoulder+2.5),high-10,5)],[.46,.17],4)
 return s.export()

def venus():
 p=[pal('sulfur ceramic',30,[.86,.77,.49]),pal('dark chitin',35,[.30,.34,.34]),
    pal('acid reservoir',12,[.12,1,.46],1.35),pal('chitin edge',51,[.59,.48,.32]),
    pal('acid heart',12,[.22,1,.58],2.6),pal('leg claw',35,[.14,.18,.21]),
    pal('ochre armor',7,[.77,.64,.37])]
 s=Sculpture('Venus','Кислотная владычица','Venus',.26,p)
 s.part('thorax',(0,24,0),'breathe');s.ball(0,24,0,6,8,6,0)
 s.ball(0,23,5,3.2,4,2,2);s.ball(0,23,7,1.7,2,1.2,4)
 for side in (-1,1):
  for y in (18,23,28):
   s.stroke([(side*1.5,y,5.5),(side*4,y+2,4.4),(side*6,y+1,1)],[.8,1,.5],3)
  s.stroke([(side*3,30,3),(side*6,34,-1),(side*4,40,-2)],[1.7,1.2,.1],6)
 s.part('abdomen',(0,18,-9),'breathe');s.ball(0,19,-8,7.5,7.2,10,1)
 for z in (-15,-11,-7,-3):
  for side in (-1,1):s.stroke([(side*3,24,z),(side*5,22,z+1)],[1.2,.25],6)
 s.ball(0,18,-16,2.5,2.3,1.8,2)
 s.part('crown',(0,33,4),'look');s.stroke([(0,32,3),(0,39,4),(0,46,4)],[2.4,2.3,1.6],1)
 s.ball(0,47,5,2.5,3,2.8,0)
 s.ball(0,46,7,1.3,2.2,1.0,4)
 for side in (-1,1):
  s.stroke([(side*1.8,47,4),(side*3.8,51,4),(side*4.4,54,4)],[1.1,.7,.05],3)
  s.ball(side*1.9,46,7.1,.55,.6,.55,2)
 for side in (-1,1):
  for row in (-1,0,1):
   anchor=(side*(4.5+abs(row)*1.2),22,-8+row*7)
   s.part(('L' if side<0 else 'R')+'-leg-'+str(row),anchor,'stride')
   x,y,z=anchor;s.stroke([(x,y,z),(x+side*4,16,z+row*2),(x+side*6,8,z+row*3),(x+side*7,2,z+row*3+2)],[2,1.9,1.15,.5],1)
   s.stroke([(x+side*6,8,z+row*3),(x+side*7.2,0,z+row*3+3)],[1.3,1.1],5)
  s.part(('L' if side<0 else 'R')+'-scythe',(side*6,32,4),'arm')
  s.stroke([(side*4,31,3),(side*9,30,7),(side*12,24,9)],[2.4,1.6,1.2],1)
  s.stroke([(side*12,24,9),(side*15,30,10),(side*16,38,10)],[1.3,.8,.04],3)
  s.stroke([(side*12,24,9),(side*13,18,12)],[1,.04],5)
 s.part('tail',(0,19,-17),'tail')
 s.stroke([(0,19,-16),(0,17,-23),(0,11,-27),(0,12,-32)],[2.8,2.1,1.4,.16],1)
 for z in (-19,-23,-26):
  for side in (-1,1):s.stroke([(side*1,19,z),(side*3,21,z-2)],[.7,.08],3)
 return s.export()

def mercury():
 p=[pal('dark wrought iron',51,[.31,.35,.37]),pal('burnished bronze',24,[.79,.51,.25]),
    pal('silver mirror blade',51,[.92,.89,.78]),pal('gold rim',26,[.95,.71,.31]),
    pal('white solar core',12,[1,.93,.64],3.3),pal('blue sensor',12,[.19,.58,1],1.5),
    pal('mechanical joint',35,[.18,.20,.24]),pal('heat damage',35,[.28,.24,.19])]
 s=Sculpture('Mercury','Солнечный скарабей','Mercury',.25,p)
 s.part('abdomen',(0,19,-3),'breathe');s.ball(0,21,-2,9,7.5,13,0)
 s.ball(0,26,-4,9.5,4,12,1)
 for z in (-11,-6,-1,4):
  for side in (-1,1):s.stroke([(side*6,25,z),(side*8,24,z)],[.8,.2],3)
 s.part('thorax',(0,20,8),'breathe');s.ball(0,21,8,7,6,7,1)
 s.ball(0,21,14,4.5,4.8,2.2,6);s.ball(0,22,16,2,2,1.4,4)
 for side in (-1,1):s.ball(side*4,22,14.5,1,1,1,5)
 s.part('head',(0,23,18),'look');s.ball(0,22,18,4.5,4.2,4.5,0)
 s.stroke([(0,20,19),(0,19,24)],[2,1],1)
 for side in (-1,1):
  s.ball(side*2.6,24,21,.9,.8,.9,5)
  s.stroke([(side*3,23,21),(side*5,22,25),(side*6,18,27)],[.9,.55,.05],6)
 # six jointed, visibly separated legs; no antigravity hovering
 for side in (-1,1):
  for row,z in enumerate((-10,0,10)):
   s.part(('L' if side<0 else 'R')+'-leg-'+str(row),(side*7,19,z),'stride')
   s.stroke([(side*7,20,z),(side*11,17,z+row*2),(side*14,9,z+row*3),(side*15,1,z+row*4)],[2.3,2,1.1,.7],0)
   s.stroke([(side*15,1,z+row*4),(side*17,.5,z+row*4+2)],[1.0,.3],6)
   s.ball(side*11,17,z+row*2,1.5,1.5,1.5,3)
 # six sun-shield blades are separate pivotable 3D shapes, rising over the back.
 for side in (-1,1):
  for blade,z in enumerate((-10,-1,8)):
   s.part(('L' if side<0 else 'R')+'-mirror-'+str(blade),(side*5,26,z),'mirror')
   start=(side*5,27,z)
   s.stroke([start,(side*(7+blade*.8),33,z-2),(side*(9+blade),39+blade%2*3,z-4),(side*(10+blade),44+blade%2*4,z-7)],[2.4,2.4,1.5,.12],2)
   s.stroke([(side*5,27,z),(side*(7+blade*.8),33,z-2),(side*(9+blade),39+blade%2*3,z-4)],[.65,.62,.28],3)
 return s.export()

if __name__=='__main__':
 OUT.mkdir(parents=True,exist_ok=True)
 mars();venus();mercury()
