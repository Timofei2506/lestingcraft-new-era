using NUnit.Framework;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
using LestingCraft.World;
namespace LestingCraft.Tests
{
    public sealed class CraftingSurvivalTests
    {
        static ItemStack S(int item,int count=1,int durability=0)=>new ItemStack{item=item,count=count,durability=durability};
        [Test] public void LogsMatchAnywhereInsidePersonalGrid(){var inv=new Inventory();inv.grid[4]=S(4,3);Assert.AreEqual(120,CraftingTable.Match(inv.grid,2).Output);Assert.AreEqual(4,CraftingTable.Take(inv,2,false));Assert.AreEqual(2,inv.grid[4].count);Assert.AreEqual(4,inv.cursor.count);}
        [Test] public void StickRecipeIsVerticalNotShapeless(){var inv=new Inventory();inv.grid[0]=S(120);inv.grid[1]=S(120);Assert.IsNull(CraftingTable.Match(inv.grid,2));inv.grid[1]=new ItemStack();inv.grid[3]=S(120);Assert.AreEqual(121,CraftingTable.Match(inv.grid,2).Output);}
        [Test] public void PickRequiresThreeByThreeAndOnlyConsumesOnePerCell(){var inv=new Inventory();inv.Add(120,9);inv.Add(121,6);Assert.IsFalse(CraftingTable.Fill(inv,CraftingTable.All[2],2));Assert.IsTrue(CraftingTable.Fill(inv,CraftingTable.All[2],3));Assert.IsNull(CraftingTable.Match(inv.grid,2));Assert.AreEqual(1,CraftingTable.Take(inv,3,false));Assert.AreEqual(100,inv.cursor.item);Assert.AreEqual(60,inv.cursor.durability);Assert.AreEqual(6,inv.Count(120));Assert.AreEqual(4,inv.Count(121));}
        [Test] public void MirroredAxeMatches(){var inv=new Inventory();inv.grid[0]=S(1);inv.grid[1]=S(1);inv.grid[3]=S(121);inv.grid[4]=S(1);inv.grid[6]=S(121);Assert.AreEqual(102,CraftingTable.Match(inv.grid,3).Output);}
        [Test] public void ExtraOrHiddenIngredientInvalidatesRecipe(){var inv=new Inventory();inv.grid[0]=S(4);inv.grid[8]=S(2);Assert.IsNull(CraftingTable.Match(inv.grid,2));Assert.IsNull(CraftingTable.Match(inv.grid,3));}
        [Test] public void IncompatibleCursorDoesNotConsumeCraft(){var inv=new Inventory();inv.grid[0]=S(4,2);inv.cursor=S(1);Assert.AreEqual(0,CraftingTable.Take(inv,2,false));Assert.AreEqual(2,inv.grid[0].count);}
        [Test] public void FullBagDoesNotConsumeShiftCraft(){var inv=new Inventory();inv.Add(1,45*64);inv.grid[0]=S(4);Assert.AreEqual(0,CraftingTable.Take(inv,2,true));Assert.AreEqual(1,inv.grid[0].count);}
        [Test] public void ShiftCraftMakesMaximumWithoutDuplicating(){var inv=new Inventory();inv.grid[0]=S(4,7);Assert.AreEqual(28,CraftingTable.Take(inv,2,true));Assert.AreEqual(28,inv.Count(120));Assert.AreEqual(0,inv.grid[0].count);Assert.AreEqual(0,CraftingTable.Take(inv,2,true));}
        [Test] public void FailedBookFillIsAtomicAndRetainsPreviousGrid(){var inv=new Inventory();inv.grid[0]=S(4,2);Assert.IsFalse(CraftingTable.Fill(inv,CraftingTable.All[3],3));Assert.AreEqual(2,inv.grid[0].count);Assert.AreEqual(0,inv.Count(4));}
        [Test] public void FullBagReturnKeepsCraftingMaterials(){var inv=new Inventory();inv.Add(1,45*64);inv.grid[0]=S(4);Assert.IsFalse(CraftingTable.ReturnGrid(inv));Assert.AreEqual(4,inv.grid[0].item);}
        [Test] public void RightClickSplitsCeilingHalfAndPlacesOne(){var inv=new Inventory();inv.slots[0]=S(4,9);InventoryInteraction.Click(inv,0,true,false);Assert.AreEqual(5,inv.cursor.count);Assert.AreEqual(4,inv.slots[0].count);InventoryInteraction.Click(inv,45,true,false);Assert.AreEqual(1,inv.grid[0].count);Assert.AreEqual(4,inv.cursor.count);}
        [Test] public void ToolSwapAndShiftRetainDurability(){var inv=new Inventory();inv.slots[0]=S(101,1,17);InventoryInteraction.Click(inv,0,false,false);InventoryInteraction.Click(inv,45,false,false);InventoryInteraction.Click(inv,45,false,true);Assert.AreEqual(17,inv.slots[0].durability);InventoryInteraction.Click(inv,0,false,true);Assert.AreEqual(17,inv.slots[9].durability);}
        [Test] public void ShiftMovesBetweenBagAndHotbar(){var inv=new Inventory();inv.slots[0]=S(4,12);InventoryInteraction.Click(inv,0,false,true);Assert.AreEqual(12,inv.slots[9].count);InventoryInteraction.Click(inv,9,false,true);Assert.AreEqual(12,inv.slots[0].count);Assert.AreEqual(12,inv.Count(4));}
        [Test] public void FurnaceCooksOnlyWithFuelAfterEightSeconds(){var f=new FurnaceState{input=S(110,2),fuel=S(4)};for(int i=0;i<7;i++)f.Step(1);Assert.AreEqual(0,f.output.count);f.Step(1);Assert.AreEqual(112,f.output.item);Assert.AreEqual(1,f.output.count);Assert.AreEqual(1,f.input.count);Assert.AreEqual(0,f.fuel.count);Assert.That(f.burn,Is.EqualTo(10).Within(.001));}
        [Test] public void SmokeCookingBudgetAllowsSlowRenderingButHasBothLimits()
        {
            Assert.IsFalse(LestingCraft.Runtime.BuildSmokeProbe.CookingWaitExpired(0,0,4,20),"20 wall seconds are not necessarily 8 game seconds");
            Assert.IsFalse(LestingCraft.Runtime.BuildSmokeProbe.CookingWaitExpired(0,0,9,90));
            Assert.IsTrue(LestingCraft.Runtime.BuildSmokeProbe.CookingWaitExpired(0,0,14,100),"Failure after adequate simulation time");
            Assert.IsTrue(LestingCraft.Runtime.BuildSmokeProbe.CookingWaitExpired(0,0,0,150),"A frozen simulation must not wait forever");
        }
        [Test] public void FurnaceWithoutFuelCannotProgress(){var f=new FurnaceState{input=S(114)};for(int i=0;i<20;i++)f.Step(1);Assert.AreEqual(0,f.output.count);Assert.AreEqual(1,f.input.count);}
        [Test] public void BlockedOutputDoesNotBurnNewFuel(){var f=new FurnaceState{input=S(110),fuel=S(4),output=S(112,32)};f.Step(1);Assert.AreEqual(1,f.fuel.count);Assert.AreEqual(0,f.burn);}
        [Test] public void InvalidFurnaceDepositsAndOutputDepositsAreRejected(){var inv=new Inventory();inv.cursor=S(1);InventoryInteraction.Click(inv,54,false,false);InventoryInteraction.Click(inv,55,false,false);InventoryInteraction.Click(inv,56,false,false);Assert.AreEqual(1,inv.cursor.item);Assert.AreEqual(0,inv.furnace.input.count);Assert.AreEqual(0,inv.furnace.fuel.count);Assert.AreEqual(0,inv.furnace.output.count);}
        [Test] public void ChangingSmeltRecipeCannotReusePreviousProgress(){var f=new FurnaceState{input=S(110),fuel=S(4)};for(int i=0;i<6;i++)f.Step(1);f.input=S(114);f.Step(1);Assert.AreEqual(1,f.progress);Assert.AreEqual(0,f.output.count);}
        [Test] public void SmeltingWoodCreatesUsableCharcoal(){var f=new FurnaceState{input=S(4),fuel=S(120,2)};for(int i=0;i<8;i++)f.Step(1);Assert.AreEqual(123,f.output.item);Assert.AreEqual(40,FurnaceState.FuelSeconds(123));}
        [Test] public void SavePreservesGridCursorFurnaceGraveAndOxygen(){var save=new SessionSave();save.player.inventory.grid[0]=S(4,8);save.player.inventory.cursor=S(101,1,12);save.player.inventory.furnace.input=S(114,3);save.player.inventory.furnace.progress=3;save.player.inventory.furnace.recipe=114;save.player.graveExtras=save.player.inventory.ExtraStacks();save.player.survival.air=43;var copy=JsonUtility.FromJson<SessionSave>(JsonUtility.ToJson(save));copy.player.Normalize();Assert.AreEqual(8,copy.player.inventory.grid[0].count);Assert.AreEqual(12,copy.player.inventory.cursor.durability);Assert.AreEqual(3,copy.player.inventory.furnace.progress);Assert.AreEqual(21,copy.player.graveExtras.Length);Assert.AreEqual(43,copy.player.survival.air);}
        [Test] public void GraveWithMoreThan45StacksCanBeRecoveredPartiallyWithoutLoss()
        {
            var inv=new Inventory();var grave=new ItemStack[46];for(int i=0;i<46;i++)grave[i]=S(101,1,17);
            Assert.AreEqual(45,inv.RecoverStacks(grave));Assert.AreEqual(1,grave[45].count);Assert.AreEqual(45,inv.Count(101));Assert.AreEqual(17,inv.slots[0].durability);
            InventoryInteraction.Click(inv,0,false,false);Assert.AreEqual(1,inv.RecoverStacks(grave));Assert.AreEqual(0,grave[45].count);Assert.AreEqual(17,inv.cursor.durability);
        }
        [Test] public void OlderInventoryWithoutNewFieldsNormalizesSafely(){var inv=new Inventory{grid=null,cursor=null,furnace=null};inv.Normalize();Assert.AreEqual(9,inv.grid.Length);Assert.IsNotNull(inv.cursor);Assert.IsNotNull(inv.furnace);}
        [Test] public void DrowningDamagesSurvivalButNotCreative(){var v=new SurvivalState();for(int i=0;i<15;i++)v.StepWater(1,GameMode.Survival,true,true);Assert.Less(v.health,100);Assert.AreEqual(1,v.wetness);v.StepWater(1,GameMode.Survival,false,false);Assert.Greater(v.air,0);var c=new SurvivalState();for(int i=0;i<30;i++)c.StepWater(1,GameMode.Creative,true,true);Assert.AreEqual(100,c.health);Assert.AreEqual(100,c.air);}
        [Test] public void EveryRegisteredSolidBlockHasExplicitPbrFaces(){var textures=Resources.Load<Texture2DArray>("Generated/Albedo");Assert.AreEqual(24,textures.depth);for(ushort block=1;block<=18;block++)for(int face=0;face<6;face++){int layer=SurfaceLayers.ForFace(block,face);Assert.That(layer,Is.InRange(0,23));Assert.AreNotEqual("Air",BlockRegistry.Get(block).Name);}Assert.AreEqual(17,SurfaceLayers.ForFace(16,2));Assert.AreEqual(20,SurfaceLayers.ForFace(17,5));Assert.AreEqual(16,SurfaceLayers.ForFace(18,0));}
    }
}
