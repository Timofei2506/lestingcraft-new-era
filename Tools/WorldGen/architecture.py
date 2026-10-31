"""Authored content-11 voxel architecture. Pure data, metres/blocks, inclusive boxes.
The frozen <=10 blueprints remain separate. All doors have >=2x3 clearance.
"""
from math import sin,cos,pi
PLANS=[]
STONE,BRICK,TIMBER,PLANK,ROOF,GLASS,CARVED=37,36,4,18,59,44,58
class Plan:
 def __init__(self,name,radius):self.name=name;self.radius=radius;self.boxes=[];self.entrances=[];PLANS.append(self)
 def b(self,x0,x1,y0,y1,z0,z1,id):
  assert x0<=x1 and y0<=y1 and z0<=z1
  self.boxes.append([x0,x1,y0,y1,z0,z1,id])
 def rim(self,x0,x1,z0,z1,y,id=STONE):
  self.b(x0,x1,y,y,z0,z0,id);self.b(x0,x1,y,y,z1,z1,id)
  self.b(x0,x0,y,y,z0+1,z1-1,id);self.b(x1,x1,y,y,z0+1,z1-1,id)
 def floor(self,r,id=BRICK):self.b(-r,r,0,0,-r,r,id)
 def door(self,x,z,y=1,width=2):
  self.b(x,x+width-1,y,y+2,z,z,0);self.entrances.append([x,z,y,width])
 def lamp(self,x,z,y=1):
  self.b(x,x,y,y+2,z,z,TIMBER);self.b(x-1,x+1,y+3,y+3,z,z,CARVED);self.b(x,x,y+4,y+4,z,z,56)
 def house(self,cx,cz,w=4,d=4,base=0,ruined=False,stories=1):
  b=self.b;Y=base;wall=4+5*(stories-1)
  b(cx-w,cx+w,Y,Y,cz-d,cz+d,BRICK)
  b(cx-w,cx+w,Y+1,Y+wall,cz-d,cz+d,PLANK)
  b(cx-w+1,cx+w-1,Y+1,Y+wall+1,cz-d+1,cz+d-1,0)
  for x in [cx-w,cx+w]:
   for z in [cz-d,cz+d]:b(x,x,Y+1,Y+wall+1,z,z,TIMBER)
  for level in range(stories):
   for yy in [Y+1+level*5,Y+4+level*5]:self.rim(cx-w,cx+w,cz-d,cz+d,yy,CARVED)
   for x in [cx-w+2,cx+w-2]:b(x,x,Y+2+level*5,Y+3+level*5,cz+d,cz+d,GLASS)
   for z in [cz-d+2,cz+d-2]:
    for x in [cx-w,cx+w]:b(x,x,Y+2+level*5,Y+3+level*5,z,z,GLASS)
  for k in range(w+2):b(cx-w-1+k,cx+w+1-k,Y+wall+1+k,Y+wall+1+k,cz-d-1,cz+d+1,ROOF)
  b(cx,cx,Y+wall+1+w,Y+wall+2+w,cz-d-1,cz+d+1,CARVED)
  # Functional hearth/chimney is against the rear wall, never on the entry route.
  b(cx+w-2,cx+w-1,Y+1,Y+wall+4,cz+d-1,cz+d,STONE)
  b(cx+w-2,cx+w-2,Y+1,Y+2,cz+d-2,cz+d-2,17)
  b(cx-w+1,cx-w+1,Y+1,Y+1,cz+d-2,cz+d-1,55)
  b(cx-w+1,cx-w+2,Y+1,Y+2,cz-d+1,cz-d+1,57)
  b(cx+w-1,cx+w-1,Y+1,Y+1,cz-d+1,cz-d+1,16)
  b(cx,cx,Y+4,Y+4,cz,cz,56)
  self.door(cx-1,cz-d,Y+1)
  b(cx-2,cx+1,Y,Y,cz-d-2,cz-d-1,STONE)
  if ruined:
   b(cx+1,cx+w+1,Y+4,Y+w+6,cz+1,cz+d+1,0)
   b(cx+w,cx+w,Y+2,Y+4,cz,cz+d-1,0)
   b(cx+w+1,cx+w+2,Y+1,Y+1,cz+d,cz+d+1,38)
 def tower(self,cx,cz,r=3,h=12,ruined=False):
  b=self.b
  b(cx-r,cx+r,0,h,cz-r,cz+r,STONE);b(cx-r+1,cx+r-1,1,h+1,cz-r+1,cz+r-1,0)
  for yy in [0,5,h]:self.rim(cx-r,cx+r,cz-r,cz+r,yy,BRICK)
  # Enclosed clockwise staircase, 2x2 central void, 3 blocks headroom.
  route=[]
  for x in range(cx-r+1,cx+r):route.append((x,cz-r+1))
  for z in range(cz-r+2,cz+r):route.append((cx+r-1,z))
  for x in range(cx+r-2,cx-r,-1):route.append((x,cz+r-1))
  for z in range(cz+r-2,cz-r+1,-1):route.append((cx-r+1,z))
  for i in range(h):
   x,z=route[i%len(route)];b(x,x,i,i,z,z,PLANK)
  for x in range(cx-r,cx+r+1,2):
   for z in [cz-r,cz+r]:b(x,x,h+1,h+2,z,z,STONE)
  for z in range(cz-r+2,cz+r,2):
   for x in [cx-r,cx+r]:b(x,x,h+1,h+2,z,z,STONE)
  for yy in range(3,h-1,4):
   for x in [cx-r,cx+r]:b(x,x,yy,yy+1,cz,cz,0)
  self.door(cx-1,cz-r,1)
  if ruined:b(cx,cx+r,h-3,h+2,cz,cz+r,0)

p=Plan('Деревня с колодцем',27);p.floor(26,3)
p.b(-2,2,0,0,-26,26,32);p.b(-26,26,0,0,-2,2,32)
for x,z in [(-15,-15),(14,-15),(-15,14),(14,14)]:p.house(x,z)
p.b(-3,3,0,1,-3,3,BRICK);p.b(-1,1,1,1,-1,1,7)
for x in [-3,3]:
 for z in [-3,3]:p.b(x,x,2,5,z,z,TIMBER)
for k in range(5):p.b(-4+k,4-k,6+k,6+k,-4,4,ROOF)
for x,z in [(-7,0),(7,0),(0,-8),(0,8)]:p.lamp(x,z)
p.b(8,23,0,0,-6,-4,2);p.b(9,22,1,1,-5,-5,33)

p=Plan('Пограничный замок',26);p.floor(24)
# Curtain walls with walkable wall tops, buttresses, crenels and a vaulted gatehouse.
for z in [-22,22]:p.b(-22,22,1,7,z,z,STONE)
for x in [-22,22]:p.b(x,x,1,7,-22,22,STONE)
for yy in [1,7]:p.rim(-23,23,-23,23,yy,BRICK)
for k in range(-16,17,4):
 for z in [-22,22]:p.b(k,k+1,8,9,z,z,STONE)
 for x in [-22,22]:p.b(x,x,8,9,k,k+1,STONE)
for x,z in [(-20,-20),(-20,20),(20,-20),(20,20)]:p.tower(x,z,3,13)
# Keep is hollow, with windows, gallery, a real stair and furniture.
p.house(0,7,7,6,stories=2);p.b(-6,6,5,5,2,12,PLANK);p.b(-1,1,5,5,2,5,0)
p.b(-5,-4,5,5,3,6,0)
for i in range(5):p.b(-5,-4,i+1,i+1,3+i,3+i,PLANK)
for x in [-5,5]:p.b(x,x,6,7,12,12,57)
p.b(-4,4,0,0,-24,-18,STONE);p.b(-5,5,1,8,-23,-20,STONE)
p.b(-2,2,1,4,-24,-19,0);p.b(-1,1,5,5,-23,-20,0)
p.entrances.append([-2,-22,1,5]);p.lamp(-6,-18);p.lamp(6,-18)

p=Plan('Разрушенная усадьба',17);p.floor(16,35);p.house(-3,0,6,5,ruined=True)
for z in [-12,12]:p.b(-14,14,1,1,z,z,38)
p.b(-2,1,1,1,-12,-12,0);p.door(-2,-5)
p.b(8,12,0,0,5,9,38);p.b(9,11,1,4,6,8,38);p.b(10,10,1,4,7,7,0)
p.b(8,12,5,5,5,9,38);p.b(10,12,3,5,7,9,0)

p=Plan('Сломанная дозорная башня',14);p.floor(12,35);p.tower(0,0,5,16,True)
p.b(-2,1,0,0,-12,-6,32);p.lamp(-7,-7)
p.b(7,9,1,2,4,6,38);p.b(-10,-8,1,1,5,8,38)

p=Plan('Заброшенный лагерь',13);p.floor(12,32)
# Open lean-to shelters: pitched roofs on articulated timber frames.
for cx,cz in [(-6,-5),(6,5)]:
 p.b(cx-4,cx+4,0,0,cz-3,cz+3,PLANK)
 for x in [cx-3,cx+3]:
  for z in [cz-2,cz+2]:p.b(x,x,1,3,z,z,TIMBER)
 for k in range(5):p.b(cx-4+k,cx+4-k,4+k,4+k,cz-3,cz+3,ROOF)
 p.b(cx-3,cx-3,1,1,cz,cz+1,55);p.b(cx+2,cx+2,1,1,cz+2,cz+2,16)
p.b(-1,1,0,0,-1,1,38);p.b(0,0,1,1,0,0,15)
for x in [-4,4]:p.b(x,x,1,1,-1,2,PLANK)

p=Plan('Обелиск стража',22);p.floor(21,35)
for r,y in [(18,1),(14,2),(10,3)]:p.b(-r,r,y,y,-r,r,STONE)
for r,y in [(18,2),(14,3),(10,4)]:p.rim(-r,r,-r,r,y,BRICK)
# South processional stairs, 3-block clear path to the raised dais.
for i in range(4):p.b(-2,2,i,i,-21+i,-21+i,STONE)
p.b(-2,2,1,3,-18,-10,0);p.b(-2,2,3,3,-18,-10,STONE)
for x,z in [(-15,-15),(15,-15),(-15,15),(15,15)]:
 p.b(x-1,x+1,2,2,z-1,z+1,BRICK);p.b(x,x,3,10,z,z,38);p.b(x-1,x+1,11,11,z-1,z+1,CARVED);p.b(x,x,12,12,z,z,56)
for w,y0,y1 in [(3,4,4),(2,5,12),(1,13,16)]:p.b(-w,w,y0,y1,-w,w,40)
p.b(0,0,6,14,-2,-2,11)

p=Plan('Шахтёрский лагерь',15);p.floor(14,32);p.house(-7,2,4,4)
# Braced adit portal; stairs lead below the platform instead of ending at a wall.
p.b(2,12,0,6,-8,-4,STONE);p.b(5,9,1,4,-9,-3,0)
for x in [4,10]:p.b(x,x,1,5,-8,-8,TIMBER)
p.b(4,10,5,5,-8,-8,TIMBER)
for i in range(6):
 p.b(6,8,-i,-i,-7+i,-7+i,PLANK);p.b(6,8,1-i,3-i,-7+i,-7+i,0)
p.b(7,7,1,1,-14,-9,85);p.lamp(3,-10)
p.b(10,11,1,2,4,5,PLANK);p.b(4,5,1,1,7,8,17)

p=Plan('Каменный круг',16);p.floor(15,35)
for i in range(8):
 a=i*pi/4;x=round(cos(a)*11);z=round(sin(a)*11)
 p.b(x-1,x+1,1,1,z-1,z+1,STONE);p.b(x,x,2,7+i%3,z,z,38);p.b(x-1,x+1,8+i%3,8+i%3,z,z,STONE)
p.b(-3,3,1,1,-3,3,BRICK);p.b(-1,1,2,2,-1,1,40);p.b(0,0,3,3,0,0,11)
p.b(-2,2,0,0,-15,-4,32)

p=Plan('Руины библиотеки',18);p.floor(17,35)
p.b(-12,12,0,0,-12,12,BRICK)
for z in [-12,12]:p.b(-12,12,1,7,z,z,STONE)
for x in [-12,12]:p.b(x,x,1,7,-12,12,STONE)
for x in [-12,-6,0,6,12]:
 for z in [-12,12]:p.b(x,x,1,9,z,z,BRICK)
for x in [-8,-4,4,8]:
 p.b(x-1,x+1,3,6,-12,-12,GLASS);p.b(x-1,x+1,3,6,12,12,GLASS)
p.door(-1,-12);p.b(-2,2,4,5,-12,-12,0)
p.b(-12,12,8,8,-12,12,PLANK);p.b(-5,8,8,8,-5,8,0)
for x in [-9,-5,5,9]:
 for z in [-8,3]:p.b(x,x+1,1,3,z,z+5,57)
for x in [-2,2]:p.b(x,x,1,1,-2,4,PLANK)
for z in [-5,7]:p.b(-3,3,1,1,z,z,PLANK)
p.b(6,12,4,9,6,12,0);p.b(10,14,1,1,9,14,38)
p.lamp(-14,-10);p.lamp(14,-10)

p=Plan('Караванный пост',17);p.floor(16,32);p.house(0,7,6,5)
# Covered arcade and two distinct market bays.
for x in [-13,-7,7,13]:
 for z in [-10,-2]:p.b(x,x,1,4,z,z,TIMBER)
for x0,x1 in [(-14,-6),(6,14)]:
 p.b(x0,x1,5,5,-11,-1,ROOF);p.b(x0+1,x1-1,1,1,-3,-3,PLANK)
 p.b(x0+2,x0+3,2,2,-3,-3,16);p.b(x1-2,x1-1,2,2,-3,-3,57)
p.b(-2,2,0,0,-16,1,STONE);p.lamp(-4,-13);p.lamp(4,-13)
# Loading platform with open access at both ends.
p.b(-12,-7,1,1,9,13,PLANK);p.b(-12,-12,2,2,9,13,CARVED)

for p in PLANS:
 for x0,x1,y0,y1,z0,z1,id in p.boxes:
  assert max(abs(x0),abs(x1),abs(z0),abs(z1))<=p.radius,(p.name,'outside radius')
  assert -8<=y0<=y1<=48 and 0<=id<=89
