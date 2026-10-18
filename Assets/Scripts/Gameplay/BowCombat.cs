using UnityEngine;using LestingCraft.Presentation;using LestingCraft.Networking;using LestingCraft.Runtime;
namespace LestingCraft.Gameplay
{
 public sealed class BowCombat:MonoBehaviour
 {
  public SurvivalSession Session;float nextShot;
  public void Fire()
  {
   var s=Session;if(!s||!s.CanInteract||s.Game.StoryLocked||Time.time<nextShot)return;
   if(!s.Creative&&!s.Inventory.Remove(165,1)){s.Notice="Для лука нужны стрелы";return;}nextShot=Time.time+1.3f/s.Vitals.body.Dexterity;if(!s.Creative)s.Inventory.WearTool();
   int id=s.Animals.NewArrowShot();if(s.Game.Online)s.Game.SendStory(new NetPacket{type="arrow_shot",requestId=id});
   var root=new GameObject("Выпущенная воксельная стрела",typeof(MeshFilter),typeof(MeshRenderer));root.transform.SetParent(s.World.transform,false);root.transform.position=s.Player.Camera.transform.position+s.Player.Camera.transform.forward*.2f;root.transform.localScale=Vector3.one*.85f;root.GetComponent<MeshFilter>().sharedMesh=VoxelItemModels.MeshFor(165);root.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Generated/VoxelObjects");var flight=root.AddComponent<ArrowFlight>();flight.Session=s;flight.RequestId=id;flight.Velocity=s.Player.Camera.transform.forward*35;
  }
 }
 public sealed class ArrowFlight:MonoBehaviour
 {
  public SurvivalSession Session;public int RequestId;public Vector3 Velocity;float distance;
  void Update(){var s=Session;if(!s||s.Vitals.dead||s.Data.campaign.runEnded){Destroy(gameObject);return;}if(!s.Game.Online&&s.Game.Paused)return;float dt=Mathf.Min(.15f,Time.deltaTime);Velocity+=Vector3.down*(9.81f*dt);Vector3 step=Velocity*dt;float length=step.magnitude;var ray=new Ray(transform.position,step.normalized);bool wall=VoxelRaycast.Trace(ray.origin,ray.direction,length,s.World.GetBlock,out var block);if(s.Animals.Intersect(ray,wall?block.Distance:length,out string id,164)){s.Animals.Attack(id,true,RequestId);Destroy(gameObject);return;}if(wall){Destroy(gameObject);return;}transform.position+=step;distance+=length;transform.rotation=Quaternion.LookRotation(Velocity)*Quaternion.Euler(90,0,0);if(distance>=20)Destroy(gameObject);}
 }
}
