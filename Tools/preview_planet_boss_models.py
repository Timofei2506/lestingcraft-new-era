"""Render exposed faces of the EXACT three Unity PlanetBoss*.json meshes.
Software vertex-tint preview; no fake AI art and NOT a Unity shader screenshot.
"""
import json,math
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont,ImageFilter
R=Path(__file__).resolve().parents[1]
OUT=R.parent.parent/'work/boss-models'
OUT.mkdir(exist_ok=True,parents=True)
TITLES={'Mars':'КУЗНЕЦ РАЗЛОМА · МАРС','Venus':'КИСЛОТНАЯ ВЛАДЫЧИЦА · ВЕНЕРА','Mercury':'СОЛНЕЧНЫЙ СКАРАБЕЙ · МЕРКУРИЙ'}
BACK={'Mars':(34,18,20),'Venus':(33,33,16),'Mercury':(14,20,30)}
try:FONT=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',19)
except OSError:FONT=ImageFont.load_default()

def preview(slug):
 d=json.loads((R/f'Assets/Resources/Models/PlanetBoss{slug}.json').read_text(encoding='utf8'))
 occ={}
 for part in d['parts']:
  a=part['cells'];px,py,pz=part['pivot']
  for i in range(0,len(a),4):occ[a[i]+px,a[i+1]+py,a[i+2]+pz]=a[i+3]
 palette=d['palette'];size=1300
 base=BACK[slug]
 im=Image.new('RGB',(size,size));pixels=im.load()
 for y in range(size):
  c=tuple(max(4,v+int((y/size-.4)*5)) for v in base)
  for x in range(size):pixels[x,y]=c
 dr=ImageDraw.Draw(im)
 if slug=='Mars':dr.ellipse((185,1080,1135,1230),fill=(52,25,23))
 if slug=='Venus':dr.ellipse((115,1080,1175,1235),fill=(53,51,24))
 if slug=='Mercury':dr.ellipse((180,1090,1100,1250),fill=(32,39,43))
 angle=math.radians(30)
 projected=[((x-z)*math.cos(angle),(x+z)*math.sin(angle)-y) for x,y,z in occ]
 minx=min(t[0] for t in projected);maxx=max(t[0] for t in projected)
 miny=min(t[1] for t in projected);maxy=max(t[1] for t in projected)
 S=min(1100/max(.01,maxx-minx),900/max(.01,maxy-miny),18)
 offsetx=size*.5-(minx+maxx)*S*.5;offsety=655-(miny+maxy)*S*.5
 def p(x,y,z):return (offsetx+(x-z)*math.cos(angle)*S,offsety+((x+z)*math.sin(angle)-y)*S)
 def paint(color,f):return tuple(max(0,min(255,int((v**.92)*255*f))) for v in color)
 faces=0
 for x,y,z in sorted(occ,key=lambda q:q[0]+q[1]+q[2]):
  mat=palette[occ[(x,y,z)]];tint=mat['tint'];glow=mat['emission']
  # Conservative brightness and emission; Unity PBR has additional details absent from this preview.
  def face(points,light):
   nonlocal faces
   dr.polygon([p(*v) for v in points],fill=paint(tint,light+min(.32,glow*.08)));faces+=1
  if (x+1,y,z) not in occ:face(((x+1,y,z),(x+1,y+1,z),(x+1,y+1,z+1),(x+1,y,z+1)),.67)
  if (y+1==0 or (x,y+1,z) not in occ):face(((x,y+1,z),(x+1,y+1,z),(x+1,y+1,z+1),(x,y+1,z+1)),1.05)
  if (x,y,z+1) not in occ:face(((x,y,z+1),(x+1,y,z+1),(x+1,y+1,z+1),(x,y+1,z+1)),.83)
 dr.text((42,36),TITLES[slug],font=FONT,fill=(236,239,228))
 dr.text((43,67),'ИГРОВАЯ ВОКСЕЛЬНАЯ ГЕОМЕТРИЯ / РЕНДЕР-ЧЕРНОВИК',font=FONT,fill=(131,159,171))
 dr.text((43,1239),f"{len(d['parts'])} АНИМИРУЕМЫХ ЧАСТЕЙ  ·  {len(occ)} ВОКСЕЛЕЙ  ·  {faces} ВИДИМЫХ ГРАНЕЙ",font=FONT,fill=(155,177,187))
 target=OUT/(slug.lower()+'_game_mesh.png')
 im.save(target,optimize=True)
 print(slug,'game mesh',len(occ),'voxels',faces,'faces',target)

if __name__=='__main__':
 for slug in TITLES:preview(slug)
