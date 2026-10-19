using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LestingCraft.World;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
namespace LestingCraft.Tests
{
 public sealed class ExpandedWildlifeTests
 {
  static float Flat(float x,float z,float y,float radius,float height)=>50;
  [Test] public void ExpandedCatalogHas30PlaceableMinedAndTexturedBlocks()
  {Assert.AreEqual(35,ExpandedContent.Blocks.Length);foreach(var b in ExpandedContent.Blocks){Assert.AreEqual(b.Id,Items.Get(b.Id).Block);Assert.AreEqual(b.Name,SurfaceLayers.DisplayName((ushort)b.Id));Assert.AreEqual(b.Layer,SurfaceLayers.ForFace((ushort)b.Id,0));Assert.IsTrue(BlockRegistry.IsSolid((ushort)b.Id));Assert.IsNotNull(ItemArtwork.Get(b.Id));}}
  [Test] public void CoalOreHasActualCoalDropAndAllSevenMineralsSmelt()
  {Assert.AreEqual(147,Items.ToolDrop(19));Assert.AreEqual(40,FurnaceState.FuelSeconds(147));for(int id=20;id<=26;id++)Assert.AreEqual(id+120,FurnaceState.Result(id));Assert.AreEqual(44,FurnaceState.Result(6));Assert.AreEqual(39,FurnaceState.Result(33));Assert.AreEqual(138,FurnaceState.Result(135));}
  [Test] public void LegacyGenerationFlagPreservesEverySample()
  {for(int d=0;d<1;d++)for(int x=-17;x<=17;x+=5)for(int y=0;y<100;y+=7)for(int z=-15;z<18;z+=6)Assert.AreEqual(TerrainGenerator.Sample(x,y,z,2506,(Dimension)d),TerrainGenerator.SampleContent(x,y,z,2506,(Dimension)d,1));}
  [Test] public void NewGeneratorUsesRealOreIdsAndNeverTurnsBedrockIntoOre()
  {var found=new HashSet<ushort>();for(int x=-32;x<32;x++)for(int y=0;y<44;y++)found.Add(TerrainGenerator.SampleContent(x,y,7,2506,Dimension.Lestingia,2));Assert.IsTrue(found.Contains(19));Assert.IsTrue(found.Contains(20));for(int x=-20;x<20;x++)Assert.AreEqual(9,TerrainGenerator.SampleContent(x,0,3,2506,Dimension.Lestingia,2));}
  [Test] public void SaveV3RoundTripsAnimalsAndLootClaimsWithNewInventory()
  {var s=new SessionSave();s.player.inventory.Add(46,19);s.player.claimedAnimalLoot=new[]{"a:0:0"};s.wildlife.animals=new[]{new AnimalState{id="a:0:0",species=2,health=0,dead=true,x=1,y=50,z=2}};var copy=JsonUtility.FromJson<SessionSave>(JsonUtility.ToJson(s));copy.player.Normalize();Assert.AreEqual(3,copy.schema);Assert.AreEqual(7,copy.contentVersion);Assert.AreEqual(19,copy.player.inventory.Count(46));Assert.IsTrue(copy.wildlife.animals[0].dead);Assert.AreEqual("a:0:0",copy.player.claimedAnimalLoot[0]);}
  [Test] public void ExplicitSchemaMigrationDoesNotReinterpretLegacyTerrain()
  {const string key="LC.Session.V2",newKey="LC.Session.V3";bool hadOld=PlayerPrefs.HasKey(key),hadNew=PlayerPrefs.HasKey(newKey);string old=PlayerPrefs.GetString(key),next=PlayerPrefs.GetString(newKey);try{PlayerPrefs.DeleteKey(newKey);PlayerPrefs.SetString(key,"{\"schema\":2,\"seed\":2506,\"mode\":0,\"player\":{}}");var loaded=LocalSave.Load();Assert.IsNotNull(loaded);Assert.AreEqual(1,loaded.contentVersion);Assert.AreEqual(3,loaded.schema);LocalSave.Store(loaded);Assert.IsTrue(PlayerPrefs.HasKey(key));Assert.AreEqual(1,LocalSave.Load().contentVersion);}finally{if(hadOld)PlayerPrefs.SetString(key,old);else PlayerPrefs.DeleteKey(key);if(hadNew)PlayerPrefs.SetString(newKey,next);else PlayerPrefs.DeleteKey(newKey);PlayerPrefs.Save();}}
  [Test] public void WildlifeSpawnsAllSpeciesOnceAndPersistsDeadAnimals()
  {var sim=new WildlifeSimulation(2506);var p=new AnimalObserver{id="p",x=0,y=51.62f,z=0,alive=true};sim.Spawn(p,Flat);var species=new HashSet<int>();foreach(var a in sim.Animals)species.Add(a.species);Assert.AreEqual(3,species.Count);int count=sim.Animals.Count;var a0=sim.Animals[0];a0.dead=true;a0.health=0;var copy=new WildlifeSimulation(2506,sim.Save());copy.Spawn(p,Flat);Assert.AreEqual(count,copy.Animals.Count);Assert.IsTrue(copy.Animals[0].dead);}
  [Test] public void TransparentCrystalAndGlassUseDedicatedShaderAndGlassCollision()
  {foreach(string name in new[]{"Crystal","Glass"}){var m=Resources.Load<Material>("Generated/"+name);Assert.IsNotNull(m);Assert.AreEqual("LestingCraft/CrystalGlass",m.shader.name);Assert.GreaterOrEqual(m.renderQueue,3000);Assert.Less(m.GetFloat("_Density"),.2f);}Assert.IsTrue(BlockRegistry.IsSolid(44));Assert.IsFalse(BlockRegistry.Occludes(44));var c=new ChunkData();c.SetBlock(1,2,1,44);var data=ChunkMesher.Build(c,0,0,(x,y,z)=>0);Assert.AreEqual(36,data.TransparentTriangles.Count);Assert.AreEqual(36,data.ColliderTriangles.Count);Assert.AreEqual(0,data.OpaqueTriangles.Count);}
  [Test] public void NeighboringGlassOmitsInternalFacesWithoutLosingSolidCollider()
  {var c=new ChunkData();c.SetBlock(1,2,1,44);c.SetBlock(2,2,1,44);var data=ChunkMesher.Build(c,0,0,(x,y,z)=>0);Assert.AreEqual(60,data.TransparentTriangles.Count);Assert.AreEqual(60,data.ColliderTriangles.Count);}
  [Test] public void AnimalsUseSeparateGeneratedCoatsRatherThanATintedSharedTexture()
  {var layers=new HashSet<int>();for(int kind=0;kind<3;kind++){int layer=AnimalVisual.FurLayerForSpecies(kind);layers.Add(layer);Assert.AreEqual(AnimalVisual.SpeciesFurNames[kind],SurfaceLayers.Names[layer]);Assert.AreNotEqual(System.Array.IndexOf(SurfaceLayers.Names,"wildlife-fur"),layer);}Assert.AreEqual(3,layers.Count);}
  [Test] public void AuthoredWildlifeHasVoxelNormalsMovingLegsAndNoEmissiveFur()
  {for(int species=0;species<3;species++){var obj=new GameObject("Animal test");try{var visual=obj.AddComponent<AnimalVisual>();visual.Build(species);Assert.AreEqual(4,visual.Legs.Count);Assert.IsNotNull(visual.Head);var filters=obj.GetComponentsInChildren<MeshFilter>();Assert.GreaterOrEqual(filters.Length,6);foreach(var filter in filters){var mesh=filter.sharedMesh;Assert.Greater(mesh.vertexCount,20);foreach(var n in mesh.normals)Assert.AreEqual(1,Mathf.Abs(n.x)+Mathf.Abs(n.y)+Mathf.Abs(n.z));foreach(var color in mesh.colors32)Assert.AreEqual(0,color.a,"No fake glowing fur");}}finally{UnityEngine.Object.DestroyImmediate(obj);}}}
 }
}
