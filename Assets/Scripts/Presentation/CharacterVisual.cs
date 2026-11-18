using UnityEngine;
using System.Collections.Generic;
namespace LestingCraft.Presentation
{
    /// <summary>
    /// Original block adventurer, second rig: cuboid head with a real neck pivot that looks
    /// around and blinks (second face tile), two-segment legs with knees, two-segment arms with
    /// elbows, a swaying scarf tail, textured cloth and equipment. Still one blocky silhouette.
    /// </summary>
    public sealed class CharacterVisual : MonoBehaviour
    {
        Transform thighL, thighR, shinL, shinR, upperL, upperR, foreL, foreR, headPivot, scarfTail;
        MeshFilter headFilter;
        Vector3 previous; float phase; Material material, metal, shadow, hair; string skinKey = ""; readonly List<GameObject> extras = new List<GameObject>(); string gearKey = "";
        GameObject carried; int carriedId = -1; bool resting; int restingVariant; float smoothSpeed, airWeight;
        float blinkIn = 2.6f, blinkLeft; bool blinking; float lookYaw, lookPitch;
        public void SetRestPose(int variant) { resting = true; restingVariant = variant; ApplyRestPose(); }
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
            if (key == skinKey) return; skinKey = key;
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
            if (carriedId == id) return; carriedId = id; if (carried) Destroy(carried);
            if (id != 148 || !foreL) return;
            carried = new GameObject("Visible left-hand torch");
            carried.transform.SetParent(foreL, false);
            carried.transform.localPosition = new Vector3(0, -.24f, .16f);
            carried.transform.localRotation = Quaternion.Euler(-20, 0, 0);
            carried.transform.localScale = Vector3.one * .75f;
            carried.AddComponent<TorchVisual>().Build(lighting);
        }
        public void Build()
        {
            if (material) return;
            material = BlockGeometry.CharacterMaterial();
            material.SetFloat("_Smoothness", .22f);
            metal = new Material(material); metal.SetFloat("_Metallic", .65f); metal.SetFloat("_Smoothness", .34f);
            shadow = new Material(material); shadow.SetColor("_BaseColor", new Color(.11f, .12f, .14f, 1f));
            hair = new Material(material); hair.SetColor("_BaseColor", new Color(.17f, .12f, .09f, 1f));
            Part("Block torso", new Vector3(0, 1.06f, 0), new Vector3(.42f, .58f, .26f), 1, transform);
            Part("Belt", new Vector3(0, .80f, .012f), new Vector3(.45f, .085f, .30f), 6, transform);
            Part("Square buckle", new Vector3(0, .80f, .174f), new Vector3(.095f, .07f, .018f), 5, transform);
            Part("Backpack", new Vector3(0, 1.06f, -.23f), new Vector3(.35f, .44f, .23f), 6, transform);
            Part("Backpack flap", new Vector3(0, 1.26f, -.25f), new Vector3(.38f, .10f, .25f), 7, transform);
            Part("Scarf collar", new Vector3(0, 1.32f, .01f), new Vector3(.37f, .085f, .29f), 1, transform);
            BlockGeometry.Part("Collar shadow", transform, new Vector3(0, 1.285f, .01f), new Vector3(.37f, .02f, .28f), shadow, 6);
            scarfTail = Pivot("Scarf tail", new Vector3(0, 1.29f, -.19f));
            Part("Scarf tail", new Vector3(0, -.16f, -.02f), new Vector3(.15f, .30f, .05f), 1, scarfTail);
            // head on a neck pivot: looks around and blinks
            headPivot = Pivot("Neck pivot", new Vector3(0, 1.41f, 0));
            headFilter = Part("Cubical head", new Vector3(0, .135f, 0), new Vector3(.36f, .38f, .36f), 0, headPivot, 3).GetComponent<MeshFilter>();
            Part("Jaw", new Vector3(0, -.105f, .01f), new Vector3(.30f, .075f, .30f), 3, headPivot);
            // dark sockets, nose and mouth line give the face real depth instead of flat paint
            for (int i = -1; i <= 1; i += 2)
            {
                Part("Eye socket", new Vector3(i * .085f, .172f, .176f), new Vector3(.055f, .055f, .022f), 6, headPivot);
                Part("Eye", new Vector3(i * .085f, .172f, .193f), new Vector3(.045f, .045f, .012f), 4, headPivot);
                Part("Ear", new Vector3(i * .185f, .13f, 0), new Vector3(.04f, .12f, .09f), 3, headPivot);
            }
            Part("Nose", new Vector3(0, .105f, .208f), new Vector3(.075f, .075f, .055f), 3, headPivot);
            Part("Mouth line", new Vector3(0, .045f, .202f), new Vector3(.10f, .018f, .012f), 6, headPivot);
            // hair cap and side locks use the dedicated hair material so they read as hair, not skin
            BlockGeometry.Part("Hair cap", headPivot, new Vector3(0, .258f, -.005f), new Vector3(.375f, .095f, .375f), hair, 6);
            for (int i = -1; i <= 1; i += 2)
                BlockGeometry.Part("Side lock", headPivot, new Vector3(i * .175f, .115f, -.01f), new Vector3(.045f, .17f, .30f), hair, 6);
            Part("Block hood top", new Vector3(0, .342f, -.015f), new Vector3(.43f, .045f, .43f), 1, headPivot);
            Part("Block hood back", new Vector3(0, .13f, -.21f), new Vector3(.43f, .40f, .04f), 1, headPivot);
            for (int i = -1; i <= 1; i += 2)
            {
                Part("Pack strap", new Vector3(i * .16f, 1.08f, .145f), new Vector3(.044f, .52f, .022f), 6, transform);
                Part("Square coat clasp", new Vector3(i * .16f, 1.21f, .164f), new Vector3(.06f, .043f, .018f), 5, transform);
            }
            thighL = Leg("Left", -.125f); thighR = Leg("Right", .125f);
            upperL = Arm("Left", -.33f); upperR = Arm("Right", .33f);
            shinL = thighL.Find("Shin pivot"); shinR = thighR.Find("Shin pivot");
            foreL = upperL.Find("Elbow pivot"); foreR = upperR.Find("Elbow pivot");
            previous = transform.position;
        }
        Transform Pivot(string name, Vector3 position)
        { var pivot = new GameObject(name).transform; pivot.SetParent(transform, false); pivot.localPosition = position; return pivot; }
        Transform Leg(string side, float x)
        {
            var thigh = Pivot(side + " thigh pivot", new Vector3(x, .78f, 0));
            Part("Cuboid trousers", new Vector3(0, -.17f, 0), new Vector3(.21f, .37f, .24f), 2, thigh);
            var shin = Pivot("Shin pivot", new Vector3(0, -.35f, 0));
            shin.SetParent(thigh, false);
            Part("Cuboid shin", new Vector3(0, -.16f, 0), new Vector3(.20f, .33f, .23f), 2, shin);
            Part("Cuboid boot", new Vector3(0, -.345f, .018f), new Vector3(.22f, .18f, .30f), 4, shin);
            Part("Boot heel", new Vector3(0, -.415f, -.12f), new Vector3(.06f, .07f, .09f), 5, shin);
            Part("Boot sole", new Vector3(0, -.445f, .022f), new Vector3(.235f, .026f, .305f), 7, shin);
            return thigh;
        }
        Transform Arm(string side, float x)
        {
            var upper = Pivot(side + " shoulder pivot", new Vector3(x, 1.30f, 0));
            Part("Cuboid sleeve", new Vector3(0, -.15f, 0), new Vector3(.19f, .33f, .24f), 1, upper);
            var fore = Pivot("Elbow pivot", new Vector3(0, -.30f, 0));
            fore.SetParent(upper, false);
            Part("Cuboid forearm", new Vector3(0, -.13f, 0), new Vector3(.18f, .27f, .23f), 1, fore);
            Part("Square cuff", new Vector3(0, -.26f, 0), new Vector3(.20f, .06f, .25f), 6, fore);
            Part("Block hand", new Vector3(0, -.34f, 0), new Vector3(.185f, .16f, .22f), 0, fore);
            return upper;
        }
        Transform Part(string name, Vector3 position, Vector3 scale, int tile, Transform parent, int front = -1)
            => BlockGeometry.Part(name, parent, position, scale, material, tile, front);
        public void SetEquipment(int[] ids)
        {
            if (ids == null || ids.Length != 8) ids = new int[8]; string key = string.Join(",", ids); if (key == gearKey) return; gearKey = key;
            foreach (var obj in extras) if (obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); } extras.Clear();
            if (!thighL) return;
            if (ids[0] != 0) Gear("Cloth hood", headPivot, new Vector3(0, .35f, 0), new Vector3(.46f, .08f, .46f), 1);
            if (ids[1] != 0) { Gear("Warm mantle back", transform, new Vector3(0, 1.10f, -.24f), new Vector3(.58f, .67f, .09f), 7); Gear("Mantle collar", transform, new Vector3(0, 1.34f, -.01f), new Vector3(.59f, .10f, .35f), 6); }
            if (ids[2] != 0) { Gear("Left trouser shell", thighL, new Vector3(0, -.18f, 0), new Vector3(.24f, .40f, .27f), 2); Gear("Right trouser shell", thighR, new Vector3(0, -.18f, 0), new Vector3(.24f, .40f, .27f), 2); }
            if (ids[3] != 0) { Gear("Left leather boot", shinL, new Vector3(0, -.35f, .03f), new Vector3(.25f, .22f, .32f), 6); Gear("Right leather boot", shinR, new Vector3(0, -.35f, .03f), new Vector3(.25f, .22f, .32f), 6); }
            if (ids[4] != 0) { Gear("Riveted helmet crown", headPivot, new Vector3(0, .36f, 0), new Vector3(.49f, .10f, .49f), 8, true); Gear("Helmet back", headPivot, new Vector3(0, .15f, -.24f), new Vector3(.49f, .36f, .06f), 8, true); for (int side = -1; side <= 1; side += 2) Gear("Helmet cheek", headPivot, new Vector3(side * .23f, .15f, 0), new Vector3(.06f, .34f, .46f), 8, true); }
            if (ids[5] != 0) { Gear("Breastplate", transform, new Vector3(0, 1.06f, .19f), new Vector3(.52f, .49f, .10f), 8, true); Gear("Left pauldron", upperL, new Vector3(0, -.06f, 0), new Vector3(.26f, .19f, .31f), 9, true); Gear("Right pauldron", upperR, new Vector3(0, -.06f, 0), new Vector3(.26f, .19f, .31f), 9, true); }
            if (ids[6] != 0) { Gear("Left leg plate", thighL, new Vector3(0, -.17f, .15f), new Vector3(.245f, .36f, .07f), 8, true); Gear("Right leg plate", thighR, new Vector3(0, -.17f, .15f), new Vector3(.245f, .36f, .07f), 8, true); }
            if (ids[7] != 0) { Gear("Left armored boot", shinL, new Vector3(0, -.36f, .025f), new Vector3(.27f, .24f, .35f), 9, true); Gear("Right armored boot", shinR, new Vector3(0, -.36f, .025f), new Vector3(.27f, .24f, .35f), 9, true); }
        }
        void Gear(string name, Transform parent, Vector3 position, Vector3 scale, int tile, bool armored = false)
        { extras.Add(BlockGeometry.Part(name, parent, position, scale, armored ? metal : material, tile).gameObject); }
        void LateUpdate()
        {
            if (!thighL) return;
            float dt = Mathf.Max(.0001f, Time.deltaTime);
            Vector3 delta = transform.position - previous; previous = transform.position;
            if (resting) { ApplyRestPose(); return; }
            float speed = new Vector3(delta.x, 0, delta.z).magnitude / dt, vy = delta.y / dt;
            if (speed > 45 || Mathf.Abs(vy) > 60) { speed = 0; vy = 0; } // teleport / respawn: no animation spike
            float follow = 1 - Mathf.Exp(-12 * dt);
            smoothSpeed = Mathf.Lerp(smoothSpeed, speed, follow);
            airWeight = Mathf.Lerp(airWeight, Mathf.Abs(vy) > 2.2f ? 1f : 0f, 1 - Mathf.Exp(-10 * dt));
            phase += dt * Mathf.Clamp(smoothSpeed * 2, 0, 12);
            float swing = Mathf.Sin(phase) * Mathf.Clamp(smoothSpeed * 7, 0, 32);
            float stride = Mathf.Clamp(smoothSpeed * 4, 0, 34);
            float idle = 1 - Mathf.Clamp01(smoothSpeed * 2); float t = Time.time;
            // Idle breathing: limbs drift a few degrees; fades out as soon as the character moves.
            float breatheX = Mathf.Sin(t * 1.3f) * 1.6f * idle, breatheZ = Mathf.Sin(t * 1.6f) * 2.6f * idle;
            // Airborne pose: arms lift outward, knees tuck (blended in/out smoothly).
            float kneeL = Mathf.Lerp((1 - Mathf.Cos(phase)) * .5f * stride, 34f, airWeight);
            float kneeR = Mathf.Lerp((1 - Mathf.Cos(phase + Mathf.PI)) * .5f * stride, 26f, airWeight);
            float thighLa = Mathf.Lerp(swing, -14f, airWeight), thighRa = Mathf.Lerp(-swing, 16f, airWeight);
            float spread = Mathf.Lerp(0f, 24f, airWeight);
            float elbowBase = -14 - Mathf.Clamp(smoothSpeed * 2.5f, 0, 18) - (carriedId == 148 ? 26 : 0);
            thighL.localRotation = Quaternion.Euler(thighLa, 0, 0);
            thighR.localRotation = Quaternion.Euler(thighRa, 0, 0);
            shinL.localRotation = Quaternion.Euler(kneeL, 0, 0);
            shinR.localRotation = Quaternion.Euler(kneeR, 0, 0);
            upperL.localRotation = Quaternion.Euler(-swing * .7f + breatheX, 0, -breatheZ - spread);
            upperR.localRotation = Quaternion.Euler(swing * .7f - breatheX, 0, breatheZ + spread);
            foreL.localRotation = Quaternion.Euler(Mathf.Lerp(elbowBase, -38f, airWeight) - swing * .25f, 0, 0);
            foreR.localRotation = Quaternion.Euler(Mathf.Lerp(elbowBase, -38f, airWeight) + swing * .25f, 0, 0);
            // scarf follows the stride and wind
            if (scarfTail) scarfTail.localRotation = Quaternion.Euler(-Mathf.Clamp(smoothSpeed * 3.2f, 0, 26) + Mathf.Sin(t * 2.1f + phase) * 5, 0, Mathf.Sin(t * 1.7f) * 4);
            // blink: swap the shared face mesh to the closed-eye tile for a moment
            blinkIn -= dt;
            if (blinkLeft > 0) blinkLeft -= dt;
            if (blinkIn <= 0 && blinkLeft <= 0) { blinkLeft = .11f; blinkIn = 2.2f + (Mathf.Sin(GetInstanceID() + t) * .5f + .5f) * 3.4f; }
            if (headFilter)
            {
                bool want = blinkLeft > 0;
                if (want != blinking) { blinking = want; headFilter.sharedMesh = BlockGeometry.Box(0, blinking ? 11 : 3); }
            }
            // head looks towards the camera within a natural range
            if (headPivot && Camera.main)
            {
                Vector3 fwd = Quaternion.Inverse(transform.rotation) * Camera.main.transform.forward;
                float yaw = Mathf.Clamp(Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg, -42f, 42f);
                float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg, -24f, 24f);
                lookYaw = Mathf.Lerp(lookYaw, yaw, 1 - Mathf.Exp(-8 * dt));
                lookPitch = Mathf.Lerp(lookPitch, pitch, 1 - Mathf.Exp(-8 * dt));
                headPivot.localRotation = Quaternion.Euler(lookPitch, lookYaw, 0);
            }
        }
        public void Release() { if (metal) { if (Application.isPlaying) Destroy(metal); else DestroyImmediate(metal); metal = null; } if (shadow) { if (Application.isPlaying) Destroy(shadow); else DestroyImmediate(shadow); shadow = null; } if (hair) { if (Application.isPlaying) Destroy(hair); else DestroyImmediate(hair); hair = null; } if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); material = null; } }
        void OnDestroy() { Release(); }
    }
}
