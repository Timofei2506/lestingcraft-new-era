using System.Collections.Generic;
using LestingCraft.Gameplay;
using NUnit.Framework;

namespace LestingCraft.Tests.EditMode
{
    public class StationVisitTests
    {
        static StationDocking Latched()
        {
            var dock=new StationDocking();
            Assert.IsTrue(dock.TryLatch(OrbitalStation.Port,new SpaceVector(0,.1,0),
                new SpaceVector(-1,0,0),new SpaceVector(1,0,0),true,true));
            return dock;
        }
        [Test] public void ShipMustActuallyMeetPortAndComeToRest()
        {
            var dock=new StationDocking();var normal=new SpaceVector(1,0,0),forward=new SpaceVector(-1,0,0);
            Assert.IsFalse(dock.TryLatch(OrbitalStation.Port+new SpaceVector(3,0,0),new SpaceVector(0,0,0),forward,normal,true,true));
            Assert.IsFalse(dock.TryLatch(OrbitalStation.Port,new SpaceVector(.36,0,0),forward,normal,true,true));
            Assert.IsFalse(dock.TryLatch(OrbitalStation.Port,new SpaceVector(0,0,0),normal,normal,true,true));
            Assert.IsFalse(dock.TryLatch(OrbitalStation.Port,new SpaceVector(0,0,0),forward,normal,false,true));
            Assert.IsFalse(dock.TryLatch(OrbitalStation.Port,new SpaceVector(0,0,0),forward,normal,true,false));
            Assert.IsFalse(dock.Latched);
        }
        [Test] public void HatchRequiresPressurizationAndUndockingRequiresEveryoneBack()
        {
            var dock=Latched();Assert.IsFalse(dock.SetHatch(true));
            dock.Advance(7.9);Assert.IsFalse(dock.SetHatch(true));
            dock.Advance(.1);Assert.IsTrue(dock.SetHatch(true));
            Assert.IsFalse(dock.TryUndock(true,true));Assert.IsTrue(dock.SetHatch(false));
            Assert.IsFalse(dock.TryUndock(false,true));Assert.IsFalse(dock.TryUndock(true,false));
            Assert.IsTrue(dock.TryUndock(true,true));Assert.IsFalse(dock.Pressurized);
        }
        [Test] public void OnlyShortContiguousWalksCanCrossAnOpenHatch()
        {
            Assert.IsTrue(StationInteriorLayout.Inside(11,1,0));
            Assert.IsFalse(StationInteriorLayout.CanWalk(-42.2,1,0,-41.8,1,0,false));
            Assert.IsTrue(StationInteriorLayout.CanWalk(-42.2,1,0,-41.8,1,0,true));
            Assert.IsFalse(StationInteriorLayout.CanWalk(-.2,1,0,17,1,0,true));
            Assert.IsFalse(StationInteriorLayout.CanWalk(6,1,0,6,1,3,true));
            Assert.IsFalse(StationInteriorLayout.Inside(double.NaN,1,0));
        }
        [Test] public void AstronautTradeIsProximityAndCrewGatedAndAtomic()
        {
            var dock=Latched();dock.Advance(8);Assert.IsTrue(dock.SetHatch(true));
            var original=new Inventory();Assert.IsTrue(original.Add(141,6));Inventory result;
            Assert.IsFalse(StationAstronauts.TryTrade(dock,false,0,new SpaceVector(5,0,0),original,out result));
            Assert.IsFalse(StationAstronauts.TryTrade(dock,true,0,new SpaceVector(17,0,0),original,out result));
            Assert.IsFalse(StationAstronauts.TryTrade(dock,true,3,new SpaceVector(5,0,0),original,out result));
            Assert.IsTrue(StationAstronauts.TryTrade(dock,true,0,new SpaceVector(5,0,0),original,out result));
            Assert.AreEqual(6,original.Count(141));Assert.AreEqual(0,original.Count(FuelChain.CompressedAir));
            Assert.AreEqual(0,result.Count(141));Assert.AreEqual(4,result.Count(FuelChain.CompressedAir));
            Assert.IsTrue(dock.SetHatch(false));
            Assert.IsFalse(StationAstronauts.TryTrade(dock,true,0,new SpaceVector(5,0,0),original,out result));
        }
        [Test] public void ResidentsKeepAppearanceAcrossSessionsButNotAllLookAlike()
        {
            var looks=new HashSet<string>();
            for(int i=0;i<24;i++)
            {
                string id="v:story:"+i;var a=SettlerAppearance.For(id,i%3),b=SettlerAppearance.For(id,i%3);
                Assert.AreEqual(a.Skin,b.Skin);Assert.AreEqual(a.Coat,b.Coat);
                Assert.AreEqual(a.Hair,b.Hair);Assert.AreEqual(a.Headwear,b.Headwear);
                looks.Add(a.Skin+":"+a.Coat+":"+a.Hair+":"+a.Headwear+":"+a.Detail);
            }
            Assert.GreaterOrEqual(looks.Count,12);
        }
    }
}
