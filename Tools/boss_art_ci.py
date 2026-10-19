"""Offline Blender art QA/export. Accepts candidate boss .blend sources for human approval.
Example: blender -b -t 4 --python-exit-code 23 --python Tools/boss_art_ci.py -- Mars
It never synthesizes a boss from reference images or old blockouts.
"""
import bpy, json, math, sys
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
slug=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else ''
SPEC={
 'Mars':{'parts':['arm','arm','arm','arm','leg','leg','head','eye','eye'],'triangles':25000},
 'Venus':{'parts':['leg']*6+['head','eye','eye','reservoir'],'triangles':25000},
 'Mercury':{'parts':['leg']*6+['mirror']*4+['head','eye','eye'],'triangles':25000},
}
if slug not in SPEC:raise RuntimeError('Select Mars, Venus or Mercury')
source=ROOT/'ArtSource/BossModels/Candidates'/f'{slug}.blend'
if not source.is_file():raise RuntimeError(f'No game-ready model candidate source: {source}. Refuse to export old blockouts.')
out=ROOT/'BossModelReview'/slug;out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source))
objs=[o for o in bpy.data.objects if o.type=='MESH' and not o.hide_render]
if len(objs)<6:raise RuntimeError(f'{slug}: only {len(objs)} visible mesh components; anatomical rig insufficient')
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objs)
if triangles<SPEC[slug]['triangles']:raise RuntimeError(f'{slug}: {triangles} triangles; detailed sculpt/relief missing')
missing=[o.name for o in objs if not o.data.uv_layers]
if missing:raise RuntimeError(f'{slug}: missing game texture UVs: {missing[:5]}')
names=[o.name.lower() for o in objs]
for target in set(SPEC[slug]['parts']):
 need=SPEC[slug]['parts'].count(target)
 got=sum((target in n or (slug=='Venus' and target=='head' and 'crown' in n)) for n in names)
 if got<need:raise RuntimeError(f'{slug}: visible body part {target}: {got} / {need}')
materials={m for o in objs for m in o.data.materials if m}
if len(materials)<4:raise RuntimeError(f'{slug}: at least four distinct physically-based materials required')
normal_mats=sum(any(n.type=='NORMAL_MAP' for n in m.node_tree.nodes) for m in materials if m.use_nodes)
if normal_mats<3:raise RuntimeError(f'{slug}: at least three materials with independently authored normal maps required')
used_images={n.image for m in materials if m.use_nodes for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
if len(used_images)<9:raise RuntimeError(f'{slug}: <9 real textures; albedo+normal+mask sets are required')
for im in used_images:
 if im.packed_file:continue
 path=Path(bpy.path.abspath(im.filepath))
 if not path.is_file():raise RuntimeError(f'{slug}: missing texture: {im.name}: {path}')
if slug=='Venus':
 if not any(m.use_nodes and any(n.type=='BSDF_PRINCIPLED' and (n.inputs.get('Transmission Weight') and n.inputs['Transmission Weight'].default_value>.1 or n.inputs['Alpha'].default_value<.95) for n in m.node_tree.nodes) for m in materials):
  raise RuntimeError('Venus: requires actual transmissive/transparent reservoir material, not opaque green tint')
# Export only authored geometry/rig; never studio lights, floors or cameras.
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
 if o.type in ('MESH','ARMATURE') and o in objs or (o.type=='ARMATURE' and any(c in objs for c in o.children)):
  o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(out/(slug+'.glb')),export_format='GLB',use_selection=True,export_apply=False)
bpy.ops.export_scene.fbx(filepath=str(out/(slug+'.fbx')),use_selection=True,path_mode='COPY',embed_textures=True,add_leaf_bones=False)
# Three actual geometry views under controlled light; art director still approves them manually.
scene=bpy.context.scene
pivot=Vector((0,0,0));bounds=[]
for o in objs:
 bounds.extend([o.matrix_world@Vector(c) for c in o.bound_box])
low=Vector(tuple(min(v[i] for v in bounds) for i in range(3)))
high=Vector(tuple(max(v[i] for v in bounds) for i in range(3)))
pivot=(low+high)*.5;radius=max(high.x-low.x,high.y-low.y,high.z-low.z)*1.2
if radius<=0:raise RuntimeError('Invalid empty model bounds')
for pos,power,color in ((Vector((-radius,-radius,radius)),2100,(1,.88,.77)),(Vector((radius,radius,radius)),1800,(.55,.70,1))):
 lamp=bpy.data.lights.new('Preview only / studio area','AREA');lamp.energy=power;lamp.color=color;lamp.shape='DISK';lamp.size=radius*.8
 obj=bpy.data.objects.new(lamp.name,lamp);scene.collection.objects.link(obj);obj.location=pivot+pos;obj.rotation_euler=(pivot-obj.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.cameras.new('Preview only / neutral orthographic');camera=bpy.data.objects.new(cam.name,cam);scene.collection.objects.link(camera);scene.camera=camera
cam.type='ORTHO';cam.ortho_scale=radius*1.45
scene.render.engine='CYCLES';scene.cycles.samples=28;scene.cycles.use_denoising=False
scene.render.resolution_x=960;scene.render.resolution_y=960;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.world.color=(.11,.11,.11)
for label,azimuth in (('front',0),('side',math.pi*.5),('back',math.pi)):
 camera.location=pivot+Vector((radius*math.sin(azimuth),-radius*math.cos(azimuth),radius*.38))
 camera.rotation_euler=(pivot-camera.location).to_track_quat('-Z','Y').to_euler()
 scene.render.filepath=str(out/(slug+'-'+label+'.png'))
 bpy.ops.render.render(write_still=True)
record={'planet':slug,'review_source':str(source.relative_to(ROOT)),'triangles':triangles,'visible_meshes':len(objs),'materials':len(materials),'pbr_images':len(used_images),'normal_mapped_materials':normal_mats,'note':'QA art export only; no gameplay spawns or combat AI'}
(out/'manifest.json').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf8')
print('BOSS_ART_QA_PASS',slug,triangles,'triangles',len(materials),'materials',len(used_images),'textures')
