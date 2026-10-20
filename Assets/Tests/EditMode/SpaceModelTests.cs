using LestingCraft.Gameplay;
using LestingCraft.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace LestingCraft.Tests.EditMode
{
    public class SpaceModelTests
    {
        [TestCase("Shuttle",13929)]
        [TestCase("LaunchPad",27813)]
        [TestCase("Station",10375)]
        public void ApprovedThreeDimensionalModelImportsAndMeshes(string assetName,int cells)
        {
            var source=Resources.Load<TextAsset>("Space/"+assetName);
            Assert.IsNotNull(source,"Approved model must be a Unity resource");
            Assert.AreEqual(cells,SpaceVoxelAsset.Read(source.bytes).SolidCount);
            var root=new GameObject("Space model test");
            try
            {
                var visual=SpaceVoxelVisual.Create(assetName,root.transform,assetName=="LaunchPad");
                var mesh=visual.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsNotNull(mesh);
                Assert.Greater(mesh.vertexCount,10000);
                Assert.Greater(mesh.bounds.size.x,10);
                Assert.Greater(mesh.bounds.size.y,20);
                Assert.AreEqual(SpaceVoxelAsset.Read(source.bytes).Palette.Length,mesh.subMeshCount);
                Assert.IsNotNull(visual.GetComponent<MeshRenderer>());
                if(assetName=="LaunchPad")Assert.IsNotNull(visual.GetComponent<MeshCollider>());
            }
            finally {Object.DestroyImmediate(root);}
        }
    }
}
