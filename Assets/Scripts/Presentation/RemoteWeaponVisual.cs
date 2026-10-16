using UnityEngine;using LestingCraft.Gameplay;
namespace LestingCraft.Presentation
{
 public sealed class RemoteWeaponVisual:MonoBehaviour
 {
  int shown=-1;MeshFilter held;
  public static float Scale(int id){if(Weapons.Has(id)){string kind=Weapons.Get(id).Kind;return kind=="spear"?.75f:kind=="greatsword"?.62f:kind=="dagger"?.32f:kind=="bow"?.60f:.48f;}return .45f;}
  public void Show(int id){if(id==shown)return;shown=id;if(!held){var o=new GameObject("Оружие в правой руке",typeof(MeshFilter),typeof(MeshRenderer));o.transform.SetParent(transform,false);o.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Generated/VoxelObjects");held=o.GetComponent<MeshFilter>();}held.sharedMesh=id>0?VoxelItemModels.MeshFor(id):null;float size=Weapons.Has(id)&&Weapons.Get(id).Kind=="spear"?2.0f:Scale(id)*2;held.transform.localPosition=new Vector3(.37f,.87f+size*.45f,.20f);held.transform.localRotation=Quaternion.Euler(12,0,0);held.transform.localScale=Vector3.one*size;held.gameObject.SetActive(held.sharedMesh);}
 }
}
