using UnityEngine;
using LestingCraft.Gameplay;

namespace LestingCraft.Presentation
{
 [DefaultExecutionOrder(25)]
 public sealed class SettlerVisual : MonoBehaviour
 {
  CharacterVisual villager;
  Transform head, tail, workArm;
  GameObject workTool;
  Material workMaterial, skinMaterial, hairMaterial, coatMaterial, accentMaterial;
  bool working;
  int profession;
  readonly System.Collections.Generic.List<Transform> legs = new System.Collections.Generic.List<Transform>();
  Vector3 previous;
  float phase;
  bool cat;

  public void Build(int kind, int job) { Build(kind, job, "v:legacy:" + job); }

  public void Build(int kind, int job, string actorId)
  {
   cat = kind == 1;
   profession = job;
   previous = transform.position;
   if (!cat)
   {
    var look = SettlerAppearance.For(actorId, job);
    villager = gameObject.AddComponent<CharacterVisual>();
    villager.Build();
    villager.SetFaceTiles(14, 15);
    Color[] skins =
    {
     new Color(1f,.88f,.78f), new Color(.96f,.77f,.60f), new Color(.80f,.58f,.41f), new Color(.62f,.42f,.30f),
     new Color(.93f,.72f,.57f), new Color(.99f,.84f,.72f), new Color(.72f,.49f,.35f), new Color(.50f,.34f,.24f)
    };
    Color[] coats =
    {
     new Color(.87f,.76f,.54f), new Color(.46f,.73f,.76f), new Color(.58f,.76f,.47f), new Color(.80f,.49f,.40f),
     new Color(.42f,.35f,.72f), new Color(.92f,.93f,.95f), new Color(.69f,.50f,.32f), new Color(.36f,.42f,.49f),
     new Color(.56f,.22f,.28f), new Color(.92f,.52f,.18f)
    };
    Color[] hair =
    {
     new Color(.34f,.23f,.16f), new Color(.72f,.45f,.22f), new Color(.18f,.20f,.24f), new Color(.82f,.74f,.50f),
     new Color(.50f,.29f,.20f), new Color(.86f,.86f,.89f), new Color(.62f,.28f,.17f), new Color(.25f,.20f,.28f)
    };
    workMaterial = BlockGeometry.CharacterMaterial();
    skinMaterial = new Material(workMaterial); skinMaterial.SetColor("_BaseColor", skins[look.Skin % skins.Length]);
    hairMaterial = new Material(workMaterial); hairMaterial.SetColor("_BaseColor", hair[look.Hair % hair.Length]);
    coatMaterial = new Material(workMaterial); coatMaterial.SetColor("_BaseColor", coats[look.Coat % coats.Length]);
    accentMaterial = new Material(workMaterial); accentMaterial.SetColor("_BaseColor", new Color(.96f, .82f, .32f));

    foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
    {
     string nm = r.name;
     if (nm == "Cubical head" || nm == "Neck column" || nm == "Nose" || nm == "Ear" || nm == "Block hand")
      r.sharedMaterial = skinMaterial;
     else if (nm == "Block torso" || nm == "Tunic hem" || nm == "Cuboid sleeve" || nm == "Cuboid forearm" || nm == "Scarf collar" || nm == "Scarf tail")
      r.sharedMaterial = coatMaterial;
     else if (nm == "Hair cap" || nm == "Hair back" || nm == "Side lock")
      r.sharedMaterial = hairMaterial;
    }

    Transform headPivot = transform.Find("Neck pivot");
    if (!headPivot) headPivot = transform;
    foreach (string hide in new[] { "Block hood top", "Block hood back" })
    {
     var hPart = headPivot.Find(hide);
     if (hPart) hPart.gameObject.SetActive(false);
    }

    int toolId =
     job == 1 ? 160 : // Blacksmith hammer
     job == 0 ? 113 : // Farmer grain sheaf
     job == 3 ? 11  : // Astronomer crystal prism
     job == 4 ? 112 : // Healer roasted berries tonic
     job == 5 ? 102 : // Carpenter axe
     job == 6 ? 150 : // Miner iron pickaxe
     job == 7 ? 156 : // Guard spear
     job == 8 ? 57  : // Librarian tome / bookshelf miniature
     job == 9 ? 173 : // Cosmodrome engineer fuel canister
     139;             // Merchant pouch

    workArm = transform.Find("Right shoulder pivot"); if (!workArm) workArm = transform;
    workTool = new GameObject("Village work tool", typeof(MeshFilter), typeof(MeshRenderer));
    workTool.transform.SetParent(workArm, false);
    workTool.transform.localPosition = new Vector3(0, -.60f, .17f);
    workTool.transform.localScale = Vector3.one * (job == 1 || job == 7 ? .42f : .28f);
    workTool.GetComponent<MeshFilter>().sharedMesh = VoxelItemModels.MeshFor(toolId);
    workTool.GetComponent<MeshRenderer>().sharedMaterial = Resources.Load<Material>("Generated/VoxelObjects");
    workTool.SetActive(false);

    BlockGeometry.Part("Coloured work coat", transform, new Vector3(0, 1.04f, .142f), new Vector3(.45f, .46f, .026f), coatMaterial, 13);
    BlockGeometry.Part("Coat hem", transform, new Vector3(0, .77f, .01f), new Vector3(.48f, .10f, .29f), coatMaterial, 1);

    // Individual facial & clothing accessories parented cleanly so head details move with headPivot!
    if (look.Detail == 0) BlockGeometry.Part("Short beard", headPivot, new Vector3(0, .035f, .195f), new Vector3(.22f, .075f, .032f), hairMaterial, 10);
    else if (look.Detail == 1) BlockGeometry.Part("Apron pocket", transform, new Vector3(.09f, .90f, .162f), new Vector3(.15f, .11f, .022f), workMaterial, 6);
    else if (look.Detail == 2) BlockGeometry.Part("Neck scarf", transform, new Vector3(0, 1.35f, .01f), new Vector3(.48f, .085f, .31f), accentMaterial, 1);
    else if (look.Detail == 3 || job == 8)
    {
     BlockGeometry.Part("Scholar spectacles bridge", headPivot, new Vector3(0, .208f, .202f), new Vector3(.27f, .022f, .018f), accentMaterial, 5);
    }
    else if (look.Detail == 4 || job == 2 || job == 4)
    {
     BlockGeometry.Part("Travel backpack", transform, new Vector3(0, 1.08f, -.21f), new Vector3(.35f, .40f, .15f), workMaterial, 6);
    }
    else
    {
     BlockGeometry.Part("Braided beard", headPivot, new Vector3(0, -.01f, .198f), new Vector3(.14f, .15f, .04f), hairMaterial, 10);
    }

    // Distinct profession & individual headwear parented to headPivot (local head center = y:.185, top = y:.375)
    switch (look.Headwear)
    {
     case 0:
      BlockGeometry.Part("Wide hat brim", headPivot, new Vector3(0, .385f, 0), new Vector3(.60f, .048f, .58f), coatMaterial, job == 0 ? 5 : 6);
      BlockGeometry.Part("Hat crown", headPivot, new Vector3(0, .455f, -.015f), new Vector3(.39f, .12f, .39f), coatMaterial, job == 0 ? 5 : 6);
      break;
     case 1:
      BlockGeometry.Part("Short cap", headPivot, new Vector3(0, .395f, 0), new Vector3(.42f, .08f, .42f), coatMaterial, 1);
      BlockGeometry.Part("Cap peak", headPivot, new Vector3(0, .372f, .225f), new Vector3(.34f, .032f, .14f), coatMaterial, 1);
      break;
     case 2:
      BlockGeometry.Part("Side-parted hair", headPivot, new Vector3(-.03f, .392f, .01f), new Vector3(.38f, .065f, .41f), hairMaterial, 10);
      BlockGeometry.Part("Side locks", headPivot, new Vector3(-.198f, .22f, -.03f), new Vector3(.048f, .25f, .33f), hairMaterial, 10);
      break;
     case 4: // Astronomer pointed star hat
      BlockGeometry.Part("Astronomer brim", headPivot, new Vector3(0, .382f, 0), new Vector3(.64f, .045f, .64f), coatMaterial, 1);
      BlockGeometry.Part("Astronomer cone", headPivot, new Vector3(0, .495f, -.015f), new Vector3(.34f, .20f, .34f), coatMaterial, 1);
      BlockGeometry.Part("Astronomer spire", headPivot, new Vector3(0, .645f, -.03f), new Vector3(.17f, .15f, .17f), accentMaterial, 5);
      break;
     case 5: // Miner hard-hat + headlamp
      BlockGeometry.Part("Miner hard hat", headPivot, new Vector3(0, .40f, 0), new Vector3(.44f, .11f, .46f), accentMaterial, 8);
      BlockGeometry.Part("Miner headlamp", headPivot, new Vector3(0, .415f, .238f), new Vector3(.10f, .08f, .045f), workMaterial, 5);
      break;
     case 6: // Guard iron helm + crest
      BlockGeometry.Part("Guard iron helm", headPivot, new Vector3(0, .405f, 0), new Vector3(.44f, .13f, .44f), workMaterial, 8);
      BlockGeometry.Part("Guard plume", headPivot, new Vector3(0, .515f, -.02f), new Vector3(.085f, .13f, .36f), coatMaterial, 1);
      break;
     case 7: // Cosmodrome flight comms headset
      BlockGeometry.Part("Comms headband", headPivot, new Vector3(0, .392f, 0), new Vector3(.44f, .045f, .10f), workMaterial, 8);
      BlockGeometry.Part("Comms earcup L", headPivot, new Vector3(-.225f, .18f, 0), new Vector3(.055f, .13f, .13f), accentMaterial, 8);
      BlockGeometry.Part("Comms earcup R", headPivot, new Vector3(.225f, .18f, 0), new Vector3(.055f, .13f, .13f), accentMaterial, 8);
      break;
     default:
      BlockGeometry.Part("Long hair back", headPivot, new Vector3(0, .12f, -.212f), new Vector3(.40f, .46f, .048f), hairMaterial, 10);
      BlockGeometry.Part("Fringe", headPivot, new Vector3(0, .365f, .115f), new Vector3(.395f, .058f, .19f), hairMaterial, 10);
      break;
    }
    return;
   }

   int fur = System.Array.IndexOf(SurfaceLayers.Names, "cat-fur"), pale = 55;
   var body = new AnimalVisual.Part();
   body.Box(new Vector3(0, .31f, 0), new Vector3(.27f, .28f, .56f), Color.white, fur);
   body.Box(new Vector3(0, .20f, .08f), new Vector3(.19f, .08f, .38f), Color.white, pale);
   body.Build("Tabby fur and chest", transform, Vector3.zero);
   var h = new AnimalVisual.Part();
   h.Box(Vector3.zero, new Vector3(.28f, .24f, .25f), Color.white, fur);
   h.Box(new Vector3(0, -.065f, .145f), new Vector3(.19f, .095f, .095f), Color.white, pale);
   h.Box(new Vector3(0, -.022f, .202f), new Vector3(.044f, .026f, .016f), new Color(.78f, .46f, .44f), pale);
   for (int side = -1; side <= 1; side += 2)
   {
    h.Box(new Vector3(side * .10f, .155f, 0), new Vector3(.075f, .14f, .085f), Color.white, fur);
    h.Box(new Vector3(side * .10f, .16f, .044f), new Vector3(.04f, .09f, .012f), new Color(.86f, .62f, .60f), pale);
    h.Box(new Vector3(side * .076f, .025f, .128f), new Vector3(.062f, .048f, .016f), new Color(.48f, .74f, .30f), pale);
    h.Box(new Vector3(side * .076f, .025f, .139f), new Vector3(.016f, .038f, .012f), Color.black, pale);
    for (int i = 0; i < 3; i++)
     h.Box(new Vector3(side * .155f, -.044f - i * .016f, .165f), new Vector3(.16f, .006f, .006f), new Color(.84f, .84f, .80f), pale);
   }
   head = h.Build("Square head, ears, whiskers and eyes", transform, new Vector3(0, .47f, .29f)).transform;
   for (int i = 0; i < 4; i++)
   {
    var p = new AnimalVisual.Part();
    p.Box(new Vector3(0, -.10f, 0), new Vector3(.082f, .21f, .092f), Color.white, fur);
    p.Box(new Vector3(0, -.19f, .024f), new Vector3(.088f, .058f, .135f), Color.white, pale);
    legs.Add(p.Build("Cat paw " + i, transform, new Vector3(i % 2 == 0 ? -.098f : .098f, .24f, i < 2 ? -.195f : .195f)).transform);
   }
   tail = new GameObject("Articulated voxel tail").transform;
   tail.SetParent(transform, false);
   tail.localPosition = new Vector3(0, .40f, -.27f);
   Transform joint = tail;
   for (int i = 0; i < 5; i++)
   {
    var p = new AnimalVisual.Part();
    p.Box(new Vector3(0, 0, -.055f), new Vector3(.055f - i * .005f, .055f - i * .005f, .13f), Color.white, fur);
    joint = p.Build("Tail joint " + i, joint, i == 0 ? Vector3.zero : new Vector3(0, 0, -.11f)).transform;
    joint.localRotation = Quaternion.Euler(-12, 0, 0);
   }
  }

  public void Animate(LivingActor a, float dt)
  {
   working = a.working;
   if (!cat)
   {
    if (workTool) workTool.SetActive(working || profession == 7);
    return;
   }
   float speed = (transform.position - previous).magnitude / Mathf.Max(.001f, dt);
   previous = transform.position;
   phase += dt * speed * 8;
   for (int i = 0; i < legs.Count; i++)
    legs[i].localRotation = Quaternion.Euler(a.sitting ? (i < 2 ? -70 : 0) : Mathf.Sin(phase + (i == 0 || i == 3 ? 0 : Mathf.PI)) * Mathf.Min(35, speed * 15), 0, 0);
   tail.localRotation = Quaternion.Euler(a.sitting ? -55 : -10, Mathf.Sin(Time.time * 1.6f) * 18, 0);
   head.localRotation = Quaternion.Euler(a.sitting ? 8 : 0, Mathf.Sin(Time.time * .6f) * 7, 0);
  }

  void LateUpdate()
  {
   if (!cat && working && workArm)
    workArm.localRotation = Quaternion.Euler(profession == 1 || profession == 6 ? -45 + Mathf.Sin(Time.time * 4.8f) * 35 : -35 + Mathf.Sin(Time.time * 2.8f) * 14, 0, 0);
  }

  static void SafeDestroy(Object obj)
  {
   if (!obj) return;
   if (Application.isPlaying) Destroy(obj);
   else DestroyImmediate(obj);
  }

  void OnDestroy()
  {
   SafeDestroy(workMaterial);
   SafeDestroy(skinMaterial);
   SafeDestroy(hairMaterial);
   SafeDestroy(coatMaterial);
   SafeDestroy(accentMaterial);
  }
 }
}
