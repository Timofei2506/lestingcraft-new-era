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
        static readonly Color Ink=new Color(.055f,.086f,.10f), Panel=new Color(.07f,.115f,.13f,.96f), Muted=new Color(.58f,.66f,.66f), White=new Color(.91f,.94f,.90f), Accent=new Color(.65f,.86f,.48f);
        PrototypeGame game;Canvas canvas;RectTransform frame;GameObject content,background;Font body,display;Sprite rounded;
        readonly List<GameObject> transient=new List<GameObject>();readonly List<Button> actions=new List<Button>();
        Page page,settingsBack;Text status,realmHUD,netHUD,perfHUD,itemHUD,loadingHUD;Image[] slots;RectTransform[] slotRects;
        Text vitalsHUD,objectiveHUD,noticeHUD;RawImage[] slotIcons;Text[] slotCounts,slotFallback;int lastInventoryRevision=-1,lastSlot=-1,inventoryFrom=-1;
        public bool InventoryOpen=>page==Page.Inventory;
        float timer,averageFrame=.016f;ushort lastSelection;bool ready;
        public void Initialize(PrototypeGame owner)
        {
            Debug.Log("[Presentation] Loading local UI fonts and sprites");
            game=owner;body=Resources.Load<Font>("UI/Manrope");display=Resources.Load<Font>("UI/Unbounded");
            var texture=Resources.Load<Texture2D>("UI/RoundRect");rounded=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));
            Debug.Log("[Presentation] Creating canvas");
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scale=gameObject.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1600,900);scale.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            if(!EventSystem.current){var input=new GameObject("Interface Input");input.AddComponent<EventSystem>();input.AddComponent<InputSystemUIInputModule>();}
            background=Rect("Menu Art",transform,0,0,0,0).gameObject;Stretch(background.GetComponent<RectTransform>());
            var image=background.AddComponent<RawImage>();image.texture=Resources.Load<Texture2D>("UI/MenuBackground");image.raycastTarget=false;
            var shade=Rect("Art Shade",background.transform,0,0,0,0);Stretch(shade);var shading=shade.gameObject.AddComponent<RawImage>();shading.texture=Resources.Load<Texture2D>("UI/MenuShade");shading.raycastTarget=false;
            frame=Rect("1600x900 Interface",transform,0,0,1600,900);frame.anchorMin=frame.anchorMax=new Vector2(.5f,.5f);frame.pivot=new Vector2(.5f,.5f);frame.anchoredPosition=Vector2.zero;
            content=Rect("Content",frame,0,0,1600,900).gameObject;ready=true;ShowHome();
            Debug.Log("[Presentation] Home screen created");
        }
        static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var obj=new GameObject(name,typeof(RectTransform));var rt=obj.GetComponent<RectTransform>();rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
        }
        static void Stretch(RectTransform rt){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,.5f);rt.offsetMin=rt.offsetMax=Vector2.zero;}
        void Clear(Page next)
        {
            page=next;foreach(Transform child in content.transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}actions.Clear();transient.Clear();status=null;
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
            Label(content.transform,"Рабочий прототип  /  графическая итерация 02",945,830,590,24,12,Muted,false,TextAnchor.MiddleRight);
        }
        public void ShowHome()
        {
            Clear(Page.Home);Header("Voxel sandbox · облачный проект",false);
            Label(content.transform,"ПОСТРОЙ ТО, ЧЕГО ЕЩЁ НЕТ",67,230,700,28,13,Accent);
            Label(content.transform,"ТВОЙ МИР.\nНОВАЯ ЭРА.",58,282,980,170,56,White,true);
            Label(content.transform,"Пять измерений. Один общий мир.\nИсследуй, строй и приглашай друзей.",67,487,790,92,24,White);
            Button("Начать путешествие  →",64,631,370,64,ShowWorld,true);
            Button("Играть с друзьями",452,631,290,64,ShowRooms);
            if(game.HasSave)Button("Продолжить сохранение",760,631,307,64,game.ContinueOffline);
            Button("Настройки",64,715,180,52,()=>ShowSettings(Page.Home));
            Button("Выйти",261,715,150,52,()=>Application.Quit());
            var card=Box(content.transform,1100,577,435,200,new Color(.045f,.085f,.095f,.84f));
            Label(card.transform,"01  /  ПЕРВАЯ ЭКСПЕДИЦИЯ",24,23,380,28,13,Accent);
            Label(card.transform,"ЛЕСТИНГИЯ",24,66,375,43,25,White,true);
            Label(card.transform,"Леса и скалы. Озёра и горизонты.\nМесто, с которого всё начинается.",24,123,383,63,17,Muted);
            Label(content.transform,"Фон — концепт-арт. Игровой мир создаётся из вокселей.",64,836,920,24,12,Muted);
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
            Label(content.transform,"ESC — продолжить",505,552,760,37,16,Muted);
            if(game.Online)Button("Скопировать код комнаты",505,619,380,58,()=>GUIUtility.systemCopyBuffer=game.RoomCode);
        }
        public void ShowInventory()
        {
            Clear(Page.Inventory);Header("Инвентарь · крафт",false);inventoryFrom=-1;
            Label(content.transform,"СНАРЯЖЕНИЕ И ПРИПАСЫ",64,165,1400,59,32,White,true);
            Label(content.transform,"Выберите ячейку, затем ячейку назначения. Первые 9 — быстрые слоты. ПКМ в мире: еда / накидка / установка.",66,250,1465,53,16,Muted);
            if(!game.Session)return;var inventory=game.Session.Inventory;
            for(int i=0;i<45;i++)
            {
                int index=i;var stack=inventory.slots[i];var button=Button("",64+(i%9)*96,328+(i/9)*89,91,79,()=>
                {
                    if(inventoryFrom<0){inventoryFrom=index;buttonHighlight(index);}
                    else {game.Session.Transfer(inventoryFrom,index);ShowInventory();}
                });
                Icon(button.transform,stack.item,22,3,48);Label(button.transform,stack.count>0?stack.count.ToString():"—",7,58,77,17,12,Muted,false,TextAnchor.MiddleRight);
                if(i<9)Label(button.transform,(i+1).ToString(),7,5,20,20,11,Accent);
            }
            Label(content.transform,"СОЗДАТЬ",974,328,553,35,18,Accent,true);
            for(int i=0;i<Recipes.All.Length;i++)
            {
                int index=i;var recipe=Recipes.All[i];string costs="";foreach(var ingredient in recipe.inputs)costs+=(costs.Length==0?"":" · ")+Items.Get(ingredient.item).Name+" ×"+ingredient.count;
                var button=Button(recipe.Name+"  |  "+costs,974,383+i*48,553,42,()=>{game.Session.Craft(index);ShowInventory();});
                button.GetComponentInChildren<Text>().fontSize=14;
            }
            status=Label(content.transform,game.Session.Notice,340,803,1150,62,16,Accent);
            Button("←  В мир",64,802,230,55,game.ToggleInventory,true);
        }
        void buttonHighlight(int index)
        {
            status.text="Перенос: "+Items.Get(game.Session.Inventory.slots[index].item).Name+" — выберите целевую ячейку";
        }
        void Icon(Transform parent,int item,float x,float y,float size)
        {
            var texture=ItemIcon(item);
            if(texture){var raw=Rect("Item icon",parent,x,y,size,size).gameObject.AddComponent<RawImage>();raw.texture=texture;raw.raycastTarget=false;}
            else if(item>0)Label(parent,Items.Get(item).Name,x-9,y+8,size+18,size-8,10,White,false,TextAnchor.MiddleCenter);
        }
        Texture2D ItemIcon(int item)
        {
            if(item>0&&item<=15)return Resources.Load<Texture2D>("Icons/"+SurfaceLayers.Icon((ushort)item));
            return Resources.Load<Texture2D>("Icons/Item"+item);
        }
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
            Label(content.transform,"Высокое качество по умолчанию. Без скрытого автоматического снижения.",66,247,1400,42,19,Muted);
            var settings=VisualSettings.Current;
            Box(content.transform,64,323,713,402,Panel);Box(content.transform,800,323,736,402,Panel);
            Slider("Дальность мира",64+28,354,655,settings.ViewDistance,2,5,true,v=>{settings.ViewDistance=Mathf.RoundToInt(v);settings.Save();},v=>(int)v+" чанка/сторону");
            Slider("Масштаб рендера",92,469,655,settings.RenderScale,.85f,1.15f,false,v=>{settings.RenderScale=v;settings.Save();},v=>Mathf.RoundToInt(v*100)+"%");
            Button("Тени: "+(settings.ShadowResolution==2048?"высокие · 2048":"умеренные · 1024"),92,589,309,58,()=>{settings.ShadowResolution=settings.ShadowResolution==2048?1024:2048;settings.Save();ShowSettings(settingsBack);});
            Button("Постэффекты: "+(settings.PostEffects?"вкл.":"выкл."),419,589,328,58,()=>{settings.PostEffects=!settings.PostEffects;settings.Save();ShowSettings(settingsBack);});
            Button("Объёмы: "+(settings.VolumetricQuality==1?"высокие":"сокращённые"),92,667,655,38,()=>{settings.VolumetricQuality=1-settings.VolumetricQuality;settings.Save();ShowSettings(settingsBack);});
            Slider("Поле зрения",828,354,680,settings.FieldOfView,60,100,true,v=>{settings.FieldOfView=v;settings.Save();},v=>(int)v+"°");
            Slider("Чувствительность мыши",828,469,680,settings.Sensitivity,.04f,.25f,false,v=>{settings.Sensitivity=v;settings.Save();},v=>v.ToString("0.00"));
            Label(content.transform,"WASD — движение     SPACE — прыжок\nЛКМ — убрать блок     ПКМ — поставить\n1–9 — быстрые слоты     E — инвентарь         ESC — меню",828,596,679,106,18,White);
            Button("←  Готово",64,798,210,54,()=>{settings.Save();if(settingsBack==Page.Pause)ShowPause();else ShowHome();},true);
            Button("Вернуть высокий пресет",296,798,327,54,()=>{settings.HighDefault();ShowSettings(settingsBack);});
            Label(content.transform,"FPS зависит от железа. Текущая платформа — Windows x64.",663,792,873,69,15,Muted,false,TextAnchor.MiddleRight);
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
            Label(content.transform,"LESTINGCRAFT",36,27,430,27,15,White,true);
            realmHUD=Label(content.transform,"",36,66,800,42,16,White);
            netHUD=Label(content.transform,"",925,29,641,43,14,Accent,false,TextAnchor.MiddleRight);
            perfHUD=Label(content.transform,"",1110,71,455,28,12,Muted,false,TextAnchor.MiddleRight);
            loadingHUD=Label(content.transform,"",36,113,780,37,14,Accent);
            // Minimal reticle, not an IMGUI text '+'.
            foreach(var r in new[]{new Vector4(799,439,2,7),new Vector4(799,454,2,7),new Vector4(789,449,7,2),new Vector4(804,449,7,2)})Box(content.transform,r.x,r.y,r.z,r.w,new Color(1,1,1,.82f),false);
            slots=new Image[9];slotRects=new RectTransform[9];slotIcons=new RawImage[9];slotCounts=new Text[9];slotFallback=new Text[9];lastInventoryRevision=-1;lastSlot=-1;
            var backdrop=Box(content.transform,364,786,872,99,new Color(.025f,.05f,.055f,.88f));
            for(int i=0;i<9;i++)
            {
                var image=Box(backdrop.transform,10+i*95,10,91,79,new Color(.10f,.155f,.165f,.92f));slots[i]=image;slotRects[i]=image.rectTransform;
                var raw=Rect("Material icon",image.transform,24,5,48,48).gameObject.AddComponent<RawImage>();raw.raycastTarget=false;slotIcons[i]=raw;
                slotCounts[i]=Label(image.transform,"",51,57,32,18,11,White,false,TextAnchor.MiddleRight);
                slotFallback[i]=Label(image.transform,"",8,13,75,35,10,White,false,TextAnchor.MiddleCenter);
                Label(image.transform,(i+1).ToString(),0,55,91,18,11,Muted,false,TextAnchor.MiddleCenter);
            }
            itemHUD=Label(content.transform,"",400,743,800,33,18,White,false,TextAnchor.MiddleCenter);
            Label(content.transform,"ЛКМ  добывать   ·   ПКМ  использовать   ·   E  инвентарь",413,712,774,24,12,Muted,false,TextAnchor.MiddleCenter);
            vitalsHUD=Label(content.transform,"",36,751,325,121,15,White);
            objectiveHUD=Label(content.transform,"",36,175,1000,63,15,Accent);
            noticeHUD=Label(content.transform,"",400,656,800,41,14,Accent,false,TextAnchor.MiddleCenter);
            Label(content.transform,"ESC  меню",1357,841,210,29,12,Muted,false,TextAnchor.MiddleRight);
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
            if(page!=Page.HUD)return;
            averageFrame=Mathf.Lerp(averageFrame,Mathf.Min(Time.unscaledDeltaTime,.5f),.04f);timer+=Time.unscaledDeltaTime;
            if(timer>.25f)
            {
                timer=0;realmHUD.text=game.WorldLabel+"  /  "+game.ModeName;
                if(game.Session)
                {
                    var session=game.Session;var v=session.Vitals;
                    vitalsHUD.text=session.Creative?"ТВОРЧЕСКИЙ РЕЖИМ":$"Здоровье  {v.health:0}   ·   Голод  {v.hunger:0}\nТемпература  {v.temperature:0.0}°C\nПромокание  {v.wetness*100:0}%   ·   Силы  {v.stamina:0}";
                    objectiveHUD.text=session.Creative?"":GameRules.NextObjective(session.Data.campaign.chapter,session.Data.campaign.milestones);
                    noticeHUD.text=session.Notice;
                }
                netHUD.text=game.Online?(game.LastError.Length>0?game.LastError:"●  КООПЕРАТИВ   ·   "+game.PlayerCount+" / 8"):"ЛОКАЛЬНЫЙ МИР";
                perfHUD.text=Mathf.RoundToInt(1/Mathf.Max(.001f,averageFrame))+" FPS   ·   "+game.ChunkCount+" чанков";
                loadingHUD.text=!game.WorldReady?"Формирование ландшафта… "+game.ChunkCount+" чанков":"";
            }
            if(game.Session&&(lastInventoryRevision!=game.Session.Inventory.revision||lastSlot!=game.Session.Inventory.selected))
            {
                var inventory=game.Session.Inventory;lastInventoryRevision=inventory.revision;lastSlot=inventory.selected;
                for(int i=0;i<9;i++)
                {
                    var stack=inventory.slots[i];slots[i].color=i==inventory.selected?new Color(.36f,.49f,.30f,.97f):new Color(.10f,.155f,.165f,.92f);
                    var texture=ItemIcon(stack.item);slotIcons[i].texture=texture;slotIcons[i].enabled=texture;
                    slotFallback[i].text=texture||stack.item==0?"":Items.Get(stack.item).Name;
                    slotCounts[i].text=stack.count>0?(game.Session.Creative?"∞":stack.count.ToString()):"";
                }
                itemHUD.text=inventory.Active.count>0?Items.Get(inventory.Active.item).Name:"Свободная рука";
            }
        }
        void OnDestroy(){if(rounded)Destroy(rounded);}
    }
}
