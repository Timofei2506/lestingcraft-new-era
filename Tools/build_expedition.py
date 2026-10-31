"""Content 5: reproducible weapons and authored modular settlements. No changes to old generators."""
from pathlib import Path
import json,hashlib,subprocess,sys
R=Path(__file__).resolve().parents[1]
weapons=[(152,'Стражевой меч','sword',140,17,650,3.5,380),(153,'Алмазный меч «Иней»','sword',149,23,600,3.5,1100),(154,'Кузнечный кинжал','dagger',140,10,320,2.7,260),(155,'Алмазный стилет','dagger',149,14,300,2.7,800),(156,'Крылатое копьё','spear',140,16,850,5.0,340),(157,'Алмазная пика','spear',149,22,800,5.0,1050),(158,'Боевой топор','axe',140,24,1100,3.2,420),(159,'Алмазная секира','axe',149,32,1050,3.2,1200),(160,'Рунный молот','hammer',140,30,1400,3.0,500),(161,'Алмазный молот «Грань»','hammer',149,40,1350,3.0,1400),(162,'Двуручный меч','greatsword',140,26,1150,4.2,450),(163,'Алмазный двуручник','greatsword',149,35,1100,4.2,1300),(164,'Походный лук','bow',120,18,1300,20,400)]
(R/'Assets/Resources/Models/ExpeditionWeapons.json').write_text(json.dumps([dict(id=i,name=n,kind=k,material=m,damage=d,cooldown=cd,reach=r,durability=dur) for i,n,k,m,d,cd,r,dur in weapons],ensure_ascii=False,separators=(',',':')))
cs='''namespace LestingCraft.Gameplay {public readonly struct WeaponDefinition {public readonly int Id,Damage,Cooldown,Durability;public readonly float Reach;public readonly string Name,Kind;public WeaponDefinition(int id,string name,string kind,int damage,int cooldown,float reach,int durability){Id=id;Name=name;Kind=kind;Damage=damage;Cooldown=cooldown;Reach=reach;Durability=durability;}} public static class Weapons {public static readonly WeaponDefinition[] All={'''+','.join(f'new WeaponDefinition({i},"{n}","{k}",{d},{cd},{r}f,{dur})' for i,n,k,m,d,cd,r,dur in weapons)+'''};public static bool Has(int id)=>id>=152&&id<=164;public static WeaponDefinition Get(int id)=>Has(id)?All[id-152]:new WeaponDefinition(id,"","",id==103?12:id==102?8:3,500,3.5f,0);}}'''
(R/'Assets/Scripts/Gameplay/Weapons.cs').write_text(cs)
(R/'Server/src/weapons.js').write_text('export const weapons='+json.dumps({i:dict(damage=d,cooldown=cd,reach=r) for i,n,k,m,d,cd,r,dur in weapons},separators=(',',':'))+';\nexport const weapon=id=>weapons[id]||{damage:id===103?12:id===102?8:3,cooldown:500,reach:3.5};\n')
blueprints=[]
def box(b,x0,x1,y0,y1,z0,z1,id):b.extend([x0,x1,y0,y1,z0,z1,id])
def house(b,cx,cz,w=4,d=4,ruined=False):
 box(b,cx-w,cx+w,0,0,cz-d,cz+d,36);box(b,cx-w,cx+w,1,4,cz-d,cz+d,18);box(b,cx-w+1,cx+w-1,1,5,cz-d+1,cz+d-1,0)
 for x in [cx-w,cx+w]:
  for z in [cz-d,cz+d]:box(b,x,x,1,5,z,z,4)
 for x in [cx-w+2,cx+w-2]:box(b,x,x,2,3,cz+d,cz+d,44)
 box(b,cx-1,cx,1,2,cz-d,cz-d,0)
 for k in range(w+2):box(b,cx-w-1+k,cx+w+1-k,5+k,5+k,cz-d-1,cz+d+1,18)
 box(b,cx-w+1,cx-w+1,1,1,cz+d-1,cz+d-1,16);box(b,cx+w-1,cx+w-1,1,1,cz+d-1,cz+d-1,17)
 if ruined:
  box(b,cx+1,cx+w+1,3,10,cz,cz+d+1,0);box(b,cx+w,cx+w,1,2,cz+d,cz+d,38);box(b,cx+w+2,cx+w+3,1,1,cz+d,cz+d+1,38)
b=[]
box(b,-26,26,0,0,-26,26,3);box(b,-2,2,0,0,-26,26,32);box(b,-26,26,0,0,-2,2,32)
for x,z in [(-15,-15),(14,-15),(-15,14),(14,14)]:house(b,x,z)
box(b,-3,3,0,1,-3,3,35);box(b,-1,1,1,2,-1,1,7)
for x in [-3,3]:
 for z in [-3,3]:box(b,x,x,2,5,z,z,4)
box(b,-4,4,6,6,-4,4,18);box(b,-19,-11,1,1,-6,-4,18);box(b,10,18,0,0,5,8,33)
blueprints.append(dict(name='Деревня с колодцем',radius=27,top=11,boxes=b))
b=[]
box(b,-23,23,0,0,-23,23,36)
for x in [-22,22]:box(b,x,x,1,7,-22,22,37)
for z in [-22,22]:box(b,-22,22,1,7,z,z,37)
box(b,-2,2,1,4,-22,-22,0)
for q in range(-22,23,3):
 for x in [-22,22]:box(b,x,x,8,8,q,q+1,37)
 for z in [-22,22]:box(b,q,q+1,8,8,z,z,37)
for x in [-21,21]:
 for z in [-21,21]:
  box(b,x-3,x+3,1,12,z-3,z+3,37);box(b,x-2,x+2,1,13,z-2,z+2,0);box(b,x-3,x+3,10,10,z-3,z+3,18)
  box(b,x-1,x+1,1,2,z-3,z-3,0)
  for q in range(-3,4,2):
   box(b,x+q,x+q,13,13,z-3,z-3,37);box(b,x+q,x+q,13,13,z+3,z+3,37)
  route=[(-2,-2),(-1,-2),(0,-2),(1,-2),(2,-2),(2,-1),(2,0),(2,1),(2,2),(1,2)]
  for step,(sx,sz) in enumerate(route,1):
   box(b,x+sx,x+sx,step+1,step+3,z+sz,z+sz,0);box(b,x+sx,x+sx,step,step,z+sz,z+sz,18)
house(b,0,10,7,6);box(b,6,8,1,4,-12,-10,38)
# A walkable stepped wall access, not an inaccessible ornament.
for t in range(8):box(b,-19,-17,t+1,t+1,-17+t,-17+t,37)
blueprints.append(dict(name='Пограничный замок',radius=26,top=14,boxes=b))
b=[];box(b,-14,14,0,0,-14,14,35);house(b,-4,0,5,4,True);box(b,7,10,1,5,-8,-5,38);box(b,8,9,1,6,-7,-6,0);box(b,7,8,1,1,3,4,38)
blueprints.append(dict(name='Разрушенная усадьба',radius=17,top=12,boxes=b))
b=[];box(b,-12,12,0,0,-12,12,35);box(b,-5,5,1,10,-5,5,38);box(b,-4,4,1,12,-4,4,0);box(b,-1,1,1,2,-5,-5,0);box(b,0,5,7,13,0,5,0);box(b,-7,-6,1,2,4,5,38)
for t in range(6):box(b,-3,-1,t+1,t+1,-2+t,-2+t,18)
blueprints.append(dict(name='Сломанная дозорная башня',radius=14,top=14,boxes=b))
b=[];box(b,-11,11,0,0,-11,11,32)
for x,z in [(-7,-6),(7,6)]:box(b,x-3,x+3,1,3,z-3,z+3,18);box(b,x-2,x+2,1,4,z-2,z+2,0);box(b,x,x,1,2,z-3,z-3,0);box(b,x-4,x+4,4,4,z-4,z+4,4)
box(b,-1,-1,1,1,0,0,15);box(b,2,2,1,1,0,0,16)
blueprints.append(dict(name='Заброшенный лагерь',radius=13,top=5,boxes=b))
(R/'Assets/Resources/Models/ExpeditionStructures.json').write_text(json.dumps(blueprints,ensure_ascii=False,separators=(',',':')))
# This legacy authoring tool must never delete newer server plans or shrink
# the C# content-10 catalog back to five entries when regenerating weapons.
current=json.loads(subprocess.check_output(['node','--input-type=module','-e',"import {plans} from './Server/src/structure-plan.js';console.log(JSON.stringify(plans.slice(0,5)));"],cwd=R,text=True))
if current!=blueprints:raise RuntimeError('Frozen legacy structure source drift; refusing to rewrite existing worlds')
subprocess.run([sys.executable,str(R/'Tools/build_architecture.py')],check=True,cwd=R)
print(len(weapons),'weapons;',len(blueprints),'frozen legacy structure blueprints verified; content-10/11 catalogs retained')
