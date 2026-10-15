using UnityEngine;
using UnityEngine.UI;
using LestingCraft.Gameplay;
using LestingCraft.Runtime;
namespace LestingCraft.Presentation
{
 public sealed class CharacterPreview : MonoBehaviour
 {
  const int Layer=29;GameObject root;CharacterVisual model;RenderTexture texture;PrototypeGame game;int revision=-1;
  public void Initialize(PrototypeGame owner)
  {
   game=owner;root=new GameObject("Inventory portrait scene");root.transform.position=new Vector3(0,-1024,0);
   var actor=new GameObject("Equipped adventurer preview");actor.transform.SetParent(root.transform,false);actor.transform.localRotation=Quaternion.Euler(0,-22,0);model=actor.AddComponent<CharacterVisual>();model.Build();
   var cameraObject=new GameObject("Portrait camera");cameraObject.transform.SetParent(root.transform,false);cameraObject.transform.localPosition=new Vector3(0,1.04f,3.0f);cameraObject.transform.LookAt(root.transform.position+Vector3.up*.98f);
   var camera=cameraObject.AddComponent<Camera>();camera.cullingMask=1<<Layer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.06f,.065f,1);camera.fieldOfView=38;camera.nearClipPlane=.1f;camera.farClipPlane=8;camera.allowHDR=true;
   texture=new RenderTexture(192,288,24,RenderTextureFormat.ARGB32);texture.Create();camera.targetTexture=texture;GetComponent<RawImage>().texture=texture;
   var lamp=new GameObject("Portrait key light");lamp.transform.SetParent(root.transform,false);lamp.transform.localPosition=new Vector3(-1,2.5f,2);var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.intensity=3;light.range=8;light.cullingMask=1<<Layer;light.color=new Color(1,.88f,.72f);
   Refresh();
  }
  void Refresh(){if(!game||!game.Session)return;var inv=game.Session.Inventory;if(inv.revision==revision)return;revision=inv.revision;var ids=new int[8];for(int i=0;i<8;i++)ids[i]=inv.equipment[i].count>0?inv.equipment[i].item:0;model.SetEquipment(ids);foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=Layer;}
  void LateUpdate(){Refresh();}
  void OnDestroy(){if(root){root.SetActive(false);if(Application.isPlaying)Destroy(root);else DestroyImmediate(root);}if(texture){texture.Release();Destroy(texture);}}
 }
}
