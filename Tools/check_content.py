from model_io import load_catalog,save_catalog
"""Cross-check the shipped catalog, generated models/icons, maps and AI masters without Unity."""
from pathlib import Path
import json,re
from PIL import Image
R=Path(__file__).resolve().parents[1]
blocks=json.loads((R/'Assets/Resources/Models/ExpandedBlocks.json').read_text())['blocks']
assert [b['id'] for b in blocks]==list(range(19,54))
names=re.findall(r'"([a-z-]+)"',(R/'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text().split('Names={')[1].split('};')[0])
assert len(names)==63 and len(set(names))==63
rows=re.findall(r'new ExpandedBlock\((\d+),"([^"]+)","([^"]+)",([\d.]+)f,(true|false),(\d+)\)',(R/'Assets/Scripts/Gameplay/ExpandedContent.cs').read_text())
assert len(rows)==35
for b,row in zip(blocks,rows):
 id,name,surface,hardness,pick,layer=row
 assert int(id)==b['id'] and name==b['name'] and surface==b['surface']
 assert float(hardness)==b['hardness'] and (pick=='true')==b['pick'] and names[int(layer)]==surface
catalog=load_catalog(R)
expected=set(range(1,18))|set(range(19,54))|set(range(100,109))|set(range(110,115))|set(range(120,124))|set(range(130,133))|{135,136,138,139,140,141,142,143,144,145,146,147,148}|set(range(149,166))|set(range(170,178))
assert len(expected)==111 and {m['id'] for m in catalog['models']}==expected
assert len(catalog['colors'])==len(catalog['layers']) and all(0<=x<len(names) for x in catalog['layers'])
for m in catalog['models']:
 assert len(m['cells'])%4==0 and len(m['cells'])>0
 assert all(0<=p<len(catalog['colors']) for p in m['cells'][3::4])
 image=Image.open(R/f'Assets/Resources/VoxelIcons/Item{m["id"]}.png').convert('RGBA')
 assert image.size==(384,384) and image.getchannel('A').getextrema()[1]>0
 if m['id'] in [11,44]:assert image.getchannel('A').getextrema()[1]<255
masters=list((R/'ArtSource/Textures/Iteration11').glob('*.jpg'));assert len(masters)==9
for p in masters:
 image=Image.open(p);assert min(image.size)>=512
animal_info=json.loads((R/'Assets/Art/PBR/materials.json').read_text())['animalTextures']
assert set(animal_info['species'].values())=={'deer-fur','hare-fur','boar-bristles'}
import hashlib
for item in animal_info['masters']:
 p=R/item['path'];assert hashlib.sha256(p.read_bytes()).hexdigest()==item['sha256']
 assert min(Image.open(p).size)>=512
assert len(animal_info['masters'])==3
visual=(R/'Assets/Scripts/Presentation/AnimalVisual.cs').read_text()
assert 'fur=FurLayerForSpecies(kind)' in visual and 'if(fur<0)fur=4' not in visual
assert '_Tint'  in (R/'Assets/Resources/CrystalGlass.shader').read_text()
print('PASS: 35 catalog/registry/layer definitions, 63 surface layers, 111 shared voxel models and 384px transparent icons, 9 base AI masters + 3 species-specific AI masters; no missing resource assets')

assert "145,146,147,148" in (R/"Assets/Scripts/Presentation/ItemArtwork.cs").read_text(), "Torch omitted from generated icon index"
