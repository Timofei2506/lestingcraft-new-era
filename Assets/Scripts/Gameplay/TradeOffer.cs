namespace LestingCraft.Gameplay
{
 public readonly struct TradeOffer
 {
  public readonly int Input,InputCount,Output,OutputCount;
  public TradeOffer(int input,int inputCount,int output,int outputCount){Input=input;InputCount=inputCount;Output=output;OutputCount=outputCount;}
  public static TradeOffer ForJob(int job)=>job==0?new TradeOffer(4,8,111,2):job==1?new TradeOffer(139,4,140,1):new TradeOffer(147,6,138,3);
  public static string Profession(int job)=>job==0?"Фермер":job==1?"Кузнец":"Торговец";
  public static bool CanTrade(Inventory inventory,TradeOffer offer)
  {if(inventory==null||inventory.Count(offer.Input)<offer.InputCount)return false;var copy=inventory.Clone();return copy.Remove(offer.Input,offer.InputCount)&&copy.CanAdd(offer.Output,offer.OutputCount);}
  public static bool Apply(Inventory inventory,TradeOffer offer,out Inventory updated)
  {updated=null;if(!CanTrade(inventory,offer))return false;var copy=inventory.Clone();if(!copy.Remove(offer.Input,offer.InputCount)||!copy.Add(offer.Output,offer.OutputCount))return false;updated=copy;return true;}
 }
}
