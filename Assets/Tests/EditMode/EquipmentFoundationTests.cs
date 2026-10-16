using NUnit.Framework;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
using LestingCraft.World;
namespace LestingCraft.Tests
{
 public sealed class EquipmentFoundationTests
 {
  [Test] public void ShiftEquipAndUnequipPreserveOwnershipAndWear()
  {var inv=new Inventory();inv.Add(105,1);inv.slots[0].durability=17;InventoryInteraction.Click(inv,0,false,true);Assert.AreEqual(0,inv.Count(105));Assert.AreEqual(17,inv.equipment[4].durability);InventoryInteraction.Click(inv,62,false,true);Assert.AreEqual(1,inv.Count(105));Assert.AreEqual(17,inv.slots[0].durability);}
  [Test] public void WrongEquipmentSlotRejectsWithoutChangingCursor()
  {var inv=new Inventory();inv.cursor=new ItemStack{item=105,count=1,durability=180};InventoryInteraction.Click(inv,59,false,false);Assert.AreEqual(105,inv.cursor.item);Assert.AreEqual(0,inv.equipment[1].count);InventoryInteraction.Click(inv,62,false,false);Assert.AreEqual(0,inv.cursor.count);Assert.AreEqual(105,inv.equipment[4].item);}
  [Test] public void ClothingAndArmorCoexistAndProvideRealEffects()
  {var inv=new Inventory();inv.Add(104,1);Equipment.Equip(inv,0);inv.Add(106,1);Equipment.Equip(inv,0);Assert.AreEqual(10,Equipment.Insulation(inv));Assert.Greater(Equipment.Protection(inv),.1f);Equipment.Wear(inv,16);Assert.AreEqual(258,inv.equipment[5].durability);}
  [Test] public void BrokenArmorStopsProtectingAndDisappears()
  {var inv=new Inventory();inv.Add(105,1);Equipment.Equip(inv,0);inv.equipment[4].durability=1;Equipment.Wear(inv,8);Assert.AreEqual(0,inv.equipment[4].count);Assert.AreEqual(0,Equipment.Protection(inv));}
  [Test] public void LegacyCoatMigrationMovesOneItemAndIsIdempotent()
  {var save=new PlayerSave{equippedCoat=true};save.inventory.Add(104,1);save.Normalize();save.Normalize();Assert.AreEqual(0,save.inventory.Count(104));Assert.AreEqual(104,save.inventory.equipment[1].item);Assert.IsFalse(save.equippedCoat);}
  [Test] public void EquipmentRoundTripsAndGoesIntoGraveExtras()
  {var save=new PlayerSave();save.inventory.Add(106,1);Equipment.Equip(save.inventory,0);save.inventory.equipment[5].durability=73;var copy=JsonUtility.FromJson<PlayerSave>(JsonUtility.ToJson(save));copy.Normalize();Assert.AreEqual(73,copy.inventory.equipment[5].durability);Assert.AreEqual(106,copy.inventory.ExtraStacks()[18].item);Assert.AreNotSame(copy.inventory.equipment[5],copy.inventory.Clone().equipment[5]);}
  [Test] public void CampfirePlacementUsesTheExactIconAndHeldMesh()
  {var root=new GameObject("Campfire parity test");try{var obj=PlacedVoxelModel.Create(15,root.transform,new Vector3(2,5,3),Resources.Load<Material>("Generated/VoxelObjects"));Assert.AreSame(VoxelItemModels.MeshFor(15),obj.GetComponent<MeshFilter>().sharedMesh);Assert.AreEqual(0,obj.GetComponentsInChildren<Collider>().Length);Assert.AreEqual(5,obj.transform.position.y+obj.GetComponent<MeshFilter>().sharedMesh.bounds.min.y*.95f,.001f);}finally{Object.DestroyImmediate(root);}}
  [Test] public void CampfireDoesNotOccludeAdjacentFullFacesOrRenderACube()
  {var chunk=new ChunkData();chunk.SetBlock(1,3,1,15);chunk.SetBlock(2,3,1,1);var data=ChunkMesher.Build(chunk,0,0,(x,y,z)=>0);Assert.AreEqual(36,data.OpaqueTriangles.Count);Assert.IsFalse(BlockRegistry.Occludes(15));Assert.IsTrue(BlockRegistry.IsSolid(15));}
  [Test] public void GrassIsCurvedDenseDeterministicAndNonColliding()
  {var a=new ChunkMeshData();var b=new ChunkMeshData();for(int x=0;x<8;x++){SurfaceDetailMesher.AddGrass(a,x,10,0,0,0,2506);SurfaceDetailMesher.AddGrass(b,x,10,0,0,0,2506);}Assert.Greater(a.Vertices.Count,100);CollectionAssert.AreEqual(a.Vertices,b.Vertices);Assert.AreEqual(a.Vertices.Count,a.UVs.Count);Assert.AreEqual(a.Vertices.Count,a.Colors.Count);Assert.AreEqual(0,a.ColliderTriangles.Count);}
  [Test] public void UltraGrassAddsDetailWithoutChangingCollisionOrAutoSelectingQuality()
  {var medium=new ChunkMeshData();var ultra=new ChunkMeshData();for(int x=0;x<8;x++){SurfaceDetailMesher.AddGrass(medium,x,10,0,0,0,2506,0);SurfaceDetailMesher.AddGrass(ultra,x,10,0,0,0,2506,1);}Assert.Greater(ultra.Vertices.Count,medium.Vertices.Count*2);CollectionAssert.AreEqual(medium.ColliderTriangles,ultra.ColliderTriangles);}
  [Test] public void CommandsCannotCheatInSurvivalOrCoop()
  {var inv=new Inventory();ChatCommands.Execute("/give 105",false,true,inv,1,Vector3.zero,1);ChatCommands.Execute("/give 105",true,false,inv,1,Vector3.zero,2);Assert.AreEqual(0,inv.Count(105));ChatCommands.Execute("/give 105",true,true,inv,1,Vector3.zero,1);Assert.AreEqual(1,inv.Count(105));Assert.AreEqual("",ChatCommands.Execute("/time night",true,false,inv,1,Vector3.zero,1).Action);}
  [Test] public void CommandsRejectInvalidAmountsAndUnknownItems()
  {var inv=new Inventory();foreach(string text in new[]{"/give 99999","/give 105 -1","/give 105 99999999999999","/give 105 0"})ChatCommands.Execute(text,true,true,inv,0,Vector3.zero,1);Assert.AreEqual(0,inv.Count(105));Assert.AreEqual("time",ChatCommands.Execute("/time day",true,true,inv,0,Vector3.zero,1).Action);}
  [Test] public void CoopSaveCommandDoesNotSendRateLimitedStatePackets()
  {var inv=new Inventory();Assert.AreEqual("",ChatCommands.Execute("/save",false,false,inv,0,Vector3.zero,2).Action);Assert.AreEqual("save",ChatCommands.Execute("/save",false,true,inv,0,Vector3.zero,1).Action);}
  [Test] public void OldRoomsRequireAnEquipmentCapableServerBeforeMigration()
  {Assert.IsTrue(GameRules.AcceptsRoom(5,6));Assert.IsTrue(GameRules.AcceptsRoom(4,6));Assert.IsTrue(GameRules.AcceptsRoom(3,6));Assert.IsFalse(GameRules.AcceptsRoom(4,4));Assert.IsFalse(GameRules.AcceptsRoom(3,3));Assert.IsFalse(GameRules.AcceptsRoom(3,0));Assert.IsFalse(GameRules.AcceptsRoom(2,4));}
  [Test] public void MediumAndUltraAreExplicitAndNeverClaimUrpRayTracing()
  {var s=new VisualSettings();Assert.AreEqual(0,s.Preset);s.ShadowResolution=4096;s.VolumetricQuality=2;s.RenderScale=1.5f;s.Clamp();Assert.AreEqual(4096,s.ShadowResolution);Assert.AreEqual(2,s.VolumetricQuality);Assert.AreEqual(1.5f,s.RenderScale);Assert.IsFalse(VisualSettings.PipelineRayTracing);}
 }
}
