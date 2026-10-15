using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Runtime
{
    public sealed class VoxelWorld : MonoBehaviour
    {
        sealed class View { public ChunkData Data; public GameObject Object; public Mesh Visual, Collision; }
        readonly Dictionary<Vector2Int,View> chunks = new Dictionary<Vector2Int,View>();
        readonly Dictionary<Vector3Int,ushort> edits = new Dictionary<Vector3Int,ushort>();
        readonly HashSet<Vector2Int> dirty = new HashSet<Vector2Int>();
        Material material,waterMaterial,grassMaterial;
        NativeArray<ushort> pendingBuffer;
        JobHandle pendingJob;
        public int Seed { get; private set; }
        public Dimension Dimension { get; private set; }
        public bool Online { get; private set; }
        public bool Ready { get; private set; }
        public int LoadedChunks => chunks.Count;
        public Transform Player;
        int Radius => LestingCraft.Presentation.VisualSettings.Current.ViewDistance;
        public void Initialize(int seed, Dimension dimension, bool online)
        {
            Seed=seed;Dimension=dimension;Online=online;
            material=new Material(Resources.Load<Material>("Generated/VoxelPBR"));
            waterMaterial=new Material(Resources.Load<Material>("Generated/Water"));
            grassMaterial=new Material(Resources.Load<Material>("Generated/Grass"));
            RenderSettings.fog=false;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogStartDistance=Radius*10;RenderSettings.fogEndDistance=Radius*16;
            StartCoroutine(Stream());
        }
        public static int ChunkCoordinate(int value) => Mathf.FloorToInt(value/16f);
        public bool Contains(int x,int y,int z) => y>=0&&y<256&&Mathf.Abs(x)<32000&&Mathf.Abs(z)<32000&&(!Online||(x>=-64&&x<64&&z>=-64&&z<64));
        public bool Loaded(Vector3 p) => chunks.ContainsKey(new Vector2Int(ChunkCoordinate(Mathf.FloorToInt(p.x)),ChunkCoordinate(Mathf.FloorToInt(p.z))));
        public ushort GetBlock(int x,int y,int z)
        {
            if(!Contains(x,y,z))return 0;
            var pos=new Vector3Int(x,y,z);if(edits.TryGetValue(pos,out var value))return value;
            var key=new Vector2Int(ChunkCoordinate(x),ChunkCoordinate(z));
            if(chunks.TryGetValue(key,out var view))return view.Data.GetBlock(x-key.x*16,y,z-key.y*16);
            return TerrainGenerator.Sample(x,y,z,Seed,Dimension);
        }
        public bool CanAcceptEdit(Vector3Int cell)=>edits.ContainsKey(cell)||edits.Count<(Online?4096:8192);
        public void Edit(int x,int y,int z,ushort block)
        {
            if(!Contains(x,y,z)||y==0||block==BlockRegistry.BedrockId||block>15)return;
            var pos=new Vector3Int(x,y,z);edits[pos]=block;
            var key=new Vector2Int(ChunkCoordinate(x),ChunkCoordinate(z));
            if(chunks.TryGetValue(key,out var v))v.Data.SetBlock(x-key.x*16,y,z-key.y*16,block);
            dirty.Add(key);
            if(x-key.x*16==0)dirty.Add(key+Vector2Int.left);
            if(x-key.x*16==15)dirty.Add(key+Vector2Int.right);
            if(z-key.y*16==0)dirty.Add(key+Vector2Int.down);
            if(z-key.y*16==15)dirty.Add(key+Vector2Int.up);
        }
        IEnumerator Stream()
        {
            while(true)
            {
                RenderSettings.fogStartDistance=Mathf.Max(18,Radius*10);RenderSettings.fogEndDistance=Radius*16;
                var center=Player?new Vector2Int(ChunkCoordinate(Mathf.FloorToInt(Player.position.x)),ChunkCoordinate(Mathf.FloorToInt(Player.position.z))):Vector2Int.zero;
                // Center first, then rings; each generation job can run off the main thread.
                for(int ring=0;ring<=Radius;ring++) for(int dz=-ring;dz<=ring;dz++)for(int dx=-ring;dx<=ring;dx++)
                {
                    if(Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dz))!=ring)continue;
                    var key=center+new Vector2Int(dx,dz);
                    if(chunks.ContainsKey(key)||!Contains(key.x*16,1,key.y*16))continue;
                    var buffer=new NativeArray<ushort>(ChunkData.BlockCount,Allocator.Persistent);
                    var job=new GenerateChunkJob {OriginX=key.x*16,OriginZ=key.y*16,Seed=Seed,Dimension=Dimension,Blocks=buffer};
                    var handle=job.Schedule(256,16);
                    pendingBuffer=buffer;pendingJob=handle;
                    // Complete and dispose even when the world is destroyed mid-generation.
                    try
                    {
                        while(!handle.IsCompleted)yield return null;
                        handle.Complete();
                        var view=new View {Data=new ChunkData(buffer.ToArray()),Object=new GameObject($"Chunk {key.x} {key.y}")};
                        view.Object.transform.SetParent(transform,false);view.Object.transform.localPosition=new Vector3(key.x*16,0,key.y*16);
                        view.Object.AddComponent<MeshFilter>();var renderer=view.Object.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{material,waterMaterial,grassMaterial};renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;renderer.receiveShadows=true;
                        view.Object.AddComponent<MeshCollider>();chunks.Add(key,view);
                        foreach(var edit in edits)if(ChunkCoordinate(edit.Key.x)==key.x&&ChunkCoordinate(edit.Key.z)==key.y)
                            view.Data.SetBlock(edit.Key.x-key.x*16,edit.Key.y,edit.Key.z-key.y*16,edit.Value);
                        Rebuild(key,view);
                    }
                    finally {handle.Complete();if(pendingBuffer.IsCreated){pendingBuffer.Dispose();pendingBuffer=default;}}
                    yield return null;
                }
                Ready=true;
                var remove=new List<Vector2Int>();
                foreach(var pair in chunks)
                    if(Mathf.Abs(pair.Key.x-center.x)>Radius+1||Mathf.Abs(pair.Key.y-center.y)>Radius+1)remove.Add(pair.Key);
                foreach(var key in remove){DisposeView(chunks[key]);chunks.Remove(key);}
                if(dirty.Count>0)
                {
                    var keys=new List<Vector2Int>(dirty);dirty.Clear();
                    foreach(var key in keys)if(chunks.TryGetValue(key,out var v)){Rebuild(key,v);yield return null;}
                }
                yield return null;
            }
        }
        public LestingCraft.Gameplay.SavedBlock[] ExportEdits()
        {
            var result=new List<LestingCraft.Gameplay.SavedBlock>();
            foreach(var edit in edits)result.Add(new LestingCraft.Gameplay.SavedBlock{realm=(int)Dimension,x=edit.Key.x,y=edit.Key.y,z=edit.Key.z,block=edit.Value});return result.ToArray();
        }
        public bool NearCampfire(Vector3 point,float radius=6)
        {foreach(var edit in edits)if(edit.Value==15&&Vector3.Distance(point,(Vector3)edit.Key+Vector3.one*.5f)<radius)return true;return false;}
        public List<Vector3> NearbyFires(Vector3 point)
        {var found=new List<Vector3>();foreach(var edit in edits)if(edit.Value==15&&Vector3.Distance(point,(Vector3)edit.Key)<24)found.Add((Vector3)edit.Key+new Vector3(.5f,1,.5f));return found;}
        void Rebuild(Vector2Int key,View v)
        {
            var data=ChunkMesher.Build(v.Data,key.x*16,key.y*16,GetBlock,Seed,Dimension==LestingCraft.World.Dimension.Lestingia);
            var mesh=data.CreateMesh();var collider=data.CreateMesh(true);
            v.Object.GetComponent<MeshFilter>().sharedMesh=mesh;
            v.Object.GetComponent<MeshCollider>().sharedMesh=collider;
            if(v.Visual)Destroy(v.Visual);if(v.Collision)Destroy(v.Collision);v.Visual=mesh;v.Collision=collider;
        }
        void DisposeView(View v){if(v.Visual)Destroy(v.Visual);if(v.Collision)Destroy(v.Collision);if(v.Object)Destroy(v.Object);}
        void OnDestroy(){StopAllCoroutines();pendingJob.Complete();if(pendingBuffer.IsCreated){pendingBuffer.Dispose();pendingBuffer=default;}foreach(var v in chunks.Values)DisposeView(v);if(material)Destroy(material);if(waterMaterial)Destroy(waterMaterial);if(grassMaterial)Destroy(grassMaterial);}
    }
}
