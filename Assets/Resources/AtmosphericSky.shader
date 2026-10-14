Shader "LestingCraft/AtmosphericSky"
{
    Properties {_Zenith("Zenith",Color)=(.13,.31,.48,1) _Horizon("Horizon",Color)=(.67,.76,.72,1) _SunColor("Sun",Color)=(1,.89,.69,1) _SunDirection("Direction",Vector)=(0,1,0,0)}
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _Zenith,_Horizon,_SunColor,_SunDirection;
            struct A{float4 vertex:POSITION;};struct V{float4 vertex:SV_POSITION;float3 ray:TEXCOORD0;};
            V Vert(A i){V o;o.vertex=TransformObjectToHClip(i.vertex.xyz);o.ray=i.vertex.xyz;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
            half4 Frag(V i):SV_Target
            {
                float3 ray=normalize(i.ray);float h=saturate(ray.y);
                half3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(h,.42));
                float sun=saturate(dot(ray,normalize(_SunDirection.xyz)));
                color+=_SunColor.rgb*(pow(sun,1600)*12+pow(sun,25)*.17);
                float2 uv=ray.xz/max(.12,ray.y)*2+_Time.y*float2(.003,.0015);
                float clouds=noise(uv)*.6+noise(uv*2.04)*.28+noise(uv*4.1)*.12;
                float density=smoothstep(.57,.75,clouds)*smoothstep(.08,.27,h);
                color=lerp(color,lerp(_Horizon.rgb,_SunColor.rgb,.24)*1.2,density*.66);
                if(ray.y<0)color=lerp(_Horizon.rgb,half3(.15,.19,.17),saturate(-ray.y*2));
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
