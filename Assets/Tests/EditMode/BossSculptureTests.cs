using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LestingCraft.Presentation;

namespace LestingCraft.Tests
{
    /// <summary>
    /// Guarantees the guardians stay real sculptures: the catalogue parses, every archetype that the
    /// boss roster can ask for exists, geometry is surface-culled onto the right PBR layers, and the
    /// animated parts sit on the mesh instead of drifting away from it.
    /// </summary>
    public sealed class BossSculptureTests
    {
        static readonly string[] RequiredKeys = { "warden", "colossus", "archon", "titan", "ember" };

        static string Catalogue()
        {
            var asset = Resources.Load<TextAsset>("Models/BossSculpts");
            Assert.IsNotNull(asset, "BossSculpts.json is missing from Resources/Models");
            return asset.text;
        }

        [Test]
        public void CatalogueParsesAndEveryArchetypeIsPresent()
        {
            Assert.IsTrue(BossSculpture.Validate(Catalogue(), out string error), error);
            foreach (string key in RequiredKeys)
                Assert.DoesNotThrow(() => BossSculpture.Definition(key), "missing sculpt: " + key);
        }

        [Test]
        public void SculpturesAreTallerThanThePlayerAndKeepAPalettePerMaterial()
        {
            foreach (string key in RequiredKeys)
            {
                var definition = BossSculpture.Definition(key);
                var body = BossSculpture.CreateGeometry(definition.unit, definition.cells, definition.palette, true);
                Assert.Greater(body.bounds.size.y, 4.5f, key + " must tower over a 1.8 m player");
                Assert.Less(body.bounds.size.y, 9.5f, key + " must still fit under a forest canopy");
                var layers = new HashSet<int>();
                for (int i = 0; i < definition.palette.Length; i++) layers.Add(definition.palette[i].layer);
                Assert.GreaterOrEqual(layers.Count, 4, key + " needs several distinct material layers");
                Object.DestroyImmediate(body);
            }
        }

        [Test]
        public void EverySculptureHasAnimatedPartsWithValidPivots()
        {
            foreach (string key in RequiredKeys)
            {
                var definition = BossSculpture.Definition(key);
                Assert.IsNotNull(definition.parts);
                var names = new HashSet<string>();
                foreach (var part in definition.parts) names.Add(part.name);
                Assert.IsTrue(names.Contains("head"), key + " needs a head part");
                Assert.IsTrue(names.Contains("arm_l") && names.Contains("arm_r"), key + " needs both arms");
                Assert.IsTrue(names.Contains("halo"), key + " needs a halo/crown part");
                foreach (var part in definition.parts)
                {
                    Assert.AreEqual(3, part.pivot.Length);
                    Assert.GreaterOrEqual(part.cells.Length, 4 * 4, key + "/" + part.name + " is empty");
                }
            }
        }

        [Test]
        public void GeometryIsSurfaceCulledAndUsesContinuousMetreUvs()
        {
            var definition = BossSculpture.Definition("warden");
            var mesh = BossSculpture.CreateGeometry(definition.unit, definition.cells, definition.palette, true);
            var uvs = new List<Vector3>();
            mesh.GetUVs(0, uvs);
            Assert.AreEqual(mesh.vertexCount, uvs.Count);
            float minU = float.MaxValue, maxU = float.MinValue;
            foreach (var uv in uvs)
            {
                minU = Mathf.Min(minU, uv.x);
                maxU = Mathf.Max(maxU, uv.x);
            }
            // One limb spans far more than a single 1x1 tile: geometry is wrapped, not tiled per voxel.
            Assert.Greater(maxU - minU, 2f, "UVs must run continuously across the sculpture");
            int quads = mesh.vertexCount / 4;
            int voxels = definition.cells.Length / 4;
            Assert.Less(quads, voxels * 3, "inner faces must be culled");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void SculpturesStandOnTheGroundAndExposeTheirCore()
        {
            foreach (string key in RequiredKeys)
            {
                var definition = BossSculpture.Definition(key);
                var mesh = BossSculpture.CreateGeometry(definition.unit, definition.cells, definition.palette, true);
                Assert.Less(mesh.bounds.min.y, 0.35f, key + " must not float above its arena");
                Object.DestroyImmediate(mesh);
                int core = System.Array.IndexOf(definition.anchorNames, "core");
                Assert.GreaterOrEqual(core, 0, key + " needs a weak-point anchor");
                float y = definition.anchorCells[core * 3 + 1] * definition.unit;
                Assert.Greater(y, 1.5f, key + " core must sit in the chest");
            }
        }
    }
}
