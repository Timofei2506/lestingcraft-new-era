Shader "LestingCraft/Water"
{
    Properties { _NormalArray("Normals",2DArray)=""{} }
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
            #include "../Shaders/VoxelSky.hlsl"
            TEXTURE2D_ARRAY(_NormalArray); SAMPLER(sampler_NormalArray);
            float4 _EnvironmentShallowWater, _EnvironmentDeepWater;
            float _EnvironmentWaterReflection;
            float _LCWaterSteps, _LCWaterReflectionProbe;
            float _LCWaterRain;
            struct A { float4 p:POSITION; float3 n:NORMAL; float4 t:TANGENT; };
            struct V
            {
                float4 p:SV_POSITION;
                float3 ws:TEXCOORD0;
                half3 n:TEXCOORD1;
                half4 t:TEXCOORD2;
                half fog:TEXCOORD3;
            };

            void Wave(float2 p,float2 direction,float frequency,float amplitude,float speed,
                inout float3 displacement,inout float2 slope)
            {
                direction=normalize(direction);
                float phase=dot(p,direction)*frequency+_EnvironmentTime*speed;
                float s,c;
                sincos(phase,s,c);
                displacement.y+=s*amplitude;
                displacement.xz+=direction*c*amplitude*.25;
                slope+=direction*c*amplitude*frequency;
            }

            V Vert(A i)
            {
                V o;
                float3 ws=TransformObjectToWorld(i.p.xyz);
                // Sparse greedy geometry must not carry short waves: evaluate fine ripples
                // per fragment. Only a small, long-wavelength displacement reaches vertices.
                float3 displacement=0;
                float2 slope=0;
                Wave(ws.xz,float2(.72,.41),.12,.026,.42,displacement,slope);
                Wave(ws.xz,float2(-.39,1.07),.19,.013,-.57,displacement,slope);
                // Apply the same displacement to all shared edges, including falling sheets.
                ws+=displacement;
                VertexNormalInputs basis=GetVertexNormalInputs(i.n,i.t);
                o.p=TransformWorldToHClip(ws);
                o.ws=ws;
                o.n=basis.normalWS;
                o.t=half4(basis.tangentWS,i.t.w*GetOddNegativeScale());
                o.fog=ComputeFogFactor(o.p.z);
                return o;
            }

            half3 ReflectScene(float3 ws,half3 normal,half3 view,half3 sky,out half confidence)
            {
                float3 origin=TransformWorldToView(ws+normal*.07);
                float3 direction=TransformWorldToViewDir(reflect(-view,normal),true);
                confidence=0;
                float previousDiff=-1,previousAlong=0;
                int steps=(int)clamp(_LCWaterSteps,10,20);
                [loop] for(int index=1;index<=20;index++)
                {
                    if(index>steps)break;
                    float along=index*10.0/steps;
                    float distanceAlong=.20+along*along*.27;
                    float3 ray=origin+direction*distanceAlong;
                    if(ray.z>=-.05)break;
                    float4 projected=ComputeScreenPos(mul(UNITY_MATRIX_P,float4(ray,1)));
                    float2 uv=projected.xy/projected.w;
                    if(any(uv<.003)||any(uv>.997))break;
                    float raw=SampleSceneDepth(uv);
                    #if UNITY_REVERSED_Z
                        bool background=raw<.00001;
                    #else
                        bool background=raw>.99999;
                    #endif
                    float opaqueDepth=LinearEyeDepth(raw,_ZBufferParams);
                    float diff=-ray.z-opaqueDepth;
                    if(!background&&diff>0&&previousDiff<0&&diff<.30+distanceAlong*.045)
                    {
                        float low=previousAlong,high=distanceAlong;
                        float2 hitUV=uv;
                        [unroll] for(int refine=0;refine<3;refine++)
                        {
                            float mid=(low+high)*.5;
                            float3 q=origin+direction*mid;
                            float4 h=ComputeScreenPos(mul(UNITY_MATRIX_P,float4(q,1)));
                            float2 candidate=h.xy/h.w;
                            float depth=LinearEyeDepth(SampleSceneDepth(candidate),_ZBufferParams);
                            if(-q.z>depth){high=mid;hitUV=candidate;}else low=mid;
                        }
                        float edge=saturate(min(min(hitUV.x,1-hitUV.x),min(hitUV.y,1-hitUV.y))*12);
                        confidence=edge*(1-smoothstep(18,28,distanceAlong))*.88;
                        return lerp(sky,SampleSceneColor(hitUV),confidence);
                    }
                    previousDiff=diff;
                    previousAlong=distanceAlong;
                }
                return sky;
            }

            // Concentric rain rings: each 0.55 m cell drops at its own phase, so the pattern
            // never repeats visibly and costs one hash per fragment.
            void RainRipples(float2 p,float time,float amount,inout float2 slope)
            {
                if(amount<=.01)return;
                float2 scaled=p*1.85;
                float2 cell=floor(scaled);
                float2 local=frac(scaled)-.5;
                float seed=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                float phase=frac(time*1.25+seed);
                float radius=phase*.52;
                float ring=1-abs(length(local)-radius)/.075;
                ring=saturate(ring);ring*=ring*(1-phase);
                float2 direction=local/max(.001,length(local));
                slope+=direction*ring*amount*.55;
                // A second, slower droplet per cell keeps the surface busy between impacts.
                float phase2=frac(time*.79+seed*.61+0.37);
                float ring2=saturate(1-abs(length(local*1.4)-phase2*.5)/.06);
                ring2*=ring2*(1-phase2);
                slope+=direction*ring2*amount*.35;
            }

            half4 Frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                float time=_EnvironmentTime;
                half top=saturate(abs(i.n.y));
                float cameraDistance=distance(i.ws,_WorldSpaceCameraPos);
                float nearDetail=1-smoothstep(12,65,cameraDistance);
                float2 uv=lerp(float2(i.ws.x+i.ws.z,i.ws.y+time*.5)*.23,i.ws.xz*.18,top);
                half3 na=SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,uv+time*float2(.018,.012),8).rgb*2-1;
                half3 nb=SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,
                    mul(float2x2(.8,-.6,.6,.8),uv)*2.173-time*float2(.014,.019),8).rgb*2-1;
                half2 fine=0;
                if(nearDetail>.01)
                    fine=(SAMPLE_TEXTURE2D_ARRAY(_NormalArray,sampler_NormalArray,
                        uv*5.37+time*float2(.021,-.017),8).rg*2-1)*nearDetail*.10;
                float3 displacement=0;
                float2 slope=0;
                Wave(i.ws.xz,float2(.72,.41),.83,.038,.72,displacement,slope);
                Wave(i.ws.xz,float2(-.39,1.07),1.14,.022,-.91,displacement,slope);
                Wave(i.ws.xz,float2(.91,-.27),2.2,.008*nearDetail,1.31,displacement,slope);
                RainRipples(i.ws.xz,time,saturate(_LCWaterRain),slope);
                half3 n=normalize(i.n),t=normalize(i.t.xyz),b=cross(n,t)*i.t.w;
                half2 textureSlope=(na.xy+nb.xy)*(.25+_GlobalWindStrength*.30)*
                    lerp(.2,1,nearDetail)+fine;
                half3 nts=normalize(half3(textureSlope,1));
                n=normalize(mul(nts,half3x3(t,b,n))+float3(-slope.x,0,-slope.y)*top);
                n*=IS_FRONT_VFACE(face,1,-1);
                half3 view=GetWorldSpaceNormalizeViewDir(i.ws);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.ws));
                half sky=LC_SkyEnergy(LC_SurfaceSky(i.ws,n,1));
                sun.color*=sky;
                float2 screen=GetNormalizedScreenSpaceUV(i.p);
                float eyeDepth=-TransformWorldToView(i.ws).z;
                float raw=SampleSceneDepth(screen);
                float sceneDepth=LinearEyeDepth(raw,_ZBufferParams);
                float waterDepth=min(36,max(0,sceneDepth-eyeDepth));
                float2 viewNormal=TransformWorldToViewDir(n,true).xy;
                float2 refractedUV=saturate(screen+viewNormal*.014*saturate(waterDepth*.5));
                if(LinearEyeDepth(SampleSceneDepth(refractedUV),_ZBufferParams)<eyeDepth+.02)refractedUV=screen;
                half3 behind=SampleSceneColor(refractedUV);
                half3 transmission=exp(-half3(.25,.075,.043)*waterDepth);
                half3 body=lerp(_EnvironmentShallowWater.rgb,_EnvironmentDeepWater.rgb,saturate(waterDepth*.14));
                body*=lerp(.12,1,_EnvironmentDaylight)*sky;
                half nl=saturate(dot(n,sun.direction));
                if(waterDepth<8&&top>.5&&sky>.01)
                {
                    float deviceDepth=raw;
                    #if !UNITY_REVERSED_Z
                        deviceDepth=lerp(UNITY_NEAR_CLIP_VALUE,1,deviceDepth);
                    #endif
                    float3 bed=ComputeWorldSpacePosition(screen,deviceDepth,UNITY_MATRIX_I_VP);
                    float2 p=bed.xz;
                    float a=sin(p.x*3.1+sin(p.y*2.7+time*.7)+time*.8);
                    float c=sin(p.y*4.3+sin(p.x*2.2-time*.6)-time*.9);
                    float caustic=pow(saturate(1-abs(a+c)*.7),12);
                    float second=pow(saturate(1-abs(sin(p.x*5.1-time*.65)+
                        sin(p.y*4.7+time*.83))*.65),16);
                    behind*=1+(caustic+second*.5)*.18*exp(-waterDepth*.55)*nl*sun.shadowAttenuation*sky;
                }
                half3 refracted=behind*transmission+body*(1-transmission);
                half fresnel=.02+.98*pow(1-saturate(dot(n,view)),5);
                half confidence;
                half3 reflected=ReflectScene(i.ws,n,view,LC_SkyRadiance(reflect(-view,n))*sky,confidence);
                reflected=lerp(body,reflected,saturate(_EnvironmentWaterReflection));
                if(_LCWaterReflectionProbe>.5)
                    return half4(confidence>.02?half3(1,0,1):half3(0,0,1),1);
                half nv=max(.04,saturate(dot(n,view)));
                half nh=saturate(dot(n,SafeNormalize(sun.direction+view)));
                float rough=.09+_GlobalWindStrength*.04+(1-nearDetail)*.055;
                float a2=rough*rough;
                float denominator=max(.0001,nh*nh*(a2-1)+1);
                float distribution=a2/(PI*denominator*denominator);
                float visibility=1/max(.15,4*nv*max(.05,nl));
                half spec=min(5,distribution*visibility*.020*nl)*sun.shadowAttenuation;
                // Sun glitter road: a broad path toward the low sun plus micro-sparkle driven by
                // the ripple normals — the signature "light carpet" of a real sea at golden hour.
                half rd=saturate(dot(reflect(-view,n),sun.direction));
                half path=pow(rd,10)*.22+pow(rd,80)*1.05;
                half sparkle=saturate(.62+na.x*1.25+nb.y*1.05);
                half glitter=path*sparkle*sparkle*nl*sun.shadowAttenuation*sky;
                half3 color=lerp(refracted,reflected,fresnel)+sun.color*(spec+glitter);
                half foamNoise=saturate(.5+na.x*.34+nb.y*.27+
                    sin(dot(i.ws.xz,float2(3.7,2.3))-time*.9)*.12);
                half shore=1-smoothstep(.03,.58,waterDepth);
                half crest=smoothstep(.35,.65,foamNoise);
                half foam=shore*crest*top;
                // Rain-driven chop raises the foam line and adds a fine spray sheen.
                foam=saturate(foam+saturate(_LCWaterRain)*.18*top*crest);
                color=lerp(color,half3(.78,.83,.79)*lerp(.16,1,_EnvironmentDaylight),foam*.58*sky);
                // Aerial perspective: the far sea dissolves into the lit horizon haze exactly as
                // in the reference seascape, without touching near-field readability.
                half3 horizonHaze=LC_SkyRadiance(normalize(float3(-view.x,.045,-view.z)))*sky;
                float haze=saturate(1-exp(-cameraDistance*.00115))*.82;
                color=lerp(color,horizonHaze,haze);
                // Radia's black flooded basin keeps just a trace of sky glint; otherwise the gray storm ceiling washes out the submerged blue rows.
                color=lerp(body,color,saturate(_EnvironmentWaterReflection));
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
