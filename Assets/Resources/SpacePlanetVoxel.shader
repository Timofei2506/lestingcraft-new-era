Shader "LestingCraft/SpacePlanetVoxel"
{
    Properties
    {
        _BaseMap("Voxel Atlas Albedo", 2D) = "white" {}
        _BumpMap("Surface Normal & Specular(A)", 2D) = "bump" {}
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _AtmoColor("Atmosphere Rim Color", Color) = (0.35, 0.65, 1.0, 0.0)
        _SunDirWS("Star Direction World", Vector) = (0.45, 0.72, -0.53, 0)
        _SolarFlux("Solar Flux", Float) = 1.0
        _NightAmbient("Night Side Ambient", Float) = 0.014
        _EmissionGain("Self Illumination", Float) = 0.0
        _SpecStrength("Ocean/Ice Specular", Float) = 0.35
        _CityGlow("Night City Lights", Float) = 0.0
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
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _AtmoColor;
                float4 _SunDirWS;
                float _SolarFlux;
                float _NightAmbient;
                float _EmissionGain;
                float _SpecStrength;
                float _CityGlow;
                float _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 tangentWS  : TEXCOORD2;
                float3 bitanWS    : TEXCOORD3;
                float3 normalOS   : TEXCOORD4;
                float2 uv         : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 tWS = length(input.tangentOS.xyz) > 0.01
                    ? TransformObjectToWorldDir(input.tangentOS.xyz)
                    : normalize(cross(abs(output.normalWS.y) > 0.9 ? float3(1, 0, 0) : float3(0, 1, 0), output.normalWS));
                output.tangentWS = tWS;
                output.bitanWS = cross(output.normalWS, tWS) * (input.tangentOS.w == 0 ? 1.0 : input.tangentOS.w);
                output.normalOS = input.normalOS;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a - _Cutoff);

                half4 nSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                float3 nTS = normalize(float3(nSample.rg * 2.0 - 1.0, max(0.25, nSample.b * 2.0 - 1.0)));
                float3 geomN = normalize(input.normalWS);
                float3 detailN = normalize(nTS.x * normalize(input.tangentWS) + nTS.y * normalize(input.bitanWS) + nTS.z * geomN);

                float3 sunDir = normalize(length(_SunDirWS.xyz) > 0.001 ? _SunDirWS.xyz : float3(0.45, 0.72, -0.53));
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);

                half macroNdl = dot(geomN, sunDir);
                half microNdl = dot(detailN, sunDir);
                half terminator = smoothstep(-0.10, 0.22, macroNdl);
                half sunlit = terminator * (0.20 + 0.96 * saturate(microNdl));
                half axisFacet = abs(input.normalOS.y) > 0.5 ? 1.04 : (abs(input.normalOS.z) > 0.5 ? 0.96 : 0.89);

                // Specular sun reflection across oceans, glacial ice & planetary rings (packed in _BumpMap.a)
                float3 halfDir = normalize(sunDir + viewDir);
                half specMask = nSample.a;
                half spec = pow(saturate(dot(detailN, halfDir)), 42.0) * specMask * _SpecStrength * terminator * _SolarFlux;

                // Atmospheric Rayleigh Fresnel halo along sunlit grazing cube faces & limbs
                half fresnel = pow(1.0 - saturate(dot(geomN, viewDir)), 3.2);
                half3 atmo = _AtmoColor.rgb * (_AtmoColor.a * fresnel * smoothstep(-0.22, 0.45, macroNdl) * _SolarFlux);

                // Warm orbital night-side city lights along populated continents
                half nightMask = smoothstep(0.02, -0.22, macroNdl) * _CityGlow * saturate(1.0 - specMask * 2.2);
                half citySparkle = saturate(sin(input.uv.x * 430.0) * cos(input.uv.y * 290.0));
                half3 nightCities = half3(1.0, 0.74, 0.36) * (nightMask * pow(citySparkle, 4.0) * 0.85);

                half shade = _NightAmbient + sunlit * _SolarFlux * axisFacet + _EmissionGain;
                half3 col = tex.rgb * _BaseColor.rgb * shade + half3(1.0, 0.97, 0.90) * spec + atmo + nightCities;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
