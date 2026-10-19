"""Draft ISS-inspired voxel study. Preview only: NEVER overwrites game assets.
Run: python Tools/preview_station_v2.py
"""
import math
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT.parent.parent / 'work/station-preview/station_v2_iso.png'
DRAFT=OUT.with_name('station_v2_draft.json')
X,Y,Z=112,56,104
palette=[('vacuum','#000000'),('white ceramic','#e9eef2'),('edge plate','#a8b4c2'),('titanium','#647789'),
         ('dark frame','#29394e'),('solar blue','#0c3677'),('solar glint','#3067bc'),('gold frame','#c6a35b'),
         ('panel grid','#8bb7e3'),('glass','#67d4ec'),('radiator','#fafcff'),('thermal gold','#e8b555'),
         ('dark joint','#161f35'),('navigation red','#dc6967'),('shadow panel','#1a4a94')]
V=np.zeros((X,Y,Z),np.uint8)
M={n:i for i,(n,_) in enumerate(palette)}
def put(x,y,z,mat):
    if 0<=x<X and 0<=y<Y and 0<=z<Z: V[x,y,z]=M[mat]
def box(x0,x1,y0,y1,z0,z1,mat):V[x0:x1,y0:y1,z0:z1]=M[mat]
def tube_x(x0,x1,cy,cz,r,inside,skin='white ceramic',rib=0):
    for x in range(x0,x1):
        for y in range(cy-r-1,cy+r+2):
            for z in range(cz-r-1,cz+r+2):
                d=math.hypot(y-cy,z-cz)
                if inside<d<=r+.05:
                    mat=skin
                    if rib and x%rib==0:mat='edge plate'
                    elif y>=cy+2 and abs(z-cz)<=1 and x%7 in (2,3):mat='glass'
                    put(x,y,z,mat)
def tube_z(z0,z1,cx,cy,r,inside,mat='white ceramic'):
    for z in range(z0,z1):
        for x in range(cx-r-1,cx+r+2):
            for y in range(cy-r-1,cy+r+2):
                d=math.hypot(x-cx,y-cy)
                if inside<d<=r+.05:put(x,y,z,mat if z%5 else 'edge plate')

# Slim backbone (avoid the cage that concealed the old station).
for x in range(5,107):
    for zz in (49,54):box(x,x+1,32,34,zz,zz+1,'titanium')
    if x%6==0:
        box(x,x+1,32,34,49,55,'dark frame')
        for k in range(5):put(x+k,32+(k//2),49,'edge plate');put(x+k,32+(k//2),54,'edge plate')
for x in (6,30,54,78,102):
    box(x,x+2,31,34,45,59,'dark frame')
    box(x,x+2,34,35,48,56,'edge plate')
    for z in (47,56):put(x,33,z,'thermal gold')

# Four long hollow pressurised modules with bulkheads and external bands.
tube_x(22,38,22,52,5,3.4,rib=5) # habitation
tube_x(38,63,22,52,6,4.3,rib=7) # laboratory
tube_x(63,83,22,52,5,3.4,rib=5) # node / airlock
for x0,x1,r in ((22,38,5),(38,63,6),(63,83,5)):
    for x in (x0,x1-1):
        for y in range(22-r,22+r+1):
            for z in range(52-r,52+r+1):
                d=math.hypot(y-22,z-52)
                if 2.3<d<=r:put(x,y,z,'edge plate')
    # steel hangers from truss to pressure shell (outside only)
    for x in range(x0+2,x1-1,5):
        for y in range(27,32):
            for z in (49,54):put(x,y,z,'titanium')
# two radial modules projecting along Z for iconic ISS cross silhouette
tube_z(37,49,48,22,4,2.5)
tube_z(55,68,70,22,4,2.5)
for z in (37,67):
    for x in range(45 if z==37 else 67,52 if z==37 else 74):
        for y in range(19,26):
            if 2.2<math.hypot(x-(48 if z==37 else 70),y-22)<4:put(x,y,z,'dark joint')
# end docking connectors
for x0,x1 in ((16,22),(83,89)):
    tube_x(x0,x1,22,52,3,1.4,skin='edge plate',rib=3)
    box(x0,x0+1,18,19,50,54,'thermal gold')
for x in (17,88):
    for y in range(20,25):
        for z in range(50,55):
            if 1.4<math.hypot(y-22,z-52)<3.1:put(x,y,z,'dark joint')
# cupola underside and window strip
for x in range(50,58):
    for y in range(14,17):
        for z in range(49,56):
            if 2<math.hypot(y-16,z-52)<4:put(x,y,z,'glass' if x%2 else 'edge plate')
# gold service foils on radial module and airlock
for z in range(59,64):
    for x in range(67,74):
        if x in (67,73) or z in (59,63):put(x,26,z,'thermal gold')
for x in range(76,83):
    for z in (47,57):put(x,22,z,'thermal gold')

# Eight BLUE solar wings (4 paired positions). Real depth and articulated boom; distinct cells and gilt edging.
for x0 in (10,34,66,90):
    x1=x0+12
    for direction in (-1,1):
        # booms across Z from truss to array root, plus hinge
        for z in (range(42,49) if direction<0 else range(55,62)):
            box(x0+5,x0+7,32,34,z,z+1,'titanium')
        for zz in (42,61):
            if (direction<0 and zz==42) or (direction>0 and zz==61):
                box(x0+4,x0+8,31,35,zz,zz+1,'gold frame')
        lo,hi=(6,43) if direction<0 else (61,98)
        for z in range(lo,hi):
            for x in range(x0,x1):
                edge=x in (x0,x1-1) or z in (lo,hi-1)
                rib=(x-x0)%4==0 or (z-lo)%9==0
                mat='gold frame' if edge else 'panel grid' if rib else ('solar glint' if (x+(z-lo))//3%11==0 else 'solar blue' if (z-lo)%18<9 else 'shadow panel')
                put(x,32,z,mat)
        # external powered backing ribs / box supports
        for z in range(lo,hi,9):
            box(x0+1,x1-1,31,32,z,z+1,'dark frame')
        for x in (x0,x1-1):
            box(x,x+1,31,32,lo,hi,'dark frame')

# Two sail-like white radiators at centre (clearly separated from solar wings).
for x in range(46,62):
    for z in range(42,48):
        put(x,36,z,'edge plate' if x%4==0 or z in (42,47) else 'radiator')
    for z in range(56,62):
        put(x,36,z,'edge plate' if x%4==0 or z in (56,61) else 'radiator')
    for y in range(33,36):
        for z in (47,56):put(x,y,z,'titanium')

# antennas and navigation beacons above the station, clipped to intentional high bounds
for x in (27,58,84):
    box(x-1,x+2,33,35,49,55,'titanium') # face-connect each mast to both truss rails
    for y in range(34,45 if x==58 else 40):put(x,y,52,'dark joint')
    for d in range(-3,4):put(x+d,42 if x==58 else 38,52,'edge plate')
    put(x,44 if x==58 else 39,52,'navigation red')
for x in (26,58,84):
    box(x,x+1,28,32,52,53,'titanium')

# Topological sanity: fully symmetric wings, very different silhouette from former station.
left=(V[:,:,6:43]==M['solar blue']) | (V[:,:,6:43]==M['shadow panel'])
right=(V[:,:,61:98]==M['solar blue']) | (V[:,:,61:98]==M['shadow panel'])
print('solar cells',int(left.sum()),int(right.sum()))
assert int(left.sum())==int(right.sum()) and int(left.sum())>800
assert (V[:,:,50:55]!=0).sum()>1000
# A buildable station must be one connected structure, not decorative islands.
from collections import deque
solid=set(zip(*np.nonzero(V)))
seen={next(iter(solid))}; queue=deque(seen)
while queue:
    x,y,z=queue.popleft()
    for d in ((1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)):
        n=(x+d[0],y+d[1],z+d[2])
        if n in solid and n not in seen:seen.add(n);queue.append(n)
assert len(seen)==len(solid),f'{len(solid)-len(seen)} unattached voxels, example {next(iter(solid-seen))}'
print('voxels',len(solid),'solar blue each side',int(left.sum()),'connected',len(seen))

# Isometric voxel render from +X,+Y,+Z. Renderer is a preview of these EXACT cells, not generative concept art.
W,H,S=1750,1220,7.9
ang=math.radians(30)
def proj(x,y,z):return (W//2+(x-z)*math.cos(ang)*S,H//2-100+(x+z)*math.sin(ang)*S-y*S)
def rgb(h):return tuple(int(h[i:i+2],16) for i in (1,3,5))
base_colors=[rgb(c) for _,c in palette]
# quiet space gradient, stars far behind model
back=Image.new('RGB',(W,H));b=back.load()
for y in range(H):
    k=y/H
    c=(9+int(5*k),13+int(8*k),27+int(12*k))
    for x in range(W):b[x,y]=c
rng=np.random.default_rng(882)
d=ImageDraw.Draw(back)
for i in range(270):
    sx=int(rng.integers(0,W));sy=int(rng.integers(0,H));r=1 if i%8 else 2
    tone=(74,100,146) if i%4 else (153,175,209)
    d.ellipse((sx-r,sy-r,sx+r,sy+r),fill=tone)
# block faces nearest projection, use painter's order. Object blank silhouettes retained.
ys,xs,zs=np.nonzero(V) # actually xs,ys,zs
xs,ys,zs=np.nonzero(V)
order=np.argsort(xs+ys+zs,kind='stable')
def light(c,f):return tuple(min(255,max(0,int(v*f))) for v in c)
for ii in order:
    x,y,z=map(int,(xs[ii],ys[ii],zs[ii]));mat=int(V[x,y,z]);color=base_colors[mat]
    def pts(*corners):return [proj(*q) for q in corners]
    # top +y, x-facing +x, z-facing +z
    if y+1==Y or V[x,y+1,z]==0:
        d.polygon(pts((x,y+1,z),(x+1,y+1,z),(x+1,y+1,z+1),(x,y+1,z+1)),fill=light(color,1.07))
    if x+1==X or V[x+1,y,z]==0:
        d.polygon(pts((x+1,y,z),(x+1,y+1,z),(x+1,y+1,z+1),(x+1,y,z+1)),fill=light(color,.74))
    if z+1==Z or V[x,y,z+1]==0:
        d.polygon(pts((x,y,z+1),(x+1,y,z+1),(x+1,y+1,z+1),(x,y+1,z+1)),fill=light(color,.62))
# subtle header outside model
font=ImageFont.load_default()
d.text((60,50),'LESTINGCRAFT  /  ORBITAL STATION     MODEL STUDY V2',font=font,fill=(186,211,230))
d.text((60,70),'EXACT VOXEL RENDER   •   MODEL NOT YET IN GAME',font=font,fill=(92,141,174))
OUT.parent.mkdir(parents=True,exist_ok=True)
back.save(OUT,optimize=True)
# A reproducible DRAFT mesh only; intentionally outside the game asset tree until approved.
flat=V.reshape(-1)
runs=[];last=int(flat[0]);length=0
for val in flat:
    cur=int(val)
    if cur==last:length+=1
    else:runs.append([last,length]);last=cur;length=1
runs.append([last,length])
assert sum(n for _,n in runs)==V.size
DRAFT.write_text(json.dumps({'name':'Station V2 DRAFT — needs visual approval',
 'size':[X,Y,Z],'palette':[{'name':n,'hex':h} for n,h in palette],
 'runs':runs},separators=(',',':')),encoding='utf-8')
print('preview',OUT,'draft RLE',DRAFT,'runs',len(runs))
