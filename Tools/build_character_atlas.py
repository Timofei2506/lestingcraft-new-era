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
  closed = (i==11)
  d.rectangle((12,8,115,27),fill=(69,48,34));d.rectangle((20,26,107,33),fill=(89,58,39))
  for k in range(9):d.point((16+k*11,28+((k*7)%5)),fill=(58,40,28))
  for x0 in [29,79]:
   if closed:
    d.rectangle((x0+1,50,x0+22,53),fill=(150,96,66))          # relaxed lid
    d.rectangle((x0+1,58,x0+22,61),fill=(74,46,32))          # lash line
    d.rectangle((x0+2,44,x0+21,47),fill=(83,52,36))          # calm brow
   else:
    d.rectangle((x0,44,x0+24,50),fill=(73,45,32));d.point((x0+22,49),fill=(73,45,32));d.point((x0+1,49),fill=(73,45,32))
    d.rectangle((x0+1,53,x0+22,56),fill=(176,124,92))        # socket shade
    d.rectangle((x0+1,55,x0+22,66),fill=(214,208,178))       # white
    d.rectangle((x0+9,55,x0+17,66),fill=(64,74,52))          # iris
    d.rectangle((x0+11,57,x0+14,63),fill=(24,26,22))         # pupil
    d.rectangle((x0+10,56,x0+12,58),fill=(235,240,228))      # catch light
    d.rectangle((x0+1,66,x0+22,67),fill=(150,96,66))         # lower lid
  d.rectangle((56,62,72,84),fill=(147,88,58));d.rectangle((58,64,66,80),fill=(179,120,80))
  d.rectangle((58,80,66,84),fill=(120,70,46))
  if closed:
   d.rectangle((44,96,84,102),fill=(96,60,42));d.rectangle((50,96,78,100),fill=(132,78,52))
  else:
   d.rectangle((37,93,93,113),fill=(91,58,41));d.rectangle((49,93,80,99),fill=(128,73,49))
   d.rectangle((37,108,93,113),fill=(74,46,32))              # lower lip shade
  for cx0,cy0 in [(24,78),(104,78)]:
   for a in range(3):
    for b in range(2):d.point((cx0-a+ b,cy0+a),fill=(158,102,72))   # cheek tone
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
