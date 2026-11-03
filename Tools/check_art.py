from pathlib import Path
from PIL import Image
import numpy as np,json
root=Path(__file__).resolve().parents[1];folder=root/'Assets/Art/PBR'
info=json.loads((folder/'materials.json').read_text())
import re
names=re.findall(r'"([a-z0-9-]+)"',(root/'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text().split('Names={')[1].split('};')[0])
assert info['layers']==names, 'materials.json layers differ from SurfaceLayers.Names'
for layer in names:
 for suffix in ('albedo','normal','mask'):
  assert any((folder/f'{layer}_{suffix}.{ext}').exists() for ext in ('jpg','png')), f'missing PBR map: {layer}_{suffix}'
for layer in info['layers']:
 for suffix in ('albedo','normal','mask'):
  path=folder/f'{layer}_{suffix}.jpg';im=Image.open(path if path.exists() else path.with_suffix('.png'));im=im.convert('RGBA' if suffix=='mask' else 'RGB');assert im.size in ((512,512),(1024,1024))
  a=np.asarray(im)
  if suffix=='albedo':
   assert np.abs(a[0].astype(float)-a[-1]).mean()<3 if path.exists() else np.abs(a[0].astype(float)-a[-1]).max()<=1
   assert np.abs(a[:,0].astype(float)-a[:,-1]).mean()<3 if path.exists() else np.abs(a[:,0].astype(float)-a[:,-1]).max()<=1
  if suffix=='normal':
   assert a[:,:,2].min()>180
  if suffix=='mask':assert a.shape[-1]==4
for name in ('PrototypeGame','FirstPersonController'):
 assert 'OnGUI(' not in (root/f'Assets/Scripts/Runtime/{name}.cs').read_text()
assert 'defaultRenderPipeline=null' not in (root/'Assets/Scripts/Runtime/PrototypeGame.cs').read_text()
print('PASS:',len(info['layers']),'material sets, dimensions, periodic albedo seams, normals/masks, no legacy IMGUI/pipeline shutdown')
