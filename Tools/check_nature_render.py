"""Source/asset contract checks only. This does NOT compile shaders or render a frame."""
from pathlib import Path
from PIL import Image
import numpy as np
root=Path(__file__).resolve().parents[1]
def read(p):return (root/p).read_text()
leaf=np.asarray(Image.open(root/'Assets/Resources/GeneratedLeafOpacity.png').convert('RGBA'))[:,:,3]
assert leaf.shape==(512,512) and .15<(leaf<128).mean()<.35
voxel=read('Assets/Resources/VoxelPBR.shader')
assert voxel.count('LeafClip(i.uv)')==4
assert read('Assets/Resources/VoxelObjects.shader').count('LeafClip(i.uv)')==4
for name in ['ForwardLit','ShadowCaster','DepthOnly','DepthNormals']:assert f'Name "{name}"' in voxel
for name in ['VoxelPBR','VoxelObjects','Grass','AvatarLit']:
 s=read(f'Assets/Resources/{name}.shader');assert 'LC_FragmentPBR(' in s and 'VoxelLighting.hlsl' in s
pbr=read('Assets/Shaders/VoxelLighting.hlsl');assert 'mainLight.color*=step' in pbr and 'lightingData.giColor*=LC_SkyEnergy(sky)' in pbr
assert 'lightingData.additionalLightsColor +=' in pbr and 'CreateLightingData(inputData, surfaceData)' in pbr
fog=read('Assets/Resources/VolumetricAtmosphere.shader');assert 'SolarOpen' in fog and 'SampleSceneDepth(uv)' in fog and '*LC_SkyEnergy(sky)' in fog and '*step(.001,sky)' in fog
fire=read('Assets/Scripts/Presentation/DynamicWeather.cs');assert 'source.shadows=LightShadows.Soft' in fire
assert 'VolumetricQuality==2?48:32' in fire
assert 'int contentVersion=4' in read('Assets/Scripts/Runtime/VoxelWorld.cs')
print(f'PASS render source contracts: four cutout passes, {np.mean(leaf<128):.1%} leaf holes, spatial sky PBR, additional lights preserved, depth-tested glare, 32/48 shaft samples. NOT shader/GPU validation.')
