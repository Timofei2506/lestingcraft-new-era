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
                float halo=.085*exp(-r2*85)+.22*exp(-r2*1500);
                float streak=(exp(-abs(delta.y)*1200)+exp(-abs(delta.x)*1800))*.045*exp(-r2*130);
                return _EnvironmentSunColor.rgb*(halo+streak)*visible*_LCSunGlareWeather*
                    saturate(_EnvironmentSunDirection.y*8);
            }
            half4 Frag(V i):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.p);
                float raw=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 end=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
                float3 delta=end-_WorldSpaceCameraPos;
                float lengthSquared=max(dot(delta,delta),.000001);
                float3 direction=delta*rsqrt(lengthSquared);
                float fullDistance=min(sqrt(lengthSquared),160);
                half3 glare=SolarGlare(uv);
                float nearClear=lerp(1.5,0,_Submerged);
                float marchDistance=max(0,fullDistance-nearClear);
                if(marchDistance<=.01||_Density<=0)return half4(glare,0);
                // Runtime quality takes precedence over legacy weather materials writing 32/48.
                int maximum=(int)clamp(_LCVolumetricSteps>0?_LCVolumetricSteps:_Steps,6,12);
                int count=max(3,(int)ceil(maximum*saturate(marchDistance/48)));
                float stepLength=marchDistance/count;
                Light sun=GetMainLight();
                float mu=dot(direction,sun.direction),g=.68;
                float phase=(1-g*g)/(12.56637*pow(max(.01,1+g*g-2*g*mu),1.5));
                float jitter=LC_IGN(i.p.xy);
                float transmittance=1;
                half3 scattering=0;
                [loop] for(int n=0;n<12;n++)
                {
                    if(n>=count||transmittance<.015)break;
                    float3 p=_WorldSpaceCameraPos+direction*(nearClear+(n+jitter)*stepLength);
                    float heightDensity=lerp(exp(-max(0,p.y-_FogHeight)*.034),1,_Submerged);
                    if(heightDensity<.002)continue;
                    half sky=LC_SkyAt(p,1);
                    float forestMedium=.006*sky*(1-sky)*(1-_Submerged);
                    float opticalDepth=(_Density*lerp(sky,1,_Submerged)+forestMedium)*heightDensity*stepLength;
                    if(opticalDepth<.00001)continue;
                    float extinction=1-exp(-opticalDepth);
                    half shadow=sky>.001?MainLightRealtimeShadow(TransformWorldToShadowCoord(p)):0;
                    half3 light=_FogTint.rgb*.22*LC_SkyEnergy(sky)+
                        sun.color*(min(phase,.6)*1.4+.10)*shadow*(1-_Submerged*.9)*step(.001,sky);
                    scattering+=transmittance*extinction*light;
                    transmittance*=1-extinction;
                }
                return half4(scattering+glare,1-transmittance);
            }
            ENDHLSL
        }
    }
}
