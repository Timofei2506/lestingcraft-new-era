"""Original detailed block-character texture atlas, 16 x 128px tiles. Not a Minecraft skin."""
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
R=Path(__file__).resolve().parents[1];O=R/'Assets/Art/Characters';O.mkdir(parents=True,exist_ok=True)
rng=np.random.default_rng(2506);size=128
base=[(166,112,77),(69,86,62),(41,40,34),(166,112,77),(48,40,32),(184,139,66),(96,72,43),(71,64,49)]
atlas=Image.new('RGB',(512,512))
for i in range(16):
 c=np.array(base[i%8],float);y,x=np.mgrid[0:size,0:size]
 # Pixel-resolved pores/grain, woven threads, patch wear; square geometry remains dominant.
 noise=rng.normal(0,2.8,(size,size,1));weave=((x%4==0)*2-(y%4==0)*2)[:,:,None]
 arr=np.uint8(np.clip(c+noise+weave,0,255));im=Image.fromarray(arr);d=ImageDraw.Draw(im)
 if i%8 in [1,2,4,6,7]:
  for a in [7,120]:
   d.line((a,0,a,127),fill=tuple(int(v*.65) for v in c),width=2)
   for b in range(3,128,8):d.rectangle((a+2,b,a+3,b+3),fill=tuple(min(255,int(v*1.40)) for v in c))
  for j in range(45):
   px,py=map(int,rng.integers(12,117,2));d.rectangle((px,py,px+int(rng.integers(1,5)),py+1),fill=tuple(int(v*.86) for v in c))
 if i%8==3:
  d.rectangle((12,8,115,27),fill=(69,48,34));d.rectangle((20,26,107,33),fill=(89,58,39))
  for x0 in [29,79]:
   d.rectangle((x0,45,x0+24,51),fill=(73,45,32));d.rectangle((x0+1,54,x0+22,65),fill=(209,203,171));d.rectangle((x0+9,54,x0+17,65),fill=(46,51,41));d.rectangle((x0+10,54,x0+12,57),fill=(210,219,200))
  d.rectangle((56,62,72,84),fill=(147,88,58));d.rectangle((58,64,66,80),fill=(179,120,80))
  d.rectangle((37,93,93,113),fill=(91,58,41));d.rectangle((49,93,80,99),fill=(128,73,49))
 if i%8==5:
  d.rectangle((12,12,116,116),outline=(86,63,29),width=8);d.rectangle((26,27,99,99),outline=(230,188,94),width=4)
 if i in [8,9]:
  arr=np.uint8(np.clip(np.array([113,121,126])+rng.normal(0,3,(size,size,1))+(x[:,:,None]/size)*9,0,255));im=Image.fromarray(arr);d=ImageDraw.Draw(im)
  d.rectangle((4,4,123,123),outline=(48,54,58),width=4);d.rectangle((9,9,118,118),outline=(171,179,182),width=2)
  for a,b in [(15,15),(112,15),(15,112),(112,112)]:d.rectangle((a-3,b-3,a+3,b+3),fill=(188,194,192));d.point((a+1,b+2),fill=(53,59,63))
  for j in range(24):
   px,py=map(int,rng.integers(19,111,2));d.line((px,py,px+int(rng.integers(3,11)),py+1),fill=(84,90,95),width=1)
 atlas.paste(im,((i%4)*128,(i//4)*128))
atlas.save(O/'BlockCharacter.png',optimize=True)
print('Character atlas: 512x512, 128px face/skin/fabric/leather/buckle tiles')
