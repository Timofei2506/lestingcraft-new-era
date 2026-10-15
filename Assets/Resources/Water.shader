Shader "LestingCraft/Water"
{
    Properties {_NormalArray("Normals",2DArray)=""{}}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../Shaders/EnvironmentLighting.hlsl"
            TEXTURE2D_ARRAY(_NormalArray);SAMPLER(sampler_NormalArray);
            float4 _EnvironmentShallowWater,_EnvironmentDeepWater;
            struct A{float4 p:POSITION;float3 n:NORMAL;float4 t:TANGENT;};
            struct V{float4 p:SV_POSITION;float3 ws:TEXCOORD0;half3 n:TEXCOORD1;half4 t:TEXCOORD2;half fog:TEXCOORD3;};
            V Vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.p.xyz);VertexNormalInputs n=GetVertexNormalInputs(i.n,i.t);o.p=p.positionCS;o.ws=p.positionWS;o.n=n.normalWS;o.t=half4(n.tangentWS,i.t.w*GetOddNegativeScale());o.fog=ComputeFogFactor(p.positionCS.z);return o;}
            half4 Frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                float time=_EnvironmentTime;float2 uv=i.ws.xz*.18;
                half3 na=SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,uv+time*float2(.018,.012),8).rgb*2-1;
                half3 nb=SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,uv*2.1-time*float2(.014,.019),8).rgb*2-1;
                half2 wave=half2(sin(i.ws.x*.75+time*.8),cos(i.ws.z*.83+time*.6))*.065;
                half3 nts=normalize(half3((na.xy+nb.xy)*(.75+_GlobalWindStrength*.6)+wave,1));
                half3 n=normalize(i.n),t=normalize(i.t.xyz),b=cross(n,t)*i.t.w;
                n=normalize(mul(nts,half3x3(t,b,n)))*IS_FRONT_VFACE(face,1,-1);
                half3 view=GetWorldSpaceNormalizeViewDir(i.ws);Light sun=GetMainLight(TransformWorldToShadowCoord(i.ws));
                float2 screen=GetNormalizedScreenSpaceUV(i.p);float eyeDepth=-TransformWorldToView(i.ws).z;
                float sceneDepth=LinearEyeDepth(SampleSceneDepth(screen),_ZBufferParams);
                float waterDepth=min(36,max(0,sceneDepth-eyeDepth));
                // Do not refract foreground objects across the water silhouette.
                float2 refractedUV=saturate(screen+nts.xy*.012*saturate(waterDepth*.5));
                float distortedDepth=LinearEyeDepth(SampleSceneDepth(refractedUV),_ZBufferParams);
                if(distortedDepth<eyeDepth+.02)refractedUV=screen;
                half3 behind=SampleSceneColor(refractedUV);
                half3 transmission=exp(-half3(.25,.075,.043)*waterDepth);
                half3 body=lerp(_EnvironmentShallowWater.rgb,_EnvironmentDeepWater.rgb,saturate(waterDepth*.14));
                body*=lerp(.12,1,_EnvironmentDaylight);
                half3 refracted=behind*transmission+body*(1-transmission);
                half fresnel=.02+.98*pow(1-saturate(dot(n,view)),5);
                half3 reflected=LC_SkyRadiance(reflect(-view,n));
                half spec=pow(saturate(dot(n,normalize(sun.direction+view))),180)*sun.shadowAttenuation;
                half3 color=lerp(refracted,reflected,fresnel)+sun.color*spec*.75;
                half foamNoise=.5+.25*sin(i.ws.x*7+time*.7)*sin(i.ws.z*9-time*.6)+.18*sin((i.ws.x+i.ws.z)*17);
                half foam=(1-smoothstep(.05,.65,waterDepth))*smoothstep(.25,.65,foamNoise);
                color=lerp(color,half3(.68,.75,.70)*lerp(.16,1,_EnvironmentDaylight),foam*.7);
                color=MixFog(color,i.fog);
                // Scene color is already included above: a second alpha mix would wash out absorption.
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
