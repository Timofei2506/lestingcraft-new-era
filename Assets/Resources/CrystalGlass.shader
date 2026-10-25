Shader "LestingCraft/CrystalGlass"
{
    // Transmission shader for the glass block (44), armoured glass (84) and crystal (11/67).
    // Glass: frosted panes with a readable bevel and depth tint.
    // Crystal: dispersion, inner facets and view-dependent glints.
    Properties
    {
        _Tint("Transmission tint", Color) = (0.78, 0.68, 0.98, 1)
        _Density("Absorption / opacity", Range(0.02, 0.5)) = 0.09
        _NormalArray("Microfracture normals", 2DArray) = "" {}
        _Sparkle("View dependent glints", Range(0, 6)) = 3
        _GlassEdge("Readable glass edge", Range(0, 1)) = 0
        _Frost("Frosted pane", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "TransmittingFacets"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../Shaders/VoxelSky.hlsl"
            #include "../Shaders/EnvironmentLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D_ARRAY(_NormalArray); SAMPLER(sampler_NormalArray);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Density, _Sparkle, _GlassEdge, _Frost;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float3 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2; float3 uv:TEXCOORD3; half fog:TEXCOORD4; };

            V Vert(A i)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(i.normalOS, i.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = half4(n.tangentWS, i.tangentOS.w * GetOddNegativeScale());
                o.uv = i.uv;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 n = normalize(i.normalWS), t = normalize(i.tangentWS.xyz), b = cross(n, t) * i.tangentWS.w;
                // Glass uses an almost-flat normal; crystal uses its authored microfracture layer.
                half3 micro = SAMPLE_TEXTURE2D_ARRAY(_NormalArray, sampler_NormalArray, i.uv.xy, i.uv.z).xyz * 2 - 1;
                micro.xy *= (_Sparkle > 0 ? .16 : .022);
                micro = normalize(micro);
                n = normalize(mul(micro, half3x3(t, b, n)));
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half nv = saturate(dot(n, view));
                half f0 = _Sparkle > 0 ? .055 : .045;
                half fresnel = f0 + (1 - f0) * pow(1 - nv, 5);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 halfway = SafeNormalize(view + sun.direction);
                half highlight = pow(saturate(dot(n, halfway)), 220) * sun.shadowAttenuation;
                float3 cell = floor(i.positionWS * 95);
                float grain = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                half sparkle = step(.965, grain) * pow(saturate(dot(n, halfway)), 420) * _Sparkle * sun.shadowAttenuation;

                half3 reflected = LC_SkyRadiance(reflect(-view, n));
                half sky = LC_SkyEnergy(LC_SurfaceSky(i.positionWS, normalize(i.normalWS), 1));

                float2 screen = GetNormalizedScreenSpaceUV(i.positionCS);
                float eye = -TransformWorldToView(i.positionWS).z;
                float raw = SampleSceneDepth(screen);
                float sceneDepth = LinearEyeDepth(raw, _ZBufferParams);
                float thickness = max(0, sceneDepth - eye);
                float2 viewNormal = TransformWorldToViewDir(n, true).xy;
                // Thicker stacks of glass bend the image more, like real panes.
                float bend = (_Sparkle > 0 ? .006 : .0022) * saturate(1 + thickness * .35);
                float2 refractedUV = saturate(screen + viewNormal * bend);
                if (LinearEyeDepth(SampleSceneDepth(refractedUV), _ZBufferParams) < eye + .01) refractedUV = screen;

                half3 transmitted;
                if (_Sparkle > 0)
                {
                    // Chromatic dispersion: each channel bends a little differently.
                    float2 spread = viewNormal * bend * .55;
                    transmitted = half3(
                        SampleSceneColor(saturate(refractedUV + spread)).r,
                        SampleSceneColor(refractedUV).g,
                        SampleSceneColor(saturate(refractedUV - spread)).b);
                }
                else
                {
                    // Frosted pane: four taps emulate a wide, soft blur without a blur pass.
                    float2 frost = viewNormal * .012 * _Frost + .004;
                    half3 a = SampleSceneColor(saturate(refractedUV + float2(frost.x, frost.y)));
                    half3 c = SampleSceneColor(saturate(refractedUV - float2(frost.x, frost.y)));
                    half3 d = SampleSceneColor(saturate(refractedUV + float2(frost.y, -frost.x)));
                    half3 e = SampleSceneColor(saturate(refractedUV - float2(frost.y, -frost.x)));
                    transmitted = lerp(SampleSceneColor(refractedUV), (a + c + d + e) * .25, _Frost);
                }
                // Absorption grows with the optical path, so a wall of glass reads cooler than a single pane.
                half3 absorption = lerp(half3(1, 1, 1), _Tint.rgb, saturate(.22 + thickness * .05));
                transmitted *= absorption;

                half3 color = transmitted * (1 - fresnel) + reflected * fresnel * sky + sun.color * (highlight * 2.4 + sparkle) * sky;

                // Thin frosted bevels make a transparent block identifiable even against a bright sky.
                float2 faceUV = frac(i.uv.xy);
                float borderDistance = min(min(faceUV.x, 1 - faceUV.x), min(faceUV.y, 1 - faceUV.y));
                half edge = (1 - smoothstep(.012, .045, borderDistance)) * _GlassEdge;
                color = lerp(color, half3(.62, .82, .86) * (sky * .7 + .3), edge * .7);

                half alpha = saturate(_Density + fresnel * .62 + highlight * .38 + sparkle * .12 + edge + thickness * .006);
                // Back faces are dimmer: looking through two panes should not turn opaque.
                alpha *= IS_FRONT_VFACE(face, 1, .62);
                return half4(MixFog(color, i.fog), alpha);
            }
            ENDHLSL
        }
    }
}
