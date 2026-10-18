using UnityEngine;using LestingCraft.Runtime;
namespace LestingCraft.Presentation
{
 [DefaultExecutionOrder(100)]
 public sealed class VoxelSkyVolume:MonoBehaviour
 {
  static VoxelSkyVolume owner;public VoxelWorld World;const int Width=192,Height=128;Texture3D texture;byte[] pixels;Vector3Int origin;bool invalid=true,uploaded;float next;
  // Snapped to chunk centres: upload only on field edits/streaming or a 16m boundary, not every step.
  public void Invalidate(){invalid=true;}
  void LateUpdate()
  {
   if(!World||Time.unscaledTime<next)return;Vector3 p=World.Player?World.Player.position:new Vector3(0,60,0);
   var target=new Vector3Int(Mathf.FloorToInt(p.x/16)*16+8-Width/2,Mathf.Clamp(Mathf.FloorToInt(p.y/16)*16-96,0,256-Height),Mathf.FloorToInt(p.z/16)*16+8-Width/2);
   if(!invalid&&uploaded&&target==origin)return;next=Time.unscaledTime+.18f;origin=target;
   if(!texture){texture=new Texture3D(Width,Height,Width,TextureFormat.R8,false){name="Local propagated voxel skylight",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};pixels=new byte[Width*Height*Width];}
   World.Skylight.CopyVolume(pixels,origin.x,origin.y,origin.z,Width,Height,Width);texture.SetPixelData(pixels,0);texture.Apply(false,false);
   owner=this;Shader.SetGlobalTexture("_LCVoxelSky",texture);Shader.SetGlobalVector("_LCVoxelSkyOrigin",new Vector4(origin.x,origin.y,origin.z,1));Shader.SetGlobalVector("_LCVoxelSkySize",new Vector4(Width,Height,Width,0));invalid=false;uploaded=true;
  }
  void OnDestroy(){if(owner==this){owner=null;Shader.SetGlobalVector("_LCVoxelSkyOrigin",Vector4.zero);}if(texture)Destroy(texture);}
 }
}
