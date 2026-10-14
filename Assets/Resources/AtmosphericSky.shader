Shader "LestingCraft/AtmosphericSky"
{
    Properties
    {
        _Zenith("Zenith",Color)=(.13,.31,.48,1) _Horizon("Horizon",Color)=(.67,.76,.72,1)
        _SunColor("Sun",Color)=(1,.89,.69,1) _SunDirection("Direction",Vector)=(0,1,0,0)
        _CloudNoise("Cloud noise",3D)="white"{} _CloudCoverage("Coverage",Float)=.4 _CloudBase("Base",Float)=128 _CloudSteps("Cloud steps",Float)=24
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
            TEXTURE3D(_CloudNoise);SAMPLER(sampler_CloudNoise);
            float4 _Zenith,_Horizon,_SunColor,_SunDirection;float _CloudCoverage,_CloudBase,_CloudSteps;
            struct A{float4 vertex:POSITION;};struct V{float4 vertex:SV_POSITION;float3 ray:TEXCOORD0;};
            V Vert(A i){V o;o.vertex=TransformObjectToHClip(i.vertex.xyz);o.ray=i.vertex.xyz;return o;}
            float density(float3 p)
            {
                float profile=saturate((p.y-_CloudBase)/9)*saturate((_CloudBase+48-p.y)/15);
                float3 uv=p*.0035+_Time.y*float3(.00017,0,.00007);
                float base=SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,uv,0).r;
                float detail=SAMPLE_TEXTURE3D_LOD(_CloudNoise,sampler_CloudNoise,uv*3.3,0).r;
                return saturate((base*.78+detail*.22-(.74-_CloudCoverage*.35))*5)*profile;
            }
            half4 Frag(V i):SV_Target
            {
                float3 ray=normalize(i.ray),sunDir=normalize(_SunDirection.xyz);float h=saturate(ray.y),sun=saturate(dot(ray,sunDir));
                half3 background=lerp(_Horizon.rgb,_Zenith.rgb,pow(h,.42));background+=_SunColor.rgb*(pow(sun,1600)*9+pow(sun,25)*.16);
                if(ray.y<0)return half4(lerp(_Horizon.rgb,half3(.08,.11,.095),saturate(-ray.y*2)),1);
                if(ray.y<.015)return half4(background,1);
                float start=max(0,(_CloudBase-_WorldSpaceCameraPos.y)/ray.y),end=min(1800,(_CloudBase+48-_WorldSpaceCameraPos.y)/ray.y);
                if(end<=start)return half4(background,1);
                float stepLength=(end-start)/max(1,_CloudSteps),transmittance=1;half3 clouds=0;
                float jitter=frac(sin(dot(i.vertex.xy,float2(12.9898,78.233)))*43758.5453);
                for(int n=0;n<32;n++)
                {
                    if(n>=(int)_CloudSteps||transmittance<.015)break;
                    float3 point=_WorldSpaceCameraPos+ray*(start+(n+jitter)*stepLength);float d=density(point);
                    if(d>.003)
                    {
                        float sunlight=exp(-(density(point+sunDir*14)+density(point+sunDir*30))*1.7);
                        float alpha=1-exp(-d*stepLength*.065);
                        half3 lighting=_Horizon.rgb*.30+_SunColor.rgb*(sunlight*.8+pow(sun,8)*.15);
                        clouds+=lighting*alpha*transmittance;transmittance*=1-alpha;
                    }
                }
                return half4(clouds+background*transmittance,1);
            }
            ENDHLSL
        }
    }
}
