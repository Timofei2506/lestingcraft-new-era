Shader "LestingCraft/ProceduralGlow"
{
    // Additive unlit sprite used for nebulae, accretion discs, galaxy plates and planet halos.
    Properties
    {
        _MainTex("Glow", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Intensity("Intensity", Float) = 1.4
        _Pulse("Pulse", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+40" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Intensity;
                half _Pulse;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half wave = 1 + _Pulse * sin(_Time.y * 1.7);
                half3 glow = tex.rgb * _Color.rgb * _Intensity * wave;
                half alpha = saturate(tex.a * _Color.a);
                return half4(glow * alpha, 0);
            }
            ENDHLSL
        }
    }
}
