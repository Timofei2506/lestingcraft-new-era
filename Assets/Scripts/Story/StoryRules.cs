using System;
namespace LestingCraft.Story
{
 [Serializable] public sealed class StoryState
 {
  public bool bridgeSolved;public int bridgeMask;public int stage,line=-1,revision,searched,choice,rewards,runes,bossHealth,bossMax=180,bossPhase,pylons,attack,attackSerial;public long sceneAt,vulnerableUntil,impactAt,nextAttack;public float attackX,attackZ;public int acked,waiting;
  public StoryState Copy()=>(StoryState)MemberwiseClone();
 }
 public static class StoryRules
 {
  public static bool Dialogue(int s)=>s==3||s==5||s==7||s==9||s==13;
  public static bool Locked(StoryState s)=>s.stage==0||s.stage==1||s.stage==12||s.stage==10&&s.sceneAt>0||Dialogue(s.stage)&&s.line>=0;
  public static string Objective(StoryState s)
  {switch(s.stage){case 0:return "Ожидаем готовности всей команды";case 1:return "Выбраться из-под завалов";case 2:return s.searched==3?"Подойдите к раненым у стены":"Обыщите ящик и дорожную сумку в доме";case 3:return "Последние слова смотрителей";case 4:if(!s.bridgeSolved)return "У тракта: подберите противовесы общим весом 7";return "Дойдите по тракту до Тихого Брода. Найдите Веру у колодца";case 5:return "Узнайте, что случилось с деревней";case 6:return "Встретьтесь с Мирой у старой мельницы";case 7:return "Выслушайте Миру";case 8:return "Откройте печати мельницы в порядке старой песни";case 9:return "Вернитесь к кузнецу Илье за снаряжением";case 10:return "Найдите алтарь корневого святилища";case 11:return s.pylons==7?"Сердце открыто — ударьте Стража!":"Зажгите три жаровни, чтобы открыть сердце Стража";case 12:return "Печать рушится";case 13:return "Цена закрытой двери";default:return "Глава I завершена · Отдохните в Тихом Броде";}}
  public static bool Act(StoryState s,string action,int arg,long now,int lines)
  {
   if(action=="ready"&&s.stage==0){s.stage=1;s.sceneAt=now;}
   else if(action=="crate"&&s.stage==2){if((s.searched&1)!=0)return false;s.searched|=1;s.rewards|=1;}
   else if(action=="satchel"&&s.stage==2){if((s.searched&2)!=0)return false;s.searched|=2;s.rewards|=2;}
   else if(action=="witness"&&s.stage==2&&s.searched==3){s.stage=3;s.line=0;}
   else if(action=="weight"&&s.stage==4&&!s.bridgeSolved&&arg>=0&&arg<3){s.bridgeMask^=1<<arg;s.bridgeSolved=((s.bridgeMask&1)!=0?2:0)+((s.bridgeMask&2)!=0?3:0)+((s.bridgeMask&4)!=0?5:0)==7;}
   else if(action=="village"&&s.stage==4&&s.bridgeSolved){s.stage=5;s.line=0;}
   else if(action=="mill"&&s.stage==6){s.stage=7;s.line=0;}
   else if(action=="rune"&&s.stage==8&&arg>=0&&arg<3){int wanted=s.runes==0?2:s.runes==1?0:1;s.runes=arg==wanted?s.runes+1:0;if(s.runes==3){s.stage=9;s.line=-1;}}
   else if(action=="smith"&&s.stage==9&&s.line<0)s.line=0;
   else if(action=="altar"&&s.stage==10&&s.sceneAt==0)s.sceneAt=now;
   else if(action=="choice"&&s.stage==3&&s.line==10&&s.choice==0&&(arg==1||arg==2)){s.choice=arg;s.line++;}
   else if(action=="ack"&&Dialogue(s.stage)&&s.line>=0&&!(s.stage==3&&s.line==10))
   {s.line++;if(s.line>=lines){int old=s.stage;s.stage=old==3?4:old==5?6:old==7?8:old==9?10:14;s.line=-1;s.sceneAt=0;if(old==9)s.rewards|=4;}}
   else if(action=="pylon"&&s.stage==11&&arg>=0&&arg<3){if((s.pylons&(1<<arg))!=0)return false;s.pylons|=1<<arg;if(s.pylons==7)s.vulnerableUntil=now+12000;}
   else if(action=="hit"&&s.stage==11&&s.pylons==7&&now<s.vulnerableUntil)
   {s.bossHealth=Math.Max(0,s.bossHealth-8);if(s.bossHealth==0){s.stage=12;s.sceneAt=now;s.rewards|=8;}else if(s.bossHealth<=s.bossMax/2&&s.bossPhase==0){s.bossPhase=1;s.pylons=0;s.vulnerableUntil=0;}}
   else return false;s.revision++;s.acked=0;return true;
  }
  public static bool Tick(StoryState s,long now)
  {bool changed=false;if(s.stage==1&&now-s.sceneAt>=14000){s.stage=2;s.sceneAt=0;changed=true;}if(s.stage==10&&s.sceneAt>0&&now-s.sceneAt>=12000){s.stage=11;s.bossHealth=s.bossMax;s.sceneAt=0;changed=true;}if(s.stage==12&&now-s.sceneAt>=16000){s.stage=13;s.line=0;changed=true;}if(s.stage==11&&s.pylons==7&&now>=s.vulnerableUntil){s.pylons=0;changed=true;}if(changed)s.revision++;return changed;}
 }
 [Serializable] public sealed class StoryLine {public string speaker,text;public bool choice;}
 [Serializable] public sealed class StoryNode {public int stage;public StoryLine[] lines;}
 [Serializable] public sealed class StoryContent {public string title,choiceA,choiceB;public StoryNode[] nodes;public StoryLine[] Lines(int stage){foreach(var n in nodes)if(n.stage==stage)return n.lines;return Array.Empty<StoryLine>();}}
}
