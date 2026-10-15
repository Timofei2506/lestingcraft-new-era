"""Original furnace masonry, fractured bedrock and three ore appearances.
Derived PBR estimates, not scanned maps. Ore variants are visual only (existing block 13).
Run AFTER refine_materials_v4.py/build_station_materials.py. 512px seamless maps.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageFilter
R=Path(__file__).resolve().parents[1];O=R/'Assets/Art/PBR';N=512
rng=np.random.default_rng(10617);y,x=np.mgrid[:N,:N];u=x/N;v=y/N

def blur(a,r):return np.asarray(Image.fromarray(np.uint8(np.clip(a,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),float)/255

def source(n):return np.asarray(Image.open(O/f'{n}_albedo.png').convert('RGB'),float)/255

def periodic(a):
 a=a.copy()
 for axis in (0,1):
  for k in range(12):
   w=(1-k/12)**2*.5
   if axis==0:q,t=a[k].copy(),a[-1-k].copy();a[k]=q*(1-w)+t*w;a[-1-k]=t*(1-w)+q*w
   else:q,t=a[:,k].copy(),a[:,-1-k].copy();a[:,k]=q*(1-w)+t*w;a[:,-1-k]=t*(1-w)+q*w
 return a
fine=rng.random((N,N));micro=blur(fine,.45)-.5;macro=blur(rng.random((N,N)),15)-.5
stone=source('stone');basalt=source('basalt');base=np.clip(stone*.72+basalt*.18+.035,0,1)
# Cellular fracture network with periodic seeds; nearest-two distances define slate seams.
dist=[]
for i in range(38):
 sx,sy=rng.uniform(0,N,2);dx=np.abs(x-sx);dy=np.abs(y-sy);dist.append(np.sqrt(np.minimum(dx,N-dx)**2+np.minimum(dy,N-dy)**2))
dist=np.partition(np.stack(dist),1,axis=0);edge=dist[1]-dist[0];cracks=np.exp(-edge*.75)
bed=np.clip(basalt*.42+stone*.19+np.array([.040,.045,.055]),0,1)
strata=np.sin(v*2*np.pi*13+np.sin(u*2*np.pi*3)*1.7)*.018
bed=(bed+strata[:,:,None]+micro[:,:,None]*.055)*(1-cracks[:,:,None]*.72)
# Masonry firebrick with broken mortar, pores, soot halo and iron firebox.
row=y//128;xx=(x+(row%2)*128)%256;yy=y%128
joint=(xx<9)|(yy<9);bevel=np.minimum(np.minimum(xx,256-xx),np.minimum(yy,128-yy));brick=base*np.array([1.04,.97,.91]);brick*=((.88+.12*row/3)*(1-micro*.18))[:,:,None]
brick[joint]=np.clip(base[joint]*.29,0,1);brick*=np.clip(.66+bevel*.055,.66,1)[:,:,None]
side=brick.copy();front=brick.copy();top=brick.copy()
# Soot accumulates around, not over the entire front.
d=np.sqrt(((x-256)/190)**2+((y-258)/240)**2);front*=np.clip(.48+d*.52,.48,1)[:,:,None]
firebox=(x>80)&(x<432)&(y>120)&(y<423);front[firebox]=np.array([.025,.022,.021])+micro[firebox,None]*.018
rim=((abs(x-80)<8)|(abs(x-432)<8))&(y>112)&(y<432);rim|=((abs(y-120)<8)|(abs(y-423)<8))&(x>72)&(x<440)
front[rim]=np.array([.12,.13,.13])+micro[rim,None]*.07
for cx in range(104,425,42):front[285:416,cx:cx+9]=np.array([.16,.17,.17])+micro[285:416,cx:cx+9,None]*.10
for cy in [126,415]:
 for cx in [82,429]:front[cy-4:cy+4,cx-4:cx+4]=(.38,.36,.32)
# Top is ash-flecked stone, with a dark square flue; front is unlit until a real station burns.
top[112:400,112:400]*=.65;top[147:365,147:365]=basalt[147:365,147:365]*.22
# Mineral lodes use coherent veins + irregular inclusions, not a painted generic coloured cube.
lode=blur(rng.random((N,N)),3);lode=(lode-lode.min())/(lode.max()-lode.min())
vein=np.abs(np.sin(u*2*np.pi*3+np.sin(v*2*np.pi*2)*1.8+blur(fine,2)*1.5))
mineral=((vein<.25)&(lode>.36))|(lode>.73)
ore_specs=[]
for name,tint,metal,smooth in [('ore',(.48,.44,.35),.60,.43),('ore-copper',(.54,.29,.13),.68,.48),('ore-coal',(.035,.038,.043),.05,.27)]:
 a=base.copy();a[mineral]=np.array(tint)*( .70+fine[mineral,None]*.45);a*=1-np.exp(-np.abs(vein-.25)*38)[:,:,None]*.035
 ore_specs.append((name,a,smooth,mineral.astype(float)*metal))
specs=[('bedrock',bed,.17,None),('furnace-side',side,.17,None),('furnace-front',front,.22,rim.astype(float)*.6),('furnace-top',top,.13,None)]+ore_specs
for name,a,smooth,metal in specs:
 a=periodic(np.clip(a,0,1));h=periodic(blur(a.mean(-1),.65));dx=(np.roll(h,-1,1)-np.roll(h,1,1))*4;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*4
 slope=np.maximum(1,np.sqrt(dx*dx+dy*dy)/.83);dx/=slope;dy/=slope
 normal=np.stack([-dx,dy,np.ones_like(h)],-1);normal/=np.linalg.norm(normal,axis=-1)[:,:,None]
 metallic=np.zeros_like(h) if metal is None else periodic(metal)
 mask=np.stack([metallic,np.clip(.94-(blur(h,4)-h)*.8,.63,1),h,np.clip(smooth+(h-h.mean())*.28,.06,.65)],-1)
 for kind,data in [('albedo',a),('normal',normal*.5+.5),('mask',mask)]:Image.fromarray(np.uint8(np.clip(data,0,1)*255)).save(O/f'{name}_{kind}.png',optimize=True)
info=json.loads((O/'materials.json').read_text());info['layers']=[n for n in info['layers'] if n not in ('ore-copper','ore-coal')]+['ore-copper','ore-coal'];info['version']=5;info['iteration10']='Original firebrick/soot/iron firebox, fractured bedrock; iron/copper/coal-like visual lodes of the SAME existing ore block. Derived PBR, not scans.';(O/'materials.json').write_text(json.dumps(info,ensure_ascii=False,indent=2))
print('7 PBR sets refined; total',len(info['layers']))
