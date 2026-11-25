using System;
using UnityEngine;
namespace LestingCraft.Gameplay
{
 public readonly struct CommandResult
 {public readonly string Action,Message;public readonly int Value;public CommandResult(string message,string action="",int value=0){Message=message;Action=action;Value=value;}}
 public static class ChatCommands
 {
  public static CommandResult Execute(string text,bool creative,bool offline,Inventory inv,int seed,Vector3 position,int players)
  {
   var p=(text??"").Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);if(p.Length==0)return new CommandResult("Введите /help");
   switch(p[0].ToLowerInvariant())
   {
    case "/help":return new CommandResult("/help /seed /where /players /sethome /home /wp [имя] /lan /market /hell /overworld /clear /save · creative: /give ID [1..64], /time day|night, /tp X Y Z");
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
     return offline&&(creative)?new CommandResult("Переход в измерение «Ад · Базальтовая Преисподняя»…","inferno")
      :new CommandResult("В выживании постройте или найдите Обсидиановые Врата Ада (или чертёж [B]) и шагните в арку!");
    case "/overworld":
    case "/earth":
     return offline&&creative?new CommandResult("Возвращение в Лестингию…","overworld")
      :new CommandResult("Встаньте в светящуюся обсидиановую арку возврата, чтобы покинуть Преисподнюю");
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
