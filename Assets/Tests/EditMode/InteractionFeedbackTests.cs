using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LestingCraft.Runtime;
using LestingCraft.Presentation;
namespace LestingCraft.Tests
{
 public sealed class InteractionFeedbackTests
 {
  [TestCase(1)] [TestCase(-1)]
  public void GridRayFindsExactFaceAcrossNegativeAndPositiveChunks(int sign)
  {
   int x=sign>0?16:-17;var start=new Vector3(sign>0?14.5f:-14.5f,5.5f,.5f);
   bool found=VoxelRaycast.Trace(start,Vector3.right*sign,6,(a,b,c)=>(ushort)(a==x&&b==5&&c==0?1:0),out var hit);
   Assert.IsTrue(found);Assert.AreEqual(new Vector3Int(x,5,0),hit.Cell);Assert.AreEqual(new Vector3Int(-sign,0,0),hit.Normal);
   Assert.AreEqual(hit.Cell+hit.Normal,hit.Adjacent);
  }
  [Test] public void TraversalIgnoresWaterAndImmediatelySeesDataChanges()
  {
   ushort target=1;
   ushort Sample(int x,int y,int z)=>(ushort)(z==1?7:z==3?target:0);
   Assert.IsTrue(VoxelRaycast.Trace(new Vector3(.5f,2.5f,.5f),Vector3.forward,6,Sample,out var a));Assert.AreEqual(3,a.Cell.z);
   target=0;Assert.IsFalse(VoxelRaycast.Trace(new Vector3(.5f,2.5f,.5f),Vector3.forward,6,Sample,out _));
  }
  [Test] public void GridRayHonoursReachAndAxisParallelDirections()
  {
   Assert.IsFalse(VoxelRaycast.Trace(new Vector3(.5f,.5f,.5f),Vector3.up,2,(x,y,z)=>(ushort)(y==4?1:0),out _));
   Assert.IsTrue(VoxelRaycast.Trace(new Vector3(.5f,.5f,.5f),Vector3.up,5,(x,y,z)=>(ushort)(y==4?1:0),out var hit));Assert.AreEqual(Vector3Int.down,hit.Normal);
  }
  [Test] public void CreativeMiningIsNotOneBlockPerRenderFrame()
  {Assert.GreaterOrEqual(VoxelRaycast.MiningSeconds(true,1,1),.2f);Assert.Less(VoxelRaycast.MiningSeconds(false,3,3),VoxelRaycast.MiningSeconds(false,3,1));}
  [Test] public void TouchingFloorDoesNotCountAsInsideThePlayer()
  {
   var body=new Bounds(new Vector3(.5f,1.9f,.5f),new Vector3(.6f,1.8f,.6f));
   Assert.IsFalse(VoxelRaycast.OverlapsPlayer(new Vector3Int(0,0,0),body));Assert.IsTrue(VoxelRaycast.OverlapsPlayer(new Vector3Int(0,1,0),body));
  }
  [Test] public void CloudsAdvectVisiblyAndDoNotDependOnFrameRate()
  {var a=CloudField.Offset(420,2506,.5f);var b=CloudField.Offset(430,2506,.5f);Assert.Greater((b-a).magnitude,10);Assert.AreEqual(a,CloudField.Offset(420,2506,.5f));}
  [Test] public void NoiseTilesContinuouslyWithoutIdenticalNeighbouringCells()
  {
   Assert.AreEqual(CloudField.Noise(.25f,.6f,.8f,4),CloudField.Noise(4.25f,.6f,.8f,4),.00001f);
   Assert.AreNotEqual(CloudField.Shape(.1f,.2f,.3f),CloudField.Shape(.5f,.2f,.3f));
   Assert.Less(Mathf.Abs(CloudField.Shape(.99999f,.4f,.3f)-CloudField.Shape(.00001f,.4f,.3f)),.001f);
  }
  [TestCase("BlockDamage")] [TestCase("Precipitation")]
  public void NewFeedbackShadersImportWithoutErrors(string name)
  {var shader=Shader.Find("LestingCraft/"+name);Assert.IsNotNull(shader);Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader));}
  [Test] public void SelectionFeedbackMovesOnlyItsOwnRootNeverThePlayer()
  {
   var owner=new GameObject("Inactive feedback ownership test");owner.SetActive(false);owner.transform.position=new Vector3(2,10,3);owner.transform.rotation=Quaternion.Euler(0,35,0);
   try
   {
    var game=owner.AddComponent<PrototypeGame>();var session=owner.AddComponent<LestingCraft.Gameplay.SurvivalSession>();typeof(PrototypeGame).GetProperty("Session").SetValue(game,session);
    var controller=owner.AddComponent<FirstPersonController>();controller.Game=game;controller.Active=true;
    typeof(FirstPersonController).GetProperty("HasTarget").SetValue(controller,true);
    var cell=new Vector3Int(7,8,9);typeof(FirstPersonController).GetProperty("Target").SetValue(controller,new VoxelRaycast.Hit(cell,Vector3Int.up,3));
    var effect=owner.AddComponent<BlockFeedback>();effect.Initialize(controller);
    effect.SendMessage("LateUpdate");
    Assert.AreEqual(new Vector3(2,10,3),owner.transform.position,"Target selection must NEVER teleport the player");
    Assert.IsNotNull(effect.VisualRoot);Assert.AreNotEqual(owner.transform,effect.VisualRoot.parent);
    Assert.AreEqual((Vector3)cell+Vector3.one*.5f,effect.VisualRoot.position);Assert.Less(Quaternion.Angle(Quaternion.identity,effect.VisualRoot.rotation),.001f);
    Assert.AreEqual(0,effect.VisualRoot.GetComponentsInChildren<Collider>().Length);
   }
   finally{Object.DestroyImmediate(owner);}
  }
  [Test] public void HudDoesNotShowWorldSeedFpsObjectivesOrKeyHints()
  {
   var owner=new GameObject("Inactive feedback owner");owner.SetActive(false);var root=new GameObject("Clean HUD test");var previous=EventSystem.current;
   try
   {
    var game=owner.AddComponent<PrototypeGame>();VisualSettings.Load();var ui=root.AddComponent<GameUI>();ui.Initialize(game);ui.ShowHUD();
    foreach(var label in root.GetComponentsInChildren<Text>())
    {StringAssert.DoesNotContain("SEED",label.text);StringAssert.DoesNotContain("ЛОКАЛЬНЫЙ МИР",label.text);StringAssert.DoesNotContain("LESTINGCRAFT",label.text);StringAssert.DoesNotContain("ЛКМ  добывать",label.text);}
    Assert.IsNotNull(root.GetComponentInChildren<HealthSilhouette>());
   }
   finally{Object.DestroyImmediate(root);Object.DestroyImmediate(owner);if(!previous&&EventSystem.current)Object.DestroyImmediate(EventSystem.current.gameObject);}
  }
 }
}
