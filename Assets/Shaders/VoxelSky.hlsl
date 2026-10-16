#ifndef LC_VOXEL_SKY_INCLUDED
#define LC_VOXEL_SKY_INCLUDED
TEXTURE3D(_LCVoxelSky);SAMPLER(sampler_LCVoxelSky);
float4 _LCVoxelSkyOrigin,_LCVoxelSkySize;
half LC_SkyAt(float3 p,half fallback)
{
 if(_LCVoxelSkyOrigin.w<.5)return fallback;
 float3 uv=(p-_LCVoxelSkyOrigin.xyz)/_LCVoxelSkySize.xyz;
 if(any(uv<0)||any(uv>1))return fallback;
 return SAMPLE_TEXTURE3D_LOD(_LCVoxelSky,sampler_LCVoxelSky,uv,0).r;
}
half LC_SurfaceSky(float3 p,half3 normal,half fallback){return LC_SkyAt(p+normal*.58,fallback);}
half LC_SkyEnergy(half sky){return sky*sky;}
#endif
