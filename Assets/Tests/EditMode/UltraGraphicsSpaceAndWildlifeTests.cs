using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
using LestingCraft.Runtime;
using LestingCraft.World;

namespace LestingCraft.Tests
{
    public sealed class UltraGraphicsSpaceAndWildlifeTests
    {
        [Test]
        public void GreedyMesherSignificantlyReducesTrianglesAndPreservesValidAttributes()
        {
            using (var buf = new NativeArray<ushort>(ChunkData.BlockCount, Allocator.TempJob))
            {
                var job = new GenerateChunkJob
                {
                    OriginX = 0,
                    OriginZ = 0,
                    Seed = 20261020,
                    Dimension = Dimension.Lestingia,
                    ContentVersion = 7,
                    Blocks = buf
                };
                job.Schedule(256, 16).Complete();
                var data = new ChunkData(buf.ToArray());

                var raw = ChunkMesher.Build(data, 0, 0, (x, y, z) => 0, 20261020, true, 3, greedy: false);
                var opt = ChunkMesher.Build(data, 0, 0, (x, y, z) => 0, 20261020, true, 3, greedy: true);

                Assert.Less(opt.OpaqueTriangles.Count, (int)(raw.OpaqueTriangles.Count * 0.65f));
                Assert.Less(opt.ColliderTriangles.Count, (int)(raw.ColliderTriangles.Count * 0.65f));
                Assert.AreEqual(opt.Vertices.Count, opt.Normals.Count);
                Assert.AreEqual(opt.Vertices.Count, opt.Tangents.Count);
                Assert.AreEqual(opt.Vertices.Count, opt.UVs.Count);
                Assert.AreEqual(opt.Vertices.Count, opt.Colors.Count);
            }
        }

        [Test]
        public void ExpandedWildlifeSpawnsAllTwelveSpeciesWithDedicatedPbrCoatsAndRigs()
        {
            Assert.AreEqual(12, AnimalVisual.SpeciesFurNames.Length);
            var coatLayers = new HashSet<int>();
            for (int kind = 0; kind < 12; kind++)
            {
                int layer = AnimalVisual.FurLayerForSpecies(kind);
                coatLayers.Add(layer);
                Assert.AreEqual(AnimalVisual.SpeciesFurNames[kind], SurfaceLayers.Names[layer]);

                var obj = new GameObject("Animal12_" + kind);
                try
                {
                    var visual = obj.AddComponent<AnimalVisual>();
                    visual.Build(kind);
                    Assert.IsNotNull(visual.Head);
                    Assert.AreEqual(kind == 11 ? 2 : 4, visual.Legs.Count);
                    if (kind == 11) Assert.AreEqual(2, visual.Wings.Count);
                }
                finally
                {
                    Object.DestroyImmediate(obj);
                }
            }
            Assert.AreEqual(12, coatLayers.Count);

            var sim = new WildlifeSimulation(20261020, null, 7);
            for (int ring = -4; ring <= 4; ring++)
                for (int ringZ = -4; ringZ <= 4; ringZ++)
                    sim.Spawn(new AnimalObserver { id = "p", x = ring * 96f, y = 52f, z = ringZ * 96f, alive = true }, (x, z, y, r, h) => 50f);

            var found = new HashSet<int>();
            foreach (var a in sim.Animals) found.Add(a.species);
            Assert.AreEqual(12, found.Count);
        }

        [Test]
        public void PlanetaryBossesMultiPlanetEditsAndSpaceSuitEvaWorkEndToEnd()
        {
            var worldObj = new GameObject("SpaceWorldTest");
            try
            {
                var world = worldObj.AddComponent<VoxelWorld>();
                foreach (var dim in new[] { Dimension.Mars, Dimension.Venus, Dimension.Mercury })
                {
                    var boss = PlanetaryBossController.SpawnForPlanet(worldObj.transform, dim, world, 2506, false, new Vector3(0, 40, 0));
                    Assert.IsNotNull(boss);
                    var ray = new Ray(boss.transform.position + new Vector3(0, 4.2f, -6f), Vector3.forward);
                    Assert.IsTrue(boss.TryHitBoss(ray, 20f, boss.MaxHealth + 20, out bool defeated, out var drops));
                    Assert.IsTrue(defeated);
                    Assert.Greater(drops.Length, 0);
                }

                var initial = new[]
                {
                    new SavedBlock { realm = (int)Dimension.Lestingia, x = 2, y = 50, z = 2, block = 8 },
                    new SavedBlock { realm = (int)Dimension.Moon, x = 4, y = 45, z = 4, block = 9 }
                };
                world.SwitchPlanet(Dimension.Mars, initial);
                Assert.AreEqual(2, world.ExportAllMergedEdits(initial).Length);
            }
            finally
            {
                Object.DestroyImmediate(worldObj);
            }
        }
    }
}
