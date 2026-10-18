import sys
from pathlib import Path
sys.path.insert(0,'Tools')
from model_io import load_catalog,save_catalog
import numpy as np
from PIL import Image
R=Path(__file__).resolve().parents[1];d=load_catalog(R);pal=list(zip(d['colors'],d['layers']));index=pal.index(('#ffffff',59))
for m in d['models']:
 if m['id']==139:
  for i in range(3,len(m['cells']),4):m['cells'][i]=index
save_catalog(R,d)
source=(R/'Tools/build_voxel_items.py').read_text();ctx={'__file__':str(R/'Tools/build_voxel_items.py')};exec(compile(source.split('serial=[]')[0],'voxel-icon-runtime','exec'),ctx);ctx['PALETTE']=list(zip(d['colors'],d['layers']));ctx['COLORS']=[np.array([int(c[i:i+2],16)/255 for i in (1,3,5)]) for c in d['colors']]
count=0
for m in d['models']:
 changed=m['id'] in [7,8,11,44,46,135,136,138,139,145] or any(d['layers'][p]==51 for p in m['cells'][3::4])
 if not changed:continue
 cells=m['cells'];model={'id':m['id'],'name':m['name'],'voxels':{tuple(cells[i:i+3]):cells[i+3] for i in range(0,len(cells),4)}}
 im=ctx['render'](model)
 if m['id'] in [11,44,145]:
  a=np.asarray(im).copy();a[:,:,3]=np.uint8(a[:,:,3].astype(float)*(.22 if m['id']==44 else .58));im=Image.fromarray(a)
 im.quantize(colors=192,method=Image.Quantize.FASTOCTREE,dither=Image.Dither.NONE).save(R/f'Assets/Resources/VoxelIcons/Item{m["id"]}.png',optimize=True);count+=1
print('Refreshed actual-model icons:',count)
