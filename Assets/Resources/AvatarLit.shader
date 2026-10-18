Shader "LestingCraft/AvatarLit"
{
 Properties{_BaseMap("Atlas",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1) _Smoothness("Smoothness",Range(0,1))=.13}
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #include "../Shaders/VoxelLighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);CBUFFER_START(UnityPerMaterial) half4 _BaseColor;half _Smoothness;float4 _BaseMap_ST;CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float3 ws:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;};
   V Vert(A i){V o;o.ws=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.ws);o.n=TransformObjectToWorldNormal(i.n);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);return o;}
   half4 Frag(V i):SV_Target{InputData d=(InputData)0;d.positionWS=i.ws;d.normalWS=normalize(i.n);d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.ws);d.shadowCoord=TransformWorldToShadowCoord(i.ws);d.bakedGI=SampleSH(d.normalWS);d.shadowMask=1;d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.vertexLighting=VertexLighting(i.ws,d.normalWS);SurfaceData s=(SurfaceData)0;s.albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;s.smoothness=_Smoothness;s.specular=.04;s.occlusion=1;s.alpha=1;return LC_FragmentPBR(d,s,LC_SurfaceSky(i.ws,d.normalWS,1));}
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
