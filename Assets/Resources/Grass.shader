Shader "LestingCraft/Grass"
{
    Properties {_AlbedoArray("Albedo",2DArray)=""{}}
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "../Shaders/EnvironmentLighting.hlsl"
    struct A{float4 p:POSITION;float3 n:NORMAL;float3 uv:TEXCOORD0;half4 color:COLOR;};
    float3 GrassPosition(A i)
    {
        float3 world=TransformObjectToWorld(i.p.xyz);
        float rootHeight=TransformObjectToWorld(float3(0,i.uv.z,0)).y;
        return LC_BladePosition(world,rootHeight,i.uv.y);
    }
    ENDHLSL
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"} Cull Off
        Pass
        {
            Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "../Shaders/VoxelLighting.hlsl"
            float _LCAmbientOcclusionProbe;
            TEXTURE2D_ARRAY(_AlbedoArray);SAMPLER(sampler_AlbedoArray);
            struct V{float4 p:SV_POSITION;float3 ws:TEXCOORD0;half3 n:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;half fog:TEXCOORD3;};
            V Vert(A i){V o;o.ws=GrassPosition(i);o.p=TransformWorldToHClip(o.ws);o.n=TransformObjectToWorldNormal(i.n);o.uv=i.uv.xy;o.color=i.color;o.fog=ComputeFogFactor(o.p.z);return o;}
            half4 Frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                half3 normal=normalize(i.n)*IS_FRONT_VFACE(face,1,-1);
                InputData lighting=(InputData)0;lighting.positionWS=i.ws;lighting.normalWS=normal;lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.ws);
                lighting.shadowCoord=TransformWorldToShadowCoord(i.ws);lighting.bakedGI=SampleSH(normal);lighting.shadowMask=1;
                lighting.vertexLighting=VertexLighting(i.ws,normal);lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
                half3 albedo=SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray,sampler_AlbedoArray,i.uv*float2(.35,.6),2).rgb;
                SurfaceData s=(SurfaceData)0;s.albedo=albedo*i.color.rgb*lerp(.55,1.12,i.uv.y);s.alpha=1;s.smoothness=.13;s.occlusion=lerp(.65,1,i.uv.y);
                // Small transmitted term for sunlit blades; this is not a subsurface-scattering solver.
                Light sun=GetMainLight(lighting.shadowCoord);s.emission=albedo*sun.color*pow(saturate(dot(-sun.direction,lighting.viewDirectionWS)),4)*.12*sun.shadowAttenuation*LC_SkyEnergy(LC_SkyAt(i.ws,1));
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
                half4 color=LC_FragmentPBR(lighting,s,LC_SurfaceSky(i.ws,normal,1));color.rgb=MixFog(color.rgb,i.fog);return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection,_LightPosition;
            float4 ShadowVert(A i):SV_POSITION
            {
                float3 p=GrassPosition(i),n=TransformObjectToWorldNormal(i.n);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 l=normalize(_LightPosition-p);
                #else
                    float3 l=_LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p,n,l)));
            }
            half4 ShadowFrag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(A i):SV_POSITION{return TransformWorldToHClip(GrassPosition(i));}
            half4 DepthFrag():SV_Target{return 0;}
            ENDHLSL
        }
    }
}
