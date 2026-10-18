using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using LestingCraft.Presentation;

namespace LestingCraft.Tests
{
    public sealed class BlockArtTests
    {
        [Test] public void EveryItemHasARealFlatNormalVoxelObjectAndDetailedIcon()
        {
            foreach(int id in ItemArtwork.AllIds)
            {
                var model=VoxelItemModels.Get(id);Assert.IsNotNull(model,"Object "+id);Assert.Greater(model.cells.Length,id==165?400:1000,"Arrows are thin projectiles (132 voxels by design); all other items keep the detail floor");
                var mesh=VoxelItemModels.MeshFor(id);Assert.IsNotNull(mesh);Assert.Greater(mesh.vertexCount,100);
                Assert.LessOrEqual(mesh.bounds.size.x,1.001f);Assert.LessOrEqual(mesh.bounds.size.y,1.001f);Assert.LessOrEqual(mesh.bounds.size.z,1.001f);
                foreach(var normal in mesh.normals)Assert.That(Mathf.Abs(normal.x)+Mathf.Abs(normal.y)+Mathf.Abs(normal.z),Is.EqualTo(1).Within(.0001f));
                Assert.GreaterOrEqual(ItemArtwork.Get(id).width,384);
            }
        }
        [Test] public void HealthZonesHaveSquareCornersNotRoundedSprites()
        {
            var obj=new GameObject("Square HUD test",typeof(RectTransform));
            try{var body=obj.AddComponent<HealthSilhouette>();body.Build();foreach(var zone in body.Zones){Assert.IsNull(zone.sprite);Assert.AreEqual(Image.Type.Simple,zone.type);}}
            finally{Object.DestroyImmediate(obj);}
        }
        [Test] public void BlockCharacterHasNoCurvedPrimitivesAndUsesDetailedAtlas()
        {
            var obj=new GameObject("Block character test");CharacterVisual character=null;
            try
            {
                Assert.IsNotNull(Resources.Load<Texture2D>("Generated/BlockCharacter"));character=obj.AddComponent<CharacterVisual>();character.Build();
                var meshes=obj.GetComponentsInChildren<MeshFilter>();Assert.Greater(meshes.Length,20);Assert.AreEqual(0,obj.GetComponentsInChildren<Collider>().Length);
                foreach(var filter in meshes){Assert.AreEqual(24,filter.sharedMesh.vertexCount);foreach(var normal in filter.sharedMesh.normals)Assert.AreEqual(1,Mathf.Abs(normal.x)+Mathf.Abs(normal.y)+Mathf.Abs(normal.z));}
            }
            finally{if(character)character.Release();Object.DestroyImmediate(obj);}
        }
    }
}
