Shader "LestingCraft/CrystalGlass"
{
 Properties
 {
  _Tint("Transmission tint",Color)=(0.78,0.68,0.98,1)
  _Density("Absorption / opacity",Range(0.02,0.5))=0.09
  _NormalArray("Microfracture normals",2DArray)=""{}
  _Sparkle("View dependent glints",Range(0,6))=3
  _GlassEdge("Readable glass edge",Range(0,1))=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10"}
  Pass
  {
   Name "TransmittingFacets" Tags {"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Back
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "../Shaders/VoxelSky.hlsl"
   #include "../Shaders/EnvironmentLighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   TEXTURE2D_ARRAY(_NormalArray);SAMPLER(sampler_NormalArray);
   CBUFFER_START(UnityPerMaterial)
    half4 _Tint;half _Density,_Sparkle,_GlassEdge;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float3 uv:TEXCOORD0;};
   struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float3 uv:TEXCOORD3;half fog:TEXCOORD4;};
   V Vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(i.normalOS,i.tangentOS);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;o.tangentWS=half4(n.tangentWS,i.tangentOS.w*GetOddNegativeScale());o.uv=i.uv;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 Frag(V i):SV_Target
   {
    half3 n=normalize(i.normalWS),t=normalize(i.tangentWS.xyz),b=cross(n,t)*i.tangentWS.w;
    // Glass cubes use an almost-flat normal; crystal uses its authored quartz layer (12).
    half3 micro=SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,i.uv.xy,i.uv.z).xyz*2-1;micro.xy*=(_Sparkle>0?.15:.012);micro=normalize(micro);n=normalize(mul(micro,half3x3(t,b,n)));
    half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);half nv=saturate(dot(n,view));half f0=_Sparkle>0?.055:.04;half fresnel=f0+(1-f0)*pow(1-nv,5);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));half3 halfway=SafeNormalize(view+sun.direction);half highlight=pow(saturate(dot(n,halfway)),220)*sun.shadowAttenuation;
    float3 cell=floor(i.positionWS*95);float grain=frac(sin(dot(cell,float3(12.9898,78.233,37.719)))*43758.5453);
    half sparkle=step(.965,grain)*pow(saturate(dot(n,halfway)),420)*_Sparkle*sun.shadowAttenuation;
    half3 reflected=LC_SkyRadiance(reflect(-view,n));
    half sky=LC_SkyEnergy(LC_SurfaceSky(i.positionWS,normalize(i.normalWS),1));
    float2 screen=GetNormalizedScreenSpaceUV(i.positionCS);float eye=-TransformWorldToView(i.positionWS).z;
    float2 offset=TransformWorldToViewDir(n,true).xy*(_Sparkle>0?.006:.0015);float2 refractedUV=saturate(screen+offset);
    if(LinearEyeDepth(SampleSceneDepth(refractedUV),_ZBufferParams)<eye+.01)refractedUV=screen;
    half3 transmitted=SampleSceneColor(refractedUV)*lerp(half3(1,1,1),_Tint.rgb,_Sparkle>0?.25:.35);
    half3 color=transmitted*(1-fresnel)+reflected*fresnel*sky+sun.color*(highlight*2.4+sparkle)*sky;
    // Thin frosted bevels make a transparent block identifiable even against a bright sky.
    float2 faceUV=frac(i.uv.xy);float borderDistance=min(min(faceUV.x,1-faceUV.x),min(faceUV.y,1-faceUV.y));
    half edge=(1-smoothstep(.012,.032,borderDistance))*_GlassEdge;
    color=lerp(color,half3(.55,.73,.78)*(sky*.65+.35),edge*.65);
    half alpha=saturate(_Density+fresnel*.62+highlight*.38+sparkle*.12+edge);
    return half4(MixFog(color,i.fog),alpha);
   }
   ENDHLSL
  }
 }
}
