using NUnit.Framework;using UnityEngine;using LestingCraft.Gameplay;
namespace LestingCraft.Tests
{
    public sealed class SurvivalTests
    {
        [Test] public void AtmosphericAssetsExist()
        {
            Assert.IsNotNull(Resources.Load<Material>("Generated/Atmosphere"));
            Assert.IsNotNull(Resources.Load<Texture3D>("Generated/CloudNoise"));
            Assert.IsNotNull(Resources.Load<Material>("Generated/Precipitation"));
            Assert.IsNotNull(Resources.Load<Material>("Generated/Fire"));
        }
        [Test] public void InventoryStartsWith45EmptySlotsAndNineQuickSlots(){var inv=new Inventory();Assert.AreEqual(45,inv.slots.Length);Assert.AreEqual(9,Inventory.HotbarCount);Assert.AreEqual(0,inv.Count(1));}
        [Test] public void StacksSplitAndMergeWithoutLoss(){var inv=new Inventory();Assert.IsTrue(inv.Add(4,100));Assert.AreEqual(100,inv.Count(4));Assert.AreEqual(64,inv.slots[0].count);Assert.AreEqual(36,inv.slots[1].count);inv.Transfer(0,1);Assert.AreEqual(100,inv.Count(4));Assert.AreEqual(64,inv.slots[1].count);}
        [Test] public void FailedAddIsAtomic(){var inv=new Inventory();for(int i=0;i<45;i++)Assert.IsTrue(inv.Add(1,64));Assert.IsFalse(inv.Add(2,1));Assert.AreEqual(2880,inv.Count(1));Assert.AreEqual(0,inv.Count(2));}
        [Test] public void FailedRemoveIsAtomic(){var inv=new Inventory();inv.Add(4,2);Assert.IsFalse(inv.Remove(4,3));Assert.AreEqual(2,inv.Count(4));}
        [Test] public void ToolsDoNotStackAndWearOut(){var inv=new Inventory();inv.Add(100,2);Assert.AreEqual(1,inv.slots[0].count);Assert.AreEqual(1,inv.slots[1].count);for(int i=0;i<60;i++)inv.WearTool();Assert.AreEqual(1,inv.Count(100));}
        [Test] public void CraftConsumesOnlyRequiredMaterials(){var inv=new Inventory();inv.Add(4,2);Assert.IsTrue(Recipes.All[0].Craft(inv));Assert.AreEqual(1,inv.Count(4));Assert.AreEqual(4,inv.Count(120));}
        [Test] public void CraftMissingIngredientIsAtomic(){var inv=new Inventory();inv.Add(120,3);Assert.IsFalse(Recipes.All[2].Craft(inv));Assert.AreEqual(3,inv.Count(120));Assert.AreEqual(0,inv.Count(100));}
        [Test] public void FullInventoryCraftIsAtomic(){var inv=new Inventory();inv.Add(4,64);for(int i=1;i<45;i++)inv.Add(1,64);Assert.IsFalse(Recipes.All[0].Craft(inv));Assert.AreEqual(64,inv.Count(4));}
        [Test] public void CreativeIgnoresSurvivalAndDamage(){var v=new SurvivalState();v.Step(2,GameMode.Creative,-20,1,false,false,true,0);v.Damage(100,GameMode.Creative);Assert.AreEqual(100,v.health);Assert.AreEqual(100,v.hunger);Assert.AreEqual(0,v.wetness);}
        [Test] public void RainWetsAndShelterDries(){var v=new SurvivalState();v.Step(2,GameMode.Survival,2,1,false,false,false,0);Assert.Greater(v.wetness,0);v.Step(2,GameMode.Survival,2,1,true,false,false,0);Assert.AreEqual(0,v.wetness);}
        [Test] public void CampfireImprovesBodyTemperature(){var cold=new SurvivalState{temperature=34};var warm=new SurvivalState{temperature=34};for(int i=0;i<100;i++){cold.Step(1,GameMode.Survival,-10,0,false,false,false,0);warm.Step(1,GameMode.Survival,-10,0,true,true,false,10);}Assert.Greater(warm.temperature,cold.temperature);}
        [Test] public void DeathAndRespawnDoNotLeaveDeadFlag(){var v=new SurvivalState();v.Damage(100,GameMode.Survival);Assert.IsTrue(v.dead);v.Respawn();Assert.IsFalse(v.dead);Assert.AreEqual(100,v.health);}
        [Test] public void WeatherIsDeterministicAcrossClients(){for(int realm=0;realm<5;realm++)for(int t=0;t<5000;t+=177){var a=WeatherSample.At(2506,realm,t);var b=WeatherSample.At(2506,realm,t);Assert.AreEqual(a.Kind,b.Kind);Assert.AreEqual(a.Air,b.Air);}}
        [Test] public void OtherRealmsAreLockedOnNewCampaign(){var c=new CampaignProgress();Assert.IsTrue(c.CanEnter(0));for(int i=1;i<5;i++)Assert.IsFalse(c.CanEnter(i));}
        [Test] public void SaveRoundTripPreservesInventoryAndHardcoreEnd(){var save=new SessionSave{seed=2506,mode=1};save.player.inventory.Add(101,1);save.campaign.runEnded=true;var copy=JsonUtility.FromJson<SessionSave>(JsonUtility.ToJson(save));copy.player.Normalize();Assert.AreEqual(1,copy.player.inventory.Count(101));Assert.IsTrue(copy.campaign.runEnded);}
        [Test] public void InventoryNormalizeHandlesMissingSlotsAndUnknownItems(){var inv=new Inventory{slots=new ItemStack[45]};inv.slots[0]=new ItemStack{item=999,count=64};inv.Normalize();Assert.AreEqual(0,inv.slots[0].item);Assert.IsNotNull(inv.slots[44]);}
    }
}
