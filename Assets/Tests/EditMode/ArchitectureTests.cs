using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.World;
namespace LestingCraft.Tests
{
    public sealed class ArchitectureTests
    {
        [Test] public void LegacySaveKeepsItsContentVersionWhenNewLocalDefaultIsEleven()
        {
            var old=new SessionSave { contentVersion=9,seed=2506 };
            var copy=JsonUtility.FromJson<SessionSave>(JsonUtility.ToJson(old));
            Assert.AreEqual(9,copy.contentVersion);
            Assert.AreEqual(11,new SessionSave().contentVersion);
            Assert.AreEqual(14,GameRules.Version,"Do not claim the unfinished network migration via a version-only bump");
        }
        [Test] public void VersionAwareChunkJobMatchesPointSamplerAcrossNewArchitectureColumns()
        {
            const int seed=2506;int found=0;
            using(var blocks=new NativeArray<ushort>(ChunkData.BlockCount,Allocator.TempJob))
            {
                // Probe actual structure columns outside the reserved story area.
                for(int z=200;z<=1200 && found<4;z+=37)
                for(int x=-1200;x<=1200 && found<4;x+=11)
                {
                    var column=ExpeditionGenerator.At(x,z,seed,11);
                    if(!column.Surface)continue;
                    int ox=(int)System.Math.Floor(x/16.0)*16,oz=(int)System.Math.Floor(z/16.0)*16;
                    var job=new GenerateChunkJob { OriginX=ox,OriginZ=oz,Seed=seed,ContentVersion=11,Dimension=Dimension.Lestingia,Blocks=blocks };
                    int lx=x-ox,lz=z-oz;job.Execute(lx+16*lz);
                    for(int y=1;y<=105;y++)Assert.AreEqual(TerrainGenerator.SampleContent(x,y,z,seed,Dimension.Lestingia,11),blocks[ChunkData.IndexOf(lx,y,lz)],"column="+x+","+z+" y="+y);
                    found++;
                }
            }
            Assert.AreEqual(4,found,"Fixture must cover real generated structure columns");
        }
        [Test] public void UnboundedLegacyHelpersStillSelectOnlyOriginalFivePlans()
        {
            for(int x=-1500;x<=1500;x+=7)
            {
                var legacy=ExpeditionGenerator.At(x,357,2506);
                Assert.IsTrue(!legacy.Surface||legacy.Kind<5);
            }
        }
    }
}
