using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using LestingCraft.Runtime;
using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>Native scalable uGUI, no IMGUI or embedded webpage. All fonts/art are local.</summary>
    public sealed class GameUI : MonoBehaviour
    {
        enum Page{Home,World,Rooms,Settings,Pause,HUD}
        static readonly Color Ink=new Color(.055f,.086f,.10f), Panel=new Color(.07f,.115f,.13f,.96f), Muted=new Color(.58f,.66f,.66f), White=new Color(.91f,.94f,.90f), Accent=new Color(.65f,.86f,.48f);
        static readonly string[] RealmNames={"Лестингия","Пепельные земли","Небесный предел","Склеп эха","Разлом хаоса"};
        static readonly string[] RealmDetails={"Леса, озёра и скалистые высоты.\nПервый дом в новой эпохе.","Базальтовые плато и реки лавы.\nМир с тяжёлым характером.","Плавающие острова над облаками.\nВысота становится маршрутом.","Холодный камень и лиловые кристаллы.\nСвет среди тишины.","Обломки миров и чужие материалы.\nНестабильный горизонт."};
        PrototypeGame game;Canvas canvas;RectTransform frame;GameObject content,background;Font body,display;Sprite rounded;
        readonly List<GameObject> transient=new List<GameObject>();readonly List<Button> actions=new List<Button>();
        Page page,settingsBack;Text status,realmHUD,netHUD,perfHUD,itemHUD,loadingHUD;Image[] slots;RectTransform[] slotRects;
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
            Clear(Page.World);Header("Новый мир");
            Label(content.transform,"ВЫБЕРИ ГОРИЗОНТ",64,171,1370,67,36,White,true);
            Label(content.transform,"Каждое измерение — отдельная процедурная среда.",66,247,1000,42,19,Muted);
            for(int i=0;i<5;i++)
            {
                int realm=i;float x=64+i*296;bool selected=game.SelectedRealm==i;
                var button=Button("",x,331,280,254,()=>{game.SelectedRealm=realm;ShowWorld();},selected);
                var parent=button.transform;
                Label(parent,"0"+(i+1),22,20,240,39,25,selected?Ink:Accent,true);
                Label(parent,RealmNames[i],22,87,240,83,23,selected?Ink:White,true);
                Label(parent,RealmDetails[i],22,177,239,61,15,selected?new Color(.15f,.24f,.17f):Muted);
                if(selected)Label(parent,"ВЫБРАНО",109,27,145,30,12,Ink,false,TextAnchor.MiddleRight);
            }
            Input("SEED МИРА",game.WorldSeed,"Например: 2506",64,628,440,v=>game.WorldSeed=v);
            Label(content.transform,"Локальный мир",542,626,550,34,20,White);
            Label(content.transform,"Не требует сервера или подключения.\nСохранение построек между запусками пока не реализовано.",542,675,610,72,16,Muted);
            Button("Создать мир  →",1227,668,309,64,game.PlayOffline,true);
            status=Label(content.transform,"Порталы, крафт и сюжет ещё в разработке.",542,784,900,66,14,Muted);
        }
        public void ShowRooms()
        {
            Clear(Page.Rooms);Header("Кооператив · до 8 игроков");
            Label(content.transform,"ОДИН МИР. ВМЕСТЕ.",64,171,1400,67,36,White,true);
            Label(content.transform,"Создай комнату и передай её код друзьям — или присоединись к их миру.",66,247,1380,42,19,Muted);
            Input("ТВОЁ ИМЯ",game.PlayerName,"Исследователь",64,321,440,v=>game.PlayerName=v);
            var left=Box(content.transform,64,454,710,305,Panel);var right=Box(content.transform,795,454,741,305,Panel);
            Label(left.transform,"НОВАЯ КОМНАТА",28,28,655,41,25,White,true);
            Label(left.transform,RealmNames[game.SelectedRealm]+" · seed: "+game.WorldSeed+"\nИзменения блоков синхронизируются в реальном времени.",28,96,650,76,17,Muted);
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
            Label(content.transform,"ACES, bloom, мягкая виньетка. Геометрия и текстуры не упрощаются.",92,667,653,50,14,Muted);
            Slider("Поле зрения",828,354,680,settings.FieldOfView,60,100,true,v=>{settings.FieldOfView=v;settings.Save();},v=>(int)v+"°");
            Slider("Чувствительность мыши",828,469,680,settings.Sensitivity,.04f,.25f,false,v=>{settings.Sensitivity=v;settings.Save();},v=>v.ToString("0.00"));
            Label(content.transform,"WASD — движение     SPACE — прыжок\nЛКМ — убрать блок     ПКМ — поставить\n1–9 — материал         ESC — меню",828,596,679,106,18,White);
            Button("←  Готово",64,798,210,54,()=>{settings.Save();if(settingsBack==Page.Pause)ShowPause();else ShowHome();},true);
            Button("Вернуть высокий пресет",296,798,327,54,()=>{settings.HighDefault();ShowSettings(settingsBack);});
            Label(content.transform,"FPS зависит от железа. WebGL обычно медленнее Windows-сборки.",663,792,873,69,15,Muted,false,TextAnchor.MiddleRight);
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
            slots=new Image[9];slotRects=new RectTransform[9];
            var backdrop=Box(content.transform,364,786,872,99,new Color(.025f,.05f,.055f,.88f));
            var ids=new ushort[]{1,2,3,4,5,6,11,12,14};
            for(int i=0;i<9;i++)
            {
                var image=Box(backdrop.transform,10+i*95,10,91,79,new Color(.10f,.155f,.165f,.92f));slots[i]=image;slotRects[i]=image.rectTransform;
                var raw=Rect("Material icon",image.transform,24,5,48,48).gameObject.AddComponent<RawImage>();raw.texture=Resources.Load<Texture2D>("Icons/"+SurfaceLayers.Icon(ids[i]));raw.raycastTarget=false;
                Label(image.transform,(i+1).ToString(),0,55,91,18,11,Muted,false,TextAnchor.MiddleCenter);
            }
            itemHUD=Label(content.transform,"",400,743,800,33,18,White,false,TextAnchor.MiddleCenter);
            Label(content.transform,"ЛКМ  убрать   ·   ПКМ  поставить   ·   1–9  материал",413,712,774,24,12,Muted,false,TextAnchor.MiddleCenter);
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
                timer=0;realmHUD.text=game.WorldLabel;
                netHUD.text=game.Online?(game.LastError.Length>0?game.LastError:"●  КООПЕРАТИВ   ·   "+game.PlayerCount+" / 8"):"ЛОКАЛЬНЫЙ МИР";
                perfHUD.text=Mathf.RoundToInt(1/Mathf.Max(.001f,averageFrame))+" FPS   ·   "+game.ChunkCount+" чанков";
                loadingHUD.text=!game.WorldReady?"Формирование ландшафта… "+game.ChunkCount+" чанков":"";
            }
            if(lastSelection!=game.SelectedBlock)
            {
                lastSelection=game.SelectedBlock;var ids=new ushort[]{1,2,3,4,5,6,11,12,14};
                for(int i=0;i<9;i++){bool selected=ids[i]==lastSelection;slots[i].color=selected?new Color(.36f,.49f,.30f,.97f):new Color(.10f,.155f,.165f,.92f);}
                itemHUD.text=SurfaceLayers.DisplayName(lastSelection);
            }
        }
        void OnDestroy(){if(rounded)Destroy(rounded);}
    }
}
