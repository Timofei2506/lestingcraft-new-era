using LestingCraft.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace LestingCraft.Tests.EditMode
{
    public class LaunchSiteVisualTests
    {
        [Test] public void ApprovedPadAndOrbiterShareOnePhysicalOriginAndOpenApproach()
        {
            var root=new GameObject("shared launch site test");
            try
            {
                var site=new Vector3Int(168,70,168); // authoritative server seed 2506
                var visual=LaunchSiteVisual.Create(root.transform,site,2506);
                Assert.AreEqual((Vector3)site,visual.transform.position);
                Assert.IsNotNull(visual.Cabin);
                Assert.AreEqual(2f,visual.Cabin.transform.parent.localPosition.y,.001f);
                Assert.IsNotNull(visual.GetComponentInChildren<VerticalShuttleInteriorVisual>());
                Assert.AreEqual(2,visual.GetComponentsInChildren<SpaceVoxelVisual>().Length);
                foreach(var model in visual.GetComponentsInChildren<SpaceVoxelVisual>())
                    Assert.IsNotNull(model.GetComponent<MeshCollider>(),"Both approved models are colliding");
                Assert.IsNotNull(visual.transform.Find("Walkable ramp 0").GetComponent<BoxCollider>());
                Assert.IsNotNull(visual.transform.Find("Walkable ramp 81").GetComponent<BoxCollider>());
                var last=visual.transform.Find("Walkable ramp 81");
                Assert.AreEqual(LaunchSiteVisual.LiftWorldX-site.x,last.localPosition.x,.001f);
                Assert.AreEqual(LaunchSiteVisual.RampEndZ-site.z-.5f,last.localPosition.z,.001f);
                Assert.IsFalse(visual.Cabin.HatchOpen,"No local-only launch before server authority");
                Assert.AreEqual(0f,visual.Cabin.Lift.Height,.001f);
                var platform=visual.transform.Find("Moving crew lift");
                var bridge=visual.transform.Find("Tower lift exit bridge");
                var guide=visual.transform.Find("Lift guide rear 3");
                Assert.IsNotNull(platform);Assert.IsNotNull(bridge);Assert.IsNotNull(guide);
                Assert.AreEqual(visual.transform,platform.parent);
                var platformWorld=platform.position,bridgeWorld=bridge.position,guideWorld=guide.position;
                var orbiterWorld=visual.Cabin.transform.position;
                visual.ShuttleRoot.position+=Vector3.up*6;
                Assert.AreEqual(platformWorld,platform.position,"Platform stays with ground");
                Assert.AreEqual(bridgeWorld,bridge.position,"Tower bridge stays with pad");
                Assert.AreEqual(guideWorld,guide.position,"Lift guide stays with pad");
                Assert.AreEqual(orbiterWorld+Vector3.up*6,visual.Cabin.transform.position,
                    "Hull and cockpit alone can rise continuously");
                visual.Cabin.SyncLift(new LestingCraft.Networking.NetShuttleLift{
                    active=true,moving=true,direction="up",progress=.5f,serverNow=1000},null,"");
                Assert.Less(Vector3.Distance(platformWorld+Vector3.up*12.5f,platform.position),.001f,
                    "Server lift height remains pad-relative after hull starts moving");
                Assert.AreEqual(bridgeWorld,bridge.position);
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test] public void LocalSiteCalculationUsesTheSameSeedAndIsWithinWorldHeight()
        {
            var a=LaunchSiteVisual.OfflineSite(2506);
            var b=LaunchSiteVisual.OfflineSite(2506);
            Assert.AreEqual(a,b);
            Assert.AreEqual(168,a.x);Assert.AreEqual(168,a.z);
            Assert.LessOrEqual(Mathf.Abs(a.y-70),1,"Server picks the actual vertical origin");
            Assert.Less(a.y+80,240);
        }
    }
}
