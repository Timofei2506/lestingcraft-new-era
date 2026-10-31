using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using LestingCraft.Presentation;
using LestingCraft.Runtime;
using LestingCraft.World;

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
        [Test] public void StandardHealthBarTracksGlobalHealth()
        {
            var obj=new GameObject("Health test",typeof(RectTransform));
            try
            {
                var body=obj.AddComponent<HealthSilhouette>();body.Build();Assert.AreEqual(1,body.Zones.Length);
                var state=new LestingCraft.Gameplay.SurvivalState();state.Respawn();state.DamagePart(50,LestingCraft.Gameplay.GameMode.Survival,LestingCraft.Gameplay.BodyPart.LeftLeg);body.SetBody(state);Assert.AreEqual(state.health,body.Health);
                Assert.AreEqual(.5f,body.Zones[0].rectTransform.anchorMax.x,.001f);
                Assert.IsFalse(state.dead);Assert.AreEqual(1,state.body.Movement);Assert.IsTrue(state.Treat());Assert.AreEqual(72,state.health);
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
        [Test] public void TwoBlockOpeningInRealVoxelColliderIsTraversable()
        {
            var root=new GameObject("Voxel tunnel fixture");Mesh mesh=null;
            try
            {
                var chunk=new ChunkData();
                for(int z=0;z<12;z++)for(int x=0;x<3;x++)chunk.SetBlock(x,0,z,1);
                for(int z=2;z<10;z++)
                {
                    for(int x=0;x<3;x++)chunk.SetBlock(x,3,z,1);
                    for(int y=1;y<3;y++){chunk.SetBlock(0,y,z,1);chunk.SetBlock(2,y,z,1);}
                }
                mesh=ChunkMesher.Build(chunk,0,0,(x,y,z)=>0).CreateMesh(true);
                root.AddComponent<MeshCollider>().sharedMesh=mesh;
                var obj=new GameObject("Voxel tunnel player");obj.transform.SetParent(root.transform);obj.transform.position=new Vector3(1.5f,1.025f,.5f);
                var controller=obj.AddComponent<CharacterController>();PlayerCapsule.Configure(controller);Physics.SyncTransforms();
                for(int n=0;n<100;n++){PlayerCapsule.UpdateStep(controller,Vector3.forward);controller.Move(new Vector3(0,-.02f,.06f));}
                Assert.Greater(obj.transform.position.z,6,"Player must cross actual meshed voxel lintel");
            }
            finally{Object.DestroyImmediate(root);if(mesh)Object.DestroyImmediate(mesh);Physics.SyncTransforms();}
        }
        [Test] public void HeldBlocksUseCompactFirstPersonScaleInsteadOfRemoteWeaponScale()
        {
            Assert.LessOrEqual(FirstPersonHands.HeldScale(1),.18f);
            Assert.LessOrEqual(FirstPersonHands.HeldScale(48),.18f);
            Assert.LessOrEqual(FirstPersonHands.HeldScale(101),.30f);
            Assert.Less(FirstPersonHands.HeldScale(153),RemoteWeaponVisual.Scale(153));
        }
        [Test] public void HandUsesAnIsolatedCameraAndDoesNotAddPhysicsColliders()
        {
            var cameraObject=new GameObject("Viewmodel test camera");var playerObject=new GameObject("Active hand owner");
            try
            {
                var camera=cameraObject.AddComponent<Camera>();int mask=camera.cullingMask;
                var controller=playerObject.AddComponent<FirstPersonController>();
                var hands=playerObject.AddComponent<FirstPersonHands>();hands.Initialize(controller,camera);
                Assert.IsNotNull(hands.ViewCamera);Assert.AreEqual(1<<30,hands.ViewCamera.cullingMask);
                Assert.AreEqual(0,camera.cullingMask&(1<<30));
                Assert.AreEqual(5,cameraObject.GetComponentsInChildren<Renderer>().Length);
                foreach(var filter in cameraObject.GetComponentsInChildren<MeshFilter>())
                    if(filter.sharedMesh)Assert.AreEqual(24,filter.sharedMesh.vertexCount,"Arm pieces must be cuboids, not curved primitives");
                Assert.AreEqual(0,cameraObject.GetComponentsInChildren<Collider>().Length);
                hands.Release();Assert.AreEqual(mask,camera.cullingMask);
                Assert.AreEqual(1,cameraObject.GetComponentsInChildren<Camera>().Length);
                hands.Release();Assert.AreEqual(mask,camera.cullingMask); // idempotent owner cleanup
                Object.DestroyImmediate(hands);
            }
            finally{Object.DestroyImmediate(playerObject);Object.DestroyImmediate(cameraObject);}
        }
        static void Box(GameObject root,string name,Vector3 position,Vector3 scale)
        {var obj=new GameObject(name,typeof(BoxCollider));obj.transform.SetParent(root.transform);obj.transform.position=position;obj.GetComponent<BoxCollider>().size=scale;}
    }
}
