namespace LestingCraft.Presentation
{
    public static class SurfaceLayers
    {
        public static readonly string[] Names={"stone","dirt","grass","grass-side","bark","wood-end","leaves","sand","water","lava","bedrock","basalt","crystal","aether","ore","snow","planks","workbench-top","workbench-side","furnace-side","furnace-front","furnace-top","campfire-side","campfire-top","ore-copper","ore-coal"};
        public static int ForFace(ushort block,int face)
        {
            switch(block)
            {
                case 1:return 0;case 2:return 1;case 3:return face==2?2:face==3?1:3;
                case 4:return face==2||face==3?5:4;case 5:return 6;case 6:return 7;
                case 7:return 8;case 8:return 9;case 9:return 10;case 10:return 11;
                case 11:return 12;case 12:return 13;case 13:return 14;case 14:return 15;
                case 15:return face==2?23:22;
                case 16:return face==2?17:face==3?16:18;
                case 17:return face==2||face==3?21:face==5?20:19;
                case 18:return 16;
                default:return 0;
            }
        }
        // Appearance only: the existing ore ID, drops, save format and generator remain unchanged.
        public static int ForCellFace(ushort block,int face,int x,int y,int z,int seed)
        {
            if(block!=13)return ForFace(block,face);
            uint variant=LestingCraft.World.TerrainGenerator.Hash(UnityEngine.Mathf.FloorToInt(x/4f),UnityEngine.Mathf.FloorToInt(y/4f),UnityEngine.Mathf.FloorToInt(z/4f),seed^27183)%3;
            return variant==0?14:variant==1?24:25;
        }
        public static string DisplayName(ushort id)
        {
            string[] names={"Воздух","Камень","Земля","Дёрн","Древесина","Листва","Песок","Вода","Лава","Коренная порода","Базальт","Кристалл","Эфирный камень","Руда","Снег","Костёр","Верстак","Печь","Доски"};
            return id<names.Length?names[id]:"Неизвестный блок";
        }
        public static string Icon(ushort id)=>id==15?"Item15":Names[ForFace(id,2)];
    }
}
