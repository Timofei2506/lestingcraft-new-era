using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Reproducible presentation assets on clean cloud runners, not hand-authored YAML.</summary>
public static class PresentationAssets
{
    static readonly string[] Names={"stone","dirt","grass","grass-side","bark","wood-end","leaves","sand","water","lava","bedrock","basalt","crystal","aether","ore","snow"};
    const string Folder="Assets/Resources/Generated";
    [InitializeOnLoadMethod] static void Queue(){EditorApplication.delayCall+=Ensure;}
    public static void Ensure()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Ensure;return;}
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Resources","Generated");
        var albedo=Array("Albedo","albedo",false);var normal=Array("Normal","normal",true);var mask=Array("Mask","mask",true);
        Material material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/VoxelPBR.mat");
        if(!material){material=new Material(Shader.Find("LestingCraft/VoxelPBR"));AssetDatabase.CreateAsset(material,Folder+"/VoxelPBR.mat");}
        material.SetTexture("_AlbedoArray",albedo);material.SetTexture("_NormalArray",normal);material.SetTexture("_MaskArray",mask);
        Material water=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Water.mat");
        if(!water){water=new Material(Shader.Find("LestingCraft/Water"));AssetDatabase.CreateAsset(water,Folder+"/Water.mat");}
        water.SetTexture("_AlbedoArray",albedo);water.SetTexture("_NormalArray",normal);
        var avatar=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Avatar.mat");
        if(!avatar){avatar=new Material(Shader.Find("Universal Render Pipeline/Lit"));avatar.SetColor("_BaseColor",new Color(.3f,.65f,.6f));avatar.SetFloat("_Smoothness",.4f);AssetDatabase.CreateAsset(avatar,Folder+"/Avatar.mat");}
        if(!AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Sky.mat"))AssetDatabase.CreateAsset(new Material(Shader.Find("LestingCraft/AtmosphericSky")),Folder+"/Sky.mat");
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder+"/HighDefinition.asset");
        if(!pipeline)
        {
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,Folder+"/Renderer.asset");
            pipeline=UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR=true;pipeline.msaaSampleCount=4;pipeline.renderScale=1f;
            pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=true;
            pipeline.mainLightShadowmapResolution=2048;pipeline.shadowDistance=80;pipeline.shadowCascadeCount=2;
            var serialized=new SerializedObject(pipeline);
            serialized.FindProperty("m_MainLightShadowsSupported").boolValue=true;
            serialized.FindProperty("m_SoftShadowsSupported").boolValue=true;
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(pipeline,Folder+"/HighDefinition.asset");
        }
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/WorldVolume.asset");
        if(!profile)
        {
            profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Folder+"/WorldVolume.asset");
            var bloom=profile.Add<Bloom>(true);bloom.threshold.value=1.1f;bloom.intensity.value=.22f;bloom.scatter.value=.65f;
            var tone=profile.Add<Tonemapping>(true);tone.mode.value=TonemappingMode.ACES;
            var colors=profile.Add<ColorAdjustments>(true);colors.contrast.value=7;colors.saturation.value=-5;
            var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.13f;vignette.smoothness.value=.65f;
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
        }
        EditorUtility.SetDirty(material);EditorUtility.SetDirty(water);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
    }
    static Texture2DArray Array(string assetName,string kind,bool linear)
    {
        var path=Folder+"/"+assetName+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);if(existing)return existing;
        const int size=512;
        var result=new Texture2DArray(size,size,Names.Length,TextureFormat.RGBA32,true,linear){name=assetName,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};
        for(int i=0;i<Names.Length;i++)
        {
            // Load raw PNG bytes to preserve RGB normals/packed masks without importer repacking.
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,linear);
            if(!texture.LoadImage(File.ReadAllBytes("Assets/Art/PBR/"+Names[i]+"_"+kind+".png")))throw new InvalidOperationException("Invalid material image");
            result.SetPixels32(texture.GetPixels32(),i,0);UnityEngine.Object.DestroyImmediate(texture);
        }
        result.Apply(true,true);AssetDatabase.CreateAsset(result,path);return result;
    }
}

public sealed class PresentationTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Art/PBR/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.sRGBTexture=assetPath.Contains("_albedo");
        importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
        importer.maxTextureSize=512;importer.anisoLevel=8;importer.textureCompression=TextureImporterCompression.CompressedHQ;
    }
}
