using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using LestingCraft.Gameplay;
namespace LestingCraft.Presentation
{
 public sealed partial class GameUI
 {
  Text interactionHUD,biomeHUD,padHUD;
  void BuildUsabilityHUD()
  {
   interactionHUD=Label(content.transform,"",400,494,480,58,18,White,false,TextAnchor.MiddleCenter);
   biomeHUD=Label(content.transform,"",26,814,230,36,17,Muted);
   padHUD=Label(content.transform,"",834,30,425,64,15,Accent,false,TextAnchor.UpperRight);
  }
  bool creativeBag;string catalogSearch="",traderId="";int catalogCategory,catalogPage;RectTransform catalogGrid;Text catalogSummary,tradeSummary;Button tradeButton;
  public int CatalogVisibleCount{get;private set;}public string OpenTraderId=>page==Page.Trade?traderId:"";
  void ShowCreativeBag(){creativeBag=true;ShowInventory();creativeBag=false;}
  public void ShowCreativeCatalog()
  {
   if(!game.Session||!game.Session.Creative)return;
   Clear(Page.Creative);LayoutWidth(1280);Header("Творческий · каталог",false);stationKind=0;inventoryViews.Clear();equipmentStatus=null;craftOutput=null;stoveStatus=null;
   Box(content.transform,16,119,1248,765,new Color(.025f,.043f,.05f,.98f),false);
   Button("Рюкзак / экипировка",424,28,244,52,ShowCreativeBag);
   for(int i=0;i<CreativeCatalog.Categories.Length;i++){int category=i;Button(CreativeCatalog.Categories[i],32+i*152,133,146,43,()=>{catalogCategory=category;catalogPage=0;ShowCreativeCatalog();},i==catalogCategory);}
   Input("Поиск по названию или ID",catalogSearch,"Например: стекло, алмаз, меч…",32,183,1042,v=>{catalogSearch=v;catalogPage=0;RefreshCatalog();});
   Button("Очистить",1090,217,158,58,()=>{catalogSearch="";catalogPage=0;ShowCreativeCatalog();});
   catalogGrid=Rect("Creative catalog grid",content.transform,32,290,1216,428);
   catalogSummary=Label(content.transform,"",260,729,720,40,18,White,false,TextAnchor.MiddleCenter);
   Button("←",32,727,100,43,()=>{catalogPage=Mathf.Max(0,catalogPage-1);RefreshCatalog();});Button("→",140,727,100,43,()=>{catalogPage++;RefreshCatalog();});
   Button("Удалить с курсора",984,727,264,43,()=>{game.Session.Inventory.cursor=new ItemStack();game.Session.Inventory.revision++;RefreshCraftInventory();});
   Label(content.transform,"ЛКМ — взять стак · ПКМ — 1 · Shift-клик — в выбранный слот · 1–9 — выбрать слот",32,773,1216,28,17,Muted);
   for(int i=0;i<9;i++)CreateCell(i,32+i*99,807,92,67);
   Button("← В мир",1034,813,214,55,game.ToggleInventory,true);
   cursorRoot=Rect("Carried creative catalog stack",content.transform,0,0,64,80);cursorImage=cursorRoot.gameObject.AddComponent<RawImage>();cursorImage.raycastTarget=false;cursorCount=Label(cursorRoot,"",0,50,62,29,15,White,false,TextAnchor.MiddleRight);
   status=null;RefreshCraftInventory();RefreshCatalog();
  }
  void RefreshCatalog()
  {
   if(!catalogGrid||page!=Page.Creative)return;HideItemTooltip();
   for(int i=catalogGrid.childCount-1;i>=0;i--){var child=catalogGrid.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}
   if(catalogCategory==7)
   {
    string query=(catalogSearch??"").Trim();int choices=0;
    if(query.Length==0||"Стартовый стол".IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||"космодром".IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)
    {Button("СТАРТОВЫЙ СТОЛ + ШАТТЛ\nПоставить комплекс",24,18,555,154,()=>{game.ArmLaunchPlacement("pad");game.ToggleInventory();},true,catalogGrid);choices++;}
    if(query.Length==0||"Шаттл".IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||"ракета".IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)
    {Button("ШАТТЛ + СТАРТОВЫЙ СТОЛ\nПоставить комплекс",610,18,555,154,()=>{game.ArmLaunchPlacement("shuttle");game.ToggleInventory();},true,catalogGrid);choices++;}
    Label(catalogGrid,"Оба чертежа создают один действующий комплекс: платформа, рампа, лифт и кабина.\nПрицелитесь в землю у начала рампы и нажмите ПКМ. Старт полёта пока заблокирован.",32,208,1140,126,21,Muted,false,TextAnchor.MiddleCenter);
    CatalogVisibleCount=choices;catalogSummary.text=choices+" чертежа · по одному комплексу на мир";return;
   }
   int[] ids=CreativeCatalog.Query(catalogSearch,catalogCategory);int pages=Mathf.Max(1,(ids.Length+31)/32);catalogPage=Mathf.Clamp(catalogPage,0,pages-1);CatalogVisibleCount=0;
   for(int i=catalogPage*32;i<Mathf.Min(ids.Length,(catalogPage+1)*32);i++)
   {
    int slot=i-catalogPage*32,id=ids[i];var entry=Box(catalogGrid,(slot%8)*152,(slot/8)*107,146,102,Panel);entry.raycastTarget=true;
    var action=entry.gameObject.AddComponent<CreativeCatalogSlot>();action.Owner=this;action.Item=id;AttachHover(entry.gameObject,id);
    Icon(entry.transform,id,49,2,48);Label(entry.transform,Items.Get(id).Name,8,50,130,51,16,White,false,TextAnchor.UpperCenter);CatalogVisibleCount++;
   }
   catalogSummary.text=ids.Length+" предметов · страница "+(catalogPage+1)+" / "+pages;
   if(ids.Length==0)Label(catalogGrid,"Ничего не найдено. Измените запрос или категорию.",20,70,1160,60,22,Muted,false,TextAnchor.MiddleCenter);
  }
  public bool CatalogTake(int id,bool single,bool directHotbar)
  {
   if(page!=Page.Creative||!game.Session||!CreativeCatalog.Take(game.Session.Inventory,id,single,directHotbar,game.Session.Creative))return false;
   RefreshCraftInventory();game.Session.Checkpoint();return true;
  }
  public void ShowTrade(string actorId)
  {
   if(!game.Session||!game.Session.Living.TryTrader(actorId,out var actor))return;
   traderId=actorId;Clear(Page.Trade);LayoutWidth(1280);Header("Обмен с жителем",false);inventoryViews.Clear();cursorRoot=null;
   Box(content.transform,180,150,920,580,new Color(.025f,.043f,.05f,1),false);
   var offer=TradeOffer.ForJob(actor.job);string profession=TradeOffer.Profession(actor.job);
   Label(content.transform,profession,224,180,820,52,30,Accent,true);Label(content.transform,"Проверьте предложение и подтвердите обмен.\nПКМ только открывает окно — ресурсы не списываются.",224,250,820,78,21,Muted);
   Icon(content.transform,offer.Input,260,370,108);Label(content.transform,offer.InputCount+" × "+Items.Get(offer.Input).Name,220,484,330,64,22,White,false,TextAnchor.MiddleCenter);
   Label(content.transform,"→",574,392,100,72,42,Accent,false,TextAnchor.MiddleCenter);
   Icon(content.transform,offer.Output,786,370,108);Label(content.transform,offer.OutputCount+" × "+Items.Get(offer.Output).Name,698,484,330,64,22,White,false,TextAnchor.MiddleCenter);
   tradeSummary=Label(content.transform,"",224,555,820,64,20,White,false,TextAnchor.MiddleCenter);
   tradeButton=Button("Обменять",408,642,464,62,()=>{game.Session.Living.TryTrade(traderId);RefreshTrade();},true);tradeButton.name="Trade.Confirm";
   Button("← Закрыть",32,814,230,60,game.Resume);Label(content.transform,"Esc / E — закрыть · обмен доступен только рядом с жителем",300,824,948,44,18,Muted);
   RefreshTrade();
  }
  void RefreshTrade()
  {
   if(page!=Page.Trade||!tradeSummary||!game.Session)return;var living=game.Session.Living;
   if(!living.TryTrader(traderId,out var actor)){tradeSummary.text="Житель отошёл слишком далеко или недоступен";tradeButton.interactable=false;return;}
   var offer=TradeOffer.ForJob(actor.job);int have=game.Session.Inventory.Count(offer.Input);bool pending=living.Pending;
   bool available=TradeOffer.CanTrade(game.Session.Inventory,offer);tradeButton.interactable=!pending&&available;
   string availability=available?"Обмен доступен":have<offer.InputCount?"Не хватает ресурсов":"Нет места для результата";
   tradeSummary.text=pending?"Ожидаем подтверждение сервера…":"У вас: "+have+" / "+offer.InputCount+" · "+availability+(string.IsNullOrEmpty(game.Session.Notice)?"":"\n"+game.Session.Notice);
  }
  void UpdateUsability()
  {
   if(page==Page.HUD&&game.Session)
   {
    if(interactionHUD)interactionHUD.text=game.Session.Living.Hint;
    var position=game.Session.Player.transform.position;
    if(biomeHUD)biomeHUD.text=game.Session.World.ContentVersion>=7&&game.Session.World.Dimension==LestingCraft.World.Dimension.Lestingia?LestingCraft.World.BiomeGenerator.Name(LestingCraft.World.BiomeGenerator.At(Mathf.FloorToInt(position.x),Mathf.FloorToInt(position.z),game.Session.World.Seed)):"";
    if(padHUD)
    {
     if(game.InMoonFlight)padHUD.text=game.MoonFlightStatus;
     else if(!game.Online||!game.HasSharedLaunchSite)padHUD.text="";
     else if(Mathf.Abs(position.x-168)<50&&Mathf.Abs(position.z-168)<40)
      padHUD.text=$"КОСМОДРОМ · X {position.x:0} / Z {position.z:0}\nЛифт: X 148 · Z 171 · E — взаимодействие";
     else if(Mathf.Abs(position.x-148)<22&&position.z>45&&position.z<183)
      padHUD.text=$"КОСМОДРОМ · X {position.x:0} / Z {position.z:0}\nПо эстакаде к лифту: X 148 · Z 171";
     else padHUD.text=$"КОСМОДРОМ · X {position.x:0} / Z {position.z:0}\nИдите к началу эстакады: X 148 · Z 58";
    }
    return;
   }
   if(page==Page.Trade){RefreshTrade();return;}
   if(page!=Page.Creative)return;
   foreach(var cell in inventoryViews)if(cell.Index<9)cell.Icon.transform.parent.GetComponent<Image>().color=cell.Index==game.Session.Inventory.selected?new Color(.36f,.49f,.30f,.97f):Panel;
   if(EditingText||Keyboard.current==null)return;
   var k=Keyboard.current;var keys=new[]{k.digit1Key,k.digit2Key,k.digit3Key,k.digit4Key,k.digit5Key,k.digit6Key,k.digit7Key,k.digit8Key,k.digit9Key};
   for(int i=0;i<keys.Length;i++)if(keys[i].wasPressedThisFrame){game.Session.Inventory.selected=i;game.Session.Inventory.revision++;}
  }
 }
 public sealed class CreativeCatalogSlot:MonoBehaviour,IPointerClickHandler
 {
  public GameUI Owner;public int Item;
  public void OnPointerClick(PointerEventData data)
  {if(!Owner||data.button!=PointerEventData.InputButton.Left&&data.button!=PointerEventData.InputButton.Right)return;var k=Keyboard.current;Owner.CatalogTake(Item,data.button==PointerEventData.InputButton.Right,k!=null&&(k.leftShiftKey.isPressed||k.rightShiftKey.isPressed));}
 }
}
