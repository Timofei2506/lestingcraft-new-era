using System;
namespace LestingCraft.Gameplay
{
    public enum GameMode { Survival=0,Hardcore=1,Creative=2,Story=3 }
    public static class GameRules
    {
        public const int Version=10;
        public static readonly string[] ModeNames={"Выживание","Хардкор","Творческий","Сюжет"};
        public static readonly string[] ModeDescriptions={"Свободное выживание и добровольная история.\nПосле вступления выбирайте свой путь.","Одна жизнь на команду.\nЛюбая смерть завершает забег.","Свободное строительство.\nБез голода, холода и урона.","Глава I: Пока горит последний дом.\nОбщие диалоги, загадки и Страж корней."};
        public static bool AcceptsRoom(int roomRules,int serverRules)=>serverRules>=Version&&(roomRules==Version||roomRules==9||roomRules==8||roomRules==3||roomRules==4||roomRules==5||roomRules==6||roomRules==7);
        public static bool IsValid(int mode)=>mode>=0&&mode<4;
        public static bool CanDie(GameMode mode)=>mode!=GameMode.Creative;
        public static readonly string[] ChapterNames={"Пока горит последний дом","Под пеплом","Выше облаков","Память камня","Сердце разлома"};
        public static readonly string[] BossNames={"Страж корней","Пепельный титан","Небесный хранитель","Повелитель эха","Ядро хаоса"};
        public static string NextObjective(int chapter,int milestones)
        {
            if(chapter>=5)return "Эпоха восстановлена. Финал кампании.";
            if((milestones&1)==0)return "Соберите древесину и создайте инструменты";
            if((milestones&2)==0)return "Постройте укрытие и разожгите костёр";
            return "Следующая цель кампании: "+BossNames[Math.Min(chapter,4)]+" (контент ещё разрабатывается)";
        }
    }
    [Serializable] public sealed class CampaignProgress
    {
        public int chapter,milestones;public int unlockedMask=1;public bool runEnded;
        public bool CanEnter(int realm)=>realm>=0&&realm<5&&(unlockedMask&(1<<realm))!=0;
    }
}
