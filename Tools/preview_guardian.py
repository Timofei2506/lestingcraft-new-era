"""Neutral software preview of the EXACT shared RootGuardian.json sculpture. Not Unity/gameplay.
No generated design imagery. Geometry, albedo layers, tints, UV scale and emission come from the model.
Lighting is approximate studio lighting, not Unity URP/shadows/bloom validation.
"""
from pathlib import Path
import json,sys,math
import numpy as np
from PIL import Image,ImageFilter
R=Path(__file__).resolve().parents[1]
data=json.loads((R/'Assets/Resources/Models/RootGuardian.json').read_text());unit=data['unit'];pal=data['palette'];flat=data['cells'];cells={tuple(flat[i:i+3]):flat[i+3] for i in range(0,len(flat),4)}
names=['stone','dirt','grass','grass-side','bark','wood-end','leaves','sand','water','lava','bedrock','basalt','crystal','aether','ore','snow','planks','workbench-top','workbench-side','furnace-side','furnace-front','furnace-top','campfire-side','campfire-top','ore-copper','ore-coal','ore-gold','ore-silver','ore-tin','ore-quartz','ore-sulfur','granite','limestone','slate','marble','obsidian','gravel','clay','mud','mossy-stone']
textures={}
for p in pal:
 name=names[p['layer']]
 if name not in textures:
  f=next(f for f in (R/'Assets/Art/PBR').glob(name+'_albedo.*') if f.suffix!='.meta');textures[name]=(np.asarray(Image.open(f).convert('RGB'),float)/255)**2.2
C=np.array([[[1,0,0],[1,1,0],[1,1,1],[1,0,1]],[[0,0,1],[0,1,1],[0,1,0],[0,0,0]],[[0,1,1],[1,1,1],[1,1,0],[0,1,0]],[[0,0,0],[1,0,0],[1,0,1],[0,0,1]],[[1,0,1],[1,1,1],[0,1,1],[0,0,1]],[[0,0,0],[0,1,0],[1,1,0],[1,0,0]]],float)
N=np.array([[1,0,0],[-1,0,0],[0,1,0],[0,-1,0],[0,0,1],[0,0,-1]],int)
W=H=1200
Y,X=np.mgrid[:H,:W];rad=((X-W*.49)/(W*.8))**2+((Y-H*.38)/(H*.9))**2
rgb=np.ones((H,W,3))*np.array([.12,.14,.16]);rgb*=np.clip(1-rad[:,:,None]*.35,.45,1)
shadow=np.exp(-(((X-W*.5)/(W*.24))**2+((Y-H*.92)/(H*.018))**2)*1.7);rgb*=1-shadow[:,:,None]*.55
depth=np.full((H,W),-np.inf);glow=np.zeros((H,W,3));eye=np.array([5.,5.7,15.]);target=np.array([0,3.40,.12]);view=eye-target;view/=np.linalg.norm(view)
right=np.cross([0.,1,0],view);right/=np.linalg.norm(right);up=np.cross(view,right);light=np.array([-.55,.8,.7]);light/=np.linalg.norm(light);scale=143
faces=0
for k,mat in cells.items():
 p=pal[mat];tex=textures[names[p['layer']]];tint=np.array(p['tint'])**2.2
 for face in range(6):
  n=N[face]
  if tuple(np.array(k)+n) in cells or n@view<=0:continue
  faces+=1;verts=(np.array(k)+C[face])*unit;q=verts-target
  screen=np.column_stack([W*.50+q@right*scale,H*.49-q@up*scale,q@view])
  if face<2:uv=verts[:,[2,1]]*.8
  elif face<4:uv=verts[:,[0,2]]*.8
  else:uv=verts[:,[0,1]]*.8
  # Approximate neighbouring-root shade. It is not represented as a Unity rendering claim.
  occupancy=0
  for jump in [2,4,7]:
   probe=np.array(k)+n*jump
   occupancy+=tuple(probe) in cells
  lightness=.36+max(0,float(n@light))*.95
  lightness*=1-.13*occupancy
  for ids in [[0,1,2],[0,2,3]]:
   pts=screen[ids];tuv=uv[ids];a,b,c=pts
   x0=max(0,int(np.floor(pts[:,0].min())));x1=min(W-1,int(np.ceil(pts[:,0].max())))
   y0=max(0,int(np.floor(pts[:,1].min())));y1=min(H-1,int(np.ceil(pts[:,1].max())))
   if x1<x0 or y1<y0:continue
   yy,xx=np.mgrid[y0:y1+1,x0:x1+1];xx=xx+.5;yy=yy+.5
   den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
   if abs(den)<1e-8:continue
   u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
   v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w=1-u-v
   z=u*a[2]+v*b[2]+w*c[2];sub=depth[y0:y1+1,x0:x1+1]
   valid=(u>=-1e-6)&(v>=-1e-6)&(w>=-1e-6)&(z>sub)
   if not valid.any():continue
   coords=u[:,:,None]*tuv[0]+v[:,:,None]*tuv[1]+w[:,:,None]*tuv[2]
   tx=(np.mod(coords[:,:,0],1)*tex.shape[1]).astype(int)%tex.shape[1]
   ty=((1-np.mod(coords[:,:,1],1))*tex.shape[0]).astype(int)%tex.shape[0]
   albedo=tex[ty,tx]*tint
   # Only the actual heart/eyes glow, not generic magical ornaments added for this preview.
   energy=p['emission']*1.5;toward=np.array([0,3.17,.60])-verts.mean(axis=0);distance=np.linalg.norm(toward);warm=max(0,float(n@toward/max(.01,distance)))*2.7/(1+distance*distance*2);linear=albedo*(lightness+energy+np.array([1,.28,.025])*warm)
   color=(linear/(1+linear*.24))**(1/2.2)
   rgb[y0:y1+1,x0:x1+1][valid]=np.clip(color[valid],0,1);sub[valid]=z[valid]
   glow[y0:y1+1,x0:x1+1][valid]=np.clip(albedo[valid]*energy,0,1)
im=np.uint8(np.clip(rgb,0,1)*255);g=Image.fromarray(np.uint8(glow*255)).filter(ImageFilter.GaussianBlur(9))
im=Image.fromarray(np.uint8(np.clip(im.astype(float)+np.asarray(g,float)*.33,0,255))).resize((1000,1000),Image.Resampling.LANCZOS)
out=Path(sys.argv[1]) if len(sys.argv)>1 else Path('/home/user/Предпросмотр/Страж-корней-v2.jpg');out.parent.mkdir(exist_ok=True,parents=True);im.save(out,quality=93,optimize=True)
print('Software model preview, NOT a Unity screenshot:',len(cells),'voxels;',faces,'camera-facing surface quads;',out.stat().st_size,'bytes;',out)
