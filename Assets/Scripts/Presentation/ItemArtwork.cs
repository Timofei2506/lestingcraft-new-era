using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Gameplay;

namespace LestingCraft.Presentation
{
    public static class ItemArtwork
    {
        static readonly int[] LegacyIds={1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,100,101,102,103,104,105,106,107,108,130,131,132,110,111,112,113,114,120,121,122,123};
        public static readonly int[] AllIds=BuildIds();
        static int[] BuildIds(){var ids=new List<int>(LegacyIds);foreach(var b in ExpandedContent.Blocks)ids.Add(b.Id);ids.AddRange(new[]{135,136,138,139,140,141,142,143,144,145,146,147,148,149,150,151,152,153,154,155,156,157,158,159,160,161,162,163,164,165,170,171,172,173,174,175,176,177,190,191,192,193,194,195});return ids.ToArray();}
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
