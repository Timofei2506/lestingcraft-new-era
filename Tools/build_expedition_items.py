from model_io import load_catalog,save_catalog
"""Append exact shared weapon geometry + software icons. Never rewrites old model cells/icons."""
from pathlib import Path
import json,math
from PIL import Image
R=Path(__file__).resolve().parents[1];source=(R/'Tools/build_voxel_items.py').read_text();ctx={'__file__':str(R/'Tools/build_voxel_items.py')};exec(compile(source.split('serial=[]')[0],str(R/'Tools/build_voxel_items.py'),'exec'),ctx)
p=R/'Assets/Resources/Models/VoxelItems.json';d=load_catalog(R);palette=list(zip(d['colors'],d['layers']));ctx['PALETTE']=palette
import numpy as np
ctx['COLORS']=[np.array([int(c[i:i+2],16)/255 for i in (1,3,5)]) for c,l in palette]
def pal(color,layer):
 if (color,layer) not in palette:palette.append((color,layer));ctx['COLORS'].append(np.array([int(color[i:i+2],16)/255 for i in (1,3,5)]))
 return palette.index((color,layer))
metal=pal('#c7d1d7',51);edge=pal('#727d86',51);gold=pal('#cda554',51);dark=pal('#403536',4);wood=pal('#b39870',4);dia=pal('#e0fbff',58);diaedge=pal('#65c6e6',58);ore=pal('#ffffff',57);cord=pal('#c3b28b',15)
new=[];box=ctx['box'];model=ctx['model']
for id,name in [(47,'Алмазная руда'),(48,'Алмазный блок')]:
 m=model(id,name);box(m,0,0,0,12,12,12,ore if id==47 else dia);new.append(m)
m=model(149,'Алмаз')
for y in range(15):
 radius=max(1,5-abs(y-8)//2);box(m,-radius,y,-radius,radius+1,y+1,radius+1,dia)
 for x in range(-radius,radius+1):
  for z in range(-radius,radius+1):
   if abs(x)+abs(z)>radius+2:m['voxels'].pop((x,y,z),None)
   elif x<0 and z>0:m['voxels'][x,y,z]=diaedge
new.append(m)
weapons=json.loads((R/'Assets/Resources/Models/ExpeditionWeapons.json').read_text())
for id,name,kind,crystal in [(150,'Железная кирка','pick',False),(151,'Алмазная кирка','pick',True)]+[(w['id'],w['name'],w['kind'],w['material']==149) for w in weapons]:
 m=model(id,name);face=dia if crystal else metal;rim=diaedge if crystal else edge;h=9 if kind in ['sword','dagger','greatsword'] else 29 if kind=='spear' else 19
 box(m,-1,0,-1,2,h,2,wood)
 for y in range(1,h-1,3):box(m,-1,y,-2,2,y+1,3,dark)
 box(m,-1,0,-2,2,2,3,gold)
 if kind in ['sword','dagger','greatsword']:
  length=14 if kind=='dagger' else 29 if kind=='sword' else 38;wide=2 if kind=='dagger' else 3 if kind=='sword' else 4
  box(m,-wide-3,h-1,-2,wide+4,h+2,3,edge);box(m,-wide-2,h,-2,wide+3,h+1,3,gold)
  for y in range(h+2,h+length):
   w=min(wide,max(0,(h+length-y)//3));box(m,-w,y,-1,w+1,y+1,2,rim);box(m,-max(0,w-1),y,-2,max(0,w-1)+1,y+1,3,face)
   if kind!='dagger' and y<h+length-5:box(m,0,y,2,1,y+1,3,edge if not crystal else diaedge)
  box(m,-1,h+3,3,2,h+5,4,diaedge if crystal else gold)
 elif kind=='pick':
  for x in range(-11,12):
   yy=h+3-abs(x)//4;box(m,x,yy,-2,x+1,yy+3,3,rim);box(m,x,yy+1,-2,x+1,yy+2,3,face)
 elif kind=='spear':
  box(m,-2,h-1,-2,3,h+2,3,gold)
  for y in range(h+2,h+17):
   w=min(3,max(0,(h+17-y)//3));box(m,-w,y,-1,w+1,y+1,2,rim);box(m,-max(0,w-1),y,1,max(0,w-1)+1,y+1,2,face)
  for x in [-5,4]:box(m,x,h,-1,x+2,h+5,2,rim)
 elif kind=='axe':
  box(m,-3,h,-3,4,h+5,4,edge)
  for x in range(-10,11):
   w=abs(x);low=h-3+w//4;high=h+7-w//5;box(m,x,low,-2,x+1,high,3,face if w>7 else rim)
  box(m,-1,h+1,-4,2,h+4,5,gold)
 elif kind=='hammer':
  box(m,-9,h,-4,10,h+8,5,rim);box(m,-7,h+1,-3,8,h+7,4,face);box(m,-10,h+1,-3,-8,h+7,4,gold);box(m,9,h+1,-3,11,h+7,4,gold)
  for x,y in [(-3,h+3),(-2,h+4),(0,h+4),(1,h+3),(2,h+2)]:box(m,x,y,4,x+1,y+1,5,gold if not crystal else diaedge)
 elif kind=='bow':
  m['voxels']={}
  for y in range(34):
   x=int(8*math.sin(math.pi*y/33));box(m,x,y,-1,x+2,y+1,2,wood);box(m,0,y,0,1,y+1,1,cord)
   if y%6==0:box(m,x,y,-2,x+2,y+1,3,gold)
  box(m,7,13,-2,11,20,3,dark)
 new.append(m)
m=model(165,'Стрелы');box(m,0,0,0,1,28,1,wood);box(m,-1,27,-1,2,30,2,metal);box(m,0,30,0,1,32,1,edge)
for k in range(5):box(m,-3+k//2,k,-1,4-k//2,k+1,2,cord)
new.append(m)
d['models']=[m for m in d['models'] if m['id'] not in {m['id'] for m in new}]
for m in new:
 im=ctx['render'](m);im.quantize(colors=192,method=Image.Quantize.FASTOCTREE,dither=Image.Dither.NONE).save(R/f'Assets/Resources/VoxelIcons/Item{m["id"]}.png',optimize=True)
 cells=[]
 for (x,y,z),mat in sorted(m['voxels'].items()):cells.extend([x,y,z,mat])
 d['models'].append(dict(id=m['id'],name=m['name'],cells=cells))
d['colors']=[c for c,l in palette];d['layers']=[l for c,l in palette];save_catalog(R,d)
print(len(new),'new models/icons;',len(d['models']),'total. Previous 79 models/icons unchanged.')
