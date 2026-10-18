"""Separate AI-generated coats for deer, hare and boar; run AFTER build_materials_v6.py.
512px seamless derivatives. Normal/AO/height are image-derived estimates, NOT scans.
Original masters are retained verbatim. Underfur and keratin are derived auxiliaries,
not additional independently generated AI images. Existing layer indices stay stable.
"""
from pathlib import Path
import hashlib,json,re,uuid
import numpy as np
from PIL import Image,ImageFilter
R=Path(__file__).resolve().parents[1];S=R/'ArtSource/Textures/Animals';O=R/'Assets/Art/PBR';N=512

def source(name):
 return np.asarray(Image.open(S/f'{name}.jpg').convert('RGB').resize((N,N),Image.Resampling.LANCZOS),float)/255

def blur(a,r):
 return np.asarray(Image.fromarray(np.uint8(np.clip(a,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),float)/255

def periodic(a):
 a=a.copy()
 for axis in [0,1]:
  for k in range(20):
   t=.5*(1-k/20)**2
   if axis==0:b,c=a[k].copy(),a[-1-k].copy();a[k]=b*(1-t)+c*t;a[-1-k]=c*(1-t)+b*t
   else:b,c=a[:,k].copy(),a[:,-1-k].copy();a[:,k]=b*(1-t)+c*t;a[:,-1-k]=c*(1-t)+b*t
 return a

coats={n:source(n) for n in ['deer-fur','hare-fur','boar-bristles']}
specs=[('deer-fur',coats['deer-fur'],.17,1.8),('hare-fur',coats['hare-fur'],.10,2.3),('boar-bristles',coats['boar-bristles'],.13,3.2),
 ('animal-underfur',(.66+coats['hare-fur'].mean(-1)*.28)[:,:,None]*np.array([1.,.99,.97]),.1,1.5),
 ('animal-keratin',(.065+blur(coats['boar-bristles'].mean(-1),2)*.13)[:,:,None]*np.array([1.,.96,.92]),.43,.5)]
for name,a,smooth,strength in specs:
 a=periodic(np.clip(a,0,1));h=periodic(blur(a.mean(-1),.5));dx=(np.roll(h,-1,1)-np.roll(h,1,1))*strength;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*strength
 normal=np.stack([-dx,dy,np.ones_like(h)],-1);normal/=np.linalg.norm(normal,axis=-1)[:,:,None]
 mask=np.stack([np.zeros_like(h),np.clip(.96-(blur(h,3)-h)*.7,.7,1),h,np.clip(smooth+(h-h.mean())*.12,.05,.55)],-1)
 Image.fromarray(np.uint8(a*255)).save(O/f'{name}_albedo.jpg',quality=95,subsampling=0,optimize=True)
 for kind,data in [('normal',normal*.5+.5),('mask',mask)]:
  Image.fromarray(np.uint8(np.clip(data,0,1)*255)).quantize(colors=256,method=Image.Quantize.FASTOCTREE,dither=Image.Dither.NONE).save(O/f'{name}_{kind}.png',optimize=True)
 for kind,ext in [('albedo','jpg'),('normal','png'),('mask','png')]:
  p=O/f'{name}_{kind}.{ext}';meta=Path(str(p)+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'lestingcraft-new-era/'+p.relative_to(R).as_posix()).hex+'\n')
info=json.loads((O/'materials.json').read_text());info['version']=7
info['layers']=re.findall(r'"([a-z-]+)"',(R/'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text().split('Names={')[1].split('};')[0])
info['animalTextures']={'version':1,'masters':[{'path':str(p.relative_to(R)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(S.glob('*.jpg'))],
 'species':{'deer':'deer-fur','hare':'hare-fur','boar':'boar-bristles'},'auxiliaries':['animal-underfur','animal-keratin'],
 'resolution':512,'provenance':'3 separate AI-generated image masters; estimated normals/AO/height, not measured scans; pale underfur and dark keratin derived from these masters'}
(O/'materials.json').write_text(json.dumps(info,ensure_ascii=False,indent=2))
print('PASS: 3 species-specific AI coats + 2 derived auxiliary PBR sets;',len(info['layers']),'total layers; original images preserved')
