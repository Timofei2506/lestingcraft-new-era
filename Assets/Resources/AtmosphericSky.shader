Shader "LestingCraft/AtmosphericSky"
{
    Properties
    {
        _CloudNoise("Cloud noise",3D)="white"{}
        _CloudCoverage("Coverage",Float)=.4 _CloudBase("Base",Float)=144 _CloudSteps("Cloud steps",Float)=24
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/EnvironmentLighting.hlsl"
            TEXTURE3D(_CloudNoise);SAMPLER(sampler_CloudNoise);
            float _CloudCoverage,_CloudBase,_CloudSteps;
            struct A{float4 vertex:POSITION;};struct V{float4 vertex:SV_POSITION;float3 ray:TEXCOORD0;};
            V Vert(A i){V o;o.vertex=TransformObjectToHClip(i.vertex.xyz);o.ray=i.vertex.xyz;return o;}
            float density(float3 p)
            {
                float profile=saturate((p.y-_CloudBase)/9)*saturate((_CloudBase+48-p.y)/15);
                float3 uv=p*.0035+_EnvironmentTime*float3(.00017,0,.00007)*(.5+_GlobalWindStrength);
                float base=SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,uv,0).r;
                float detail=SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,uv*3.3,0).r;
                return saturate((base*.78+detail*.22-(.74-_CloudCoverage*.35))*5)*profile;
            }
            half4 Frag(V i):SV_Target
            {
                float3 ray=normalize(i.ray);
                float3 sunDir=_EnvironmentSunDirection.y>-.025?_EnvironmentSunDirection.xyz:_EnvironmentMoonDirection.xyz;
                half3 background=LC_SkyRadiance(ray);float sun=saturate(dot(ray,sunDir));
                if(ray.y<0)return half4(lerp(_EnvironmentHorizon.rgb,_EnvironmentZenith.rgb*.45,saturate(-ray.y*2)),1);
                if(ray.y<.015)return half4(background,1);
                float start=max(0,(_CloudBase-_WorldSpaceCameraPos.y)/ray.y),end=min(2400,(_CloudBase+48-_WorldSpaceCameraPos.y)/ray.y);
                if(end<=start)return half4(background,1);
                float stepLength=(end-start)/max(1,_CloudSteps),transmittance=1;half3 clouds=0;
                float jitter=frac(sin(dot(i.vertex.xy,float2(12.9898,78.233)))*43758.5453);
                half3 direct=lerp(half3(.055,.075,.13),_EnvironmentSunColor.rgb,_EnvironmentDaylight);
                for(int n=0;n<32;n++)
                {
                    if(n>=(int)_CloudSteps||transmittance<.015)break;
                    float3 point=_WorldSpaceCameraPos+ray*(start+(n+jitter)*stepLength);float d=density(point);
                    if(d>.003)
                    {
                        float sunlight=exp(-(density(point+sunDir*14)+density(point+sunDir*30))*1.7);
                        float alpha=1-exp(-d*stepLength*.065);
                        half3 lighting=_EnvironmentHorizon.rgb*.28+direct*(sunlight*.70+pow(sun,8)*.18);
                        // More aerial perspective along the horizon, less floating white slabs.
                        lighting=lerp(lighting,background,saturate((start+n*stepLength)/2400)*.5);
                        clouds+=lighting*alpha*transmittance;transmittance*=1-alpha;
                    }
                }
                return half4(clouds+background*transmittance,1);
            }
            ENDHLSL
        }
    }
}
