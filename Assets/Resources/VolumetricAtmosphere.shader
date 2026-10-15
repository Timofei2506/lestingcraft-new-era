Shader "LestingCraft/VolumetricAtmosphere"
{
    Properties {_Density("Density",Float)=.00035 _Steps("Steps",Float)=16 _Submerged("Underwater",Float)=0 _FogTint("Fog tint",Color)=(.6,.7,.7,1)}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"} Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float _Density,_Steps,_Submerged;half4 _FogTint;float _FogHeight;
            struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;};
            V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);return o;}
            half4 Frag(V i):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.p);float raw=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 end=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);float3 direction=normalize(end-_WorldSpaceCameraPos);
                float fullDistance=min(length(end-_WorldSpaceCameraPos),160);
                float nearClear=lerp(8,0,_Submerged);
                float distance=max(0,fullDistance-nearClear);
                if(distance<=0)return half4(0,0,0,0);
                float stepLength=distance/max(1,_Steps);
                Light sun=GetMainLight();float mu=dot(direction,sun.direction),g=.45;
                float phase=(1-g*g)/(12.56637*pow(max(.01,1+g*g-2*g*mu),1.5));
                float jitter=frac(sin(dot(i.p.xy,float2(12.9898,78.233)))*43758.5453);float transmittance=1;half3 scattering=0;
                for(int n=0;n<24;n++)
                {
                    if(n>=(int)_Steps)break;
                    float3 samplePosition=_WorldSpaceCameraPos+direction*(nearClear+(n+jitter)*stepLength);
                    float heightDensity=lerp(exp(-max(0,samplePosition.y-_FogHeight)*.034),1,_Submerged);
                    float extinction=1-exp(-_Density*heightDensity*stepLength);
                    half shadow=MainLightRealtimeShadow(TransformWorldToShadowCoord(samplePosition));
                    half3 light=_FogTint.rgb*.42+sun.color*(min(phase,.35)*1.1+.10)*shadow*(1-_Submerged*.9);
                    scattering+=transmittance*extinction*light;transmittance*=1-extinction;
                }
                return half4(scattering,1-transmittance);
            }
            ENDHLSL
        }
    }
}
