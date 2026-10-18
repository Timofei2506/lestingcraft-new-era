"""Author the Root Warden as ONE coherent voxel sculpture, not stacked terrain blocks.
Same cells/palette are consumed by Unity and the honest offline model preview.
No image generation, screenshots, archives or changes to terrain/gameplay.
"""
from pathlib import Path
import math,json
R=Path(__file__).resolve().parents[1]
U=.10
cells={}
palette=[
 {'name':'old outer bark','layer':4,'tint':[.77,.69,.57],'emission':0},
 {'name':'inner root grain','layer':4,'tint':[1,.88,.69],'emission':0},
 {'name':'petrified carved wood','layer':39,'tint':[.96,.88,.69],'emission':0},
 {'name':'moss in bark crevices','layer':39,'tint':[.68,.86,.52],'emission':0},
 {'name':'amber living heart','layer':12,'tint':[.95,.28,.03],'emission':1.2},
 {'name':'ember eye','layer':12,'tint':[1,.64,.15],'emission':2},
 {'name':'charred fissures','layer':4,'tint':[.35,.31,.26],'emission':0},
 {'name':'broken branch end','layer':5,'tint':[.76,.64,.44],'emission':0},
 {'name':'heart hot central facet','layer':12,'tint':[1,.67,.18],'emission':1.9},
 {'name':'heart dark side facet','layer':12,'tint':[.72,.25,.025],'emission':1.3},
]
def brush(p,r,m,flatten=(1,1,1)):
 if r<=0:return
 rx,ry,rz=[r*v for v in flatten]
 for x in range(math.floor((p[0]-rx)/U),math.ceil((p[0]+rx)/U)):
  for y in range(max(0,math.floor((p[1]-ry)/U)),math.ceil((p[1]+ry)/U)):
   for z in range(math.floor((p[2]-rz)/U),math.ceil((p[2]+rz)/U)):
    d=[abs(((a+.5)*U-b)/s) for a,b,s in zip((x,y,z),p,(rx,ry,rz))]
    # Bevelled voxel cross section. No sphere meshes or smooth shading.
    if max(d)<=1 and sum(v*v for v in d)<=1.18:cells[x,y,z]=m

def path(points,radii,m=0):
 # Catmull-Rom paths are sampled into cubes; final silhouette remains voxel geometry.
 for i in range(len(points)-1):
  p0=points[max(0,i-1)];p1=points[i];p2=points[i+1];p3=points[min(len(points)-1,i+2)]
  length=math.dist(p1,p2)
  for j in range(max(2,math.ceil(length/(U*.38)))+1):
   t=j/max(2,math.ceil(length/(U*.38)))
   p=[.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t) for a,b,c,d in zip(p0,p1,p2,p3)]
   brush(p,radii[i]*(1-t)+radii[i+1]*t,m)

def mirrored(points,side):return [(x*side,y,z) for x,y,z in points]
# Weight-bearing toes grow into braided legs; each limb has a readable bend and separated digits.
for side in [-1,1]:
 for j in [-1,0,1]:
  path(mirrored([(.52,2.25,-.08),(.70+j*.12,1.52,-.08),(.65+j*.13,.82,.12),(.69+j*.16,.20,.38)],side),[.22,.24,.21,.16],0 if j else 1)
  path(mirrored([(.69+j*.16,.22,.32),(.72+j*.20,.12,.65),(.79+j*.22,.10,.99-abs(j)*.11)],side),[.19,.16,.055],0)
 path(mirrored([(.58,.31,.12),(.86,.15,-.10),(1.04,.1,-.23)],side),[.18,.14,.045],0)
# A bent spine and pelvic arch; the breast is an OPEN cage, never a solid rectangular trunk.
path([(-.55,2.12,0),(0,2.27,.05),(.55,2.12,0)],[.32,.36,.32],0)
path([(0,2.1,-.13),(-.08,2.8,-.43),(.08,3.62,-.43),(0,4.28,-.10),(0,4.55,0)],[.29,.32,.38,.30,.22],0)
for side in [-1,1]:
 path(mirrored([(.43,2.15,.0),(.85,2.65,.15),(1.02,3.39,.01),(.88,3.94,-.04),(.32,4.26,.0)],side),[.26,.31,.37,.42,.23],0)
 # Ribs sweep around the heart; slots remain open and reveal the ember behind them.
 for j in range(3):
  y=3.74-j*.36
  path(mirrored([(.12,y,-.36),(.68,y+.06,.10),(.83,y-.05,.49),(.53,y-.28,.79),(.44,y-.41,.84)],side),[.16,.19,.18,.13,.07],1 if j==1 else 0)
 path(mirrored([(.56,2.25,.12),(.54,2.56,.67),(.30,2.75,.85)],side),[.26,.19,.07],0)
 # Oversized asymmetrical shoulders grow into the arm instead of ending as block joints.
 path(mirrored([(.75,3.77,-.03),(1.16,4.03,-.03),(1.45,3.62,.07),(1.58,3.19,.16)],side),[.38,.47 if side==-1 else .39,.36,.29],0)
 for strand in range(3):
  angle=strand*math.tau/3
  dx=math.cos(angle)*.16;dz=math.sin(angle)*.15
  path(mirrored([(1.42+dx,3.57,.07+dz),(1.66-dx,2.92,.19-dz),(1.97+dx,2.31,.37+dz),(2.03,1.98,.43)],side),[.19,.21,.24,.23],0 if strand!=1 else 1)
 brush((side*2.04,1.98,.47),.32,0,flatten=(1,1.25,.85))
 for finger in range(3):
  x=1.71+finger*.30
  path(mirrored([(x,1.98,.55),(x+.03,1.57,.76),(x-.04,1.26+abs(finger-1)*.12,.98),(x-.15,1.19+abs(finger-1)*.12,.88)],side),[.12,.105,.075,.025],0)
 path(mirrored([(1.85,2.13,.49),(1.61,1.91,.70),(1.55,1.66,.87)],side),[.16,.12,.03],0)
 # Forked, irregular shoulder branches. Deliberately not a mirrored set of log steps.
 path(mirrored([(1.08,4.0,-.12),(1.43,4.44,-.20),(1.82,4.65,-.26),(2.05,4.90 if side==-1 else 4.71,-.19)],side),[.24,.19,.13,.025],0)
 path(mirrored([(1.42,4.41,-.2),(1.46,4.80,-.40),(1.40,5.04,-.45)],side),[.13,.09,.02],0)
 path(mirrored([(.97,3.94,-.27),(1.38,4.15,-.71),(1.67,4.34,-.84)],side),[.20,.14,.025],0)
# A faceted, suspended heart, inset behind the front ribs (not a white square on the chest).
for x in range(-3,4):
 for y in range(27,37):
  for z in range(-1,7):
   dx=abs((x+.5)*U)/.34;dy=abs((y+.5)*U-3.17)/.53;dz=abs((z+.5)*U-.34)/.32
   if dx+dy*.68+dz*.55<1.08:cells[x,y,z]=8 if -.10<(x+.5)*U<.11 else 4 if x<0 else 9
# Small root tethers visibly connect the suspended heart to the cage.
for side in [-1,1]:path(mirrored([(.22,3.45,.32),(.43,3.64,.19),(.69,3.76,.01)],side),[.065,.08,.13],1)
# Tapered funerary mask: long cheek bones, brow, recesses, chin; not a cubic head.
for y in range(44,57):
 yy=(y+.5)*U
 width=.26+(min(yy,5.16)-4.45)*.48 if yy<5.16 else .60-(yy-5.16)*.55
 for x in range(-7,8):
  xx=(x+.5)*U
  for z in range(-4,8):
   zz=(z+.5)*U
   if abs(xx)/max(.15,width)+abs(zz-.19)/.58*.54<=1.05 and -.30<zz<.68:
    cells[x,y,z]=2
# Cut dark, slanted eye sockets BEFORE placing recessed amber slits.
for k in list(cells):
 x,y,z=[(v+.5)*U for v in k]
 if .13<abs(x)<.57 and 4.96+abs(x)*.20<y<5.20+abs(x)*.20 and z>.29:del cells[k]
for side in [-1,1]:
 path(mirrored([(.16,5.04,.33),(.31,5.09,.35),(.44,5.14,.29)],side),[.065,.075,.045],5)
 path(mirrored([(.07,5.18,.66),(.31,5.24,.62),(.57,5.35,.43)],side),[.11,.13,.09],0)
 path(mirrored([(.53,4.97,.45),(.42,4.73,.59),(.18,4.48,.58)],side),[.11,.12,.06],0)
 path(mirrored([(.27,4.49,.31),(.25,4.24,.42),(.39,4.10,.48)],side),[.13,.095,.025],0)
path([(0,5.18,.65),(0,4.93,.75),(0,4.79,.70)],[.09,.11,.06],0)
path([(-.23,4.59,.57),(0,4.67,.64),(.23,4.59,.57)],[.06,.055,.06],6)
# Growing crown with forks of different lengths and separate negative-space windows.
for side in [-1,1]:
 path(mirrored([(.36,5.38,-.12),(.55,5.82,-.18),(.79,6.13,-.36),(1.00,6.48,-.39),(1.12 if side==1 else 1.39,6.87 if side==1 else 6.60,-.25)],side),[.20,.20,.16,.105,.025],0)
 path(mirrored([(.73,6.08,-.34),(1.14,6.25,-.24),(1.47,6.45,-.05),(1.62,6.64,.01)],side),[.125,.11,.065,.02],0)
 path(mirrored([(.54,5.83,-.18),(.98,5.84,.02),(1.24,6.02,.10)],side),[.14,.10,.025],0)
 path(mirrored([(1.08,6.28,-.24),(1.17,6.55,-.44),(1.10,6.71,-.51)],side),[.075,.055,.02],0)
path([(.04,5.42,-.19),(.17,5.91,-.56),(.05,6.29,-.66),(-.10,6.48,-.58)],[.17,.13,.085,.02],0)
# Bark plates and green growth follow the anatomy; never alternating stone/wood limb blocks.
def hashed(x,y,z):return ((x*73856093)^(y*19349663)^(z*83492791))&0xffffffff
for k,m in list(cells.items()):
 x,y,z=k;h=hashed(x//3,y//4,z//3)
 if m in [0,1] and y>18 and (x,y,z+1) not in cells and h%23==0:cells[k]=6
 if m in [0,2] and (x,y+1,z) not in cells and y>29 and h%7<2:cells[k]=3
# Hanging moss threads at shoulder/back only; leave the silhouette and heart readable.
for side in [-1,1]:
 for j in range(3):
  path(mirrored([(1.02+j*.18,3.92,-.43),(1.06+j*.18,3.68-j*.07,-.43),(1.0+j*.18,3.45-j*.1,-.40)],side),[.065,.06,.02],3)
flat=[v for (x,y,z),m in sorted(cells.items()) for v in (x,y,z,m)]
data={'version':2,'name':'Страж корней — резной хранитель','unit':U,'palette':palette,'cells':flat}
p=R/'Assets/Resources/Models/RootGuardian.json';p.write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')))
print('Root Guardian v2:',len(cells),'authored voxels;',p.stat().st_size,'bytes; shared Unity/preview model')
