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
