Shader "LestingCraft/SmokeParticles"
{
 Properties{_BaseMap("Turbulent soft smoke",2D)="white"{}}
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+2" "RenderType"="Transparent"}
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float3 ws:TEXCOORD1;};
   V Vert(A i){V o;o.ws=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.ws);o.uv=i.uv;o.color=i.color;return o;}
   half4 Frag(V i):SV_Target
   {
    float eye=-TransformWorldToView(i.ws).z;
    float depth=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.p)),_ZBufferParams);
    half soft=saturate((depth-eye)*2.5)*smoothstep(.25,.85,distance(i.ws,_WorldSpaceCameraPos));
    half illumination=.4+saturate(dot(GetMainLight().color,half3(.2126,.7152,.0722)))*.6;
    return half4(i.color.rgb*illumination,i.color.a*SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*soft);
   }
   ENDHLSL
  }
 }
}
