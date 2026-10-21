using LestingCraft.Gameplay;
using LestingCraft.World;

namespace LestingCraft.Story
{
    public readonly struct LoreCodexEntry
    {
        public readonly int Id, Chapter;
        public readonly string ChapterRu, TitleRu, SpeakerRu, BodyRu;
        public LoreCodexEntry(int id, int chapter, string chapterRu, string titleRu, string speakerRu, string bodyRu)
        {
            Id = id; Chapter = chapter; ChapterRu = chapterRu; TitleRu = titleRu; SpeakerRu = speakerRu; BodyRu = bodyRu;
        }
    }

    public static class StoryChapters
    {
        public static readonly LoreCodexEntry[] Entries =
        {
            new LoreCodexEntry(0, 1, "Глава I", "Дневник смотрителей тракта", "Марта и Орен",
                "«Корни под мельницей не искали воду. Три печати Тихого Брода — Солнце, Ворон и Узел — были выкованы вокруг обломка звёздного навигационного ядра.»"),

            new LoreCodexEntry(1, 1, "Глава I", "Клятва у внешней печати", "Вера и Илья",
                "«Мира удержала внешний круг святилища ценой собственной жизни, но за расколотым камнем открылись звёздные координаты и чертёж орбитального корабля.»"),

            new LoreCodexEntry(2, 2, "Глава II", "Решётка Солнечной системы", "Бортовой ИИ «Астра»",
                "«Все миры Солнечной системы — от Меркурия и Венеры до колец Сатурна, ледяной Европы, метановых озёр Титана и Плутона — образуют единую маяковую сеть.»"),

            new LoreCodexEntry(3, 2, "Глава II", "Шёпот сонного паралича", "Лекарь Тихого Брода",
                "«Просыпаясь во Сне среди бледных колонн, путники видят безмолвный силуэт. Это не призрак, а квантовое эхо далёкого мира за пределами Млечного Пути.»"),

            new LoreCodexEntry(4, 3, "Глава III", "Мост в Галактику Андромеды M31", "Илья · Космодром",
                "«Резонансный вектор ведёт за 2,54 миллиона световых лет — через системы Альфы Центавра, TRAPPIST-1 и Kepler-452 прямо в спиральный рукав Элизиума M31!»"),

            new LoreCodexEntry(5, 3, "Глава III", "Голос из Кристалла Андромеды", "Эхо Миры · Сомния",
                "«Вы прошли сквозь ночь и две галактики. Пока зажжены фонари Тихого Брода и поёт Кристалл Андромеды, ни один мир больше не останется в темноте.»"),

            new LoreCodexEntry(6, 4, "Глава IV", "Протокол «Черенковский Затон»", "Дозиметрический пост №6",
                "«Затопленный бассейн охладителя под двумя геликоидальными башнями не отпускает облучённых живыми. Чтобы открыть шлюз плотины, вырви шесть излучателей со дна и сокруши трёх Стражей Спирали.»")
        };

        public static string EvaluateProgressionTransmission(
            Dimension dim, GalaxyId galaxy, int dreamVisits, bool hasAndromedaCrystal, ref int unlockedMask)
        {
            if ((unlockedMask & 1) == 0 && dim != Dimension.Lestingia && dim != Dimension.Dream && dim != Dimension.Radia)
            {
                unlockedMask |= 1;
                return "Вера (радиосвязь): Глава II открыта! Вы вышли за пределы Лестингии — исследуйте луны гигантов и соседние звёздные системы!";
            }
            if ((unlockedMask & 2) == 0 && dreamVisits > 0)
            {
                unlockedMask |= 2;
                return "Илья: Ты выбрался из измерения «Сон»! Частота тех бледных монолитов совпадает с маяком в галактике Андромеды (M31)!";
            }
            if ((unlockedMask & 4) == 0 && (galaxy == GalaxyId.Andromeda || dim == Dimension.AndromedaPrime || dim == Dimension.Somnia))
            {
                unlockedMask |= 4;
                return "Бортовой ИИ «Астра»: Глава III — Вы достигли Галактики Андромеды (M31)! Спуститесь на Элизиум или Сомнию за Кристаллом Андромеды.";
            }
            if ((unlockedMask & 8) == 0 && hasAndromedaCrystal)
            {
                unlockedMask |= 8;
                return "Эхо Миры: Кристалл Андромеды резонирует с корнями Тихого Брода... Сага двух галактик объединена!";
            }
            return null;
        }
    }
}
