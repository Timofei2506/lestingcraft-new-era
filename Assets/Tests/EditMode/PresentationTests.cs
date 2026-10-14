using NUnit.Framework;
using UnityEngine;
using LestingCraft.World;
using LestingCraft.Presentation;

namespace LestingCraft.Tests
{
    public sealed class PresentationTests
    {
        [Test] public void TexturedFacesHaveConsistentVertexChannels()
        {
            var chunk=new ChunkData();chunk.SetBlock(3,3,3,BlockRegistry.StoneId);
            var data=ChunkMesher.Build(chunk,0,0,(x,y,z)=>0);
            Assert.AreEqual(24,data.Vertices.Count);Assert.AreEqual(24,data.UVs.Count);
            Assert.AreEqual(24,data.Tangents.Count);Assert.AreEqual(24,data.Colors.Count);
            foreach(var uv in data.UVs){Assert.That(uv.x,Is.InRange(0,1));Assert.That(uv.y,Is.InRange(0,1));Assert.AreEqual(0,uv.z);}
            foreach(var tangent in data.Tangents)Assert.AreEqual(1,new Vector3(tangent.x,tangent.y,tangent.z).magnitude,.001);
        }
        [Test] public void GrassHasTopSideAndDirtUnderside()
        {
            Assert.AreEqual(2,SurfaceLayers.ForFace(BlockRegistry.GrassId,2));
            Assert.AreEqual(1,SurfaceLayers.ForFace(BlockRegistry.GrassId,3));
            Assert.AreEqual(3,SurfaceLayers.ForFace(BlockRegistry.GrassId,0));
        }
        [Test] public void WoodEndGrainOnlyOnHorizontalFaces()
        {Assert.AreEqual(5,SurfaceLayers.ForFace(4,2));Assert.AreEqual(5,SurfaceLayers.ForFace(4,3));Assert.AreEqual(4,SurfaceLayers.ForFace(4,0));}
        [Test] public void WaterUsesSeparateTransparentSubmeshWithoutCollider()
        {
            var chunk=new ChunkData();chunk.SetBlock(2,4,2,7);chunk.SetBlock(5,4,5,1);
            var data=ChunkMesher.Build(chunk,0,0,(x,y,z)=>0);
            Assert.AreEqual(36,data.WaterTriangles.Count);Assert.AreEqual(36,data.OpaqueTriangles.Count);Assert.AreEqual(36,data.ColliderTriangles.Count);
            var mesh=data.CreateMesh();Assert.AreEqual(2,mesh.subMeshCount);Object.DestroyImmediate(mesh);
        }
        [Test] public void MaterialLayerIdsAreValidForEveryBlockAndFace()
        {for(ushort id=1;id<=14;id++)for(int face=0;face<6;face++)Assert.That(SurfaceLayers.ForFace(id,face),Is.InRange(0,15));}
        [Test] public void DefaultPresetIsHighAndSmallAdjustmentIsBounded()
        {
            var settings=new VisualSettings();Assert.AreEqual(2048,settings.ShadowResolution);Assert.AreEqual(1,settings.RenderScale);Assert.IsTrue(settings.PostEffects);
            settings.RenderScale=.2f;settings.ViewDistance=999;settings.Clamp();Assert.AreEqual(.85f,settings.RenderScale);Assert.AreEqual(5,settings.ViewDistance);
        }
        [Test] public void LocalFontsContainCyrillicGlyphs()
        {
            var font=Resources.Load<Font>("UI/Manrope");Assert.IsNotNull(font);
            Assert.IsNotNull(Resources.Load<Font>("UI/Unbounded"));
        }
        [Test] public void GeneratedMaterialsAndArraysExist()
        {
            Assert.IsNotNull(Resources.Load<Material>("Generated/VoxelPBR"));
            Assert.IsNotNull(Resources.Load<Material>("Generated/Water"));
            var textures=Resources.Load<Texture2DArray>("Generated/Albedo");Assert.IsNotNull(textures);Assert.AreEqual(16,textures.depth);Assert.AreEqual(512,textures.width);
        }
    }
}
