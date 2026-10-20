namespace LestingCraft.Gameplay
{
 public readonly struct SettlerAppearance
 {
  public readonly int Skin,Coat,Hair,Headwear,Detail;
  public SettlerAppearance(int skin,int coat,int hair,int headwear,int detail)
  {Skin=skin;Coat=coat;Hair=hair;Headwear=headwear;Detail=detail;}

  public static SettlerAppearance For(string actorId,int job)
  {
   uint h = 2166136261u ^ ((uint)(job + 11) * 2654435761u);
   if (actorId != null)
    for (int i = 0; i < actorId.Length; i++) { h ^= actorId[i]; h *= 16777619u; }
   h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
   int head = job == 3 ? 4 : job == 6 ? 5 : job == 7 ? 6 : job == 9 ? 7 : (int)((h >> 12) % 6);
   return new SettlerAppearance(
    (int)(h % 8),
    (int)(((h >> 3) + (uint)job) % 10),
    (int)((h >> 8) % 8),
    head,
    (int)((h >> 16) % 6));
  }
 }
}
