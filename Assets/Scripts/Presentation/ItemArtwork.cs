using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Gameplay;

namespace LestingCraft.Presentation
{
    public static class ItemArtwork
    {
        public static readonly int[] AllIds={1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,100,101,102,103,104,110,111,112,113,114,120,121,122,123};
        static readonly Dictionary<int,Texture2D> icons=new Dictionary<int,Texture2D>();
        public static Texture2D Get(int id)
        {
            if(id<=0)return null;
            if(icons.TryGetValue(id,out var cached)&&cached)return cached;
            var icon=Resources.Load<Texture2D>("Generated/Icons/Item"+id);
            if(!icon)icon=Resources.Load<Texture2D>("VoxelIcons/Item"+id);
            icons[id]=icon;return icon;
        }
    }
}
