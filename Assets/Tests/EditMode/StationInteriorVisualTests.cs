using LestingCraft.Gameplay;
using LestingCraft.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace LestingCraft.Tests.EditMode
{
    public class StationInteriorVisualTests
    {
        [Test] public void CorridorRunsThroughApprovedHollowAxisWithoutReplacingExterior()
        {
            var source=Resources.Load<TextAsset>("Space/Station");Assert.IsNotNull(source);
            var model=SpaceVoxelAsset.Read(source.bytes);
            Assert.AreEqual(112,model.X);Assert.AreEqual(56,model.Y);Assert.AreEqual(104,model.Z);
            for(int x=-44;x<=20;x++)
                for(int y=21;y<=22;y++)
                    Assert.AreEqual(0,model.At(x+model.X/2,y,model.Z/2),"Blocked centerline at "+x+","+y);
            for(int role=0;role<StationAstronauts.Count;role++)
            {
                var pos=StationAstronauts.Position(role);
                Assert.IsTrue(StationInteriorLayout.Inside(pos.X,pos.Y,pos.Z));
            }
        }
        [Test] public void InteriorHasSolidWalkwayAndAnActuallyBlockingHatch()
        {
            var root=new GameObject("station test");
            try
            {
                var visual=StationInteriorVisual.Create(root.transform);
                Assert.IsNotNull(visual.GetComponentInChildren<SpaceVoxelVisual>());
                Assert.Greater(visual.GetComponentsInChildren<BoxCollider>().Length,40);
                var hatch=visual.transform.Find("Sliding pressure hatch");Assert.IsNotNull(hatch);
                Assert.IsTrue(hatch.GetComponent<BoxCollider>().enabled);
                var dock=new StationDocking();
                Assert.IsTrue(dock.TryLatch(OrbitalStation.Port,new SpaceVector(0,0,0),
                    new SpaceVector(-1,0,0),new SpaceVector(1,0,0),true,true));
                dock.Advance(8);Assert.IsTrue(dock.SetHatch(true));visual.SyncHatch(dock);
                Assert.IsFalse(hatch.GetComponent<BoxCollider>().enabled);
                Assert.Greater(hatch.localPosition.y,24);
            }
            finally {Object.DestroyImmediate(root);}
        }
    }
}
