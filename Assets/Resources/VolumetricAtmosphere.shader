Shader "LestingCraft/VolumetricAtmosphere"
{
    Properties {_Density("Density",Float)=.006 _Steps("Steps",Float)=16 _FogTint("Fog tint",Color)=(.6,.7,.7,1)}
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
            float _Density,_Steps;half4 _FogTint;float _FogHeight;
            struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;};
            V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);return o;}
            half4 Frag(V i):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.p);float raw=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 end=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);float3 direction=normalize(end-_WorldSpaceCameraPos);
                float distance=min(length(end-_WorldSpaceCameraPos),110);float stepLength=distance/max(1,_Steps);
                Light sun=GetMainLight();float mu=dot(direction,sun.direction),g=.72;
                float phase=(1-g*g)/(12.56637*pow(max(.01,1+g*g-2*g*mu),1.5));
                float jitter=frac(sin(dot(i.p.xy,float2(12.9898,78.233)))*43758.5453);float transmittance=1;half3 scattering=0;
                for(int n=0;n<24;n++)
                {
                    if(n>=(int)_Steps)break;
                    float3 point=_WorldSpaceCameraPos+direction*(n+jitter)*stepLength;
                    float heightDensity=exp(-max(0,point.y-_FogHeight)*.034);
                    float extinction=1-exp(-_Density*heightDensity*stepLength);
                    half shadow=MainLightRealtimeShadow(TransformWorldToShadowCoord(point));
                    half3 light=_FogTint.rgb*.42+sun.color*(phase*4+.13)*shadow;
                    scattering+=transmittance*extinction*light;transmittance*=1-extinction;
                }
                return half4(scattering,1-transmittance);
            }
            ENDHLSL
        }
    }
}
