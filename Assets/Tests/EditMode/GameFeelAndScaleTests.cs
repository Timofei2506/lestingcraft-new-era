using NUnit.Framework;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
using LestingCraft.Runtime;

namespace LestingCraft.Tests
{
    public sealed class GameFeelAndScaleTests
    {
        [Test]
        public void PlayerVisualStandsTwoBlocksTallAndFitsTwoBlockEyeLevel()
        {
            var root = new GameObject("TwoBlockPlayer");
            try
            {
                var vis = root.AddComponent<CharacterVisual>();
                vis.Build();
                var renderers = root.GetComponentsInChildren<MeshRenderer>();
                Assert.GreaterOrEqual(renderers.Length, 10);
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                Assert.AreEqual(0f, combined.min.y, 0.04f, "Boot soles should rest on Y = 0");
                Assert.AreEqual(2.00f, combined.max.y, 0.06f, "Character crown should stand ~2.00 blocks tall");
                Assert.AreEqual(1.78f, PlayerCapsule.EyeHeight, 0.01f);
                Assert.Less(PlayerCapsule.Height + PlayerCapsule.Skin * 2f, 2.00f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AllThirtyFourMobSculptsHaveRealisticBlockProportions()
        {
            Assert.AreEqual(34, AnimalSculpts.SpeciesNames.Length);
            Assert.AreEqual(34, AnimalSculpts.SpeciesScale.Length);

            // Small critters strictly below 1 block tall
            foreach (int small in new[] { 1, 5, 8, 9, 10, 11, 12, 13, 15, 16, 18, 25, 31, 32 })
            {
                var d = AnimalSculpts.For(small);
                Assert.Less(d.Height, 0.90f, "Small species should be under 1 block tall: " + small);
            }

            // Large beasts, towering mutants, and cosmic whale
            Assert.Greater(AnimalSculpts.For(4).Height, 1.55f, "Bear height");
            Assert.Greater(AnimalSculpts.For(6).Height, 1.95f, "Bison height");
            Assert.Greater(AnimalSculpts.For(14).Height, 2.35f, "Camel taller than 2-block player");
            Assert.Greater(AnimalSculpts.For(17).Height, 2.55f, "Moose antlers above player");
            Assert.Greater(AnimalSculpts.For(22).Depth, 2.20f, "Rad-Scorpion length");
            Assert.Greater(AnimalSculpts.For(27).Height, 2.50f, "Bone Stalker towers over player");
            Assert.Greater(AnimalSculpts.For(28).Height, 2.95f, "Trinitite Golem > 3 blocks");

            var whale = AnimalSculpts.For(33);
            Assert.Greater(whale.Height, 2.30f, "Sky Whale height");
            Assert.Greater(whale.Width, 5.00f, "Sky Whale wingspan");
            Assert.Greater(whale.Depth, 6.00f, "Sky Whale length");
        }

        [Test]
        public void ProceduralAudioSynthesizesFiniteNonSilentClipsAndCleansUp()
        {
            var host = new GameObject("AudioTestHost");
            try
            {
                var audio = GameAudio.Ensure(host.transform);
                string[] keys =
                {
                    "step_grass", "step_stone", "step_wood", "step_snow", "step_water",
                    "mine_stone", "break_stone", "place_block", "swing", "hit_flesh",
                    "bow_shot", "craft_done", "ui_click", "amb_wind", "amb_radia"
                };
                foreach (string key in keys)
                {
                    var clip = audio.GetClip(key);
                    Assert.IsNotNull(clip, "Missing synthesized clip: " + key);
                    Assert.Greater(clip.samples, 32, "Clip too short: " + key);
                    var samples = new float[clip.samples];
                    clip.GetData(samples, 0);
                    float energy = 0f;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        Assert.IsFalse(float.IsNaN(samples[i]) || float.IsInfinity(samples[i]), "Invalid sample in " + key);
                        energy += Mathf.Abs(samples[i]);
                    }
                    Assert.Greater(energy, 0.1f, "Silent clip: " + key);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AnimalHitFlashRestoresZeroAlphaOnReset()
        {
            var go = new GameObject("WolfFlashTest");
            try
            {
                var vis = go.AddComponent<AnimalVisual>();
                vis.Build(3);
                vis.TriggerHit(Vector3.back);
                vis.ResetMotion();
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    var cols = mf.sharedMesh.colors;
                    for (int i = 0; i < cols.Length; i++)
                        Assert.AreEqual(0f, cols[i].a, 0.001f);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CrouchLedgeGuardPreventsWalkingOffSingleBlockPlatform()
        {
            // Single solid stone block at (0, 10, 0); air everywhere else
            System.Func<int, int, int, ushort> world = (x, y, z) => (x == 0 && y == 10 && z == 0) ? (ushort)1 : (ushort)0;

            // Trying to step way off +X into empty air from (0.85, 11.02, 0.50) gets clamped to 0 on X, while Z inside block moves freely
            Vector3 clamped = FirstPersonController.ClampLedgeStep(new Vector3(0.85f, 11.02f, 0.50f), new Vector3(0.45f, 0f, 0.10f), 0.24f, world);
            Assert.AreEqual(0f, clamped.x, 1e-4f, "X motion over abyss must be clamped while crouching");
            Assert.AreEqual(0.10f, clamped.z, 1e-4f, "Z motion parallel to ledge should slide smoothly");
        }
    }
}
