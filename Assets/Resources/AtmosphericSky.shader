Shader "LestingCraft/AtmosphericSky"
{
    Properties
    {
        _CloudNoise("Cloud noise",3D)="white"{}
        _CloudOffset("Wind advection",Vector)=(0,0,0,0)
        _CloudCoverage("Coverage",Float)=.4
        _CloudBase("Base",Float)=144
        _CloudSteps("Cloud steps",Float)=22
        _HorizonGlow("Horizon glow",Float)=1.0
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/EnvironmentLighting.hlsl"
            TEXTURE3D(_CloudNoise); SAMPLER(sampler_CloudNoise);
            float _CloudCoverage, _CloudBase, _CloudSteps, _LCCloudSteps;
            float4 _CloudOffset;
            float _HorizonGlow;
            struct A { float4 vertex:POSITION; };
            struct V { float4 vertex:SV_POSITION; float3 ray:TEXCOORD0; };
            V Vert(A i)
            {
                V o;
                o.vertex=TransformObjectToHClip(i.vertex.xyz);
                o.ray=i.vertex.xyz;
                return o;
            }
            float Density(float3 p, bool detail)
            {
                // Три слоя вместо двух: низкие кучевые, средний растянутый слой и тонкий высокий кокосовый приют.
                float low=saturate((p.y-_CloudBase)/9)*saturate((_CloudBase+48-p.y)/15);
                float mid=saturate((p.y-_CloudBase-44)/11)*saturate((_CloudBase+84-p.y)/14)*.42;
                float high=saturate((p.y-_CloudBase-90)/8)*saturate((_CloudBase+124-p.y)/12)*.30;
                float profile=max(max(low,mid),high);
                if(profile<=0)return 0;
                float3 shifted=p+_CloudOffset.xyz;
                float2 rotated=mul(float2x2(.848,-.530,.530,.848),shifted.xz);
                float3 uv=float3(rotated.x,shifted.y*.82,rotated.y)*.0031;
                float base=SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,uv,0).r;
                float threshold=.62-_CloudCoverage*.32;
                if(base<threshold-.08)return 0;
                float shape=base;
                if(detail)
                {
                    float fine=SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,
                        uv*2.713+float3(.49,.13,.67),0).r;
                    float micro=SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,
                        uv*5.9+float3(.81,.22,.91),0).r;
                    // Three noise octaves plus erosion: fine noise carves the silhouette
                    // instead of only blurring it.
                    shape=base*.68+fine*.22+micro*.10;
                    shape-=.05*(1-fine)*saturate((shape-threshold)*8);
                }
                return saturate((shape-threshold)*5.5)*profile;
            }
            half4 Frag(V i):SV_Target
            {
                float3 ray=normalize(i.ray);
                half3 background=LC_SkyRadiance(ray);
                if(ray.y<0)
                    return half4(lerp(_EnvironmentHorizon.rgb,_EnvironmentZenith.rgb*.45,saturate(-ray.y*2)),1);
                if(ray.y<.004||_CloudCoverage<.015)
                    return half4(background,1);
                float start=max(0,(_CloudBase-_WorldSpaceCameraPos.y)/ray.y);
                float end=min(14000,(_CloudBase+124-_WorldSpaceCameraPos.y)/ray.y);
                if(end<=start)return half4(background,1);
                float3 sunDir=_EnvironmentSunDirection.y>-.025
                    ?_EnvironmentSunDirection.xyz:_EnvironmentMoonDirection.xyz;
                float mu=clamp(dot(ray,sunDir),-1,1);
                int maximum=(int)clamp(_LCCloudSteps>0?_LCCloudSteps:_CloudSteps,12,40);
                int count=max(8,(int)ceil(maximum*lerp(.65,1,saturate(ray.y*3))));
                float stepLength=(end-start)/count;
                float transmittance=1;
                half3 clouds=0;
                float jitter=LC_IGN(i.vertex.xy);
                // Warm low sun vs cool night moonlight; keep a touch of sky blue in day shadow.
                half3 direct=lerp(half3(.055,.075,.13),
                    lerp(_EnvironmentSunColor.rgb,_EnvironmentSunColor.rgb*half3(1.06,.90,.74),saturate(1-_EnvironmentSunDirection.y*3)),
                    _EnvironmentDaylight);
                half3 ambient=lerp(_EnvironmentHorizon.rgb,_EnvironmentZenith.rgb,.25)*
                    lerp(.30,.68,_EnvironmentDaylight);
                // Dual-lobe Henyey-Greenstein phase, normalized relative to isotropic scattering.
                float forward=.85*(1-.72*.72)/pow(max(.04,1+.72*.72-2*.72*mu),1.5)
                    +.15*(1-.25*.25)/pow(max(.04,1+.25*.25+2*.25*mu),1.5);
                [loop] for(int n=0;n<32;n++)
                {
                    if(n>=count||transmittance<.025)break;
                    float along=start+(n+jitter)*stepLength;
                    float3 p=_WorldSpaceCameraPos+ray*along;
                    float d=Density(p,true);
                    if(d<.003)continue;
                    float shadowDensity=Density(p+sunDir*22,false);
                    shadowDensity+=Density(p+sunDir*58,false)*.6;
                    // Two-tap self shadow keeps bases heavy while sun-facing billows stay bright.
                    float sunlight=exp(-shadowDensity*3.6);
                    float powder=1-exp(-d*stepLength*.13);
                    float alpha=1-exp(-d*stepLength*.085);
                    // Silver lining: bright rims where thin cloud meets strong forward light.
                    float silver=forward*(1-d)*sunlight*powder*.42*saturate(mu);
                    // Height inside the layer: undersides sink toward their own shadow color.
                    float interior=saturate((p.y-_CloudBase)/38);
                    half3 ambientLayer=ambient*lerp(.55,1.15,interior);
                    half3 lighting=ambientLayer+direct*(sunlight*.62+min(forward,8)*.045+silver);
                    lighting=lerp(lighting,background,(1-exp(-along*.00016))*.75);
                    clouds+=lighting*alpha*transmittance;
                    transmittance*=1-alpha;
                }
                // Яркий закатный горизонт: полоса света у солнца над линией горизонта.
                float horizon=smoothstep(.004,.025,ray.y);
                half3 glow=half3(0,0,0);
                if(_HorizonGlow>0 && ray.y<.42)
                {
                    float band=exp(-pow((ray.y-.09)*7.5,2));
                    float sunSide=saturate(dot(ray,sunDir));
                    glow=half3(1.05,.62,.28)*band*pow(sunSide,3)*_EnvironmentDaylight*1.4*_HorizonGlow;
                }
                half3 result=lerp(background,clouds+background*transmittance,horizon)+glow;
                return half4(result,1);
            }
            ENDHLSL
        }
    }
}
