using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using LestingCraft.Presentation;
using LestingCraft.Runtime;

namespace LestingCraft.Tests
{
    public sealed class PlayerFeedbackTests
    {
        [Test] public void EveryNonemptyItemHasAnExplicitIcon()
        {
            foreach(int id in ItemArtwork.AllIds)
            {
                var texture=Resources.Load<Texture2D>("Generated/Icons/Item"+id);
                Assert.IsNotNull(texture,"Missing item "+id);Assert.GreaterOrEqual(texture.width,96);Assert.IsNotNull(ItemArtwork.Get(id));
            }
            Assert.IsNull(ItemArtwork.Get(0));
        }
        [Test] public void ClearWeatherDoesNotObscureNearbyBlocks()
        {
            Assert.Less(AtmosphereProfile.OpacityAt(20,0),.01);
            Assert.Less(AtmosphereProfile.OpacityAt(60,0),.025);
            Assert.Greater(AtmosphereProfile.Density(1),AtmosphereProfile.Density(0));
            Assert.Greater(AtmosphereProfile.Density(0,1),AtmosphereProfile.Density(1));
        }
        [Test] public void SixBodyZonesVisualizeTheSameExistingHealth()
        {
            var obj=new GameObject("Health test",typeof(RectTransform));
            try
            {
                var body=obj.AddComponent<HealthSilhouette>();body.Build();Assert.AreEqual(6,body.Zones.Length);
                body.SetHealth(25);Assert.AreEqual(25,body.Health);
                foreach(var image in body.Zones)Assert.AreEqual(body.Zones[0].color,image.color);
                body.SetHealth(-5);Assert.AreEqual(0,body.Health);
            }
            finally{Object.DestroyImmediate(obj);}
        }
        [TestCase(2,true)] [TestCase(1,false)]
        public void CapsulePhysicallyFitsTwoBlockTunnelButNotOne(int opening,bool shouldPass)
        {
            var root=new GameObject("Tunnel collision fixture");
            try
            {
                Box(root,"Floor",new Vector3(0,-.5f,0),new Vector3(6,1,12));
                Box(root,"Roof",new Vector3(0,opening+.5f,1.5f),new Vector3(3,1,5));
                Box(root,"Left wall",new Vector3(-1,1,1.5f),new Vector3(1,4,5));
                Box(root,"Right wall",new Vector3(1,1,1.5f),new Vector3(1,4,5));
                var player=new GameObject("Capsule");player.transform.SetParent(root.transform);player.transform.position=new Vector3(0,.025f,-2.0f);
                var cc=player.AddComponent<CharacterController>();PlayerCapsule.Configure(cc);Physics.SyncTransforms();
                for(int n=0;n<80;n++){PlayerCapsule.UpdateStep(cc,Vector3.forward);cc.Move(new Vector3(0,-.02f,.06f));}
                if(shouldPass)Assert.Greater(player.transform.position.z,2,"Capsule blocked by a two-block lintel");
                else Assert.Less(player.transform.position.z,-.7f,"One-block opening must remain impassable");
            }
            finally{Object.DestroyImmediate(root);Physics.SyncTransforms();}
        }
        static void Box(GameObject root,string name,Vector3 position,Vector3 scale)
        {var obj=new GameObject(name,typeof(BoxCollider));obj.transform.SetParent(root.transform);obj.transform.position=position;obj.GetComponent<BoxCollider>().size=scale;}
    }
}
