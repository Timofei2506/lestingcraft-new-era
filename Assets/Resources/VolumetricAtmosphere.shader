Shader "LestingCraft/VolumetricAtmosphere"
{
    Properties
    {
        _Density("Density",Float)=.00035
        _Steps("Steps",Float)=6
        _Submerged("Underwater",Float)=0
        _FogTint("Fog tint",Color)=(.6,.7,.7,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Off
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
            float _LCSunGlareWeather, _Density, _Steps, _Submerged, _FogHeight, _LCVolumetricSteps;
            half4 _FogTint;
            struct A { float4 p:POSITION; };
            struct V { float4 p:SV_POSITION; };
            V Vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); return o; }

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
                if(clip.w<=0)return 0;
                float4 screen=ComputeScreenPos(clip);
                float2 solar=screen.xy/screen.w;
                float2 delta=(uv-solar)*float2(_ScaledScreenParams.x/_ScaledScreenParams.y,1);
                float r2=dot(delta,delta);
                if(r2>.12)return 0;
                float2 texel=1/_ScaledScreenParams.xy;
                half visible=SolarOpen(solar)*.4;
                visible+=(SolarOpen(solar+float2(3,0)*texel)+SolarOpen(solar-float2(3,0)*texel)+
                    SolarOpen(solar+float2(0,3)*texel)+SolarOpen(solar-float2(0,3)*texel))*.15;
                float halo=.065*exp(-r2*95)+.18*exp(-r2*1600);
                float streak=(exp(-abs(delta.y)*1300)+exp(-abs(delta.x)*1900))*.035*exp(-r2*140);
                return _EnvironmentSunColor.rgb*(halo+streak)*visible*_LCSunGlareWeather*
                    saturate(_EnvironmentSunDirection.y*8);
            }
            half4 Frag(V i):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.p);
                float raw=SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool isSkyBg = raw < .00001;
                #else
                    bool isSkyBg = raw > .99999;
                #endif
                half3 glare=SolarGlare(uv);
                // Do not raymarch volumetric shadow cascades over open sky pixels — AtmosphericSky already
                // computes physical sky scattering, and marching cascades at far clip causes concentric ring banding.
                if(isSkyBg && _Submerged < .5) return half4(glare,0);

                #if !UNITY_REVERSED_Z
                    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 end=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
                float3 delta=end-_WorldSpaceCameraPos;
                float lengthSquared=max(dot(delta,delta),.000001);
                float3 direction=delta*rsqrt(lengthSquared);
                float fullDistance=min(sqrt(lengthSquared),140);
                float nearClear=lerp(2.0,0,_Submerged);
                float marchDistance=max(0,fullDistance-nearClear);
                if(marchDistance<=.01||_Density<=0)return half4(glare,0);

                int maximum=(int)clamp(_LCVolumetricSteps>0?_LCVolumetricSteps:_Steps,8,28);
                int count=max(4,(int)ceil(maximum*saturate(marchDistance/44)));
                float stepLength=marchDistance/count;
                Light sun=GetMainLight();
                float mu=dot(direction,sun.direction),g=.66;
                float phase=(1-g*g)/(12.56637*pow(max(.015,1+g*g-2*g*mu),1.5));
                float jitter=.25+.50*LC_IGN(i.p.xy);
                float transmittance=1;
                half3 scattering=0;
                [loop] for(int n=0;n<28;n++)
                {
                    if(n>=count||transmittance<.015)break;
                    float distAlong=nearClear+(n+jitter)*stepLength;
                    float3 p=_WorldSpaceCameraPos+direction*distAlong;
                    float heightDensity=lerp(exp(-max(0,p.y-_FogHeight)*.042),1,_Submerged);
                    if(heightDensity<.002)continue;
                    half sky=LC_SkyAt(p,1);
                    float forestMedium=.0045*sky*(1-sky)*(1-_Submerged);
                    float opticalDepth=(_Density*lerp(sky,1,_Submerged)+forestMedium)*heightDensity*stepLength;
                    if(opticalDepth<.00001)continue;
                    float extinction=1-exp(-opticalDepth);
                    // Smoothly blend realtime shadow toward 1 past cascade range so cascade edges never form rings.
                    float cascadeFade=saturate((distAlong-68.0)/24.0);
                    half shadow=sky>.001?lerp(MainLightRealtimeShadow(TransformWorldToShadowCoord(p)),1.0,cascadeFade):0;
                    half3 sunVeil=sun.color*lerp(half3(1,1,1),half3(1.14,.86,.60),
                        saturate(1-_EnvironmentSunDirection.y*2.8)*(1-_Submerged));
                    half3 light=_FogTint.rgb*.20*LC_SkyEnergy(sky)+
                        sunVeil*(min(phase,.55)*1.35+.08)*shadow*(1-_Submerged*.9)*step(.001,sky);
                    scattering+=transmittance*extinction*light;
                    transmittance*=1-extinction;
                }
                return half4(scattering+glare,1-transmittance);
            }
            ENDHLSL
        }
    }
}
