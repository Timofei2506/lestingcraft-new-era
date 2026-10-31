using System;
using System.Threading.Tasks;
using NUnit.Framework;
using LestingCraft.World;
namespace LestingCraft.Tests
{
    public sealed class ParallelMeshingTests
    {
        static ChunkData Fixture(int seed)
        {
            var c=new ChunkData();var random=new Random(seed);
            for(int z=0;z<16;z++)for(int x=0;x<16;x++)
            {
                int top=6+random.Next(10);
                for(int y=0;y<=top;y++)c.SetBlock(x,y,z,(ushort)(y==top?3:y%4==0?37:1));
                if((x+z)%5==0)c.SetBlock(x,top+1,z,44);
                if((x+z)%7==0)c.SetBlock(x,top+2,z,63);
            }
            return c;
        }
        static ChunkMeshData Build(ChunkData c,int seed)
        {
            return ChunkMesher.Build(c,0,0,(x,y,z)=>(ushort)0,seed,false,1,(x,y,z)=>(byte)15,null,true);
        }
        [Test] public void IndependentParallelBuildsMatchSerialGeometryAndDoNotMutateInput()
        {
            const int count=8;
            var chunks=new ChunkData[count];var tasks=new Task<ChunkMeshData>[count];var serial=new ChunkMeshData[count];
            for(int i=0;i<count;i++){int seed=i;chunks[i]=Fixture(seed);serial[i]=Build(chunks[i],seed);}
            for(int i=0;i<count;i++){int index=i;tasks[i]=Task.Run(()=>Build(chunks[index],index));}
            Assert.IsTrue(Task.WaitAll(tasks,60000),"Parallel mesh tasks did not complete");
            for(int i=0;i<count;i++)
            {
                var mesh=tasks[i].Result;var expected=serial[i];
                CollectionAssert.AreEqual(expected.Vertices,mesh.Vertices);
                CollectionAssert.AreEqual(expected.Normals,mesh.Normals);
                CollectionAssert.AreEqual(expected.UVs,mesh.UVs);
                CollectionAssert.AreEqual(expected.Colors,mesh.Colors);
                CollectionAssert.AreEqual(expected.Tangents,mesh.Tangents);
                CollectionAssert.AreEqual(expected.Triangles,mesh.Triangles);
                CollectionAssert.AreEqual(expected.ColliderTriangles,mesh.ColliderTriangles);
                CollectionAssert.AreEqual(expected.TransparentTriangles,mesh.TransparentTriangles);
                CollectionAssert.AreEqual(expected.IceTriangles,mesh.IceTriangles);
                var original=Fixture(i);
                for(int y=0;y<256;y++)for(int z=0;z<16;z++)for(int x=0;x<16;x++)
                    if(original.GetBlock(x,y,z)!=chunks[i].GetBlock(x,y,z))Assert.Fail("Meshing modified chunk data");
            }
        }
    }
}
