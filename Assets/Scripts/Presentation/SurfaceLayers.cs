namespace LestingCraft.Presentation
{
    public static class SurfaceLayers
    {
        public static readonly string[] Names={"stone","dirt","grass","grass-side","bark","wood-end","leaves","sand","water","lava","bedrock","basalt","crystal","aether","ore","snow"};
        public static int ForFace(ushort block,int face)
        {
            switch(block)
            {
                case 1:return 0;case 2:return 1;case 3:return face==2?2:face==3?1:3;
                case 4:return face==2||face==3?5:4;case 5:return 6;case 6:return 7;
                case 7:return 8;case 8:return 9;case 9:return 10;case 10:return 11;
                case 11:return 12;case 12:return 13;case 13:return 14;case 14:return 15;
                default:return 0;
            }
        }
        public static string DisplayName(ushort id)
        {
            string[] names={"Воздух","Камень","Земля","Дёрн","Древесина","Листва","Песок","Вода","Лава","Коренная порода","Базальт","Кристалл","Эфирный камень","Руда","Снег"};
            return id<names.Length?names[id]:"Неизвестный блок";
        }
        public static string Icon(ushort id)=>Names[ForFace(id,2)];
    }
}
