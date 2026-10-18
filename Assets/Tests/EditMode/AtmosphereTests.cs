using NUnit.Framework;
using UnityEngine;
using LestingCraft.Presentation;
using LestingCraft.World;

namespace LestingCraft.Tests
{
    public sealed class AtmosphereTests
    {
        [Test] public void MiddayIsBrighterThanMoonlitNight()
        {
            var noon=AtmosphereLighting.At(600,Dimension.Lestingia,.28f,0);var night=AtmosphereLighting.At(0,Dimension.Lestingia,.28f,0);
            Assert.Greater(noon.DirectIntensity,night.DirectIntensity*5);Assert.Greater(noon.Daylight,.99f);Assert.Less(night.Daylight,.01f);
            Assert.IsFalse(noon.MoonIsPrimary);Assert.IsTrue(night.MoonIsPrimary);Assert.Greater(night.MoonDirection.y,0);
        }
        [Test] public void LightingLoopsWithoutADayBoundaryJump()
        {
            var a=AtmosphereLighting.At(0,Dimension.Lestingia,.5f,.2f);var b=AtmosphereLighting.At(1200,Dimension.Lestingia,.5f,.2f);
            Assert.Less(Vector3.Distance(a.SunDirection,b.SunDirection),.00001f);Assert.AreEqual(a.Zenith,b.Zenith);Assert.AreEqual(a.DirectIntensity,b.DirectIntensity);
        }
        [Test] public void SunriseIsWarmerThanMidday()
        {
            var dawn=AtmosphereLighting.At(310,Dimension.Lestingia,.28f,0);var noon=AtmosphereLighting.At(600,Dimension.Lestingia,.28f,0);
            Assert.Less(dawn.SunColor.b/dawn.SunColor.r,noon.SunColor.b/noon.SunColor.r);
        }
        [Test] public void StormReducesDirectLight()
        {Assert.Less(AtmosphereLighting.At(600,Dimension.Lestingia,.9f,1).DirectIntensity,AtmosphereLighting.At(600,Dimension.Lestingia,.28f,0).DirectIntensity);}
        [Test] public void DimensionPalettesRemainDifferent()
        {Assert.AreNotEqual(AtmosphereLighting.At(600,Dimension.Lestingia,.28f,0).Zenith,AtmosphereLighting.At(600,Dimension.EchoCrypt,.28f,0).Zenith);}
        [TestCase(-1200)] [TestCase(0)] [TestCase(300)] [TestCase(1200000)]
        public void LightDirectionsAreUnitAndOpposite(double clock)
        {
            var frame=AtmosphereLighting.At(clock,Dimension.Lestingia,0,0);
            Assert.AreEqual(1,frame.SunDirection.magnitude,.0001f);Assert.Less((frame.MoonDirection+frame.SunDirection).sqrMagnitude,.0001f);
        }
        static ChunkData Meadow()
        {var chunk=new ChunkData();for(int z=0;z<16;z++)for(int x=0;x<16;x++)chunk.SetBlock(x,20,z,BlockRegistry.GrassId);return chunk;}
        [Test] public void GrassIsSeededBatchedAndNotCollidable()
        {
            var a=ChunkMesher.Build(Meadow(),-16,32,(x,y,z)=>0,2506,true);
            var b=ChunkMesher.Build(Meadow(),-16,32,(x,y,z)=>0,2506,true);
            var plain=ChunkMesher.Build(Meadow(),-16,32,(x,y,z)=>0);
            Assert.Greater(a.DetailTriangles.Count,0);Assert.AreEqual(a.DetailTriangles.Count,b.DetailTriangles.Count);
            CollectionAssert.AreEqual(a.Vertices,b.Vertices);CollectionAssert.AreEqual(a.ColliderTriangles,plain.ColliderTriangles);
            Assert.AreEqual(a.Vertices.Count,a.Normals.Count);Assert.AreEqual(a.Vertices.Count,a.UVs.Count);
            Assert.AreEqual(a.Vertices.Count,a.Tangents.Count);Assert.AreEqual(a.Vertices.Count,a.Colors.Count);
            Assert.Less(a.DetailTriangles.Count,10000);
        }
        [Test] public void DecorationsChangeWithSeedAndNeverAppearOnStone()
        {
            var a=ChunkMesher.Build(Meadow(),0,0,(x,y,z)=>0,1,true);var b=ChunkMesher.Build(Meadow(),0,0,(x,y,z)=>0,902,true);
            CollectionAssert.AreNotEqual(a.Vertices,b.Vertices);
            var stone=new ChunkData();stone.SetBlock(2,20,2,BlockRegistry.StoneId);
            Assert.AreEqual(0,ChunkMesher.Build(stone,0,0,(x,y,z)=>0,1,true).DetailTriangles.Count);
        }
        [Test] public void GrassDoesNotGrowInsideCoveringWater()
        {
            var chunk=Meadow();for(int z=0;z<16;z++)for(int x=0;x<16;x++)chunk.SetBlock(x,21,z,BlockRegistry.WaterId);
            Assert.AreEqual(0,ChunkMesher.Build(chunk,0,0,(x,y,z)=>0,2506,true).DetailTriangles.Count);
        }
        [Test] public void GrassWindingMatchesItsStoredNormal()
        {
            var mesh=ChunkMesher.Build(Meadow(),0,0,(x,y,z)=>0,2506,true);
            int a=mesh.DetailTriangles[0],b=mesh.DetailTriangles[1],c=mesh.DetailTriangles[2];
            Assert.Greater(Vector3.Dot(Vector3.Cross(mesh.Vertices[b]-mesh.Vertices[a],mesh.Vertices[c]-mesh.Vertices[a]).normalized,mesh.Normals[a]),.5f);
        }
        [TestCase("AvatarLit")] [TestCase("VoxelObjects")] [TestCase("CrystalGlass")] [TestCase("VoxelPBR")] [TestCase("Grass")] [TestCase("Water")] [TestCase("AtmosphericSky")] [TestCase("VolumetricAtmosphere")]
        public void PresentationShadersImportWithoutReportedErrors(string name)
        {
            var shader=Shader.Find("LestingCraft/"+name);Assert.IsNotNull(shader);
            var messages=UnityEditor.ShaderUtil.GetShaderMessages(shader);
            var report=new System.Text.StringBuilder();
            foreach(var message in messages)report.AppendLine(message.file+":"+message.line+" "+message.message);
            Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(shader),"Shader import error: "+name+"\n"+report);
        }
        [Test] public void GeneratedGrassHasTheExpectedShaderAndTextureArray()
        {
            var material=Resources.Load<Material>("Generated/Grass");Assert.IsNotNull(material);Assert.AreEqual("LestingCraft/Grass",material.shader.name);
            Assert.IsNotNull(material.GetTexture("_AlbedoArray"));Assert.IsNotNull(Resources.Load<Material>("Generated/Sky").GetTexture("_CloudNoise"));
        }
    }
}
