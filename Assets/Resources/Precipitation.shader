Shader "LestingCraft/Precipitation"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent"}
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float3 ws:TEXCOORD1;};
   V Vert(A i){V o;o.ws=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.ws);o.uv=i.uv;o.color=i.color;return o;}
   half4 Frag(V i):SV_Target
   {
    float2 q=abs(i.uv-.5)*2;
    float streak=pow(saturate(1-q.x*q.x),2)*smoothstep(1,.48,q.y);
    float eye=-TransformWorldToView(i.ws).z;
    float depth=LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.p)),_ZBufferParams);
    float soft=saturate((depth-eye)*3)*smoothstep(.55,1.8,distance(i.ws,_WorldSpaceCameraPos));
    return half4(i.color.rgb,i.color.a*streak*soft);
   }
   ENDHLSL
  }
 }
}
