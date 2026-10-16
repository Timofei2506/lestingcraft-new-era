"""Iteration 11. Eight actual AI image masters -> seamless 512px material derivatives.
Normal/height/AO are ESTIMATES from image luminance, not measured scans. Additional
geology/masonry derives from these masters; gold/silver/tin are separately identified
resources, not random visual skins. JPEG albedo q95 4:4:4; lossless RGB normals/packed
RGBA masks. Run after v5; then build_animal_materials.py, then build_voxel_items.py. No export ZIPs are produced.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image,ImageFilter
R=Path(__file__).resolve().parents[1]; O=R/'Assets/Art/PBR'; S=R/'ArtSource/Textures/Iteration11'; N=512
y,x=np.mgrid[:N,:N]
def master(n):return np.asarray(Image.open(S/(n+'.jpg')).convert('RGB').resize((N,N),Image.Resampling.LANCZOS),float)/255
A={n:master(n) for n in ['iron-ore','copper-ore','gold-ore','coal-ore','bedrock','limestone-marble','obsidian','clay-brick']}
def blur(a,r):return np.asarray(Image.fromarray(np.uint8(np.clip(a,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),float)/255
def periodic(a):
 a=a.copy()
 for axis in [0,1]:
  for k in range(20):
   t=.5*(1-k/20)**2
   if axis==0:b,c=a[k].copy(),a[-1-k].copy();a[k]=b*(1-t)+c*t;a[-1-k]=c*(1-t)+b*t
   else:b,c=a[:,k].copy(),a[:,-1-k].copy();a[:,k]=b*(1-t)+c*t;a[:,-1-k]=c*(1-t)+b*t
 return a
spec={}
def add(n,a,s=.23,metal=0,strength=3):spec[n]=(a,s,metal,strength)
add('ore',A['iron-ore'],.38,.58);add('ore-copper',A['copper-ore'],.48,.67);add('ore-gold',A['gold-ore'],.6,.83);add('ore-coal',A['coal-ore'],.18)
gold=A['gold-ore'];gray=gold.mean(-1);mineral=np.clip((gold[:,:,0]-gold[:,:,2])/.22,0,1)
add('ore-silver',gray[:,:,None]*np.array([.95,1.01,1.07]),.53,mineral*.75)
add('ore-tin',A['iron-ore'].mean(-1)[:,:,None]*np.array([.94,.97,.94]),.4,.45)
add('ore-quartz',A['limestone-marble']*.84+np.array([.08,.09,.11]),.57)
add('ore-sulfur',gold*(1-mineral[:,:,None]*.25)+mineral[:,:,None]*np.array([.12,.10,-.13]),.2)
add('bedrock',A['bedrock'],.15,strength=5)
rock=A['limestone-marble'];obs=A['obsidian'];clay=A['clay-brick']
add('granite',A['iron-ore']*.5+rock*.35+np.array([.06,.025,.025]),.27)
add('limestone',rock*.88+np.array([.06,.05,.025]),.19)
add('slate',A['bedrock']*.74+rock*.1,.22)
add('marble',rock*.87+.10,.79,strength=.65)
add('obsidian',obs,.88,.08,strength=1.4)
add('gravel',A['bedrock']*.64+A['iron-ore']*.3,.12,strength=5)
add('clay',blur(clay.mean(-1),4)[:,:,None]*np.array([1.3,.78,.55]),.22,strength=1.6)
add('mud',blur(A['bedrock'].mean(-1),2)[:,:,None]*np.array([.52,.41,.28]),.52,strength=1.5)
vegetation=blur(A['coal-ore'].mean(-1),5);moss=np.clip((vegetation-.22)*4,0,.8)
add('mossy-stone',A['bedrock']*(1-moss[:,:,None]) + moss[:,:,None]*np.array([.19,.25,.09]),.15)
# Staggered masonry relief masks applied to generated stone/brick imagery.
for name,src,w,h in [('cobblestone',A['bedrock'],128,128),('stone-bricks',rock*.72,256,128),('cracked-bricks',A['bedrock'],256,128),('clay-bricks',clay,256,128)]:
 xx=(x+(y//h%2)*w//2)%w;yy=y%h;edge=np.minimum.reduce([xx,w-xx,yy,h-yy]);bevel=np.clip(edge/7,.24,1)
 a=src*bevel[:,:,None];a[edge<3]*=.45;add(name,a,.2,strength=4)
add('polished-stone',rock*.62,.78,strength=.5)
add('sandstone',rock*np.array([1.11,.95,.70]),.16)
add('coal-block',A['coal-ore'].mean(-1)[:,:,None]*np.array([.3,.31,.34]),.31)
add('copper-block',(.63+blur(A['copper-ore'].mean(-1),1)*.3)[:,:,None]*np.array([.8,.37,.17]),.73,.93,strength=.6)
add('polished-granite',spec['granite'][0],.83,strength=.42)
add('polished-marble',spec['marble'][0]*.99+.01,.93,strength=.18)
add('wildlife-fur',master('wildlife-fur'),.12,strength=2.8)
add('metal-ingot',(.58+blur(A['copper-ore'].mean(-1),1)*.32)[:,:,None]*np.ones(3),.72,.94,strength=.45)
# Colourless fine fractures for a transmitting violet-tinted crystal material, no painted candy stripes.
add('crystal',rock.mean(-1)[:,:,None]*np.array([.94,.96,1.0]),.94,strength=.25)
for name,(a,smooth,metal,strength) in spec.items():
 a=periodic(np.clip(a,0,1));h=periodic(blur(a.mean(-1),.7));dx=(np.roll(h,-1,1)-np.roll(h,1,1))*strength;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*strength
 normal=np.stack([-dx,dy,np.ones_like(h)],-1);normal/=np.linalg.norm(normal,axis=-1)[:,:,None]
 metallic=np.zeros_like(h)+metal if np.isscalar(metal) else metal
 if name in ['ore','ore-copper','ore-gold','ore-tin']:metallic*=np.clip((a.max(-1)-a.min(-1))/.17,0,1)
 mask=np.stack([periodic(metallic),np.clip(.94-(blur(h,4)-h)*.9,.6,1),h,np.clip(smooth+(h-h.mean())*.14,.05,.98)],-1)
 Image.fromarray(np.uint8(a*255)).save(O/f'{name}_albedo.jpg',quality=95,subsampling=0,optimize=True)
 for kind,data in [('normal',normal*.5+.5),('mask',mask)]:
  # Palette storage only: full 512px resolution, no runtime quality-profile change.
  im=Image.fromarray(np.uint8(np.clip(data,0,1)*255));im=im.quantize(colors=256,method=Image.Quantize.FASTOCTREE,dither=Image.Dither.NONE);im.save(O/f'{name}_{kind}.png',optimize=True)
 for suffix in ['.png','.png.meta']:
  (O/f'{name}_albedo{suffix}').unlink(missing_ok=True)
info=json.loads((O/'materials.json').read_text());info['version']=6;info['layers']=__import__('re').findall(r'"([a-z-]+)"',(R/'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text().split('Names={')[1].split('};')[0]);info['iteration11']={'masters':[str(p.relative_to(R)) for p in S.glob('*.jpg')],'derivatives':list(spec),'resolution':512,'maps':'AI-derived albedo, estimated normal / metallic-AO-height-smoothness; not scans','albedo':'JPEG q95 4:4:4, seam-corrected before encoding; lossy seam tolerance applies'}
(O/'materials.json').write_text(json.dumps(info,ensure_ascii=False,indent=2));print(len(spec),'sets derived;',len(info['layers']),'total PBR layers')
