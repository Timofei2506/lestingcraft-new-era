// Adapted from Unity Graphics URP Lighting.hlsl. Copyright (c) 2020 Unity Technologies ApS.
// Unity Companion License for Unity-dependent projects; see Docs/ThirdParty/Unity-Graphics-LICENSE.md.
// GI/reflections and sun only are gated; additional fire lights and emission remain local.
#ifndef LC_VOXEL_LIGHTING_INCLUDED
#define LC_VOXEL_LIGHTING_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "VoxelSky.hlsl"
#include "EnvironmentLighting.hlsl"
half4 LC_FragmentPBR(InputData inputData, SurfaceData surfaceData,half sky)
{
    #if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
    #else
    bool specularHighlightsOff = false;
    #endif
    BRDFData brdfData;

    // NOTE: can modify "surfaceData"...
    InitializeBRDFData(surfaceData, brdfData);

    #if defined(DEBUG_DISPLAY)
    half4 debugColor;

    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
    #endif

    // Clear-coat calculation...
    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
    // Shadow maps (including leaf cutout) decide direct transmission.
    // Sky presence only prevents far/unshadowed directional light leaking into sealed caves.
    mainLight.color*=step(.001,sky);

    // NOTE: We don't apply AO to the GI here because it's done in the lighting calculation below...
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    LightingData lightingData = CreateLightingData(inputData, surfaceData);

    lightingData.giColor = GlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
                                              inputData.bakedGI, aoFactor.indirectAmbientOcclusion, inputData.positionWS,
                                              inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
    lightingData.giColor*=LC_SkyEnergy(sky);
    // Analytic fallback reflection when no authored reflection probe exists.
    // Rough surfaces receive a broad sky/ground lobe, smooth materials a directional one.
    half3 reflection = reflect(-inputData.viewDirectionWS, inputData.normalWS);
    half roughness = 1 - surfaceData.smoothness;
    half3 ground = _EnvironmentHorizon.rgb * half3(.22,.20,.17);
    half3 environment = lerp(ground, LC_SkyRadiance(reflection), smoothstep(-.12,.12,reflection.y));
    half3 broad = lerp(ground, _EnvironmentHorizon.rgb, saturate(inputData.normalWS.y*.5+.5));
    environment = lerp(environment, broad, roughness*roughness);
    half nv = saturate(dot(inputData.normalWS,inputData.viewDirectionWS));
    half3 fresnel = brdfData.specular + (1-brdfData.specular)*pow(1-nv,5);
    lightingData.giColor += environment*fresnel*surfaceData.occlusion*LC_SkyEnergy(sky)*.45;

#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = LightingPhysicallyBased(brdfData, brdfDataClearCoat,
                                                              mainLight,
                                                              inputData.normalWS, inputData.viewDirectionWS,
                                                              surfaceData.clearCoatMask, specularHighlightsOff);
    }

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, brdfDataClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          surfaceData.clearCoatMask, specularHighlightsOff);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);

#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            lightingData.additionalLightsColor += LightingPhysicallyBased(brdfData, brdfDataClearCoat, light,
                                                                          inputData.normalWS, inputData.viewDirectionWS,
                                                                          surfaceData.clearCoatMask, specularHighlightsOff);
        }
    LIGHT_LOOP_END
    #endif

    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
    #endif

#if REAL_IS_HALF
    // Clamp any half.inf+ to HALF_MAX
    return min(CalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return CalculateFinalColor(lightingData, surfaceData.alpha);
#endif

}
#endif
