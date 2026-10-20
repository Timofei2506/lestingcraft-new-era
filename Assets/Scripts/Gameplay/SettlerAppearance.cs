using System;
namespace LestingCraft.Gameplay
{
    /// <summary>Appearance only. No new save/network fields: the same actor ID has the same
    /// face, hair and clothing on every client, including after reconnect.</summary>
    public struct SettlerAppearance
    {
        public int Skin,Coat,Hair,Headwear,Detail;
        public static SettlerAppearance For(string actorId,int job)
        {
            if(actorId==null)actorId="";
            unchecked
            {
                uint h=2166136261u;
                for(int i=0;i<actorId.Length;i++){h^=actorId[i];h*=16777619u;}
                h^=(uint)job*0x9e3779b9u;
                h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;
                return new SettlerAppearance{
                    Skin=(int)(h%5),Coat=(int)((h>>4)%6),Hair=(int)((h>>9)%5),
                    Headwear=(int)((h>>15)%4),Detail=(int)((h>>21)%4)
                };
            }
        }
    }
}
