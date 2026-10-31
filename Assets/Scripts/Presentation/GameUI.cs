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
    public sealed partial class GameUI : MonoBehaviour
    {
        enum Page{Account,Achievements,Home,World,Rooms,AddServer,Settings,Pause,Inventory,Creative,Trade,Death,HUD,Lobby,Journal,Store,Market,Friends}
        static readonly Color Ink=new Color(.055f,.086f,.10f), Panel=new Color(.07f,.115f,.13f,.96f), Muted=new Color(.58f,.66f,.66f), White=new Color(.91f,.94f,.90f), Accent=new Color(.88f,.77f,.53f);
        PrototypeGame game;Canvas canvas;RectTransform frame;GameObject content,background;Font body,display;Sprite rounded;
        readonly List<GameObject> transient=new List<GameObject>();readonly List<Button> actions=new List<Button>();
        Page page,settingsBack;Text status,loadingHUD;Image[] slots;RectTransform[] slotRects;
        Text vitalsHUD,objectiveHUD,noticeHUD,healthHUD;HealthSilhouette healthBody;RawImage[] slotIcons;Text[] slotCounts;ItemHover[] slotHovers;RectTransform tooltip;Text tooltipText;int lastInventoryRevision=-1,lastSlot=-1;
        Image oxygenBar;Image[] slotFrames;Image[] objectiveChecks;Text[] objectiveLabels;
        Image miningBar;Text miningLabel;GameObject chatRoot;Text chatText;InputField chatInput;int chatRevision=-1;string lastNotice="";float noticeAt;
        public string ChatText=>chatInput?chatInput.text:"";
        public void SetChatOpen(bool open)
        {
            if(!chatInput)return;chatInput.gameObject.SetActive(open);if(open){chatRoot.SetActive(true);chatInput.text="";StartCoroutine(FocusChat());}
            else {chatInput.DeactivateInputField();UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);}
        }
        System.Collections.IEnumerator FocusChat(){yield return null;if(chatInput&&game.ChatOpen){chatInput.text="";chatInput.ActivateInputField();}}
        sealed class CellView {public int Index;public RawImage Icon;public Text Count;public ItemHover Hover;public Image Wear;public float WearWidth;}
        readonly List<CellView> inventoryViews=new List<CellView>();int stationKind,craftWidth=2;Vector3Int stationCell;
        RawImage craftOutput,cursorImage;Text craftCount,cursorCount,stoveStatus,equipmentStatus;RectTransform cursorRoot;int craftRevision=-1;
        public bool InventoryOpen=>page==Page.Inventory||page==Page.Creative||page==Page.Trade;
        public bool TradeOpen=>page==Page.Trade;
        public bool EditingText=>EventSystem.current&&EventSystem.current.currentSelectedGameObject&&EventSystem.current.currentSelectedGameObject.GetComponent<InputField>()&&EventSystem.current.currentSelectedGameObject.GetComponent<InputField>().isFocused;
        public bool IsReady=>ready;
        public float InventoryTextPixelSize=>18*canvas.scaleFactor;
        void LayoutWidth(float width)
        {
            GetComponent<CanvasScaler>().referenceResolution=new Vector2(width,900);
            frame.sizeDelta=new Vector2(width,900);content.GetComponent<RectTransform>().sizeDelta=new Vector2(width,900);
        }
        public bool InventoryRectsDoNotOverlap(out string reason)
        {
            reason="";if(page!=Page.Inventory){reason="Not bag inventory";return false;}
            var all=new List<UnityEngine.Rect>();var corners=new Vector3[4];
            foreach(var cell in inventoryViews)
            {
                var rt=cell.Icon.transform.parent.GetComponent<RectTransform>();rt.GetWorldCorners(corners);
                Vector2 a=frame.InverseTransformPoint(corners[0]),b=frame.InverseTransformPoint(corners[2]);var box=UnityEngine.Rect.MinMaxRect(a.x,a.y,b.x,b.y);
                if(box.xMin<frame.rect.xMin||box.xMax>frame.rect.xMax||box.yMin<frame.rect.yMin||box.yMax>frame.rect.yMax){reason="Cell outside frame: "+cell.Index;return false;}
                foreach(var prev in all)if(box.Overlaps(prev)){reason="Overlapping cell: "+cell.Index;return false;}all.Add(box);
            }
            var preview=content.transform.Find("3D equipped character").GetComponent<RectTransform>();preview.GetWorldCorners(corners);
            var pa=frame.InverseTransformPoint(corners[0]);var pb=frame.InverseTransformPoint(corners[2]);var portrait=UnityEngine.Rect.MinMaxRect(pa.x,pa.y,pb.x,pb.y);
            foreach(var box in all)if(portrait.Overlaps(box)){reason="Portrait overlaps equipment";return false;}
            return true;
        }
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
            HideItemTooltip();LayoutWidth(1600);page=next;content.SetActive(true);
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
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.16f,1.16f,1.16f);colors.pressedColor=new Color(.86f,.89f,.87f);colors.disabledColor=new Color(.5f,.5f,.5f,.6f);colors.fadeDuration=.14f;button.colors=colors;
            var stroke=Rect("Focus border",image.transform,0,0,0,0);Stretch(stroke);var border=stroke.gameObject.AddComponent<Image>();border.sprite=MenuArtwork.Outline;border.type=Image.Type.Sliced;border.raycastTarget=false;border.color=new Color(Accent.r,Accent.g,Accent.b,.34f);
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
            if(InventoryOpen)
            {
                Label(content.transform,"LESTINGCRAFT",32,24,560,34,22,White,true);
                Label(content.transform,"NEW ERA",34,64,280,25,15,Accent);
                Label(content.transform,section.ToUpperInvariant(),784,30,464,42,20,Accent,false,TextAnchor.MiddleRight);
                Box(content.transform,32,108,1216,1,new Color(.8f,.9f,.8f,.16f),false);
                Box(content.transform,32,108,86,2,Accent,false);
                return;
            }
            Label(content.transform,"LESTINGCRAFT",64,42,400,32,19,White,true);
            Label(content.transform,"N E W  E R A",66,81,280,24,12,Accent);
            Label(content.transform,section.ToUpperInvariant(),1060,48,470,40,14,Accent,false,TextAnchor.MiddleRight);
            Box(content.transform,64,119,1472,1,new Color(.8f,.9f,.8f,.13f),false);
            Box(content.transform,64,119,96,2,Accent,false);
            if(back)Button("←  Назад",64,798,150,48,()=>{if(game.InWorld)ShowPause();else ShowHome();});
            Label(content.transform,"0.3.3  /  графика 07 · золотой час",945,830,590,24,12,Muted,false,TextAnchor.MiddleRight);
        }
        public void ShowHome()
        {
            Clear(Page.Home);
            // Keep the landscape visible while reserving a calm, high-contrast reading surface.
            // All action names below are stable: the executable smoke test clicks them by name.
            Box(content.transform,56,44,1480,1,new Color(.82f,.74f,.54f,.36f),false);
            Label(content.transform,"L / C",76,66,120,36,27,Accent,true);
            Label(content.transform,"НОВАЯ ЭРА · ПЕРВАЯ ЭКСПЕДИЦИЯ",207,71,750,28,13,White);
            var version=Box(content.transform,1190,61,346,39,new Color(.035f,.055f,.065f,.86f));
            Label(version.transform,"WINDOWS  /  РАННЯЯ ВЕРСИЯ",15,0,316,39,12,White,false,TextAnchor.MiddleCenter);
            var account=Button("Аккаунт · достижения",1190,114,346,52,ShowAccount);account.name="Home.Account";
            if(game.Accounts!=null&&game.Accounts.Logged){var walletEntry=Button("Кошелёк и друзья",1190,174,346,48,ShowStore);walletEntry.name="Home.Store";}
            var panel=Box(content.transform,56,189,710,610,new Color(.023f,.047f,.055f,.87f));
            panel.raycastTarget=false;
            Box(content.transform,56,189,4,610,Accent,false);
            Label(content.transform,"МИР, КОТОРЫЙ ТЫ СТРОИШЬ САМ",86,214,640,27,13,Accent,true);
            Label(content.transform,"LESTINGCRAFT",82,254,660,73,54,White,true);
            Label(content.transform,"N E W   E R A",89,330,588,41,25,Accent,true);
            Box(content.transform,88,393,78,2,Accent,false);
            Label(content.transform,"Исследуй. Строй. Выживай. Вместе.",88,420,632,39,22,White,true);
            Label(content.transform,"Лестингия ждёт: леса, пещеры, деревни и ваша история.\nВыбери свою экспедицию или позови друга.",88,472,624,54,17,White);
            if(game.HasSave)
            {
                HomeAction("Продолжить экспедицию","Вернуться к своему сохранённому миру",546,game.ContinueOffline,true,"Home.Continue");
                HomeAction("Новая экспедиция","Внимание: заменит прежнее локальное сохранение",624,ShowWorld,false,"Home.Play");
                HomeAction("Мультиплеер","Добавить сервер по IP и порту",702,ShowRooms,false,"Home.Coop");
            }
            else
            {
                HomeAction("Начать экспедицию","Выбрать режим и создать свой мир",546,ShowWorld,true,"Home.Play");
                HomeAction("Мультиплеер","Добавить сервер по IP и порту",624,ShowRooms,false,"Home.Coop");
            }
            var settings=Button("Настройки",88,821,200,52,()=>ShowSettings(Page.Home));settings.name="Home.Settings";
            var quit=Button("Выйти",302,821,170,52,()=>Application.Quit());quit.name="Home.Quit";
            Label(content.transform,"Космические полёты пока в разработке",1057,601,465,32,17,White,true);
            var card=Box(content.transform,1047,646,474,138,new Color(.021f,.043f,.052f,.88f));
            card.raycastTarget=false;
            Box(card.transform,0,0,4,138,Accent,false);
            Label(card.transform,"01   /   ЛЕСТИНГИЯ",22,18,425,27,13,Accent,true);
            Label(card.transform,"Приключение начинается здесь",22,53,425,38,21,White,true);
            Label(card.transform,"Космос и совместные перелёты — следующая глава.",22,101,430,25,13,White);
            Label(content.transform,"Фон — концепт-арт · разработка продолжается",1049,818,472,34,11,White,false,TextAnchor.MiddleRight);
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
            Label(content.transform,"Все начинают в Лестингии. Космос и планеты пока в разработке: здесь экспедиция начинается в Лестингии.",66,247,1440,42,19,Muted);
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
            status=Label(content.transform,"Сюжет: глава I в Лестингии. Остальные главы ещё разрабатываются.",542,784,940,66,14,Muted);
        }
        int lobbyRevision=-1;
        public void ShowLobby()
        {
            Clear(Page.Lobby);lobbyRevision=game.LobbyRevision;Header("Сервер · "+game.ModeName);
            Label(content.transform,"МИР ГОТОВ К ЗАХОДУ",64,160,1000,48,30,White,true);
            LobbyAddressCard(content.transform,64,222,1472);
            string names="";int count=0;foreach(var peer in game.LobbyPlayers){names+=peer.name+(peer.id==game.HostId?" · хозяин":"")+"\n";count++;}
            if(count==0)names="Пока только вы.\nПозовите друзей: Мультиплеер → «Добавить сервер» → "+game.EndpointAddress;
            Label(content.transform,names,64,382,1000,320,24,White);
            if(game.IsHost)Button("Начать игру  →",1120,392,416,68,game.StartLobby,true);
            else Label(content.transform,"Ждём, когда хозяин начнёт игру",1120,398,416,70,22,Accent);
            var invite=Button("Скопировать приглашение",1120,486,416,58,()=>GUIUtility.systemCopyBuffer=game.InviteText,true);invite.name="Lobby.Invite";
            Label(content.transform,"Скопирует текст с адресом и ссылкой на скачивание — можно сразу отправить другу.",1120,548,416,54,13,Muted);
            Button("Выйти",1120,612,416,58,game.ReturnToMenu);
            status=Label(content.transform,"Друзьям нужен только этот адрес: мир уже опубликован на сервере.",64,806,1000,32,15,Muted);
            if(game.IsHost&&game.AutoStartLobby&&!game.IsConnecting)game.StartLobby();
        }
        public void ShowPause()
        {
            Clear(Page.Pause);Header("Пауза");
            Label(content.transform,"МИР ПОДОЖДЁТ",64,210,680,76,44,White,true);
            Label(content.transform,game.WorldLabel+"\n"+(game.Online?"Комната "+game.RoomCode+" · сетевой мир не останавливается":"Локальная экспедиция"),67,300,680,82,21,Muted);
            var storyCard=Box(content.transform,776,174,760,334,Panel);
            Label(storyCard.transform,"СЮЖЕТ И БОРТОВОЙ ЖУРНАЛ",28,22,704,36,22,Accent,true);
            Label(storyCard.transform,game.PauseStorySummary,28,68,704,176,17,White);
            Button("Открыть все 3 главы сюжета  →",28,254,704,56,ShowStoryJournal,true,storyCard.transform);
            Button("Продолжить  →",64,534,395,64,game.Resume,true);
            Button("Сюжет и журнал",505,534,420,64,ShowStoryJournal);
            Button("Аккаунт / достижения",505,698,420,58,ShowAccount);
            Button("Настройки",64,619,395,58,()=>ShowSettings(Page.Pause));
            Button("В главное меню",64,698,395,58,game.ReturnToMenu);
            Label(content.transform,"ESC — продолжить · E — инвентарь · Шаттл: SHIFT/CTRL — тяга, X — тормоз, G — цель, J — прыжок, L — посадка",952,535,584,84,15,Muted);
            if(game.Online)Button("Скопировать адрес сервера",505,619,420,58,()=>GUIUtility.systemCopyBuffer=game.EndpointAddress);
            else {var hostServer=Button("Создать сервер",952,630,584,62,()=>game.OpenWorldAsServer("Мир "+game.PlayerName),true);hostServer.name="Pause.HostServer";
                  Label(content.transform,"Мир откроется для друзей по адресу "+game.EndpointAddress+".",952,696,584,54,14,Muted);}
        }
        public void ShowStoryJournal()
        {
            Clear(Page.Journal);Header("Сюжет · Архив экспедиции");
            var card=Box(content.transform,64,150,1472,620,Panel);
            Label(card.transform,"ТЕКУЩАЯ ЗАДАЧА: "+game.PauseStorySummary.Replace("\n\n","  ·  "),32,20,1408,58,18,Accent,true);
            var entries=LestingCraft.Story.StoryChapters.Entries;
            for(int i=0;i<entries.Length;i++)
            {
                int col=i%2,row=i/2;
                var box=Box(card.transform,32+col*710,88+row*168,690,156,new Color(.045f,.075f,.088f,.98f));
                Label(box.transform,entries[i].ChapterRu+" · "+entries[i].TitleRu,18,12,654,30,18,Accent,true);
                Label(box.transform,entries[i].BodyRu,18,46,654,74,14,White);
                Label(box.transform,"Источник: "+entries[i].SpeakerRu,18,122,654,28,14,Muted);
            }
            Button("← Назад в меню паузы",64,798,380,62,ShowPause,true);
            Button("Продолжить игру  →",468,798,380,62,game.Resume);
        }
        public void ShowInventory(int station=0,Vector3Int cell=default)
        {
            if(station==0&&game.Session&&game.Session.Creative&&!creativeBag){ShowCreativeCatalog();return;}
            stationKind=station;stationCell=cell;craftWidth=station==16?3:2;
            Clear(Page.Inventory);LayoutWidth(1280);
            Box(content.transform,16,119,1248,688,new Color(.025f,.043f,.05f,.97f));
            Header(station==16?"Верстак · 3×3":station==17?"Печь · личная готовка":"Инвентарь · 2×2",false);
            inventoryViews.Clear();craftRevision=-1;
            if(game.Session&&game.Session.Creative)Button("Каталог предметов",424,28,244,52,ShowCreativeCatalog);
            if(!game.Session)return;
            for(int i=0;i<Equipment.Count;i++)
            {float x=32+i*96;Label(content.transform,i==7?"Броне-\nсапоги":Equipment.Names[i],x,145,90,49,16,Muted);CreateCell(58+i,x,197,62,62);}
            Label(content.transform,"Левая рука · G",800,145,100,45,16,Muted);CreateCell(66,800,197,62,62);
            equipmentStatus=Label(content.transform,"",936,144,166,135,18,White);equipmentStatus.name="Equipment summary";
            var portrait=Rect("3D equipped character",content.transform,1124,130,120,180).gameObject.AddComponent<RawImage>();portrait.raycastTarget=false;portrait.gameObject.AddComponent<CharacterPreview>().Initialize(game);
            Label(content.transform,"ЛКМ — взять / положить · ПКМ — разделить\nShift-клик — перенести или надеть",32,279,528,55,17,Muted);
            Label(content.transform,"РЮКЗАК",32,334,528,30,20,Accent,true);
            for(int i=0;i<45;i++)CreateCell(i,32+(i%9)*59,375+(i/9)*63,56,59);
            Label(content.transform,"Наведи на предмет — название и прочность.\nЭкипировка: Shift-клик — надеть или снять.",32,720,528,62,17,Muted);
            Label(content.transform,station==17?"ПЕЧЬ":"СЕТКА "+craftWidth+"×"+craftWidth,588,318,328,38,20,Accent,true);
            if(station==17)
            {
                Label(content.transform,"Сырьё",588,368,140,29,18,Muted);CreateCell(54,588,403,78,78);
                Label(content.transform,"Топливо",588,512,145,29,18,Muted);CreateCell(55,588,547,78,78);
                Label(content.transform,"→",686,448,50,48,30,Accent);CreateCell(56,768,432,82,82);
                Label(content.transform,"Результат",768,527,145,29,18,Muted);
                stoveStatus=Label(content.transform,"",588,648,328,91,18,White);stoveStatus.name="Cooking status";
                Label(content.transform,"Личная очередь сохраняется.\nДля готовки оставайся рядом с печью.",588,747,328,51,16,Muted);
            }
            else
            {
                for(int y=0;y<craftWidth;y++)for(int x=0;x<craftWidth;x++)CreateCell(45+y*3+x,588+x*68,381+y*68,63,63);
                Label(content.transform,"→",798,433,36,48,28,Accent);
                var result=Button("",843,429,73,73,()=>{});result.name="Craft.Result";
                var click=result.gameObject.AddComponent<InventorySlot>();click.Owner=this;click.Index=57;
                craftOutput=Rect("Recipe output icon",result.transform,7,3,59,53).gameObject.AddComponent<RawImage>();craftOutput.raycastTarget=false;
                craftCount=Label(result.transform,"",7,46,59,29,15,White,false,TextAnchor.MiddleRight);AttachHover(result.gameObject,0);
                Button("Вернуть материалы",588,612,328,50,()=>{if(game.Session.CanInteract){game.Session.Notice=CraftingTable.ReturnGrid(game.Session.Inventory)?"Материалы возвращены":"Недостаточно места — сетка сохранена";RefreshCraftInventory();}});
                Label(content.transform,"Книга выкладывает рецепт.\nРезультат забирается вручную.\nShift-клик — изготовить максимум.\nСетка и предмет на курсоре сохраняются.",588,689,328,110,17,Muted);
            }
            BuildRecipeBook();
            status=Label(content.transform,game.Session.Notice,248,823,996,62,18,Accent);
            Button("←  В мир",32,824,190,55,game.ToggleInventory,true);
            cursorRoot=Rect("Carried inventory stack",content.transform,0,0,64,77);
            cursorImage=cursorRoot.gameObject.AddComponent<RawImage>();cursorImage.raycastTarget=false;
            cursorCount=Label(cursorRoot,"",0,50,62,29,15,White,false,TextAnchor.MiddleRight);
            RefreshCraftInventory();
        }
        void CreateCell(int index,float x,float y,float w,float h)
        {
            var button=Button("",x,y,w,h,()=>{});button.name="Inventory.Slot."+index;
            var click=button.gameObject.AddComponent<InventorySlot>();click.Owner=this;click.Index=index;
            AttachHover(button.gameObject,0);var icon=Rect("Stack icon",button.transform,7,3,w-14,h-20).gameObject.AddComponent<RawImage>();icon.raycastTarget=false;
            var count=Label(button.transform,"",3,h-30,w-6,29,15,White,false,TextAnchor.MiddleRight);
            var wear=Box(button.transform,4,h-5,w-8,3,Accent,false);wear.gameObject.name="Equipment durability "+index;
            inventoryViews.Add(new CellView{Index=index,Icon=icon,Count=count,Hover=button.GetComponent<ItemHover>(),Wear=wear,WearWidth=w-8});
            if(index<9)Label(button.transform,(index+1).ToString(),5,3,17,17,10,Accent);
        }
        void BuildRecipeBook()
        {
            bool cooking=stationKind==17;
            Label(content.transform,cooking?"РЕЦЕПТЫ ПЕЧИ":"КНИГА РЕЦЕПТОВ",944,318,304,38,20,Accent,true);
            var viewport=Rect("Recipe viewport",content.transform,944,366,304,395);viewport.gameObject.AddComponent<RectMask2D>();
            var backing=viewport.gameObject.AddComponent<Image>();backing.color=new Color(.06f,.10f,.11f,.95f);
            var list=Rect("Recipes",viewport,0,0,304,0);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=list;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            if(cooking)
            {
                int row=0;foreach(int id in ItemArtwork.AllIds)
                {int output=FurnaceState.Result(id);if(output<=0)continue;
                 var entry=Box(list,0,row*67,298,62,Panel);Icon(entry.transform,id,3,6,46);Label(entry.transform,"→",50,15,30,40,20,Accent);Icon(entry.transform,output,78,6,46);
                 Label(entry.transform,Items.Get(output).Name,135,4,158,32,16,White);Label(entry.transform,FurnaceState.CookSeconds+" с",135,34,158,24,15,Muted);row++;}
                list.sizeDelta=new Vector2(304,row*67);
            }
            else
            {
                list.sizeDelta=new Vector2(304,CraftingTable.All.Length*62);
                for(int i=0;i<CraftingTable.All.Length;i++)
                {
                    int index=i;var r=CraftingTable.All[i];
                    var button=Button("",0,i*62,298,57,()=>{game.Session.FillRecipe(index,craftWidth);RefreshCraftInventory();},false,list);
                    Icon(button.transform,r.Output,3,3,49);Label(button.transform,Items.Get(r.Output).Name+(r.Fits(craftWidth)?"":" · 3×3"),58,6,234,49,17,r.Fits(craftWidth)?White:Muted);
                }
            }
            Label(content.transform,"Колёсико — прокрутка",944,777,304,27,16,Muted);
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
                view.Count.text=stack.count>0?stack.count.ToString():"";view.Hover.Item=stack.count>0?stack.item:0;view.Hover.Durability=stack.count>0?stack.durability:-1;
                int max=Items.Get(stack.item).Durability;bool worn=stack.count>0&&max>0;view.Wear.gameObject.SetActive(worn);
                if(worn){float fraction=Mathf.Clamp01((float)stack.durability/max);view.Wear.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,view.WearWidth*fraction);view.Wear.color=Color.Lerp(new Color(.95f,.25f,.12f),Accent,fraction);view.Count.text=Mathf.RoundToInt(fraction*100)+"%";}
            }
            if(equipmentStatus)equipmentStatus.text="Защита: "+Mathf.RoundToInt(Equipment.Protection(inv)*100)+"%\nТепло: +"+Equipment.Insulation(inv).ToString("0")+"°\n\nБроня: верстак";
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
        public void ShowItemTooltip(int item,Vector2 screenPosition,int durability=-1)
        {
            if(item<=0||!frame)return;
            if(!tooltip)
            {
                var panel=Box(frame,0,0,400,144,new Color(.025f,.045f,.05f,1f),false);tooltip=panel.rectTransform;
                tooltip.name="Item name tooltip";tooltipText=Label(tooltip,"",18,7,364,130,18,White,false,TextAnchor.MiddleLeft);
            }
            var definition=Items.Get(item);tooltipText.text=definition.Name;
            if(definition.Durability>0&&durability>=0)tooltipText.text+="\nПрочность: "+durability+" / "+definition.Durability;
            if(Equipment.ItemProtection(item)>0)tooltipText.text+=" · защита "+Mathf.RoundToInt(Equipment.ItemProtection(item)*100)+"%";
            if(definition.Insulation>0)tooltipText.text+="\nУтепление: +"+definition.Insulation.ToString("0")+"°";
            if(Equipment.Slot(item)>=0)tooltipText.text+="\nShift + клик — надеть / снять";tooltip.gameObject.SetActive(true);tooltip.SetAsLastSibling();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,screenPosition,null,out var local);
            tooltip.anchoredPosition=new Vector2(Mathf.Clamp(local.x-frame.rect.xMin+18,8,frame.rect.width-408),-Mathf.Clamp(frame.rect.yMax-local.y+20,8,frame.rect.height-152));
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
            Button("Объёмы: "+(settings.VolumetricQuality==2?"Ultra":settings.VolumetricQuality==1?"высокие":"сокращённые"),92,667,315,38,()=>{settings.VolumetricQuality=(settings.VolumetricQuality+1)%3;settings.Save();ShowSettings(settingsBack);});
            Button("SSAO: "+(settings.AmbientOcclusion?"вкл.":"выкл."),424,667,323,38,()=>{settings.AmbientOcclusion=!settings.AmbientOcclusion;settings.Save();ShowSettings(settingsBack);});
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
            Clear(Page.HUD);LayoutWidth(1280);
            loadingHUD=Label(content.transform,"",36,31,780,37,14,Accent);
            // Fine reticle: thin cross with a soft center dot, not an IMGUI text '+'.
            foreach(var r in new[]{new Vector4(639,437,2,6),new Vector4(639,457,2,6),new Vector4(627,449,6,2),new Vector4(647,449,6,2)})Box(content.transform,r.x,r.y,r.z,r.w,new Color(1,1,1,.68f),false);
            Box(content.transform,639,449,2,2,new Color(1,1,1,.9f),false);
            slots=new Image[9];slotRects=new RectTransform[9];slotIcons=new RawImage[9];slotCounts=new Text[9];slotHovers=new ItemHover[9];slotFrames=new Image[9];lastInventoryRevision=-1;lastSlot=-1;
            var backdrop=Box(content.transform,271,797,738,92,new Color(.025f,.05f,.055f,.88f));
            for(int i=0;i<9;i++)
            {
                var image=Box(backdrop.transform,10+i*80,10,76,72,new Color(.10f,.155f,.165f,.92f));slots[i]=image;slotRects[i]=image.rectTransform;
                image.raycastTarget=true;AttachHover(image.gameObject,0);slotHovers[i]=image.GetComponent<ItemHover>();
                var raw=Rect("Item icon",image.transform,14,2,48,48).gameObject.AddComponent<RawImage>();raw.raycastTarget=false;slotIcons[i]=raw;
                slotCounts[i]=Label(image.transform,"",45,50,27,22,15,White,false,TextAnchor.MiddleRight);
                Label(image.transform,(i+1).ToString(),6,50,24,22,12,Muted,false,TextAnchor.MiddleLeft);
                var frame=Rect("Selected frame",image.transform,0,0,0,0);Stretch(frame);
                var frameImage=frame.gameObject.AddComponent<Image>();frameImage.sprite=MenuArtwork.Outline;frameImage.type=Image.Type.Sliced;frameImage.raycastTarget=false;frameImage.color=new Color(Accent.r,Accent.g,Accent.b,0);slotFrames[i]=frameImage;
            }
            // Item names are hover/focus tooltips, never permanent slot text.

            var healthPanel=Box(content.transform,24,652,252,142,new Color(.025f,.05f,.055f,.91f));
            healthHUD=Label(healthPanel.transform,"HP · 100 / 100",14,10,224,28,17,White);
            var body=Rect("Health bar",healthPanel.transform,14,42,224,9);healthBody=body.gameObject.AddComponent<HealthSilhouette>();healthBody.Build();
            Label(healthPanel.transform,"O₂",14,60,30,24,15,White);
            Box(healthPanel.transform,48,66,190,8,new Color(.10f,.14f,.16f,.95f),false);
            oxygenBar=Box(healthPanel.transform,48,66,190,8,new Color(.42f,.72f,.88f),false);
            vitalsHUD=Label(healthPanel.transform,"",14,82,224,52,12,White);
            // Objectives panel, top-right: living checklist driven by the current expedition
            // state. Checkbox squares are plain images (font-safe, square-corner HUD language).
            var objectivePanel=Box(content.transform,982,24,274,178,new Color(.025f,.05f,.055f,.82f));
            Label(objectivePanel.transform,"ЗАДАЧИ",18,12,238,30,18,Accent,true);
            Box(objectivePanel.transform,18,42,54,2,Accent,false);
            objectiveChecks=new Image[3];objectiveLabels=new Text[3];
            for(int i=0;i<3;i++)
            {
                var check=Box(objectivePanel.transform,20,58+i*30,14,14,new Color(.32f,.40f,.42f,.9f),false);
                check.raycastTarget=false;objectiveChecks[i]=check;
                objectiveLabels[i]=Label(objectivePanel.transform,"",44,51+i*30,214,28,14,White);
            }
            objectiveHUD=Label(objectivePanel.transform,"",18,150,240,24,11,Muted);
            miningBar=Box(content.transform,565,484,0,4,Accent,false);miningBar.gameObject.name="Mining progress";
            miningLabel=Label(content.transform,"",540,494,200,29,12,White,false,TextAnchor.MiddleCenter);
            chatRoot=Box(content.transform,26,265,590,260,new Color(.025f,.045f,.04f,.78f)).gameObject;
            chatText=Label(chatRoot.transform,"",18,13,554,168,15,White);chatText.verticalOverflow=VerticalWrapMode.Truncate;
            var chatBox=Box(chatRoot.transform,14,193,562,49,Panel);chatBox.raycastTarget=true;chatInput=chatBox.gameObject.AddComponent<InputField>();
            chatInput.targetGraphic=chatBox;chatInput.characterLimit=240;chatInput.lineType=InputField.LineType.SingleLine;
            chatInput.textComponent=Label(chatBox.transform,"",12,0,538,49,15,White,false,TextAnchor.MiddleLeft);
            chatInput.placeholder=Label(chatBox.transform,"Сообщение…",12,0,538,49,15,Muted,false,TextAnchor.MiddleLeft);
            chatInput.gameObject.SetActive(false);chatRoot.SetActive(false);chatRevision=-1;
            noticeHUD=Label(content.transform,"",320,620,640,51,18,Accent,false,TextAnchor.MiddleCenter);BuildUsabilityHUD();
        }
        void Update()
        {
            if(page==Page.Account&&status&&game.Accounts)status.text=game.Accounts.Message;
            if(game.InLobby){if(page!=Page.Lobby||lobbyRevision!=game.LobbyRevision)ShowLobby();return;}
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
            if((page==Page.Inventory||page==Page.Creative)&&game.Session)
            {
                if(craftRevision!=game.Session.Inventory.revision)RefreshCraftInventory();
                if(cursorRoot&&UnityEngine.InputSystem.Mouse.current!=null)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,UnityEngine.InputSystem.Mouse.current.position.ReadValue(),null,out var local);
                    cursorRoot.anchoredPosition=new Vector2(local.x-frame.rect.xMin+12,local.y-frame.rect.yMax+12);cursorRoot.SetAsLastSibling();
                }
                if(page==Page.Inventory&&stationKind==17&&stoveStatus){var f=game.Session.Inventory.furnace;stoveStatus.text=$"Готовка: {Mathf.Clamp01(f.progress/FurnaceState.CookSeconds)*100:0}%\nТопливо: {f.burn:0.0} с\nПауза, если отошёл от печи";}
            }
            UpdateUsability();
            if(page!=Page.HUD)return;
            if(UnityEngine.InputSystem.Keyboard.current!=null&&UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)content.gameObject.SetActive(!content.gameObject.activeSelf);
            averageFrame=Mathf.Lerp(averageFrame,Mathf.Min(Time.unscaledDeltaTime,.5f),.04f);timer+=Time.unscaledDeltaTime;
            if(timer>.25f)
            {
                timer=0;
                if(game.Session)
                {
                    var session=game.Session;var v=session.Vitals;
                    healthBody.SetBody(v);healthHUD.text=session.Creative?"ТВОРЧЕСКИЙ РЕЖИМ":$"HP · {v.health:0} / 100";
                    string extraTech="";
                    if(game.Tech)
                    {
                        var sim=game.Tech.Sim;
                        if(sim.CurrentMillisievertsPerHour>8f||sim.RadiationDose01>0.03f||session.Inventory.Active.item==Items.Dosimeter)
                            extraTech+=$"\n☢ Фон  {sim.CurrentMillisievertsPerHour:0} мЗв/ч ({sim.RadiationDose01*100f:0}%)";
                        if(sim.Grid.GenerationKw>0.1f||sim.Grid.DemandKw>0.1f)
                            extraTech+=$"\n⚡ Сеть  {sim.Grid.GenerationKw:0}/{sim.Grid.DemandKw:0} кВт · {sim.Grid.StoredKwh:0} кВт·ч";
                    }
                    vitalsHUD.text=$"Еда {v.hunger:0}  ·  Силы {v.stamina:0}\n{v.temperature:0.0}°C  ·  Влага {v.wetness*100:0}%"+(v.air<100?$"  ·  O₂ {v.air:0}%":"")+extraTech;
                    if(oxygenBar)oxygenBar.rectTransform.sizeDelta=new Vector2(Mathf.Max(0,190*Mathf.Clamp01(v.air/100)),8);
                    RefreshObjectives(session);

                    if(lastNotice!=session.Notice){lastNotice=session.Notice;noticeAt=Time.unscaledTime;}
                    noticeHUD.text=Time.unscaledTime-noticeAt<4?lastNotice:"";
                }
                if(!game.WorldReady)
                    loadingHUD.text="Формирование ландшафта… "+game.ChunkCount+" чанков";
                else if(game.InRadiaDimension&&game.Tech)
                {
                    var s=game.Tech.Sim;
                    loadingHUD.text=s.EscapePortalOpen
                        ?"☢ ЧЕРЕНКОВСКИЙ ЗАТОН · ШЛЮЗ ОТКРЫТ! Идите в белый разлом над терминалом плотины!"
                        :s.PurgeActive
                            ?$"☢ СИРЕНА ПРОДУВКИ ШЛЮЗА · Продержитесь ещё {s.PurgeRemainingSeconds:0} сек!"
                            :$"☢ ЧЕРЕНКОВСКИЙ ЗАТОН · Ядра со дна: {s.HarvestedSubmergedCores}/6 · Боссы Спирали: {s.DefeatedBossCount}/3";
                }
                else loadingHUD.text="";
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
                    var stack=inventory.slots[i];
                    bool chosen=i==inventory.selected;
                    slots[i].color=chosen?new Color(.20f,.23f,.17f,.97f):new Color(.10f,.155f,.165f,.92f);
                    if(slotFrames!=null&&i<slotFrames.Length&&slotFrames[i])
                        slotFrames[i].color=chosen?new Color(Accent.r,Accent.g,Accent.b,.95f):new Color(Accent.r,Accent.g,Accent.b,0f);
                    var texture=ItemArtwork.Get(stack.item);slotIcons[i].texture=texture;slotIcons[i].enabled=texture;
                    slotHovers[i].Item=stack.count>0?stack.item:0;
                    slotCounts[i].text=stack.count>0?(game.Session.Creative?"∞":stack.count.ToString()):"";
                }

            }
        }
        void RefreshObjectives(LestingCraft.Gameplay.SurvivalSession session)
        {
            if(objectiveChecks==null||objectiveLabels==null||session==null)return;
            string[] lines;bool[] done;
            if(session.Creative)
            {
                lines=new[]{"Свободное строительство","Каталог предметов","Экспериментируйте с формами"};
                done=new[]{true,true,false};
            }
            else
            {
                var inv=session.Inventory;
                bool wood=inv.Count(4)>0||inv.Count(Items.Planks)>0;
                bool bench=inv.Count(Items.Workbench)>0||inv.Count(Items.Planks)>=4;
                bool tool=inv.Count(Items.WoodPick)>0||inv.Count(Items.StonePick)>0||inv.Count(Items.Axe)>0||
                          inv.Count(150)>0||inv.Count(151)>0;
                lines=new[]{"Добыть древесину","Собрать верстак","Создать инструмент"};
                done=new[]{wood,bench,tool};
            }
            for(int i=0;i<3;i++)
            {
                if(objectiveLabels[i])objectiveLabels[i].text=lines[i];
                if(objectiveChecks[i])
                    objectiveChecks[i].color=done[i]?new Color(Accent.r,Accent.g,Accent.b,.95f):new Color(.32f,.40f,.42f,.9f);
                if(objectiveLabels[i])objectiveLabels[i].color=done[i]?Muted:White;
            }
            if(objectiveHUD)
            {
                string summary=game!=null?game.PauseStorySummary:"";
                if(!string.IsNullOrEmpty(summary))
                {
                    int cut=summary.IndexOf('\n');if(cut>0)summary=summary.Substring(0,cut);
                    objectiveHUD.text=summary.Length>54?summary.Substring(0,52)+"…":summary;
                }
                else objectiveHUD.text="";
            }
        }
        void OnDestroy(){if(rounded)Destroy(rounded);}
        int achievementPage;bool achievementOnlyEarned;
        void RefreshAccountIfOpen(){if(page==Page.Account)ShowAccount();}
        void ShowAccount()
        {
            Clear(Page.Account);Header("Игровой аккаунт",false);var a=game.Accounts;
            Button("← Назад",64,806,205,52,()=>{if(game.InWorld)ShowPause();else ShowHome();});
            if(!a.Logged)
            {
                string username="",password="",recovery="";
                Label(content.transform,"Аккаунт необязателен для игры. Войдите до экспедиции, чтобы сохранять прогресс между устройствами.",64,214,1450,52,20,White);
                Input("ЛОГИН · 3–24 латинских символа",username,"Explorer_01",64,285,590,v=>username=v);
                var pw=Input("ПАРОЛЬ · минимум 12 символов",password,"Не отправляйте пароль в чат",64,398,590,v=>password=v);pw.contentType=InputField.ContentType.Password;pw.characterLimit=128;
                var rc=Input("КОД ВОССТАНОВЛЕНИЯ · только для восстановления",recovery,"Получается один раз при регистрации",64,510,590,v=>recovery=v);rc.contentType=InputField.ContentType.Password;
                Action<string> submit=action=>{string capturedPassword=password,r=recovery;pw.text="";rc.text="";a.Authenticate(action,username,capturedPassword,r,RefreshAccountIfOpen);};
                Button("Войти",64,636,280,58,()=>submit("login"),true);Button("Регистрация",364,636,290,58,()=>submit("register"));Button("Восстановить доступ",64,711,590,52,()=>submit("recover"));
                Label(content.transform,"Без электронной почты\n\nПри регистрации вы получите личный код восстановления. Сохраните его вне игры и никому не показывайте. При восстановлении старый код заменяется.\n\nПароль и токен входа не записываются в PlayerPrefs. Вход действует в памяти текущего запуска игры. Без сервиса аккаунтов локальное выживание остаётся доступным.",800,285,650,390,20,Muted);
                status=Label(content.transform,a.Message,800,705,650,90,18,Accent);return;
            }
            var p=a.Profile;Label(content.transform,p.username+" · уровень "+p.level+" · "+p.xp+" XP",64,205,1450,46,27,White,true);
            Label(content.transform,p.titleName+" · "+p.achievements.Length+" / 200 достижений",64,260,1450,36,21,Accent);
            if(!string.IsNullOrEmpty(a.RecoveryCode))
            {
                Label(content.transform,"СОХРАНИТЕ КОД ВОССТАНОВЛЕНИЯ. Он не будет храниться в игре. Не отправляйте его в чат.",64,340,1410,90,23,White);
                Label(content.transform,a.RecoveryCode,64,465,1400,100,23,Accent);
                Button("Скопировать код",64,620,450,64,()=>GUIUtility.systemCopyBuffer=a.RecoveryCode);Button("Код сохранён →",550,620,450,64,()=>{a.DismissRecovery();ShowAccount();},true);return;
            }
            Label(content.transform,"@"+p.username+"  ·  уровень "+p.level+"  ·  "+p.xp+" XP",64,196,1450,44,26,White,true);
            Label(content.transform,p.titleName+"  ·  "+p.achievements.Length+" / 200 достижений",64,246,1450,32,19,Accent);
            Label(content.transform,"Охота: "+a.Counter("kills")+" · Страж: "+a.Counter("bosses")+" · Добыто: "+a.Counter("mined")+" · Построено: "+a.Counter("placed")+" · Создано: "+a.Counter("crafted")+" · Смертей: "+a.Counter("deaths")+" · В игре: "+(a.Counter("alive_s")/60)+" мин",64,284,1460,40,17,White);
            if(a.Wallet==null)a.CloudState(()=>{if(page==Page.Account)ShowAccount();});
            string walletLine=a.Wallet==null?"Кошелёк: загружается…":"Кошелёк: "+CloudFormat.Coins(a.Wallet.balance)+" монет"+(a.Wallet.profile!=null&&!string.IsNullOrEmpty(a.Wallet.profile.avatar_skin)?" · скин "+a.Wallet.profile.avatar_skin:" · скин не выбран");
            Label(content.transform,walletLine,64,330,1460,28,17,Accent);
            // Friends are added by nickname, so the nickname is the visible identity:
            // one compact card with the name and a copy button, nothing technical.
            var nick=Box(content.transform,64,372,760,92,new Color(.055f,.085f,.10f,.98f));
            Label(nick.transform,"ВАШ НИК",22,10,300,22,13,Accent,true);
            Label(nick.transform,"@"+a.Profile.username,22,32,520,42,26,White,true);
            Label(nick.transform,"Друзья находят вас по этому нику",22,76,560,14,12,Muted);
            Button("Скопировать",600,26,140,42,()=>GUIUtility.systemCopyBuffer=a.Profile.username,false,nick.transform);
            Button("200 достижений  →",64,486,440,50,()=>{achievementPage=0;ShowAchievements();},true);
            Button("Синхронизировать",530,486,440,50,()=>a.Sync(RefreshAccountIfOpen));
            Button("Сменить титул",996,486,440,50,()=>{var titles=new List<string>{""};foreach(var d in a.Achievements)if(!string.IsNullOrEmpty(d.title)&&Array.IndexOf(p.achievements,d.id)>=0)titles.Add(d.id);a.ChangeTitle(titles[(titles.IndexOf(p.title)+1)%titles.Count],RefreshAccountIfOpen);});
            string current="",next="";var old=Input("ТЕКУЩИЙ ПАРОЛЬ",current,"Для смены пароля / удаления",64,556,660,v=>current=v);old.contentType=InputField.ContentType.Password;old.characterLimit=128;
            var fresh=Input("НОВЫЙ ПАРОЛЬ",next,"Минимум 12 символов",780,556,660,v=>next=v);fresh.contentType=InputField.ContentType.Password;fresh.characterLimit=128;
            Button("Изменить пароль",64,664,440,52,()=>{string c=current,n=next;old.text="";fresh.text="";a.ChangePassword(c,n,RefreshAccountIfOpen);});
            bool confirm=false;Button("Удалить аккаунт (двойное подтверждение)",530,664,660,52,()=>{if(!confirm){confirm=true;a.Message="Повторное нажатие удалит профиль, достижения и статистику. Нужен текущий пароль.";if(status)status.text=a.Message;return;}string c=current;old.text="";a.Delete(c,RefreshAccountIfOpen);});
            var walletButton=Button("Кошелёк и скины  →",64,732,440,50,ShowStore,true);walletButton.name="Account.Store";
            var friendsButton=Button("Друзья  →",530,732,440,50,()=>{a.CloudFriends(AfterCloud);ShowFriends();});friendsButton.name="Account.Friends";
            Button("Выйти из аккаунта",64,798,440,44,()=>a.Logout(RefreshAccountIfOpen));
            status=Label(content.transform,a.Message,996,732,440,110,16,Accent);
        }
        void ShowAchievements()
        {
            Clear(Page.Achievements);Header("Достижения · 200",false);var a=game.Accounts;if(!a.Logged){ShowAccount();return;}
            var list=new List<AchievementDefinition>();foreach(var d in a.Achievements)if(!achievementOnlyEarned||Array.IndexOf(a.Profile.achievements,d.id)>=0)list.Add(d);
            int pages=Math.Max(1,(list.Count+7)/8);achievementPage=Math.Max(0,Math.Min(pages-1,achievementPage));
            for(int i=0;i<8;i++){int index=achievementPage*8+i;if(index>=list.Count)break;var d=list[index];bool earned=Array.IndexOf(a.Profile.achievements,d.id)>=0;float x=64+i%2*746,y=230+i/2*133;var card=Box(content.transform,x,y,718,118,Panel);Label(card.transform,(earned?"✓ ":"")+d.name,16,10,680,30,19,earned?Accent:White);Label(card.transform,d.description,16,44,680,42,14,Muted);Label(card.transform,d.category+" · "+Math.Min(d.target,a.Counter(d.counter))+" / "+d.target+" · "+d.xp+" XP",16,90,680,22,13,Muted);}
            Button("← Профиль",64,807,230,52,ShowAccount);Button("←",600,807,90,52,()=>{achievementPage--;ShowAchievements();});Label(content.transform,(achievementPage+1)+" / "+pages,715,821,150,35,21,White);Button("→",865,807,90,52,()=>{achievementPage++;ShowAchievements();});Button(achievementOnlyEarned?"Только полученные":"Все достижения",1110,807,375,52,()=>{achievementOnlyEarned=!achievementOnlyEarned;achievementPage=0;ShowAchievements();});
        }
    }
}
