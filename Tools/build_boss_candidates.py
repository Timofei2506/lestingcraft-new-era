"""Voxel art revision built FROM the first rejected boss blockouts, not new concept art.
Builds game-compatible FBX/GLB mesh candidates and editable textured .blend sources.
These are REVIEW CANDIDATES, never automatically placed into Unity gameplay.
Run: blender -b -t 4 --python-exit-code 22 --python Tools/build_boss_candidates.py
"""
import bpy,json,math,hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'ArtSource/BossModels';INPUT=ART/'Blockouts';OUT=ART/'Candidates';OUT.mkdir(exist_ok=True,parents=True)
TEX=ROOT/'Assets/Art/PBR'
SOURCES={
 'Mars':['basalt','obsidian','ore-copper','lava','ore-copper','lava','obsidian','lava'],
 'Venus':['ore-sulfur','obsidian','crystal','ore-sulfur','crystal','obsidian','granite'],
 'Mercury':['metal-ingot','ore-copper','polished-marble','ore-gold','crystal','crystal','obsidian','ore-copper']
}
TINTS={
 'Mars':[(.82,.79,.74),(.72,.68,.81),(.92,.65,.52),(1,.32,.07),(.9,.57,.29),(1,.58,.13),(.66,.64,.65),(1,.71,.25)],
 'Venus':[(.92,.88,.73),(.57,.68,.64),(.07,.47,.27),(.81,.70,.50),(.08,.58,.29),(.45,.52,.56),(.85,.72,.54)],
 'Mercury':[(.66,.75,.83),(.93,.71,.42),(.98,.99,1),(.97,.77,.46),(1,.94,.7),(.21,.57,.95),(.57,.60,.64),(.61,.51,.47)]
}
# Exterior face directions in original voxel axes (x,h,depth).
FACES=[((1,0,0),((1,0,0),(1,0,1),(1,1,1),(1,1,0))),((-1,0,0),((0,0,1),(0,0,0),(0,1,0),(0,1,1))),
 ((0,1,0),((0,1,0),(1,1,0),(1,1,1),(0,1,1))),((0,-1,0),((0,0,1),(1,0,1),(1,0,0),(0,0,0))),
 ((0,0,1),((1,0,1),(0,0,1),(0,1,1),(1,1,1))),((0,0,-1),((0,0,0),(1,0,0),(1,1,0),(0,1,0)))]

def hashval(*args):return int.from_bytes(hashlib.blake2b(':'.join(map(str,args)).encode(),digest_size=4).digest(),'little')
def texfile(src,kind):
 for ext in ('png','jpg'):
  path=TEX/f'{src}_{kind}.{ext}'
  if path.is_file():return path
 raise FileNotFoundError(f'{src} {kind}')
def texnode(m,src,kind,space):
 img=bpy.data.images.load(str(texfile(src,kind)),check_existing=True)
 img.colorspace_settings.name=space
 n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=img;n.extension='REPEAT';return n

def material(name,src,tint,emit=0,metal=0,transmission=0,alpha=1):
 m=bpy.data.materials.new(name);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
 p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=.76 if not metal else .28
 p.inputs['Base Color'].default_value=(*tint,1)
 c=texnode(m,src,'albedo','sRGB');mix=m.node_tree.nodes.new('ShaderNodeMixRGB')
 mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.32;mix.inputs[2].default_value=(*tint,1)
 mix.inputs[0].default_value=.82 if 'acid' in name else .32
 m.node_tree.links.new(c.outputs['Color'],mix.inputs[1]);m.node_tree.links.new(mix.outputs['Color'],p.inputs['Base Color'])
 normal=texnode(m,src,'normal','Non-Color');nm=m.node_tree.nodes.new('ShaderNodeNormalMap')
 nm.inputs['Strength'].default_value=1.25;m.node_tree.links.new(normal.outputs['Color'],nm.inputs['Color']);m.node_tree.links.new(nm.outputs['Normal'],p.inputs['Normal'])
 mask=texnode(m,src,'mask','Non-Color');split=m.node_tree.nodes.new('ShaderNodeSeparateColor')
 m.node_tree.links.new(mask.outputs['Color'],split.inputs['Color'])
 m.node_tree.links.new(split.outputs['Red'],p.inputs['Metallic'])
 invert=m.node_tree.nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1.0
 m.node_tree.links.new(mask.outputs['Alpha'],invert.inputs[1]);m.node_tree.links.new(invert.outputs[0],p.inputs['Roughness'])
 if emit:
  p.inputs['Emission Color'].default_value=(*tint,1);p.inputs['Emission Strength'].default_value=emit
 if transmission:
  p.inputs['Transmission Weight'].default_value=transmission;p.inputs['Alpha'].default_value=alpha
  m.surface_render_method='BLENDED'
 return m

def eye_material(name,rgb,rough=.4,emission=0,transmission=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*rgb,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF')
 p.inputs['Base Color'].default_value=(*rgb,1);p.inputs['Roughness'].default_value=rough
 p.inputs['Emission Color'].default_value=(*rgb,1);p.inputs['Emission Strength'].default_value=emission
 p.inputs['Transmission Weight'].default_value=transmission
 return m

def eye_set(slug,root,unit,yoffset):
 if slug=='Mars':positions=[(-1.9,39,3.8),(1.9,39,3.8)];iris=(.35,.078,.016);lit=(1,.34,.07)
 elif slug=='Venus':positions=[(-1.9,46,7),(1.9,46,7)];iris=(.02,.24,.11);lit=(.08,.7,.25)
 else:positions=[(-2.8,24,21.0),(2.8,24,21.0)];iris=(.025,.10,.37);lit=(.15,.43,.8)
 sock=eye_material(slug+'/charred eye socket',(.018,.022,.026),.86)
 cornea=eye_material(slug+'/translucent cornea',iris,.06,0,.55)
 colored=eye_material(slug+'/colored iris',lit,.16,.28)
 pupil=eye_material(slug+'/contracted dark pupil',(.005,.006,.008),.20)
 sparkle=eye_material(slug+'/specular catchlight',(.72,.81,.84),.035,.10)
 for side,(x,y,z) in enumerate(positions):
  if slug=='Mars':y+=yoffset
  center=Vector((x*unit,-(z+.07)*unit,y*unit));rad=unit*(.67 if slug=='Mars' else .50 if slug=='Venus' else .74)
  for title,dx,dy,dz,sx,sy,sz,mat in (
   ('black socket',0,.020,0,1.48,.45,1.05,sock),
   ('glass cornea',0,-.025,0,1.01,.25,.77,cornea),
   ('iris',0,-.074,0,.53,.12,.57,colored),
   ('vertical pupil',0,-.107,0,.17,.07,.45,pupil),
   ('catchlight',-.19,-.136,.23,.14,.065,.12,sparkle)):
   bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=center+Vector((dx*rad,dy,dz*rad)))
   ob=bpy.context.object;ob.name=f'{slug} / eye {side+1} / {title}';ob.scale=(rad*sx,rad*sy,rad*sz);ob.data.materials.append(mat)
   ob.parent=root;ob.matrix_parent_inverse=root.matrix_world.inverted()

def glass_chambers(slug,root,u,m):
 if slug!='Venus':return
 for x,y,z,sx,sy,sz in ((0,23,6,3.1,3.0,2.1),(0,46,7,1.35,2.0,1.05)):
  bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=20,location=(x*u,-z*u,y*u))
  ob=bpy.context.object;ob.name='Venus / transparent reservoir faceted 3D shell';ob.scale=(sx*u,sz*u,sy*u)
  ob.data.materials.append(m);ob.parent=root;ob.matrix_parent_inverse=root.matrix_world.inverted()

def add_wing_cells(vox,pivot,part):
 # Convert old needle-like Mercury mirror spikes into wider stepped reflective fins.
 side=-1 if part.startswith('L') else 1
 z0=int(pivot[2]);blade=int(part.split('-')[-1]);top=44+blade%2*3
 for y in range(30,top):
  t=(y-30)/max(1,top-30);x=side*(6+int(t*(4+blade)))
  z=z0-2-int(t*4);half_z=1+int(math.sin(math.pi*t)*3.3)
  for zz in range(z-half_z,z+half_z+1):
   for xx in range(x-side*1,x+side*2,side):
    if abs(xx)<=2:continue
    local=(xx-pivot[0],y-pivot[1],zz-pivot[2]);vox[local]=3 if zz in (z-half_z,z+half_z) else 2

def replace_materials_and_relief(slug,part,vox):
 # Old coarse blockouts become 2x finer geometry; textures are world-tiled
 # and chiseled edge cells are actually raised/eroded for visible 3D relief.
 up={}
 for (x,y,z),m in vox.items():
  for dx in (0,1):
   for dy in (0,1):
    for dz in (0,1):up[(2*x+dx,2*y+dy,2*z+dz)]=m
 original=set(up);out=dict(up)
 for x,y,z in original:
  m=up[(x,y,z)]
  if slug=='Mars' and m==5 and part=='head':
   out[(x,y,z)]=1 # old square glowing eyes become recesses behind fitted cornea/iris/pupil
   continue
  if slug=='Mars' and m in (3,7):
   world_y=y+50  # torso pivot at y=25 before 2x expansion
   center_line=round(.13*(world_y-52))
   is_fissure=(abs(x-center_line)<=1 and z>7) or (world_y>55 and abs(abs(x)-2)<=1 and z>9)
   out[(x,y,z)]=3 if is_fissure else 1
   continue
  if slug=='Mars' and m==4:continue
  if slug=='Venus' and m in (2,4):continue
  if slug=='Mercury' and m in (2,3,4,5):continue
  exposed=[]
  for (dd,_) in FACES:
   q=(x+dd[0],y+dd[1],z+dd[2])
   if q not in original:exposed.append(dd)
  if not exposed:continue
  h=hashval(slug,part,x,y,z)
  if slug=='Mars' and m in (0,1,2,6):
   if h%67==0 and y>8:out[(x,y,z)]=4 # fissure glimpses
   elif h%13==0:out[(x,y,z)]=2 if h%4==0 else 1
  if slug=='Venus' and m in (0,1,3,6):
   if h%11==0:out[(x,y,z)]=6 if m==0 else 3
  if slug=='Mercury' and m in (0,1,6,7):
   if h%13==0:out[(x,y,z)]=3 if m==1 else 7
  # Keep the projected shape coherent: sparse outward stone layer and microchips.
  if m in (0,1,6) and h%17==0:
   dx,dy,dz=exposed[h%len(exposed)]
   q=(x+dx,y+dy,z+dz)
   out[q]=out[(x,y,z)]
  if m in (0,1,6) and h%43==0 and len(exposed)==1:
   out.pop((x,y,z),None)
 return out

def build(slug):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 data=json.loads((INPUT/f'{slug}.json').read_text(encoding='utf8'));u=data['unit']*.5
 root=bpy.data.objects.new(slug+' / articulated boss candidate',None);bpy.context.collection.objects.link(root)
 palette=[]
 for i,item in enumerate(data['palette']):
  isglass=slug=='Venus' and i==2
  metal=.84 if slug=='Mercury' and i in (0,1,2,3) else 0
  palette.append(material(f'{slug} / {item["name"]}',SOURCES[slug][i],TINTS[slug][i],emit=item['emission']*(.10 if slug=='Venus' else .24) if not isglass else 0,metal=metal,transmission=.56 if isglass else 0,alpha=.64 if isglass else 1))
 ground=bpy.data.materials.new('Preview stage / dark mineral');ground.diffuse_color=(.12,.08,.065,1)
 exposed_total=0;candidate_faces=0
 for part in data['parts']:
  arr=part['cells'];pivot=part['pivot'];old={tuple(arr[i:i+3]):arr[i+3] for i in range(0,len(arr),4)}
  if len(old)!=len(arr)//4:raise RuntimeError(f'{slug} {part["name"]}: duplicate cell coordinate')
  if slug=='Mercury' and 'mirror' in part['name']:add_wing_cells(old,pivot,part['name'])
  up=replace_materials_and_relief(slug,part['name'],old)
  vertices=[];faces=[];ids=[];uvcoords=[]
  for (x,y,z),m in up.items():
   for direction,quad in FACES:
    if (x+direction[0],y+direction[1],z+direction[2]) in up:continue
    base=len(vertices)
    for dx,dy,dz in quad:
     wx=x+dx+2*pivot[0];wy=y+dy+2*pivot[1];wz=z+dz+2*pivot[2]
     if slug=='Venus' and part['name']=='crown' and wy>88:
      wx*=1.40 # stronger jaw/temple silhouette around the real eyes
     if slug=='Mars':
      if 'leg' in part['name']:wy*=1.34
      else:wy+=11
      if 'arm' in part['name']:
       wx+=(4 if 'arm-0' in part['name'] else 6)*(-1 if 'left' in part['name'] else 1)
     vertices.append((wx*u,-wz*u,wy*u))
     tex=(wx*u,wz*u) if direction[1] else (wx*u,wy*u) if direction[2] else (wz*u,wy*u)
     uvcoords.append((tex[0]*.85,tex[1]*.85))
    faces.append((base,base+1,base+2,base+3));ids.append(m);exposed_total+=1
  mesh=bpy.data.meshes.new(part['name']+' / surface-culled textured microvoxel mesh')
  mesh.from_pydata(vertices,[],faces);mesh.update()
  joint=bpy.data.objects.new(slug+' / '+part['name']+' / '+part['motion'],None);bpy.context.collection.objects.link(joint)
  joint.parent=root
  joint.location=((pivot[0]+ (2 if 'arm-0' in part['name'] else 3)*(-1 if 'left' in part['name'] else 1) if slug=='Mars' and 'arm' in part['name'] else pivot[0])*data['unit'], -pivot[2]*data['unit'],
      (pivot[1]*1.34 if 'leg' in part['name'] else pivot[1]+5.5 if slug=='Mars' else pivot[1])*data['unit'])
  bpy.context.view_layer.update()  # parent inverse must use the actual pivot, not a stale matrix
  ob=bpy.data.objects.new(slug+' / '+part['name']+' / GAME MESH',mesh);bpy.context.collection.objects.link(ob)
  for mat in palette:mesh.materials.append(mat)
  uv=mesh.uv_layers.new(name='continuous world-metre UV')
  for polygon,m in zip(mesh.polygons,ids):
   polygon.material_index=m
   for li in polygon.loop_indices:uv.data[li].uv=uvcoords[mesh.loops[li].vertex_index]
  ob.parent=joint;ob.matrix_parent_inverse=joint.matrix_world.inverted()
  candidate_faces+=len(faces)
 bpy.context.view_layer.update()
 eye_set(slug,root,data['unit'],5.5 if slug=='Mars' else 0)
 glass_chambers(slug,root,data['unit'],palette[2])
 # Additional roughness/normal texture diversity is already in the palette.
 # Save the actual editable source. Existing repo PBR texture sources remain linked.
 bpy.context.preferences.filepaths.save_version=0
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(slug+'.blend')))
 bpy.ops.file.make_paths_relative()
 bpy.ops.wm.save_as_mainfile(filepath=str(OUT/(slug+'.blend')))
 # Do not export studio environment; game geometry only.
 bpy.ops.object.select_all(action='DESELECT')
 for ob in bpy.data.objects:ob.select_set(True)
 bpy.ops.export_scene.gltf(filepath=str(OUT/(slug+'.glb')),export_format='GLB',use_selection=True)
 # Render physical geometry under neutral strong light. This is NOT concept art.
 bpy.ops.mesh.primitive_plane_add(size=2000);floor=bpy.context.object;floor.name='Preview-only studio floor';floor.location.z=-.08;floor.data.materials.append(ground)
 scene=bpy.context.scene;scene.world.use_nodes=True;bg=scene.world.node_tree.nodes.get('Background')
 bg.inputs['Color'].default_value=(.13,.13,.14,1);bg.inputs['Strength'].default_value=.8
 spans=([14,9,8] if slug=='Mars' else [14,10,10] if slug=='Venus' else [11.7,11,12])
 def area(location,power,color,size):
  bpy.ops.object.light_add(type='AREA',location=location);ob=bpy.context.object;ob.data.energy=power;ob.data.color=color;ob.data.shape='DISK';ob.data.size=size
  ob.rotation_euler=(Vector((0,0,spans[0]*.5))-ob.location).to_track_quat('-Z','Y').to_euler()
 area((-7,-12,18),3800,(1,.90,.79),10);area((9,5,15),3200,(.62,.76,1),8)
 area((1,-10,5),1000,(1,.50,.30) if slug=='Mars' else (.44,1,.71) if slug=='Venus' else (1,.84,.59),6)
 bpy.ops.object.camera_add(location=(12,-25,spans[0]*.71));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,spans[0]*.51))-cam.location).to_track_quat('-Z','Y').to_euler()
 cam.data.type='ORTHO';cam.data.ortho_scale=spans[0]*1.14;scene.camera=cam
 scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=False
 scene.render.resolution_x=1100;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
 scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/(slug+'-candidate.png'))
 scene.view_settings.view_transform='AgX';bpy.ops.render.render(write_still=True)
 print('BOSS_CANDIDATE',slug,'parts',len(data['parts']),'quads',candidate_faces,'preview',scene.render.filepath)

for name in ('Mars','Venus','Mercury'):build(name)
