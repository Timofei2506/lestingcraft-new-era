using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
using LestingCraft.Runtime;
using LestingCraft.World;
namespace LestingCraft.Tests
{
 public sealed class MaterialEffectsTests
 {
  [Test] public void UrpSsaoIsARealEnabledFeatureWithDepthReconstructedNormals()
  {
   var renderer=Resources.Load<UniversalRendererData>("Generated/Renderer");Assert.IsNotNull(renderer);
   ScreenSpaceAmbientOcclusion ao=null;foreach(var feature in renderer.rendererFeatures)if(feature is ScreenSpaceAmbientOcclusion f){Assert.IsNull(ao,"No duplicate AO passes");ao=f;}
   Assert.IsNotNull(ao);Assert.IsTrue(ao.isActive);var s=new SerializedObject(ao).FindProperty("m_Settings");
   Assert.AreEqual(0,s.FindPropertyRelative("Source").intValue);Assert.IsFalse(s.FindPropertyRelative("AfterOpaque").boolValue);Assert.Greater(s.FindPropertyRelative("Intensity").floatValue,0);
  }
  [Test] public void All26PbrLayersHaveDetailedNonPlaceholderAlbedoAndNormalMaskMaps()
  {
   var a=Resources.Load<Texture2DArray>("Generated/Albedo");var n=Resources.Load<Texture2DArray>("Generated/Normal");var m=Resources.Load<Texture2DArray>("Generated/Mask");
   Assert.AreEqual(26,a.depth);Assert.AreEqual(26,n.depth);Assert.AreEqual(26,m.depth);
   for(int layer=0;layer<26;layer++)
   {
    var image=new Texture2D(2,2,TextureFormat.RGBA32,false);Assert.IsTrue(image.LoadImage(System.IO.File.ReadAllBytes("Assets/Art/PBR/"+SurfaceLayers.Names[layer]+"_albedo.png")));
    var colors=image.GetPixels();Object.DestroyImmediate(image);float lo=1,hi=0;
    foreach(var c in colors){lo=Mathf.Min(lo,c.grayscale);hi=Mathf.Max(hi,c.grayscale);}
    Assert.Greater(hi-lo,.025f,SurfaceLayers.Names[layer]+" must not be a flat color");
   }
  }
  [Test] public void OreAppearancesAreStableAcrossFacesAndSeedsWithoutChangingBlockOrGenerator()
  {
   var layers=new HashSet<int>();for(int x=-48;x<48;x+=4)for(int y=8;y<24;y+=4)
   {
    int layer=SurfaceLayers.ForCellFace(13,0,x,y,-11,2506);layers.Add(layer);
    for(int face=1;face<6;face++)Assert.AreEqual(layer,SurfaceLayers.ForCellFace(13,face,x,y,-11,2506));
    Assert.IsTrue(layer==14||layer==24||layer==25);
   }
   Assert.AreEqual(3,layers.Count);Assert.AreEqual(1,TerrainGenerator.Version);Assert.AreEqual(13,BlockRegistry.OreId);
  }
  [Test] public void EveryPlaceableBlockHasANonMagentaBreakTintAndExplicitMaterial()
  {for(ushort id=1;id<=18;id++){Assert.AreNotEqual(new Color32(255,0,255,255),ChunkMesher.Palette(id),"Block "+id);for(int face=0;face<6;face++)Assert.That(SurfaceLayers.ForCellFace(id,face,0,30,0,1),Is.InRange(0,25));}}
  [Test] public void CrystalDoesNotRenderOrCollideAsAnOpaqueCube()
  {var chunk=new ChunkData();chunk.SetBlock(1,3,1,11);var data=ChunkMesher.Build(chunk,0,0,(x,y,z)=>0);Assert.AreEqual(0,data.OpaqueTriangles.Count);Assert.AreEqual(0,data.ColliderTriangles.Count);Assert.IsFalse(BlockRegistry.Occludes(11));Assert.IsTrue(BlockRegistry.IsSolid(11));}
  [Test] public void CrystalBatchesUseTheSharedAuthoredSurfaceForRenderingAndCollision()
  {
   var root=new GameObject("Crystal batch test");try
   {
    var source=VoxelItemModels.MeshFor(11);var batch=PlacedVoxelModel.CreateCrystalBatch(root.transform,new List<Vector3>{new Vector3(2,5,3),new Vector3(4,5,3)},Resources.Load<Material>("Generated/VoxelObjects"));
    var mesh=batch.GetComponent<MeshFilter>().sharedMesh;Assert.AreEqual(source.vertexCount*2,mesh.vertexCount);Assert.AreSame(mesh,batch.GetComponent<MeshCollider>().sharedMesh);Assert.AreEqual(5,mesh.bounds.min.y,.001f);Assert.LessOrEqual(mesh.bounds.max.y,5.951f);
   }finally{Object.DestroyImmediate(root);}
  }
  [Test] public void CrystalRaycastHitsItsCoreButPassesThroughEmptySpaceBesideTheTip()
  {
   ushort Sample(int x,int y,int z)=>(ushort)(x==0&&y==0?(z==0?11:z==2?1:0):0);
   Assert.IsTrue(VoxelRaycast.Trace(new Vector3(.5f,.5f,-1),Vector3.forward,4,Sample,out var core));Assert.AreEqual(Vector3Int.zero,core.Cell);Assert.Greater(core.Distance,1);
   Assert.IsTrue(VoxelRaycast.Trace(new Vector3(.05f,.9f,-1),Vector3.forward,4,Sample,out var air));Assert.AreEqual(new Vector3Int(0,0,2),air.Cell);
   Assert.IsFalse(VoxelRaycast.Trace(new Vector3(.05f,.9f,-1),Vector3.forward,2,Sample,out _));
  }
  [Test] public void MiningCracksUseTheAuthoredMeshAndRestoreTheFullCubeForOrdinaryBlocks()
  {
   var root=new GameObject("Shape feedback test");root.SetActive(false);try
   {
    var controller=root.AddComponent<FirstPersonController>();var feedback=root.AddComponent<BlockFeedback>();feedback.Initialize(controller);feedback.SetShape(11);
    var cracks=feedback.VisualRoot.Find("Target cracks");Assert.AreSame(VoxelItemModels.MeshFor(11),cracks.GetComponent<MeshFilter>().sharedMesh);
    Assert.AreEqual(PlacedVoxelModel.Scale*1.004f,cracks.localScale.x,.0001f);
    feedback.SetShape(1);Assert.AreSame(BlockGeometry.Box(0),cracks.GetComponent<MeshFilter>().sharedMesh);Assert.AreEqual(Vector3.zero,cracks.localPosition);
   }finally{Object.DestroyImmediate(root);}
  }
  [Test] public void CampfireRaycastDoesNotSelectAnInvisibleUpperCornerOfItsCell()
  {Assert.IsFalse(VoxelRaycast.Trace(new Vector3(.02f,.95f,-1),Vector3.forward,2,(x,y,z)=>(ushort)(x==0&&y==0&&z==0?15:0),out _));}
  [TestCase(16)] [TestCase(17)] public void StationItemFacesUseTheSamePbrLayersAsPlacedWorldFaces(int id)
  {
   var mesh=VoxelItemModels.MeshFor(id);var uv=new List<Vector3>();mesh.GetUVs(0,uv);var normals=mesh.normals;
   for(int i=0;i<normals.Length;i++){int face=System.Array.IndexOf(BlockGeometry.Directions,Vector3Int.RoundToInt(normals[i]));Assert.AreEqual(SurfaceLayers.ForFace((ushort)id,face),uv[i].z);}
  }
  [Test] public void SmokeSparksAndFlameHaveDifferentBoundedEmittersAndRealMaterials()
  {
   var root=new GameObject("Campfire FX test");try
   {
    var fx=root.AddComponent<CampfireEffects>();fx.Build();fx.SetWind(.7f,.3f);
    Assert.AreEqual(3,root.GetComponentsInChildren<ParticleSystem>().Length);Assert.Greater(fx.Smoke.main.startLifetime.constantMax,2);Assert.LessOrEqual(fx.Smoke.main.maxParticles,48);Assert.Less(fx.Sparks.main.startSize.constantMax,.05f);
    foreach(var ps in root.GetComponentsInChildren<ParticleSystem>())Assert.IsNotNull(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial);
    Assert.AreEqual("LestingCraft/SmokeParticles",fx.Smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name);
   }finally{Object.DestroyImmediate(root);}
  }
  [Test] public void ArmorHasDocumentedItemContributionsAndFullSetProtection()
  {
   var inv=new Inventory();foreach(int id in new[]{105,106,107,108}){inv.Add(id,1);Equipment.Equip(inv,System.Array.FindIndex(inv.slots,s=>s.item==id));}
   Assert.AreEqual(.5f,Equipment.Protection(inv),.0001f);Assert.AreEqual(.09f,Equipment.ItemProtection(105));Assert.AreEqual(.18f,Equipment.ItemProtection(106));Assert.AreEqual(0,Equipment.ItemProtection(104));
  }
  [Test] public void ExistingSettingsUpgradeEnablesSsaoButRespectsExplicitOptOut()
  {Assert.IsTrue(VisualSettings.FromJson("{\"Preset\":0,\"PostEffects\":true}").AmbientOcclusion);Assert.IsFalse(VisualSettings.FromJson("{\"AmbientOcclusion\":false}").AmbientOcclusion);}
  [Test] public void HighQualityPresetsEnableRealSsaoWithoutClaimingHardwareRt()
  {var settings=new VisualSettings();Assert.IsTrue(settings.AmbientOcclusion);Assert.IsFalse(VisualSettings.PipelineRayTracing);}
 }
}
