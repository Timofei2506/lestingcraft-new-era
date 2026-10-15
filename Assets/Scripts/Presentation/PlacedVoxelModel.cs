using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace LestingCraft.Presentation
{
 public static class PlacedVoxelModel
 {
  public const float Scale=.95f;
  public static Vector3 Offset(int id)=>new Vector3(.5f,-VoxelItemModels.MeshFor(id).bounds.min.y*Scale,.5f);
  public static GameObject Create(int id,Transform parent,Vector3 cell,Material material)
  {
   var mesh=VoxelItemModels.MeshFor(id);if(!mesh)throw new System.InvalidOperationException("Placed model missing: "+id);
   var obj=new GameObject("Placed voxel model "+id,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
   obj.transform.localPosition=cell+Offset(id);obj.transform.localScale=Vector3.one*Scale;
   obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;return obj;
  }
  // One draw/collider per chunk, NOT one renderer per naturally occurring crystal.
  // Completely enclosed clusters are omitted; neighboring edits rebuild their visibility.
  public static GameObject CreateCrystalBatch(Transform parent,List<Vector3> cells,Material material)
  {
   if(cells.Count==0)return null;
   var source=VoxelItemModels.MeshFor(11);var instances=new CombineInstance[cells.Count];
   for(int i=0;i<cells.Count;i++)instances[i]=new CombineInstance{mesh=source,transform=Matrix4x4.TRS(cells[i]+Offset(11),Quaternion.identity,Vector3.one*Scale)};
   var mesh=new Mesh{name="Shared authored crystal clusters",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(instances,true,true);
   var obj=new GameObject("Placed crystal clusters",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));obj.transform.SetParent(parent,false);
   obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;obj.GetComponent<MeshCollider>().sharedMesh=mesh;
   obj.AddComponent<PlacedModelBatchLifetime>().Mesh=mesh;return obj;
  }
 }
 public sealed class PlacedModelBatchLifetime:MonoBehaviour
 {public Mesh Mesh;void OnDestroy(){if(Mesh){if(Application.isPlaying)Destroy(Mesh);else DestroyImmediate(Mesh);}}}
}
