using System;
using LestingCraft.World;
// Pure C# tables, no Unity stubs or editor required. Stdout is a parity fixture.
class ArchitectureCoreCheck
{
 static void Main()
 {
  int checks=0;var random=new Random(11031);
  for(int kind=0;kind<10;kind++)
  {
   int r=ArchitecturePlan.Radius[kind];
   for(int n=0;n<15000;n++)
   {
    int x=random.Next(-r,r+1),z=random.Next(-r,r+1),y=random.Next(-32,49);
    int id=ArchitecturePlan.Apply(kind,x,y,z);
    if(id<0||id>89)throw new Exception("Undefined material");
    Console.WriteLine(kind+","+x+","+y+","+z+","+id);checks++;
   }
  }
  if(StructurePlan.Radius.Length!=10||StructurePlan.Offsets.Length!=11)throw new Exception("Missing content-10 plans");
  int[] frozen={84,224,26,12,11};for(int i=0;i<5;i++)if((StructurePlan.Offsets[i+1]-StructurePlan.Offsets[i])/7!=frozen[i])throw new Exception("Legacy plan drift");
  Console.Error.WriteLine("PASS "+checks+" architecture table probes; ten plans, frozen legacy box counts");
 }
}
