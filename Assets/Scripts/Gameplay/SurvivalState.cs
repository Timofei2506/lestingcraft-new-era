using System;
using UnityEngine;
namespace LestingCraft.Gameplay
{
    public enum WeatherKind { Clear,Cloudy,Rain,Snow,Storm }
    [Serializable] public sealed class SurvivalState
    {
        public BodyHealth body=new BodyHealth();
        public float health=100,hunger=100,stamina=100,temperature=37,wetness,air=100,comfort=80;public bool dead;
        public void Step(float seconds,GameMode mode,float air,float precipitation,bool sheltered,bool nearFire,bool sprinting,float insulation)
        {
            if(seconds<=0||dead||mode==GameMode.Creative)return;seconds=Mathf.Min(seconds,2);
            hunger=Mathf.Max(0,hunger-seconds*(sprinting?.07f:.035f));
            stamina=Mathf.Clamp(stamina+seconds*(sprinting?-14:10),0,100);
            wetness=Mathf.Clamp01(wetness+seconds*(sheltered||nearFire?-.035f:precipitation*.02f-.003f));
            float effective=air+insulation+(sheltered?7:0)+(nearFire?24:0)-wetness*14;
            float target=Mathf.Clamp(37+(effective-16)*.10f,30,41);
            temperature=Mathf.MoveTowards(temperature,target,seconds*.025f);
            if(hunger<=0)Damage(seconds*.35f,mode);
            if(temperature<34)Damage(seconds*(34-temperature)*.18f,mode);
            if(temperature>39)Damage(seconds*(temperature-39)*.15f,mode);
            body.Recover(seconds,hunger>75&&temperature>35&&temperature<38.5f);
            if(body.trauma<=0&&hunger>75&&temperature>35&&temperature<38.5f)health=Mathf.Min(100,health+seconds*.2f);
        }
        public void StepWater(float seconds,GameMode mode,bool underwater,bool immersed)
        {
            if(mode==GameMode.Creative){air=100;return;}if(dead||seconds<=0)return;
            if(immersed)wetness=1;air=Mathf.Clamp(air+seconds*(underwater?-8:25),0,100);
            if(underwater&&air<=0)Damage(seconds*5,mode);
        }
        public void Damage(float amount,GameMode mode){if(mode==GameMode.Creative||dead)return;body.Systemic(Mathf.Max(0,amount));health=Mathf.Max(0,health-Mathf.Max(0,amount));dead=health<=0;if(dead)health=0;}
        public void DamagePart(float amount,GameMode mode,BodyPart part){if(mode==GameMode.Creative||dead||amount<=0)return;body.Systemic(amount);health=Mathf.Max(0,health-amount);dead=health<=0;if(dead)health=0;}
        public bool Treat(){if(dead||health>=98)return false;health=Mathf.Min(100,health+22);body.trauma=Mathf.Min(body.trauma,5);return true;}
        public void Eat(float amount){if(!dead)hunger=Mathf.Min(100,hunger+amount);}
        public void Respawn(){body=new BodyHealth();body.Normalize(100);health=100;hunger=70;temperature=37;wetness=0;stamina=100;air=100;dead=false;}
        public void Normalize(){if(body==null)body=new BodyHealth();body.Normalize(health);air=Mathf.Clamp(air,0,100);health=Mathf.Clamp(health,0,100);hunger=Mathf.Clamp(hunger,0,100);stamina=Mathf.Clamp(stamina,0,100);temperature=Mathf.Clamp(temperature,25,43);wetness=Mathf.Clamp01(wetness);dead=health<=0;}
    }
    public readonly struct WeatherSample
    {
        public readonly WeatherKind Kind;public readonly float Precipitation,Air,Clouds,Wind;
        public WeatherSample(WeatherKind kind,float rain,float air,float clouds,float wind){Kind=kind;Precipitation=rain;Air=air;Clouds=clouds;Wind=wind;}
        public static WeatherSample At(int seed,int realm,double clock)
        {
            if(realm==1)return new WeatherSample(WeatherKind.Clear,0,-110,0,0);
            if(realm==2)return new WeatherSample(WeatherKind.Clear,0,-55,.08f,.22f);
            if(realm==3)return new WeatherSample(WeatherKind.Cloudy,0,460,.90f,.16f);
            if(realm==4)return new WeatherSample(WeatherKind.Clear,0,-85,0,0);
            double cycle=clock/1200.0;float hour=(float)(cycle-Math.Floor(cycle))*24;
            uint h=unchecked((uint)(seed^((int)(clock/180)*1597334677)^realm*381201));h^=h>>16;h*=2246822519u;h^=h>>13;
            float chance=(h&65535)/65535f;float baseline=18;
            if(realm==0&&((int)cycle%8)>=5)baseline-=13;
            float air=baseline+Mathf.Sin((hour-6)*Mathf.PI/12)*7;
            if(clock<480&&realm==0)return new WeatherSample(WeatherKind.Clear,0,air,.28f,.15f);
            if(chance<.35f)return new WeatherSample(WeatherKind.Clear,0,air,.28f,.16f);
            if(chance<.58f)return new WeatherSample(WeatherKind.Cloudy,0,air,.56f,.35f);
            bool snow=air<2;bool storm=chance>.88f;
            return new WeatherSample(snow?WeatherKind.Snow:storm?WeatherKind.Storm:WeatherKind.Rain,storm?1:.65f,air-3,storm?.90f:.75f,storm?1:.5f);
        }
    }
    [Serializable] public sealed class PlayerSave
    {
        public int claimedStoryRewards;public string[] claimedAnimalLoot=Array.Empty<string>();public Inventory inventory=new Inventory();public SurvivalState survival=new SurvivalState();public bool equippedCoat;public int milestones;public ItemStack[] graveExtras=Array.Empty<ItemStack>();public ItemStack[] grave=Array.Empty<ItemStack>();public float graveX,graveY,graveZ;
        public bool hasBedRespawn;public int bedX,bedY,bedZ;public int dreamsExperienced;
        public int dreamVisits{get=>dreamsExperienced;set=>dreamsExperienced=value;}
        public void Normalize(){if(claimedAnimalLoot==null)claimedAnimalLoot=Array.Empty<string>();if(claimedAnimalLoot.Length>256)Array.Resize(ref claimedAnimalLoot,256);if(graveExtras==null)graveExtras=Array.Empty<ItemStack>();if(graveExtras.Length!=0&&graveExtras.Length!=13&&graveExtras.Length!=21&&graveExtras.Length!=22)graveExtras=Array.Empty<ItemStack>();for(int i=0;i<graveExtras.Length;i++)graveExtras[i]=Inventory.Clean(graveExtras[i]);if(grave==null)grave=Array.Empty<ItemStack>();if(grave.Length>0){var graveInventory=new Inventory{slots=grave};graveInventory.Normalize();grave=graveInventory.slots;}if(inventory==null)inventory=new Inventory();inventory.Normalize();if(equippedCoat&&inventory.equipment[1].count==0){int coat=Array.FindIndex(inventory.slots,s=>s.item==104&&s.count>0);if(coat>=0)Equipment.Equip(inventory,coat);}equippedCoat=false;if(survival==null)survival=new SurvivalState();survival.Normalize();}
    }
    [Serializable] public sealed class SessionSave
    {
        public LivingSave living=new LivingSave();
        public int schema=3,contentVersion=9,seed,mode;public LestingCraft.Story.StoryState story;public WildlifeSave wildlife=new WildlifeSave();public double clock=420;public CampaignProgress campaign=new CampaignProgress();public PlayerSave player=new PlayerSave();
        public bool launchPlaced;public int launchX,launchZ;
        public int activeDimension,defeatedPlanetBossMask;public bool dockedAtStation;
        public int activeGalaxy,activeSystem;
        public SavedBlock[] blocks=Array.Empty<SavedBlock>();
    }
    [Serializable] public sealed class SavedBlock {public int realm,x,y,z,block;}
    public static class LocalSave
    {
        const string Key="LC.Session.V3",LegacyKey="LC.Session.V2";
        public static void Store(SessionSave save){PlayerPrefs.SetString(Key,JsonUtility.ToJson(save));PlayerPrefs.Save();}
        public static SessionSave Load()
        {
            try{
                string raw=PlayerPrefs.GetString(PlayerPrefs.HasKey(Key)?Key:LegacyKey,"");
                var data=JsonUtility.FromJson<SessionSave>(raw);
                if(data==null||(data.schema!=2&&data.schema!=3)||!GameRules.IsValid(data.mode))return null;
                if(data.schema==3&&!raw.Contains("\"contentVersion\""))return null;
                if(data.schema==2){data.contentVersion=1;data.schema=3;}
                if(data.contentVersion<1||data.contentVersion>9)return null;
                if(data.player==null)return null;
                if(data.launchPlaced&&(data.launchX < -320||data.launchX > 320||data.launchZ < -260||data.launchZ > 320))return null;
                data.activeDimension=Mathf.Clamp(data.activeDimension,0,14);
                data.player.Normalize();
                if(data.campaign==null)data.campaign=new CampaignProgress();
                if(data.blocks==null)data.blocks=Array.Empty<SavedBlock>();
                if(data.blocks.Length>8192)return null;
                int maxBlock=data.contentVersion>=9?89:data.contentVersion>=8?69:data.contentVersion>=7?53:data.contentVersion>=5?48:46;
                foreach(var b in data.blocks)if(b==null||b.realm<0||b.realm>14||b.y<=0||b.y>=256||Math.Abs((long)b.x)>=32000||Math.Abs((long)b.z)>=32000||b.block<0||b.block>maxBlock||b.block==9)return null;
                return data;
            }catch{return null;}
        }
        public static bool Exists=>PlayerPrefs.HasKey(Key)||PlayerPrefs.HasKey(LegacyKey);
    }
}
