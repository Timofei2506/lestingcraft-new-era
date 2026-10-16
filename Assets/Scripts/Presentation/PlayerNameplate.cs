using System.Collections.Generic;using UnityEngine;using UnityEngine.UI;using LestingCraft.Runtime;using LestingCraft.World;
namespace LestingCraft.Presentation
{
 // Camera-projected local uGUI text works in URP, including Cyrillic; voxel line-of-sight prevents wall ESP.
 public sealed class PlayerNameplate:MonoBehaviour
 {
  static int layoutFrame=-1;static readonly List<Rect> occupied=new List<Rect>();static Canvas layer;Text label;VoxelWorld world;float height=2.1f;string nickname;
  public void Initialize(string name,VoxelWorld owner,float headHeight=2.1f)
  {
   world=owner;height=headHeight;nickname=(name??"Игрок").Replace('\n',' ').Replace('\r',' ');if(nickname.Length>24)nickname=nickname.Substring(0,24);
   if(!layer){var root=new GameObject("Visible player names",typeof(Canvas));layer=root.GetComponent<Canvas>();layer.renderMode=RenderMode.ScreenSpaceOverlay;layer.sortingOrder=85;}
   var obj=new GameObject("Name · "+nickname,typeof(RectTransform),typeof(Image));obj.transform.SetParent(layer.transform,false);var bg=obj.GetComponent<Image>();bg.color=new Color(.015f,.025f,.03f,.72f);bg.raycastTarget=false;var rt=obj.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(220,33);rt.anchorMin=rt.anchorMax=Vector2.zero;
   var child=new GameObject("Readable nickname",typeof(RectTransform),typeof(Text));child.transform.SetParent(obj.transform,false);var cr=child.GetComponent<RectTransform>();cr.anchorMin=Vector2.zero;cr.anchorMax=Vector2.one;cr.offsetMin=cr.offsetMax=Vector2.zero;
   label=child.GetComponent<Text>();label.font=Resources.Load<Font>("UI/Manrope")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=nickname;label.supportRichText=false;label.fontSize=18;label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;label.raycastTarget=false;
  }
  void LateUpdate()
  {
   if(!label)return;var camera=Camera.main;bool visible=world&&camera&&world.Loaded(transform.position);Vector3 target=transform.position+Vector3.up*height,point=visible?camera.WorldToScreenPoint(target):Vector3.zero;float distance=visible?Vector3.Distance(camera.transform.position,target):100;
   visible=visible&&point.z>0&&distance<40&&distance>.5f;
   if(visible){Vector3 dir=transform.position+Vector3.up*1.65f-camera.transform.position;visible=!VoxelRaycast.Trace(camera.transform.position,dir.normalized,Mathf.Max(0,dir.magnitude-.5f),(x,y,z)=>{ushort b=world.GetBlock(x,y,z);return b==5||b==44||b==7?(ushort)0:b;},out _);}
   var panel=label.transform.parent.gameObject;panel.SetActive(visible);if(visible){float scale=Mathf.Clamp(Screen.height/900f,.7f,1.7f);if(layoutFrame!=Time.frameCount){layoutFrame=Time.frameCount;occupied.Clear();}float width=220*scale,h=33*scale;var rect=new Rect(Mathf.Clamp(point.x-width*.5f,0,Mathf.Max(0,Screen.width-width)),Mathf.Clamp(point.y-h*.5f,0,Mathf.Max(0,Screen.height-h)),width,h);for(int i=0;i<16;i++){bool overlaps=false;foreach(var other in occupied)if(rect.Overlaps(other)){overlaps=true;break;}if(!overlaps)break;rect.y+=(point.y>Screen.height*.6f?-1:1)*(h+3);}occupied.Add(rect);panel.GetComponent<RectTransform>().anchoredPosition=rect.center;panel.transform.localScale=Vector3.one*scale;}
  }
  void OnDisable(){if(label)label.transform.parent.gameObject.SetActive(false);}
  void OnDestroy(){if(label)Destroy(label.transform.parent.gameObject);}
 }
}
