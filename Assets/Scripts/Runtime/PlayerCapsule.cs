using UnityEngine;

namespace LestingCraft.Runtime
{
    public static class PlayerCapsule
    {
        static readonly RaycastHit[] hits=new RaycastHit[16];
        // Visual character is 2.00 blocks tall; capsule height 1.92 + 2*skinWidth (0.04) = 1.96m so a
        // 2-block-tall player still passes smoothly through standard 2-block-high doorways and tunnels.
        public const float Height=1.92f,Radius=.28f,Skin=.02f,Step=.25f,EyeHeight=1.78f;
        public static void Configure(CharacterController controller)
        {controller.height=Height;controller.radius=Radius;controller.center=Vector3.up*(Height*.5f);controller.skinWidth=Skin;controller.stepOffset=Step;controller.minMoveDistance=0;controller.slopeLimit=46;controller.enableOverlapRecovery=true;}
        public static void UpdateStep(CharacterController controller,Vector3 movement)
        {
            // Unity's automatic capsule step may raise the head into the lintel. Disable it under low ceilings,
            // including the entrance just ahead, while retaining ordinary stair movement outside tunnels.
            Vector3 direction=movement.sqrMagnitude>.000001f?movement.normalized:Vector3.zero;
            Vector3 top=controller.transform.position+controller.center+Vector3.up*(Height*.5f-Radius);
            bool ceiling=Ceiling(top,controller)||Ceiling(top+direction*.38f,controller);
            controller.stepOffset=ceiling?0:Step;
        }
        static bool Ceiling(Vector3 center,CharacterController controller)
        {
            int count=Physics.SphereCastNonAlloc(center,Radius-Skin,Vector3.up,hits,.38f,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++){var hit=hits[i];if(hit.collider!=controller&&!hit.collider.transform.IsChildOf(controller.transform)&&hit.normal.y<-.25f)return true;}
            return false;
        }
    }
}
