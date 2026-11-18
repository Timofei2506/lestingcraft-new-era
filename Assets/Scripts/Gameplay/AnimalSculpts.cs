using System;
using UnityEngine;
using LestingCraft.Presentation;

namespace LestingCraft.Gameplay
{
    /// <summary>
    /// Pure voxel recipes for every wildlife species. Each species is authored through the same
    /// <see cref="Sculptor"/> engine used for planetary bosses, so animals are real
    /// surface-culled voxel models (bevelled ellipsoids, limbs, hooves, horns) instead of
    /// flat box drawings. Part names ("body", "head", "leg0..N", "tail", "wingL/R", "earL/R")
    /// are the articulation pivots <see cref="AnimalVisual"/> animates; geometry is authored in
    /// absolute world metres with the pivot at the joint, so rotating a part rotates around it.
    /// </summary>
    public static class AnimalSculpts
    {
        public const float Unit = .12f;

        // Palette slots (indices into the palette built by Palette()).
        const int FUR = 0, UNDER = 1, KERATIN = 2, SCLERA = 3, IRIS = 4, GLOW = 5, HOOF = 6, BONE = 7;

        public static readonly string[] SpeciesFurNames =
        {
            "deer-fur","hare-fur","boar-bristles","wolf-fur","bear-fur","fox-fur",
            "bison-fur","goat-fur","lynx-fur","beaver-fur","frog-skin","eagle-feathers",
            "owl-feathers","penguin-plumage","camel-coat","scorpion-chitin",
            "turtle-shell","moose-fur","capybara-fur","voidray-skin",
            "ash-hound-hide","cherenkov-leech-skin","rad-scorpion-carapace","mutant-boar-hide","slag-carapace",
            "fallout-crow-plumage","gamma-ray-membrane","bone-stalker-hide","vitrified-golem-crust","wraith-shroud",
            "wolf-fur","scorpion-chitin","turtle-shell","voidray-skin"
        };

        public static readonly string[] SpeciesNames =
        {
            "Олень","Заяц","Кабан","Волк","Бурый медведь","Лисица",
            "Зубр","Горный козёл","Рысь","Бобр","Жаба","Орёл",
            "Полярная сова","Северный пингвин","Верблюд","Песчаный скорпион",
            "Болотная черепаха","Северный лось","Капибара","Эфирный скат",
            "Пепельная гончая","Черенковская пиявка","Радио-скорпион","Двухголовый секач","Шлаковый краб",
            "Пепельный ворон","Гамма-скат","Костяной ловчий","Тринититовый голем","Фантом Спирали",
            "Ледяной волк","Стеклянный паук","Грибной жук","Небесный кит"
        };

        /// <summary>Canonical coat tint per species; assigned to the fur palette before authoring.</summary>
        public static readonly Color[] CoatTints =
        {
            new Color(.52f, .34f, .20f), new Color(.56f, .48f, .38f), new Color(.32f, .22f, .17f),
            new Color(.40f, .40f, .42f), new Color(.30f, .20f, .12f), new Color(.72f, .36f, .12f),
            new Color(.26f, .20f, .16f), new Color(.62f, .58f, .52f), new Color(.66f, .55f, .38f),
            new Color(.34f, .24f, .16f), new Color(.36f, .44f, .24f), new Color(.36f, .26f, .16f),
            new Color(.90f, .90f, .94f), new Color(.16f, .18f, .22f), new Color(.74f, .62f, .44f),
            new Color(.48f, .40f, .20f), new Color(.30f, .36f, .24f), new Color(.28f, .22f, .18f),
            new Color(.46f, .34f, .22f), new Color(.24f, .18f, .34f),
            new Color(.22f, .20f, .19f), new Color(.30f, .62f, .40f), new Color(.34f, .16f, .16f),
            new Color(.44f, .26f, .20f), new Color(.36f, .30f, .28f), new Color(.20f, .18f, .20f),
            new Color(.26f, .40f, .46f), new Color(.62f, .58f, .50f), new Color(.30f, .34f, .30f),
            new Color(.24f, .22f, .30f), new Color(.72f, .78f, .86f), new Color(.62f, .70f, .74f),
            new Color(.56f, .40f, .26f), new Color(.42f, .48f, .58f)
        };

        // 0 quadruped, 1 biped, 2 arthropod, 3 flyer, 4 swimmer, 5 hopper
        const int Q = 0, B = 1, A = 2, F = 3, S = 4, H = 5;

        struct Metrics
        {
            public float width, length, height, bodyY, legLen, headY, headW, headH, headZ;
            public int legs, type;
            public Metrics(float width,float length,float height,float bodyY,float legLen,float headY,float headW,float headH,float headZ,int legs,int type)
            {
                this.width=width;this.length=length;this.height=height;this.bodyY=bodyY;this.legLen=legLen;
                this.headY=headY;this.headW=headW;this.headH=headH;this.headZ=headZ;this.legs=legs;this.type=type;
            }
        }

        static readonly Metrics[] Table =
        {
            // 0 deer       1 hare       2 boar        3 wolf        4 bear         5 fox
            new Metrics(.5f,1.05f,.55f,.95f,.66f,1.42f,.26f,.30f,.50f,4,Q),
            new Metrics(.3f,.45f,.30f,.26f,.20f,.40f,.24f,.24f,.24f,4,H),
            new Metrics(.62f,1.08f,.55f,.54f,.38f,.64f,.42f,.36f,.50f,4,Q),
            new Metrics(.48f,1.15f,.48f,.72f,.54f,.98f,.32f,.32f,.55f,4,Q),
            new Metrics(1.12f,1.6f,1.05f,.98f,.60f,1.31f,.62f,.48f,.80f,4,Q),
            new Metrics(.32f,.83f,.36f,.46f,.34f,.66f,.26f,.26f,.40f,4,Q),
            // 6 bison      7 goat        8 lynx        9 beaver      10 toad        11 eagle
            new Metrics(1.2f,1.85f,1.05f,1.08f,.66f,1.17f,.70f,.62f,.90f,4,Q),
            new Metrics(.43f,.85f,.54f,.78f,.56f,1.12f,.26f,.34f,.42f,4,Q),
            new Metrics(.47f,.88f,.47f,.62f,.44f,.84f,.38f,.32f,.43f,4,Q),
            new Metrics(.55f,.80f,.43f,.34f,.20f,.45f,.30f,.26f,.38f,4,Q),
            new Metrics(.62f,.61f,.25f,.24f,.18f,.30f,.68f,.22f,.30f,4,H),
            new Metrics(.4f,.65f,.4f,.40f,.20f,.68f,.28f,.26f,.32f,2,F),
            // 12 owl       13 penguin    14 camel      15 scorpion   16 turtle      17 moose
            new Metrics(.38f,.46f,.42f,.38f,.18f,.66f,.36f,.32f,.22f,2,F),
            new Metrics(.42f,.40f,.68f,.48f,.20f,.90f,.30f,.28f,.20f,2,B),
            new Metrics(.72f,1.55f,.74f,1.18f,.90f,1.90f,.34f,.34f,.76f,4,Q),
            new Metrics(.52f,.76f,.22f,.24f,.0f,.28f,.34f,.18f,.38f,6,A),
            new Metrics(.68f,.78f,.38f,.30f,.20f,.40f,.24f,.20f,.38f,4,Q),
            new Metrics(.92f,1.75f,.88f,1.26f,.90f,1.74f,.44f,.48f,.86f,4,Q),
            // 18 capybara  19 voidray    20 ash hound  21 leech      22 rad scorp  23 mutant boar
            new Metrics(.54f,.94f,.50f,.44f,.30f,.60f,.34f,.30f,.46f,4,Q),
            new Metrics(.96f,1.15f,.20f,.34f,.0f,.36f,.52f,.18f,.55f,0,S),
            new Metrics(.54f,1.32f,.56f,.82f,.62f,1.08f,.36f,.34f,.64f,4,Q),
            new Metrics(.46f,.92f,.30f,.24f,.0f,.30f,.34f,.22f,.44f,0,S),
            new Metrics(.66f,.96f,.30f,.30f,.0f,.38f,.38f,.24f,.46f,6,A),
            new Metrics(.78f,1.38f,.72f,.74f,.54f,.96f,.56f,.46f,.68f,4,Q),
            // 24 slag crab 25 crow       26 gamma ray  27 bone stalk 28 vitrified  29 wraith
            new Metrics(.86f,.96f,.46f,.40f,.0f,.52f,.44f,.30f,.48f,6,A),
            new Metrics(.54f,.72f,.44f,.48f,.22f,.62f,.30f,.30f,.36f,2,F),
            new Metrics(.44f,.72f,.44f,.44f,.0f,.50f,.32f,.30f,.36f,0,S),
            new Metrics(.62f,1.25f,.68f,1.24f,.96f,1.68f,.42f,.46f,.62f,2,B),
            new Metrics(1.28f,1.48f,1.24f,1.32f,.82f,2.02f,.64f,.52f,.74f,2,B),
            new Metrics(.66f,1.05f,1.00f,1.16f,.86f,1.80f,.44f,.48f,.52f,2,B),
            // 30 frost wolf 31 glass spider 32 beetle  33 sky whale
            new Metrics(.64f,1.10f,.56f,.74f,.62f,.98f,.40f,.34f,.55f,4,Q),
            new Metrics(.40f,.58f,.22f,.20f,.0f,.30f,.30f,.20f,.29f,8,A),
            new Metrics(.40f,.58f,.18f,.18f,.0f,.28f,.42f,.24f,.29f,6,A),
            new Metrics(1.10f,2.30f,.62f,1.05f,.0f,1.10f,.55f,.34f,1.15f,0,S),
        };

        public static SculptData For(int kind)
        {
            if (kind < 0 || kind >= Table.Length) kind = 3;
            var s = new Sculptor(Unit, (int)(kind * 2654435761u & 0x7fffffff));
            Palette(s, kind);
            var m = Table[kind];
            Build(s, m, kind);
            return s.Finish("animal_" + kind);
        }

        static void Palette(Sculptor s, int kind)
        {
            Color c = CoatTints[kind];
            // Glowing mutants (Radia Abyss 20..29) and cosmic swimmers (19, 26, 33) carry
            // cherenkov emission on eyes/spines; the rest stay opaque.
            bool glow = kind >= 20 && kind < 30 || kind == 19 || kind == 26 || kind == 33;
            s.Pal("fur", AnimalSculpts.SpeciesFurNames[kind], c.r, c.g, c.b, 0);
            s.Pal("underfur", "animal-underfur", Mathf.Min(1, c.r + .3f), Mathf.Min(1, c.g + .3f), Mathf.Min(1, c.b + .25f), 0);
            s.Pal("keratin", "animal-keratin", .82f, .78f, .70f, 0);
            s.Pal("sclera", "animal-keratin", .92f, .88f, .80f, 0);
            s.Pal("iris", "animal-keratin", glow ? .22f : .30f, glow ? .95f : .22f, glow ? 1f : .18f, glow ? 1.6f : 0);
            s.Pal("glow", "cherenkov-crystal", .25f, .88f, 1f, 2.2f);
            s.Pal("hoof", "animal-keratin", .18f, .16f, .14f, 0);
            s.Pal("bone", "animal-keratin", .78f, .76f, .70f, 0);
        }

        static void Build(Sculptor s, Metrics m, int kind)
        {
            bool glow = kind >= 20 && kind < 30 || kind == 19 || kind == 26 || kind == 33;
            bool flyer = m.type == F;
            bool swimmer = m.type == S;
            bool arthropod = m.type == A;
            bool biped = m.type == B;
            bool hopper = m.type == H;

            // ---- Body ----
            s.Part("body", 0, m.bodyY, 0);
            if (swimmer)
            {
                // Tapered torso: round mid, tapered rump, rounded rostrum.
                s.Ellipsoid(0, m.bodyY, 0, m.width * .5f, m.height * .5f, m.length * .42f, FUR);
                s.Ellipsoid(0, m.bodyY, -m.length * .34f, m.width * .36f, m.height * .38f, m.length * .24f, FUR);
                s.Ellipsoid(0, m.bodyY, m.length * .36f, m.width * .34f, m.height * .40f, m.length * .20f, FUR);
            }
            else
            {
                s.Ellipsoid(0, m.bodyY, 0, m.width * .5f, m.height * .5f, m.length * .46f, FUR);
                // Shoulder barrel + rump massing for quadrupeds.
                if (m.type == Q)
                {
                    s.Ellipsoid(0, m.bodyY + m.height * .08f, m.length * .24f, m.width * .52f, m.height * .40f, m.length * .22f, FUR);
                    s.Ellipsoid(0, m.bodyY - m.height * .04f, -m.length * .26f, m.width * .46f, m.height * .36f, m.length * .22f, FUR);
                }
            }
            // Underfur belly band.
            s.Ellipsoid(0, m.bodyY - m.height * .30f, 0, m.width * .46f, m.height * .20f, m.length * .40f, UNDER);

            // ---- Legs ----
            if (m.legs == 4)
            {
                float zRear = -m.length * .30f, zFront = m.length * .30f;
                Leg(s, "leg0", -m.width * .34f, m.bodyY - m.height * .42f, zRear, m.legLen, kind);
                Leg(s, "leg1", m.width * .34f, m.bodyY - m.height * .42f, zRear, m.legLen, kind);
                Leg(s, "leg2", -m.width * .34f, m.bodyY - m.height * .38f, zFront, m.legLen, kind);
                Leg(s, "leg3", m.width * .34f, m.bodyY - m.height * .38f, zFront, m.legLen, kind);
            }
            else if (m.legs == 2)
            {
                float z = m.length * .28f;
                Leg(s, "leg0", -m.width * .30f, m.bodyY - m.height * .42f, z, m.legLen, kind);
                Leg(s, "leg1", m.width * .30f, m.bodyY - m.height * .42f, z, m.legLen, kind);
            }
            else if (arthropod)
            {
                for (int i = 0; i < m.legs; i++)
                {
                    float side = (i % 2 == 0 ? -1 : 1) * m.width * .50f;
                    float z = -m.length * .30f + (i / 2) * m.length * .20f;
                    ArthropodLeg(s, "leg" + i, side, m.bodyY, z, m.legLen, kind);
                }
            }
            else if (m.legs == 8) // glass spider
            {
                for (int i = 0; i < 8; i++)
                {
                    float side = (i % 2 == 0 ? -1 : 1) * m.width * .52f;
                    float z = m.length * (.30f - (i / 2) * .22f);
                    ArthropodLeg(s, "leg" + i, side, m.bodyY, z, m.legLen, kind);
                }
            }

            // ---- Neck + Head ----
            float neckTop = m.bodyY + m.height * .20f;
            if (kind == 0 || kind == 17 || kind == 14) // deer, moose, camel: rising neck
            {
                s.Part("neck", 0, neckTop, m.headZ * .55f);
                s.Ellipsoid(0, neckTop + m.height * .18f, m.headZ * .85f, m.width * .20f, m.height * .30f, m.width * .20f, FUR);
            }
            s.Part("head", 0, m.headY, m.headZ);
            Head(s, m, kind, glow, 0, m.headY, m.headZ);

            // ---- Tail ----
            float tailOX = 0, tailOY = m.bodyY - m.height * .10f, tailOZ = -m.length * .50f;
            s.Part("tail", tailOX, tailOY, tailOZ);
            Tail(s, m, kind, glow, tailOX, tailOY, tailOZ);

            // ---- Wings (flyers) ----
            if (flyer)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float wx = side * m.width * .42f, wy = m.bodyY + m.height * .10f, wz = 0;
                    s.Part("wing" + (side > 0 ? "R" : "L"), wx, wy, wz);
                    Wing(s, side, m, kind, glow, wx, wy, wz);
                }
            }

            Signature(s, m, kind, glow);
        }

        /// <summary>Two-bone quadruped/biped leg: thick upper from hip to knee, thin lower to the ground, hoof/paw pad.</summary>
        static void Leg(Sculptor s, string name, float x, float hipY, float z, float len, int kind)
        {
            if (len <= .02f) return;
            s.Part(name, x, hipY, z);
            bool hooved = kind == 0 || kind == 6 || kind == 7 || kind == 14 || kind == 17;
            bool padded = kind == 3 || kind == 4 || kind == 5 || kind == 8 || kind == 9 || kind == 30;
            float upper = hooved ? len * .16f : padded ? len * .22f : len * .18f;
            float lower = upper * .70f;
            float knee = hipY - len * .46f;
            s.Ellipsoid(x, (hipY + knee) * .5f, z, upper, len * .23f, upper, FUR);          // upper limb
            s.Ellipsoid(x, (knee + hipY - len) * .5f, z, lower, len * .18f, lower, FUR);    // lower limb
            if (hooved)
                s.Ellipsoid(x, .05f, z, lower * 1.5f, .07f, lower * 1.7f, HOOF);
            else if (padded)
                s.Ellipsoid(x, .04f, z, lower * 1.6f, .07f, lower * 1.9f, HOOF);
            else
                s.Ellipsoid(x, .04f, z, lower * 1.3f, .06f, lower * 1.5f, HOOF);
        }

        /// <summary>Arthropod digit leg: a thin segmented limb with a claw tip, pivoted at the body.</summary>
        static void ArthropodLeg(Sculptor s, string name, float x, float y, float z, float len, int kind)
        {
            s.Part(name, x, y, z);
            float side = Mathf.Sign(x) < 0 ? -1 : 1;
            float seg = len * .34f;
            float baseR = len * .10f;
            s.Ellipsoid(x, y - seg * .2f, z, baseR, seg * .5f, baseR, KERATIN);
            s.Ellipsoid(x + side * seg * .30f, y - seg * .55f, z - seg * .10f, baseR * .85f, seg * .45f, baseR * .85f, KERATIN);
            s.Ellipsoid(x + side * seg * .55f, y - len * .80f, z - seg * .18f, baseR * .70f, seg * .40f, baseR * .70f, KERATIN);
            s.Ellipsoid(x + side * seg * .70f, y - len * .95f, z - seg * .24f, baseR * .50f, .03f, baseR * .50f, HOOF);
        }

        static void Head(Sculptor s, Metrics m, int kind, bool glow, float ox, float oy, float oz)
        {
            float hw = m.headW * .5f, hh = m.headH * .5f;
            // Author in absolute world coords: every offset is relative to the head pivot.
            void E(float x, float y, float z, float rx, float ry, float rz, int mat) => s.Ellipsoid(ox + x, oy + y, oz + z, rx, ry, rz, mat);
            E(0, 0, 0, hw, hh, m.headZ * .30f, FUR);
            // Snout wedge.
            bool predator = kind == 3 || kind == 5 || kind == 8 || kind == 20 || kind == 22 || kind == 27 || kind == 30 || kind == 31;
            bool rodent = kind == 1 || kind == 9 || kind == 18;
            float snout = rodent ? m.headW * .26f : predator ? m.headW * .40f : m.headW * .30f;
            E(0, -hh * .18f, m.headZ * .34f, snout, snout * .70f, snout, FUR);
            E(0, -hh * .30f, m.headZ * .46f, snout * .80f, snout * .40f, snout * .60f, KERATIN); // nose/mouth
            // Eyes.
            float sep = m.headW * .26f;
            s.Eyes(hw, oy, oz + m.headZ * .18f, sep, m.headH * .16f, SCLERA, IRIS, HOOF);
        }

        static void Tail(Sculptor s, Metrics m, int kind, bool glow, float ox, float oy, float oz)
        {
            void E(float x, float y, float z, float rx, float ry, float rz, int mat) => s.Ellipsoid(ox + x, oy + y, oz + z, rx, ry, rz, mat);
            if (kind == 15) // scorpion: segmented curved tail + stinger
            {
                for (int seg = 0; seg < 5; seg++)
                    E(0, .08f + seg * .10f, -.10f - seg * .06f + (seg > 2 ? (seg - 2) * .12f : 0), .12f, .12f, .14f, KERATIN);
                E(0, .60f, .02f, .09f, .12f, .18f, HOOF);
            }
            else if (kind == 19 || kind == 26) // voidray / gamma ray: luminous whip
            {
                E(0, 0, -.55f, .08f, .06f, 1.10f, glow ? GLOW : FUR);
            }
            else if (kind == 9) // beaver: flat rudder
            {
                E(0, -.17f, -.36f, .40f, .08f, .65f, HOOF);
            }
            else if (kind == 11 || kind == 12) // birds: feathered fan
            {
                for (int i = 0; i < 5; i++)
                    E((i - 2) * .085f, -.035f, -.18f, .085f, .05f, .38f, FUR);
            }
            else
            {
                E(0, -.03f, -.12f, .14f, .14f, .24f, FUR);
            }
        }

        static void Wing(Sculptor s, float side, Metrics m, int kind, bool glow, float ox, float oy, float oz)
        {
            bool ray = kind == 19 || kind == 26;
            float span = ray ? 1.05f : kind == 33 ? 1.25f : .64f;
            float wy = kind == 13 ? -.16f : 0;
            void E(float x, float y, float z, float rx, float ry, float rz, int mat) => s.Ellipsoid(ox + x, oy + y, oz + z, rx, ry, rz, mat);
            // Shoulder + tapered blade.
            E(side * span * .25f, wy, -.02f, span * .30f, .10f, .42f, FUR);
            E(side * span * .55f, wy - .04f, -.04f, span * .22f, .07f, ray ? .55f : .28f, ray || kind == 25 ? GLOW : FUR);
            E(side * span * .80f, wy - .06f, -.05f, span * .12f, .05f, ray ? .30f : .18f, ray || kind == 25 ? GLOW : FUR);
        }

        /// <summary>Per-species signature silhouette: antlers, horns, ears, manes, humps, shells.</summary>
        static void Signature(Sculptor s, Metrics m, int kind, bool glow)
        {
            // Every signature detail is authored in absolute world coords relative to the
            // pivot of the part it belongs to. P() selects (and records) that pivot.
            float ox = 0, oy = 0, oz = 0;
            void E(float x, float y, float z, float rx, float ry, float rz, int mat) => s.Ellipsoid(ox + x, oy + y, oz + z, rx, ry, rz, mat);
            void Sh(float x, float y, float z, float rx, float ry, float rz, float th, int mat) => s.Shell(ox + x, oy + y, oz + z, rx, ry, rz, th, mat);
            void P(string name, float px, float py, float pz) { s.Part(name, px, py, pz); ox = px; oy = py; oz = pz; }

            switch (kind)
            {
                case 0: // deer: branched rack on the head
                {
                    P("rack", 0, m.headY + m.headH * .55f, m.headZ * .55f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        E(side * .07f, .02f, 0, .05f, .18f, .05f, BONE);
                        E(side * .19f, .11f, -.04f, .21f, .05f, .05f, BONE);
                        for (int t = 0; t < 3; t++)
                            E(side * (.12f + t * .08f), .21f + t * .02f, -.05f - t * .05f, .035f, .19f - t * .03f, .035f, BONE);
                    }
                    break;
                }
                case 1: // hare: tall ears + tail tuft
                {
                    P("earL", -.06f, m.headY + m.headH * .55f, m.headZ * .30f);
                    E(0, .14f, -.03f, .075f, .28f, .055f, FUR);
                    E(0, .30f, -.045f, .06f, .10f, .045f, HOOF);
                    P("earR", .06f, m.headY + m.headH * .55f, m.headZ * .30f);
                    E(0, .14f, -.03f, .075f, .28f, .055f, FUR);
                    E(0, .30f, -.045f, .06f, .10f, .045f, HOOF);
                    P("tail", 0, m.bodyY - m.height * .10f, -m.length * .50f);
                    E(0, 0, -.10f, .13f, .13f, .09f, UNDER);
                    break;
                }
                case 2: // boar: shoulder mane
                {
                    P("mane", 0, m.bodyY + m.height * .55f, m.length * .10f);
                    for (int i = 0; i < 8; i++)
                        E(0, .10f, -.40f + i * .12f, .07f, .10f + (i % 2) * .04f, .08f, UNDER);
                    break;
                }
                case 3: // wolf: ear tufts (brush is the default tail; ears handled below)
                    break;
                case 4: // bear: round ears + heavy mantle
                {
                    P("mantle", 0, m.bodyY + m.height * .42f, m.length * .10f);
                    E(0, 0, 0, m.width * .58f, m.height * .22f, m.length * .28f, FUR);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        P("ear" + (side > 0 ? "R" : "L"), side * m.width * .34f, m.bodyY + m.height * .66f, m.length * .10f);
                        E(0, 0, 0, .12f, .14f, .10f, FUR);
                    }
                    break;
                }
                case 5: // fox: ear tufts (handled below)
                    break;
                case 6: // bison: sweeping horns + shoulder hump
                {
                    P("hump", 0, m.bodyY + m.height * .55f, m.length * .20f);
                    E(0, 0, 0, m.width * .40f, m.height * .30f, m.length * .20f, FUR);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        P("horn" + (side > 0 ? "R" : "L"), side * m.width * .34f, m.bodyY + m.height * .68f, m.length * .34f);
                        E(side * .04f, .02f, 0, .10f, .16f, .10f, BONE);
                        E(side * .13f, .13f, 0, .12f, .12f, .09f, BONE);
                        E(side * .23f, .20f, 0, .10f, .08f, .08f, BONE);
                    }
                    break;
                }
                case 7: // goat: sweeping horns + beard
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        P("horn" + (side > 0 ? "R" : "L"), side * m.width * .34f, m.bodyY + m.height * .68f, m.length * .34f);
                        E(0, .06f, -.02f, .09f, .18f, .09f, BONE);
                        E(0, .20f, -.10f, .08f, .16f, .08f, BONE);
                        E(0, .32f, -.20f, .07f, .14f, .07f, BONE);
                    }
                    P("beard", 0, m.bodyY - m.height * .42f, m.length * .42f);
                    E(0, -.08f, 0, .10f, .24f, .10f, UNDER);
                    break;
                }
                case 8: // lynx: tufted ears (handled below)
                    break;
                case 10: // toad: eye domes + wide jaw (on the head)
                {
                    P("head", 0, m.headY, m.headZ);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        E(side * m.headW * .40f, m.headH * .45f, m.headZ * .18f, m.headW * .30f, m.headH * .30f, m.headW * .30f, FUR);
                        E(side * m.headW * .40f, m.headH * .55f, m.headZ * .30f, m.headW * .20f, m.headH * .15f, m.headW * .10f, KERATIN);
                    }
                    break;
                }
                case 11: // eagle: beak on the head
                {
                    P("head", 0, m.headY, m.headZ);
                    E(0, -m.headH * .30f, m.headZ * .55f, m.headW * .18f, m.headH * .20f, m.headW * .18f, KERATIN);
                    break;
                }
                case 12: // owl: ear tufts + beak
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        P("ear" + (side > 0 ? "R" : "L"), side * m.headW * .35f, m.headY + m.headH * .45f, m.headZ);
                        E(side * .14f, .21f, -.02f, .08f, .14f, .08f, FUR);
                    }
                    P("head", 0, m.headY, m.headZ);
                    E(0, -m.headH * .25f, m.headZ * .42f, m.headW * .16f, m.headH * .18f, m.headW * .16f, KERATIN);
                    break;
                }
                case 13: // penguin: white belly bib + beak
                {
                    P("bib", 0, m.bodyY - m.height * .10f, m.headZ * .30f);
                    E(0, 0, 0, m.width * .45f, m.height * .40f, m.length * .20f, UNDER);
                    P("head", 0, m.headY, m.headZ);
                    E(0, -m.headH * .20f, m.headZ * .45f, m.headW * .20f, m.headH * .22f, m.headW * .20f, KERATIN);
                    break;
                }
                case 14: // camel: twin humps
                {
                    P("hump1", 0, m.bodyY + m.height * .62f, m.length * .10f);
                    E(0, 0, 0, m.width * .34f, m.height * .28f, m.length * .18f, FUR);
                    P("hump2", 0, m.bodyY + m.height * .62f, -m.length * .12f);
                    E(0, 0, 0, m.width * .32f, m.height * .26f, m.length * .16f, FUR);
                    break;
                }
                case 15: // scorpion: pincer arms
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        P("claw" + (side > 0 ? "R" : "L"), side * m.width * .50f, m.bodyY, m.length * .30f);
                        E(side * .14f, 0, .30f, .14f, .11f, .36f, KERATIN);
                        E(side * .22f, 0, .55f, .22f, .14f, .26f, KERATIN);
                    }
                    break;
                }
                case 16: // turtle: domed shell rim
                {
                    P("shell", 0, m.bodyY + m.height * .40f, 0);
                    Sh(0, 0, 0, m.width * .50f, m.height * .40f, m.length * .42f, .10f, KERATIN);
                    E(0, m.height * .42f, 0, m.width * .42f, .06f, m.length * .36f, KERATIN);
                    break;
                }
                case 17: // moose: palmate antlers + hump
                {
                    P("hump", 0, m.bodyY + m.height * .55f, m.length * .22f);
                    E(0, 0, 0, m.width * .38f, m.height * .26f, m.length * .22f, FUR);
                    P("rack", 0, m.headY + m.headH * .50f, m.headZ * .50f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        E(side * .12f, .10f, .10f, .30f, .08f, .16f, BONE);
                        for (int t = 0; t < 4; t++)
                            E(side * (.24f + t * .10f), .18f, -.10f + t * .08f, .06f, .18f, .06f, BONE);
                    }
                    break;
                }
                case 18: // capybara: rounded ears
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        P("ear" + (side > 0 ? "R" : "L"), side * m.headW * .40f, m.headY + m.headH * .55f, m.headZ * .30f);
                        E(0, 0, 0, .07f, .09f, .08f, FUR);
                    }
                    break;
                }
                case 19: // voidray: dorsal crest
                {
                    P("crest", 0, m.bodyY + m.height * .50f, 0);
                    E(0, 0, 0, m.width * .26f, .08f, m.length * .44f, GLOW);
                    break;
                }
                case 21: // leech: glowing rings
                {
                    P("rings", 0, m.bodyY, 0);
                    for (int ring = 0; ring < 5; ring++)
                        E(0, 0, -m.length * .38f + ring * m.length * .19f, m.width * .53f, m.height * .53f, .06f, ring % 2 == 0 ? GLOW : FUR);
                    break;
                }
                case 22: // rad scorpion: pincers
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        P("claw" + (side > 0 ? "R" : "L"), side * m.width * .50f, m.bodyY, m.length * .40f);
                        E(side * .16f, 0, .20f, .16f, .14f, .24f, KERATIN);
                    }
                    break;
                }
                case 23: // two-headed boar: second head
                {
                    P("head2", -m.width * .52f, m.bodyY + m.height * .46f, m.length * .40f);
                    E(0, 0, 0, m.width * .26f, m.height * .22f, m.length * .10f, FUR);
                    E(0, -m.height * .10f, m.length * .18f, m.width * .20f, m.height * .18f, m.length * .16f, FUR);
                    s.Eyes(m.width * .24f, oy, oz + m.length * .08f, m.width * .10f, m.height * .10f, SCLERA, IRIS, HOOF);
                    break;
                }
                case 24: // slag crab: claws
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        P("claw" + (side > 0 ? "R" : "L"), side * m.width * .50f, m.bodyY, m.length * .45f);
                        E(side * .16f, 0, .20f, .18f, .16f, .24f, KERATIN);
                    }
                    break;
                }
                case 27: // bone stalker: rib cage
                {
                    P("ribs", 0, m.bodyY, 0);
                    for (int rib = 0; rib < 6; rib++)
                        for (int side = -1; side <= 1; side += 2)
                            E(side * m.width * .52f, m.height * .10f, -m.length * .26f + rib * m.length * .10f, .05f, m.height * .38f, .06f, BONE);
                    break;
                }
                case 28: // vitrified golem: armored shoulders
                {
                    P("pauldron", 0, m.bodyY + m.height * .34f, m.length * .12f);
                    E(0, 0, 0, m.width * .62f, m.height * .24f, m.length * .28f, KERATIN);
                    break;
                }
                case 30: // frost wolf: mane
                {
                    P("mane", 0, m.bodyY + m.height * .34f, m.length * .30f);
                    for (int i = 0; i < 4; i++)
                        E((i % 2 == 0 ? -1 : 1) * m.width * .30f, .05f, -.05f + i * .10f, .10f, .20f, .12f, UNDER);
                    break;
                }
                case 31: // glass spider: faceted abdomen
                {
                    P("abdomen", 0, m.bodyY - m.height * .10f, -m.length * .20f);
                    Sh(0, 0, 0, m.width * .40f, m.height * .40f, m.length * .28f, .06f, KERATIN);
                    break;
                }
                case 32: // beetle: domed shell + caps
                {
                    P("shell", 0, m.bodyY + m.height * .30f, 0);
                    Sh(0, 0, 0, m.width * .61f, m.height * .31f, m.length * .46f, .08f, FUR);
                    for (int i = 0; i < 3; i++)
                        E(0, m.height * .34f, -m.length * .24f + i * m.length * .24f, m.width * .30f, .06f, m.width * .30f, UNDER);
                    break;
                }
                case 33: // sky whale: flukes + glowing belly
                {
                    P("flukes", 0, m.bodyY, -m.length * .50f);
                    E(-.36f, 0, -.06f, .52f, .06f, .22f, FUR);
                    E(.36f, 0, -.06f, .52f, .06f, .22f, FUR);
                    P("belly", 0, m.bodyY - m.height * .42f, 0);
                    for (int i = 0; i < 5; i++)
                        E(0, 0, -m.length * .34f + i * m.length * .17f, m.width * .37f, .05f, .09f, GLOW);
                    break;
                }
            }

            // Ears for the remaining quadruped predators/canines.
            if (kind == 3 || kind == 5 || kind == 8)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    P("ear" + (side > 0 ? "R" : "L"), side * m.width * .30f, m.bodyY + m.height * .62f, m.length * .12f);
                    float ear = kind == 5 ? .17f : kind == 8 ? .15f : .12f;
                    float eh = kind == 5 ? .26f : kind == 8 ? .30f : .22f;
                    E(0, 0, 0, ear, eh, ear * .7f, FUR);
                    E(0, eh * .18f, ear * .16f, ear * .62f, eh * .5f, ear * .3f, UNDER);
                }
            }
        }
    }
}
