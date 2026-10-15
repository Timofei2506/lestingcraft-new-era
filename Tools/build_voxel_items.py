"""Original voxel objects + orthographic textured icons. JSON is the SAME geometry Unity uses in hand.
No painted symbol placeholders: flat faces, square cross-sections, opaque cubic berries.
"""
from pathlib import Path
import json,math,zipfile,io
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Assets/Resources/VoxelIcons';OUT.mkdir(parents=True,exist_ok=True)
MODELS=ROOT/'Assets/Resources/Models';MODELS.mkdir(parents=True,exist_ok=True)
NAMES=['stone','dirt','grass','grass-side','bark','wood-end','leaves','sand','water','lava','bedrock','basalt','crystal','aether','ore','snow','planks','workbench-top','workbench-side','furnace-side','furnace-front','furnace-top','campfire-side','campfire-top']
TEXTURES=[np.asarray(Image.open(ROOT/f'Assets/Art/PBR/{n}_albedo.png').convert('RGB').resize((256,256)),dtype=float)/255 for n in NAMES]
# Palette (colour multiplies material texture); snow is a neutral textile/paint basis.
PALETTE=[('#ffffff',0),('#ffffff',1),('#ffffff',2),('#ffffff',3),('#ffffff',4),('#ffffff',5),('#ffffff',6),('#ffffff',7),('#ffffff',8),('#ffffff',9),('#ffffff',10),('#ffffff',11),('#ffffff',12),('#ffffff',13),('#ffffff',14),('#ffffff',15),('#fff0ce',5),('#e6bb8c',4),('#68513e',4),('#ffffff',0),('#777e82',0),('#f0d294',15),('#54705a',15),('#263a30',15),('#c69a55',15),('#a21d38',15),('#d53951',15),('#5c102a',15),('#618535',15),('#34532b',15),('#c7b78e',15),('#867955',15),('#ff9f18',15),('#d6a258',15),('#92612f',15),('#edd29b',15),('#ffdc65',15)]
PALETTE += [('#ffffff',i) for i in range(16,24)]
COLORS=[np.array([int(c[i:i+2],16)/255 for i in (1,3,5)]) for c,_ in PALETTE]
models=[]
def model(id,name):return {'id':id,'name':name,'voxels':{}}
def box(m,x0,y0,z0,x1,y1,z1,p):
 for x in range(x0,x1):
  for y in range(y0,y1):
   for z in range(z0,z1):m['voxels'][x,y,z]=p
# Full 12^3 cubes; grass/wood ends use distinct surface cells, no spherical silhouette.
layer={1:0,2:1,3:3,4:4,5:6,6:7,7:8,8:9,9:10,10:11,11:12,12:13,13:14,14:15}
ru=['Камень','Земля','Дёрн','Древесина','Листва','Песок','Вода','Лава','Коренная порода','Базальт','Кристалл','Эфирный камень','Руда','Снег']
for id in range(1,15):
 m=model(id,ru[id-1]);box(m,0,0,0,12,12,12,layer[id])
 if id==3:box(m,0,10,0,12,12,12,2)
 if id==4:box(m,0,11,0,12,12,12,5)
 if id==13:
  m['voxels']={};box(m,0,0,0,12,12,12,14)
 if id==11:
  m['voxels']={};box(m,2,0,2,10,10,10,12);box(m,3,10,3,9,14,9,12);box(m,4,14,4,8,17,8,12)
  box(m,0,0,5,3,7,9,13);box(m,9,0,1,12,9,5,12)
 models.append(m)
m=model(15,'Костёр');box(m,0,0,2,14,3,5,4);box(m,0,0,9,14,3,12,4);box(m,3,2,0,6,5,14,4);box(m,9,2,0,12,5,14,4)
for x,z,h in [(5,5,8),(7,6,14),(9,8,10),(5,9,11)]:
 box(m,x,4,z,x+2,h,z+2,32);box(m,x,5,z,x+1,max(6,h-2),z+1,36)
models.append(m)
# Tools have 3D thickness, inset grip bands, dark stone edge, visibly stepped cutting profile.
for id,name in [(100,'Деревянная кирка'),(101,'Каменная кирка'),(102,'Каменный топор'),(103,'Каменный меч')]:
 m=model(id,name);box(m,8,0,3,11,20 if id!=103 else 10,6,17)
 for y in [2,6,10,14]:box(m,8,y,2,11,y+1,7,18)
 if id in [100,101]:
  edge=18 if id==100 else 20;face=16 if id==100 else 19
  box(m,2,19,2,18,23,7,edge);box(m,3,20,2,17,22,7,face)
  box(m,0,16,2,3,21,7,edge);box(m,17,16,2,20,21,7,edge)
  box(m,-1,14,3,1,18,6,face);box(m,19,14,3,21,18,6,face)
 elif id==102:
  box(m,4,17,1,12,24,8,20);box(m,2,18,1,8,25,8,19);box(m,0,19,2,3,24,7,19);box(m,8,20,0,11,22,9,24)
 else:
  box(m,4,8,1,15,11,8,18);box(m,5,9,1,14,10,8,24)
  box(m,7,11,2,12,26,7,20);box(m,8,11,1,11,27,8,19);box(m,8,27,2,11,29,7,19);box(m,9,29,3,10,31,6,19)
  box(m,8,0,2,11,2,7,24)
 # Square rivets and chipped stone edges add authored structure, not smooth bevels.
 if id in [100,101,102]:
  box(m,9,20,7,10,21,8,24)
  for cell in [(2,22,2),(17,22,6),(0,20,2),(20,17,5)]:m['voxels'].pop(cell,None)
 models.append(m)
m=model(104,'Тёплая накидка');box(m,3,0,1,17,15,5,23);box(m,4,1,4,16,16,7,22);box(m,5,16,2,15,21,7,23);box(m,7,17,5,13,20,8,18)
for x in [4,7,10,13,16]:box(m,x,0,4,x+1,2,7,30)
box(m,9,13,7,11,15,8,24);models.append(m)
m=model(110,'Лесные ягоды')
for x,y,z,p in [(0,1,1,25),(5,0,3,26),(3,5,0,25),(7,5,4,27),(1,6,6,26)]:
 box(m,x,y,z,x+5,y+5,z+5,p);box(m,x+1,y+4,z+1,x+3,y+5,z+3,26)
box(m,5,9,3,7,14,5,18);box(m,0,12,3,6,14,7,28);box(m,7,11,2,12,13,6,29);models.append(m)
m=model(111,'Походный хлеб');box(m,0,0,0,19,5,9,34);box(m,1,5,0,18,8,9,33);box(m,2,8,1,17,10,8,33)
for x in [4,9,14]:box(m,x,9,1,x+1,10,8,21)
box(m,18,1,1,19,6,8,35)
for y,z in [(2,2),(4,5),(1,6),(4,3)]:box(m,18,y,z,19,y+1,z+1,34)
models.append(m)
m=model(120,'Доски');
for y in [0,4,8]:box(m,0,y,0,18,y+3,8,16);box(m,0,y+1,0,18,y+2,1,17)
for x in [3,14]:box(m,x,0,-1,x+2,11,9,18)
models.append(m)
m=model(121,'Палки');box(m,0,0,0,3,20,3,17);box(m,4,0,2,7,18,5,16);box(m,2,0,6,5,19,9,17)
for y in [6,12]:box(m,0,y,-1,7,y+2,10,18)
models.append(m)
m=model(122,'Волокно')
for x in range(0,12,2):box(m,x,0,x%3,x+1,17+(x%4),6+x%3,30)
for y in [4,10]:box(m,-1,y,-1,13,y+2,9,31)
models.append(m)
DIRECTIONS=[(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]
CORNERS=[[(1,0,0),(1,1,0),(1,1,1),(1,0,1)],[(0,0,1),(0,1,1),(0,1,0),(0,0,0)],[(0,1,1),(1,1,1),(1,1,0),(0,1,0)],[(0,0,0),(1,0,0),(1,0,1),(0,0,1)],[(1,0,1),(1,1,1),(0,1,1),(0,0,1)],[(0,0,0),(0,1,0),(1,1,0),(1,0,0)]]
# New survival stations and ingredients, exported and rendered by the SAME mesh pipeline.
for id,name,p in [(16,'Верстак',39),(17,'Печь',40)]:
 m=model(id,name);box(m,0,0,0,12,12,12,p)
 box(m,0,11,0,12,12,12,38 if id==16 else 42)
 if id==17:box(m,0,0,0,12,11,1,41)
 models.append(m)
m=model(112,'Печёные ягоды')
for px,py,pz,p in [(0,1,1,27),(5,0,3,25),(3,5,0,27),(7,5,4,25),(1,6,6,27)]:box(m,px,py,pz,px+5,py+5,pz+5,p)
box(m,0,0,0,13,1,12,16);models.append(m)
m=model(113,'Дикое зерно')
for px,pz,ht in [(1,2,18),(5,5,22),(9,3,19),(3,8,16)]:
 box(m,px,0,pz,px+1,ht,pz+1,31)
 for yy in range(ht-9,ht,3):box(m,px-1,yy,pz-1,px+3,yy+2,pz+2,21)
box(m,1,4,1,11,6,10,18);models.append(m)
m=model(114,'Тесто');box(m,0,0,0,16,4,10,35);box(m,1,4,1,15,6,9,21);box(m,3,6,2,13,8,8,35);models.append(m)
m=model(123,'Древесный уголь');box(m,1,0,1,13,6,11,10);box(m,2,6,2,12,10,10,11);box(m,3,10,3,10,12,8,10)
for xx in [3,7,10]:box(m,xx,2,0,xx+1,6,1,20)
models.append(m)
# Equipment shares the very same authored catalogue/icon/held-object pipeline.
for id,name in [(105,'Рудный шлем'),(106,'Рудный нагрудник'),(107,'Рудные поножи'),(108,'Рудные сапоги'),(130,'Тканевый капюшон'),(131,'Походные штаны'),(132,'Кожаные ботинки')]:
 m=model(id,name);armor=id<110;p=19 if armor else 22;edge=20 if armor else 23
 if id in [105,130]:
  box(m,0,11,0,16,15,14,p);box(m,0,0,0,3,12,14,p);box(m,13,0,0,16,12,14,p);box(m,3,0,0,13,12,3,edge)
  for xx in [2,13]:box(m,xx,8,14,xx+1,10,15,24)
 elif id==106:
  box(m,3,0,1,17,16,6,p);box(m,0,13,0,6,19,7,edge);box(m,14,13,0,20,19,7,edge);box(m,5,2,6,15,14,8,p)
  for xx in [5,14]:box(m,xx,3,8,xx+1,14,9,24)
 elif id in [107,131]:
  box(m,0,12,0,16,17,6,edge);box(m,0,0,0,7,14,6,p);box(m,9,0,0,16,14,6,p);box(m,0,0,6,7,3,7,edge);box(m,9,0,6,16,3,7,edge)
 else:
  for xx in [0,10]:box(m,xx,0,0,xx+8,4,13,edge);box(m,xx+1,4,0,xx+7,13,8,p);box(m,xx,1,1,xx+8,4,14,p)
 models.append(m)
def faces(m):
 cells=m['voxels']
 for (x,y,z),p in cells.items():
  for side,(dx,dy,dz) in enumerate(DIRECTIONS):
   if (x+dx,y+dy,z+dz) not in cells:
    yield np.array(CORNERS[side],float)+[x,y,z],side,p
# Orthographic z-buffer renderer, transparent output, actual textured cube faces (not flattened illustrations).
def render(m,S=768):
 camera=np.array([1.4,1.05,-1.8 if m["id"]==17 else 1.8]);camera/=np.linalg.norm(camera);right=np.cross([0,1,0],camera);right/=np.linalg.norm(right);up=np.cross(camera,right)
 fs=list(faces(m));pts=np.concatenate([f[0] for f in fs]);xy=np.stack([pts@right,pts@up],axis=1);lo=xy.min(0);hi=xy.max(0);scale=(S*.82)/max(hi-lo);mid=(lo+hi)/2
 rgba=np.zeros((S,S,4),dtype=np.uint8);depth=np.full((S,S),-1e6,dtype=float)
 light=np.array([-.4,.85,.6]);light/=np.linalg.norm(light)
 for verts,side,p in fs:
  n=np.array(DIRECTIONS[side],float)
  if np.dot(n,camera)<=0:continue
  screen=np.stack([(verts@right-mid[0])*scale+S/2,S/2-(verts@up-mid[1])*scale],axis=1);z=verts@camera
  for inds in [(0,1,2),(0,2,3)]:
   ids=np.array(inds);q=screen[ids];zz=z[ids];world=verts[ids]
   x0=max(0,int(q[:,0].min()));x1=min(S,int(q[:,0].max())+1);y0=max(0,int(q[:,1].min()));y1=min(S,int(q[:,1].max())+1)
   if x1<=x0 or y1<=y0:continue
   yy,xx=np.mgrid[y0:y1,x0:x1];px=xx+.5;py=yy+.5
   den=(q[1,1]-q[2,1])*(q[0,0]-q[2,0])+(q[2,0]-q[1,0])*(q[0,1]-q[2,1])
   if abs(den)<1e-8:continue
   a=((q[1,1]-q[2,1])*(px-q[2,0])+(q[2,0]-q[1,0])*(py-q[2,1]))/den;b=((q[2,1]-q[0,1])*(px-q[2,0])+(q[0,0]-q[2,0])*(py-q[2,1]))/den;c=1-a-b
   d=a*zz[0]+b*zz[1]+c*zz[2];vis=(a>=-1e-6)&(b>=-1e-6)&(c>=-1e-6)&(d>depth[y0:y1,x0:x1])
   if not vis.any():continue
   pos=a[:,:,None]*world[0]+b[:,:,None]*world[1]+c[:,:,None]*world[2]
   uv=pos[:,:,[2,1]] if side<2 else pos[:,:,[0,2]] if side<4 else pos[:,:,[0,1]]
   material_layer=([18,18,17,16,18,18] if m["id"]==16 else [19,19,21,21,19,20])[side] if m["id"] in [16,17] else PALETTE[p][1]
   tex=TEXTURES[material_layer];ij=np.floor(uv*256/12).astype(int)%256;albedo=tex[255-ij[:,:,1],ij[:,:,0]]*COLORS[p]
   illumination=.43+.65*max(0,np.dot(n,light));shade=albedo*illumination
   # Restrained microfacet-ish highlights for crystals/ore, not a baked bloom cloud.
   if p in [12,13,14,24]:shade+=max(0,np.dot(n,(camera+light)/np.linalg.norm(camera+light)))**36*.12
   if p in [32,36]:shade=np.maximum(shade,COLORS[p]*.95)
   pixels=np.uint8(np.clip(shade,0,1)*255);patch=rgba[y0:y1,x0:x1];patch[vis,:3]=pixels[vis];patch[vis,3]=255;depth[y0:y1,x0:x1][vis]=d[vis]
 return Image.fromarray(rgba).resize((384,384),Image.Resampling.LANCZOS)
serial=[]
for m in models:
 render(m).save(OUT/f'Item{m["id"]}.png',optimize=True)
 data=[]
 for (x,y,z),p in sorted(m['voxels'].items()):data.extend([x,y,z,p])
 serial.append({'id':m['id'],'name':m['name'],'cells':data})
 print(m['id'],m['name'],len(m['voxels']),flush=True)
(MODELS/'VoxelItems.json').write_text(json.dumps({'version':1,'colors':[c for c,l in PALETTE],'layers':[l for c,l in PALETTE],'models':serial},ensure_ascii=False,separators=(',',':')))
# A downloadable, engine-neutral representation of these exact models.
path=ROOT.parent/'game-assets';path.mkdir(exist_ok=True)
with zipfile.ZipFile(path/'LestingCraft-voxel-objects.zip','w',zipfile.ZIP_DEFLATED) as archive:
 for m in models:
  lines=['# LestingCraft original voxel object, same source as Unity and icon.'];index=1
  for verts,side,p in faces(m):
   for v in verts:
    lines.append('v '+' '.join(str(round(float(a)/16,5)) for a in v))
    u,w=(v[2],v[1]) if side<2 else (v[0],v[2]) if side<4 else (v[0],v[1])
    lines.append(f'vt {u/12:.6f} {w/12:.6f}')
   lines.append(f'usemtl palette{p}');lines.append('f '+' '.join(f'{index+k}/{index+k}' for k in range(4)));index+=4
  lines.insert(0,'mtllib palette.mtl');archive.writestr(f'{m["id"]}.obj','\n'.join(lines))
 archive.writestr('palette.mtl','\n'.join(f'newmtl palette{i}\nKd '+ ' '.join(str(round(float(v),4)) for v in COLORS[i])+'\nmap_Kd textures/'+NAMES[PALETTE[i][1]]+'.png\n' for i in range(len(PALETTE))))
 for i,name in enumerate(NAMES):
  stream=io.BytesIO();Image.fromarray(np.uint8(TEXTURES[i]*255)).save(stream,format='PNG');archive.writestr('textures/'+name+'.png',stream.getvalue())
 archive.writestr('README.txt',str(len(models))+' original voxel objects. OBJ/MTL export includes UVs and 256px albedo textures. Unity uses the same voxel coordinates plus full PBR normal/mask layers. Icons are textured orthographic renders of these objects. No spherical geometry.\n')
print('Generated',len(models),'textured voxel icons and shared model catalog')
