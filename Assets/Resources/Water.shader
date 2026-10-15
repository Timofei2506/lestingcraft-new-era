Shader "LestingCraft/Water"
{
    Properties {_NormalArray("Normals",2DArray)=""{}}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../Shaders/EnvironmentLighting.hlsl"
            TEXTURE2D_ARRAY(_NormalArray);SAMPLER(sampler_NormalArray);
            float4 _EnvironmentShallowWater,_EnvironmentDeepWater;float _LCWaterSteps;
            struct A{float4 p:POSITION;float3 n:NORMAL;float4 t:TANGENT;};
            struct V{float4 p:SV_POSITION;float3 ws:TEXCOORD0;half3 n:TEXCOORD1;half4 t:TEXCOORD2;half fog:TEXCOORD3;};
            // Continuous world-space waves: adjacent chunks evaluate exactly the same phase.
            V Vert(A i)
            {
                V o;float3 ws=TransformObjectToWorld(i.p.xyz);float time=_EnvironmentTime;
                float p0=dot(ws.xz,float2(.72,.41))+time*.72;
                float p1=dot(ws.xz,float2(-.39,1.07))-time*.91;
                ws.y+=sin(p0)*.028+sin(p1)*.016;
                float2 slope=cos(p0)*float2(.72,.41)*.028+cos(p1)*float2(-.39,1.07)*.016;
                VertexNormalInputs n=GetVertexNormalInputs(i.n,i.t);
                o.p=TransformWorldToHClip(ws);o.ws=ws;
                o.n=normalize(lerp(n.normalWS,normalize(float3(-slope.x,1,-slope.y)),saturate(n.normalWS.y)));
                o.t=half4(n.tangentWS,i.t.w*GetOddNegativeScale());o.fog=ComputeFogFactor(o.p.z);return o;
            }
            half3 ReflectScene(float3 ws,half3 normal,half3 view,half3 sky)
            {
                // Bounded SSR, no history/ghosting; off-screen geometry deliberately falls back to sky.
                float3 origin=TransformWorldToView(ws+normal*.07);
                float3 direction=TransformWorldToViewDir(reflect(-view,normal),true);
                float previousDiff=-1;
                int steps=clamp((int)_LCWaterSteps,10,24);[loop] for(int stepIndex=1;stepIndex<=24;stepIndex++)
                {
                    if(stepIndex>steps)break;float along=stepIndex*10.0/steps;float distanceAlong=.20+along*along*.27;
                    float3 ray=origin+direction*distanceAlong;if(ray.z>=-.05)break;
                    float4 clip=mul(UNITY_MATRIX_P,float4(ray,1));float4 projected=ComputeScreenPos(clip);
                    float2 uv=projected.xy/projected.w;
                    if(any(uv<.003)||any(uv>.997))break;
                    float opaqueDepth=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                    float diff=-ray.z-opaqueDepth;
                    if(diff>0&&previousDiff<0&&diff<.30+distanceAlong*.06)
                    {
                        float edge=saturate(min(min(uv.x,1-uv.x),min(uv.y,1-uv.y))*12);
                        return lerp(sky,SampleSceneColor(uv),edge*.82);
                    }
                    previousDiff=diff;
                }
                return sky;
            }
            half4 Frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                float time=_EnvironmentTime;float2 uv=i.ws.xz*.18;
                half3 na=SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,uv+time*float2(.018,.012),8).rgb*2-1;
                half3 nb=SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,mul(float2x2(.8,-.6,.6,.8),uv)*2.173-time*float2(.014,.019),8).rgb*2-1;
                half2 wave=half2(sin(i.ws.x*.75+time*.8),cos(i.ws.z*.83+time*.6))*.065;
                half3 nts=normalize(half3((na.xy+nb.xy)*(.32+_GlobalWindStrength*.38)+wave*.6,1));
                half3 n=normalize(i.n),t=normalize(i.t.xyz),b=cross(n,t)*i.t.w;
                n=normalize(mul(nts,half3x3(t,b,n)))*IS_FRONT_VFACE(face,1,-1);
                half3 view=GetWorldSpaceNormalizeViewDir(i.ws);Light sun=GetMainLight(TransformWorldToShadowCoord(i.ws));
                float2 screen=GetNormalizedScreenSpaceUV(i.p);float eyeDepth=-TransformWorldToView(i.ws).z;
                float sceneDepth=LinearEyeDepth(SampleSceneDepth(screen),_ZBufferParams);
                float waterDepth=min(36,max(0,sceneDepth-eyeDepth));
                // Do not refract foreground objects across the water silhouette.
                float2 viewNormal=TransformWorldToViewDir(n,true).xy;
                float2 refractedUV=saturate(screen+viewNormal*.014*saturate(waterDepth*.5));
                float distortedDepth=LinearEyeDepth(SampleSceneDepth(refractedUV),_ZBufferParams);
                if(distortedDepth<eyeDepth+.02)refractedUV=screen;
                half3 behind=SampleSceneColor(refractedUV);
                half3 transmission=exp(-half3(.25,.075,.043)*waterDepth);
                half3 body=lerp(_EnvironmentShallowWater.rgb,_EnvironmentDeepWater.rgb,saturate(waterDepth*.14));
                body*=lerp(.12,1,_EnvironmentDaylight);
                half3 refracted=behind*transmission+body*(1-transmission);
                half fresnel=.02+.98*pow(1-saturate(dot(n,view)),5);
                half3 reflected=ReflectScene(i.ws,n,view,LC_SkyRadiance(reflect(-view,n)));
                // Energy-limited GGX sun lobe replaces the previous painted-looking pow highlight.
                half nv=max(.04,saturate(dot(n,view))),nl=saturate(dot(n,sun.direction));
                half nh=saturate(dot(n,normalize(sun.direction+view)));half rough=.075+_GlobalWindStrength*.04;
                half a2=rough*rough;half denominator=max(.001,nh*nh*(a2-1)+1);
                half distribution=a2/(PI*denominator*denominator);
                half visibility=1/max(.15,4*nv*max(.05,nl));
                half spec=min(5,distribution*visibility*.020*nl)*sun.shadowAttenuation;
                half3 color=lerp(refracted,reflected,fresnel)+sun.color*spec;
                // Subtle moving caustics only in shallow, sunlit water; no extra transparent draw pass.
                half caustic=pow(saturate(sin(i.ws.x*4.1+time*.8)*sin(i.ws.z*4.7-time*.6)),9);
                color+=behind*caustic*.10*exp(-waterDepth*.8)*nl*sun.shadowAttenuation;
                half foamNoise=saturate(.5+na.x*.34+nb.y*.27+sin(dot(i.ws.xz,float2(3.7,2.3))-time*.55)*.12);
                half foam=(1-smoothstep(.05,.65,waterDepth))*smoothstep(.25,.65,foamNoise);
                color=lerp(color,half3(.68,.75,.70)*lerp(.16,1,_EnvironmentDaylight),foam*.30);
                color=MixFog(color,i.fog);
                // Scene color is already included above: a second alpha mix would wash out absorption.
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
