"""Iteration 09: AI albedo source JPEGs + deterministic derived PBR estimates, not scans.
512 texels per metre, raw RGB normals, packed metallic/AO/height/smoothness.
Run after build_material_maps.py and build_station_materials.py to reproduce this refinement.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageFilter,ImageEnhance
R=Path(__file__).resolve().parents[1];O=R/'Assets/Art/PBR';N=512
rng=np.random.default_rng(90917)
def load(p):return np.asarray(Image.open(p).convert('RGB').resize((N,N),Image.Resampling.LANCZOS),dtype=float)/255
def blur(a,r):return np.asarray(Image.fromarray(np.uint8(np.clip(a,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=float)/255
def periodic(a):
 a=a.copy()
 for axis in [0,1]:
  for k in range(24):
   w=(1-k/24)**2*.5
   if axis==0:q,t=a[k].copy(),a[-1-k].copy();a[k]=q*(1-w)+t*w;a[-1-k]=t*(1-w)+q*w
   else:q,t=a[:,k].copy(),a[:,-1-k].copy();a[:,k]=q*(1-w)+t*w;a[:,-1-k]=t*(1-w)+q*w
 # Exact wrapping endpoints, including corners.
 a[-1]=a[0];a[:,-1]=a[:,0];return a
stone=periodic(load(R/'ArtSource/Textures/Iteration09/stone-source.jpg'))
grass=periodic(load(R/'ArtSource/Textures/Iteration09/forest-floor-source.jpg'))
bark=periodic(load(R/'ArtSource/Textures/Iteration09/bark-source.jpg'))
dirt=load(O/'dirt_albedo.png');oldgrass=load(O/'grass-side_albedo.png');side=dirt.copy()
y,x=np.mgrid[:N,:N];edge=85+(np.sin(np.arange(N)/N*6*np.pi)*11+np.sin(np.arange(N)/N*22*np.pi)*6).astype(int)
mask=y<edge[None,:];side[mask]=grass[mask];fringe=(y>=edge[None,:])&(y<edge[None,:]+19);side[fringe]=side[fringe]*.65+grass[fringe]*.35
basalt=stone*np.array([.48,.49,.53]);bedrock=stone*.28
ore=stone.copy();flecks=blur(rng.random((N,N)),.8)>.59;ore[flecks]=ore[flecks]*.3+np.array([.43,.25,.095])
materials={'stone':(stone,.22,0),'grass':(grass,.16,0),'grass-side':(side,.12,0),'bark':(bark,.18,0),'basalt':(basalt,.21,0),'bedrock':(bedrock,.15,0),'ore':(ore,.39,.42)}
for name,(a,smooth,metal) in materials.items():
 a=periodic(np.clip(a,0,1));lum=a@np.array([.2126,.7152,.0722]);h=blur(lum,.75)
 # Height from albedo is an estimate: modest relief, never geometric/collision displacement.
 h=(h-h.mean())*1.2+.5;h=np.clip(periodic(h),.15,.85)
 dx=(np.roll(h,-1,1)-np.roll(h,1,1))*5.5;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*5.5
 slope=np.maximum(1,np.sqrt(dx*dx+dy*dy)/1.10);dx/=slope;dy/=slope
 norm=np.stack([-dx,dy,np.ones_like(h)],-1);norm/=np.linalg.norm(norm,axis=-1)[:,:,None]
 packed=np.stack([np.full_like(h,metal),np.clip(.94-(blur(h,3)-h)*.6,.73,1),h,np.clip(smooth+(h-.5)*.12,.06,.75)],-1)
 for kind,data in [('albedo',a),('normal',norm*.5+.5),('mask',packed)]:Image.fromarray(np.uint8(np.clip(data,0,1)*255)).save(O/f'{name}_{kind}.png',optimize=True)
info=json.loads((O/'materials.json').read_text());info.update(version=4,refinement='AI stone/meadow/bark albedos; derived estimated PBR; exact periodic boundaries',resolution=512);(O/'materials.json').write_text(json.dumps(info,ensure_ascii=False,indent=2))
print('Refined',len(materials),'full material sets; all 24 runtime layers retained.')
