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
        EnsureMenuBackdrop();
        EnsureItemIcons();
        EnsureCharacterAtlas();
        var albedo=Array("Albedo","albedo",false);var normal=Array("Normal","normal",true);var mask=Array("Mask","mask",true);
        Material material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/VoxelPBR.mat");
        if(!material){material=new Material(Shader.Find("LestingCraft/VoxelPBR"));AssetDatabase.CreateAsset(material,Folder+"/VoxelPBR.mat");}
        material.SetFloat("_NormalStrength",1.15f);
        material.SetTexture("_AlbedoArray",albedo);material.SetTexture("_NormalArray",normal);material.SetTexture("_MaskArray",mask);
        Material water=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Water.mat");
        if(!water){water=new Material(Shader.Find("LestingCraft/Water"));AssetDatabase.CreateAsset(water,Folder+"/Water.mat");}
        water.SetTexture("_NormalArray",normal);
        var grass=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Grass.mat");
        if(!grass){grass=new Material(Shader.Find("LestingCraft/Grass"));AssetDatabase.CreateAsset(grass,Folder+"/Grass.mat");}
        grass.SetTexture("_AlbedoArray",albedo);EditorUtility.SetDirty(grass);
        var objectMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/VoxelObjects.mat");
        if(!objectMaterial){objectMaterial=new Material(Shader.Find("LestingCraft/VoxelObjects"));AssetDatabase.CreateAsset(objectMaterial,Folder+"/VoxelObjects.mat");}
        objectMaterial.SetTexture("_AlbedoArray",albedo);objectMaterial.SetTexture("_NormalArray",normal);objectMaterial.SetTexture("_MaskArray",mask);EditorUtility.SetDirty(objectMaterial);
        var avatar=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Avatar.mat");
        if(!avatar){avatar=new Material(Shader.Find("Universal Render Pipeline/Lit"));avatar.SetColor("_BaseColor",new Color(.3f,.65f,.6f));avatar.SetFloat("_Smoothness",.4f);AssetDatabase.CreateAsset(avatar,Folder+"/Avatar.mat");}
        if(!AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Sky.mat"))AssetDatabase.CreateAsset(new Material(Shader.Find("LestingCraft/AtmosphericSky")),Folder+"/Sky.mat");
        if(!AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Atmosphere.mat"))AssetDatabase.CreateAsset(new Material(Shader.Find("LestingCraft/VolumetricAtmosphere")),Folder+"/Atmosphere.mat");
        var cloud=AssetDatabase.LoadAssetAtPath<Texture3D>(Folder+"/CloudNoise.asset");
        if(!cloud)
        {
            const int n=64;cloud=new Texture3D(n,n,n,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            var pixels=new Color[n*n*n];
            for(int z=0;z<n;z++)for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                // Periodic smooth 3D value field from trigonometric domain warping, no seams at wrapping.
                float a=x*2*Mathf.PI/n,b=y*2*Mathf.PI/n,c=z*2*Mathf.PI/n;
                float v=.5f+.19f*Mathf.Sin(a+Mathf.Sin(b))+.14f*Mathf.Cos(b+Mathf.Sin(c))+.12f*Mathf.Sin(c+Mathf.Cos(a))+.06f*Mathf.Sin(a*3+b*2-c);
                pixels[x+n*(y+n*z)]=new Color(Mathf.Clamp01(v),0,0,1);
            }
            cloud.SetPixels(pixels);cloud.Apply(false,true);AssetDatabase.CreateAsset(cloud,Folder+"/CloudNoise.asset");
        }
        var sky=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Sky.mat");sky.SetTexture("_CloudNoise",cloud);sky.SetFloat("_CloudBase",144);EditorUtility.SetDirty(sky);
        ParticleMaterial("Precipitation",false);ParticleMaterial("Fire",true);
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder+"/HighDefinition.asset");
        if(!pipeline)
        {
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,Folder+"/Renderer.asset");
            pipeline=UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline,Folder+"/HighDefinition.asset");
        }
        // Reapply on every Ensure, including an existing Generated directory/cache.
        pipeline.supportsHDR=true;pipeline.msaaSampleCount=4;pipeline.renderScale=1f;
        pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=true;
        pipeline.maxAdditionalLightsCount=8;pipeline.mainLightShadowmapResolution=2048;pipeline.shadowDistance=80;pipeline.shadowCascadeCount=2;
        var serialized=new SerializedObject(pipeline);
        serialized.FindProperty("m_MainLightShadowsSupported").boolValue=true;
        serialized.FindProperty("m_SoftShadowsSupported").boolValue=true;
        serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue=(int)LightRenderingMode.PerPixel;
        serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/WorldVolume.asset");
        if(!profile)
        {
            profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Folder+"/WorldVolume.asset");
        }
        var bloom=Component<Bloom>(profile);bloom.threshold.value=1.6f;bloom.intensity.value=.055f;bloom.scatter.value=.45f;
        var tone=Component<Tonemapping>(profile);tone.mode.value=TonemappingMode.ACES;
        var colors=Component<ColorAdjustments>(profile);colors.contrast.value=5;colors.saturation.value=-3;
        var vignette=Component<Vignette>(profile);vignette.intensity.value=.09f;vignette.smoothness.value=.65f;
        foreach(var component in profile.components)EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(material);EditorUtility.SetDirty(water);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
    }
    static void EnsureCharacterAtlas()
    {
        const string path=Folder+"/BlockCharacter.asset";
        if(AssetDatabase.LoadAssetAtPath<Texture2D>(path))return;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name="Block character detailed atlas",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Point};
        if(!texture.LoadImage(File.ReadAllBytes("Assets/Art/Characters/BlockCharacter.png"),false))throw new InvalidOperationException("Character atlas decode failed");
        AssetDatabase.CreateAsset(texture,path);
    }
    static void EnsureItemIcons()
    {
        if(!AssetDatabase.IsValidFolder(Folder+"/Icons"))AssetDatabase.CreateFolder(Folder,"Icons");
        foreach(int id in LestingCraft.Presentation.ItemArtwork.AllIds)
        {
            string path=Folder+"/Icons/Item"+id+".asset";
            if(AssetDatabase.LoadAssetAtPath<Texture2D>(path))continue;
            string source="Assets/Resources/VoxelIcons/Item"+id+".png";
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name="Item"+id,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
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
