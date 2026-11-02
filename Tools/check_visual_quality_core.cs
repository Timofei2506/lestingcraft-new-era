using System;
using LestingCraft.Presentation;

public static class VisualQualityCoreCheck
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
    }

    public static int Main()
    {
        Require(VisualQualityPolicy.DefaultPreset(false) == VisualQualityPolicy.High, "desktop default is High");
        Require(VisualQualityPolicy.DefaultPreset(true) == VisualQualityPolicy.Balanced, "mobile default is Balanced");
        Require(VisualQualityPolicy.MigratePreset(0, 0) == VisualQualityPolicy.High, "legacy 0 migrates to High, not Mobile");
        Require(VisualQualityPolicy.MigratePreset(1, 0) == VisualQualityPolicy.Ultra, "legacy 1 migrates to Ultra");
        Require(VisualQualityPolicy.MigratePreset(0, 2) == VisualQualityPolicy.Mobile, "schema 2 keeps explicit Mobile");
        Require(VisualQualityPolicy.ClampPreset(VisualQualityPolicy.Cinematic, true) == VisualQualityPolicy.Ultra, "mobile cannot select Cinematic");
        Require(VisualQualityPolicy.ClampRenderScale(.2f, false) == .85f, "desktop render scale floor");
        Require(VisualQualityPolicy.ClampRenderScale(2f, false) == 1.5f, "desktop render scale ceiling");
        Require(VisualQualityPolicy.ClampRenderScale(.2f, true) == .65f, "mobile render scale floor");
        Require(VisualQualityPolicy.ClampRenderScale(2f, true) == 1f, "mobile render scale ceiling");
        Require(VisualQualityPolicy.ClampShadowResolution(4096, true) == 2048, "mobile shadow cap");
        Require(VisualQualityPolicy.ClampViewDistance(99, true) == 4, "mobile view distance cap");
        Require(VisualQualityPolicy.ClampViewDistance(99, false) == 5, "desktop view distance cap");

        var mobileUltra = VisualQualityPolicy.For(VisualQualityPolicy.Cinematic, true);
        Require(mobileUltra.ShadowResolution <= 2048, "mobile Ultra shadow cap");
        Require(mobileUltra.ShadowCascades <= 3, "mobile Ultra cascade cap");
        Require(mobileUltra.GrassRange <= 42 && mobileUltra.CloudSteps <= 24 && mobileUltra.FogSteps <= 20, "mobile Ultra raymarch/grass caps");
        Require(!mobileUltra.UltraEffects, "mobile Ultra avoids desktop-only ultra effects");
        Require(VisualQualityPolicy.For(VisualQualityPolicy.Cinematic, true).DetailQuality == VisualQualityPolicy.For(VisualQualityPolicy.Ultra, true).DetailQuality, "mobile Cinematic clamps to Ultra");

        int lastDetail = -1, lastGrass = -1, lastCloud = -1;
        for (int preset = VisualQualityPolicy.Mobile; preset <= VisualQualityPolicy.Cinematic; preset++)
        {
            var profile = VisualQualityPolicy.For(preset);
            Require(profile.DetailQuality > lastDetail, "detail budget increases with preset");
            Require(profile.GrassRange > lastGrass, "grass range increases with preset");
            Require(profile.CloudSteps > lastCloud, "cloud budget increases with preset");
            Require(profile.ShadowResolution >= 512 && profile.ShadowResolution <= 4096, "shadow resolution bounded");
            lastDetail = profile.DetailQuality;
            lastGrass = profile.GrassRange;
            lastCloud = profile.CloudSteps;
        }
        Console.WriteLine("PASS visual quality core: legacy migration, mobile caps, monotonic explicit budgets.");
        return 0;
    }
}
