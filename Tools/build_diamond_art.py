from pathlib import Path
import numpy as np
from PIL import Image,ImageFilter
import json
R=Path(__file__).resolve().parents[1];out=R/'Assets/Art/PBR'
for name in ['diamond-ore','diamond-facets']:
 p=R/f'ArtSource/AI/{name}-master.jpg';im=Image.open(p).convert('RGB');im.thumbnail((768,768));im.save(p,quality=90,subsampling=0,optimize=True)
 a=np.asarray(im.resize((512,512),Image.Resampling.LANCZOS),float)/255
 # Periodic opposite-edge crossfade, preserving central material detail.
 for axis in [0,1]:
  for k in range(16):
   lo=np.take(a,k,axis=axis);hi=np.take(a,511-k,axis=axis);mix=(lo+hi)/2;t=(16-k)/16
   if axis==0:a[k]=lo*(1-t)+mix*t;a[511-k]=hi*(1-t)+mix*t
   else:a[:,k]=lo*(1-t)+mix*t;a[:,511-k]=hi*(1-t)+mix*t
 lum=a@np.array([.2126,.7152,.0722]);height=np.asarray(Image.fromarray(np.uint8(lum*255)).filter(ImageFilter.GaussianBlur(.7)),float)/255
 dx=(np.roll(height,-1,1)-np.roll(height,1,1))*1.5;dy=(np.roll(height,-1,0)-np.roll(height,1,0))*1.5;n=np.stack([-dx,dy,np.ones_like(dx)],-1);n/=np.linalg.norm(n,axis=2)[:,:,None]
 gem=np.clip((a[:,:,2]-a[:,:,0])*.8+lum*.3,0,1);smooth=np.clip(.22+gem*.45 if name=='diamond-ore' else .68+lum*.16,.1,.9)
 mask=np.stack([np.zeros_like(lum),np.ones_like(lum)*.95,height,smooth],-1)
 Image.fromarray(np.uint8(a*255)).save(out/f'{name}_albedo.jpg',quality=94,subsampling=0,optimize=True)
 for kind,arr in [('normal',n*.5+.5),('mask',mask)]:Image.fromarray(np.uint8(np.clip(arr,0,1)*255)).quantize(colors=128,method=Image.Quantize.FASTOCTREE,dither=Image.Dither.NONE).save(out/f'{name}_{kind}.png',optimize=True)
p=out/'materials.json';d=json.loads(p.read_text());d['version']=7;d['layers']=[n for n in d['layers'] if n not in ['diamond-ore','diamond-facets']]+['diamond-ore','diamond-facets'];d['expedition']={'materials':['diamond-ore','diamond-facets'],'maps':'AI-authored albedo with estimated periodic normal / metallic-AO-height-smoothness, not scans'};p.write_text(json.dumps(d,ensure_ascii=False,indent=2))
print('Two diamond materials, 512px, albedo + normal + packed mask.')
