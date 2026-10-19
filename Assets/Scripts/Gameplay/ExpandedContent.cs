using System;
namespace LestingCraft.Gameplay
{
 public readonly struct ExpandedBlock
 {public readonly int Id,Layer;public readonly string Name,Surface;public readonly float Hardness;public readonly bool Pick;public ExpandedBlock(int id,string name,string surface,float hardness,bool pick,int layer){Id=id;Name=name;Surface=surface;Hardness=hardness;Pick=pick;Layer=layer;}}
 public static class ExpandedContent
 {
  public const int MaxBlock=53,Version=3;
  public static readonly ExpandedBlock[] Blocks={new ExpandedBlock(19,"Угольная руда","ore-coal",2.5f,true,25),
new ExpandedBlock(20,"Железная руда","ore",3f,true,14),
new ExpandedBlock(21,"Медная руда","ore-copper",2.8f,true,24),
new ExpandedBlock(22,"Золотая руда","ore-gold",3.2f,true,26),
new ExpandedBlock(23,"Серебряная руда","ore-silver",3f,true,27),
new ExpandedBlock(24,"Оловянная руда","ore-tin",2.7f,true,28),
new ExpandedBlock(25,"Кварцевая жила","ore-quartz",2.6f,true,29),
new ExpandedBlock(26,"Серная руда","ore-sulfur",2.3f,true,30),
new ExpandedBlock(27,"Гранит","granite",2.5f,true,31),
new ExpandedBlock(28,"Известняк","limestone",1.6f,true,32),
new ExpandedBlock(29,"Сланец","slate",1.8f,true,33),
new ExpandedBlock(30,"Мрамор","marble",2.4f,true,34),
new ExpandedBlock(31,"Обсидиан","obsidian",6f,true,35),
new ExpandedBlock(32,"Гравий","gravel",0.7f,false,36),
new ExpandedBlock(33,"Глина","clay",0.6f,false,37),
new ExpandedBlock(34,"Ил","mud",0.5f,false,38),
new ExpandedBlock(35,"Замшелый камень","mossy-stone",1.5f,true,39),
new ExpandedBlock(36,"Булыжник","cobblestone",1.7f,true,40),
new ExpandedBlock(37,"Каменные кирпичи","stone-bricks",2f,true,41),
new ExpandedBlock(38,"Треснувшие кирпичи","cracked-bricks",1.8f,true,42),
new ExpandedBlock(39,"Глиняные кирпичи","clay-bricks",1.9f,true,43),
new ExpandedBlock(40,"Полированный камень","polished-stone",2.1f,true,44),
new ExpandedBlock(41,"Полированный гранит","polished-granite",2.6f,true,48),
new ExpandedBlock(42,"Полированный мрамор","polished-marble",2.5f,true,49),
new ExpandedBlock(43,"Песчаник","sandstone",1.2f,true,45),
new ExpandedBlock(44,"Стекло","crystal",0.4f,false,12),
new ExpandedBlock(45,"Угольный блок","coal-block",3f,true,46),
new ExpandedBlock(46,"Медный блок","copper-block",3.5f,true,47),new ExpandedBlock(47,"Алмазная руда","diamond-ore",5f,true,57),new ExpandedBlock(48,"Алмазный блок","diamond-facets",5.5f,true,58),
new ExpandedBlock(49,"Нефтяная скважина","basalt",2.5f,true,11),new ExpandedBlock(50,"Компрессор","metal-ingot",3f,true,51),
new ExpandedBlock(51,"Перегонный куб","obsidian",3.5f,true,35),new ExpandedBlock(52,"Криоустановка","snow",2.8f,true,15),
new ExpandedBlock(53,"Смеситель топлива","clay-bricks",2.6f,true,43)};
  public static bool Has(int id)=>id>=19&&id<=53;
  public static bool NeedsPick(int id)=>id==1||id>=10&&id<=13||id==17||Has(id)&&Blocks[id-19].Pick;
  public static int Smelt(int id)=>id>=20&&id<=26?140+id-20:id==6?44:id==33?39:id==135?138:0;
 }
}
