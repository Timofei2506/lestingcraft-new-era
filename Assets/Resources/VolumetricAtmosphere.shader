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
            #include "../Shaders/VoxelSky.hlsl"
            #include "../Shaders/EnvironmentLighting.hlsl"
            float _LCSunGlareWeather;float _Density,_Steps,_Submerged;half4 _FogTint;float _FogHeight;
            struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;};
            V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);return o;}
            half SolarOpen(float2 uv)
            {
                if(any(uv<0)||any(uv>1))return 0;
                float d=SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                return step(d,.00001);
                #else
                return step(.99999,d);
                #endif
            }
            half3 SolarGlare(float2 uv)
            {
                if(_EnvironmentSunDirection.y<.015||_Submerged>.5)return 0;
                float4 clip=TransformWorldToHClip(_WorldSpaceCameraPos+_EnvironmentSunDirection.xyz*1500);
                if(clip.w<=0)return 0;float4 screen=ComputeScreenPos(clip);float2 solar=screen.xy/screen.w;
                float2 texel=1/_ScaledScreenParams.xy;half visible=SolarOpen(solar)*.4;
                visible+=(SolarOpen(solar+float2(3,0)*texel)+SolarOpen(solar-float2(3,0)*texel)+SolarOpen(solar+float2(0,3)*texel)+SolarOpen(solar-float2(0,3)*texel))*.15;
                float2 delta=(uv-solar)*float2(_ScaledScreenParams.x/_ScaledScreenParams.y,1);float r2=dot(delta,delta);
                float halo=.085*exp(-r2*85)+.22*exp(-r2*1500);
                float streak=(exp(-abs(delta.y)*1200)+exp(-abs(delta.x)*1800))*.045*exp(-r2*130);
                return _EnvironmentSunColor.rgb*(halo+streak)*visible*_LCSunGlareWeather*saturate(_EnvironmentSunDirection.y*8);
            }
            half4 Frag(V i):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.p);float raw=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 end=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);float3 direction=normalize(end-_WorldSpaceCameraPos);
                float fullDistance=min(length(end-_WorldSpaceCameraPos),160);
                half3 glare=SolarGlare(uv);
                float nearClear=lerp(1.5,0,_Submerged);
                float distance=max(0,fullDistance-nearClear);
                if(distance<=0)return half4(glare,0);
                float stepLength=distance/max(1,_Steps);
                Light sun=GetMainLight();float mu=dot(direction,sun.direction),g=.68;
                float phase=(1-g*g)/(12.56637*pow(max(.01,1+g*g-2*g*mu),1.5));
                float jitter=frac(sin(dot(i.p.xy,float2(12.9898,78.233)))*43758.5453);float transmittance=1;half3 scattering=0;
                for(int n=0;n<48;n++)
                {
                    if(n>=(int)_Steps)break;
                    float3 samplePosition=_WorldSpaceCameraPos+direction*(nearClear+(n+jitter)*stepLength);
                    float heightDensity=lerp(exp(-max(0,samplePosition.y-_FogHeight)*.034),1,_Submerged);
                    half sky=LC_SkyAt(samplePosition,1);float forestMedium=.006*sky*(1-sky)*(1-_Submerged);
                    float extinction=1-exp(-(_Density*lerp(sky,1,_Submerged)+forestMedium)*heightDensity*stepLength);
                    half shadow=MainLightRealtimeShadow(TransformWorldToShadowCoord(samplePosition));
                    half3 light=_FogTint.rgb*.22*LC_SkyEnergy(sky)+sun.color*(min(phase,.6)*1.4+.10)*shadow*(1-_Submerged*.9)*step(.001,sky);
                    scattering+=transmittance*extinction*light;transmittance*=1-extinction;
                }
                return half4(scattering+glare,1-transmittance);
            }
            ENDHLSL
        }
    }
}
