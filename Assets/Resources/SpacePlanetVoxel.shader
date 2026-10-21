Shader "LestingCraft/SpacePlanetVoxel"
{
    Properties
    {
        _BaseMap("Voxel Atlas Map", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _SunDirWS("Star Direction World", Vector) = (0.45, 0.72, -0.53, 0)
        _SolarFlux("Solar Flux", Float) = 1.0
        _NightAmbient("Night Side Ambient", Float) = 0.016
        _EmissionGain("Self Illumination", Float) = 0.0
        _Cutoff("Alpha Cutoff", Float) = 0.42
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SunDirWS;
                float _SolarFlux;
                float _NightAmbient;
                float _EmissionGain;
                float _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 normalOS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalOS = input.normalOS;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a - _Cutoff);

                float3 nWS = normalize(input.normalWS);
                float3 sunDir = normalize(length(_SunDirWS.xyz) > 0.001 ? _SunDirWS.xyz : float3(0.45, 0.72, -0.53));
                half rawNdl = dot(nWS, sunDir);
                // Realistic sharp planetary day/night terminator: sunlit faces shine cleanly, night side plunges into true deep-space darkness!
                half sunlit = smoothstep(-0.08, 0.22, rawNdl) * (0.28 + 0.92 * saturate(rawNdl));
                half axisFacet = abs(input.normalOS.y) > 0.5 ? 1.05 : (abs(input.normalOS.z) > 0.5 ? 0.94 : 0.84);
                half shade = _NightAmbient + sunlit * _SolarFlux * axisFacet + _EmissionGain;
                return half4(tex.rgb * _BaseColor.rgb * shade, 1.0);
            }
            ENDHLSL
        }
    }
}
