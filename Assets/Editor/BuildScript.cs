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
        PlayerSettings.companyName = "LestingCraft";
        PlayerSettings.productName = "LestingCraft New Era";
        PlayerSettings.colorSpace = ColorSpace.Linear;

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

        EditorApplication.Exit(summary.result == BuildResult.Succeeded && summary.totalErrors == 0 ? 0 : 1);
    }
}
