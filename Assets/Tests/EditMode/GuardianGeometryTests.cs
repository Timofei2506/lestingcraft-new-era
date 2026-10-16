using NUnit.Framework;using UnityEngine;using LestingCraft.Story;
namespace LestingCraft.Tests
{
 public sealed class GuardianGeometryTests
 {
  [Test] public void AdjacentSculptureCellsCullTheInteriorFace(){var d=new GuardianDefinition{unit=.1f,name="fixture",palette=new[]{new GuardianPalette{layer=4,tint=new[]{1f,1f,1f},emission=0}},cells=new[]{0,0,0,0,1,0,0,0}};var m=GuardianSculpture.CreateGeometry(d);Assert.AreEqual(40,m.vertices.Count);Assert.AreEqual(60,m.triangles.Count);Assert.AreEqual(m.vertices.Count,m.normals.Count);Assert.AreEqual(m.vertices.Count,m.uv.Count);Assert.AreEqual(m.vertices.Count,m.colors.Count);}
  [Test] public void AmberUsesLinearTintAndPreservesEmission(){var d=new GuardianDefinition{unit=.1f,name="fixture",palette=new[]{new GuardianPalette{layer=12,tint=new[]{1f,.5f,.1f},emission=1.6f}},cells=new[]{0,0,0,0}};var m=GuardianSculpture.CreateGeometry(d);Assert.AreEqual(1,m.colors[0].r);Assert.AreEqual(.214041f,m.colors[0].g,.00001f);Assert.AreEqual(1.6f,m.colors[0].a);Assert.AreEqual(12,m.uv[0].z);}
  [Test] public void SharedSculptureAssetBuildsOneDetailedSurfaceMesh(){var text=Resources.Load<TextAsset>("Models/RootGuardian");Assert.IsNotNull(text);var d=JsonUtility.FromJson<GuardianDefinition>(text.text);Assert.Greater(d.cells.Length/4,9000);var m=GuardianSculpture.CreateGeometry(d);Assert.Greater(m.vertices.Count,20000);Assert.Less(m.vertices.Count,60000);foreach(int i in m.triangles)Assert.That(i,Is.InRange(0,m.vertices.Count-1));}
 }
}
