from pathlib import Path
from PIL import Image
import numpy as np,json
root=Path(__file__).resolve().parents[1];folder=root/'Assets/Art/PBR'
info=json.loads((folder/'materials.json').read_text())
for layer in info['layers']:
 for suffix in ('albedo','normal','mask'):
  im=Image.open(folder/f'{layer}_{suffix}.png');assert im.size==(512,512)
  a=np.asarray(im)
  if suffix=='albedo':
   assert np.abs(a[0].astype(float)-a[-1]).max()<=1
   assert np.abs(a[:,0].astype(float)-a[:,-1]).max()<=1
  if suffix=='normal':
   assert a[:,:,2].min()>180
  if suffix=='mask':assert a.shape[-1]==4
for name in ('PrototypeGame','FirstPersonController'):
 assert 'OnGUI(' not in (root/f'Assets/Scripts/Runtime/{name}.cs').read_text()
assert 'defaultRenderPipeline=null' not in (root/'Assets/Scripts/Runtime/PrototypeGame.cs').read_text()
print('PASS:',len(info['layers']),'material sets, dimensions, periodic albedo seams, normals/masks, no legacy IMGUI/pipeline shutdown')
