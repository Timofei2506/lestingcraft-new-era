using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using LestingCraft.Runtime;
using LestingCraft.World;
using LestingCraft.Gameplay;

namespace LestingCraft.Presentation
{
    /// <summary>Native scalable uGUI, no IMGUI or embedded webpage. All fonts/art are local.</summary>
    public sealed class GameUI : MonoBehaviour
    {
        enum Page{Home,World,Rooms,Settings,Pause,Inventory,Death,HUD}
        static readonly Color Ink=new Color(.055f,.086f,.10f), Panel=new Color(.07f,.115f,.13f,.96f), Muted=new Color(.58f,.66f,.66f), White=new Color(.91f,.94f,.90f), Accent=new Color(.88f,.77f,.53f);
        PrototypeGame game;Canvas canvas;RectTransform frame;GameObject content,background;Font body,display;Sprite rounded;
        readonly List<GameObject> transient=new List<GameObject>();readonly List<Button> actions=new List<Button>();
        Page page,settingsBack;Text status,realmHUD,netHUD,perfHUD,loadingHUD;Image[] slots;RectTransform[] slotRects;
        Text vitalsHUD,objectiveHUD,noticeHUD,healthHUD;HealthSilhouette healthBody;RawImage[] slotIcons;Text[] slotCounts;ItemHover[] slotHovers;RectTransform tooltip;Text tooltipText;int lastInventoryRevision=-1,lastSlot=-1;
        Image miningBar;Text miningLabel;GameObject chatRoot;Text chatText;InputField chatInput;int chatRevision=-1;string lastNotice="";float noticeAt;
        public string ChatText=>chatInput?chatInput.text:"";
        public void SetChatOpen(bool open)
        {
            if(!chatInput)return;chatInput.gameObject.SetActive(open);if(open){chatRoot.SetActive(true);chatInput.text="";StartCoroutine(FocusChat());}
            else {chatInput.DeactivateInputField();UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);}
        }
        System.Collections.IEnumerator FocusChat(){yield return null;if(chatInput&&game.ChatOpen){chatInput.text="";chatInput.ActivateInputField();}}
        sealed class CellView {public int Index;public RawImage Icon;public Text Count;public ItemHover Hover;}
        readonly List<CellView> inventoryViews=new List<CellView>();int stationKind,craftWidth=2;Vector3Int stationCell;
        RawImage craftOutput,cursorImage;Text craftCount,cursorCount,stoveStatus;RectTransform cursorRoot;int craftRevision=-1;
        public bool InventoryOpen=>page==Page.Inventory;
        public bool IsReady=>ready;
        public string ScreenName=>page.ToString();
        float timer,averageFrame=.016f;ushort lastSelection;bool ready;
        public void Initialize(PrototypeGame owner)
        {
            Debug.Log("[Presentation] Loading local UI fonts and sprites");
            game=owner;body=Resources.Load<Font>("UI/Manrope");display=Resources.Load<Font>("UI/Unbounded");
            if(!body)body=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(!display)display=body;
            if(!body)throw new InvalidOperationException("UI font could not be loaded.");
            rounded=MenuArtwork.Rounded;
            if(!rounded)throw new InvalidOperationException("Procedural UI chrome could not be created.");
            Debug.Log("[Presentation] Creating canvas");
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scale=gameObject.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1600,900);scale.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            if(!EventSystem.current){var input=new GameObject("Interface Input");input.AddComponent<EventSystem>();input.AddComponent<InputSystemUIInputModule>();}
            background=Rect("Menu Art",transform,0,0,0,0).gameObject;Stretch(background.GetComponent<RectTransform>());
            var image=background.AddComponent<RawImage>();image.texture=MenuArtwork.Backdrop;image.color=image.texture?Color.white:new Color(.035f,.065f,.078f);image.raycastTarget=false;background.AddComponent<MenuBackdropFit>();
            var shade=Rect("Art Shade",background.transform,0,0,0,0);Stretch(shade);var shading=shade.gameObject.AddComponent<RawImage>();shading.texture=MenuArtwork.Shade;shading.raycastTarget=false;
            frame=Rect("1600x900 Interface",transform,0,0,1600,900);frame.anchorMin=frame.anchorMax=new Vector2(.5f,.5f);frame.pivot=new Vector2(.5f,.5f);frame.anchoredPosition=Vector2.zero;
            content=Rect("Content",frame,0,0,1600,900).gameObject;ShowHome();ready=true;
            Debug.Log("[Presentation] Home screen created");
        }
        static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var obj=new GameObject(name,typeof(RectTransform));var rt=obj.GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        static void Stretch(RectTransform rt){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,.5f);rt.offsetMin=rt.offsetMax=Vector2.zero;}
        void Clear(Page next)
        {
            HideItemTooltip();page=next;content.SetActive(true);
            for(int i=content.transform.childCount-1;i>=0;i--){var child=content.transform.GetChild(i).gameObject;child.SetActive(false);if(Application.isPlaying)Destroy(child);else DestroyImmediate(child);}
            actions.Clear();transient.Clear();status=null;
            background.SetActive(next!=Page.HUD);lastSelection=0;
            if(game.InWorld&&next!=Page.HUD)
            {
                background.SetActive(false);var veil=Box(content.transform,0,0,1600,900,new Color(.02f,.04f,.045f,.91f),false);
                veil.raycastTarget=true;
            }
        }
        Image Box(Transform parent,float x,float y,float w,float h,Color color,bool round=true)
        {
            var rt=Rect("Panel",parent,x,y,w,h);var image=rt.gameObject.AddComponent<Image>();image.color=color;
            if(round){image.sprite=rounded;image.type=Image.Type.Sliced;}image.raycastTarget=false;return image;
        }
        Text Label(Transform parent,string text,float x,float y,float w,float h,int size,Color color,bool title=false,TextAnchor align=TextAnchor.UpperLeft)
        {
            var rt=Rect(text.Length>30?text.Substring(0,30):text,parent,x,y,w,h);var label=rt.gameObject.AddComponent<Text>();
            label.text=text;label.font=title?display:body;label.fontSize=size;label.color=color;label.alignment=align;label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.supportRichText=false;label.lineSpacing=1.12f;return label;
        }
        Button Button(string text,float x,float y,float w,float h,Action action,bool primary=false,Transform parent=null)
        {
            parent=parent?parent:content.transform;var image=Box(parent,x,y,w,h,primary?Accent:Panel);image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.13f,1.13f,1.13f);colors.pressedColor=new Color(.83f,.87f,.85f);colors.disabledColor=new Color(.5f,.5f,.5f,.6f);colors.fadeDuration=.14f;button.colors=colors;
            var stroke=Rect("Focus border",image.transform,0,0,0,0);Stretch(stroke);var border=stroke.gameObject.AddComponent<Image>();border.sprite=MenuArtwork.Outline;border.type=Image.Type.Sliced;border.raycastTarget=false;border.color=new Color(Accent.r,Accent.g,Accent.b,.2f);
            var feedback=image.gameObject.AddComponent<MenuButtonFeedback>();feedback.Border=border;feedback.Tint=Accent;
            button.onClick.AddListener(()=>{if(action!=null)action();});var navigation=button.navigation;navigation.mode=Navigation.Mode.Automatic;button.navigation=navigation;
            Label(image.transform,text,14,0,w-28,h,18,primary?Ink:White,false,TextAnchor.MiddleCenter);
            return button;
        }
        InputField Input(string label,string value,string placeholder,float x,float y,float w,Action<string> changed)
        {
            Label(content.transform,label,x,y,w,25,14,Muted);var image=Box(content.transform,x,y+34,w,58,new Color(.09f,.145f,.155f));image.raycastTarget=true;
            var field=image.gameObject.AddComponent<InputField>();field.targetGraphic=image;field.characterLimit=64;
            field.textComponent=Label(image.transform,"",18,0,w-36,58,19,White,false,TextAnchor.MiddleLeft);
            field.placeholder=Label(image.transform,placeholder,18,0,w-36,58,19,Muted,false,TextAnchor.MiddleLeft);
            field.text=value;field.onValueChanged.AddListener(v=>changed(v));return field;
        }
        void Header(string section,bool back=true)
        {
            Label(content.transform,"LESTINGCRAFT",64,42,400,32,19,White,true);
            Label(content.transform,"N E W  E R A",66,81,280,24,12,Accent);
            Label(content.transform,section.ToUpperInvariant(),1060,48,470,40,14,Muted,false,TextAnchor.MiddleRight);
            Box(content.transform,64,119,1472,1,new Color(.8f,.9f,.8f,.13f),false);
            if(back)Button("←  Назад",64,798,150,48,()=>{if(game.InWorld)ShowPause();else ShowHome();});
            Label(content.transform,"Тестовая Windows-версия  /  интерфейс 04",945,830,590,24,12,Muted,false,TextAnchor.MiddleRight);
        }
        public void ShowHome()
        {
            Clear(Page.Home);
            Label(content.transform,"L / C",86,46,220,40,27,Accent,true);
            Label(content.transform,"A NEW ERA OF ADVENTURE",87,99,580,22,12,Muted);
            var version=Box(content.transform,1247,48,267,40,new Color(.035f,.055f,.065f,.72f));
            Label(version.transform,"WINDOWS · ТЕСТОВАЯ ВЕРСИЯ",10,0,247,40,11,White,false,TextAnchor.MiddleCenter);
            Label(content.transform,"LESTINGCRAFT",80,223,1060,90,58,White,true);
            Label(content.transform,"N E W   E R A",87,316,730,48,25,Accent,true);
            Box(content.transform,88,395,64,2,Accent,false);
            Label(content.transform,"Твоя история начинается здесь.",87,424,760,45,23,White);
            Label(content.transform,"Исследуй Лестингию. Построй своё убежище.\nОтправляйся навстречу неизвестному — вместе.",88,480,720,69,17,Muted);
            float y=594;
            if(game.HasSave){HomeAction("Продолжить экспедицию","Вернуться в локальное сохранение",y,game.ContinueOffline,true,"Home.Continue");y+=82;}
            HomeAction(game.HasSave?"Новая экспедиция":"Начать экспедицию","Выбрать режим и открыть новый мир",y,ShowWorld,!game.HasSave,"Home.Play");
            HomeAction("Играть с друзьями","Создать комнату или войти по коду",y+82,ShowRooms,false,"Home.Coop");
            var settings=Button("Настройки",88,game.HasSave?843:807,210,43,()=>ShowSettings(Page.Home));settings.name="Home.Settings";
            var quit=Button("Выйти из игры",312,game.HasSave?843:807,229,43,()=>Application.Quit());quit.name="Home.Quit";
            var card=Box(content.transform,1110,645,405,131,new Color(.035f,.055f,.065f,.83f));
            Box(card.transform,0,22,2,87,Accent,false);
            Label(card.transform,"01  /  ЛЕСТИНГИЯ",25,21,355,23,12,Accent);
            Label(card.transform,"Первая экспедиция",25,56,355,31,22,White,true);
            Label(card.transform,"Леса, озёра и неизведанные горизонты",25,98,357,20,12,Muted);
            Label(content.transform,"Фон — концепт-арт. Кампания ещё разрабатывается.",1100,839,415,34,11,Muted,false,TextAnchor.MiddleRight);
        }
        void HomeAction(string title,string subtitle,float y,Action action,bool primary,string objectName)
        {
            var button=Button("",88,y,507,70,action,primary);button.name=objectName;
            Label(button.transform,title,23,12,426,28,19,primary?Ink:White,true);
            Label(button.transform,subtitle,24,43,425,20,12,primary?new Color(.20f,.23f,.20f):Muted);
            Label(button.transform,"→",456,0,33,70,23,primary?Ink:Accent,false,TextAnchor.MiddleCenter);
        }
        public void ShowWorld()
        {
            Clear(Page.World);Header("Режим новой экспедиции");
            Label(content.transform,"КАК НАЧНЁТСЯ ТВОЯ ЭРА?",64,171,1400,67,36,White,true);
            Label(content.transform,"Все начинают в Лестингии. Другие измерения открываются по ходу кампании.",66,247,1440,42,19,Muted);
            for(int i=0;i<4;i++)
            {
                int mode=i;bool chosen=game.SelectedMode==i;var button=Button("",64+i*368,331,352,254,()=>{game.SelectedMode=mode;ShowWorld();},chosen);
                Label(button.transform,"0"+(i+1),22,20,305,39,25,chosen?Ink:Accent,true);
                Label(button.transform,GameRules.ModeNames[i],22,85,305,55,24,chosen?Ink:White,true);
                Label(button.transform,GameRules.ModeDescriptions[i],22,164,305,79,17,chosen?Ink:Muted);
            }
            Label(content.transform,"СТАРТ: ЛЕСТИНГИЯ",64,628,470,37,19,Accent,true);
            Label(content.transform,"Мир генерируется автоматически.\nВыбирается только режим экспедиции.",64,678,465,80,18,Muted);
            Label(content.transform,"Локальная экспедиция",542,626,640,34,20,White);
            Label(content.transform,"Персонаж и изменённые блоки сохраняются локально.\nНовый мир заменяет предыдущее локальное сохранение.",542,675,650,72,16,Muted);
            Button("Начать  →",1227,668,309,64,game.PlayOffline,true);
            status=Label(content.transform,"Полная кампания и боссы ещё разрабатываются.",542,784,940,66,14,Muted);
        }
        public void ShowRooms()
        {
            Clear(Page.Rooms);Header("Кооператив · до 8 игроков");
            Label(content.transform,"ОДИН МИР. ВМЕСТЕ.",64,171,1400,67,36,White,true);
            Label(content.transform,"Создай комнату и передай её код друзьям — или присоединись к их миру.",66,247,1380,42,19,Muted);
            Input("ТВОЁ ИМЯ",game.PlayerName,"Исследователь",64,321,440,v=>game.PlayerName=v);
            Button("Режим: "+GameRules.ModeNames[game.SelectedMode],542,355,430,58,()=>{game.SelectedMode=(game.SelectedMode+1)%4;ShowRooms();});
            var left=Box(content.transform,64,454,710,305,Panel);var right=Box(content.transform,795,454,741,305,Panel);
            Label(left.transform,"НОВАЯ КОМНАТА",28,28,655,41,25,White,true);
            Label(left.transform,GameRules.ModeNames[game.SelectedMode]+" · Лестингия\nОбщий мир и сохранение персонажей.",28,96,650,76,17,Muted);
            actions.Add(Button("Создать и пригласить",28,204,652,64,game.CreateRoom,true,left.transform));
            Label(right.transform,"ПРИСОЕДИНИТЬСЯ",28,28,680,41,25,White,true);
            // Reusable input anchored in the card rather than relying on browser forms.
            var input=Input("КОД КОМНАТЫ",game.RoomCode,"32 символа от хозяина комнаты",823,540,350,v=>game.RoomCode=v);
            actions.Add(Button("Войти  →",404,111,307,64,game.JoinRoom,true,right.transform));
            Label(right.transform,"Сервер: "+game.ServerLabel,28,211,685,64,14,Muted);
            status=Label(content.transform,"",250,798,930,73,16,Accent);
        }
        public void ShowPause()
        {
            Clear(Page.Pause);Header("Пауза");
            Label(content.transform,"МИР ПОДОЖДЁТ",64,248,1000,86,44,White,true);
            Label(content.transform,game.WorldLabel+"\n"+(game.Online?"Комната "+game.RoomCode+" · сетевой мир не останавливается":"Локальная экспедиция"),67,373,1050,90,22,Muted);
            Button("Продолжить  →",64,534,395,64,game.Resume,true);
            Button("Настройки",64,619,395,58,()=>ShowSettings(Page.Pause));
            Button("В главное меню",64,698,395,58,game.ReturnToMenu);
            Label(content.transform,"ESC — продолжить · E — инвентарь · Enter / T — чат\nЛКМ — добывать · ПКМ — использовать · F1 — интерфейс · F2 — снимок",505,535,950,68,15,Muted);
            if(game.Online)Button("Скопировать код комнаты",505,619,380,58,()=>GUIUtility.systemCopyBuffer=game.RoomCode);
        }
        public void ShowInventory(int station=0,Vector3Int cell=default)
        {
            stationKind=station;stationCell=cell;craftWidth=station==16?3:2;
            Clear(Page.Inventory);Header(station==16?"Верстак · 3×3":station==17?"Печь · готовка":"Инвентарь · 2×2",false);
            inventoryViews.Clear();craftRevision=-1;
            Label(content.transform,"",64,165,1400,59,32,White,true);
            Label(content.transform,"ЛКМ — взять / положить · ПКМ — половина / по одному · Shift-клик — быстро перенести · Можно перетаскивать мышью",66,248,1465,50,15,Muted);
            if(!game.Session)return;
            for(int i=0;i<45;i++)CreateCell(i,64+(i%9)*82,328+(i/9)*83,77,74);
            Label(content.transform,"Shift-клик по одежде/броне — надеть; по экипировке — снять",64,759,740,25,14,Muted);
            Label(content.transform,station==17?"ПЕРСОНАЛЬНАЯ ОЧЕРЕДЬ ГОТОВКИ":"СЕТКА "+craftWidth+"×"+craftWidth,852,307,380,32,16,Accent,true);
            if(station==17)
            {
                CreateCell(54,867,377,82,82);CreateCell(55,867,527,82,82);CreateCell(56,1103,431,86,86);
                Label(content.transform,"Сырьё",867,349,145,26,14,Muted);Label(content.transform,"Топливо",867,498,145,26,14,Muted);
                Label(content.transform,"→",1016,446,65,50,29,Accent);Label(content.transform,"Результат",1084,534,145,26,14,Muted);
                stoveStatus=Label(content.transform,"",852,642,370,103,15,Muted);
                Label(content.transform,"Ягоды → печёные ягоды\nТесто → хлеб · Древесина → уголь\nОчередь сохраняется у игрока; работает рядом с печью.",852,702,370,90,13,Muted);
            }
            else
            {
                for(int y=0;y<craftWidth;y++)for(int x=0;x<craftWidth;x++)CreateCell(45+y*3+x,852+x*77,365+y*77,70,70);
                Label(content.transform,"→",1088,414,55,48,28,Accent);
                var result=Button("",1141,407,83,83,()=>{});result.name="Craft.Result";
                var click=result.gameObject.AddComponent<InventorySlot>();click.Owner=this;click.Index=57;
                craftOutput=Rect("Recipe output icon",result.transform,9,3,65,65).gameObject.AddComponent<RawImage>();craftOutput.raycastTarget=false;
                craftCount=Label(result.transform,"",8,60,65,20,13,White,false,TextAnchor.MiddleRight);AttachHover(result.gameObject,0);
                Button("Вернуть материалы",852,625,372,45,()=>{if(game.Session.CanInteract){game.Session.Notice=CraftingTable.ReturnGrid(game.Session.Inventory)?"Материалы возвращены":"Недостаточно места — сетка сохранена";RefreshCraftInventory();}});
                Label(content.transform,"Книга справа выкладывает рецепт из ваших ресурсов. Результат забирается вручную.\nShift-клик по результату — изготовить максимум.\nМатериалы в сетке и на курсоре сохраняются.",852,690,370,112,14,Muted);
            }
            for(int i=0;i<Equipment.Count;i++)
            {float x=64+i*145;Label(content.transform,Equipment.Names[i],x,165,176,23,12,Muted);CreateCell(58+i,x,191,62,55);}
            var portrait=Rect("3D equipped character",content.transform,1260,145,100,150).gameObject.AddComponent<RawImage>();portrait.raycastTarget=false;portrait.gameObject.AddComponent<CharacterPreview>().Initialize(game);
            Label(content.transform,"Броня снижает\nурон от ударов;\nодежда греет.\nНезависимые слоты.",1375,172,160,98,12,Muted);
            BuildRecipeBook();
            status=Label(content.transform,game.Session.Notice,340,812,1180,56,15,Accent);
            Button("←  В мир",64,812,230,51,game.ToggleInventory,true);
            cursorRoot=Rect("Carried inventory stack",content.transform,0,0,64,77);
            cursorImage=cursorRoot.gameObject.AddComponent<RawImage>();cursorImage.raycastTarget=false;
            cursorCount=Label(cursorRoot,"",0,57,62,20,13,White,false,TextAnchor.MiddleRight);
            RefreshCraftInventory();
        }
        void CreateCell(int index,float x,float y,float w,float h)
        {
            var button=Button("",x,y,w,h,()=>{});button.name="Inventory.Slot."+index;
            var click=button.gameObject.AddComponent<InventorySlot>();click.Owner=this;click.Index=index;
            AttachHover(button.gameObject,0);var icon=Rect("Stack icon",button.transform,10,2,w-20,h-15).gameObject.AddComponent<RawImage>();icon.raycastTarget=false;
            var count=Label(button.transform,"",5,h-21,w-10,18,12,White,false,TextAnchor.MiddleRight);
            inventoryViews.Add(new CellView{Index=index,Icon=icon,Count=count,Hover=button.GetComponent<ItemHover>()});
            if(index<9)Label(button.transform,(index+1).ToString(),5,3,17,17,10,Accent);
        }
        void BuildRecipeBook()
        {
            Label(content.transform,"КНИГА РЕЦЕПТОВ",1250,307,285,32,16,Accent,true);
            var viewport=Rect("Recipe viewport",content.transform,1250,350,285,407);viewport.gameObject.AddComponent<RectMask2D>();
            var backing=viewport.gameObject.AddComponent<Image>();backing.color=new Color(.06f,.10f,.11f,.7f);
            var list=Rect("Recipes",viewport,0,0,285,CraftingTable.All.Length*55);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=list;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            for(int i=0;i<CraftingTable.All.Length;i++)
            {
                int index=i;var r=CraftingTable.All[i];
                var button=Button("",0,i*55,280,50,()=>{if(stationKind==17){game.Session.Notice="Крафт — в инвентаре или на верстаке";}else game.Session.FillRecipe(index,craftWidth);RefreshCraftInventory();},false,list);
                Icon(button.transform,r.Output,3,1,46);Label(button.transform,Items.Get(r.Output).Name+(r.Fits(craftWidth)?"":" · 3×3"),55,8,216,39,13,r.Fits(craftWidth)?White:Muted);
            }
            Label(content.transform,"Колёсико мыши — прокрутка",1250,769,290,25,12,Muted);
        }
        public bool BeginInventoryDrag(int index)
        {
            if(!game.Session||!game.Session.CanInteract)return false;
            if(game.Session.Inventory.cursor.count==0)InventoryClick(index,false,false);
            return game.Session.Inventory.cursor.count>0;
        }
        public void InventoryClick(int index,bool right,bool shift)
        {
            if(!game.Session||!game.Session.CanInteract)return;
            if(stationKind!=0&&!game.Session.StationNearby(stationKind,stationCell)){game.Session.Notice="Станция недоступна — закройте окно";RefreshCraftInventory();return;}
            if(index==57)game.Session.TakeCraft(craftWidth,shift);else InventoryInteraction.Click(game.Session.Inventory,index,right,shift);
            RefreshCraftInventory();
        }
        void RefreshCraftInventory()
        {
            if(!game.Session)return;var inv=game.Session.Inventory;craftRevision=inv.revision;
            foreach(var view in inventoryViews)
            {
                var stack=InventoryInteraction.Get(inv,view.Index);view.Icon.texture=ItemArtwork.Get(stack.item);view.Icon.enabled=stack.count>0&&view.Icon.texture;
                view.Count.text=stack.count>0?stack.count.ToString():"";view.Hover.Item=stack.count>0?stack.item:0;
            }
            if(stationKind!=17&&craftOutput)
            {
                var recipe=CraftingTable.Match(inv.grid,craftWidth);craftOutput.texture=recipe==null?null:ItemArtwork.Get(recipe.Output);craftOutput.enabled=craftOutput.texture;
                craftCount.text=recipe==null?"":recipe.Count.ToString();craftOutput.transform.parent.GetComponent<ItemHover>().Item=recipe==null?0:recipe.Output;
            }
            if(cursorImage){cursorImage.texture=ItemArtwork.Get(inv.cursor.item);cursorImage.enabled=inv.cursor.count>0&&cursorImage.texture;cursorCount.text=inv.cursor.count>0?inv.cursor.count.ToString():"";}
            if(status)status.text=game.Session.Notice;
        }
        void AttachHover(GameObject target,int item)
        {var hover=target.AddComponent<ItemHover>();hover.Owner=this;hover.Item=item;}
        void Icon(Transform parent,int item,float x,float y,float size)
        {
            if(item<=0)return;
            var raw=Rect("Item icon",parent,x,y,size,size).gameObject.AddComponent<RawImage>();raw.texture=ItemArtwork.Get(item);raw.raycastTarget=false;
        }
        public void ShowItemTooltip(int item,Vector2 screenPosition)
        {
            if(item<=0||!frame)return;
            if(!tooltip)
            {
                var panel=Box(frame,0,0,360,58,new Color(.025f,.045f,.05f,.98f));tooltip=panel.rectTransform;
                tooltip.name="Item name tooltip";tooltipText=Label(tooltip,"",17,0,326,58,18,White,false,TextAnchor.MiddleLeft);
            }
            tooltipText.text=Items.Get(item).Name;tooltip.gameObject.SetActive(true);tooltip.SetAsLastSibling();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,screenPosition,null,out var local);
            tooltip.anchoredPosition=new Vector2(Mathf.Clamp(local.x+800+18,10,1230),-Mathf.Clamp(450-local.y+20,10,832));
        }
        public void HideItemTooltip(){if(tooltip)tooltip.gameObject.SetActive(false);}
        public void ShowDeathScreen()
        {
            Clear(Page.Death);Header("Экспедиция",false);bool ended=game.Session&&game.Session.Data.campaign.runEnded;
            Label(content.transform,ended?"ЗАБЕГ ЗАВЕРШЁН":"ТЫ ПОГИБ",64,259,1450,87,45,White,true);
            Label(content.transform,ended?"Хардкор: смерть одного участника завершает забег всех.\nЭтот мир нельзя продолжить повторным входом.":"Вещи остались у личного надгробия.\nВернитесь к нему, чтобы забрать снаряжение.",67,391,1380,115,24,Muted);
            if(!ended)Button("Возродиться  →",64,588,395,64,()=>game.Session.Respawn(),true);
            Button("Главное меню",64,685,395,58,game.ReturnToMenu);
        }
        void ShowSettings(Page back)
        {
            settingsBack=back;Clear(Page.Settings);Header("Графика и управление",false);
            Label(content.transform,"НАСТРОЙ ПОД СЕБЯ",64,171,1400,68,36,White,true);
            Label(content.transform,"Средние — качественный PBR без RTX. Ultra — усиленные эффекты, без автоматического понижения.",66,247,1400,42,19,Muted);
            var settings=VisualSettings.Current;
            Box(content.transform,64,323,713,402,Panel);Box(content.transform,800,323,736,402,Panel);
            Slider("Дальность мира",64+28,354,655,settings.ViewDistance,2,5,true,v=>{settings.ViewDistance=Mathf.RoundToInt(v);settings.Save();},v=>(int)v+" чанка/сторону");
            Slider("Масштаб рендера",92,469,655,settings.RenderScale,.85f,1.5f,false,v=>{settings.RenderScale=v;settings.Save();},v=>Mathf.RoundToInt(v*100)+"%");
            Button("Тени: "+settings.ShadowResolution.ToString(),92,589,309,58,()=>{settings.ShadowResolution=settings.ShadowResolution==1024?2048:settings.ShadowResolution==2048?4096:1024;settings.Save();ShowSettings(settingsBack);});
            Button("Постэффекты: "+(settings.PostEffects?"вкл.":"выкл."),419,589,328,58,()=>{settings.PostEffects=!settings.PostEffects;settings.Save();ShowSettings(settingsBack);});
            Button("Объёмы: "+(settings.VolumetricQuality==2?"Ultra":settings.VolumetricQuality==1?"высокие":"сокращённые"),92,667,655,38,()=>{settings.VolumetricQuality=(settings.VolumetricQuality+1)%3;settings.Save();ShowSettings(settingsBack);});
            Slider("Поле зрения",828,354,680,settings.FieldOfView,60,100,true,v=>{settings.FieldOfView=v;settings.Save();},v=>(int)v+"°");
            Slider("Чувствительность мыши",828,469,680,settings.Sensitivity,.04f,.25f,false,v=>{settings.Sensitivity=v;settings.Save();},v=>v.ToString("0.00"));
            Label(content.transform,"WASD — движение     SPACE — прыжок\nЛКМ — убрать блок     ПКМ — поставить\n1–9 — быстрые слоты     E — инвентарь         ESC — меню",828,596,679,106,18,White);
            Button("←  Готово",64,798,210,54,()=>{settings.Save();if(settingsBack==Page.Pause)ShowPause();else ShowHome();},true);
            Button("Средние · PBR",296,798,240,54,()=>{settings.HighDefault();ShowSettings(settingsBack);});
            Button("Ultra · без RT",551,798,230,54,()=>{settings.Ultra();ShowSettings(settingsBack);});
            Label(content.transform,"Аппаратные лучи: URP не поддерживает. Текущий API/GPU RT: "+(VisualSettings.HardwareRayTracing?"да":"нет"),810,792,726,69,15,Muted,false,TextAnchor.MiddleRight);
        }
        void Slider(string title,float x,float y,float w,float value,float min,float max,bool integer,Action<float> changed,Func<float,string> format)
        {
            Label(content.transform,title,x,y,w-160,30,18,White);var output=Label(content.transform,format(value),x+w-190,y,190,30,15,Accent,false,TextAnchor.MiddleRight);
            var track=Box(content.transform,x,y+58,w,6,new Color(.17f,.23f,.24f));
            var slider=Rect("Slider",content.transform,x,y+42,w,40).gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;slider.wholeNumbers=integer;
            var fill=Box(slider.transform,0,16,w,6,Accent);fill.rectTransform.anchorMin=new Vector2(0,.5f);fill.rectTransform.anchorMax=new Vector2(1,.5f);fill.rectTransform.offsetMin=new Vector2(0,-3);fill.rectTransform.offsetMax=new Vector2(0,3);
            var handleArea=Rect("Handle Area",slider.transform,0,0,w,40);
            var handle=Box(handleArea,0,9,22,22,White);handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(0,.5f);handle.rectTransform.pivot=new Vector2(.5f,.5f);handle.rectTransform.anchoredPosition=Vector2.zero;handle.raycastTarget=true;
            slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.value=value;
            slider.onValueChanged.AddListener(v=>{output.text=format(v);changed(v);});
        }
        public void ShowHUD()
        {
            Clear(Page.HUD);
            loadingHUD=Label(content.transform,"",36,31,780,37,14,Accent);
            // Minimal reticle, not an IMGUI text '+'.
            foreach(var r in new[]{new Vector4(799,439,2,7),new Vector4(799,454,2,7),new Vector4(789,449,7,2),new Vector4(804,449,7,2)})Box(content.transform,r.x,r.y,r.z,r.w,new Color(1,1,1,.82f),false);
            slots=new Image[9];slotRects=new RectTransform[9];slotIcons=new RawImage[9];slotCounts=new Text[9];slotHovers=new ItemHover[9];lastInventoryRevision=-1;lastSlot=-1;
            var backdrop=Box(content.transform,364,786,872,99,new Color(.025f,.05f,.055f,.88f));
            for(int i=0;i<9;i++)
            {
                var image=Box(backdrop.transform,10+i*95,10,91,79,new Color(.10f,.155f,.165f,.92f));slots[i]=image;slotRects[i]=image.rectTransform;
                image.raycastTarget=true;AttachHover(image.gameObject,0);slotHovers[i]=image.GetComponent<ItemHover>();
                var raw=Rect("Item icon",image.transform,18,2,55,55).gameObject.AddComponent<RawImage>();raw.raycastTarget=false;slotIcons[i]=raw;
                slotCounts[i]=Label(image.transform,"",51,57,32,18,11,White,false,TextAnchor.MiddleRight);
                Label(image.transform,(i+1).ToString(),0,55,91,18,11,Muted,false,TextAnchor.MiddleCenter);
            }
            // Item names are hover/focus tooltips, never permanent slot text.

            var healthPanel=Box(content.transform,26,546,310,226,new Color(.025f,.05f,.055f,.91f));
            healthHUD=Label(healthPanel.transform,"СОСТОЯНИЕ · 100 / 100",16,12,280,30,13,White);
            var body=Rect("Health silhouette",healthPanel.transform,17,52,91,157);healthBody=body.gameObject.AddComponent<HealthSilhouette>();healthBody.Build();
            vitalsHUD=Label(healthPanel.transform,"",124,61,175,146,13,White);
            miningBar=Box(content.transform,725,484,0,4,Accent,false);miningBar.gameObject.name="Mining progress";
            miningLabel=Label(content.transform,"",700,494,200,29,12,White,false,TextAnchor.MiddleCenter);
            chatRoot=Box(content.transform,26,265,590,260,new Color(.025f,.045f,.04f,.78f)).gameObject;
            chatText=Label(chatRoot.transform,"",18,13,554,168,15,White);chatText.verticalOverflow=VerticalWrapMode.Truncate;
            var chatBox=Box(chatRoot.transform,14,193,562,49,Panel);chatBox.raycastTarget=true;chatInput=chatBox.gameObject.AddComponent<InputField>();
            chatInput.targetGraphic=chatBox;chatInput.characterLimit=240;chatInput.lineType=InputField.LineType.SingleLine;
            chatInput.textComponent=Label(chatBox.transform,"",12,0,538,49,15,White,false,TextAnchor.MiddleLeft);
            chatInput.placeholder=Label(chatBox.transform,"Сообщение…",12,0,538,49,15,Muted,false,TextAnchor.MiddleLeft);
            chatInput.gameObject.SetActive(false);chatRoot.SetActive(false);chatRevision=-1;
            noticeHUD=Label(content.transform,"",400,656,800,41,14,Accent,false,TextAnchor.MiddleCenter);

        }
        void Update()
        {
            if(!ready)return;
            if(game.InWorld&&game.Paused&&page==Page.HUD)ShowPause();
            else if(game.InWorld&&!game.Paused&&page!=Page.HUD)ShowHUD();
            else if(!game.InWorld&&(page==Page.HUD||page==Page.Pause))ShowHome();
            if(page==Page.World&&status&&!string.IsNullOrEmpty(game.LastError))status.text=game.LastError;
            if(page==Page.Rooms)
            {
                if(status)status.text=game.IsConnecting?"Подключаемся к комнате…":game.LastError;
                foreach(var action in actions)if(action)action.interactable=!game.IsConnecting;
            }
            if(page==Page.Inventory&&game.Session)
            {
                if(craftRevision!=game.Session.Inventory.revision)RefreshCraftInventory();
                if(cursorRoot&&UnityEngine.InputSystem.Mouse.current!=null)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,UnityEngine.InputSystem.Mouse.current.position.ReadValue(),null,out var local);
                    cursorRoot.anchoredPosition=new Vector2(local.x+812,local.y-438);cursorRoot.SetAsLastSibling();
                }
                if(stationKind==17&&stoveStatus){var f=game.Session.Inventory.furnace;stoveStatus.text=$"Готовка: {Mathf.Clamp01(f.progress/FurnaceState.CookSeconds)*100:0}%\nТопливо: {f.burn:0.0} с\nПауза вдали от печи / при полном выходе";}
            }
            if(page!=Page.HUD)return;
            if(UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)content.gameObject.SetActive(!content.gameObject.activeSelf);
            averageFrame=Mathf.Lerp(averageFrame,Mathf.Min(Time.unscaledDeltaTime,.5f),.04f);timer+=Time.unscaledDeltaTime;
            if(timer>.25f)
            {
                timer=0;
                if(game.Session)
                {
                    var session=game.Session;var v=session.Vitals;
                    healthBody.SetHealth(v.health);healthHUD.text=session.Creative?"ТВОРЧЕСКИЙ РЕЖИМ":$"СОСТОЯНИЕ · {v.health:0} / 100";
                    vitalsHUD.text=$"Голод  {v.hunger:0}\n\nТело  {v.temperature:0.0}°C\nПромокание  {v.wetness*100:0}%\n\nСилы  {v.stamina:0}"+(v.air<100?$"\nВоздух  {v.air:0}%":"");

                    if(lastNotice!=session.Notice){lastNotice=session.Notice;noticeAt=Time.unscaledTime;}
                    noticeHUD.text=Time.unscaledTime-noticeAt<4?lastNotice:"";
                }
                loadingHUD.text=!game.WorldReady?"Формирование ландшафта… "+game.ChunkCount+" чанков":"";
            }
            if(game.Session)
            {
                float progress=game.Session.MiningProgress;miningBar.rectTransform.sizeDelta=new Vector2(150*progress,4);miningLabel.text=progress>.01f?$"{progress*100:0}%":"";
            }
            if(chatRoot)
            {
                bool visible=game.ChatOpen||(game.ChatRevision>0&&Time.unscaledTime-game.ChatLastTime<9&&!string.IsNullOrEmpty(game.ChatLog));
                chatRoot.SetActive(visible);
                if(chatRevision!=game.ChatRevision){chatRevision=game.ChatRevision;chatText.text=game.ChatLog;}
            }
            if(game.Session&&(lastInventoryRevision!=game.Session.Inventory.revision||lastSlot!=game.Session.Inventory.selected))
            {
                var inventory=game.Session.Inventory;lastInventoryRevision=inventory.revision;lastSlot=inventory.selected;
                for(int i=0;i<9;i++)
                {
                    var stack=inventory.slots[i];slots[i].color=i==inventory.selected?new Color(.36f,.49f,.30f,.97f):new Color(.10f,.155f,.165f,.92f);
                    var texture=ItemArtwork.Get(stack.item);slotIcons[i].texture=texture;slotIcons[i].enabled=texture;
                    slotHovers[i].Item=stack.count>0?stack.item:0;
                    slotCounts[i].text=stack.count>0?(game.Session.Creative?"∞":stack.count.ToString()):"";
                }

            }
        }
        void OnDestroy(){if(rounded)Destroy(rounded);}
    }
}
