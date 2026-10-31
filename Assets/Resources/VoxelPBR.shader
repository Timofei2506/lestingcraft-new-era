Shader "LestingCraft/VoxelPBR"
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
    TEXTURE2D(_LeafOpacity); SAMPLER(sampler_LeafOpacity);
    CBUFFER_START(UnityPerMaterial)
        half _NormalStrength;
    CBUFFER_END
    void LeafClip(float3 uv)
    {
        if(abs(uv.z-6)<.25)
        {
            float2 dx=ddx(uv.xy),dy=ddy(uv.xy);
            clip(SAMPLE_TEXTURE2D_GRAD(_LeafOpacity,sampler_LeafOpacity,frac(uv.xy),dx,dy).a-.5);
        }
    }
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
            #include "../Shaders/VoxelLighting.hlsl"
            #include "../Shaders/EnvironmentLighting.hlsl"
            TEXTURE2D_ARRAY(_AlbedoArray); SAMPLER(sampler_AlbedoArray);
            TEXTURE2D_ARRAY(_NormalArray); SAMPLER(sampler_NormalArray);
            TEXTURE2D_ARRAY(_MaskArray); SAMPLER(sampler_MaskArray);
            float _LCAmbientOcclusionProbe, _GlobalWetness, _GlobalSnow, _LCParallaxSteps, _LCUltra;
            float _EnvironmentWaterReflection;
            struct Attributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float4 tangentOS:TANGENT;
                float3 uv:TEXCOORD0;
                half4 color:COLOR;
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                half4 tangentWS:TEXCOORD2;
                float3 uv:TEXCOORD3;
                half4 color:COLOR;
                half fog:TEXCOORD4;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o=(Varyings)0;
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n=GetVertexNormalInputs(input.normalOS,input.tangentOS);
                o.positionCS=p.positionCS;
                o.positionWS=p.positionWS;
                o.normalWS=n.normalWS;
                o.tangentWS=half4(n.tangentWS,input.tangentOS.w*GetOddNegativeScale());
                o.uv=input.uv;
                o.color=input.color;
                o.fog=ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                LeafClip(i.uv);
                half3 n=normalize(i.normalWS),t=normalize(i.tangentWS.xyz);
                half3 b=cross(n,t)*i.tangentWS.w;
                half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 viewTS=half3(dot(view,t),dot(view,b),dot(view,n));
                float cameraDistance=distance(_WorldSpaceCameraPos,i.positionWS);
                float2 dx=ddx(i.uv.xy),dy=ddy(i.uv.xy);
                bool leaf=abs(i.uv.z-6)<.25;
                bool lava=abs(i.uv.z-9)<.25;
                bool cherenkov=abs(i.uv.z-94)<.25;
                float fade=(1-smoothstep(8,24,cameraDistance))*smoothstep(.10,.35,abs(viewTS.z));
                float2 surfaceUV=i.uv.xy;
                float reliefShadow=1;
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                if(fade>.005&&!leaf&&!lava)
                {
                    int steps=(int)clamp(lerp(4,_LCParallaxSteps,fade),4,16);
                    float2 travel=-viewTS.xy/max(.35,abs(viewTS.z))*.024*fade;
                    float2 increment=travel/steps;
                    float depth=0;
                    float stepDepth=rcp((float)steps);
                    surfaceUV-=travel*.5;
                    float2 previous=surfaceUV;
                    float previousDifference=0;
                    [loop] for(int k=0;k<16;k++)
                    {
                        if(k>=steps)break;
                        float height=1-SAMPLE_TEXTURE2D_ARRAY_GRAD(_MaskArray,sampler_MaskArray,
                            surfaceUV,i.uv.z,dx,dy).b;
                        float difference=depth-height;
                        if(difference>=0)
                        {
                            float weight=saturate(difference/max(.0001,difference-previousDifference));
                            surfaceUV=lerp(surfaceUV,previous,weight);
                            break;
                        }
                        previous=surfaceUV;
                        previousDifference=difference;
                        surfaceUV+=increment;
                        depth+=stepDepth;
                    }
                    if(_LCUltra>.5&&dot(n,sun.direction)>.1)
                    {
                        float3 lightTS=float3(dot(sun.direction,t),dot(sun.direction,b),dot(sun.direction,n));
                        float centerHeight=SAMPLE_TEXTURE2D_ARRAY_GRAD(_MaskArray,sampler_MaskArray,
                            surfaceUV,i.uv.z,dx,dy).b;
                        float2 lightStep=lightTS.xy/max(.25,lightTS.z)*.012;
                        float h0=SAMPLE_TEXTURE2D_ARRAY_GRAD(_MaskArray,sampler_MaskArray,
                            surfaceUV+lightStep,i.uv.z,dx,dy).b;
                        float h1=SAMPLE_TEXTURE2D_ARRAY_GRAD(_MaskArray,sampler_MaskArray,
                            surfaceUV+lightStep*2,i.uv.z,dx,dy).b;
                        reliefShadow=1-saturate(max(h0-centerHeight-.16,h1-centerHeight-.32)*3)*fade*.40;
                    }
                }
                if(lava)surfaceUV+=_EnvironmentTime*float2(.006,.0035);
                half4 albedo=SAMPLE_TEXTURE2D_ARRAY_GRAD(_AlbedoArray,sampler_AlbedoArray,surfaceUV,i.uv.z,dx,dy);
                half3 normalTS=SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray,sampler_NormalArray,surfaceUV,i.uv.z,dx,dy).xyz*2-1;
                half4 mask=SAMPLE_TEXTURE2D_ARRAY_GRAD(_MaskArray,sampler_MaskArray,surfaceUV,i.uv.z,dx,dy);
                float upward=saturate(n.y);
                float macroWave=sin(i.positionWS.x*.29+sin(i.positionWS.z*.23))*
                    sin(i.positionWS.z*.31+i.positionWS.y*.13);
                float macro=.96+.04*macroWave;
                float wet=saturate(_GlobalWetness)*lerp(.3,1,upward);
                float porosity=1-mask.r;
                float puddle=wet*upward*smoothstep(.30,.75,1-mask.b)*smoothstep(.08,.4,_GlobalWetness);
                float microFade=1-smoothstep(5,22,cameraDistance);
                float2 breakup=float2(sin(dot(i.positionWS,float3(.41,.27,.19))),
                    cos(dot(i.positionWS,float3(.23,.31,.37))))*.025;
                normalTS.xy=(normalTS.xy+breakup)*_NormalStrength;
                normalTS.xy*=1-puddle*.65;
                if(_GlobalWetness>.05&&upward>.5&&microFade>.01)
                {
                    float2 cell=floor(i.positionWS.xz*2.4);
                    float2 local=frac(i.positionWS.xz*2.4)-.5;
                    float random=LC_Hash(float3(cell,47));
                    float phase=frac(_EnvironmentTime*.85+random);
                    float radius=length(local);
                    float ring=sin((radius-phase*.7)*48)*exp(-abs(radius-phase*.7)*18)*
                        (1-phase)*smoothstep(.45,.1,radius);
                    float2 ripple=local/max(.04,radius)*ring*.035*wet*microFade;
                    float3 worldRipple=float3(ripple.x,0,ripple.y);
                    normalTS.xy+=float2(dot(worldRipple,t),dot(worldRipple,b))*_NormalStrength;
                }
                normalTS=normalize(normalTS);
                InputData lighting=(InputData)0;
                lighting.positionWS=i.positionWS;
                lighting.normalWS=normalize(mul(normalTS,half3x3(t,b,n)));
                lighting.viewDirectionWS=view;
                lighting.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                lighting.fogCoord=i.fog;
                lighting.vertexLighting=VertexLighting(i.positionWS,lighting.normalWS);
                lighting.bakedGI=SampleSH(lighting.normalWS);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                float snow=upward*_GlobalSnow*.75*step(i.uv.z,8.5);
                if(abs(i.uv.z-15)<.25)snow=max(snow,.9);
                half ao=saturate(mask.g*i.color.r);
                SurfaceData surface=(SurfaceData)0;
                half3 baseAlbedo=albedo.rgb*macro*(1-wet*porosity*.24)*reliefShadow;
                // The submerged Cherenkov rows are intentionally blue-on-black, not white LEDs. Suppress their surface luminance only in Radia; other worlds keep the authored material.
                if(cherenkov)baseAlbedo*=lerp(.26,1,saturate(_EnvironmentWaterReflection));
                surface.albedo=lerp(baseAlbedo,half3(.73,.79,.84),snow);
                surface.metallic=mask.r;
                surface.specular=half3(.04,.04,.04);
                surface.smoothness=saturate(lerp(lerp(mask.a+wet*.20,.92,puddle),.24,snow));
                surface.smoothness*=lerp(.55,1,ao*ao);
                surface.normalTS=normalTS;
                surface.occlusion=ao*ao;
                surface.alpha=1;
                half sky=LC_SurfaceSky(i.positionWS,n,i.color.g);
                if(leaf)
                {
                    half transmission=pow(saturate(dot(-sun.direction,view)),3);
                    surface.emission=albedo.rgb*sun.color*transmission*.28*
                        sun.shadowAttenuation*LC_SkyEnergy(sky);
                    surface.smoothness=min(surface.smoothness,.28);
                }
                if(lava)
                {
                    half heat=saturate((albedo.r-max(albedo.g,albedo.b)*1.10)*3.8);
                    half pulse=.9+.1*sin(_EnvironmentTime*.65+i.positionWS.x*.35+i.positionWS.z*.29);
                    half3 blackbody=lerp(half3(1,.085,.008),half3(1,.72,.24),heat*heat);
                    surface.emission=blackbody*heat*5.5*pulse;
                    surface.smoothness=lerp(mask.a,.87,heat);
                    surface.albedo*=lerp(1,.45,heat);
                }
                if(abs(i.uv.z-12)<.25)surface.emission+=albedo.rgb*.16;
                if(snow>.01&&microFade>.01)
                {
                    float grain=LC_Hash(floor(i.positionWS*85));
                    float footprint=max(length(ddx(i.positionWS)),length(ddy(i.positionWS)))*85;
                    half glint=step(.978,grain)*pow(saturate(dot(lighting.normalWS,
                        SafeNormalize(sun.direction+view))),128);
                    surface.emission+=sun.color*glint*snow*microFade*
                        (1-saturate(footprint))*sun.shadowAttenuation*LC_SkyEnergy(sky)*.65;
                }
                if(_LCAmbientOcclusionProbe>.5)
                {
                    #if defined(_SCREEN_SPACE_OCCLUSION)
                        half probe=GetScreenSpaceAmbientOcclusion(lighting.normalizedScreenSpaceUV).indirectAmbientOcclusion;
                        return half4(0,probe,0,1);
                    #else
                        return half4(1,0,1,1);
                    #endif
                }
                half4 color=LC_FragmentPBR(lighting,surface,sky);
                color.rgb=MixFog(color.rgb,i.fog);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            Cull Off
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection, _LightPosition;
            struct A { float4 vertex:POSITION; float3 normal:NORMAL; float3 uv:TEXCOORD0; };
            struct ShadowV { float4 p:SV_POSITION; float3 uv:TEXCOORD0; };
            ShadowV ShadowVert(A i)
            {
                float3 pos=TransformObjectToWorld(i.vertex.xyz);
                float3 normal=TransformObjectToWorldNormal(i.normal);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction=normalize(_LightPosition-pos);
                #else
                    float3 direction=_LightDirection;
                #endif
                ShadowV o;
                o.p=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(pos,normal,direction)));
                o.uv=i.uv;
                return o;
            }
            half4 ShadowFrag(ShadowV i):SV_Target { LeafClip(i.uv); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            struct DA { float4 p:POSITION; float3 uv:TEXCOORD0; };
            struct DV { float4 p:SV_POSITION; float3 uv:TEXCOORD0; };
            DV DepthVert(DA i) { DV o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; return o; }
            half4 DepthFrag(DV i):SV_Target { LeafClip(i.uv); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormals"}
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormVert
            #pragma fragment NormFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            struct NA { float4 p:POSITION; float3 n:NORMAL; float3 uv:TEXCOORD0; };
            struct NV { float4 p:SV_POSITION; float3 n:TEXCOORD0; float3 uv:TEXCOORD1; };
            NV NormVert(NA i)
            {
                NV o;
                o.p=TransformObjectToHClip(i.p.xyz);
                o.n=TransformObjectToWorldNormal(i.n);
                o.uv=i.uv;
                return o;
            }
            half4 NormFrag(NV i):SV_Target
            {
                LeafClip(i.uv);
                float3 normal=normalize(i.n);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct=PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                    return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
}
