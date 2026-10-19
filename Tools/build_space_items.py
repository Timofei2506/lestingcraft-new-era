"""Space stage 1.3 overlay: fuel items 170..177 and station buildings 49..53.

Like build_expedition_items.py this only APPENDS to the shared catalog (palette entries and
models for the new ids) and writes icons for the new ids. Old model cells and old icons are
never rewritten, so existing items keep their exact art.
"""
from model_io import load_catalog,save_catalog
from pathlib import Path
import hashlib
import numpy as np
from PIL import Image

R=Path(__file__).resolve().parents[1]
source=(R/'Tools/build_voxel_items.py').read_text(encoding='utf-8')
ctx={'__file__':str(R/'Tools/build_voxel_items.py')}
exec(compile(source.split('serial=[]')[0],str(R/'Tools/build_voxel_items.py'),'exec'),ctx)

d=load_catalog(R);palette=list(zip(d['colors'],d['layers']));ctx['PALETTE']=palette
ctx['COLORS']=[np.array([int(c[i:i+2],16)/255 for i in (1,3,5)]) for c,l in palette]
def pal(color,layer):
    if (color,layer) not in palette:palette.append((color,layer));ctx['COLORS'].append(np.array([int(color[i:i+2],16)/255 for i in (1,3,5)]))
    return palette.index((color,layer))
NAMES=ctx['NAMES'];box=ctx['box'];model=ctx['model'];render=ctx['render']
def L(name):return NAMES.index(name)

DARK=pal('#2e3440',L('basalt'));WHITE=pal('#f4f6f8',L('snow'));BRASS=pal('#c9a227',L('metal-ingot'));STEEL=pal('#8d96a3',L('metal-ingot'))
FUEL={170:('Сырая нефть','#2b2623','basalt'),171:('Бензин','#c8322b','metal-ingot'),172:('Дизель','#e0b020','metal-ingot'),
      173:('Керосин','#e8eef5','metal-ingot'),174:('Жидкий кислород','#9fe3f2','snow'),175:('Ракетное топливо','#e06a1e','metal-ingot'),
      176:('Гудрон','#18181a','basalt'),177:('Сжатый воздух','#b9c0c8','stone')}
STATIONS={49:('Нефтяная скважина','#4b5563','basalt'),50:('Компрессор','#4f7ea8','metal-ingot'),51:('Перегонный куб','#c4592e','obsidian'),
          52:('Криоустановка','#a8e6f0','snow'),53:('Смеситель топлива','#d8b43a','clay-bricks')}
def body(id):return pal(*[FUEL[id][1],L(FUEL[id][2])]) if id in FUEL else pal(STATIONS[id][1],L(STATIONS[id][2]))

new=[]
# Fuel items: canisters (171..173), drum (170), cryo tank (174), pressure tank (175), tar (176), air bottle (177).
for id in (171,172,173):
    name=FUEL[id][0];P=body(id);m=model(id,name)
    box(m,2,0,2,10,11,9,P);box(m,4,11,3,8,13,8,DARK);box(m,9,8,4,11,11,6,DARK);new.append(m)
m=model(170,FUEL[170][0]);P=body(170)
box(m,1,0,1,11,11,11,P);box(m,0,0,0,12,1,12,DARK);box(m,0,5,0,12,6,12,DARK);box(m,0,10,0,12,11,12,DARK);new.append(m)
m=model(174,FUEL[174][0]);P=body(174)
box(m,2,0,2,10,10,10,P);box(m,2,4,2,10,5,10,STEEL);box(m,3,10,3,9,12,9,WHITE);box(m,4,12,4,8,13,8,STEEL);new.append(m)
m=model(175,FUEL[175][0]);P=body(175)
box(m,2,0,2,10,11,10,P);box(m,2,5,2,10,7,10,DARK);box(m,3,11,3,9,12,9,STEEL);new.append(m)
m=model(176,FUEL[176][0]);P=body(176)
box(m,1,0,1,11,4,11,P);box(m,3,4,3,9,7,9,P);box(m,4,7,4,8,9,8,P);new.append(m)
m=model(177,FUEL[177][0]);P=body(177)
box(m,3,0,3,9,11,9,P);box(m,4,11,4,8,13,8,BRASS);box(m,8,4,3,10,7,4,BRASS);new.append(m)

# Station buildings 49..53: placeable blocks, 12..16 voxels tall, same palette/renderer as items.
m=model(49,STATIONS[49][0]);P=body(49)
box(m,0,0,0,12,2,12,DARK)
for x,z in [(0,0),(10,0),(0,10),(10,10)]:box(m,x,2,z,x+2,14,z+2,STEEL)
box(m,0,5,0,12,6,12,P);box(m,0,10,0,12,11,12,P);box(m,0,14,0,12,16,12,DARK);box(m,4,2,4,8,5,8,BRASS);new.append(m)
m=model(50,STATIONS[50][0]);P=body(50)
box(m,1,0,1,10,10,10,P);box(m,1,10,1,10,11,10,DARK);box(m,10,0,3,12,7,9,STEEL);box(m,5,11,5,7,15,7,STEEL);new.append(m)
m=model(51,STATIONS[51][0]);P=body(51)
box(m,3,0,3,9,14,9,P);box(m,1,0,1,11,2,11,DARK);box(m,2,14,2,10,16,10,DARK);box(m,9,8,4,11,9,8,STEEL);new.append(m)
m=model(52,STATIONS[52][0]);P=body(52)
box(m,2,0,2,10,10,10,P);box(m,2,4,2,10,5,10,STEEL);box(m,3,10,3,9,12,9,WHITE);new.append(m)
m=model(53,STATIONS[53][0]);P=body(53)
box(m,0,0,0,12,8,12,P);box(m,3,8,3,9,12,9,DARK);box(m,5,12,5,7,14,7,STEEL);new.append(m)

ids={m['id'] for m in new}
d['models']=[m for m in d['models'] if m['id'] not in ids]   # idempotent re-run
for m in new:
    icon=R/f'Assets/Resources/VoxelIcons/Item{m["id"]}.png'
    render(m).quantize(colors=256,method=Image.Quantize.FASTOCTREE,dither=Image.Dither.NONE).save(icon,optimize=True)
    meta=icon.with_name(icon.name+'.meta')
    if not meta.exists():
        guid=hashlib.md5(f'LestingCraft-VoxelIcon-{m["id"]}'.encode()).hexdigest()
        meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\n',encoding='utf-8')
    cells=[]
    for (x,y,z),mat in sorted(m['voxels'].items()):cells.extend([x,y,z,mat])
    d['models'].append(dict(id=m['id'],name=m['name'],cells=cells))
d['colors']=[c for c,l in palette];d['layers']=[l for c,l in palette];save_catalog(R,d)
print(len(new),'space models/icons appended;',len(d['models']),'models total')
