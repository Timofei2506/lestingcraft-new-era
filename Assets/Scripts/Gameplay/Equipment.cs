using System;
namespace LestingCraft.Gameplay
{
 public static class Equipment
 {
  public const int Count=8;
  public static readonly string[] Names={"Головной убор","Одежда","Штаны","Обувь","Шлем","Нагрудник","Поножи","Бронесапоги"};
  public static int Slot(int id){switch(id){case 130:return 0;case 104:return 1;case 131:return 2;case 132:return 3;case 105:return 4;case 106:return 5;case 107:return 6;case 108:return 7;default:return -1;}}
  public static bool Accept(int slot,int id)=>slot>=0&&slot<Count&&Slot(id)==slot;
  public static float Insulation(Inventory inv){float n=0;foreach(var s in inv.equipment)if(s.count>0)n+=Items.Get(s.item).Insulation;return n;}
  public static float ItemProtection(int id)=>id==105||id==108?.09f:id==106?.18f:id==107?.14f:0;
  public static float Protection(Inventory inv)
  {float n=0;for(int i=4;i<Count;i++)if(inv.equipment[i].count>0&&inv.equipment[i].durability>0)n+=ItemProtection(inv.equipment[i].item);return n;}
  public static int ArmorSlot(BodyPart part)=>part==BodyPart.Head?4:part==BodyPart.LeftLeg||part==BodyPart.RightLeg?6:5;
  public static float Protection(Inventory inv,BodyPart part){int i=ArmorSlot(part);var s=inv.equipment[i];float n=s.count>0&&s.durability>0?(i==4?.4f:i==5?.5f:.42f):0;if(i==6&&inv.equipment[7].count>0&&inv.equipment[7].durability>0)n+=.15f;return n;}
  public static void Wear(Inventory inv,float damage,BodyPart part){int first=ArmorSlot(part);for(int i=first;i<=(first==6?7:first);i++){var s=inv.equipment[i];if(s.count<=0)continue;s.durability-=Math.Max(1,(int)Math.Ceiling(damage/8));if(s.durability<=0)inv.equipment[i]=new ItemStack();inv.revision++;}}
  public static bool Equip(Inventory inv,int bagSlot)
  {
   if(bagSlot<0||bagSlot>=Inventory.SlotCount)return false;var s=inv.slots[bagSlot];int target=Slot(s.item);if(s.count!=1||target<0||s.durability<=0&&Items.Get(s.item).Durability>0)return false;
   inv.slots[bagSlot]=inv.equipment[target];inv.equipment[target]=s;inv.revision++;return true;
  }
  public static void Wear(Inventory inv,float damage)
  {int wear=Math.Max(1,(int)Math.Ceiling(damage/8));for(int i=4;i<Count;i++){var s=inv.equipment[i];if(s.count<=0)continue;s.durability-=wear;if(s.durability<=0)inv.equipment[i]=new ItemStack();}inv.revision++;}
 }
}
