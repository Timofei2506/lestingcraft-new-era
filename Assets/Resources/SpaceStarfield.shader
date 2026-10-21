Shader "LestingCraft/SpaceStarfield"
{
    Properties
    {
        _Exposure("Star Brightness", Float) = 1.35
        _TwinkleTime("Time", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Exposure;
                float _TwinkleTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float4 clipPos = TransformObjectToHClip(input.positionOS.xyz);
                // Keep sub-pixel stars crisp and visible at every resolution (1080p..4K) without depth-texture disappearance
                float2 corner = (input.uv - 0.5) * 2.0;
                float minNdc = 0.0022 * clipPos.w * (0.65 + input.color.a * 0.95);
                clipPos.xy += corner * float2(minNdc * (_ScreenParams.y / max(1.0, _ScreenParams.x)), minNdc);
                o.positionCS = clipPos;
                o.uv = input.uv;
                half twinkle = 0.86 + 0.14 * sin(_TwinkleTime * 2.7 + input.positionOS.x * 0.37 + input.positionOS.z * 0.29);
                o.color = half4(input.color.rgb * _Exposure * twinkle, input.color.a);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 d = (i.uv - 0.5) * 2.0;
                float r2 = dot(d, d);
                if (r2 > 1.0) discard;
                half core = exp(-r2 * 5.2);
                half crossSpike = exp(-abs(d.x) * 10.0) * exp(-abs(d.y) * 2.2) * 0.28
                                + exp(-abs(d.y) * 10.0) * exp(-abs(d.x) * 2.2) * 0.28;
                half glow = saturate(core + crossSpike) * i.color.a;
                return half4(i.color.rgb * glow, glow * 0.35);
            }
            ENDHLSL
        }
    }
}
