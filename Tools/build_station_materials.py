"""Original 512px station/campfire/plank materials, derived normals and packed PBR estimates (not scans)."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter
R=Path(__file__).resolve().parents[1];O=R/'Assets/Art/PBR';N=512
y,x=np.mgrid[:N,:N];u=x/N;v=y/N;rng=np.random.default_rng(7341)
def load(name):return np.asarray(Image.open(O/f'{name}_albedo.png').convert('RGB'),dtype=float)/255
def blur(a,r):return np.asarray(Image.fromarray(np.uint8(np.clip(a,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=float)/255
def periodic(a):
 a=a.copy()
 for axis in (0,1):
  for k in range(12):
   w=(1-k/12)**2*.5
   if axis==0:q,t=a[k].copy(),a[-1-k].copy();a[k]=q*(1-w)+t*w;a[-1-k]=t*(1-w)+q*w
   else:q,t=a[:,k].copy(),a[:,-1-k].copy();a[:,k]=q*(1-w)+t*w;a[:,-1-k]=t*(1-w)+q*w
 return a
noise=rng.random((N,N));grain=np.sin(u*2*np.pi*64+np.sin(v*2*np.pi*3)*2+blur(noise,8)*12)*.025
wood=load('wood-end')*.38+np.array([.26,.15,.065])+grain[:,:,None]
# Alternating real plank bands, fine grain, dark joints, end-grain plugs and square iron nails.
planks=wood.copy();rows=(y//128)%2;planks*=np.where(rows==0,.95,1.1)[:,:,None]
joints=((y%128)<6)|(((x+(y//128)*99)%512)<5);planks[joints]*=.30
for cy in [18,110,146,238,274,366,402,494]:
 for cx in [26,480]:planks[cy-3:cy+3,cx-3:cx+3]=np.array([.14,.15,.15]);planks[cy-2:cy,cx-2:cx+2]=.35
bench_top=wood*.85
rim=(x<32)|(x>479)|(y<32)|(y>479);bench_top[rim]*=.52
# Inlaid measuring grid, scratches and joined edge braces, not text baked into a block.
for p in [176,336]:bench_top[(abs(x-p)<3)&(y>48)&(y<464)]*=.45;bench_top[(abs(y-p)<3)&(x>48)&(x<464)]*=.45
for p in range(64,464,16):bench_top[37:48,p:p+2]=.20
for i in range(37):
 px=int(rng.integers(50,450));py=int(rng.integers(50,450));bench_top[py:py+2,px:px+int(rng.integers(4,29))]*=.68
bench_side=planks.copy();braces=(x<36)|(x>475)|((y>80)&(y<109))|((y>405)&(y<441));bench_side[braces]*=.56
# Tool silhouettes recessed into side panels and square studs.
bench_side[160:353,250:265]*=.42;bench_side[158:180,188:328]*=.42
for cy in [56,458]:
 for cx in [21,490]:bench_side[cy-5:cy+5,cx-5:cx+5]=(.40,.37,.31)
stone=load('stone');basalt=load('basalt');bricks=stone*.65+basalt*.27
mortar=(y%128<9)|((x+(y//128)*128)%256<9);bricks[mortar]*=.37
furnace_top=bricks.copy();furnace_top[80:432,80:432]*=.55;furnace_top[105:407,105:407]=basalt[105:407,105:407]*.65
furnace_front=bricks.copy();furnace_front[87:250,77:435]*=.24;furnace_front[303:451,77:435]*=.18
for cx in range(98,420,40):furnace_front[315:441,cx:cx+10]=stone[315:441,cx:cx+10]*.60
# Unlit ember bed is a warm mineral tint, not an always-burning light source.
furnace_front[408:431,100:414]*=np.array([2.0,1.1,.5])
coal=basalt*.24;ember=(blur(noise,.6)>.59);coal[ember]*=np.array([3.6,1.1,.28])
camp_side=stone*.6;logs=((y>95)&(y<215))|((y>290)&(y<410));camp_side[logs]=wood[logs]*.72
camp_top=coal.copy();logs2=(abs(x-y)<45)|(abs(x+y-512)<45);camp_top[logs2]=wood[logs2]*.58
specs=[('planks',planks,.24),('workbench-top',bench_top,.23),('workbench-side',bench_side,.19),('furnace-side',bricks,.16),('furnace-front',furnace_front,.17),('furnace-top',furnace_top,.15),('campfire-side',camp_side,.14),('campfire-top',camp_top,.19)]
for name,a,smooth in specs:
 a=np.clip(periodic(a),0,1);h=blur(a.mean(-1),.65);dx=(np.roll(h,-1,1)-np.roll(h,1,1))*5;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*5
 slope=np.maximum(1,np.sqrt(dx*dx+dy*dy)/.9);dx/=slope;dy/=slope
 normal=np.stack([-dx,dy,np.ones_like(h)],-1);normal/=np.linalg.norm(normal,axis=-1)[:,:,None]
 mask=np.stack([np.zeros_like(h),np.clip(.94-(blur(h,5)-h)*.5,.75,1),h,np.clip(smooth+(h-h.mean())*.16,.04,.6)],-1)
 for kind,data in [('albedo',a),('normal',normal*.5+.5),('mask',mask)]:Image.fromarray(np.uint8(np.clip(data,0,1)*255)).save(O/f'{name}_{kind}.png',optimize=True)
info=json.loads((O/'materials.json').read_text());info['layers']=[n for n in info['layers'] if n not in [s[0] for s in specs]]+[s[0] for s in specs];info['version']=3;info['stationSource']='Original procedural joinery/masonry + derived PBR estimates';(O/'materials.json').write_text(json.dumps(info,ensure_ascii=False,indent=2))
print('8 original 512px material sets generated; total',len(info['layers']))
