"""CI guard for authored planet boss assets; run with python Tools/check_planet_boss_models.py."""
import json
from collections import deque
from pathlib import Path
R=Path(__file__).resolve().parents[1]
NAMES={'Mars':'Кузнец разлома','Venus':'Кислотная владычица','Mercury':'Солнечный скарабей'}
total=0
for planet,name in NAMES.items():
 path=R/f'Assets/Resources/Models/PlanetBoss{planet}.json'
 d=json.loads(path.read_text(encoding='utf8'))
 assert d['version']==1 and d['slug']==planet and d['planet']==planet and d['name']==name
 assert .12<=d['unit']<=.5 and len(d['parts'])>=6 and len(d['palette'])<=255
 assert all(0<=m['layer']<63 and len(m['tint'])==3 and all(0<=c<=1 for c in m['tint']) and 0<=m['emission']<=4 for m in d['palette'])
 assert sum(m['emission']>1 for m in d['palette'])>=1
 parts=set();voxel_count=0;faces=0;all_cells=set();ground=0;anim=0
 for part in d['parts']:
  assert part['name'] not in parts;parts.add(part['name'])
  assert len(part['pivot'])==3 and len(part['cells'])>0 and len(part['cells'])%4==0
  assert part['motion'] in ('fixed','arm','stride','mirror','look','tail','breathe')
  anim+=part['motion']!='fixed'
  a=part['cells'];v={}
  for i in range(0,len(a),4):
   key=tuple(a[i:i+3]);idx=a[i+3]
   assert key not in v and 0<=idx<len(d['palette'])
   v[key]=idx
   p=tuple(key[j]+part['pivot'][j] for j in range(3));all_cells.add(p)
   assert -48<p[0]<48 and 0<=p[1]<68 and -48<p[2]<48,(planet,part['name'],p)
   ground+=p[1]<=1
  remaining=set(v);queue=[remaining.pop()]
  while queue:
   x,y,z=queue.pop()
   for neighbour in ((x+1,y,z),(x-1,y,z),(x,y+1,z),(x,y-1,z),(x,y,z+1),(x,y,z-1)):
    if neighbour in remaining:remaining.remove(neighbour);queue.append(neighbour)
  assert not remaining, (planet,part['name'],'detached geometry',len(remaining))
  for x,y,z in v:
   faces+=sum((x+dx,y+dy,z+dz) not in v for dx,dy,dz in ((1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)))
  voxel_count+=len(v)
 assert 2500<voxel_count<35000 and 800<faces<80000
 assert anim>=5 and len(all_cells)>2500 and ground>=4
 assert any('leg' in n for n in parts) and any(('head' in n or 'crown' in n) for n in parts)
 total+=voxel_count
 print(f'PASS {planet}: {len(parts)} articulated parts, {voxel_count} game voxels, {faces} exposed quads, ground cells={ground}')
print('PASS planet boss assets: 3 planetary models /',total,'real game voxels, no orbit or moon bosses')
