using NUnit.Framework;
using UnityEngine;
using LestingCraft.Presentation;

namespace LestingCraft.Tests
{
    public sealed class CutscenePathTests
    {
        [Test] public void SplinePassesThroughEveryKeyPosition()
        {
            var a = new Vector3(0, 1, 0); var b = new Vector3(4, 3, -2); var c = new Vector3(9, 2, 5);
            Assert.That(Vector3.Distance(CutscenePath.Sample(0f, (0f, a), (6f, b), (12f, c)), a), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(CutscenePath.Sample(6f, (0f, a), (6f, b), (12f, c)), b), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(CutscenePath.Sample(12f, (0f, a), (6f, b), (12f, c)), c), Is.LessThan(1e-4f));
        }

        [Test] public void ClampsBeforeFirstAndAfterLastKey()
        {
            var a = new Vector3(1, 2, 3); var b = new Vector3(7, 8, 9);
            Assert.AreEqual(a, CutscenePath.Sample(-5f, (0f, a), (10f, b)));
            Assert.AreEqual(b, CutscenePath.Sample(99f, (0f, a), (10f, b)));
        }

        [Test] public void CollinearKeysStayOnTheLineAndAreMonotone()
        {
            // Catmull-Rom is parameterised per segment, not by arc length, so x(t) need not equal t between keys.
            float previousX = float.NegativeInfinity;
            for (float t = 0; t <= 10f; t += .25f)
            {
                var p = CutscenePath.Sample(t, (0f, Vector3.zero), (5f, new Vector3(5, 0, 0)), (10f, new Vector3(10, 0, 0)));
                Assert.That(p.y, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(p.z, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(p.x, Is.GreaterThanOrEqualTo(previousX - 1e-4f));
                previousX = p.x;
            }
            Assert.That(CutscenePath.Sample(5f, (0f, Vector3.zero), (5f, new Vector3(5, 0, 0)), (10f, new Vector3(10, 0, 0))).x, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test] public void PathIsContinuousWithBoundedSteps()
        {
            var keys = new[] { (0f, new Vector3(166, 4, -100)), (6f, new Vector3(171, 7.4f, -98)), (12f, new Vector3(172, 7, -106)) };
            Vector3 previous = CutscenePath.Sample(0f, keys[0], keys[1], keys[2]);
            for (float t = 0.01f; t <= 12f; t += .01f)
            {
                Vector3 p = CutscenePath.Sample(t, keys[0], keys[1], keys[2]);
                Assert.That(Vector3.Distance(previous, p), Is.LessThan(0.05f), "step too large at t=" + t);
                previous = p;
            }
        }

        [Test] public void ShakeIsZeroBeforeImpactAndDecays()
        {
            float[] knocks = { 9f, 9.9f, 10.8f };
            Assert.AreEqual(Vector3.zero, CutscenePath.Shake(8.5f, knocks, .035f));
            float early = CutscenePath.Shake(9.05f, knocks, .035f).magnitude;
            float late = CutscenePath.Shake(9.6f, knocks, .035f).magnitude;
            Assert.Greater(early, 0f);
            Assert.Less(late, early + 0.035f);
        }

        [Test] public void ShakeIsDeterministicAndBounded()
        {
            float[] knocks = { 9f, 9.9f, 10.8f };
            for (float t = 8f; t <= 14f; t += .05f)
            {
                Vector3 a = CutscenePath.Shake(t, knocks, .035f);
                Vector3 b = CutscenePath.Shake(t, knocks, .035f);
                Assert.AreEqual(a, b);
                Assert.That(a.magnitude, Is.LessThanOrEqualTo(3f * .035f + 1e-5f));
            }
        }
    }
}
