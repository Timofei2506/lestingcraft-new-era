using LestingCraft.Gameplay;
using LestingCraft.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace LestingCraft.Tests.EditMode
{
    public class ShuttleCabinCarvingTests
    {
        [Test] public void ApprovedExteriorIsUntouchedExceptSixDoorwayCells()
        {
            var resource=Resources.Load<TextAsset>("Space/Shuttle");Assert.IsNotNull(resource);
            var approved=SpaceVoxelAsset.Read(resource.bytes);
            var fitted=ShuttleCabinCarving.Make(approved);
            Assert.AreEqual(13929,approved.SolidCount);
            Assert.Less(fitted.SolidCount,approved.SolidCount-200);
            Assert.Greater(fitted.SolidCount,approved.SolidCount-400);
            int removedSurface=0;
            for(int x=0;x<approved.X;x++)for(int y=0;y<approved.Y;y++)for(int z=0;z<approved.Z;z++)
            {
                byte old=approved.At(x,y,z),current=fitted.At(x,y,z);
                if(old==current)continue;
                Assert.AreNotEqual(0,old);Assert.AreEqual(0,current);
                Assert.IsTrue(ShuttleCabinCarving.Remove(x-31,y,z-30));
                if(approved.At(x+1,y,z)==0||approved.At(x-1,y,z)==0||approved.At(x,y+1,z)==0||
                   approved.At(x,y-1,z)==0||approved.At(x,y,z+1)==0||approved.At(x,y,z-1)==0)
                {
                    removedSurface++;
                    Assert.GreaterOrEqual(y,28);Assert.LessOrEqual(y,30);
                    Assert.GreaterOrEqual(x-31,-9);Assert.LessOrEqual(x-31,-7);
                    Assert.GreaterOrEqual(z-30,2);Assert.LessOrEqual(z-30,3);
                }
            }
            Assert.AreEqual(6,removedSurface);
            Assert.AreEqual(0,fitted.At(31,30,33)); // standing space in lower deck
            Assert.AreEqual(0,fitted.At(31,43,33)); // upper deck behind preserved windows
            Assert.AreEqual(approved.At(31,27,33),fitted.At(31,27,33)); // solid lower floor
            Assert.AreEqual(approved.At(31,45,36),fitted.At(31,45,36)); // glazing stays
        }
        [Test] public void EntryAndTwoSeatsAreContinuousLocalWalkVolumesNotTeleports()
        {
            Assert.IsTrue(VerticalShuttleWalk.Inside(-10,28,3));
            Assert.IsTrue(VerticalShuttleWalk.Inside(0,30,3));
            Assert.IsTrue(VerticalShuttleWalk.Inside(0,38,3));
            Assert.IsTrue(VerticalShuttleWalk.Inside(0,43,4));
            Assert.IsFalse(VerticalShuttleWalk.CanStep(-9.2,29,3,-8.8,29,3,false));
            Assert.IsTrue(VerticalShuttleWalk.CanStep(-9.2,29,3,-8.8,29,3,true));
            Assert.IsTrue(VerticalShuttleWalk.CanStep(.5,36,3,.5,36.4,3,true));
            for(double y=28.2;y<43;y+=.4)
                Assert.IsTrue(VerticalShuttleWalk.CanStep(.5,y,3,.5,y+.4,3,true),"Blocked continuous climb near "+y);
            Assert.IsFalse(VerticalShuttleWalk.CanStep(2.5,42,4,2.5,42.4,4,true));
            Assert.IsFalse(VerticalShuttleWalk.CanStep(-10,29,3,0,43,4,true));
            Assert.IsFalse(VerticalShuttleWalk.Inside(40,43,3));
            for(int i=0;i<VerticalShuttleWalk.SeatCount;i++)
            {
                double x,y,z;VerticalShuttleWalk.Seat(i,out x,out y,out z);
                Assert.IsTrue(VerticalShuttleWalk.Inside(x,y,z));
            }
        }
        [Test] public void PadHeightLiftCarriesOneRiderThenReturnsEmptyForSecond()
        {
            var lift=new ShuttleElevator(VerticalShuttleWalk.LiftTravel);
            Assert.IsFalse(lift.RequestUp());
            Assert.IsTrue(lift.Board());Assert.IsTrue(lift.RequestUp());
            lift.Advance(3);Assert.AreEqual(12.5f,lift.Height,.001f);
            Assert.IsFalse(lift.Exit());lift.Advance(3);
            Assert.AreEqual(25f,lift.Height,.001f);
            Assert.IsTrue(lift.Exit());Assert.IsFalse(lift.RequestDown());
            Assert.IsTrue(lift.ReturnEmpty());lift.Advance(6);
            Assert.AreEqual(ElevatorState.Ground,lift.State);
            Assert.AreEqual(0f,lift.Height,.001f);
        }
        [Test] public void PhysicallyFittedDecksSeatsAndHatchHaveCollision()
        {
            var root=new GameObject("vertical shuttle test");
            try
            {
                var visual=VerticalShuttleInteriorVisual.Create(root.transform);
                Assert.IsNotNull(visual.GetComponentInChildren<SpaceVoxelVisual>());
                Assert.Greater(visual.GetComponentsInChildren<BoxCollider>().Length,10);
                Assert.IsNotNull(visual.transform.Find("Lower crew deck").GetComponent<BoxCollider>());
                var lift=visual.transform.Find("Moving crew lift");
                var bridge=visual.transform.Find("Tower lift exit bridge");
                Assert.IsNotNull(lift.GetComponent<BoxCollider>());
                Assert.IsNotNull(bridge.GetComponent<BoxCollider>());
                visual.SyncLift(new LestingCraft.Networking.NetShuttleLift{
                    active=true,moving=true,direction="up",progress=.5f,occupied=true,
                    riderId="other",serverNow=12000},null,"me");
                Assert.AreEqual(12.5f,visual.Lift.Height,.001f);
                Assert.AreEqual(VerticalShuttleWalk.LiftGroundY+12.5f-.14f,lift.localPosition.y,.001f);
                visual.SyncLift(new LestingCraft.Networking.NetShuttleLift{
                    active=true,moving=true,direction="up",progress=.1f,serverNow=11000},null,"me");
                Assert.AreEqual(12.5f,visual.Lift.Height,.001f,"stale snapshot cannot reverse lift");
                Assert.LessOrEqual(bridge.localPosition.x-bridge.localScale.x/2,
                                   lift.localPosition.x+lift.localScale.x/2+.01f); // no gap at top
                Assert.IsNotNull(visual.transform.Find("Cockpit forward bridge").GetComponent<BoxCollider>());
                Assert.IsNotNull(visual.transform.Find("Cockpit ladder landing").GetComponent<BoxCollider>());
                for(int i=0;i<VerticalShuttleWalk.SeatCount;i++)
                    Assert.IsNotNull(visual.transform.Find("Crew seat "+i+" cushion").GetComponent<BoxCollider>());
                var door=visual.transform.Find("Closed pressure hatch · server controlled");
                Assert.IsTrue(door.GetComponent<BoxCollider>().enabled);
                visual.SyncHatch(true);
                Assert.IsFalse(door.GetComponent<BoxCollider>().enabled);
                Assert.Greater(door.localPosition.y,33);
                visual.SyncHatch(false);
                Assert.IsTrue(door.GetComponent<BoxCollider>().enabled);
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test] public void LaunchPadDerivedWellAndDoorPreserveApprovedSource()
        {
            var approved=SpaceVoxelAsset.Read(Resources.Load<TextAsset>("Space/LaunchPad").bytes);
            var fitted=LaunchPadLiftOpening.Make(approved);
            Assert.AreEqual(27813,approved.SolidCount);
            Assert.AreEqual(27803,fitted.SolidCount);
            int changed=0;
            for(int x=0;x<approved.X;x++)for(int y=0;y<approved.Y;y++)for(int z=0;z<approved.Z;z++)
                if(approved.At(x,y,z)!=fitted.At(x,y,z))
                {
                    changed++;
                    Assert.IsTrue(LaunchPadLiftOpening.Remove(x-47,y,z-45));
                    Assert.AreEqual(0,fitted.At(x,y,z));
                }
            Assert.AreEqual(10,changed); // six deck cells, four in tower partition
            Assert.AreNotEqual(0,approved.At(27,29,48));
            Assert.AreEqual(0,fitted.At(27,29,48));
            Assert.AreNotEqual(0,approved.At(34,30,48));
            Assert.AreEqual(0,fitted.At(34,30,48));
            Assert.AreNotEqual(0,approved.At(27,4,48)); // ground top Y=5
            Assert.AreNotEqual(0,approved.At(33,29,48)); // deck top Y=30
        }
        [Test] public void DerivedPadHasPhysicalCollidingDeckAndOpenWell()
        {
            var root=new GameObject("walkable pad test");
            try
            {
                var visual=SpaceVoxelVisual.CreateWalkableLaunchPad(root.transform);
                Assert.IsNotNull(visual.GetComponent<MeshCollider>());
                Assert.IsNotNull(visual.GetComponent<MeshCollider>().sharedMesh);
                Assert.Greater(visual.GetComponent<MeshFilter>().sharedMesh.vertexCount,10000);
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test] public void DerivedExteriorHasPhysicalHatchOpeningAndSolidHullCollision()
        {
            var root=new GameObject("walkable shuttle test");
            try
            {
                var visual=SpaceVoxelVisual.CreateWalkableShuttle(root.transform);
                Assert.IsNotNull(visual.GetComponent<MeshCollider>());
                Assert.IsNotNull(visual.GetComponent<MeshCollider>().sharedMesh);
                Assert.Greater(visual.GetComponent<MeshFilter>().sharedMesh.vertexCount,10000);
                Assert.AreEqual(62,SpaceVoxelAsset.Read(Resources.Load<TextAsset>("Space/Shuttle").bytes).X);
            }
            finally {Object.DestroyImmediate(root);}
        }
    }
}
