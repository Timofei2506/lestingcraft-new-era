from pathlib import Path
from PIL import Image
import numpy as np,json
root=Path(__file__).resolve().parents[1];folder=root/'Assets/Art/PBR'
info=json.loads((folder/'materials.json').read_text())
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


# ---------------------------------------------------------------------------
# Frontier sky effects: the shader branches, the C# table and the driver global must agree.
# A shader mode with no branch renders nothing; a table mode beyond the clamp renders nothing;
# a missing global leaves the effect permanently off. All three are cheap to assert here.
# ---------------------------------------------------------------------------
shader = (root / 'Assets/Resources/AtmosphericSky.shader').read_text()
sky = (root / 'Assets/Scripts/Presentation/SkyEffects.cs').read_text()
weather = (root / 'Assets/Scripts/Presentation/DynamicWeather.cs').read_text()
effects = sorted(int(m) for m in __import__('re').findall(r'=\s*(\d)\s*[,}]?\s*(?://[^\n]*)?', sky.split('public enum SkyEffect')[1].split('}')[0]) if True)
modes = [int(v) for v in __import__('re').findall(r'if \(mode == (\d+)\) return', shader)]
enum_modes = sorted(int(v) for v in __import__('re').findall(r'=\s*(\d+),?\s*(?://[^\n]*)?\n', sky.split('public enum SkyEffect')[1].split('}')[0]))
# Mode 0 is None (no branch by design) and the default branch handles the last mode.
assert modes == [m for m in enum_modes if m > 0], f'every non-None SkyEffect value needs an explicit shader branch: shader {modes} vs enum {enum_modes}'
clamp_hi = int(__import__('re').search(r'clamp\(_LCSkyEffect, 0\.0, (\d+)\.0\)', shader).group(1))
assert clamp_hi == max(modes), f'sky effect clamp {clamp_hi} must cover every branch {max(modes)}'
for mode in modes:
    assert f'mode == {mode}' in shader, f'missing branch for mode {mode}'
# The final effect is dispatched by the default branch, so its function must exist.
assert modes, 'sky dispatch must have at least one branch'
for name in ('Aurora', 'Lightning', 'Spores', 'Fractures', 'SeaHaze', 'EmberUpdraft'):
    assert f'half3 {name}(float3 ray)' in shader, f'missing shader function {name}'
for prop in ('_LCSkyEffect', '_LCSkyStrength', '_LCSkyParams'):
    assert prop in shader, f'skybox shader must declare {prop}'
    assert prop in weather, f'DynamicWeather must set {prop}'
assert 'float _LCStormFlash' in shader and '_LCStormFlash' in (root / 'Assets/Scripts/Presentation/AtmosphereLighting.cs').read_text()
assert 'SkyEffects.Resolve(' in weather, 'DynamicWeather must resolve the per-realm sky profile'
assert 'SkyEffect.SeaHaze' in sky and sky.count('case Dimension.') >= 8, f'sky table must cover the realm ladder (has {sky.count("case Dimension.")} realm rows)'
print('PASS sky effects:', len(modes), 'shader modes,', clamp_hi, 'clamp, table + driver in sync')
