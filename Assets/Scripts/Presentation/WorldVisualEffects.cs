using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Gameplay;
using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// World-space gameplay & atmospheric VFX rig (never parented under ViewCamera so
    /// FirstPersonHands renderer counts remain untouched):
    /// - 3D crescent weapon slash arc on melee swings
    /// - Hot metallic/mineral impact spark bursts when mining rock/ore/crystal or hitting bosses
    /// - Live flickering flame & rising sparks on held/offhand torches in world space
    /// - Biome-aware ambient atmosphere motes (fireflies at night, Cherenkov embers, alien spores, cinders)
    /// </summary>
    public sealed class WorldVisualEffects : MonoBehaviour
    {
        public static WorldVisualEffects Instance { get; private set; }

        Transform slashPivot;
        MeshRenderer slashRenderer;
        Mesh slashMesh;
        Material slashMat, sparkMat, moteMat;
        float slashTimer;
        const float SlashDuration = 0.19f;
        Color slashBaseColor = new Color(0.85f, 0.94f, 1f, 0.9f);

        ParticleSystem impactSparks;
        ParticleSystem ambientMotes;
        ParticleSystem heldTorchFlame;
        ParticleSystem heldTorchSparks;
        float nextMineSparkTime;

        public Transform TrackedCamera;
        public Dimension ActiveDimension = Dimension.Lestingia;
        public float DayClock01 = 0.5f;
        public bool HoldingTorch;

        public int ActiveParticleSystemCount =>
            (impactSparks ? 1 : 0) + (ambientMotes ? 1 : 0) + (heldTorchFlame ? 1 : 0) + (heldTorchSparks ? 1 : 0);

        public static WorldVisualEffects Ensure(Transform parent)
        {
            if (Instance) return Instance;
            var found = FindAnyObjectByType<WorldVisualEffects>();
            if (found) { Instance = found; return found; }
            var go = new GameObject("WorldVisualEffects");
            if (parent) go.transform.SetParent(parent, false);
            var comp = go.AddComponent<WorldVisualEffects>();
            comp.Build();
            Instance = comp;
            return comp;
        }

        static void SafeDestroy(Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        static Material MakeParticleMat(string name, Color tint)
        {
            var baseFire = Resources.Load<Material>("Generated/Fire");
            Material m;
            if (baseFire)
                m = new Material(baseFire) { name = name };
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("Universal Render Pipeline/Unlit");
                m = new Material(sh) { name = name };
            }
            m.color = tint;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            return m;
        }

        public void Build()
        {
            if (slashPivot) return;

            var sh = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            slashMat = new Material(sh) { name = "WeaponSlashArcMat", color = slashBaseColor };
            if (slashMat.HasProperty("_BaseColor")) slashMat.SetColor("_BaseColor", slashBaseColor);
            sparkMat = MakeParticleMat("MiningImpactSparksMat", Color.white);
            moteMat = MakeParticleMat("AmbientBiomeMotesMat", Color.white);

            // 1. 3D Crescent Weapon Slash Ribbon
            slashMesh = BuildCrescentArcMesh(18, 0.88f, 1.18f, 118f);
            var pivotGo = new GameObject("WeaponSlashPivot");
            pivotGo.transform.SetParent(transform, false);
            slashPivot = pivotGo.transform;

            var arcGo = new GameObject("WeaponSlashRibbon", typeof(MeshFilter), typeof(MeshRenderer));
            arcGo.transform.SetParent(slashPivot, false);
            arcGo.GetComponent<MeshFilter>().sharedMesh = slashMesh;
            slashRenderer = arcGo.GetComponent<MeshRenderer>();
            slashRenderer.sharedMaterial = slashMat;
            slashRenderer.shadowCastingMode = ShadowCastingMode.Off;
            slashRenderer.receiveShadows = false;
            slashPivot.gameObject.SetActive(false);

            // 2. Impact Sparks Emitter (burst-only)
            impactSparks = CreateEmitter("ImpactSparks", sparkMat, 96, false);
            var sm = impactSparks.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.52f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 6.4f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
            sm.gravityModifier = 1.45f;
            var sr = impactSparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.lengthScale = 2.4f;
            sr.velocityScale = 0.06f;

            // 3. Held / Offhand Torch World-Space Flame & Sparks
            heldTorchFlame = CreateEmitter("HeldTorchFlame", sparkMat, 28, true);
            var tf = heldTorchFlame.main;
            tf.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            tf.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            tf.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            tf.startColor = new Color(1f, 0.56f, 0.12f, 0.85f);
            var tfe = heldTorchFlame.emission; tfe.rateOverTime = 22f;
            var tfs = heldTorchFlame.shape; tfs.shapeType = ParticleSystemShapeType.Sphere; tfs.radius = 0.045f;

            heldTorchSparks = CreateEmitter("HeldTorchSparks", sparkMat, 20, true);
            var ts = heldTorchSparks.main;
            ts.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            ts.startSpeed = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
            ts.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.032f);
            ts.startColor = new Color(1f, 0.72f, 0.22f, 0.95f);
            ts.gravityModifier = -0.18f;
            var tse = heldTorchSparks.emission; tse.rateOverTime = 7f;
            var tss = heldTorchSparks.shape; tss.shapeType = ParticleSystemShapeType.Cone; tss.angle = 18f; tss.radius = 0.04f; tss.rotation = new Vector3(-90f, 0f, 0f);

            // 4. Ambient Biome Atmosphere Motes (48 gentle world-space particles around player)
            ambientMotes = CreateEmitter("BiomeAtmosphereMotes", moteMat, 56, true);
            var am = ambientMotes.main;
            am.startLifetime = new ParticleSystem.MinMaxCurve(3.2f, 5.8f);
            am.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
            am.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
            am.startColor = new Color(0.82f, 1f, 0.48f, 0.75f);
            var ae = ambientMotes.emission; ae.rateOverTime = 11f;
            var ash = ambientMotes.shape; ash.shapeType = ParticleSystemShapeType.Box; ash.scale = new Vector3(18f, 7f, 18f);
            var an = ambientMotes.noise; an.enabled = true; an.strength = 0.24f; an.frequency = 0.45f; an.scrollSpeed = 0.2f;
            ApplyFadeOverLife(ambientMotes);
            ApplyFadeOverLife(impactSparks);
            ApplyFadeOverLife(heldTorchFlame);
            ApplyFadeOverLife(heldTorchSparks);
        }

        ParticleSystem CreateEmitter(string label, Material mat, int maxParticles, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.loop = loop;
            m.playOnAwake = false;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.maxParticles = maxParticles;
            var e = ps.emission;
            e.rateOverTime = 0f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.18f;
            return ps;
        }

        static void ApplyFadeOverLife(ParticleSystem ps)
        {
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f), new GradientAlphaKey(0.85f, 0.65f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        static Mesh BuildCrescentArcMesh(int segments, float innerR, float outerR, float sweepDeg)
        {
            var verts = new List<Vector3>((segments + 1) * 2 * 2);
            var cols = new List<Color>((segments + 1) * 2 * 2);
            var tris = new List<int>(segments * 12);
            float halfRad = sweepDeg * 0.5f * Mathf.Deg2Rad;

            for (int side = 0; side < 2; side++)
            {
                int baseIdx = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float ang = Mathf.Lerp(-halfRad, halfRad, t);
                    // Taper crescent thickness smoothly to 0 at both tips!
                    float taper = Mathf.Sin(t * Mathf.PI);
                    float rMid = (innerR + outerR) * 0.5f;
                    float halfThick = (outerR - innerR) * 0.5f * taper;
                    float r0 = rMid - halfThick * 0.55f;
                    float r1 = rMid + halfThick;
                    verts.Add(new Vector3(Mathf.Sin(ang) * r0, 0f, Mathf.Cos(ang) * r0));
                    verts.Add(new Vector3(Mathf.Sin(ang) * r1, 0f, Mathf.Cos(ang) * r1));
                    cols.Add(new Color(1f, 1f, 1f, 0.15f * taper));
                    cols.Add(new Color(1f, 1f, 1f, 0.95f * taper));
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = baseIdx + i * 2, b = a + 1, c = a + 2, d = a + 3;
                    if (side == 0) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                    else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                }
            }
            var mesh = new Mesh { name = "CrescentSlashArc" };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void TriggerSlash(Vector3 eyePos, Quaternion lookRot, int weaponItemId)
        {
            if (!Instance) return;
            Instance.StartSlash(eyePos, lookRot, weaponItemId);
        }

        public void StartSlash(Vector3 eyePos, Quaternion lookRot, int weaponItemId)
        {
            if (!slashPivot) Build();
            // Tier-colored slash arc: diamond/crystal -> electric cyan, steel -> ice-silver, copper/wood -> warm gold
            if (weaponItemId >= 170) slashBaseColor = new Color(0.32f, 0.92f, 1f, 0.92f);
            else if (weaponItemId >= 150) slashBaseColor = new Color(0.86f, 0.93f, 1f, 0.88f);
            else slashBaseColor = new Color(1f, 0.80f, 0.42f, 0.84f);

            float tilt = (weaponItemId % 2 == 0) ? -24f : 22f;
            slashPivot.position = eyePos + lookRot * new Vector3(0f, -0.14f, 0.18f);
            slashPivot.rotation = lookRot * Quaternion.Euler(tilt, 0f, -18f);
            slashTimer = SlashDuration;
            slashPivot.gameObject.SetActive(true);
            UpdateSlashVisual(1f);
        }

        void UpdateSlashVisual(float life01)
        {
            if (!slashPivot || !slashMat) return;
            float progress = 1f - life01;
            slashPivot.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.18f, progress);
            Color c = slashBaseColor;
            c.a *= life01;
            slashMat.color = c;
            if (slashMat.HasProperty("_BaseColor")) slashMat.SetColor("_BaseColor", c);
        }

        public static void EmitImpactSparks(Vector3 worldPos, Vector3 normal, Color tint, int count = 12)
        {
            if (!Instance) return;
            Instance.BurstSparks(worldPos, normal, tint, count);
        }

        public void BurstSparks(Vector3 worldPos, Vector3 normal, Color tint, int count = 12)
        {
            if (!impactSparks) Build();
            if (Time.time < nextMineSparkTime) return;
            nextMineSparkTime = Time.time + 0.09f;
            Vector3 dir = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            impactSparks.transform.position = worldPos;
            impactSparks.transform.rotation = Quaternion.LookRotation(dir);
            var m = impactSparks.main;
            m.startColor = tint;
            impactSparks.Emit(Mathf.Clamp(count, 4, 28));
        }

        void LateUpdate()
        {
            if (slashTimer > 0f)
            {
                slashTimer -= Time.deltaTime;
                if (slashTimer <= 0f)
                {
                    if (slashPivot) slashPivot.gameObject.SetActive(false);
                }
                else
                {
                    if (TrackedCamera)
                        slashPivot.position = TrackedCamera.position + TrackedCamera.rotation * new Vector3(0f, -0.14f, 0.18f);
                    slashPivot.Rotate(0f, Time.deltaTime * 420f, 0f, Space.Self);
                    UpdateSlashVisual(Mathf.Clamp01(slashTimer / SlashDuration));
                }
            }

            if (!TrackedCamera) return;

            // Held / Offhand Torch live world-space flame & rising sparks
            if (heldTorchFlame && heldTorchSparks)
            {
                if (HoldingTorch)
                {
                    Vector3 tip = TrackedCamera.position
                        + TrackedCamera.right * -0.30f
                        + TrackedCamera.up * -0.12f
                        + TrackedCamera.forward * 0.52f;
                    heldTorchFlame.transform.position = tip;
                    heldTorchSparks.transform.position = tip;
                    if (!heldTorchFlame.isPlaying) heldTorchFlame.Play();
                    if (!heldTorchSparks.isPlaying) heldTorchSparks.Play();
                }
                else
                {
                    if (heldTorchFlame.isPlaying) heldTorchFlame.Stop();
                    if (heldTorchSparks.isPlaying) heldTorchSparks.Stop();
                }
            }

            // Ambient Biome Motes following camera
            if (ambientMotes)
            {
                ambientMotes.transform.position = TrackedCamera.position + Vector3.up * 1.2f;
                bool isNight = DayClock01 < 0.23f || DayClock01 > 0.77f;
                bool activeMotes = true;
                Color moteColor = new Color(0.84f, 1f, 0.44f, 0.78f);

                if (ActiveDimension == Dimension.Lestingia)
                {
                    activeMotes = isNight; // Fireflies at dusk & night in Overworld!
                    moteColor = new Color(0.82f, 1f, 0.38f, 0.82f);
                }
                else if (ActiveDimension == Dimension.Radia)
                    moteColor = new Color(0.22f, 0.88f, 1f, 0.75f);
                else if (ActiveDimension == Dimension.Mercury || ActiveDimension == Dimension.Venus)
                    moteColor = new Color(1f, 0.48f, 0.16f, 0.78f);
                else if (ActiveDimension == Dimension.Europa || ActiveDimension == Dimension.Pluto)
                    moteColor = new Color(0.82f, 0.94f, 1f, 0.70f);
                else if (ActiveDimension == Dimension.Trappist1e || ActiveDimension == Dimension.Kepler452b)
                    moteColor = new Color(0.45f, 1f, 0.78f, 0.75f);
                else if (ActiveDimension == Dimension.AndromedaPrime || ActiveDimension == Dimension.Somnia || ActiveDimension == Dimension.Dream)
                    moteColor = new Color(0.76f, 0.52f, 1f, 0.78f);
                else
                    activeMotes = false;

                var am = ambientMotes.main;
                am.startColor = moteColor;
                if (activeMotes && !ambientMotes.isPlaying) ambientMotes.Play();
                else if (!activeMotes && ambientMotes.isPlaying) ambientMotes.Stop();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SafeDestroy(slashMesh);
            SafeDestroy(slashMat);
            SafeDestroy(sparkMat);
            SafeDestroy(moteMat);
        }
    }
}
