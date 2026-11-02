using System.IO;
using NUnit.Framework;
using UnityEngine;
using LestingCraft.Presentation;
using LestingCraft.World;

namespace LestingCraft.Tests
{
    public sealed class VisualQualityTests
    {
        [Test]
        public void LegacyPreferencesMigrateWithoutDowngradingDesktopToMobile()
        {
            var legacy = VisualSettings.FromJson("{\"Preset\":0,\"PostEffects\":true}");
            Assert.AreEqual(VisualQualityPolicy.High, legacy.Preset);
            Assert.AreEqual(VisualQualityPolicy.SchemaVersion, legacy.SchemaVersion);
            var ultra = VisualSettings.FromJson("{\"Preset\":1}");
            Assert.AreEqual(VisualQualityPolicy.Ultra, ultra.Preset);
            var explicitMobile = VisualSettings.FromJson("{\"SchemaVersion\":2,\"Preset\":0}");
            Assert.AreEqual(VisualQualityPolicy.Mobile, explicitMobile.Preset);
        }

        [Test]
        public void GrassBudgetsAreDeterministicMonotonicAndNeverAddCollision()
        {
            int previous = 0;
            for (int quality = 0; quality <= 4; quality++)
            {
                var a = new ChunkMeshData();
                var b = new ChunkMeshData();
                for (int x = 0; x < 8; x++)
                {
                    SurfaceDetailMesher.AddGrass(a, x, 10, 0, 0, 0, 2506, quality);
                    SurfaceDetailMesher.AddGrass(b, x, 10, 0, 0, 0, 2506, quality);
                }
                CollectionAssert.AreEqual(a.Vertices, b.Vertices);
                Assert.AreEqual(0, a.ColliderTriangles.Count);
                Assert.Greater(a.Vertices.Count, previous);
                previous = a.Vertices.Count;
            }
        }

        [Test]
        public void FoliageShaderHasBoundedSkyBounceAndCanopyNormalRelaxation()
        {
            string shader = File.ReadAllText(Path.Combine(Application.dataPath, "Resources/VoxelPBR.shader"));
            Assert.IsTrue(shader.Contains("canopySky"));
            Assert.IsTrue(shader.Contains("_EnvironmentDaylight"));
            Assert.IsTrue(shader.Contains("if(foliage)normalTS.xy*=.55"));
            string grass = File.ReadAllText(Path.Combine(Application.dataPath, "Resources/Grass.shader"));
            Assert.IsTrue(grass.Contains("_EnvironmentDaylight"));
        }

        [Test]
        public void NaturalWorldGradeIsSharedAndSubtle()
        {
            Assert.LessOrEqual(VisualSettings.BloomIntensity, .5f);
            Assert.GreaterOrEqual(VisualSettings.BloomThreshold, .85f);
            Assert.LessOrEqual(VisualSettings.GradeVignette, .15f);
            Assert.IsFalse(VisualSettings.PipelineRayTracing);
        }
    }
}
