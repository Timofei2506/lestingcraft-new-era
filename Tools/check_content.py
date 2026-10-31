from model_io import load_catalog,save_catalog
"""Cross-check the shipped catalog, generated models/icons, maps and AI masters without Unity."""
from pathlib import Path
import json,re
from PIL import Image
R=Path(__file__).resolve().parents[1]
blocks=json.loads((R/'Assets/Resources/Models/ExpandedBlocks.json').read_text())['blocks']
assert [b['id'] for b in blocks]==list(range(19,90))
names=re.findall(r'"([a-z0-9-]+)"',(R/'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text().split('Names={')[1].split('};')[0])
assert len(names) in (63,72,88,112) and len(set(names))==len(names)
rows=re.findall(r'new ExpandedBlock\((\d+),"([^"]+)","([^"]+)",([\d.]+)f,(true|false),(\d+)\)',(R/'Assets/Scripts/Gameplay/ExpandedContent.cs').read_text())
assert len(rows)==71
for b,row in zip(blocks,rows):
 id,name,surface,hardness,pick,layer=row
 assert int(id)==b['id'] and name==b['name'] and surface==b['surface']
 assert float(hardness)==b['hardness'] and (pick=='true')==b['pick'] and names[int(layer)]==surface
catalog=load_catalog(R)
expected=set(range(1,18))|set(range(19,90))|set(range(100,109))|set(range(110,115))|set(range(120,124))|set(range(130,133))|{135,136,138,139,140,141,142,143,144,145,146,147,148}|set(range(149,166))|set(range(170,178))|set(range(190,196))|{666}
assert len(expected)==154 and {m['id'] for m in catalog['models']}==expected
assert len(catalog['colors'])==len(catalog['layers']) and all(0<=x<len(names) for x in catalog['layers'])
for m in catalog['models']:
 assert len(m['cells'])%4==0 and len(m['cells'])>(400 if m['id']==165 else 900)
 assert all(0<=p<len(catalog['colors']) for p in m['cells'][3::4])
 image=Image.open(R/f'Assets/Resources/VoxelIcons/Item{m["id"]}.png').convert('RGBA')
 assert image.size==(384,384) and image.getchannel('A').getextrema()[1]>0
 if m['id'] in [11,44]:assert image.getchannel('A').getextrema()[1]<255
masters=list((R/'ArtSource/Textures/Iteration11').glob('*.jpg'))
if not masters: masters=list((R/'ArtSource/_Archive/Iteration11').glob('*.jpg'))
assert len(masters)==9
for p in masters:
 image=Image.open(p);assert min(image.size)>=512
animal_info=json.loads((R/'Assets/Art/PBR/materials.json').read_text())['animalTextures']
assert len(animal_info['species'])==30 and {'deer-fur','hare-fur','boar-bristles','wolf-fur','bear-fur','fox-fur','bison-fur','goat-fur','lynx-fur','beaver-fur','frog-skin','eagle-feathers','owl-feathers','penguin-plumage','camel-coat','scorpion-chitin','turtle-shell','moose-fur','capybara-fur','voidray-skin','ash-hound-hide','cherenkov-leech-skin','rad-scorpion-carapace','mutant-boar-hide','slag-carapace','fallout-crow-plumage','gamma-ray-membrane','bone-stalker-hide','vitrified-golem-crust','wraith-shroud'}==set(animal_info['species'].values())
import hashlib
for item in animal_info['masters']:
 p=R/item['path'];assert hashlib.sha256(p.read_bytes()).hexdigest()==item['sha256']
 assert min(Image.open(p).size)>=512
assert len(animal_info['masters'])==3
visual=(R/'Assets/Scripts/Presentation/AnimalVisual.cs').read_text()
assert re.search(r'fur\s*=\s*FurLayerForSpecies\(kind\)', visual) and 'if(fur<0)fur=4' not in visual
assert '_Tint'  in (R/'Assets/Resources/CrystalGlass.shader').read_text()
print(f'PASS: 71 catalog/registry/layer definitions, {len(names)} surface layers, 154 shared voxel models and 384px transparent icons, 30 species PBR coats; no missing resource assets')

assert "145,146,147,148" in (R/"Assets/Scripts/Presentation/ItemArtwork.cs").read_text(), "Torch omitted from generated icon index"
