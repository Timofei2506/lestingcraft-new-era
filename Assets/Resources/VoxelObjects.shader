Shader "LestingCraft/VoxelObjects"
{
    Properties
    {
        _AlbedoArray("Albedo",2DArray)=""{}
        _NormalArray("Normals",2DArray)=""{}
        _MaskArray("Metallic AO Height Smoothness",2DArray)=""{}
        _NormalStrength("Normal strength",Range(0,2))=1
        _LeafOpacity("Foliage cutout",2D)="white"{}
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    TEXTURE2D(_LeafOpacity);SAMPLER(sampler_LeafOpacity);
    void LeafClip(float3 uv){if(abs(uv.z-6)<.25)clip(SAMPLE_TEXTURE2D(_LeafOpacity,sampler_LeafOpacity,uv.xy).a-.5);}
    ENDHLSL
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Shaders/VoxelLighting.hlsl"
            #include "../Shaders/EnvironmentLighting.hlsl"
            float _LCAmbientOcclusionProbe;
            TEXTURE2D_ARRAY(_AlbedoArray);SAMPLER(sampler_AlbedoArray);
            TEXTURE2D_ARRAY(_NormalArray);SAMPLER(sampler_NormalArray);
            TEXTURE2D_ARRAY(_MaskArray);SAMPLER(sampler_MaskArray);
            CBUFFER_START(UnityPerMaterial)
                half _NormalStrength;
            CBUFFER_END
            float _GlobalWetness,_GlobalSnow;
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float3 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float3 uv:TEXCOORD3;half4 color:COLOR;float4 shadowCoord:TEXCOORD4;half fog:TEXCOORD5;};
            Varyings Vert(Attributes input)
            {
                Varyings o=(Varyings)0;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n=GetVertexNormalInputs(input.normalOS,input.tangentOS);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;
                o.tangentWS=half4(n.tangentWS,input.tangentOS.w*GetOddNegativeScale());
                o.uv=input.uv;o.color=input.color;o.shadowCoord=GetShadowCoord(p);o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                LeafClip(i.uv);
                half4 albedo=SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray,sampler_AlbedoArray,i.uv.xy,i.uv.z);
                half3 normalTS=SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,i.uv.xy,i.uv.z).xyz*2-1;
                normalTS.xy*=_NormalStrength;normalTS=normalize(normalTS);
                half4 mask=SAMPLE_TEXTURE2D_ARRAY(_MaskArray,sampler_MaskArray,i.uv.xy,i.uv.z);
                half3 n=normalize(i.normalWS),t=normalize(i.tangentWS.xyz),b=cross(n,t)*i.tangentWS.w;
                InputData lighting=(InputData)0;
                lighting.positionWS=i.positionWS;lighting.normalWS=normalize(mul(normalTS,half3x3(t,b,n)));
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);lighting.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                lighting.fogCoord=i.fog;lighting.vertexLighting=VertexLighting(i.positionWS,lighting.normalWS);
                lighting.bakedGI=SampleSH(lighting.normalWS);lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);lighting.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                float upward=saturate(i.normalWS.y);
                float snow=upward*_GlobalSnow*.75*step(i.uv.z,8.5);
                float macro=.94+.06*sin(i.positionWS.x*.29+sin(i.positionWS.z*.23))*sin(i.positionWS.z*.31+i.positionWS.y*.13);
                float wet=_GlobalWetness*lerp(.3,1,upward);
                surface.albedo=lerp(albedo.rgb*i.color.rgb*macro,half3(.85,.90,.95),snow);surface.metallic=mask.r;surface.specular=half3(.04,.04,.04);
                surface.smoothness=saturate(lerp(mask.a+wet*.32,.16,snow));surface.normalTS=normalTS;surface.occlusion=mask.g;
                surface.alpha=1;
                surface.emission=surface.albedo*i.color.a*1.5;if(abs(i.uv.z-9)<.25){half heat=saturate((surface.albedo.r-max(surface.albedo.g,surface.albedo.b)*1.1)*3.8);surface.emission=surface.albedo*heat*6*(.9+.1*sin(_EnvironmentTime*.65+i.positionWS.x));surface.smoothness=lerp(surface.smoothness,.87,heat);}
                // Native evidence mode: sample the REAL GPU-bound SSAO during opaque rendering.
                // RenderGraph textures are transient; Shader.GetGlobalTexture after the frame is not a valid proof.
                if(_LCAmbientOcclusionProbe>.5)
                {
                    #if defined(_SCREEN_SPACE_OCCLUSION)
                        half ao=GetScreenSpaceAmbientOcclusion(lighting.normalizedScreenSpaceUV).indirectAmbientOcclusion;
                        return half4(0,ao,0,1);
                    #else
                        return half4(1,0,1,1);
                    #endif
                }
                half4 c=LC_FragmentPBR(lighting,surface,LC_SurfaceSky(i.positionWS,normalize(i.normalWS),1));c.rgb=MixFog(c.rgb,i.fog);return c;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"}
            Cull Off
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;float3 _LightPosition;
            struct A {float4 vertex:POSITION;float3 normal:NORMAL;float3 uv:TEXCOORD0;};
            struct ShadowV {float4 p:SV_POSITION;float3 uv:TEXCOORD0;};
            ShadowV ShadowVert(A i)
            {
                float3 pos=TransformObjectToWorld(i.vertex.xyz),normal=TransformObjectToWorldNormal(i.normal);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction=normalize(_LightPosition-pos);
                #else
                    float3 direction=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(pos,normal,direction));
                clip=ApplyShadowClamping(clip);
                ShadowV o;o.p=clip;o.uv=i.uv;return o;
            }
            half4 ShadowFrag(ShadowV i):SV_Target{LeafClip(i.uv);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct DA{float4 p:POSITION;float3 uv:TEXCOORD0;};struct DV{float4 p:SV_POSITION;float3 uv:TEXCOORD0;};
            DV DepthVert(DA i){DV o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
            half4 DepthFrag(DV i):SV_Target{LeafClip(i.uv);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags {"LightMode"="DepthNormals"} ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormVert
            #pragma fragment NormFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            struct NA{float4 p:POSITION;float3 n:NORMAL;float3 uv:TEXCOORD0;};struct NV{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 uv:TEXCOORD1;};
            NV NormVert(NA i){NV o;o.p=TransformObjectToHClip(i.p.xyz);o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv;return o;}
            half4 NormFrag(NV i):SV_Target{LeafClip(i.uv);float3 normal=normalize(i.n);
            #if defined(_GBUFFER_NORMALS_OCT)
             float2 oct=PackNormalOctQuadEncode(normal);return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
            #else
             return half4(normal,0);
            #endif
            }
            ENDHLSL
        }
    }
}
