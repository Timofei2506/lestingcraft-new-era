Shader "LestingCraft/AtmosphericSky"
{
    Properties
    {
        _CloudNoise("Cloud noise",3D)="white"{}
        _CloudOffset("Wind advection",Vector)=(0,0,0,0)
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
            float _CloudCoverage,_CloudBase,_CloudSteps;float4 _CloudOffset;
            struct A{float4 vertex:POSITION;};struct V{float4 vertex:SV_POSITION;float3 ray:TEXCOORD0;};
            V Vert(A i){V o;o.vertex=TransformObjectToHClip(i.vertex.xyz);o.ray=i.vertex.xyz;return o;}
            float density(float3 p)
            {
                float profile=saturate((p.y-_CloudBase)/9)*saturate((_CloudBase+48-p.y)/15);
                float3 shifted=p+_CloudOffset.xyz;
                float2 rotated=mul(float2x2(.848,-.530,.530,.848),shifted.xz);
                float3 uv=float3(rotated.x,shifted.y*.82,rotated.y)*.0031;
                // Different irrational scales + domain warp break the visible repeating lattice.
                float warp=SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,uv*.371+float3(.17,.39,.71),0).r-.5;
                uv+=warp*float3(.29,.12,-.23);
                float base=SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,uv,0).r;
                float detail=SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,uv*2.713+float3(.49,.13,.67),0).r;
                return saturate((base*.78+detail*.22-(.66-_CloudCoverage*.30))*5)*profile;
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
                    float3 samplePosition=_WorldSpaceCameraPos+ray*(start+(n+jitter)*stepLength);float d=density(samplePosition);
                    if(d>.003)
                    {
                        float sunlight=exp(-(density(samplePosition+sunDir*14)+density(samplePosition+sunDir*30))*1.7);
                        float alpha=1-exp(-d*stepLength*.065);
                        half3 ambient=lerp(_EnvironmentHorizon.rgb,_EnvironmentZenith.rgb,.25)*lerp(.30,.68,_EnvironmentDaylight);
                        half3 lighting=ambient+direct*(sunlight*.55+pow(sun,8)*.14);
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
