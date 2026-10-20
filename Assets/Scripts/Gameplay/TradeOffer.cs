namespace LestingCraft.Gameplay
{
 public readonly struct TradeOffer
 {
  public const int ProfessionCount = 10;
  public readonly int Input,InputCount,Output,OutputCount;
  public TradeOffer(int input,int inputCount,int output,int outputCount){Input=input;InputCount=inputCount;Output=output;OutputCount=outputCount;}

  // Jobs 0..2 keep their exact original exchanges for 100% legacy & server compatibility;
  // Jobs 3..9 expand village life across astronomy, herbalism, carpentry, mining, guard duty, lore & cosmodrome engineering!
  public static TradeOffer ForJob(int job)
  {
   switch (job < 0 ? 0 : job % ProfessionCount)
   {
    case 0: return new TradeOffer(4, 8, 111, 2);    // Фермер: 8 Древесины -> 2 Походного хлеба
    case 1: return new TradeOffer(139, 4, 140, 1);  // Кузнец: 4 Кожи -> 1 Железный слиток
    case 2: return new TradeOffer(147, 6, 138, 3);  // Торговец: 6 Угля -> 3 Жареного мяса
    case 3: return new TradeOffer(145, 4, 11, 2);   // Звездочёт: 4 Кварца -> 2 Кристалла
    case 4: return new TradeOffer(110, 6, 112, 4);  // Лекарь: 6 Лесных ягод -> 4 Печёные ягоды
    case 5: return new TradeOffer(4, 6, 55, 1);     // Плотник: 6 Древесины -> 1 Уютная кровать
    case 6: return new TradeOffer(147, 4, 56, 2);   // Шахтёр: 4 Угля -> 2 Кованых фонаря
    case 7: return new TradeOffer(140, 2, 165, 12); // Стражник: 2 Железных слитка -> 12 Стрел
    case 8: return new TradeOffer(120, 8, 57, 1);   // Библиотекарь: 8 Досок -> 1 Книжный шкаф
    default: return new TradeOffer(140, 3, 173, 2); // Инженер космодрома: 3 Железных слитка -> 2 Керосина
   }
  }

  public static string Profession(int job)
  {
   string[] names =
   {
    "Фермер","Кузнец","Торговец","Звездочёт","Лекарь",
    "Плотник","Шахтёр","Стражник","Библиотекарь","Инженер космодрома"
   };
   return names[job < 0 ? 0 : job % names.Length];
  }

  public static bool CanTrade(Inventory inventory,TradeOffer offer)
  {if(inventory==null||inventory.Count(offer.Input)<offer.InputCount)return false;var copy=inventory.Clone();return copy.Remove(offer.Input,offer.InputCount)&&copy.CanAdd(offer.Output,offer.OutputCount);}
  public static bool Apply(Inventory inventory,TradeOffer offer,out Inventory updated)
  {updated=null;if(!CanTrade(inventory,offer))return false;var copy=inventory.Clone();if(!copy.Remove(offer.Input,offer.InputCount)||!copy.Add(offer.Output,offer.OutputCount))return false;updated=copy;return true;}
 }
}
