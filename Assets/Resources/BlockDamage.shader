Shader "LestingCraft/BlockDamage"
{
 Properties { _Progress("Mining progress",Range(0,1))=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Offset -1,-1
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   float _Progress;
   struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
   V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=frac(i.uv*4)*3.9;return o;}
   float2 Hash(float2 p){return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);}
   half4 Frag(V i):SV_Target
   {
    clip(_Progress-.005);float2 cell=floor(i.uv),uv=frac(i.uv);float first=20,second=20;
    [unroll]for(int y=-1;y<=1;y++)[unroll]for(int x=-1;x<=1;x++)
    {float2 offset=float2(x,y),r=offset+Hash(cell+offset)-uv;float d=dot(r,r);if(d<first){second=first;first=d;}else second=min(second,d);}
    float edge=sqrt(second)-sqrt(first);
    float growth=smoothstep(.06,.18,_Progress)*step(Hash(cell).x, .18+_Progress*.82);
    float crack=(1-smoothstep(.018+.022*_Progress,.055+.025*_Progress,edge))*growth;
    return half4(.035,.026,.018,crack*(.35+.58*_Progress));
   }
   ENDHLSL
  }
 }
}
