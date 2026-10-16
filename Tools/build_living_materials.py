"""Eight independent AI materials; 512px periodic game maps. Derived normals are estimates, not scans.
Only these new masters are normalized on first run; historical art is never rewritten.
"""
from pathlib import Path
import json,uuid,hashlib,re
import numpy as np
from PIL import Image,ImageFilter
from model_io import load_catalog,save_catalog
R=Path(__file__).resolve().parents[1];O=R/'Assets/Art/PBR'
specs=[('metal-ingot','metal',.83,.98,.65),('leather','leather',.25,0,2),('copper-block','copper',.74,.96,.75),('raw-meat','raw-meat',.46,0,1.2),('cooked-meat','cooked-meat',.26,0,1.6),('water','water',.97,0,3),('lava','lava',.28,0,2),('cat-fur','cat-fur',.13,0,2)]
for name,source,smooth,metal,strength in specs:
 p=R/f'ArtSource/AI/living-{source}-master.jpg';im=Image.open(p).convert('RGB')
 if max(im.size)>768:im.thumbnail((768,768));im.save(p,quality=92,subsampling=0,optimize=True)
 a=np.asarray(im.resize((512,512),Image.Resampling.LANCZOS),float)/255
 for axis in [0,1]:
  for k in range(20):
   ix=[slice(None)]*3;iy=ix.copy();ix[axis]=k;iy[axis]=511-k;ix,iy=tuple(ix),tuple(iy);lo,hi=a[ix].copy(),a[iy].copy();t=.5*(1-k/20)**2;a[ix]=lo*(1-t)+hi*t;a[iy]=hi*(1-t)+lo*t
 h=np.asarray(Image.fromarray(np.uint8(a.mean(-1)*255)).filter(ImageFilter.GaussianBlur(.7)),float)/255
 dx=(np.roll(h,-1,1)-np.roll(h,1,1))*strength;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*strength;n=np.stack([-dx,dy,np.ones_like(h)],-1);n/=np.linalg.norm(n,axis=2)[:,:,None]
 mask=np.stack([np.zeros_like(h)+metal,np.clip(.96-(.5-h)*.08,.85,1),h,np.clip(smooth+(h-.5)*.08,0,1)],-1)
 Image.fromarray(np.uint8(a*255)).save(O/f'{name}_albedo.jpg',quality=95,subsampling=0,optimize=True)
 for suffix in ['.png','.png.meta']:(O/f'{name}_albedo{suffix}').unlink(missing_ok=True)
 for kind,arr in [('normal',n*.5+.5),('mask',mask)]:Image.fromarray(np.uint8(np.clip(arr,0,1)*255)).quantize(colors=256,method=Image.Quantize.FASTOCTREE,dither=Image.Dither.NONE).save(O/f'{name}_{kind}.png',optimize=True)
 for kind,ext in [('albedo','jpg'),('normal','png'),('mask','png')]:
  f=O/f'{name}_{kind}.{ext}';meta=Path(str(f)+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'lestingcraft-new-era/'+str(f.relative_to(R))).hex+'\n')
p=R/'Assets/Scripts/Presentation/SurfaceLayers.cs';s=p.read_text();names=['leather','raw-meat','cooked-meat','cat-fur']
for name in names:
 if '"'+name+'"' not in s:s=s.replace('"diamond-facets"};','"diamond-facets",'+','.join('"'+x+'"' for x in names)+'};');break
p.write_text(s)
layers=re.findall(r'"([a-z-]+)"',s.split('Names={')[1].split('};')[0]);d=load_catalog(R)
# Palette is shared with held models and software icon renderer; avoid touching unrelated cells.
for model in d['models']:
 target={136:'leather',139:'leather',135:'raw-meat',138:'cooked-meat'}.get(model['id'])
 if target:
  for i in range(3,len(model['cells']),4):
   old=model['cells'][i];color='#ffffff';pair=(color,layers.index(target));pairs=list(zip(d['colors'],d['layers']))
   if pair not in pairs:d['colors'].append(color);d['layers'].append(pair[1]);pairs.append(pair)
   model['cells'][i]=pairs.index(pair)
save_catalog(R,d)
p=O/'materials.json';info=json.loads(p.read_text());info['layers']=layers;info['livingMaterials']={'materials':[v[0] for v in specs],'masters':[{'path':str(p.relative_to(R)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted((R/'ArtSource/AI').glob('living-*-master.jpg'))],'resolution':512,'maps':'AI albedo; estimated periodic normals / metallic-AO-height-smoothness, not scans'};p.write_text(json.dumps(info,ensure_ascii=False,indent=2))
print('8 AI masters / 8 PBR sets; layers',len(layers))
