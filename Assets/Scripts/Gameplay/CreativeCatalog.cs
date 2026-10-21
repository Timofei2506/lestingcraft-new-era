using System;
using System.Collections.Generic;
namespace LestingCraft.Gameplay
{
 public static class CreativeCatalog
 {
  public static readonly string[] Categories={"Все","Блоки","Инструменты","Оружие","Экипировка","Еда","Материалы","Космос"};
  public static int Category(int id)
  {var item=Items.Get(id);if(item.Block>0)return 1;if(Items.IsPick(id)||id==102||id==Items.Dosimeter)return 2;if(id==103||id>=152&&id<=165)return 3;if(Equipment.Slot(id)>=0)return 4;if(item.Food>0||id==113||id==114)return 5;return 6;}
  public static int[] Query(string text,int category)
  {var result=new List<int>();string query=(text??"").Trim();foreach(int id in Items.CatalogIds){if(id==Items.ThermonukeId)continue;if(category>0&&Category(id)!=category)continue;if(query.Length>0&&Items.Get(id).Name.IndexOf(query,StringComparison.OrdinalIgnoreCase)<0&&id.ToString()!=query)continue;result.Add(id);}return result.ToArray();}
  public static bool Take(Inventory inventory,int id,bool single,bool directHotbar,bool creative)
  {
   if(!creative||inventory==null||id==Items.ThermonukeId||!Items.Exists(id))return false;var item=Items.Get(id);
   var stack=new ItemStack{item=id,count=single?1:item.MaxStack,durability=item.Durability};
   // Replacing a catalog cursor is deliberate: these are infinite creative supplies, not survival transactions.
   if(directHotbar)inventory.slots[Math.Max(0,Math.Min(8,inventory.selected))]=stack;else inventory.cursor=stack;
   inventory.revision++;return true;
  }
 }
}
