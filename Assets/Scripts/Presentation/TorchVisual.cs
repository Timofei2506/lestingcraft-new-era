using UnityEngine;using UnityEngine.Rendering;
namespace LestingCraft.Presentation
{
 public sealed class TorchVisual:MonoBehaviour
 {
  Light source;public Transform Owner;
  public void Build(bool lighting=true)
  {var mf=gameObject.AddComponent<MeshFilter>();mf.sharedMesh=VoxelItemModels.MeshFor(148);var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=Resources.Load<Material>("Generated/VoxelObjects");renderer.shadowCastingMode=ShadowCastingMode.Off;if(lighting){var child=new GameObject("Offhand torch point light");child.transform.SetParent(transform,false);source=child.AddComponent<Light>();source.type=LightType.Point;source.range=11;source.color=new Color(1,.48f,.16f);source.intensity=3.3f;source.shadows=LightShadows.Soft;source.shadowBias=.025f;source.shadowNormalBias=.12f;source.shadowResolution=LightShadowResolution.Medium;}}
  void LateUpdate(){if(source){source.intensity=3.25f+Mathf.Sin(Time.time*13.7f)*.12f+Mathf.Sin(Time.time*23.3f)*.05f;source.transform.position=Owner?Owner.position+Owner.right*-.32f+Vector3.up*1.5f+Owner.forward*.38f:transform.TransformPoint(Vector3.up*.46f);}}
 }
}
