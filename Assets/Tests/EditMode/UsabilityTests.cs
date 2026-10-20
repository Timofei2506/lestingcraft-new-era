using NUnit.Framework;
using LestingCraft.Gameplay;
using LestingCraft.World;
namespace LestingCraft.Tests
{
 public class UsabilityTests
 {
  [Test] public void CompleteCreativeCatalogHasSearchAndSafeStacks(){Assert.AreEqual(111,CreativeCatalog.Query("",0).Length);CollectionAssert.Contains(CreativeCatalog.Query("СТЕКЛО",0),44);var bag=new Inventory();Assert.IsFalse(CreativeCatalog.Take(bag,153,false,true,false));Assert.IsTrue(CreativeCatalog.Take(bag,153,false,true,true));Assert.AreEqual(1,bag.Active.count);Assert.AreEqual(Items.Get(153).Durability,bag.Active.durability);}
  [Test] public void TradePreviewDoesNotConsumeAndConfirmIsAtomic(){var bag=new Inventory();bag.Add(4,8);var offer=TradeOffer.ForJob(0);Assert.IsTrue(TradeOffer.CanTrade(bag,offer));Assert.AreEqual(8,bag.Count(4));Assert.IsTrue(TradeOffer.Apply(bag,offer,out var after));Assert.AreEqual(0,after.Count(4));Assert.AreEqual(2,after.Count(111));Assert.AreEqual(8,bag.Count(4));Assert.IsFalse(TradeOffer.Apply(after,offer,out _));}
  [Test] public void OldWorldsKeepTheirGenerationAndNewWorldsUseSeven(){Assert.AreEqual(7,new SessionSave().contentVersion);Assert.AreEqual(11,GameRules.Version);Assert.IsTrue(GameRules.AcceptsRoom(9,11));Assert.IsTrue(GameRules.AcceptsRoom(10,11));Assert.AreEqual(BiomeKind.MixedForest,BiomeGenerator.At(96,48,2506));for(int i=-300;i<300;i+=13)for(int y=0;y<80;y+=9)Assert.AreEqual(TerrainGenerator.SampleContent(i,y,i,2506,Dimension.Lestingia,5),TerrainGenerator.SampleContent(i,y,i,2506,Dimension.Lestingia,6));}
 }
}
