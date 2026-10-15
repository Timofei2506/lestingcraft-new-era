"""Art pipeline v2. Albedo is AI-generated; normal/height/roughness are derived estimates, not scanned PBR.
512px tile maps, periodic borders, linear RGB normals, packed R=metallic G=AO B=height A=smoothness.
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter, ImageDraw, ImageEnhance
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/PBR';OUT.mkdir(parents=True,exist_ok=True)
N=512
rng=np.random.default_rng(2506)
def periodic(a):
 a=a.copy()
 for axis in (0,1):
  for k in range(32):
   w=(1-k/32)**2*.5
   if axis==0:
    x,y=a[k].copy(),a[-1-k].copy();a[k]=x*(1-w)+y*w;a[-1-k]=y*(1-w)+x*w
   else:
    x,y=a[:,k].copy(),a[:,-1-k].copy();a[:,k]=x*(1-w)+y*w;a[:,-1-k]=y*(1-w)+x*w
 return a
def source(name):
 im=Image.open(ROOT/f'ArtSource/Textures/{name}.png').convert('RGB').resize((N,N),Image.Resampling.LANCZOS)
 im=ImageEnhance.Contrast(im).enhance(1.10).filter(ImageFilter.UnsharpMask(radius=1.4,percent=125,threshold=3))
 return periodic(np.asarray(im,dtype=np.float32)/255)
def blur(a,r):return np.asarray(Image.fromarray(np.uint8(np.clip(a,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),dtype=np.float32)/255
stone,dirt,grass,bark,sand,basalt,leaves=[source(n) for n in ['stone','dirt','grass','bark','sand','basalt','leaves']]
y,x=np.mgrid[0:N,0:N];u=x/N;v=y/N
noise=periodic(rng.random((N,N,3)).astype(np.float32))
woodrings=np.sin(np.sqrt((u-.5)**2+(v-.5)**2)*180+blur(noise[:,:,0],6)*9)*.5+.5
wood=np.stack([.46+woodrings*.17,.31+woodrings*.12,.17+woodrings*.08],axis=-1);wood=periodic(wood)
edge=.19+.055*np.sin(u*6*np.pi)+.025*np.sin(u*18*np.pi)
mix=np.clip((edge[:,None] if False else edge-v)*45+.5,0,1)[:,:,None]
grassside=dirt*(1-mix)+grass*mix
grassside=periodic(grassside)
waterwave=(np.sin(u*8*np.pi+np.sin(v*6*np.pi)*.7)+np.sin(v*10*np.pi))*.03
water=np.stack([.10+waterwave,.30+waterwave,.34+waterwave],axis=-1)
cracks=np.maximum(0,blur(basalt.mean(axis=-1),2)-.19)*6
lava=basalt*.55+np.stack([np.clip(cracks,0,1),np.clip(cracks*.35,0,1),np.clip(cracks*.025,0,1)],axis=-1)
# Distinct supplementary materials derived procedurally; no third-party texture pack.
crystal=periodic(np.stack([.35+blur(noise[:,:,0],12)*.25,.19+blur(noise[:,:,1],12)*.16,.49+blur(noise[:,:,2],12)*.35],axis=-1))
aether=periodic(stone*np.array([.38,.83,.87])+np.array([.08,.14,.17]))
ore_mask=(blur(noise[:,:,0],.5)>.66).astype(float)[:,:,None]
ore=stone*(1-ore_mask)+np.array([.72,.44,.18])*ore_mask
snow=periodic(np.ones_like(stone)*np.array([.82,.87,.89])+noise*.06)
bedrock=periodic(basalt*.62)
specs=[('stone',stone,.23,0),('dirt',dirt,.12,0),('grass',grass,.18,0),('grass-side',grassside,.16,0),('bark',bark,.19,0),('wood-end',wood,.27,0),('leaves',leaves,.26,0),('sand',sand,.15,0),('water',water,.92,0),('lava',lava,.49,0),('bedrock',bedrock,.14,0),('basalt',basalt,.21,0),('crystal',crystal,.72,.08),('aether',aether,.40,.04),('ore',ore,.43,.40),('snow',snow,.32,0)]
for name,color,smoothness,metal in specs:
 color=np.clip(periodic(color),0,1)
 lum=color@np.array([.2126,.7152,.0722])
 height=blur(lum,.65)*.78+blur(lum,3.0)*.22
 strength=7.5 if name not in ('water','snow','sand') else 2.5
 dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*strength
 dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*strength
 # Keep derived slopes physically restrained; do not amplify source colour edges into vertical facets.
 slope=np.maximum(1,np.sqrt(dx*dx+dy*dy)/1.25);dx/=slope;dy/=slope
 normal=np.stack([-dx,dy,np.ones_like(height)],axis=-1);normal/=np.linalg.norm(normal,axis=-1)[:,:,None]
 normal=(normal*.5+.5)
 roughvariation=(height-height.mean())*.24
 ao=np.clip(.92-(blur(height,5)-height)*.5,.75,1)
 mask=np.stack([np.full_like(height,metal),ao,height,np.clip(smoothness+roughvariation,.03,.98)],axis=-1)
 for kind,data in [('albedo',color),('normal',normal),('mask',mask)]:Image.fromarray(np.uint8(np.clip(data,0,1)*255)).save(OUT/f'{name}_{kind}.png')
 # Real textured cube icons for the material palette, transparent outside the cube.
 icon=Image.new('RGBA',(128,128));small=Image.fromarray(np.uint8(color*255)).resize((128,128)).convert('RGBA')
 polygons=[([(64,9),(118,36),(64,64),(10,36)],1.10), ([(10,36),(64,64),(64,119),(10,90)],.70), ([(64,64),(118,36),(118,90),(64,119)],.88)]
 for poly,shade in polygons:
  tile=np.asarray(small).copy();tile[:,:,:3]=np.uint8(np.clip(tile[:,:,:3].astype(float)*shade,0,255));layer=Image.fromarray(tile);maski=Image.new('L',(128,128));ImageDraw.Draw(maski).polygon(poly,fill=255);icon.paste(layer,(0,0),maski)
 icon.save(ROOT/f'Assets/Resources/Icons/{name}.png')
import json
(OUT/'materials.json').write_text(json.dumps({'version':2,'resolution':N,'normalEncoding':'RGB linear tangent-space','maskEncoding':'R metallic, G AO, B estimated height, A smoothness','source':'AI albedo + derived estimates; no scan/displacement','layers':[n for n,*_ in specs]},indent=2))
print('Created',len(specs),'tile sets with albedo/normal/mask + cube icons at',N,'px')
