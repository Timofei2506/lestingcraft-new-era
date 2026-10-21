using System;
namespace LestingCraft.Gameplay
{
    public enum GameMode { Survival=0,Hardcore=1,Creative=2,Story=3 }
    public static class GameRules
    {
        public const int Version=14;
        public static readonly string[] ModeNames={"Выживание","Хардкор","Творческий","Сюжет"};
        public static readonly string[] ModeDescriptions={"Свободное выживание, космос и тайны Сна.\nВсе галактики, биомы и поселения открыты.","Одна жизнь на команду.\nЛюбая смерть завершает забег.","Свободное строительство и полёты.\nПолный каталог блоков и корабль.","Трилогия Лестингии: Корни, Небо и Сон.\nСюжетные главы, космодром и сигнал М31."};
        public static bool AcceptsRoom(int roomRules,int serverRules)=>serverRules>=Version&&(roomRules>=3&&roomRules<=Version);
        public static bool IsValid(int mode)=>mode>=0&&mode<4;
        public static bool CanDie(GameMode mode)=>mode!=GameMode.Creative;
        public static readonly string[] ChapterNames={
            "Пока горит последний дом",
            "Голоса за куполом неба",
            "Обратная сторона Сна",
            "Эхо галактики Андромеды",
            "Сердце мирового разлома"
        };
        public static readonly string[] BossNames={
            "Страж корней",
            "Надзиратель орбиты",
            "Наблюдатель Сна",
            "Хранитель Элизиума М31",
            "Ядро хаоса"
        };
        public static string NextObjective(int chapter,int milestones)
        {
            if(chapter>=5)return "Эпоха восстановлена. Обе галактики и цепь печатей свободны.";
            if((milestones&1)==0)return "Соберите древесину и создайте первые инструменты";
            if((milestones&2)==0)return "Постройте укрытие, смастерите уютную кровать (55) и факелы";
            if(chapter==0)return "Глава I: Исследуйте Тихий Брод, мельницу трёх печатей и победите Стража корней";
            if(chapter==1)return "Глава II: Запустите челнок с космодрома, выйдите в открытый космос и найдите ретрансляторы Солнечной системы";
            if(chapter==2)return "Глава III: Переживите аномалию «Сон», совершите гиперпрыжок в галактику Андромеды (М31) и добудьте Кристалл Андромеды";
            return "Следующая цель экспедиции: "+BossNames[Math.Min(chapter,4)];
        }
    }
    [Serializable] public sealed class CampaignProgress
    {
        public int chapter,milestones;public int unlockedMask=1;public bool runEnded;
        public int discoveredSystemsMask=1,discoveredGalaxiesMask=1,loreUnlockedMask=1;
        public bool CanEnter(int realm)=>realm>=0&&realm<15&&(realm>=5||(unlockedMask&(1<<realm))!=0);
    }
}
