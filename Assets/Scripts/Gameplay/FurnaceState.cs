using System;
namespace LestingCraft.Gameplay
{
    /// <summary>Personal, persistent cooking queue. World block and proximity gate ticking; not a shared chest.</summary>
    [Serializable] public sealed class FurnaceState
    {
        public ItemStack input=new ItemStack(),fuel=new ItemStack(),output=new ItemStack();
        public float burn,progress;public int recipe,x,y,z,realm;
        public const float CookSeconds=8;
        public static int Result(int id)=>id==110?112:id==114?111:id==4?123:ExpandedContent.Smelt(id);
        public static float FuelSeconds(int id)=>id==4?18:id==120?6:id==121?3:id==123||id==147?40:0;
        public bool Step(float dt)
        {
            if(dt<=0)return false;dt=Math.Min(dt,2);bool changed=false;
            // Idle furnaces still use up already-lit fuel, but never consume fresh fuel when blocked.
            int result=Result(input.item);
            bool canCook=input.count>0&&result>0&&(output.count==0||output.item==result&&output.count<Items.Get(result).MaxStack);
            if(!canCook){burn=Math.Max(0,burn-dt);if(progress!=0){progress=0;changed=true;}return changed;}
            if(recipe!=input.item){recipe=input.item;progress=0;}
            float remaining=dt;
            while(remaining>.00001f)
            {
                if(burn<=0)
                {
                    float seconds=FuelSeconds(fuel.item);if(fuel.count<=0||seconds<=0)break;
                    burn=seconds;fuel.count--;if(fuel.count==0)fuel=new ItemStack();changed=true;
                }
                float used=Math.Min(remaining,burn);burn-=used;remaining-=used;progress+=used;changed=true;
                if(progress>=CookSeconds)
                {
                    progress-=CookSeconds;input.count--;if(input.count==0)input=new ItemStack();
                    if(output.count==0)output=new ItemStack{item=result};output.count++;
                    break; // max dt=2 means at most one finished operation per step.
                }
            }
            return changed;
        }
        public FurnaceState Clone()=>new FurnaceState{input=input.Clone(),fuel=fuel.Clone(),output=output.Clone(),burn=burn,progress=progress,recipe=recipe,x=x,y=y,z=z,realm=realm};
        public void Normalize()
        {
            input=Inventory.Clean(input);fuel=Inventory.Clean(fuel);output=Inventory.Clean(output);
            burn=Finite(burn)?Math.Max(0,Math.Min(40,burn)):0;progress=Finite(progress)?Math.Max(0,Math.Min(CookSeconds,progress)):0;
            if(Result(recipe)==0){recipe=0;progress=0;}if(realm<0||realm>4)realm=0;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
