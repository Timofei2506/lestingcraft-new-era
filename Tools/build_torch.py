from model_io import load_catalog,save_catalog
from pathlib import Path
import json
r=Path(__file__).resolve().parents[1];source=(r/'Tools/build_voxel_items.py').read_text();ctx={'__file__':str(r/'Tools/build_voxel_items.py')}
exec(compile(source.split('serial=[]')[0],str(r/'Tools/build_voxel_items.py'),'exec'),ctx)
m=ctx['model'](148,'Факел');box=ctx['box'];box(m,0,0,0,3,17,3,4);box(m,-1,14,-1,4,19,4,18);box(m,0,18,0,3,23,3,32);box(m,1,23,1,2,26,2,36)
im=ctx['render'](m);out=r/'Assets/Resources/VoxelIcons/Item148.png';im.quantize(colors=256,method=ctx['Image'].Quantize.FASTOCTREE,dither=ctx['Image'].Dither.NONE).save(out,optimize=True)
p=r/'Assets/Resources/Models/VoxelItems.json';data=load_catalog(r);data['models']=[model for model in data['models'] if model['id']!=148];cells=[]
for (x,y,z),pal in sorted(m['voxels'].items()):cells += [x,y,z,pal]
data['models'].append({'id':148,'name':'Факел','cells':cells});save_catalog(r,data)
print('Torch model + actual textured voxel icon:',len(cells)//4,'cells. Existing 78 models/icons untouched.')
