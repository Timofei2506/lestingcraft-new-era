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
        // Panels sit on top of a live bright world: the alpha is high enough that body text
        // stays readable over clouds and sunlit grass (player report: "плохо видно меню на ESC").
        static readonly Color Ink=new Color(.11f,.07f,.015f), Panel=new Color(.055f,.145f,.235f,.90f), Muted=new Color(.84f,.92f,.97f), White=new Color(.99f,.99f,.97f), Accent=new Color(1.00f,.78f,.20f), SpringAccent=new Color(.34f,.90f,.48f);
        static readonly string[] JoySplashes={"Солнечный живой мир!","Строй уютно, летай к звёздам!","Цветущие долины ждут!","Вместе веселее!"};
        PrototypeGame game;Canvas canvas;RectTransform frame,splashRect;GameObject content,background;RawImage backdropRaw;Font body,display;Sprite rounded;
        readonly List<GameObject> transient=new List<GameObject>();readonly List<Button> actions=new List<Button>();
        Page page,settingsBack;Text status,loadingHUD;Image[] slots;RectTransform[] slotRects;
        Text vitalsHUD,objectiveHUD,noticeHUD,healthHUD;HealthSilhouette healthBody;RawImage[] slotIcons;Text[] slotCounts;ItemHover[] slotHovers;RectTransform tooltip;Text tooltipText;int lastInventoryRevision=-1,lastSlot=-1;
        Image oxygenBar,staminaBar,thirstBar,temperatureBar;RectTransform oxygenRow,staminaRow,thirstRow,temperatureRow;RectTransform[] vitalsRows;Text foodHUD,tempHUD;Image[] slotFrames;Image[] objectiveChecks;Text[] objectiveLabels;
        Image miningBar;Text miningLabel;GameObject chatRoot;Text chatText;InputField chatInput;int chatRevision=-1;string lastNotice="";float noticeAt;
        RectTransform minimapPanel;RawImage minimapRaw;Text minimapCoords;Texture2D minimapTex;float minimapTimer;
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
        string recipeSearch="";bool recipeFitsOnly;RectTransform recipeList;ScrollRect recipeScroll;
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
            backdropRaw=background.AddComponent<RawImage>();backdropRaw.texture=MenuArtwork.Backdrop;backdropRaw.color=backdropRaw.texture?Color.white:new Color(.14f,.46f,.74f);backdropRaw.raycastTarget=false;background.AddComponent<MenuBackdropFit>();
            var shade=Rect("Art Shade",background.transform,0,0,0,0);Stretch(shade);var shading=shade.gameObject.AddComponent<RawImage>();shading.texture=MenuArtwork.Shade;shading.raycastTarget=false;
            var vig=Rect("Art Vignette",background.transform,0,0,0,0);Stretch(vig);var vignette=vig.gameObject.AddComponent<RawImage>();vignette.texture=MenuArtwork.Vignette;vignette.raycastTarget=false;
            SyncBackdropVisibility();
            frame=Rect("1600x900 Interface",transform,0,0,1600,900);frame.anchorMin=frame.anchorMax=new Vector2(.5f,.5f);frame.pivot=new Vector2(.5f,.5f);frame.anchoredPosition=Vector2.zero;frame.gameObject.AddComponent<UiSafeArea>().Initialize(canvas,frame);
            content=Rect("Content",frame,0,0,1600,900).gameObject;ShowHome();ready=true;
            Debug.Log("[Presentation] Home screen created");
        }
        void SyncBackdropVisibility()
        {
            if(!backdropRaw)return;
            bool live3D=MenuPanoramaScene.IsActive;
            backdropRaw.color=live3D?new Color(1f,1f,1f,0f):(backdropRaw.texture?Color.white:new Color(.14f,.46f,.74f));
        }
        static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var obj=new GameObject(name,typeof(RectTransform));var rt=obj.GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        static void Stretch(RectTransform rt){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,.5f);rt.offsetMin=rt.offsetMax=Vector2.zero;}
        void Clear(Page next)
        {
            HideItemTooltip();LayoutWidth(1600);page=next;content.SetActive(true);splashRect=null;
            for(int i=content.transform.childCount-1;i>=0;i--){var child=content.transform.GetChild(i).gameObject;child.SetActive(false);if(Application.isPlaying)Destroy(child);else DestroyImmediate(child);}
            actions.Clear();transient.Clear();status=null;
            background.SetActive(next!=Page.HUD);lastSelection=0;
            SyncBackdropVisibility();
            if(!game.InWorld&&next!=Page.Home&&next!=Page.HUD)
            {
                var menuScrim=Box(content.transform,0,0,1600,900,new Color(.045f,.13f,.22f,.32f),false);
                menuScrim.raycastTarget=false;
            }
            if(game.InWorld&&next!=Page.HUD)
            {
                background.SetActive(false);
                // A pause screen drawn over a sunlit world needs a real veil, not a tint.
                float veilAlpha=next==Page.Pause?.78f:.82f;
                var veil=Box(content.transform,0,0,1600,900,new Color(.03f,.075f,.125f,veilAlpha),false);
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
            Label(content.transform,label,x,y,w,25,14,Muted);var image=Box(content.transform,x,y+34,w,58,new Color(.08f,.20f,.30f,.88f));image.raycastTarget=true;
            var field=image.gameObject.AddComponent<InputField>();field.targetGraphic=image;field.characterLimit=64;
            field.textComponent=Label(image.transform,"",18,0,w-36,58,19,White,false,TextAnchor.MiddleLeft);
            field.placeholder=Label(image.transform,placeholder,18,0,w-36,58,19,Muted,false,TextAnchor.MiddleLeft);
            field.text=value;field.onValueChanged.AddListener(v=>changed(v));return field;
        }
        Text ShadowLabel(Transform parent,string text,float x,float y,float w,float h,int size,bool title,TextAnchor align=TextAnchor.UpperLeft)
        {
            return Label(parent,text,x+3,y+3,w,h,size,new Color(.02f,.07f,.12f,.52f),title,align);
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
            // Section caption used to be drawn straight onto the bright sky; a small plate keeps it legible.
            Box(content.transform,1050,40,486,42,new Color(.03f,.08f,.13f,.62f));
            Label(content.transform,section.ToUpperInvariant(),1060,48,470,40,14,Accent,false,TextAnchor.MiddleRight);
            Box(content.transform,64,119,1472,1,new Color(.8f,.9f,.8f,.13f),false);
            Box(content.transform,64,119,96,2,Accent,false);
            if(back)Button("←  Назад",64,798,150,48,()=>{if(game.InWorld)ShowPause();else ShowHome();});
            Label(content.transform,"v"+Application.version+"  ·  LestingCraft: New Era",64,866,1472,22,12,Muted,false,TextAnchor.MiddleRight);
        }
        Image AvatarPortrait(Transform parent,string avatarId,string fallbackSeed,float x,float y,float size)
        {
            var rt=Rect("Avatar",parent,x,y,size,size);
            var img=rt.gameObject.AddComponent<Image>();
            img.sprite=ProfileAvatar.GetSprite(avatarId,fallbackSeed);
            img.type=Image.Type.Simple;
            img.preserveAspect=true;
            img.raycastTarget=false;
            return img;
        }
        InputField InputIn(Transform parent,string label,string value,string placeholder,float x,float y,float w,Action<string> changed,int maxChars=64)
        {
            Label(parent,label,x,y,w,22,13,Muted,true);
            var image=Box(parent,x,y+26,w,50,new Color(.07f,.18f,.28f,.92f));image.raycastTarget=true;
            var field=image.gameObject.AddComponent<InputField>();field.targetGraphic=image;field.characterLimit=maxChars;
            field.textComponent=Label(image.transform,"",16,0,w-32,50,17,White,false,TextAnchor.MiddleLeft);
            field.placeholder=Label(image.transform,placeholder,16,0,w-32,50,17,Muted,false,TextAnchor.MiddleLeft);
            field.text=value??"";field.onValueChanged.AddListener(v=>changed(v));return field;
        }
        void OpenFriendsHub()
        {
            var a=game?game.Accounts:null;
            if(a!=null&&a.Logged)a.CloudFriends(AfterCloud);
            ShowFriends();
        }
        public void ShowHome()
        {
            Clear(Page.Home);
            var a=game?game.Accounts:null;
            bool logged=a!=null&&a.Logged&&a.Profile!=null;
            if(logged&&a.Wallet==null&&!a.CloudBusy)a.CloudState(()=>{if(page==Page.Home)ShowHome();});
            if(logged&&a.Friends==null&&!a.CloudBusy)a.CloudFriends(()=>{if(page==Page.Home)ShowHome();});

            string displayNick=logged?a.Profile.username:ProfileAvatar.LocalNickname;
            string heroAvatar=logged?ProfileAvatar.ResolveId(a.Profile.avatar,displayNick):ProfileAvatar.LocalAvatar;
            if(game)game.PlayerName=displayNick;

            // 1. Top-Left Sunlit Emblem & Hero Title (leaves center vista open for Sky Islands & Waterfalls!)
            var brandPill=Box(content.transform,56,34,474,44,new Color(.060f,.165f,.265f,.76f));
            brandPill.raycastTarget=false;
            Box(brandPill.transform,0,8,4,28,SpringAccent,false);
            Box(brandPill.transform,16,16,12,12,SpringAccent);
            Label(brandPill.transform,"L / C",36,0,66,44,18,Accent,true,TextAnchor.MiddleLeft);
            Label(brandPill.transform,"ПАРЯЩИЕ ОСТРОВА  ·  ЖИВОЙ МИР  ·  NEW ERA",104,0,356,44,11,White,true,TextAnchor.MiddleLeft);

            ShadowLabel(content.transform,"LESTINGCRAFT",56,92,540,62,44,true);
            Label(content.transform,"LESTINGCRAFT",56,92,540,62,44,White,true);
            ShadowLabel(content.transform,"N E W   E R A",58,154,290,32,20,true);
            Label(content.transform,"N E W   E R A",58,154,290,32,20,Accent,true);

            string splashLine=JoySplashes[Mathf.Abs((int)(Time.realtimeSinceStartup/8f))%JoySplashes.Length];
            var splashBadge=Box(content.transform,292,150,254,32,new Color(1f,.80f,.22f,.95f));
            splashBadge.raycastTarget=false;
            splashRect=splashBadge.rectTransform;
            splashRect.pivot=new Vector2(.5f,.5f);
            splashRect.anchoredPosition=new Vector2(292+127,-(150+16));
            Label(splashBadge.transform,splashLine,10,0,234,32,12,Ink,true,TextAnchor.MiddleCenter);

            var vistaPill=Box(content.transform,56,812,544,52,new Color(.060f,.165f,.265f,.72f));
            vistaPill.raycastTarget=false;
            Box(vistaPill.transform,0,10,4,32,Accent,false);
            Label(vistaPill.transform,"v"+Application.version+"  ·  Города, водопады, небесные острова, Ад и 17 миров",18,0,514,52,13,Muted,false,TextAnchor.MiddleLeft);

            // 2. Top-Right Corner: Quick Marketplace Button + Player Balance Widget
            var marketBtn=Button("",1024,32,192,64,ShowMarket);
            marketBtn.name="Home.Market";
            Label(marketBtn.transform,"МАРКЕТПЛЕЙС",16,10,160,18,11,SpringAccent,true,TextAnchor.MiddleLeft);
            Label(marketBtn.transform,"Лоты и чертежи →",16,30,164,24,14,White,true,TextAnchor.MiddleLeft);

            long coins=logged&&a.Wallet!=null?a.Wallet.balance:0;
            string balanceValue=CloudFormat.Coins(coins)+" монет";
            var balanceBtn=Button("",1228,32,316,64,ShowStore);
            balanceBtn.name="Home.Balance";
            var coinBadge=Box(balanceBtn.transform,14,12,40,40,Accent);
            Label(coinBadge.transform,"◈",0,0,40,40,20,Ink,true,TextAnchor.MiddleCenter);
            Label(balanceBtn.transform,"БАЛАНС",66,10,180,18,11,Accent,true,TextAnchor.MiddleLeft);
            Label(balanceBtn.transform,balanceValue,66,28,196,26,19,White,true,TextAnchor.MiddleLeft);
            Label(balanceBtn.transform,logged?"кошелёк →":"войти →",224,22,78,20,12,Muted,false,TextAnchor.MiddleRight);

            // 3. Right-Side Vertical Menu Backdrop
            bool hasSave=game!=null&&game.HasSave;
            var menuFrame=Box(content.transform,1024,110,520,572,new Color(.055f,.155f,.252f,.76f));
            menuFrame.raycastTarget=false;
            var mfBorder=Rect("Menu border",menuFrame.transform,0,0,0,0);Stretch(mfBorder);
            var mfImg=mfBorder.gameObject.AddComponent<Image>();mfImg.sprite=MenuArtwork.Outline;mfImg.type=Image.Type.Sliced;mfImg.raycastTarget=false;mfImg.color=new Color(Accent.r,Accent.g,Accent.b,.42f);
            Box(menuFrame.transform,0,20,4,532,Accent,false);

            // 4. Directly ABOVE all buttons: Avatar + Nickname Profile Card!
            var profileCard=Button("",1044,126,480,106,ShowAccount);
            profileCard.name="Home.Account";
            AvatarPortrait(profileCard.transform,heroAvatar,displayNick,14,13,80);
            var dot=Box(profileCard.transform,80,79,14,14,logged?SpringAccent:Accent);
            dot.raycastTarget=false;
            Label(profileCard.transform,"@"+displayNick,110,13,320,32,23,White,true,TextAnchor.MiddleLeft);
            string roleLine=logged
                ?("Уровень "+a.Profile.level+"  ·  "+(string.IsNullOrEmpty(a.Profile.titleName)?"Странник":a.Profile.titleName))
                :(ProfileAvatar.TitleOf(heroAvatar)+"  ·  локальный профиль");
            Label(profileCard.transform,roleLine,110,47,334,22,14,Accent,false,TextAnchor.MiddleLeft);
            string bioLine=logged&&!string.IsNullOrEmpty(a.Profile.bio)
                ?a.Profile.bio
                :(logged?"Открыть профиль, аватар и 200 достижений  →":"Настроить аватар, никнейм или войти в аккаунт  →");
            Label(profileCard.transform,bioLine,110,71,350,20,12,Muted,false,TextAnchor.MiddleLeft);

            Box(content.transform,1048,242,472,1,new Color(1f,1f,1f,.16f),false);

            // 5. The 4 Requested Main Menu Buttons directly below Avatar + Nickname!
            string singleSub=hasSave
                ?"Продолжить сохранённый мир или начать новую экспедицию"
                :"Выживание, сюжет, живые города и парящие острова";
            string friendsSub="Профили друзей, поиск по @нику и совместная игра";
            if(logged&&a.Friends!=null)
            {
                int fTotal=a.Friends.friends!=null?a.Friends.friends.Length:0;
                int fOnline=0;if(a.Friends.friends!=null)foreach(var f in a.Friends.friends)if(f!=null&&f.online)fOnline++;
                int fInc=a.Friends.incoming!=null?a.Friends.incoming.Length:0;
                friendsSub=fInc>0
                    ?("В сети: "+fOnline+" из "+fTotal+"  ·  Новых заявок: "+fInc)
                    :("Друзей: "+fTotal+" (в сети: "+fOnline+")  ·  открыть список");
            }

            HomeAction("Одиночная игра",singleSub,254,ShowWorld,true,"Home.Play");
            HomeAction("Многопользовательская игра","Сервера игроков и кооператив до 8 человек",340,ShowRooms,false,"Home.Coop");
            HomeAction("Настройки","Графика, дальность прорисовки, звук и управление",426,()=>ShowSettings(Page.Home),false,"Home.Settings");
            HomeAction("Друзья",friendsSub,512,OpenFriendsHub,false,"Home.Friends");

            if(hasSave)
            {
                var cont=Button("Продолжить мир  →",1044,606,286,52,game.ContinueOffline);cont.name="Home.Continue";
                var quit=Button("Выход",1342,606,182,52,()=>Application.Quit());quit.name="Home.Quit";
            }
            else
            {
                var quit=Button("Выход из игры",1044,606,480,52,()=>Application.Quit());quit.name="Home.Quit";
            }
        }
        void HomeAction(string title,string subtitle,float y,Action action,bool primary,string objectName)
        {
            var button=Button("",1044,y,480,76,action,primary);button.name=objectName;
            Label(button.transform,title,22,13,404,28,19,primary?Ink:White,true);
            Label(button.transform,subtitle,22,44,404,20,12,primary?new Color(.22f,.15f,.03f):Muted);
            Label(button.transform,"→",434,0,34,76,22,primary?Ink:Accent,false,TextAnchor.MiddleCenter);
        }
        public void ShowWorld()
        {
            Clear(Page.World);Header("Одиночная игра");
            Label(content.transform,"ВЫБЕРИ МИР И РЕЖИМ",64,146,900,54,34,White,true);
            Label(content.transform,"Слева — сохранённые миры этого ПК, справа — режим новой экспедиции на Лестингии.",66,202,1240,30,17,Muted);
            bool hasSave=game!=null&&game.HasSave;

            // Saved worlds live in their own left-hand list with the actions on the bottom row,
            // the way the mode screen is expected to read (player report on the old layout).
            var worldsCard=Box(content.transform,64,248,700,424,Panel);
            Label(worldsCard.transform,"МИРЫ",24,18,652,28,17,Accent,true);
            Box(worldsCard.transform,24,52,652,1,new Color(.8f,.9f,.8f,.18f),false);
            if(hasSave)
            {
                var row=Box(worldsCard.transform,24,66,652,106,new Color(.10f,.235f,.335f,.66f));
                Label(row.transform,"Лестингия · SEED "+(game!=null?game.WorldSeed:""),18,14,616,28,20,White,true);
                Label(row.transform,"Локальный мир · персонаж, постройки и прогресс",18,46,616,24,13,Muted);
                Label(row.transform,"Сохранён · готов к продолжению",18,72,616,24,13,SpringAccent);
                var resumeBtn=Button("Продолжить мир  →",24,190,652,58,game.ContinueOffline,true,worldsCard.transform);
                resumeBtn.name="World.Continue";
                Label(worldsCard.transform,"Продолжение идёт с последнего автосохранения.",26,258,648,26,13,Muted);
            }
            else
            {
                Label(worldsCard.transform,"Сохранённых миров пока нет",24,70,652,30,19,White,true);
                Label(worldsCard.transform,"Создайте мир справа — персонаж, постройки и прогресс будут храниться на этом ПК.",24,106,652,62,14,Muted);
            }
            Label(worldsCard.transform,"Новый мир заменяет предыдущий локальный. Открыть друзьям — кнопкой LAN внизу.",26,340,648,54,13,Muted);

            // Mode of the next expedition (right).
            var modeCard=Box(content.transform,792,248,744,424,Panel);
            Label(modeCard.transform,"РЕЖИМ НОВОГО МИРА",24,18,696,28,17,Accent,true);
            Box(modeCard.transform,24,52,696,1,new Color(.8f,.9f,.8f,.18f),false);
            for(int i=0;i<4;i++)
            {
                int mode=i;bool chosen=game.SelectedMode==i;
                var button=Button("",24+(i%2)*352,66+(i/2)*116,344,104,()=>{game.SelectedMode=mode;ShowWorld();},chosen,modeCard.transform);
                Label(button.transform,"0"+(i+1)+"   "+GameRules.ModeNames[i],14,10,316,26,17,chosen?Ink:White,true);
                Label(button.transform,GameRules.ModeDescriptions[i],14,40,316,58,12,chosen?Ink:Muted);
            }
            Label(modeCard.transform,"Seed создаётся автоматически. Старт всегда в Лестингии.",26,306,696,28,13,Muted);

            // Bottom action row.
            var newBtn=Button("▶  Создать мир",64,700,420,64,game.PlayOffline,true);
            newBtn.name="World.New";
            var lanBtn=Button("📡  Открыть мир по LAN",500,700,420,64,()=>{
                if(game.HasSave)game.ContinueOffline();else game.PlayOffline();
                game.OpenLocalWorldToLan("Мир "+game.PlayerName);
            });
            lanBtn.name="World.OpenLan";
            Button("Настройки",936,700,300,64,()=>ShowSettings(Page.World));
            Button("←  Назад",1252,700,284,64,ShowHome);
            status=Label(content.transform,"LAN открывает текущий мир друзьям в одной сети Wi-Fi / LAN. Единственный локальный профиль — один мир за раз.",64,780,1220,60,14,Muted);
        }
        int lobbyRevision=-1;
        public void ShowLobby()
        {
            Clear(Page.Lobby);lobbyRevision=game.LobbyRevision;Header("Сервер · "+game.ModeName);
            Label(content.transform,"МИР ГОТОВ К ЗАХОДУ",64,160,1000,48,30,White,true);
            LobbyAddressCard(content.transform,64,222,1472);
            string names="";int count=0;foreach(var peer in game.LobbyPlayers){names+=peer.name+(peer.id==game.HostId?" · хозяин":"")+"\n";count++;}
            if(count==0)names="Пока только вы.\nПозовите друзей: Мультиплеер → «Быстрый вход» → "+(game.IsHostingLan?game.ActiveLanAddress:game.EndpointAddress);
            Label(content.transform,names,64,382,1000,320,24,White);
            if(game.IsHost)Button("Начать игру  →",1120,392,416,68,game.StartLobby,true);
            else Label(content.transform,"Ждём, когда хозяин начнёт игру",1120,398,416,70,22,Accent);
            var invite=Button("Скопировать приглашение",1120,486,416,58,()=>GUIUtility.systemCopyBuffer=game.InviteText,true);invite.name="Lobby.Invite";
            Label(content.transform,"Скопирует текст с адресом и кодом — можно сразу отправить другу.",1120,548,416,54,13,Muted);
            Button("Выйти",1120,612,416,58,game.ReturnToMenu);
            status=Label(content.transform,"Друзьям нужен только этот адрес или код: мир уже открыт для входа.",64,806,1000,32,15,Muted);
            if(game.IsHost&&game.AutoStartLobby&&!game.IsConnecting)game.StartLobby();
        }
        public void ShowPause()
        {
            Clear(Page.Pause);Header("Пауза");
            Label(content.transform,"МИР ПОДОЖДЁТ",64,174,680,68,42,White,true);
            string netDesc=game.IsHostingLan
                ?("📡 LAN-сервер активен: "+game.ActiveLanAddress+" · друзья могут войти по Wi-Fi / LAN")
                :(game.Online?"Комната "+game.RoomCode+" · сетевой мир не останавливается":"Локальная экспедиция · можно открыть по LAN или в облако");
            Label(content.transform,game.WorldLabel+"\n"+netDesc,67,248,680,76,19,Muted);

            // Quick In-Game Marketplace & Blueprints Banner on Left
            var marketBtn=Button("⚖ Маркетплейс, чертежи построек [B], ритуалы и контракты  →",64,340,680,58,ShowMarket,true);
            marketBtn.name="Pause.Market";
            Label(content.transform,"Клавиши: [M] Миникарта · [N] Метка · [B] Чертёж · [Q] Бросить в колодец · [2×Пробел] Полёт · Чат: /gamemode, /fly, /paradise, /hell, /backrooms",
                66,404,680,48,13,Muted);

            if(!game.Online&&game.Session)
            {
                var curGm=game.Session.Mode;
                Button("Выживание",64,460,132,48,()=>{game.QuickSetGameMode(GameMode.Survival);ShowPause();},curGm==GameMode.Survival);
                Button("Творческий",202,460,138,48,()=>{game.QuickSetGameMode(GameMode.Creative);ShowPause();},curGm==GameMode.Creative);
                Button("Хардкор",346,460,120,48,()=>{game.QuickSetGameMode(GameMode.Hardcore);ShowPause();},curGm==GameMode.Hardcore);
                Button("Сюжет",472,460,110,48,()=>{game.QuickSetGameMode(GameMode.Story);ShowPause();},curGm==GameMode.Story);
                Button(game.CreativeFlightActive?"🕊 Полёт: ВКЛ":"🕊 Полёт",588,460,156,48,()=>{
                    if(!game.Session.Creative)game.QuickSetGameMode(GameMode.Creative);
                    game.QuickToggleFlight();
                    ShowPause();
                },game.CreativeFlightActive);
            }

            var storyCard=Box(content.transform,776,164,760,324,Panel);
            Label(storyCard.transform,"СЮЖЕТ И БОРТОВОЙ ЖУРНАЛ",28,20,704,34,21,Accent,true);
            Label(storyCard.transform,game.PauseStorySummary,28,62,704,174,16,White);
            Button("Открыть все 3 главы сюжета  →",28,248,704,54,ShowStoryJournal,true,storyCard.transform);

            Button("Продолжить  →",64,534,395,64,game.Resume,true);
            Button("Сюжет и журнал",505,534,420,64,ShowStoryJournal);
            Button("Профиль и достижения",505,698,420,58,ShowAccount);
            Button("Настройки",64,619,395,58,()=>ShowSettings(Page.Pause));
            Button("В главное меню",64,698,395,58,game.ReturnToMenu);

            if(game.Online)
            {
                string copyAddr=game.IsHostingLan?game.ActiveLanAddress:game.EndpointAddress;
                Button("Скопировать IP / адрес ("+copyAddr+")",505,619,420,58,()=>GUIUtility.systemCopyBuffer=copyAddr);
                if(game.IsHostingLan)
                {
                    var lanCard=Box(content.transform,952,534,584,222,Panel);
                    Label(lanCard.transform,"📡 ВАШ ЛОКАЛЬНЫЙ МИР ОТКРЫТ В СЕТИ!",24,18,536,28,17,SpringAccent,true);
                    Label(lanCard.transform,"Адрес для друзей: "+game.ActiveLanAddress+"\nМир виден автоматически в «Многопользовательская игра» у всех в одной сети Wi-Fi / LAN / VPN.",24,52,536,88,14,White);
                    Button("Скопировать приглашение",24,150,336,52,()=>GUIUtility.systemCopyBuffer=game.InviteText,true,lanCard.transform);
                    Button("Стоп LAN",374,150,186,52,()=>{game.StopLocalLanServer();ShowPause();},false,lanCard.transform);
                }
                else
                {
                    Label(content.transform,"ESC — продолжить · E — инвентарь · B — чертёж · M — миникарта · Шаттл: G — цель, J — прыжок",952,535,584,84,15,Muted);
                }
            }
            else
            {
                Button("Друзья и команда",505,619,420,58,OpenFriendsHub);
                var hostLan=Button("📡 Открыть мир по LAN (Wi-Fi / Сеть)",952,534,584,64,()=>{game.OpenLocalWorldToLan("Мир "+game.PlayerName);},true);
                hostLan.name="Pause.HostLan";
                var hostServer=Button("☁ Опубликовать на облачном сервере",952,612,584,58,()=>game.OpenWorldAsServer("Мир "+game.PlayerName),false);
                hostServer.name="Pause.HostServer";
                Label(content.transform,"LAN открывает текущий мир мгновенно на вашем ПК ("+game.ActiveLanAddress+"), а Облако публикует на "+game.EndpointAddress+". Все постройки сохранятся!",
                    952,680,584,76,13,Muted);
            }
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
                var box=Box(card.transform,32+col*710,88+row*168,690,156,new Color(.065f,.16f,.25f,.92f));
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
            Box(content.transform,16,119,1248,688,new Color(.055f,.140f,.220f,.93f));
            Header(station==16?"Верстак · 3×3":station==17?"Печь · личная готовка":"Инвентарь · 2×2",false);
            inventoryViews.Clear();craftRevision=-1;
            if(game.Session&&game.Session.Creative)Button("Каталог предметов",424,28,244,52,ShowCreativeCatalog);
            if(!game.Session)return;
            string[] slotNames={"Голова","Одежда","Штаны","Обувь","Шлем","Нагрудник","Поножи","Сапоги"};
            for(int i=0;i<Equipment.Count;i++)
            {float x=32+i*96;Label(content.transform,slotNames[i],x,145,92,40,14,Muted);CreateCell(58+i,x,197,62,62);}
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
            Input("",recipeSearch,"Поиск рецепта…",944,344,304,v=>{recipeSearch=v;RefreshRecipeBook();});
            Button(recipeFitsOnly?"Показывать: помещается":"Показывать: все рецепты",944,446,304,36,()=>{recipeFitsOnly=!recipeFitsOnly;ShowInventory(stationKind,stationCell);},recipeFitsOnly);
            var viewport=Rect("Recipe viewport",content.transform,944,490,304,271);viewport.gameObject.AddComponent<RectMask2D>();
            var backing=viewport.gameObject.AddComponent<Image>();backing.color=new Color(.06f,.10f,.11f,.95f);
            recipeList=Rect("Recipes",viewport,0,0,304,0);
            recipeScroll=viewport.gameObject.AddComponent<ScrollRect>();recipeScroll.viewport=viewport;recipeScroll.content=recipeList;recipeScroll.horizontal=false;recipeScroll.movementType=ScrollRect.MovementType.Clamped;recipeScroll.scrollSensitivity=35;
            Label(content.transform,"Колёсико — прокрутка",944,777,304,27,16,Muted);
            RefreshRecipeBook();
        }
        void RefreshRecipeBook()
        {
            if(!recipeList||page!=Page.Inventory)return;
            for(int i=recipeList.childCount-1;i>=0;i--){var child=recipeList.GetChild(i).gameObject;child.SetActive(false);if(Application.isPlaying)Destroy(child);else DestroyImmediate(child);}
            bool cooking=stationKind==17;string query=(recipeSearch??"").Trim();int row=0;
            if(cooking)
            {
                foreach(int id in ItemArtwork.AllIds)
                {
                    int output=FurnaceState.Result(id);if(output<=0)continue;
                    string name=Items.Get(output).Name;if(query.Length>0&&name.IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                    var entry=Box(recipeList,0,row*67,298,62,Panel);Icon(entry.transform,id,3,6,46);Label(entry.transform,"→",50,15,30,40,20,Accent);Icon(entry.transform,output,78,6,46);
                    Label(entry.transform,name,135,4,158,32,16,White);Label(entry.transform,FurnaceState.CookSeconds+" с",135,34,158,24,15,Muted);row++;
                }
            }
            else
            {
                for(int i=0;i<CraftingTable.All.Length;i++)
                {
                    int index=i;var r=CraftingTable.All[i];string name=Items.Get(r.Output).Name;
                    if(query.Length>0&&name.IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                    if(recipeFitsOnly&&!r.Fits(craftWidth))continue;
                    var button=Button("",0,row*62,298,57,()=>{game.Session.FillRecipe(index,craftWidth);RefreshCraftInventory();},false,recipeList);
                    Icon(button.transform,r.Output,3,3,49);Label(button.transform,name+(r.Fits(craftWidth)?"":" · 3×3"),58,6,234,49,17,r.Fits(craftWidth)?White:Muted);row++;
                }
            }
            recipeList.sizeDelta=new Vector2(304,Mathf.Max(1,row*67));
            if(recipeScroll)recipeScroll.verticalNormalizedPosition=1;
            if(row==0)Label(recipeList,"Ничего не найдено.\nИзмените запрос или фильтр.",12,20,280,80,17,Muted,false,TextAnchor.MiddleCenter);
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
                if(worn){float fraction=Mathf.Clamp01((float)stack.durability/max);view.Wear.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,view.WearWidth*fraction);view.Wear.color=Color.Lerp(new Color(.95f,.25f,.12f),Accent,fraction);}
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
                var panel=Box(frame,0,0,360,64,new Color(.025f,.045f,.05f,.98f),false);tooltip=panel.rectTransform;
                tooltip.name="Item name tooltip";tooltipText=Label(tooltip,"",16,8,328,48,17,White,false,TextAnchor.MiddleLeft);
            }
            var definition=Items.Get(item);tooltipText.text=definition.Name;
            if(definition.Durability>0&&durability>=0)tooltipText.text+="\nПрочность: "+durability+" / "+definition.Durability;
            if(Equipment.ItemProtection(item)>0)tooltipText.text+=" · защита "+Mathf.RoundToInt(Equipment.ItemProtection(item)*100)+"%";
            if(definition.Insulation>0)tooltipText.text+="\nУтепление: +"+definition.Insulation.ToString("0")+"°";
            if(Equipment.Slot(item)>=0)tooltipText.text+="\nShift + клик — надеть / снять";
            int lines=1;foreach(char ch in tooltipText.text)if(ch=='\n')lines++;
            float height=lines*24+18;tooltip.sizeDelta=new Vector2(360,height);tooltipText.rectTransform.sizeDelta=new Vector2(328,height-16);
            tooltip.gameObject.SetActive(true);tooltip.SetAsLastSibling();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,screenPosition,null,out var local);
            tooltip.anchoredPosition=new Vector2(Mathf.Clamp(local.x-frame.rect.xMin+18,8,frame.rect.width-368),-Mathf.Clamp(frame.rect.yMax-local.y+20,8,frame.rect.height-height-8));
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
            Label(content.transform,"Единая шкала качества: от мобильного до кино. Пресеты не понижаются автоматически по FPS; дальность и масштаб можно настроить вручную.",66,247,1400,42,19,Muted);
            var settings=VisualSettings.Current;
            Box(content.transform,64,323,713,402,Panel);Box(content.transform,800,323,736,402,Panel);
            Slider("Дальность мира",64+28,354,655,settings.ViewDistance,2,VisualSettings.IsMobile?4:5,true,v=>{settings.ViewDistance=Mathf.RoundToInt(v);settings.Save();},v=>(int)v+" чанка/сторону");
            Slider("Масштаб рендера",92,469,655,settings.RenderScale,VisualSettings.IsMobile?.65f:.85f,VisualSettings.IsMobile?1f:1.5f,false,v=>{settings.RenderScale=v;settings.Save();},v=>Mathf.RoundToInt(v*100)+"%");
            Button("Тени: "+settings.ShadowResolution.ToString(),92,589,309,58,()=>{settings.ShadowResolution=VisualSettings.IsMobile?(settings.ShadowResolution==512?1024:settings.ShadowResolution==1024?2048:512):(settings.ShadowResolution==1024?2048:settings.ShadowResolution==2048?4096:1024);settings.Save();ShowSettings(settingsBack);});
            Button("Постэффекты: "+(settings.PostEffects?"вкл.":"выкл."),419,589,328,58,()=>{settings.PostEffects=!settings.PostEffects;settings.Save();ShowSettings(settingsBack);});
            Button("Объёмы: "+(settings.VolumetricQuality==2?"Ultra":settings.VolumetricQuality==1?"высокие":"сокращённые"),92,667,315,38,()=>{settings.VolumetricQuality=(settings.VolumetricQuality+1)%3;settings.Save();ShowSettings(settingsBack);});
            Button("SSAO: "+(settings.AmbientOcclusion?"вкл.":"выкл."),424,667,323,38,()=>{settings.AmbientOcclusion=!settings.AmbientOcclusion;settings.Save();ShowSettings(settingsBack);});
            Slider("Поле зрения",828,342,680,settings.FieldOfView,60,100,true,v=>{settings.FieldOfView=v;settings.Save();},v=>(int)v+"°");
            Slider("Чувствительность мыши",828,432,680,settings.Sensitivity,.04f,.25f,false,v=>{settings.Sensitivity=v;settings.Save();},v=>v.ToString("0.00"));
            Slider("Звуки",828,522,326,settings.SfxVolume,0f,1f,false,v=>{settings.SfxVolume=v;settings.Save();},v=>Mathf.RoundToInt(v*100)+"%");
            Slider("Атмосфера",1182,522,326,settings.AmbientVolume,0f,1f,false,v=>{settings.AmbientVolume=v;settings.Save();},v=>Mathf.RoundToInt(v*100)+"%");
            Label(content.transform,"WASD — движение   SPACE — прыжок   C / CTRL — красться (защита от края)\nЛКМ — ломать/атака   ПКМ — поставить   1–9 — слоты   E — инвентарь",828,622,680,92,16,White);
            Button("←  Готово",64,798,210,54,()=>{settings.Save();if(settingsBack==Page.Pause)ShowPause();else ShowHome();},true);
            int[] presets={VisualQualityPolicy.Mobile,VisualQualityPolicy.Balanced,VisualQualityPolicy.High,VisualQualityPolicy.Ultra,VisualQualityPolicy.Cinematic};
            for(int i=0;i<presets.Length;i++)
            {
                int preset=presets[i];bool allowed=!VisualSettings.IsMobile||preset<=VisualQualityPolicy.Ultra;
                var presetButton=Button(VisualQualityPolicy.DisplayName(preset)+(settings.Preset==preset?" ✓":""),296+i*230,798,220,54,()=>{settings.ApplyPreset(preset);ShowSettings(settingsBack);},settings.Preset==preset);
                presetButton.interactable=allowed;presetButton.name="Settings.Preset."+preset;
                var presetText=presetButton.GetComponentInChildren<Text>();if(presetText)presetText.fontSize=16;
            }
            Label(content.transform,"URP · без аппаратной трассировки лучей. Кино-пресет доступен на ПК.",810,730,726,50,15,Muted,false,TextAnchor.MiddleRight);
        }
        void Slider(string title,float x,float y,float w,float value,float min,float max,bool integer,Action<float> changed,Func<float,string> format)
        {
            // The title and the value used to share the same pixels on the narrow sliders
            // ("Звуки 80%" ran into the next caption); each now owns its own box.
            Label(content.transform,title,x,y,w-125,30,18,White);var output=Label(content.transform,format(value),x+w-112,y,112,30,15,Accent,false,TextAnchor.MiddleRight);
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
            var backdrop=Box(content.transform,271,797,738,92,new Color(.055f,.145f,.225f,.80f));
            for(int i=0;i<9;i++)
            {
                var image=Box(backdrop.transform,10+i*80,10,76,72,new Color(.11f,.23f,.33f,.88f));slots[i]=image;slotRects[i]=image.rectTransform;
                image.raycastTarget=true;AttachHover(image.gameObject,0);slotHovers[i]=image.GetComponent<ItemHover>();
                var raw=Rect("Item icon",image.transform,14,2,48,48).gameObject.AddComponent<RawImage>();raw.raycastTarget=false;slotIcons[i]=raw;
                slotCounts[i]=Label(image.transform,"",45,50,27,22,15,White,false,TextAnchor.MiddleRight);
                Label(image.transform,(i+1).ToString(),6,50,24,22,12,Muted,false,TextAnchor.MiddleLeft);
                var frame=Rect("Selected frame",image.transform,0,0,0,0);Stretch(frame);
                var frameImage=frame.gameObject.AddComponent<Image>();frameImage.sprite=MenuArtwork.Outline;frameImage.type=Image.Type.Sliced;frameImage.raycastTarget=false;frameImage.color=new Color(Accent.r,Accent.g,Accent.b,0);slotFrames[i]=frameImage;
            }
            // Item names are hover/focus tooltips, never permanent slot text.

            // Vitals panel: HP silhouette + vitality bars (O₂ is suit-only, off-world).
            var healthPanel=Box(content.transform,24,616,252,178,new Color(.055f,.145f,.225f,.82f));
            healthHUD=Label(healthPanel.transform,"HP · 100 / 100",14,8,150,28,17,White);
            foodHUD=Label(healthPanel.transform,"",128,10,110,24,12,Muted,false,TextAnchor.MiddleRight);
            var body=Rect("Health bar",healthPanel.transform,14,40,224,10);healthBody=body.gameObject.AddComponent<HealthSilhouette>();healthBody.Build();
            RectTransform VitalsRow(string label,Color tint,out Image bar)
            {
                var row=Rect(label+" row",healthPanel.transform,0,56,252,23);
                Label(row,label,14,1,52,20,12,White);
                Box(row,72,8,166,8,new Color(.09f,.18f,.26f,.92f),false);
                bar=Box(row,72,8,166,8,tint,false);
                return row;
            }
            oxygenRow=VitalsRow("O₂",new Color(.38f,.80f,.98f),out oxygenBar);
            staminaRow=VitalsRow("Силы",new Color(.38f,.90f,.48f),out staminaBar);
            thirstRow=VitalsRow("Жажда",new Color(.36f,.72f,.98f),out thirstBar);
            temperatureRow=VitalsRow("Темп",new Color(.98f,.74f,.32f),out temperatureBar);
            tempHUD=Label(temperatureRow,"37.0°C",128,1,110,20,12,Muted,false,TextAnchor.MiddleRight);
            vitalsRows=new[]{oxygenRow,staminaRow,thirstRow,temperatureRow};
            vitalsHUD=Label(content.transform,"",24,540,430,68,12,Muted);
            // Objectives panel, top-right: living checklist driven by the current expedition
            // state. Checkbox squares are plain images (font-safe, square-corner HUD language).
            var objectivePanel=Box(content.transform,982,24,274,178,new Color(.055f,.145f,.225f,.76f));
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

            // Live Voxel Minimap Panel [M], aligned beside Objectives (824, 24, 148, 178)
            var mmBox=Box(content.transform,824,24,148,178,new Color(.045f,.125f,.205f,.82f));
            minimapPanel=mmBox.rectTransform;
            Label(minimapPanel,"КАРТА [M]",10,6,128,18,11,Accent,true,TextAnchor.MiddleCenter);
            if(!minimapTex)
            {
                minimapTex=new Texture2D(64,64,TextureFormat.RGBA32,false);
                minimapTex.filterMode=FilterMode.Point;
                minimapTex.wrapMode=TextureWrapMode.Clamp;
            }
            minimapRaw=Rect("Minimap Texture",minimapPanel,10,26,128,124).gameObject.AddComponent<RawImage>();
            minimapRaw.texture=minimapTex;minimapRaw.raycastTarget=false;
            minimapCoords=Label(minimapPanel,"",6,152,136,22,10,Muted,false,TextAnchor.MiddleCenter);
            minimapPanel.gameObject.SetActive(game&&game.MinimapVisible);
            minimapTimer=99f;

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
            if(page!=Page.HUD)SyncBackdropVisibility();
            if(page==Page.Home&&splashRect)
            {
                float s=1f+.038f*Mathf.Sin(Time.unscaledTime*3.4f);
                splashRect.localScale=new Vector3(s,s,1f);
                splashRect.localRotation=Quaternion.Euler(0f,0f,Mathf.Sin(Time.unscaledTime*1.7f)*2.2f);
            }
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
                    healthBody.SetBody(v);
                    healthHUD.text=session.Creative
                        ?(game.CreativeFlightActive?"🕊 ТВОРЧЕСКИЙ · ПОЛЁТ":"ТВОРЧЕСКИЙ [2×Пробел — полёт]")
                        :$"HP · {v.health:0} / 100";
                    string extraTech="";
                    if(game.Tech)
                    {
                        var sim=game.Tech.Sim;
                        if(sim.CurrentMillisievertsPerHour>8f||sim.RadiationDose01>0.03f||session.Inventory.Active.item==Items.Dosimeter)
                            extraTech+=$"\n☢ Фон  {sim.CurrentMillisievertsPerHour:0} мЗв/ч ({sim.RadiationDose01*100f:0}%)";
                        if(sim.Grid.GenerationKw>0.1f||sim.Grid.DemandKw>0.1f)
                            extraTech+=$"\n⚡ Сеть  {sim.Grid.GenerationKw:0}/{sim.Grid.DemandKw:0} кВт · {sim.Grid.StoredKwh:0} кВт·ч";
                    }
                    bool offWorld=session.World&&session.World.Dimension!=Dimension.Lestingia;
                    float rowY=56;
                    foreach(var row in vitalsRows)
                    {
                        bool show=row!=oxygenRow||offWorld;
                        row.gameObject.SetActive(show);
                        if(show){row.anchoredPosition=new Vector2(0,-rowY);rowY+=23;}
                    }
                    if(oxygenBar)oxygenBar.rectTransform.sizeDelta=new Vector2(166*Mathf.Clamp01(v.air/100),8);
                    if(staminaBar)staminaBar.rectTransform.sizeDelta=new Vector2(166*Mathf.Clamp01(v.stamina/100),8);
                    if(thirstBar)thirstBar.rectTransform.sizeDelta=new Vector2(166*Mathf.Clamp01(v.thirst/100),8);
                    if(temperatureBar)
                    {
                        temperatureBar.rectTransform.sizeDelta=new Vector2(166*Mathf.Clamp01((v.temperature-30)/13f),8);
                        temperatureBar.color=Color.Lerp(new Color(.40f,.62f,.95f),new Color(.95f,.50f,.30f),Mathf.Clamp01((v.temperature-33)/7f));
                    }
                    if(foodHUD)foodHUD.text=$"Еда {v.hunger:0}%";
                    if(tempHUD)tempHUD.text=$"{v.temperature:0.0}°C";
                    vitalsHUD.text=extraTech.Length>0?extraTech.Substring(1):"";
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
                else
                {
                    loadingHUD.text=game.CurrentRealmBannerText();
                }
            }
            if(minimapPanel&&game&&game.Session&&game.Session.World)
            {
                bool showMap=game.MinimapVisible;
                if(minimapPanel.gameObject.activeSelf!=showMap)minimapPanel.gameObject.SetActive(showMap);
                if(showMap)
                {
                    minimapTimer+=Time.unscaledDeltaTime;
                    if(minimapTimer>.45f)
                    {
                        minimapTimer=0f;
                        var w=game.Session.World;
                        var ppos=game.PlayerWorldPos;
                        RealmFeaturesAndMarket.RenderMinimap(minimapTex,w.Seed,w.Dimension,ppos,game.PlayerYaw,RealmFeaturesAndMarket.LoadWaypoints(w.Seed));
                        if(minimapCoords)minimapCoords.text=$"{Mathf.RoundToInt(ppos.x)}, {Mathf.RoundToInt(ppos.y)}, {Mathf.RoundToInt(ppos.z)}  ·  {game.CurrentMinimapTag()}";
                    }
                }
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
                    slots[i].color=chosen?new Color(.26f,.36f,.22f,.96f):new Color(.11f,.23f,.33f,.88f);
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
        void OnDestroy(){if(rounded)Destroy(rounded);if(minimapTex)Destroy(minimapTex);}
        int achievementPage;bool achievementOnlyEarned;
        int authTab; // 0=login, 1=register, 2=recover
        int accountRightTab; // 0=showcase & stats, 1=security
        string draftBio="",draftLocalNick="",localProfileNotice="";
        void RefreshAccountIfOpen(){if(page==Page.Account)ShowAccount();}
        public void ShowAccount()
        {
            Clear(Page.Account);Header("Профиль игрока",false);
            var a=game?game.Accounts:null;
            var backBtn=Button("← Назад",64,804,210,50,()=>{if(game&&game.InWorld)ShowPause();else ShowHome();});
            backBtn.name="Account.Back";

            if(a==null||!a.Logged)
            {
                if(string.IsNullOrEmpty(draftLocalNick))draftLocalNick=ProfileAvatar.LocalNickname;
                if(string.IsNullOrEmpty(draftBio))draftBio=ProfileAvatar.LocalBio;
                string curLocalAv=ProfileAvatar.LocalAvatar;

                // Left Card: Local Customizable Hero Profile (works even offline!)
                var left=Box(content.transform,64,142,664,638,Panel);
                Label(left.transform,"ЛОКАЛЬНЫЙ ПРОФИЛЬ ГЕРОЯ",24,18,500,24,14,Accent,true);
                AvatarPortrait(left.transform,curLocalAv,draftLocalNick,24,52,108);
                Label(left.transform,"@"+ProfileAvatar.LocalNickname,150,56,480,34,26,White,true);
                Label(left.transform,ProfileAvatar.TitleOf(curLocalAv)+"  ·  Одиночная и LAN-игра",150,94,480,24,15,Accent);
                Label(left.transform,string.IsNullOrEmpty(ProfileAvatar.LocalBio)?"Добавьте девиз героя ниже":ProfileAvatar.LocalBio,150,122,490,38,13,Muted);

                Label(left.transform,"ВЫБЕРИТЕ АВАТАР (12 ГЕРОЕВ)",24,178,600,22,13,Accent,true);
                for(int i=0;i<ProfileAvatar.Catalog.Length;i++)
                {
                    var arch=ProfileAvatar.Catalog[i];
                    bool selected=arch.Id==curLocalAv;
                    float sx=24+(i%6)*104,sy=206+(i/6)*82;
                    var sw=Button("",sx,sy,94,72,()=>{ProfileAvatar.LocalAvatar=arch.Id;localProfileNotice="Выбран аватар: "+arch.Title;ShowAccount();},selected,left.transform);
                    AvatarPortrait(sw.transform,arch.Id,null,21,6,52);
                }

                InputIn(left.transform,"ВАШ НИКНЕЙМ (3–24 латинских символа)",draftLocalNick,"Explorer_01",24,382,616,v=>draftLocalNick=v,24);
                InputIn(left.transform,"ДЕВИЗ ПРОФИЛЯ (до 80 символов)",draftBio,"Исследую парящие острова Лестингии!",24,470,616,v=>draftBio=v,80);
                var saveLocal=Button("Сохранить локальный профиль",24,562,360,52,()=>
                {
                    string cleanNick=ProfileAvatar.SanitizeLocalNick(draftLocalNick);
                    if(string.IsNullOrEmpty(cleanNick)){localProfileNotice="Никнейм: 3–24 латинские буквы, цифры или _.";ShowAccount();return;}
                    ProfileAvatar.LocalNickname=cleanNick;
                    ProfileAvatar.LocalBio=draftBio;
                    if(game)game.PlayerName=cleanNick;
                    localProfileNotice="Профиль обновлён: @"+cleanNick;
                    ShowAccount();
                },true,left.transform);
                saveLocal.name="Account.SaveLocal";
                Button("Скопировать @ник",398,562,242,52,()=>{GUIUtility.systemCopyBuffer=ProfileAvatar.LocalNickname;localProfileNotice="Ник @"+ProfileAvatar.LocalNickname+" скопирован!";ShowAccount();},false,left.transform);

                // Right Card: Clean Tabbed Cloud Sign-In / Registration / Recovery
                var right=Box(content.transform,752,142,784,638,Panel);
                Label(right.transform,"ЕДИНЫЙ АККАУНТ LESTINGCRAFT",28,18,720,26,15,Accent,true);
                Label(right.transform,"Войдите или создайте аккаунт, чтобы добавлять друзей по @нику, открыть кошелёк, скины и синхронизировать 200 достижений.",28,46,728,44,14,Muted);

                string[] tabs={"Вход","Регистрация","Восстановление"};
                for(int t=0;t<3;t++)
                {
                    int tabIdx=t;
                    Button(tabs[t],28+t*246,102,234,46,()=>{authTab=tabIdx;ShowAccount();},authTab==t,right.transform);
                }

                string username=ProfileAvatar.LocalNickname=="Explorer"?"":ProfileAvatar.LocalNickname,password="",recovery="";
                InputIn(right.transform,"ЛОГИН АККАУНТА · 3–24 латинских символа",username,"Explorer_01",28,168,728,v=>username=v,24);
                var pw=InputIn(right.transform,authTab==2?"НОВЫЙ ПАРОЛЬ · минимум 12 символов":"ПАРОЛЬ · минимум 12 символов",password,"Минимум 12 символов",28,258,728,v=>password=v,128);
                pw.contentType=InputField.ContentType.Password;
                InputField rc=null;
                if(authTab==2)
                {
                    rc=InputIn(right.transform,"ЛИЧНЫЙ КОД ВОССТАНОВЛЕНИЯ",recovery,"64-символьный код, выданный при регистрации",28,348,728,v=>recovery=v,128);
                    rc.contentType=InputField.ContentType.Password;
                }

                float actionY=authTab==2?444:362;
                string actionVerb=authTab==0?"login":authTab==1?"register":"recover";
                string actionLabel=authTab==0?"Войти в аккаунт  →":authTab==1?"Создать аккаунт и получить код  →":"Восстановить доступ  →";
                Button(actionLabel,28,actionY,460,56,()=>
                {
                    if(a==null)return;
                    string capturedPw=password,capturedRc=recovery,capturedUser=username;
                    pw.text="";if(rc)rc.text="";
                    a.Authenticate(actionVerb,capturedUser,capturedPw,capturedRc,()=>
                    {
                        if(a.Logged&&a.Profile!=null)
                        {
                            ProfileAvatar.LocalNickname=a.Profile.username;
                            if(game)game.PlayerName=a.Profile.username;
                            draftBio=a.Profile.bio??"";
                        }
                        RefreshAccountIfOpen();
                    });
                },true,right.transform);

                var infoBox=Box(right.transform,28,518,728,96,new Color(.05f,.13f,.21f,.88f));
                Label(infoBox.transform,"Безопасность без почты и спама",18,10,690,22,14,SpringAccent,true);
                Label(infoBox.transform,"При регистрации выдаётся одноразовый код восстановления. Пароль и сессия живут только в оперативной памяти и никогда не сохраняются в открытом виде.",18,34,692,52,13,Muted);

                string msg=!string.IsNullOrEmpty(localProfileNotice)?localProfileNotice:(a!=null?a.Message:"Локальный профиль готов к игре.");
                status=Label(content.transform,msg,300,808,1236,44,15,Accent,false,TextAnchor.MiddleRight);
                return;
            }

            var p=a.Profile;
            if(!string.IsNullOrEmpty(a.RecoveryCode))
            {
                var card=Box(content.transform,180,180,1240,540,Panel);
                Label(card.transform,"СОХРАНИТЕ КОД ВОССТАНОВЛЕНИЯ",40,36,1160,44,28,Accent,true);
                Label(card.transform,"Этот код показывается только один раз и не хранится на устройстве.\nСохраните его в надёжном месте — он заменяет электронную почту при смене пароля.",40,92,1160,64,18,White);
                var codeBox=Box(card.transform,40,182,1160,92,new Color(.04f,.10f,.16f,.96f));
                Label(codeBox.transform,a.RecoveryCode,24,0,1112,92,21,SpringAccent,true,TextAnchor.MiddleCenter);
                Button("Скопировать код в буфер",40,316,460,62,()=>{GUIUtility.systemCopyBuffer=a.RecoveryCode;a.Message="Код восстановления скопирован в буфер обмена.";},false,card.transform);
                Button("Я сохранил код — перейти в профиль  →",528,316,672,62,()=>{a.DismissRecovery();ShowAccount();},true,card.transform);
                status=Label(content.transform,a.Message,300,808,1236,44,15,Accent,false,TextAnchor.MiddleRight);
                return;
            }

            if(a.Wallet==null&&!a.CloudBusy)a.CloudState(()=>{if(page==Page.Account)ShowAccount();});
            if(a.Friends==null&&!a.CloudBusy)a.CloudFriends(()=>{if(page==Page.Account)ShowAccount();});
            if(game)game.PlayerName=p.username;

            string curAvatar=ProfileAvatar.ResolveId(p.avatar,p.username);

            // Left Hero Card: Avatar, @Nickname, Level/XP Bar, 12-Avatar Picker & Bio!
            var hero=Box(content.transform,64,142,684,638,Panel);
            AvatarPortrait(hero.transform,curAvatar,p.username,24,22,116);
            var onlineBadge=Box(hero.transform,122,120,18,18,SpringAccent);onlineBadge.raycastTarget=false;

            Label(hero.transform,"@"+p.username,158,20,330,36,27,White,true);
            Button("Копировать @ник",496,20,164,38,()=>
            {
                GUIUtility.systemCopyBuffer=p.username;
                a.Message="Ник @"+p.username+" скопирован — отправьте его другу!";
                if(status)status.text=a.Message;
            },false,hero.transform);

            string titleName=string.IsNullOrEmpty(p.titleName)?"Странник":p.titleName;
            Label(hero.transform,"Титул: "+titleName+"  ·  "+ProfileAvatar.TitleOf(curAvatar),158,60,330,24,15,Accent);
            Button("Сменить титул ↻",496,64,164,34,()=>
            {
                var titles=new List<string>{""};
                if(a.Achievements!=null&&p.achievements!=null)
                    foreach(var d in a.Achievements)
                        if(!string.IsNullOrEmpty(d.title)&&Array.IndexOf(p.achievements,d.id)>=0)titles.Add(d.id);
                string nextTitle=titles[(titles.IndexOf(p.title??"")+1)%titles.Count];
                a.UpdateProfile(nextTitle,null,null,RefreshAccountIfOpen);
            },false,hero.transform);

            // XP & Level Progress Bar
            int nextXp=AccountClient.XpFloorForLevel(p.level+1);
            float xpFill=AccountClient.XpProgress01(p.xp,p.level);
            Label(hero.transform,"Уровень "+p.level+"   ·   "+p.xp+" / "+nextXp+" XP",158,98,490,22,14,White,true);
            var xpBg=Box(hero.transform,158,124,502,14,new Color(.04f,.10f,.16f,.95f));
            if(xpFill>0.01f)Box(xpBg.transform,0,0,Mathf.Max(12f,502f*xpFill),14,Accent);

            Label(hero.transform,"ВЫБОР АВАТАРА ПРОФИЛЯ",24,160,620,22,13,Accent,true);
            for(int i=0;i<ProfileAvatar.Catalog.Length;i++)
            {
                var arch=ProfileAvatar.Catalog[i];
                bool selected=arch.Id==curAvatar;
                float sx=24+(i%6)*107,sy=188+(i/6)*82;
                var sw=Button("",sx,sy,98,74,()=>
                {
                    ProfileAvatar.LocalAvatar=arch.Id;
                    a.UpdateProfile(null,arch.Id,null,RefreshAccountIfOpen);
                },selected,hero.transform);
                AvatarPortrait(sw.transform,arch.Id,null,23,6,52);
            }

            string initialBio=p.bio??"";
            string currentBioInput=string.IsNullOrEmpty(draftBio)?initialBio:draftBio;
            InputIn(hero.transform,"ДЕВИЗ ИГРОКА (виден друзьям в карточке профиля)",currentBioInput,"Например: Строю замок на парящем острове!",24,366,636,v=>draftBio=v,80);
            Button("Сохранить девиз",24,454,260,46,()=>
            {
                string clean=(draftBio??"").Trim();
                ProfileAvatar.LocalBio=clean;
                a.UpdateProfile(null,null,clean,()=>{draftBio="";RefreshAccountIfOpen();});
            },true,hero.transform);

            // Bottom quick economy & friends strip inside Hero Card
            long balance=a.Wallet!=null?a.Wallet.balance:0;
            int friendCount=a.Friends!=null&&a.Friends.friends!=null?a.Friends.friends.Length:0;
            int incomingCount=a.Friends!=null&&a.Friends.incoming!=null?a.Friends.incoming.Length:0;
            var walletButton=Button("Кошелёк: "+CloudFormat.Coins(balance)+" монет  →",24,564,310,54,ShowStore,true,hero.transform);
            walletButton.name="Account.Store";
            string fBtnText=incomingCount>0?("Друзья ("+friendCount+") · +"+incomingCount+"  →"):("Друзья ("+friendCount+")  →");
            var friendsButton=Button(fBtnText,350,564,310,54,()=>{a.CloudFriends(AfterCloud);ShowFriends();},false,hero.transform);
            friendsButton.name="Account.Friends";

            // Right Panel: Showcase Stats + Achievements OR Account Security
            var showcase=Box(content.transform,772,142,764,638,Panel);
            Button("Витрина и достижения",24,18,350,44,()=>{accountRightTab=0;ShowAccount();},accountRightTab==0,showcase.transform);
            Button("Безопасность и вход",390,18,350,44,()=>{accountRightTab=1;ShowAccount();},accountRightTab==1,showcase.transform);

            if(accountRightTab==0)
            {
                Label(showcase.transform,"СТАТИСТИКА ЭКСПЕДИЦИЙ",24,80,500,24,14,Accent,true);
                string[] sTitles={"ВРЕМЯ В МИРЕ","ДОБЫТО БЛОКОВ","ПОСТРОЕНО","СОЗДАНО","ОХОТА И БОССЫ","ПРОЙДЕНО ПЕШКОМ"};
                string[] sVals={
                    (a.Counter("alive_s")/60)+" мин",
                    CloudFormat.Coins(a.Counter("mined")),
                    CloudFormat.Coins(a.Counter("placed")),
                    CloudFormat.Coins(a.Counter("crafted")),
                    a.Counter("kills")+" / "+a.Counter("bosses"),
                    CloudFormat.Coins(a.Counter("walk_m"))+" м"
                };
                for(int i=0;i<6;i++)
                {
                    float tx=24+(i%3)*242,ty=112+(i/3)*102;
                    var tile=Box(showcase.transform,tx,ty,230,90,new Color(.05f,.13f,.21f,.90f));
                    Label(tile.transform,sTitles[i],16,12,198,20,11,Accent,true);
                    Label(tile.transform,sVals[i],16,38,198,38,24,White,true);
                }

                int earnedCount=p.achievements!=null?p.achievements.Length:0;
                float achRatio=Mathf.Clamp01(earnedCount/200f);
                Label(showcase.transform,"ПРОГРЕСС ДОСТИЖЕНИЙ: "+earnedCount+" / 200",24,332,600,24,14,Accent,true);
                var achBar=Box(showcase.transform,24,360,716,16,new Color(.04f,.10f,.16f,.95f));
                if(achRatio>0f)Box(achBar.transform,0,0,Mathf.Max(12f,716f*achRatio),16,SpringAccent);

                // Show 2 featured achievements (most recent unlocked or next closest targets!)
                if(a.Achievements!=null)
                {
                    int shown=0;
                    for(int i=0;i<a.Achievements.Length&&shown<2;i++)
                    {
                        var d=a.Achievements[i];
                        bool unlocked=p.achievements!=null&&Array.IndexOf(p.achievements,d.id)>=0;
                        if(shown==0&&!unlocked&&earnedCount>0)continue;
                        var ac=Box(showcase.transform,24,392+shown*78,716,68,new Color(.05f,.13f,.21f,.90f));
                        Label(ac.transform,(unlocked?"★ ":"○ ")+d.name,18,8,520,24,16,unlocked?Accent:White,true);
                        Label(ac.transform,d.description,18,34,540,24,12,Muted);
                        Label(ac.transform,"+"+d.xp+" XP",580,20,118,28,15,SpringAccent,true,TextAnchor.MiddleRight);
                        shown++;
                    }
                }

                Button("Все 200 достижений  →",24,564,436,54,()=>{achievementPage=0;ShowAchievements();},true,showcase.transform);
                Button("Синхронизировать",476,564,264,54,()=>a.Sync(RefreshAccountIfOpen),false,showcase.transform);
            }
            else
            {
                Label(showcase.transform,"УПРАВЛЕНИЕ ПАРОЛЕМ И СЕССИЕЙ",24,82,680,24,14,Accent,true);
                string current="",next="";
                var old=InputIn(showcase.transform,"ТЕКУЩИЙ ПАРОЛЬ",current,"Введите текущий пароль",24,118,716,v=>current=v,128);
                old.contentType=InputField.ContentType.Password;
                var fresh=InputIn(showcase.transform,"НОВЫЙ ПАРОЛЬ (минимум 12 символов)",next,"Минимум 12 символов",24,210,716,v=>next=v,128);
                fresh.contentType=InputField.ContentType.Password;

                Button("Изменить пароль",24,308,348,52,()=>{string c=current,n=next;old.text="";fresh.text="";a.ChangePassword(c,n,RefreshAccountIfOpen);},true,showcase.transform);
                Button("Выйти из аккаунта",392,308,348,52,()=>a.Logout(RefreshAccountIfOpen),false,showcase.transform);

                var warnBox=Box(showcase.transform,24,392,716,148,new Color(.18f,.06f,.08f,.78f));
                Label(warnBox.transform,"ОПАСНАЯ ЗОНА · УДАЛЕНИЕ АККАУНТА",20,14,676,22,13,new Color(1f,.46f,.48f),true);
                Label(warnBox.transform,"Удаление необратимо стирает облачный профиль, статистику и достижения.\nДля подтверждения введите текущий пароль выше и нажмите кнопку дважды.",20,42,676,56,13,White);
                bool confirm=false;
                Button("Удалить аккаунт (двойное подтверждение)",20,96,676,42,()=>{if(!confirm){confirm=true;a.Message="Нажмите ещё раз для полного удаления профиля (требуется текущий пароль).";if(status)status.text=a.Message;return;}string c=current;old.text="";a.Delete(c,RefreshAccountIfOpen);},false,warnBox.transform);
            }

            status=Label(content.transform,a.Message,290,808,1246,44,15,Accent,false,TextAnchor.MiddleRight);
        }
        void ShowAchievements()
        {
            Clear(Page.Achievements);Header("Достижения · 200",false);var a=game?game.Accounts:null;if(a==null||!a.Logged){ShowAccount();return;}
            var list=new List<AchievementDefinition>();foreach(var d in a.Achievements)if(!achievementOnlyEarned||Array.IndexOf(a.Profile.achievements,d.id)>=0)list.Add(d);
            int pages=Math.Max(1,(list.Count+7)/8);achievementPage=Math.Max(0,Math.Min(pages-1,achievementPage));
            Label(content.transform,"Открыто: "+a.Profile.achievements.Length+" из 200  ·  Уровень "+a.Profile.level+" ("+a.Profile.xp+" XP)",64,148,900,32,18,Accent,true);
            for(int i=0;i<8;i++){int index=achievementPage*8+i;if(index>=list.Count)break;var d=list[index];bool earned=Array.IndexOf(a.Profile.achievements,d.id)>=0;float x=64+i%2*746,y=196+i/2*142;var card=Box(content.transform,x,y,718,126,Panel);Label(card.transform,(earned?"★ ":"○ ")+d.name+(!string.IsNullOrEmpty(d.title)?"  ·  титул «"+d.title+"»":""),18,10,680,30,18,earned?Accent:White,true);Label(card.transform,d.description,18,44,680,44,14,Muted);Label(card.transform,d.category+" · "+Math.Min(d.target,a.Counter(d.counter))+" / "+d.target+" · +"+d.xp+" XP",18,94,680,22,13,earned?SpringAccent:Muted);}
            Button("← Профиль",64,807,230,52,ShowAccount);Button("←",600,807,90,52,()=>{achievementPage--;ShowAchievements();});Label(content.transform,(achievementPage+1)+" / "+pages,715,821,150,35,21,White,false,TextAnchor.MiddleCenter);Button("→",865,807,90,52,()=>{achievementPage++;ShowAchievements();});Button(achievementOnlyEarned?"Показать все 200":"Только открытые",1110,807,426,52,()=>{achievementOnlyEarned=!achievementOnlyEarned;achievementPage=0;ShowAchievements();});
        }
    }
}
