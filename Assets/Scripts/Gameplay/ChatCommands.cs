using System;
using UnityEngine;
namespace LestingCraft.Gameplay
{
 public readonly struct CommandResult
 {public readonly string Action,Message;public readonly int Value;public CommandResult(string message,string action="",int value=0){Message=message;Action=action;Value=value;}}
 public static class ChatCommands
 {
  public static bool TryParseGameModeArg(string raw,out GameMode mode,out string label)
  {
   string s=(raw??"").Trim().ToLowerInvariant();
   if(s=="0"||s=="s"||s=="survival"||s=="выживание"){mode=GameMode.Survival;label="Выживание";return true;}
   if(s=="1"||s=="c"||s=="creative"||s=="креатив"||s=="творчество"){mode=GameMode.Creative;label="Творчество";return true;}
   if(s=="2"||s=="h"||s=="hardcore"||s=="хардкор"){mode=GameMode.Hardcore;label="Хардкор";return true;}
   if(s=="3"||s=="a"||s=="adventure"||s=="story"||s=="сюжет"||s=="приключение"){mode=GameMode.Story;label="Сюжет";return true;}
   mode=GameMode.Survival;label="";return false;
  }

  public static CommandResult Execute(string text,bool creative,bool offline,Inventory inv,int seed,Vector3 position,int players)
  {
   var p=(text??"").Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);if(p.Length==0)return new CommandResult("Введите /help");
   switch(p[0].ToLowerInvariant())
   {
    case "/help":return new CommandResult("/help /gamemode [0|1|2|3|creative|survival] /fly /seed /where /players /sethome /home /wp [имя] /lan /market /hell /paradise /backrooms /overworld /clear /save · creative: /give ID [1..64], /time day|night, /tp X Y Z");
    case "/gamemode":
    case "/gm":
     if(!offline)return new CommandResult("/gamemode доступна только в локальном мире или для хоста LAN");
     if(p.Length!=2||!TryParseGameModeArg(p[1],out var targetMode,out var modeLabel))
      return new CommandResult("Формат: /gamemode creative | survival | hardcore | adventure (или 0..3)");
     return new CommandResult("Режим игры переключён: «"+modeLabel+"»"+(targetMode==GameMode.Creative?" · Двойной [Пробел] или /fly — полёт!":""),"gamemode",(int)targetMode);
    case "/fly":
     if(!offline||!creative)return new CommandResult("/fly доступна только в творческом режиме (/gamemode creative)");
     return new CommandResult("Переключение полёта в креативе…","fly");
    case "/seed":return new CommandResult("Seed: "+seed);
    case "/where":return new CommandResult($"Координаты: {position.x:F1}, {position.y:F1}, {position.z:F1}");
    case "/players":return new CommandResult("Игроков в мире: "+players);
    case "/clear":return new CommandResult("Чат очищен локально","clear");
    case "/save":return offline?new CommandResult("Локальное сохранение выполнено","save"):new CommandResult("Онлайн-мир сохраняется автоматически на сервере");
    case "/sethome":return new CommandResult($"Точка дома сохранена ({position.x:F0}, {position.y:F0}, {position.z:F0})","sethome");
    case "/home":return offline?new CommandResult("Перемещение к точке дома…","home"):new CommandResult("В сетевой комнате идите по координатам путевой точки на миникарте [M]");
    case "/wp":
    case "/waypoint":
     string wpName=p.Length>=2?string.Join(" ",p,1,p.Length-1):"Метка";
     return new CommandResult("Путевая точка «"+wpName+"» сохранена на миникарте [M]","waypoint:"+wpName);
    case "/waypoints":return new CommandResult("Список путевых точек…","waypoints");
    case "/lan":return offline?new CommandResult("Открываю локальный мир для сети (LAN)…","lan"):new CommandResult("Вы уже играете в сетевом мире");
    case "/market":return new CommandResult("Открываю маркетплейс…","market");
    case "/hell":
    case "/inferno":
     return offline&&creative?new CommandResult("Переход в измерение «Ад · Базальтовая Преисподняя»…","inferno")
      :new CommandResult("Ритуал Ада: вырастите БЕЗ костной муки 2 дерева на расстоянии 7 блоков, сожгите их — когда они обгорят, сплетётся Арка в Ад!");
    case "/paradise":
    case "/rai":
    case "/рай":
     return offline&&creative?new CommandResult("Вознесение в измерение «Рай · Небесный Эдем»…","paradise")
      :new CommandResult("Ритуал Рая: выкопайте яму 2×2 глубиной 5 блоков, залейте водой, расставьте вокруг любые цветы, бросьте алмаз [Q] и прыгайте туда!");
    case "/backrooms":
     return offline&&creative?new CommandResult("Провал сквозь реальность в «Закулисье» (Backrooms)…","backrooms")
      :new CommandResult("В Закулисье можно провалиться во сне или при полном истощении");
    case "/overworld":
    case "/earth":
     return offline&&creative?new CommandResult("Возвращение в Лестингию…","overworld")
      :new CommandResult("Встаньте в светящиеся врата / фонтан возврата, чтобы вернуться в Лестингию");
    case "/tp":
     if(!creative||!offline)return new CommandResult("/tp разрешена только в локальном творческом режиме");
     float tx=0,ty=0,tz=0;
     if(p.Length!=4||!float.TryParse(p[1],out tx)||!float.TryParse(p[2],out ty)||!float.TryParse(p[3],out tz))
      return new CommandResult("Формат: /tp X Y Z");
     return new CommandResult($"Телепортация → ({tx:F0}, {ty:F0}, {tz:F0})",$"tp:{tx}:{ty}:{tz}");
    case "/give":
     int id=0,count=1;bool parsed=p.Length>=2&&p.Length<=3&&int.TryParse(p[1],out id)&&Items.Exists(id)&&(p.Length<3||int.TryParse(p[2],out count))&&count>=1&&count<=64;
     // Secret protocol /give 666 works in ALL game modes (Survival, Hardcore, Creative, Online) and is the ONLY way to obtain ID 666!
     if(parsed&&id==Items.ThermonukeId)
      return new CommandResult(inv.Add(id,Math.Min(count,Items.Get(id).MaxStack))
       ?"[СЕКРЕТНЫЙ ПРОТОКОЛ 666] Получена термоядерная боеголовка! ПКМ по земле взводит 8-секундный детонатор — БЕГИТЕ!"
       :"Недостаточно места в инвентаре","thermonuke",Items.ThermonukeId);
     if(!creative||!offline)return new CommandResult("/give разрешена только в локальном творческом режиме");
     if(!parsed)return new CommandResult("Формат: /give ID [1..64]");
     return new CommandResult(inv.Add(id,count)?"Получено: "+Items.Get(id).Name+" ×"+count:"Недостаточно места");
    case "/time":
     if(!creative||!offline)return new CommandResult("/time разрешена только в локальном творческом режиме");
     if(p.Length!=2||(p[1]!="day"&&p[1]!="night"))return new CommandResult("Формат: /time day|night");
     return new CommandResult("Время изменено","time",p[1]=="day"?420:1020);
    default:return new CommandResult("Неизвестная команда. /help — список");
   }
  }
 }
}
