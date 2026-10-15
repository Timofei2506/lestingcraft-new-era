using UnityEngine;
namespace LestingCraft.Presentation
{
 public static class PlacedVoxelModel
 {
  public static GameObject Create(int id,Transform parent,Vector3 cell,Material material)
  {
   var mesh=VoxelItemModels.MeshFor(id);if(!mesh)throw new System.InvalidOperationException("Placed model missing: "+id);
   var obj=new GameObject("Placed voxel model "+id,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
   obj.transform.localPosition=cell+new Vector3(.5f,-mesh.bounds.min.y*.95f,.5f);obj.transform.localScale=Vector3.one*.95f;
   obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=material;return obj;
  }
 }
}
