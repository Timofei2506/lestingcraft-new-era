Shader "LestingCraft/Water"
{
    Properties {_AlbedoArray("Albedo",2DArray)=""{} _NormalArray("Normals",2DArray)=""{}}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D_ARRAY(_AlbedoArray);SAMPLER(sampler_AlbedoArray);
            TEXTURE2D_ARRAY(_NormalArray);SAMPLER(sampler_NormalArray);
            struct A{float4 p:POSITION;float3 n:NORMAL;float4 t:TANGENT;float3 uv:TEXCOORD0;};
            struct V{float4 p:SV_POSITION;float3 ws:TEXCOORD0;half3 n:TEXCOORD1;half4 t:TEXCOORD2;float3 uv:TEXCOORD3;half fog:TEXCOORD4;};
            V Vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.p.xyz);VertexNormalInputs n=GetVertexNormalInputs(i.n,i.t);o.p=p.positionCS;o.ws=p.positionWS;o.n=n.normalWS;o.t=half4(n.tangentWS,i.t.w);o.uv=i.uv;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
            half4 Frag(V i):SV_Target
            {
                float2 uv=i.ws.xz*.22;
                half3 na=SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,uv+_Time.y*float2(.025,.018),8).rgb*2-1;
                half3 nb=SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,uv*1.8-_Time.y*float2(.017,.022),8).rgb*2-1;
                half2 wave=half2(sin(i.ws.x*.7+_Time.y*.8),cos(i.ws.z*.8+_Time.y*.6))*.045;
                half3 nts=normalize(half3((na.xy+nb.xy)*1.3+wave,1));half3 n=normalize(i.n),t=normalize(i.t.xyz),b=cross(n,t)*i.t.w;
                n=normalize(mul(nts,half3x3(t,b,n)));half3 view=GetWorldSpaceNormalizeViewDir(i.ws);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.ws));half fresnel=pow(1-saturate(dot(n,view)),4);
                float2 screen=GetNormalizedScreenSpaceUV(i.p);half3 behind=SampleSceneColor(screen+nts.xy*.009);
                half depth=LinearEyeDepth(SampleSceneDepth(screen),_ZBufferParams);
                half waterDepth=max(0,depth-(-TransformWorldToView(i.ws).z));half density=saturate(waterDepth*.09);
                half3 tint=lerp(half3(.13,.40,.35),half3(.035,.16,.21),density);
                half spec=pow(saturate(dot(n,normalize(sun.direction+view))),128)*sun.shadowAttenuation;
                half3 color=lerp(behind*tint*1.35,tint,.48+fresnel*.35)+sun.color*spec*.9;
                half foam=(1-saturate(waterDepth*2))*saturate(.55+sin((i.ws.x+i.ws.z)*8+_Time.y)*.25);
                color=lerp(color,half3(.7,.8,.7),foam*.5);color=MixFog(color,i.fog);
                return half4(color,.72+fresnel*.22);
            }
            ENDHLSL
        }
    }
}
