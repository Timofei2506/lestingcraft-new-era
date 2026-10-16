using NUnit.Framework;using Unity.Collections;using UnityEngine;using LestingCraft.World;using LestingCraft.Gameplay;
namespace LestingCraft.Tests
{
 public sealed class NatureLightingTests
 {
  static void Drain(VoxelSkylight s){for(int n=0;n<200&&s.Pending>0;n++)s.Process(65536);Assert.AreEqual(0,s.Pending);}
  [Test] public void ClosedCaveIsDarkOpeningLightsItAndClosingRestoresDarkness()
  {var c=new ChunkData();for(int y=0;y<13;y++)for(int z=0;z<16;z++)for(int x=0;x<16;x++)c.SetBlock(x,y,z,1);for(int y=4;y<12;y++)for(int z=4;z<12;z++)for(int x=4;x<12;x++)c.SetBlock(x,y,z,0);var s=new VoxelSkylight();s.Register(0,0,c);Drain(s);Assert.AreEqual(0,s.Get(8,8,8));Assert.AreEqual(15,s.Get(8,16,8));c.SetBlock(8,12,8,0);s.Edit(8,12,8);Drain(s);Assert.AreEqual(15,s.Get(8,8,8));Assert.Greater(s.Get(5,8,5),0);c.SetBlock(8,12,8,1);s.Edit(8,12,8);Drain(s);Assert.AreEqual(0,s.Get(8,8,8));}
  [Test] public void OpaqueFacesRemainVisibleBehindLeafHolesButLeafInteriorsAreCulled()
  {var c=new ChunkData();c.SetBlock(3,4,3,1);c.SetBlock(4,4,3,5);var m=ChunkMesher.Build(c,0,0,(x,y,z)=>0);Assert.AreEqual(11,m.FaceCount);Assert.AreEqual(66,m.ColliderTriangles.Count);c.SetBlock(3,4,3,5);Assert.AreEqual(10,ChunkMesher.Build(c,0,0,(x,y,z)=>0).FaceCount);}
  [Test] public void SkylightFallbackUsesGreenAndKeepsAmbientOcclusionInRed()
  {var c=new ChunkData();c.SetBlock(3,4,3,1);var m=ChunkMesher.Build(c,0,0,(x,y,z)=>0,0,false,0,(x,y,z)=>0);foreach(var color in m.Colors){Assert.AreEqual(0,color.g);Assert.Greater(color.r,0);}}
  [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] public void ColumnJobMatchesVersionedSampler(int content)
  {using(var blocks=new NativeArray<ushort>(ChunkData.BlockCount,Allocator.TempJob)){var job=new GenerateChunkJob{OriginX=-16,OriginZ=-16,Seed=2506,Dimension=Dimension.Lestingia,ContentVersion=content,Blocks=blocks};for(int i=0;i<256;i++)job.Execute(i);for(int x=0;x<16;x+=3)for(int z=0;z<16;z+=3)for(int y=0;y<110;y++)Assert.AreEqual(TerrainGenerator.SampleContent(x-16,y,z-16,2506,Dimension.Lestingia,content),blocks[ChunkData.IndexOf(x,y,z)]);}}
  [Test] public void OtherDimensionsAndLegacyContentRetainTheirSamplers()
  {for(int d=1;d<5;d++)for(int y=0;y<130;y++)Assert.AreEqual(TerrainGenerator.SampleContent(-7,y,9,2506,(Dimension)d,2),TerrainGenerator.SampleContent(-7,y,9,2506,(Dimension)d,3));Assert.AreEqual(1,TerrainGenerator.Version);Assert.AreEqual(6,new SessionSave().contentVersion);Assert.IsTrue(GameRules.AcceptsRoom(5,GameRules.Version));Assert.IsTrue(GameRules.AcceptsRoom(6,GameRules.Version));}
  [Test] public void OldSavedWorldKeepsGenerationAfterLoadStore()
  {const string key="LC.Session.V3";bool had=PlayerPrefs.HasKey(key);string previous=PlayerPrefs.GetString(key);try{PlayerPrefs.SetString(key,"{\"schema\":3,\"contentVersion\":2,\"seed\":2506,\"mode\":0,\"player\":{}}");var loaded=LocalSave.Load();Assert.IsNotNull(loaded);Assert.AreEqual(2,loaded.contentVersion);LocalSave.Store(loaded);Assert.AreEqual(2,LocalSave.Load().contentVersion);}finally{if(had)PlayerPrefs.SetString(key,previous);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}}
  [Test] public void LeafCutoutIsBoundAndHasForwardShadowDepthAndNormalPasses()
  {var m=Resources.Load<Material>("Generated/VoxelPBR");Assert.IsNotNull(m.GetTexture("_LeafOpacity"));foreach(var pass in new[]{"ForwardLit","ShadowCaster","DepthOnly","DepthNormals"})Assert.GreaterOrEqual(m.FindPass(pass),0);}
 }
}
