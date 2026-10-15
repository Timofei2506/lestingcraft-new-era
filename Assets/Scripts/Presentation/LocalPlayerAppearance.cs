using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Runtime;
namespace LestingCraft.Presentation
{
 public sealed class LocalPlayerAppearance : MonoBehaviour
 {
  PrototypeGame game;CharacterVisual visual;int revision=-1;
  public void Initialize(PrototypeGame owner){game=owner;var root=new GameObject("Local block body / world shadows");root.transform.SetParent(transform,false);visual=root.AddComponent<CharacterVisual>();visual.Build();Refresh();}
  void Refresh(){if(!visual||!game||!game.Session)return;var inv=game.Session.Inventory;if(inv.revision==revision)return;revision=inv.revision;var ids=new int[8];for(int i=0;i<8;i++)ids[i]=inv.equipment[i].count>0?inv.equipment[i].item:0;visual.SetEquipment(ids);foreach(var renderer in visual.GetComponentsInChildren<MeshRenderer>())renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;}
  void LateUpdate(){Refresh();}
 }
}
