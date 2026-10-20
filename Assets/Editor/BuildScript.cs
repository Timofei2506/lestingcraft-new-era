using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Точки входа для облачной сборки (GitHub Actions → -executeMethod).
/// Сцены берутся из EditorBuildSettings (ProjectSettings/EditorBuildSettings.asset).
/// </summary>
public static class BuildScript
{
    private static string[] Scenes =>
        EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path)
            .ToArray();

    public static void BuildWindows64()
    {
        Build(BuildTarget.StandaloneWindows64, "Builds/StandaloneWindows64/LestingCraft.exe");
    }

    private static void Build(BuildTarget target, string outputPath)
    {
        if (Scenes.Length == 0)
        {
            Debug.LogError("BuildScript: нет сцен в EditorBuildSettings!");
            EditorApplication.Exit(1);
            return;
        }

        PresentationAssets.Ensure();
        foreach(var layer in new[]{"Albedo","Normal","Mask"})
        {
            var array=Resources.Load<Texture2DArray>("Generated/"+layer);
            if(!array||array.depth!=LestingCraft.Presentation.SurfaceLayers.Names.Length||array.width!=512||array.height!=512)
                throw new InvalidOperationException("Incomplete current PBR array: "+layer);
        }
        if(LestingCraft.Gameplay.GameRules.Version!=12)throw new InvalidOperationException("Expected rules12 creative placement test build");
        PlayerSettings.companyName = "LestingCraft";
        PlayerSettings.productName = "LestingCraft New Era";
        // Developer playtest build from the space branch; not a public release until Unity smoke passes.
        PlayerSettings.bundleVersion = "0.3.5-ground-creative-test";
        PlayerSettings.colorSpace = ColorSpace.Linear;
        var icon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Brand/AppIcon.png");
        if(!icon)throw new InvalidOperationException("Application icon missing");
        var sizes=PlayerSettings.GetIconSizes(UnityEditor.Build.NamedBuildTarget.Standalone,IconKind.Application);
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Standalone,sizes.Select(_=>icon).ToArray(),IconKind.Application);

        var options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = outputPath,
            target = target,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"BuildScript: {target} → {summary.result}, " +
                  $"size={summary.totalSize}, time={summary.totalTime}, " +
                  $"errors={summary.totalErrors}, warnings={summary.totalWarnings}");

        var storyBlood=Resources.Load<Material>("Generated/StoryBlood");
        if(!storyBlood||!storyBlood.shader||storyBlood.shader.name!="Universal Render Pipeline/Unlit")
            throw new System.Exception("Story blood must have a serialized, included player shader");
        bool shaderErrors=false;
        foreach(var name in new[]{"VoxelPBR","VoxelObjects","Water","CrystalGlass","Grass","AtmosphericSky","VolumetricAtmosphere"})
        {
            var shader=Shader.Find("LestingCraft/"+name);
            if(!shader||ShaderUtil.ShaderHasError(shader)){Debug.LogError("BuildScript: invalid shader "+name);shaderErrors=true;}
        }
        EditorApplication.Exit(summary.result == BuildResult.Succeeded && summary.totalErrors == 0 && !shaderErrors ? 0 : 1);
    }
}
