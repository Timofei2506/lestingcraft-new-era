using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Reproducible presentation assets on clean cloud runners, not hand-authored YAML.</summary>
public static class PresentationAssets
{
    static readonly string[] Names=LestingCraft.Presentation.SurfaceLayers.Names;
    const string Folder="Assets/Resources/Generated";
    [InitializeOnLoadMethod] static void Queue(){EditorApplication.delayCall+=Ensure;}
    public static void Ensure()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Ensure;return;}
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Resources","Generated");
        EnsureMenuBackdrop();
        EnsureItemIcons();
        EnsureCharacterAtlas();
        // A serialized Resources material keeps URP/Unlit in actual players.
        // Shader.Find alone worked in the Editor but the Windows build stripped it.
        var storyBlood=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/StoryBlood.mat");
        if(!storyBlood){storyBlood=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(storyBlood,Folder+"/StoryBlood.mat");}
        storyBlood.shader=Shader.Find("Universal Render Pipeline/Unlit");storyBlood.SetColor("_BaseColor",new Color(.21f,.027f,.024f));EditorUtility.SetDirty(storyBlood);
        var albedo=Array("Albedo","albedo",false);var normal=Array("Normal","normal",true);var mask=Array("Mask","mask",true);
        Material material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/VoxelPBR.mat");
        if(!material){material=new Material(Shader.Find("LestingCraft/VoxelPBR"));AssetDatabase.CreateAsset(material,Folder+"/VoxelPBR.mat");}
        material.SetFloat("_NormalStrength",1.15f);
        var leafImporter=AssetImporter.GetAtPath("Assets/Resources/GeneratedLeafOpacity.png") as TextureImporter;
        if(leafImporter&&(!leafImporter.mipMapsPreserveCoverage||!leafImporter.mipmapEnabled||leafImporter.sRGBTexture||leafImporter.wrapMode!=TextureWrapMode.Repeat||leafImporter.textureCompression!=TextureImporterCompression.Uncompressed)){leafImporter.textureCompression=TextureImporterCompression.Uncompressed;leafImporter.alphaSource=TextureImporterAlphaSource.FromInput;leafImporter.sRGBTexture=false;leafImporter.mipmapEnabled=true;leafImporter.mipMapsPreserveCoverage=true;leafImporter.alphaTestReferenceValue=.5f;leafImporter.wrapMode=TextureWrapMode.Repeat;leafImporter.filterMode=FilterMode.Bilinear;leafImporter.SaveAndReimport();}
        material.SetTexture("_LeafOpacity",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/GeneratedLeafOpacity.png"));
        material.SetTexture("_AlbedoArray",albedo);material.SetTexture("_NormalArray",normal);material.SetTexture("_MaskArray",mask);
        foreach(string clearName in new[]{"Crystal","Glass"})
        {
            var clear=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+clearName+".mat");if(!clear){clear=new Material(Shader.Find("LestingCraft/CrystalGlass"));AssetDatabase.CreateAsset(clear,Folder+"/"+clearName+".mat");}
            clear.SetTexture("_NormalArray",normal);clear.SetColor("_Tint",clearName=="Crystal"?new Color(.78f,.68f,.98f,1):new Color(.76f,.90f,.95f,1));clear.SetFloat("_Density",clearName=="Crystal"?.09f:.16f);clear.SetFloat("_Sparkle",clearName=="Crystal"?3:0);clear.SetFloat("_GlassEdge",clearName=="Glass"?.42f:0);EditorUtility.SetDirty(clear);
        }
        Material water=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Water.mat");
        if(!water){water=new Material(Shader.Find("LestingCraft/Water"));AssetDatabase.CreateAsset(water,Folder+"/Water.mat");}
        water.SetTexture("_NormalArray",normal);
        var grass=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Grass.mat");
        if(!grass){grass=new Material(Shader.Find("LestingCraft/Grass"));AssetDatabase.CreateAsset(grass,Folder+"/Grass.mat");}
        grass.SetTexture("_AlbedoArray",albedo);EditorUtility.SetDirty(grass);
        var objectMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/VoxelObjects.mat");
        if(!objectMaterial){objectMaterial=new Material(Shader.Find("LestingCraft/VoxelObjects"));AssetDatabase.CreateAsset(objectMaterial,Folder+"/VoxelObjects.mat");}
        objectMaterial.SetTexture("_LeafOpacity",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/GeneratedLeafOpacity.png"));objectMaterial.SetTexture("_AlbedoArray",albedo);objectMaterial.SetTexture("_NormalArray",normal);objectMaterial.SetTexture("_MaskArray",mask);EditorUtility.SetDirty(objectMaterial);
        var avatar=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Avatar.mat");
        if(!avatar){avatar=new Material(Shader.Find("Universal Render Pipeline/Lit"));avatar.SetColor("_BaseColor",new Color(.3f,.65f,.6f));avatar.SetFloat("_Smoothness",.4f);AssetDatabase.CreateAsset(avatar,Folder+"/Avatar.mat");}
        avatar.shader=Shader.Find("LestingCraft/AvatarLit");EditorUtility.SetDirty(avatar);
        if(!AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Sky.mat"))AssetDatabase.CreateAsset(new Material(Shader.Find("LestingCraft/AtmosphericSky")),Folder+"/Sky.mat");
        if(!AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Atmosphere.mat"))AssetDatabase.CreateAsset(new Material(Shader.Find("LestingCraft/VolumetricAtmosphere")),Folder+"/Atmosphere.mat");
        var cloud=AssetDatabase.LoadAssetAtPath<Texture3D>(Folder+"/CloudNoise.asset");
        if(cloud&&cloud.name!="CloudNoise-v2"){AssetDatabase.DeleteAsset(Folder+"/CloudNoise.asset");cloud=null;}
        if(!cloud)
        {
            const int n=64;cloud=new Texture3D(n,n,n,TextureFormat.RGBA32,false){name="CloudNoise-v2",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            var pixels=new Color[n*n*n];
            for(int z=0;z<n;z++)for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float value=LestingCraft.Presentation.CloudField.Shape((float)x/n,(float)y/n,(float)z/n);
                pixels[x+n*(y+n*z)]=new Color(value,0,0,1);
            }
            cloud.SetPixels(pixels);cloud.Apply(false,true);AssetDatabase.CreateAsset(cloud,Folder+"/CloudNoise.asset");
        }
        var sky=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Sky.mat");sky.SetTexture("_CloudNoise",cloud);sky.SetFloat("_CloudBase",144);EditorUtility.SetDirty(sky);
        var rain=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/RainStreaks.mat");
        if(!rain){rain=new Material(Shader.Find("LestingCraft/Precipitation"));AssetDatabase.CreateAsset(rain,Folder+"/RainStreaks.mat");}
        ParticleMaterial("Precipitation",false);ParticleMaterial("Fire",true);EnsureSmokeMaterial();
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder+"/HighDefinition.asset");
        if(!pipeline)
        {
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,Folder+"/Renderer.asset");
            pipeline=UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline,Folder+"/HighDefinition.asset");
        }
        EnsureAmbientOcclusion();
        // Reapply on every Ensure, including an existing Generated directory/cache.
        pipeline.supportsHDR=true;pipeline.msaaSampleCount=4;pipeline.renderScale=1f;
        pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=true;
        pipeline.maxAdditionalLightsCount=8;pipeline.mainLightShadowmapResolution=2048;pipeline.shadowDistance=80;pipeline.shadowCascadeCount=2;
        var serialized=new SerializedObject(pipeline);
        serialized.FindProperty("m_MainLightShadowsSupported").boolValue=true;
        serialized.FindProperty("m_SoftShadowsSupported").boolValue=true;
        serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue=true;
        serialized.FindProperty("m_AdditionalLightsShadowmapResolution").intValue=2048;
        serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;
        serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/WorldVolume.asset");
        if(!profile)
        {
            profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Folder+"/WorldVolume.asset");
        }
        // Graphics v3: stronger bloom for lava/crystal/campfire, warm-cool grading, subtle film texture.
        var bloom=Component<Bloom>(profile);bloom.threshold.value=1.05f;bloom.intensity.value=.26f;bloom.scatter.value=.72f;
        var tone=Component<Tonemapping>(profile);tone.mode.value=TonemappingMode.ACES;
        var colors=Component<ColorAdjustments>(profile);colors.contrast.value=9;colors.saturation.value=7;colors.postExposure.value=.08f;
        var split=Component<SplitToning>(profile);split.shadows.value=new Color(.36f,.48f,.56f);split.highlights.value=new Color(1f,.84f,.66f);split.balance.value=-8f;
        var lift=Component<LiftGammaGain>(profile);lift.lift.value=new Vector4(-.012f,-.004f,.010f,0f);lift.gain.value=new Vector4(1.03f,1.0f,.97f,0f);
        var grain=Component<FilmGrain>(profile);grain.type.value=FilmGrainLookup.Medium1;grain.intensity.value=.10f;grain.response.value=.8f;
        var aberration=Component<ChromaticAberration>(profile);aberration.intensity.value=.05f;
        var vignette=Component<Vignette>(profile);vignette.intensity.value=.14f;vignette.smoothness.value=.70f;
        foreach(var component in profile.components)EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(material);EditorUtility.SetDirty(water);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
    }
    static void EnsureAmbientOcclusion()
    {
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Folder+"/Renderer.asset");
        ScreenSpaceAmbientOcclusion feature=null;
        foreach(var candidate in renderer.rendererFeatures)if(candidate is ScreenSpaceAmbientOcclusion ao)feature=ao;
        if(!feature){feature=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();feature.name="Contact SSAO";AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);}
        var data=new SerializedObject(feature);var settings=data.FindProperty("m_Settings");
        settings.FindPropertyRelative("Source").intValue=0; // Depth; normals reconstructed, not a missing DepthNormals pass.
        settings.FindPropertyRelative("NormalSamples").intValue=2;
        settings.FindPropertyRelative("AOMethod").intValue=1; // Interleaved gradient, no missing noise texture dependency.
        settings.FindPropertyRelative("Downsample").boolValue=true;
        settings.FindPropertyRelative("AfterOpaque").boolValue=false; // Correct direct/indirect lighting integration, not a black overlay.
        settings.FindPropertyRelative("Intensity").floatValue=.85f;settings.FindPropertyRelative("Radius").floatValue=.23f;
        settings.FindPropertyRelative("DirectLightingStrength").floatValue=.18f;settings.FindPropertyRelative("Falloff").floatValue=65;
        settings.FindPropertyRelative("Samples").intValue=1;settings.FindPropertyRelative("BlurQuality").intValue=0;
        data.ApplyModifiedPropertiesWithoutUndo();feature.SetActive(true);feature.Create();
        EditorUtility.SetDirty(feature);EditorUtility.SetDirty(renderer);
    }
    static void EnsureSmokeMaterial()
    {
        const string path=Folder+"/Smoke.mat";if(AssetDatabase.LoadAssetAtPath<Material>(path))return;
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false,true){name="Smoke soft turbulence",wrapMode=TextureWrapMode.Clamp};
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {float r=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/31.5f;float noise=Mathf.PerlinNoise(x*.11f+9,y*.11f+3)*.65f+Mathf.PerlinNoise(x*.29f,y*.29f)*.35f;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r*r),1.7f)*Mathf.Lerp(.2f,1,noise)));}
        texture.Apply();AssetDatabase.CreateAsset(texture,Folder+"/SmokeNoise.asset");
        var material=new Material(Shader.Find("LestingCraft/SmokeParticles"));material.SetTexture("_BaseMap",texture);AssetDatabase.CreateAsset(material,path);
    }
    static void EnsureCharacterAtlas()
    {
        const string path=Folder+"/BlockCharacter.asset";
        string version="BlockCharacter-"+AssetDatabase.GetAssetDependencyHash("Assets/Art/Characters/BlockCharacter.png").ToString();var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing&&existing.name==version)return;if(existing)AssetDatabase.DeleteAsset(path);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name=version,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Point};
        if(!texture.LoadImage(File.ReadAllBytes("Assets/Art/Characters/BlockCharacter.png"),false))throw new InvalidOperationException("Character atlas decode failed");
        AssetDatabase.CreateAsset(texture,path);
    }
    /// <summary>Test diagnostics: why the leaf opacity texture may not be bound on Generated/VoxelPBR.</summary>
    public static string LeafDiagnostics()
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/VoxelPBR.mat");
        var png=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/GeneratedLeafOpacity.png");
        return "updating="+EditorApplication.isUpdating+" compiling="+EditorApplication.isCompiling
            +" material="+(material?"ok":"null")+" materialLeaf="+(material&&material.GetTexture("_LeafOpacity")?"ok":"null")
            +" png="+(png?"ok":"null")+" shader="+(material&&material.shader?material.shader.name:"null");
    }
    static void EnsureItemIcons()
    {
        if(!AssetDatabase.IsValidFolder(Folder+"/Icons"))AssetDatabase.CreateFolder(Folder,"Icons");
        foreach(int id in LestingCraft.Presentation.ItemArtwork.AllIds)
        {
            string path=Folder+"/Icons/Item"+id+".asset";
            string source="Assets/Resources/VoxelIcons/Item"+id+".png";
            string version="Item"+id+"-"+AssetDatabase.GetAssetDependencyHash(source).ToString();var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing&&existing.name==version)continue;if(existing)AssetDatabase.DeleteAsset(path);
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name=version,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            if(!texture.LoadImage(File.ReadAllBytes(source),false))throw new InvalidOperationException("Cannot decode item icon: "+source);
            AssetDatabase.CreateAsset(texture,path);
        }
    }
    static void EnsureMenuBackdrop()
    {
        const string path=Folder+"/MenuBackdrop.asset";
        if(AssetDatabase.LoadAssetAtPath<Texture2D>(path))return;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name="MenuBackdrop",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        if(!texture.LoadImage(File.ReadAllBytes("Assets/Resources/UI/MenuBackground.jpg"),false))throw new InvalidOperationException("Cannot decode menu backdrop");
        AssetDatabase.CreateAsset(texture,path);
    }
    static T Component<T>(VolumeProfile profile) where T:VolumeComponent
    {
        if(profile.TryGet<T>(out var component))return component;
        component=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(component,profile);return component;
    }
    static void ParticleMaterial(string name,bool additive)
    {
        var path=Folder+"/"+name+".mat";if(AssetDatabase.LoadAssetAtPath<Material>(path))return;
        var texture=new Texture2D(32,32,TextureFormat.RGBA32,false,true);
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/16;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1-r*r)*Mathf.Clamp01(1-r)));}
        texture.Apply();AssetDatabase.CreateAsset(texture,Folder+"/"+name+"Glow.asset");
        var material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));material.SetTexture("_BaseMap",texture);material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",additive?1:10);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;
        AssetDatabase.CreateAsset(material,path);
    }
    static string MapPath(string name,string kind){string root="Assets/Art/PBR/"+name+"_"+kind;return File.Exists(root+".jpg")?root+".jpg":root+".png";}
    static Texture2DArray Array(string assetName,string kind,bool linear)
    {
        var path=Folder+"/"+assetName+".asset";
        string fingerprint;using(var hash=SHA256.Create()){using(var stream=new MemoryStream()){foreach(var name in Names){var bytes=File.ReadAllBytes(MapPath(name,kind));stream.Write(bytes,0,bytes.Length);}fingerprint=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").Substring(0,16);}}
        string version=assetName+"-v4-"+fingerprint;
        var existing=AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);if(existing&&existing.depth==Names.Length&&existing.name==version)return existing;if(existing)AssetDatabase.DeleteAsset(path);
        const int size=512;
        var result=new Texture2DArray(size,size,Names.Length,TextureFormat.RGBA32,true,linear){name=version,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};
        for(int i=0;i<Names.Length;i++)
        {
            // Load raw PNG bytes to preserve RGB normals/packed masks without importer repacking.
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,linear);
            if(!texture.LoadImage(File.ReadAllBytes(MapPath(Names[i],kind))))throw new InvalidOperationException("Invalid material image");
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
