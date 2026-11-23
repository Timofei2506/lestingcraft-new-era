using UnityEngine;
using System.Collections.Generic;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Articulated block adventurer: expressive cuboid head on a neck pivot with natural gaze and
    /// blinking face tiles, layered ranger tunic with placket & belt gear, two-segment arms with
    /// elbows & shaded knuckle hands, two-segment legs with knees & stitched leather boots.
    /// Every mesh is a strict 24-vertex axis-aligned cuboid with zero colliders.
    /// </summary>
    public sealed class CharacterVisual : MonoBehaviour
    {
        // Scales the 1.826m authored rig to strictly 2.00 blocks (2.00m) from boot sole to crown.
        public const float ModelScale = 1.095f;
        Transform thighL, thighR, shinL, shinR, upperL, upperR, foreL, foreR, headPivot, scarfTail;
        MeshFilter headFilter;
        int faceOpen = 3, faceClosed = 11;
        Vector3 previous;
        float phase;
        Material material, metal, shadow, hair;
        string skinKey = "";
        readonly List<GameObject> extras = new List<GameObject>();
        string gearKey = "";
        GameObject carried;
        int carriedId = -1;
        bool resting;
        int restingVariant;
        float smoothSpeed, airWeight;
        float blinkIn = 2.6f, blinkLeft;
        bool blinking;
        float lookYaw, lookPitch;

        public void SetFaceTiles(int openTile, int closedTile)
        {
            faceOpen = openTile;
            faceClosed = closedTile;
            if (headFilter) headFilter.sharedMesh = BlockGeometry.Box(0, blinking || resting ? faceClosed : faceOpen);
        }

        public void SetRestPose(int variant)
        {
            resting = true;
            restingVariant = variant;
            if (headFilter) headFilter.sharedMesh = BlockGeometry.Box(0, faceClosed);
            ApplyRestPose();
        }

        void ApplyRestPose()
        {
            if (!thighL) return;
            thighL.localRotation = Quaternion.Euler(restingVariant == 0 ? -8 : 12, 0, restingVariant == 0 ? -9 : 13);
            thighR.localRotation = Quaternion.Euler(restingVariant == 0 ? 14 : -7, 0, restingVariant == 0 ? 16 : -8);
            shinL.localRotation = Quaternion.Euler(restingVariant == 0 ? 10 : 6, 0, 0);
            shinR.localRotation = Quaternion.Euler(restingVariant == 0 ? 4 : 12, 0, 0);
            upperL.localRotation = Quaternion.Euler(restingVariant == 0 ? -10 : 24, 0, restingVariant == 0 ? 28 : -38);
            upperR.localRotation = Quaternion.Euler(restingVariant == 0 ? 12 : -25, 0, restingVariant == 0 ? -17 : 35);
            foreL.localRotation = Quaternion.Euler(-24, 0, 0);
            foreR.localRotation = Quaternion.Euler(-30, 0, 0);
            if (headPivot) headPivot.localRotation = Quaternion.Euler(restingVariant == 0 ? 12 : -8, restingVariant == 0 ? 18 : -22, 0);
        }

        /// <summary>
        /// Tints the block body with the colours of an equipped store skin.
        /// null restores the default fabric. Safe to call every frame.
        /// </summary>
        public void SetSkin(string[] palette)
        {
            string key = palette == null ? "default" : "skins:" + string.Join(",", LestingCraft.Gameplay.SkinPalette.Sanitize(palette));
            if (key == skinKey) return;
            skinKey = key;
            if (!material) return;
            if (palette == null) { material.SetColor("_BaseColor", Color.white); return; }
            var colors = LestingCraft.Gameplay.SkinPalette.Sanitize(palette);
            material.SetColor("_BaseColor", new Color(
                LestingCraft.Gameplay.SkinPalette.Channel(colors[0], 0),
                LestingCraft.Gameplay.SkinPalette.Channel(colors[0], 1),
                LestingCraft.Gameplay.SkinPalette.Channel(colors[0], 2), 1f));
        }

        public void SetOffhand(int id, bool lighting = true)
        {
            if (carriedId == id) return;
            carriedId = id;
            if (carried) Destroy(carried);
            if (id != 148 || !foreL) return;
            carried = new GameObject("Visible left-hand torch");
            carried.transform.SetParent(foreL, false);
            carried.transform.localPosition = new Vector3(0, -.24f, .16f) * ModelScale;
            carried.transform.localRotation = Quaternion.Euler(-20, 0, 0);
            carried.transform.localScale = Vector3.one * (.75f * ModelScale);
            carried.AddComponent<TorchVisual>().Build(lighting);
        }

        public void Build()
        {
            if (material) return;
            material = BlockGeometry.CharacterMaterial();
            material.SetFloat("_Smoothness", .18f);
            metal = new Material(material); metal.SetFloat("_Metallic", .68f); metal.SetFloat("_Smoothness", .38f);
            shadow = new Material(material); shadow.SetColor("_BaseColor", new Color(.14f, .15f, .17f, 1f));
            hair = new Material(material); hair.SetColor("_BaseColor", new Color(.44f, .31f, .22f, 1f));

            // Layered ranger torso with front placket (tile 13), tunic skirt hem, belt & expedition pack
            Part("Block torso", new Vector3(0, 1.07f, 0), new Vector3(.44f, .56f, .26f), 1, transform, 13);
            Part("Tunic hem", new Vector3(0, .77f, 0), new Vector3(.46f, .09f, .28f), 1, transform);
            Part("Belt", new Vector3(0, .82f, .005f), new Vector3(.47f, .078f, .30f), 6, transform);
            Part("Square buckle", new Vector3(0, .82f, .162f), new Vector3(.095f, .082f, .022f), 5, transform);
            Part("Hip satchel", new Vector3(.235f, .78f, .02f), new Vector3(.065f, .14f, .16f), 6, transform);
            Part("Backpack", new Vector3(0, 1.08f, -.205f), new Vector3(.34f, .42f, .16f), 6, transform);
            Part("Backpack flap", new Vector3(0, 1.26f, -.215f), new Vector3(.36f, .09f, .18f), 7, transform);
            Part("Scarf collar", new Vector3(0, 1.355f, .005f), new Vector3(.46f, .075f, .30f), 7, transform);
            BlockGeometry.Part("Collar shadow", transform, new Vector3(0, 1.312f, .005f) * ModelScale, new Vector3(.39f, .02f, .275f) * ModelScale, shadow, 2);

            scarfTail = Pivot("Scarf tail", new Vector3(.08f, 1.32f, -.165f));
            Part("Scarf tail", new Vector3(0, -.15f, -.02f), new Vector3(.13f, .28f, .04f), 7, scarfTail);

            // Expressive cuboid head on neck pivot: clean skin ears & nose relief, stranded hair & hood trim
            headPivot = Pivot("Neck pivot", new Vector3(0, 1.40f, 0));
            Part("Neck column", new Vector3(0, -.01f, -.01f), new Vector3(.19f, .08f, .19f), 0, headPivot);
            headFilter = Part("Cubical head", new Vector3(0, .185f, 0), new Vector3(.38f, .38f, .38f), 0, headPivot, faceOpen).GetComponent<MeshFilter>();
            Part("Nose", new Vector3(0, .155f, .202f), new Vector3(.058f, .068f, .032f), 0, headPivot);
            for (int i = -1; i <= 1; i += 2)
                Part("Ear", new Vector3(i * .202f, .175f, -.01f), new Vector3(.034f, .095f, .075f), 0, headPivot);

            // Stranded hair cap & temple locks (using dedicated hair strand tile 10)
            BlockGeometry.Part("Hair cap", headPivot, new Vector3(0, .382f, -.008f) * ModelScale, new Vector3(.402f, .052f, .402f) * ModelScale, hair, 10);
            BlockGeometry.Part("Hair back", headPivot, new Vector3(0, .205f, -.198f) * ModelScale, new Vector3(.396f, .33f, .036f) * ModelScale, hair, 10);
            for (int i = -1; i <= 1; i += 2)
                BlockGeometry.Part("Side lock", headPivot, new Vector3(i * .196f, .255f, -.02f) * ModelScale, new Vector3(.032f, .19f, .33f) * ModelScale, hair, 10);

            // Folded ranger hood resting cleanly at crown/back (kept named for StoryActors overrides)
            Part("Block hood top", new Vector3(0, .408f, -.045f), new Vector3(.42f, .036f, .34f), 7, headPivot);
            Part("Block hood back", new Vector3(0, .06f, -.212f), new Vector3(.42f, .16f, .048f), 7, headPivot);

            for (int i = -1; i <= 1; i += 2)
            {
                Part("Pack strap", new Vector3(i * .145f, 1.09f, .138f), new Vector3(.045f, .50f, .02f), 6, transform);
                Part("Square coat clasp", new Vector3(i * .145f, 1.22f, .152f), new Vector3(.056f, .042f, .018f), 5, transform);
            }

            thighL = Leg("Left", -.12f); thighR = Leg("Right", .12f);
            upperL = Arm("Left", -.315f); upperR = Arm("Right", .315f);
            shinL = thighL.Find("Shin pivot"); shinR = thighR.Find("Shin pivot");
            foreL = upperL.Find("Elbow pivot"); foreR = upperR.Find("Elbow pivot");
            previous = transform.position;
        }

        Transform Pivot(string name, Vector3 position)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = position * ModelScale;
            return pivot;
        }

        Transform Leg(string side, float x)
        {
            var thigh = Pivot(side + " thigh pivot", new Vector3(x, .78f, 0));
            Part("Cuboid trousers", new Vector3(0, -.17f, 0), new Vector3(.205f, .36f, .23f), 2, thigh);
            var shin = new GameObject("Shin pivot").transform;
            shin.SetParent(thigh, false);
            shin.localPosition = new Vector3(0, -.35f, 0) * ModelScale;
            Part("Cuboid shin", new Vector3(0, -.15f, 0), new Vector3(.195f, .31f, .22f), 2, shin);
            Part("Cuboid boot", new Vector3(0, -.335f, .016f), new Vector3(.215f, .19f, .285f), 4, shin);
            Part("Boot sole", new Vector3(0, -.432f, .018f), new Vector3(.225f, .028f, .295f), 6, shin);
            return thigh;
        }

        Transform Arm(string side, float x)
        {
            var upper = Pivot(side + " shoulder pivot", new Vector3(x, 1.31f, 0));
            Part("Cuboid sleeve", new Vector3(0, -.145f, 0), new Vector3(.185f, .31f, .215f), 1, upper);
            var fore = new GameObject("Elbow pivot").transform;
            fore.SetParent(upper, false);
            fore.localPosition = new Vector3(0, -.29f, 0) * ModelScale;
            Part("Cuboid forearm", new Vector3(0, -.12f, 0), new Vector3(.175f, .25f, .205f), 1, fore);
            Part("Square cuff", new Vector3(0, -.25f, 0), new Vector3(.19f, .055f, .22f), 4, fore);
            Part("Block hand", new Vector3(0, -.335f, 0), new Vector3(.17f, .15f, .195f), 12, fore);
            return upper;
        }

        Transform Part(string name, Vector3 position, Vector3 scale, int tile, Transform parent, int front = -1)
            => BlockGeometry.Part(name, parent, position * ModelScale, scale * ModelScale, material, tile, front);

        public void SetEquipment(int[] ids)
        {
            if (ids == null || ids.Length != 8) ids = new int[8];
            string key = string.Join(",", ids);
            if (key == gearKey) return;
            gearKey = key;
            foreach (var obj in extras)
                if (obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
            extras.Clear();
            if (!thighL) return;
            if (ids[0] != 0) Gear("Cloth hood", headPivot, new Vector3(0, .395f, 0), new Vector3(.435f, .075f, .435f), 1);
            if (ids[1] != 0)
            {
                Gear("Warm mantle back", transform, new Vector3(0, 1.09f, -.22f), new Vector3(.54f, .64f, .08f), 7);
                Gear("Mantle collar", transform, new Vector3(0, 1.35f, -.005f), new Vector3(.54f, .095f, .33f), 6);
            }
            if (ids[2] != 0)
            {
                Gear("Left trouser shell", thighL, new Vector3(0, -.17f, 0), new Vector3(.23f, .38f, .255f), 2);
                Gear("Right trouser shell", thighR, new Vector3(0, -.17f, 0), new Vector3(.23f, .38f, .255f), 2);
            }
            if (ids[3] != 0)
            {
                Gear("Left leather boot", shinL, new Vector3(0, -.34f, .022f), new Vector3(.235f, .21f, .305f), 6);
                Gear("Right leather boot", shinR, new Vector3(0, -.34f, .022f), new Vector3(.235f, .21f, .305f), 6);
            }
            if (ids[4] != 0)
            {
                Gear("Riveted helmet crown", headPivot, new Vector3(0, .40f, 0), new Vector3(.45f, .095f, .45f), 8, true);
                Gear("Helmet back", headPivot, new Vector3(0, .20f, -.22f), new Vector3(.45f, .34f, .055f), 8, true);
                for (int side = -1; side <= 1; side += 2)
                    Gear("Helmet cheek", headPivot, new Vector3(side * .22f, .20f, -.01f), new Vector3(.055f, .32f, .42f), 8, true);
            }
            if (ids[5] != 0)
            {
                Gear("Breastplate", transform, new Vector3(0, 1.07f, .165f), new Vector3(.49f, .47f, .085f), 8, true);
                Gear("Left pauldron", upperL, new Vector3(0, -.055f, 0), new Vector3(.24f, .18f, .27f), 9, true);
                Gear("Right pauldron", upperR, new Vector3(0, -.055f, 0), new Vector3(.24f, .18f, .27f), 9, true);
            }
            if (ids[6] != 0)
            {
                Gear("Left leg plate", thighL, new Vector3(0, -.17f, .135f), new Vector3(.23f, .34f, .06f), 8, true);
                Gear("Right leg plate", thighR, new Vector3(0, -.17f, .135f), new Vector3(.23f, .34f, .06f), 8, true);
            }
            if (ids[7] != 0)
            {
                Gear("Left armored boot", shinL, new Vector3(0, -.345f, .02f), new Vector3(.25f, .225f, .325f), 9, true);
                Gear("Right armored boot", shinR, new Vector3(0, -.345f, .02f), new Vector3(.25f, .225f, .325f), 9, true);
            }
        }

        void Gear(string name, Transform parent, Vector3 position, Vector3 scale, int tile, bool armored = false)
        {
            extras.Add(BlockGeometry.Part(name, parent, position * ModelScale, scale * ModelScale, armored ? metal : material, tile).gameObject);
        }

        void LateUpdate()
        {
            if (!thighL) return;
            float dt = Mathf.Max(.0001f, Time.deltaTime);
            Vector3 delta = transform.position - previous;
            previous = transform.position;
            if (resting) { ApplyRestPose(); return; }
            float speed = new Vector3(delta.x, 0, delta.z).magnitude / dt, vy = delta.y / dt;
            if (speed > 45 || Mathf.Abs(vy) > 60) { speed = 0; vy = 0; }
            float follow = 1 - Mathf.Exp(-12 * dt);
            smoothSpeed = Mathf.Lerp(smoothSpeed, speed, follow);
            airWeight = Mathf.Lerp(airWeight, Mathf.Abs(vy) > 2.2f ? 1f : 0f, 1 - Mathf.Exp(-10 * dt));
            phase += dt * Mathf.Clamp(smoothSpeed * 2, 0, 12);
            float swing = Mathf.Sin(phase) * Mathf.Clamp(smoothSpeed * 7, 0, 32);
            float stride = Mathf.Clamp(smoothSpeed * 4, 0, 34);
            float idle = 1 - Mathf.Clamp01(smoothSpeed * 2);
            float t = Time.time;
            float breatheX = Mathf.Sin(t * 1.3f) * 1.5f * idle, breatheZ = Mathf.Sin(t * 1.6f) * 2.2f * idle;
            float kneeL = Mathf.Lerp((1 - Mathf.Cos(phase)) * .5f * stride, 34f, airWeight);
            float kneeR = Mathf.Lerp((1 - Mathf.Cos(phase + Mathf.PI)) * .5f * stride, 26f, airWeight);
            float thighLa = Mathf.Lerp(swing, -14f, airWeight), thighRa = Mathf.Lerp(-swing, 16f, airWeight);
            float spread = Mathf.Lerp(0f, 22f, airWeight);
            float elbowBase = -12 - Mathf.Clamp(smoothSpeed * 2.4f, 0, 18) - (carriedId == 148 ? 26 : 0);
            thighL.localRotation = Quaternion.Euler(thighLa, 0, 0);
            thighR.localRotation = Quaternion.Euler(thighRa, 0, 0);
            shinL.localRotation = Quaternion.Euler(kneeL, 0, 0);
            shinR.localRotation = Quaternion.Euler(kneeR, 0, 0);
            upperL.localRotation = Quaternion.Euler(-swing * .7f + breatheX, 0, -breatheZ - spread);
            upperR.localRotation = Quaternion.Euler(swing * .7f - breatheX, 0, breatheZ + spread);
            foreL.localRotation = Quaternion.Euler(Mathf.Lerp(elbowBase, -36f, airWeight) - swing * .25f, 0, 0);
            foreR.localRotation = Quaternion.Euler(Mathf.Lerp(elbowBase, -36f, airWeight) + swing * .25f, 0, 0);

            if (scarfTail)
                scarfTail.localRotation = Quaternion.Euler(-Mathf.Clamp(smoothSpeed * 3.2f, 0, 26) + Mathf.Sin(t * 2.1f + phase) * 5, 0, Mathf.Sin(t * 1.7f) * 4);

            blinkIn -= dt;
            if (blinkLeft > 0) blinkLeft -= dt;
            if (blinkIn <= 0 && blinkLeft <= 0)
            {
                blinkLeft = .11f;
                blinkIn = 2.4f + (Mathf.Sin(GetInstanceID() + t) * .5f + .5f) * 3.4f;
            }
            if (headFilter)
            {
                bool want = blinkLeft > 0;
                if (want != blinking)
                {
                    blinking = want;
                    headFilter.sharedMesh = BlockGeometry.Box(0, blinking ? faceClosed : faceOpen);
                }
            }

            // Natural gaze toward nearby viewer in front of the character; relaxes forward otherwise
            if (headPivot && Camera.main)
            {
                Vector3 toCam = Camera.main.transform.position - headPivot.position;
                float distSq = toCam.sqrMagnitude;
                float targetYaw = 0f, targetPitch = 0f;
                if (distSq > .04f && distSq < 144f)
                {
                    Vector3 localDir = Quaternion.Inverse(transform.rotation) * toCam.normalized;
                    if (localDir.z > -.1f)
                    {
                        targetYaw = Mathf.Clamp(Mathf.Atan2(localDir.x, Mathf.Max(.05f, localDir.z)) * Mathf.Rad2Deg, -34f, 34f);
                        targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localDir.y, -1f, 1f)) * Mathf.Rad2Deg, -20f, 20f);
                    }
                }
                lookYaw = Mathf.Lerp(lookYaw, targetYaw, 1 - Mathf.Exp(-7 * dt));
                lookPitch = Mathf.Lerp(lookPitch, targetPitch, 1 - Mathf.Exp(-7 * dt));
                headPivot.localRotation = Quaternion.Euler(lookPitch, lookYaw, 0);
            }
        }

        public void Release()
        {
            if (metal) { if (Application.isPlaying) Destroy(metal); else DestroyImmediate(metal); metal = null; }
            if (shadow) { if (Application.isPlaying) Destroy(shadow); else DestroyImmediate(shadow); shadow = null; }
            if (hair) { if (Application.isPlaying) Destroy(hair); else DestroyImmediate(hair); hair = null; }
            if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); material = null; }
        }

        void OnDestroy() { Release(); }
    }
}
