using System;
using UnityEngine;

namespace LestingCraft.Gameplay
{
    /// <summary>
    /// Fine-voxel anatomical sculptures for all 34 wildlife, Radia-abyss and cosmic species.
    /// Built on <see cref="Sculptor"/> and rendered by <see cref="LestingCraft.Presentation.BossSculpture"/>
    /// with articulated transforms ("body", "head", "leg0..N", "tail", "wingL/R", "earL/R").
    /// </summary>
    public static class AnimalSculpts
    {
        public const float Unit = .05f;

        const int FUR = 0, UNDER = 1, DARK = 2, SCLERA = 3, IRIS = 4, GLOW = 5, HOOF = 6, HORN = 7, ACCENT = 8;

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

        public static readonly Color[] CoatTints =
        {
            new Color(.60f, .40f, .24f), new Color(.62f, .53f, .42f), new Color(.36f, .25f, .19f),
            new Color(.46f, .46f, .49f), new Color(.36f, .23f, .14f), new Color(.80f, .40f, .14f),
            new Color(.30f, .22f, .16f), new Color(.68f, .64f, .58f), new Color(.72f, .58f, .40f),
            new Color(.40f, .28f, .18f), new Color(.38f, .48f, .24f), new Color(.36f, .25f, .16f),
            new Color(.92f, .93f, .96f), new Color(.16f, .19f, .24f), new Color(.76f, .63f, .44f),
            new Color(.56f, .45f, .24f), new Color(.32f, .42f, .26f), new Color(.34f, .25f, .18f),
            new Color(.52f, .38f, .24f), new Color(.26f, .20f, .42f),
            new Color(.24f, .21f, .20f), new Color(.26f, .56f, .38f), new Color(.36f, .20f, .18f),
            new Color(.46f, .28f, .22f), new Color(.36f, .32f, .30f), new Color(.20f, .19f, .22f),
            new Color(.22f, .42f, .48f), new Color(.68f, .64f, .54f), new Color(.30f, .38f, .34f),
            new Color(.24f, .20f, .34f), new Color(.78f, .86f, .95f), new Color(.66f, .80f, .88f),
            new Color(.48f, .34f, .22f), new Color(.38f, .48f, .62f)
        };

        static float UnitFor(int kind)
        {
            switch (kind)
            {
                case 1: case 5: case 8: case 9: case 10: case 11: case 12: case 13:
                case 15: case 16: case 18: case 21: case 25: case 31: case 32:
                    return .038f;
                case 4: case 6: case 14: case 17: case 27: case 28: case 29: case 33:
                    return .064f;
                default:
                    return .048f;
            }
        }

        public static SculptData For(int kind)
        {
            if (kind < 0 || kind >= SpeciesNames.Length) kind = 0;
            float u = UnitFor(kind);
            var s = new Sculptor(u, (int)((uint)(kind + 1) * 2654435761u & 0x7fffffff));
            BuildPalette(s, kind);
            BuildSpecies(s, kind);
            return s.Finish("animal_" + kind);
        }

        static void BuildPalette(Sculptor s, int kind)
        {
            Color c = CoatTints[kind];
            // Strictly zero emission on all natural terrestrial/avian wildlife so vertex alpha stays 0.
            // Only cosmic rays/whales (19, 33) and Radia Abyss mutants (20..29) carry emissive accents.
            bool emissiveAllowed = (kind >= 20 && kind <= 29) || kind == 19 || kind == 33;

            Color under = kind == 5 ? new Color(.95f, .93f, .89f) :
                          kind == 13 ? new Color(.95f, .96f, .98f) :
                          kind == 0 || kind == 1 ? new Color(.90f, .86f, .80f) :
                          kind == 10 ? new Color(.68f, .72f, .46f) :
                          kind == 16 ? new Color(.72f, .66f, .46f) :
                          kind == 33 ? new Color(.86f, .89f, .94f) :
                          Color.Lerp(c, new Color(.92f, .88f, .82f), .45f);

            Color iris = kind == 3 || kind == 8 || kind == 11 || kind == 12 ? new Color(.88f, .64f, .16f) :
                         kind == 10 ? new Color(.84f, .68f, .18f) :
                         kind == 30 || kind == 31 ? new Color(.36f, .78f, .96f) :
                         new Color(.14f, .12f, .10f);

            Color glowCol = kind >= 20 && kind <= 29 ? new Color(.22f, .96f, .66f) :
                            kind == 19 ? new Color(.48f, .78f, 1.0f) :
                            new Color(.56f, .86f, 1.0f);

            Color hornCol = kind == 9 ? new Color(.88f, .52f, .18f) :
                            kind == 11 ? new Color(.92f, .72f, .20f) :
                            kind == 13 ? new Color(.94f, .54f, .16f) :
                            kind == 31 ? new Color(.82f, .94f, 1.0f) :
                            new Color(.82f, .76f, .64f);

            Color accent = kind == 1 ? new Color(.88f, .62f, .62f) :
                           kind == 2 ? new Color(.68f, .46f, .44f) :
                           kind == 11 ? new Color(.95f, .95f, .93f) :
                           kind == 13 ? new Color(.96f, .76f, .24f) :
                           kind == 32 ? new Color(.84f, .22f, .18f) :
                           Color.Lerp(c, Color.white, .25f);

            // furLayer in Assets/Art/PBR already carries the authored species coat color (fox orange, bear brown, camel tan, etc.).
            // Keep FUR near neutral white so vertex color never double-darkens the PBR coat texture!
            Color furTint = Color.Lerp(Color.white, c, .15f);
            Color darkTint = kind == 5 ? new Color(.24f, .20f, .18f) : new Color(.64f, .60f, .57f);
            string furLayer = SpeciesFurNames[kind];
            s.Pal("fur", furLayer, furTint.r, furTint.g, furTint.b, 0f);                                 // 0 FUR
            s.Pal("under", "animal-underfur", under.r, under.g, under.b, 0f);                            // 1 UNDER
            s.Pal("dark", furLayer, darkTint.r, darkTint.g, darkTint.b, 0f);                             // 2 DARK
            s.Pal("sclera", "animal-underfur", .96f, .96f, .94f, 0f);                                    // 3 SCLERA
            s.Pal("iris", "obsidian", iris.r, iris.g, iris.b, 0f);                                       // 4 IRIS
            s.Pal("glow", emissiveAllowed ? "cherenkov-crystal" : "animal-underfur",
                glowCol.r, glowCol.g, glowCol.b, emissiveAllowed ? 2.1f : 0f);                           // 5 GLOW
            s.Pal("hoof", "obsidian", .15f, .13f, .12f, 0f);                                             // 6 HOOF
            s.Pal("horn", "polished-stone", hornCol.r, hornCol.g, hornCol.b, 0f);                        // 7 HORN
            s.Pal("accent", "animal-underfur", accent.r, accent.g, accent.b, 0f);                        // 8 ACCENT
        }

        static void Eyes(Sculptor s, float hy, float hz, float sep, float r, bool glowing = false)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = side * sep;
                if (glowing)
                {
                    s.Ellipsoid(ex, hy, hz, r * 1.15f, r * .95f, r * .85f, DARK);
                    s.Ellipsoid(ex + side * r * .22f, hy, hz + r * .35f, r * .85f, r * .75f, r * .65f, GLOW);
                }
                else
                {
                    s.Ellipsoid(ex, hy, hz, r * 1.05f, r * .95f, r * .80f, SCLERA);
                    s.Ellipsoid(ex + side * r * .30f, hy, hz + r * .34f, r * .82f, r * .82f, r * .68f, IRIS);
                    s.Ellipsoid(ex + side * r * .46f, hy, hz + r * .52f, r * .52f, r * .52f, r * .48f, HOOF);
                }
            }
        }

        static void QuadLegs(Sculptor s, float halfX, float frontZ, float backZ, float hipY, float upperR, float lowerR, int footMat, bool paw = false)
        {
            // Order: leg0 (BL), leg1 (BR), leg2 (FL), leg3 (FR)
            for (int i = 0; i < 4; i++)
            {
                float lx = (i % 2 == 0 ? -1f : 1f) * halfX;
                float lz = i < 2 ? backZ : frontZ;
                s.Part("leg" + i, lx, hipY, lz);
                float kneeY = hipY * .50f;
                float kneeZ = lz + (i < 2 ? -.04f : .02f);
                s.Tube(lx, hipY, lz, lx, kneeY, kneeZ, upperR, lowerR * 1.12f, FUR);
                s.Tube(lx, kneeY, kneeZ, lx, lowerR * .9f, lz, lowerR * 1.06f, lowerR * .88f, paw && footMat != FUR ? footMat : FUR);
                if (paw)
                    s.Ellipsoid(lx, lowerR * .65f, lz + lowerR * .45f, lowerR * 1.18f, lowerR * .68f, lowerR * 1.45f, footMat);
                else
                    s.Box(lx, lowerR * .55f, lz + lowerR * .15f, lowerR * .95f, lowerR * .55f, lowerR * 1.05f, footMat);
            }
        }

        static void BuildSpecies(Sculptor s, int kind)
        {
            switch (kind)
            {
                case 0: BuildDeer(s); break;
                case 1: BuildHare(s); break;
                case 2: BuildBoar(s); break;
                case 3: BuildWolf(s, false); break;
                case 4: BuildBear(s); break;
                case 5: BuildFox(s); break;
                case 6: BuildBison(s); break;
                case 7: BuildGoat(s); break;
                case 8: BuildLynx(s); break;
                case 9: BuildBeaver(s); break;
                case 10: BuildToad(s); break;
                case 11: BuildEagle(s); break;
                case 12: BuildOwl(s); break;
                case 13: BuildPenguin(s); break;
                case 14: BuildCamel(s); break;
                case 15: BuildScorpion(s, false); break;
                case 16: BuildTurtle(s); break;
                case 17: BuildMoose(s); break;
                case 18: BuildCapybara(s); break;
                case 19: BuildRay(s, false); break;
                case 20: BuildAshHound(s); break;
                case 21: BuildCherenkovLeech(s); break;
                case 22: BuildScorpion(s, true); break;
                case 23: BuildMutantBoar(s); break;
                case 24: BuildSlagCrab(s); break;
                case 25: BuildFalloutCrow(s); break;
                case 26: BuildRay(s, true); break;
                case 27: BuildBoneStalker(s); break;
                case 28: BuildVitrifiedGolem(s); break;
                case 29: BuildSpiralWraith(s); break;
                case 30: BuildWolf(s, true); break;
                case 31: BuildGlassSpider(s); break;
                case 32: BuildMushroomBeetle(s); break;
                case 33: BuildSkyWhale(s); break;
            }
        }

        static void BuildDeer(Sculptor s)
        {
            s.Part("body", 0, .92f, 0);
            s.Ellipsoid(0, .92f, .10f, .24f, .26f, .36f, FUR);
            s.Ellipsoid(0, .90f, -.22f, .22f, .24f, .32f, FUR);
            s.Ellipsoid(0, .74f, .02f, .17f, .12f, .44f, UNDER);
            s.Tube(0, 1.04f, .30f, 0, 1.38f, .44f, .14f, .105f, FUR);
            s.Tube(0, 1.02f, .35f, 0, 1.34f, .48f, .09f, .07f, UNDER);

            s.Part("head", 0, 1.40f, .46f);
            s.Ellipsoid(0, 1.44f, .50f, .13f, .13f, .17f, FUR);
            s.Tube(0, 1.41f, .58f, 0, 1.38f, .76f, .095f, .065f, FUR);
            s.Ellipsoid(0, 1.39f, .78f, .05f, .045f, .045f, HOOF);
            Eyes(s, 1.47f, .56f, .11f, .042f);
            // Branching 6-point stag antlers
            for (int side = -1; side <= 1; side += 2)
            {
                s.Tube(side * .07f, 1.54f, .48f, side * .20f, 1.78f, .40f, .035f, .028f, HORN);
                s.Tube(side * .20f, 1.78f, .40f, side * .26f, 1.98f, .48f, .028f, .022f, HORN);
                s.Tube(side * .13f, 1.66f, .44f, side * .16f, 1.76f, .58f, .026f, .020f, HORN);
                s.Tube(side * .21f, 1.84f, .42f, side * .12f, 1.95f, .36f, .024f, .020f, HORN);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .11f, 1.52f, .44f);
                s.Tube(side * .11f, 1.52f, .44f, side * .25f, 1.60f, .38f, .045f, .032f, FUR);
            }
            s.Part("tail", 0, .98f, -.52f);
            s.Tube(0, .98f, -.52f, 0, 1.06f, -.66f, .065f, .045f, FUR);
            s.Ellipsoid(0, .96f, -.62f, .055f, .055f, .08f, UNDER);

            QuadLegs(s, .15f, .28f, -.32f, .76f, .085f, .055f, HOOF, false);
        }

        static void BuildHare(Sculptor s)
        {
            s.Part("body", 0, .24f, 0);
            s.Ellipsoid(0, .24f, -.04f, .15f, .15f, .19f, FUR);
            s.Ellipsoid(0, .25f, .10f, .125f, .13f, .15f, FUR);
            s.Ellipsoid(0, .15f, .03f, .10f, .07f, .18f, UNDER);

            s.Part("head", 0, .35f, .19f);
            s.Ellipsoid(0, .37f, .22f, .105f, .10f, .12f, FUR);
            s.Ellipsoid(0, .34f, .31f, .075f, .065f, .075f, UNDER);
            s.Ellipsoid(0, .36f, .37f, .035f, .032f, .032f, ACCENT);
            Eyes(s, .39f, .27f, .085f, .036f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .055f, .45f, .18f);
                s.Tube(side * .055f, .45f, .18f, side * .085f, .69f, .13f, .042f, .032f, FUR);
                s.Tube(side * .055f, .47f, .195f, side * .082f, .65f, .15f, .026f, .020f, ACCENT);
            }

            s.Part("tail", 0, .25f, -.22f);
            s.Ellipsoid(0, .26f, -.26f, .075f, .075f, .075f, UNDER);

            // Hind haunches (leg0, leg1) + nimble forepaws (leg2, leg3)
            for (int i = 0; i < 4; i++)
            {
                float lx = (i % 2 == 0 ? -1f : 1f) * (i < 2 ? .12f : .085f);
                float lz = i < 2 ? -.10f : .14f;
                float hipY = i < 2 ? .22f : .19f;
                s.Part("leg" + i, lx, hipY, lz);
                if (i < 2)
                {
                    s.Ellipsoid(lx, .16f, lz - .02f, .065f, .085f, .085f, FUR);
                    s.Tube(lx, .11f, lz - .02f, lx, .04f, lz + .08f, .045f, .040f, UNDER);
                }
                else
                {
                    s.Tube(lx, hipY, lz, lx, .04f, lz + .02f, .044f, .038f, FUR);
                    s.Ellipsoid(lx, .035f, lz + .04f, .044f, .035f, .055f, UNDER);
                }
            }
        }

        static void BuildBoar(Sculptor s)
        {
            s.Part("body", 0, .54f, 0);
            s.Ellipsoid(0, .58f, .12f, .30f, .30f, .36f, FUR);
            s.Ellipsoid(0, .52f, -.22f, .25f, .24f, .30f, FUR);
            // Bristly dorsal mane ridge
            s.Tube(0, .85f, .24f, 0, .74f, -.34f, .065f, .045f, DARK);

            s.Part("head", 0, .58f, .42f);
            s.Ellipsoid(0, .58f, .50f, .22f, .21f, .22f, FUR);
            s.Tube(0, .54f, .60f, 0, .48f, .82f, .15f, .105f, FUR);
            s.Ellipsoid(0, .48f, .84f, .10f, .085f, .045f, ACCENT);
            Eyes(s, .64f, .58f, .16f, .042f);
            for (int side = -1; side <= 1; side += 2)
                s.Tube(side * .11f, .47f, .74f, side * .17f, .60f, .80f, .036f, .024f, HORN);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .16f, .73f, .44f);
                s.Tube(side * .16f, .73f, .44f, side * .24f, .82f, .38f, .05f, .032f, DARK);
            }
            s.Part("tail", 0, .58f, -.50f);
            s.Tube(0, .58f, -.50f, 0, .46f, -.64f, .045f, .035f, DARK);

            QuadLegs(s, .18f, .24f, -.28f, .40f, .085f, .062f, HOOF, false);
        }

        static void BuildWolf(Sculptor s, bool frost)
        {
            s.Part("body", 0, .68f, 0);
            s.Ellipsoid(0, .70f, .14f, .23f, .24f, .32f, FUR);
            s.Ellipsoid(0, .66f, -.22f, .19f, .20f, .30f, FUR);
            s.Ellipsoid(0, .56f, .16f, .16f, .13f, .26f, UNDER);
            // Thick neck ruff
            s.Ellipsoid(0, .78f, .34f, .22f, .21f, .18f, frost ? UNDER : FUR);
            if (frost)
            {
                for (int k = 0; k < 4; k++)
                    s.Tube(0, .88f - k * .04f, .30f - k * .15f, 0, 1.04f - k * .05f, .22f - k * .16f, .045f, .025f, HORN);
            }

            s.Part("head", 0, .86f, .44f);
            s.Ellipsoid(0, .88f, .50f, .17f, .16f, .18f, FUR);
            s.Tube(0, .83f, .60f, 0, .81f, .80f, .105f, .072f, FUR);
            s.Ellipsoid(0, .78f, .68f, .085f, .045f, .12f, UNDER);
            s.Ellipsoid(0, .83f, .82f, .048f, .042f, .042f, HOOF);
            Eyes(s, .91f, .59f, .115f, .042f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .11f, 1.00f, .46f);
                s.Tube(side * .11f, 1.00f, .46f, side * .14f, 1.16f, .44f, .052f, .028f, DARK);
            }

            s.Part("tail", 0, .68f, -.48f);
            s.Tube(0, .68f, -.48f, 0, .46f, -.84f, .085f, .095f, FUR);
            s.Ellipsoid(0, .42f, -.88f, .075f, .075f, .095f, frost ? UNDER : DARK);

            QuadLegs(s, .145f, .24f, -.28f, .56f, .082f, .056f, UNDER, true);
        }

        static void BuildBear(Sculptor s)
        {
            s.Part("body", 0, .92f, 0);
            s.Ellipsoid(0, .98f, .18f, .46f, .46f, .48f, FUR);
            s.Ellipsoid(0, .90f, -.28f, .42f, .40f, .46f, FUR);
            s.Ellipsoid(0, 1.24f, .22f, .30f, .20f, .28f, DARK); // shoulder muscle hump

            s.Part("head", 0, 1.08f, .62f);
            s.Ellipsoid(0, 1.08f, .70f, .28f, .26f, .26f, FUR);
            s.Tube(0, 1.00f, .86f, 0, .98f, 1.08f, .16f, .12f, UNDER);
            s.Ellipsoid(0, 1.02f, 1.11f, .07f, .06f, .055f, HOOF);
            Eyes(s, 1.14f, .84f, .16f, .048f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .20f, 1.28f, .64f);
                s.Ellipsoid(side * .22f, 1.33f, .64f, .085f, .085f, .06f, FUR);
            }
            s.Part("tail", 0, .92f, -.72f);
            s.Ellipsoid(0, .92f, -.76f, .10f, .10f, .10f, FUR);

            QuadLegs(s, .28f, .32f, -.36f, .68f, .16f, .125f, DARK, true);
        }

        static void BuildFox(Sculptor s)
        {
            s.Part("body", 0, .42f, 0);
            s.Ellipsoid(0, .43f, .08f, .15f, .16f, .24f, FUR);
            s.Ellipsoid(0, .41f, -.16f, .135f, .145f, .22f, FUR);
            s.Ellipsoid(0, .36f, .16f, .12f, .12f, .18f, UNDER); // white chest bib
            s.Ellipsoid(0, .31f, -.02f, .10f, .07f, .26f, UNDER);

            s.Part("head", 0, .54f, .28f);
            s.Ellipsoid(0, .56f, .33f, .135f, .125f, .14f, FUR);
            s.Ellipsoid(0, .51f, .34f, .145f, .075f, .11f, UNDER); // white cheek tufts
            s.Tube(0, .52f, .42f, 0, .50f, .57f, .075f, .042f, FUR);
            s.Ellipsoid(0, .51f, .59f, .035f, .032f, .032f, HOOF);
            Eyes(s, .58f, .41f, .088f, .036f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .09f, .66f, .30f);
                s.Tube(side * .09f, .66f, .30f, side * .13f, .80f, .28f, .048f, .024f, DARK);
            }

            s.Part("tail", 0, .43f, -.36f);
            s.Tube(0, .43f, -.36f, 0, .36f, -.66f, .085f, .115f, FUR);
            s.Tube(0, .36f, -.66f, 0, .34f, -.82f, .105f, .055f, UNDER); // iconic white tail tip!

            QuadLegs(s, .10f, .17f, -.20f, .34f, .058f, .042f, DARK, true);
        }

        static void BuildBison(Sculptor s)
        {
            s.Part("body", 0, 1.02f, 0);
            // Massive shaggy shoulder cape + hump
            s.Ellipsoid(0, 1.14f, .24f, .52f, .54f, .52f, DARK);
            s.Ellipsoid(0, 1.44f, .24f, .36f, .24f, .36f, DARK);
            s.Ellipsoid(0, .96f, -.34f, .38f, .38f, .46f, FUR);

            s.Part("head", 0, .96f, .68f);
            s.Ellipsoid(0, .98f, .78f, .30f, .32f, .28f, DARK);
            s.Ellipsoid(0, .82f, .92f, .22f, .18f, .20f, FUR);
            s.Tube(0, .68f, .84f, 0, .48f, .82f, .11f, .06f, DARK); // chin beard
            Eyes(s, 1.02f, .88f, .24f, .052f);
            for (int side = -1; side <= 1; side += 2)
                s.Tube(side * .24f, 1.10f, .76f, side * .38f, 1.28f, .82f, .065f, .035f, HOOF);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .26f, 1.04f, .70f);
                s.Ellipsoid(side * .31f, 1.02f, .68f, .07f, .055f, .055f, DARK);
            }
            s.Part("tail", 0, 1.02f, -.78f);
            s.Tube(0, 1.02f, -.78f, 0, .68f, -.88f, .055f, .065f, DARK);

            QuadLegs(s, .28f, .34f, -.40f, .72f, .15f, .10f, HOOF, false);
        }

        static void BuildGoat(Sculptor s)
        {
            s.Part("body", 0, .74f, 0);
            s.Ellipsoid(0, .76f, .08f, .21f, .23f, .28f, FUR);
            s.Ellipsoid(0, .74f, -.18f, .19f, .21f, .26f, FUR);
            s.Tube(0, .84f, .24f, 0, 1.08f, .36f, .13f, .10f, FUR);

            s.Part("head", 0, 1.10f, .38f);
            s.Ellipsoid(0, 1.12f, .44f, .125f, .135f, .16f, FUR);
            s.Tube(0, 1.07f, .52f, 0, 1.03f, .66f, .085f, .060f, FUR);
            s.Tube(0, .98f, .58f, 0, .86f, .60f, .045f, .025f, UNDER); // goat beard
            Eyes(s, 1.15f, .50f, .105f, .040f);
            // Sweeping backward-curved ibex horns
            for (int side = -1; side <= 1; side += 2)
            {
                s.Path(new[]
                {
                    side * .065f, 1.22f, .44f,
                    side * .12f, 1.42f, .36f,
                    side * .16f, 1.46f, .14f,
                    side * .15f, 1.32f, -.04f
                }, new[] { .048f, .042f, .034f, .024f }, HORN);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .11f, 1.16f, .40f);
                s.Tube(side * .11f, 1.16f, .40f, side * .22f, 1.18f, .36f, .04f, .028f, FUR);
            }
            s.Part("tail", 0, .82f, -.42f);
            s.Tube(0, .82f, -.42f, 0, .94f, -.50f, .05f, .035f, FUR);

            QuadLegs(s, .135f, .20f, -.24f, .60f, .075f, .052f, HOOF, false);
        }

        static void BuildLynx(Sculptor s)
        {
            s.Part("body", 0, .56f, 0);
            s.Ellipsoid(0, .57f, .10f, .18f, .19f, .26f, FUR);
            s.Ellipsoid(0, .56f, -.18f, .17f, .18f, .25f, FUR);
            s.Ellipsoid(0, .44f, -.02f, .12f, .08f, .30f, UNDER);

            s.Part("head", 0, .72f, .32f);
            s.Ellipsoid(0, .74f, .38f, .155f, .14f, .145f, FUR);
            // Lynx flared cheek ruffs
            for (int side = -1; side <= 1; side += 2)
                s.Ellipsoid(side * .15f, .68f, .38f, .065f, .075f, .075f, UNDER);
            s.Ellipsoid(0, .70f, .49f, .075f, .060f, .065f, UNDER);
            s.Ellipsoid(0, .72f, .54f, .035f, .030f, .030f, ACCENT);
            Eyes(s, .76f, .46f, .092f, .038f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .10f, .85f, .36f);
                s.Tube(side * .10f, .85f, .36f, side * .13f, .97f, .35f, .045f, .028f, FUR);
                s.Tube(side * .13f, .97f, .35f, side * .14f, 1.05f, .34f, .024f, .018f, HOOF); // black lynx ear tuft!
            }
            s.Part("tail", 0, .60f, -.40f);
            s.Tube(0, .60f, -.40f, 0, .56f, -.54f, .052f, .052f, FUR);
            s.Ellipsoid(0, .55f, -.56f, .050f, .050f, .048f, HOOF); // bobtail black tip

            QuadLegs(s, .125f, .20f, -.24f, .46f, .072f, .055f, UNDER, true);
        }

        static void BuildBeaver(Sculptor s)
        {
            s.Part("body", 0, .28f, 0);
            s.Ellipsoid(0, .28f, -.04f, .22f, .19f, .28f, FUR);
            s.Ellipsoid(0, .27f, .14f, .18f, .17f, .20f, FUR);
            s.Ellipsoid(0, .16f, .04f, .15f, .07f, .26f, UNDER);

            s.Part("head", 0, .34f, .30f);
            s.Ellipsoid(0, .35f, .36f, .14f, .13f, .14f, FUR);
            s.Ellipsoid(0, .32f, .46f, .095f, .075f, .08f, DARK);
            s.Ellipsoid(0, .35f, .52f, .042f, .036f, .036f, HOOF);
            // Iconic orange-amber chisel incisors
            s.Box(0, .25f, .49f, .038f, .045f, .022f, HORN);
            Eyes(s, .39f, .42f, .095f, .034f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .12f, .44f, .33f);
                s.Ellipsoid(side * .13f, .45f, .33f, .038f, .038f, .032f, DARK);
            }
            // Wide flat beaver paddle tail
            s.Part("tail", 0, .20f, -.30f);
            s.Ellipsoid(0, .15f, -.48f, .13f, .035f, .22f, HOOF);

            QuadLegs(s, .15f, .16f, -.16f, .18f, .058f, .048f, HOOF, true);
        }

        static void BuildToad(Sculptor s)
        {
            s.Part("body", 0, .20f, 0);
            s.Ellipsoid(0, .21f, -.02f, .23f, .14f, .24f, FUR);
            s.Ellipsoid(0, .14f, .04f, .19f, .08f, .20f, UNDER);

            s.Part("head", 0, .24f, .16f);
            s.Ellipsoid(0, .24f, .22f, .21f, .11f, .15f, FUR);
            s.Ellipsoid(0, .18f, .24f, .18f, .07f, .13f, UNDER); // vocal throat sac
            // Raised amphibian dorsal eyes
            for (int side = -1; side <= 1; side += 2)
            {
                s.Ellipsoid(side * .12f, .33f, .23f, .058f, .055f, .058f, FUR);
                s.Ellipsoid(side * .13f, .34f, .25f, .044f, .042f, .044f, IRIS);
                s.Box(side * .14f, .34f, .28f, .026f, .016f, .020f, HOOF);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .16f, .28f, .12f);
                s.Ellipsoid(side * .17f, .28f, .11f, .048f, .036f, .065f, DARK); // parotoid glands
            }
            s.Part("tail", 0, .16f, -.24f);
            s.Ellipsoid(0, .16f, -.25f, .05f, .04f, .045f, DARK);

            for (int i = 0; i < 4; i++)
            {
                float lx = (i % 2 == 0 ? -1f : 1f) * (i < 2 ? .20f : .16f);
                float lz = i < 2 ? -.10f : .15f;
                s.Part("leg" + i, lx, .16f, lz);
                if (i < 2)
                {
                    s.Ellipsoid(lx * 1.1f, .15f, lz - .03f, .075f, .075f, .11f, FUR);
                    s.Ellipsoid(lx * 1.12f, .045f, lz + .04f, .07f, .035f, .11f, DARK);
                }
                else
                {
                    s.Tube(lx, .16f, lz, lx * .95f, .04f, lz + .05f, .048f, .040f, FUR);
                    s.Ellipsoid(lx * .95f, .035f, lz + .07f, .055f, .030f, .065f, DARK);
                }
            }
        }

        static void BuildEagle(Sculptor s)
        {
            s.Part("body", 0, .44f, 0);
            s.Ellipsoid(0, .45f, .02f, .17f, .22f, .24f, FUR);
            s.Ellipsoid(0, .58f, .10f, .14f, .14f, .15f, ACCENT); // white feathered ruff

            s.Part("head", 0, .68f, .12f);
            s.Ellipsoid(0, .72f, .14f, .12f, .12f, .13f, ACCENT); // snowy eagle crown
            // Hooked golden raptor beak
            s.Tube(0, .71f, .24f, 0, .69f, .35f, .055f, .035f, HORN);
            s.Tube(0, .69f, .35f, 0, .63f, .37f, .034f, .022f, HORN);
            Eyes(s, .74f, .20f, .085f, .034f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wingL" : "wingR", side * .16f, .52f, .04f);
                s.Tube(side * .16f, .52f, .06f, side * .52f, .56f, .02f, .06f, .045f, FUR);
                s.Ellipsoid(side * .38f, .50f, -.04f, .24f, .038f, .15f, FUR);
                s.Ellipsoid(side * .56f, .48f, -.08f, .16f, .032f, .12f, DARK);
            }

            s.Part("tail", 0, .34f, -.20f);
            s.Ellipsoid(0, .28f, -.34f, .12f, .035f, .18f, ACCENT);

            for (int i = 0; i < 2; i++)
            {
                float lx = (i == 0 ? -1f : 1f) * .09f;
                s.Part("leg" + i, lx, .26f, .02f);
                s.Tube(lx, .26f, .02f, lx, .05f, .04f, .048f, .034f, HORN);
                s.Ellipsoid(lx, .035f, .07f, .055f, .032f, .085f, HORN);
            }
        }

        static void BuildOwl(Sculptor s)
        {
            s.Part("body", 0, .40f, 0);
            s.Ellipsoid(0, .40f, 0, .18f, .22f, .19f, FUR);
            s.Ellipsoid(0, .38f, .08f, .14f, .18f, .13f, UNDER);

            s.Part("head", 0, .60f, .03f);
            s.Ellipsoid(0, .65f, .04f, .155f, .14f, .135f, FUR);
            s.Ellipsoid(0, .65f, .13f, .13f, .11f, .05f, UNDER); // facial disc
            s.Tube(0, .63f, .17f, 0, .59f, .21f, .034f, .020f, HOOF);
            Eyes(s, .66f, .15f, .072f, .042f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .10f, .76f, .04f);
                s.Tube(side * .10f, .76f, .04f, side * .15f, .85f, .02f, .035f, .022f, DARK);
                s.Part(side < 0 ? "wingL" : "wingR", side * .17f, .46f, .02f);
                s.Ellipsoid(side * .28f, .40f, -.02f, .14f, .16f, .14f, FUR);
                s.Ellipsoid(side * .38f, .36f, -.06f, .10f, .11f, .12f, DARK);
            }

            s.Part("tail", 0, .24f, -.16f);
            s.Ellipsoid(0, .20f, -.24f, .10f, .035f, .12f, FUR);

            for (int i = 0; i < 2; i++)
            {
                float lx = (i == 0 ? -1f : 1f) * .085f;
                s.Part("leg" + i, lx, .20f, .02f);
                s.Tube(lx, .20f, .02f, lx, .04f, .04f, .048f, .040f, FUR);
                s.Ellipsoid(lx, .035f, .07f, .052f, .032f, .075f, HOOF);
            }
        }

        static void BuildPenguin(Sculptor s)
        {
            s.Part("body", 0, .44f, 0);
            s.Ellipsoid(0, .44f, -.02f, .18f, .30f, .17f, FUR);
            s.Ellipsoid(0, .42f, .06f, .14f, .26f, .12f, UNDER); // crisp white tuxedo belly
            s.Ellipsoid(0, .64f, .06f, .13f, .055f, .11f, ACCENT); // golden emperor collar

            s.Part("head", 0, .72f, .01f);
            s.Ellipsoid(0, .77f, .02f, .125f, .12f, .125f, FUR);
            for (int side = -1; side <= 1; side += 2)
                s.Ellipsoid(side * .085f, .75f, .05f, .042f, .048f, .045f, ACCENT);
            s.Tube(0, .76f, .13f, 0, .74f, .26f, .042f, .022f, HORN);
            Eyes(s, .79f, .10f, .078f, .032f);

            // Flippers required by Winged test invariant (kind == 13 -> Wings.Count == 2)
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wingL" : "wingR", side * .18f, .56f, 0);
                s.Tube(side * .18f, .56f, 0, side * .24f, .28f, -.02f, .052f, .034f, FUR);
            }

            s.Part("tail", 0, .18f, -.15f);
            s.Tube(0, .18f, -.15f, 0, .11f, -.25f, .055f, .035f, FUR);

            for (int i = 0; i < 2; i++)
            {
                float lx = (i == 0 ? -1f : 1f) * .085f;
                s.Part("leg" + i, lx, .16f, .02f);
                s.Tube(lx, .16f, .02f, lx, .04f, .03f, .045f, .040f, HORN);
                s.Ellipsoid(lx, .032f, .08f, .062f, .030f, .090f, HORN);
            }
        }

        static void BuildCamel(Sculptor s)
        {
            s.Part("body", 0, 1.24f, 0);
            s.Ellipsoid(0, 1.22f, .14f, .34f, .34f, .44f, FUR);
            s.Ellipsoid(0, 1.20f, -.28f, .32f, .32f, .42f, FUR);
            // Sculpted twin desert humps
            s.Ellipsoid(0, 1.60f, .16f, .22f, .24f, .24f, DARK);
            s.Ellipsoid(0, 1.56f, -.26f, .20f, .22f, .22f, DARK);
            // S-curved neck
            s.Path(new[] { 0f, 1.16f, .48f, 0f, 1.06f, .74f, 0f, 1.46f, .86f, 0f, 1.76f, .88f },
                new[] { .18f, .15f, .13f, .12f }, FUR);

            s.Part("head", 0, 1.78f, .90f);
            s.Ellipsoid(0, 1.82f, .96f, .15f, .14f, .19f, FUR);
            s.Tube(0, 1.78f, 1.08f, 0, 1.73f, 1.28f, .11f, .085f, UNDER);
            Eyes(s, 1.86f, 1.02f, .13f, .046f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .13f, 1.92f, .90f);
                s.Ellipsoid(side * .16f, 1.94f, .88f, .05f, .06f, .045f, FUR);
            }
            s.Part("tail", 0, 1.24f, -.68f);
            s.Tube(0, 1.24f, -.68f, 0, .90f, -.76f, .05f, .055f, DARK);

            QuadLegs(s, .22f, .34f, -.40f, .96f, .11f, .082f, DARK, true);
        }

        static void BuildScorpion(Sculptor s, bool rad)
        {
            float sc = rad ? 1.25f : 1.0f;
            s.Part("body", 0, .20f * sc, 0);
            s.Ellipsoid(0, .20f * sc, .08f * sc, .20f * sc, .10f * sc, .22f * sc, FUR);
            s.Ellipsoid(0, .19f * sc, -.16f * sc, .17f * sc, .095f * sc, .20f * sc, DARK);
            if (rad) s.Ellipsoid(0, .28f * sc, -.04f * sc, .09f * sc, .06f * sc, .16f * sc, GLOW);

            s.Part("head", 0, .20f * sc, .26f * sc);
            s.Ellipsoid(0, .20f * sc, .32f * sc, .15f * sc, .09f * sc, .12f * sc, FUR);
            Eyes(s, .23f * sc, .38f * sc, .07f * sc, .032f * sc, rad);
            // Twin heavy crusher pedipalp pincers
            for (int side = -1; side <= 1; side += 2)
            {
                s.Tube(side * .14f * sc, .19f * sc, .32f * sc, side * .28f * sc, .19f * sc, .48f * sc, .045f * sc, .042f * sc, DARK);
                s.Ellipsoid(side * .26f * sc, .20f * sc, .58f * sc, .075f * sc, .055f * sc, .11f * sc, FUR);
                s.Tube(side * .23f * sc, .20f * sc, .64f * sc, side * .20f * sc, .20f * sc, .74f * sc, .035f * sc, .022f * sc, HOOF);
                s.Tube(side * .30f * sc, .20f * sc, .64f * sc, side * .26f * sc, .20f * sc, .74f * sc, .035f * sc, .022f * sc, HOOF);
            }

            // High-arched segmented stinger tail
            s.Part("tail", 0, .22f * sc, -.32f * sc);
            s.Path(new[]
            {
                0f, .22f * sc, -.32f * sc,
                0f, .38f * sc, -.48f * sc,
                0f, .58f * sc, -.38f * sc,
                0f, .64f * sc, -.16f * sc
            }, new[] { .075f * sc, .062f * sc, .052f * sc, .058f * sc }, FUR);
            s.Ellipsoid(0, .64f * sc, -.12f * sc, .065f * sc, .062f * sc, .075f * sc, rad ? GLOW : DARK);
            s.Tube(0, .62f * sc, -.06f * sc, 0, .54f * sc, .02f * sc, .035f * sc, .020f * sc, HOOF);

            // Exactly 6 articulated walking legs (leg0..leg5)
            float[] zPos = { -.18f * sc, -.02f * sc, .14f * sc };
            for (int row = 0; row < 3; row++)
                for (int side = 0; side < 2; side++)
                {
                    int idx = row * 2 + side;
                    float sx = side == 0 ? -1f : 1f;
                    float px = sx * .17f * sc, pz = zPos[row];
                    s.Part("leg" + idx, px, .18f * sc, pz);
                    s.Tube(px, .18f * sc, pz, sx * .34f * sc, .25f * sc, pz + .02f, .036f * sc, .032f * sc, DARK);
                    s.Tube(sx * .34f * sc, .25f * sc, pz + .02f, sx * .42f * sc, .03f * sc, pz, .032f * sc, .026f * sc, FUR);
                }
        }

        static void BuildTurtle(Sculptor s)
        {
            s.Part("body", 0, .22f, 0);
            // Domed carapace + marginal rim + pale plastron
            s.Ellipsoid(0, .25f, 0, .28f, .17f, .33f, FUR);
            s.Ellipsoid(0, .17f, 0, .31f, .055f, .36f, DARK);
            s.Ellipsoid(0, .12f, 0, .25f, .05f, .30f, UNDER);

            s.Part("head", 0, .20f, .32f);
            s.Tube(0, .19f, .30f, 0, .24f, .46f, .068f, .062f, DARK);
            s.Ellipsoid(0, .25f, .49f, .075f, .068f, .085f, DARK);
            Eyes(s, .27f, .52f, .058f, .028f);

            s.Part("tail", 0, .16f, -.33f);
            s.Tube(0, .16f, -.33f, 0, .13f, -.45f, .045f, .024f, DARK);

            for (int i = 0; i < 4; i++)
            {
                float lx = (i % 2 == 0 ? -1f : 1f) * .24f;
                float lz = i < 2 ? -.18f : .20f;
                s.Part("leg" + i, lx, .14f, lz);
                s.Tube(lx, .14f, lz, lx * 1.22f, .04f, lz + .04f, .058f, .052f, DARK);
                s.Ellipsoid(lx * 1.25f, .035f, lz + .06f, .062f, .032f, .075f, HOOF);
            }
        }

        static void BuildMoose(Sculptor s)
        {
            s.Part("body", 0, 1.24f, 0);
            s.Ellipsoid(0, 1.30f, .18f, .38f, .42f, .46f, FUR);
            s.Ellipsoid(0, 1.58f, .22f, .24f, .18f, .28f, DARK); // moose shoulder hump
            s.Ellipsoid(0, 1.18f, -.32f, .32f, .34f, .42f, FUR);
            s.Tube(0, 1.36f, .50f, 0, 1.56f, .72f, .22f, .17f, DARK);
            // Dewlap throat bell
            s.Tube(0, 1.26f, .64f, 0, 1.02f, .66f, .065f, .042f, DARK);

            s.Part("head", 0, 1.58f, .74f);
            s.Ellipsoid(0, 1.60f, .82f, .19f, .20f, .24f, FUR);
            // Bulbous overhanging moose muzzle
            s.Ellipsoid(0, 1.52f, 1.06f, .15f, .16f, .22f, DARK);
            Eyes(s, 1.66f, .88f, .16f, .048f);
            // Wide palmate shovel antlers + tines
            for (int side = -1; side <= 1; side += 2)
            {
                s.Tube(side * .14f, 1.72f, .78f, side * .38f, 1.84f, .74f, .05f, .05f, HORN);
                s.Ellipsoid(side * .56f, 1.94f, .70f, .24f, .14f, .24f, HORN);
                for (int t = -1; t <= 1; t++)
                    s.Tube(side * (.52f + t * .08f), 2.02f, .70f + t * .12f, side * (.66f + t * .10f), 2.18f, .72f + t * .14f, .038f, .026f, HORN);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .17f, 1.68f, .72f);
                s.Tube(side * .17f, 1.68f, .72f, side * .34f, 1.74f, .64f, .058f, .040f, FUR);
            }
            s.Part("tail", 0, 1.26f, -.72f);
            s.Ellipsoid(0, 1.24f, -.76f, .075f, .085f, .085f, DARK);

            QuadLegs(s, .22f, .32f, -.40f, .96f, .11f, .075f, HOOF, false);
        }

        static void BuildCapybara(Sculptor s)
        {
            s.Part("body", 0, .38f, 0);
            s.Ellipsoid(0, .39f, .06f, .22f, .22f, .28f, FUR);
            s.Ellipsoid(0, .38f, -.18f, .23f, .22f, .26f, FUR);

            s.Part("head", 0, .48f, .28f);
            // Iconic blunt rectangular capybara head & dark muzzle
            s.Box(0, .50f, .40f, .14f, .13f, .16f, FUR);
            s.Box(0, .48f, .52f, .125f, .115f, .08f, DARK);
            Eyes(s, .57f, .42f, .125f, .034f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .12f, .62f, .31f);
                s.Ellipsoid(side * .13f, .64f, .31f, .038f, .042f, .035f, DARK);
            }
            s.Part("tail", 0, .38f, -.42f);
            s.Ellipsoid(0, .37f, -.44f, .048f, .048f, .042f, DARK);

            QuadLegs(s, .145f, .18f, -.22f, .26f, .062f, .050f, HOOF, false);
        }

        static void BuildRay(Sculptor s, bool gamma)
        {
            // Expected: 0 legs, 2 wings (wingL, wingR), head, body, tail!
            s.Part("body", 0, .42f, 0);
            s.Ellipsoid(0, .42f, .04f, .26f, .11f, .42f, FUR);
            s.Ellipsoid(0, .36f, .04f, .22f, .06f, .36f, UNDER);
            s.Ellipsoid(0, .50f, -.04f, .08f, .06f, .28f, GLOW);

            s.Part("head", 0, .42f, .40f);
            s.Ellipsoid(0, .42f, .48f, .20f, .09f, .14f, FUR);
            // Articulated cephalic manta lobes (brings MeshFilter count to 7 >= 6 while keeping Legs == 0)
            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .14f, .42f, .52f);
                s.Tube(side * .14f, .42f, .54f, side * .18f, .41f, .74f, .048f, .030f, gamma ? GLOW : DARK);
            }
            s.Part("head", 0, .42f, .40f);
            Eyes(s, .45f, .52f, .16f, .038f, true);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wingL" : "wingR", side * .22f, .42f, .04f);
                s.Ellipsoid(side * .48f, .42f, .04f, .30f, .052f, .28f, FUR);
                s.Tube(side * .24f, .43f, .18f, side * .86f, .45f, -.08f, .055f, .028f, gamma ? GLOW : DARK);
                s.Tube(side * .30f, .42f, -.06f, side * .84f, .44f, -.16f, .045f, .025f, GLOW);
            }

            s.Part("tail", 0, .41f, -.36f);
            s.Tube(0, .41f, -.36f, 0, .38f, -.98f, .055f, .024f, DARK);
            s.Ellipsoid(0, .39f, -.92f, .042f, .042f, .08f, GLOW);
        }

        static void BuildAshHound(Sculptor s)
        {
            s.Part("body", 0, .74f, 0);
            s.Ellipsoid(0, .76f, .16f, .25f, .26f, .34f, FUR);
            s.Ellipsoid(0, .72f, -.24f, .20f, .21f, .30f, FUR);
            // Exposed pale rib arches + glowing Cherenkov dorsal spines
            for (int r = 0; r < 3; r++)
            {
                float rz = .20f - r * .16f;
                for (int side = -1; side <= 1; side += 2)
                    s.Tube(side * .18f, .82f, rz, side * .25f, .62f, rz, .032f, .026f, HORN);
                s.Tube(0, .96f, rz, 0, 1.12f, rz - .06f, .042f, .024f, GLOW);
            }

            s.Part("head", 0, .86f, .48f);
            s.Ellipsoid(0, .88f, .56f, .18f, .16f, .20f, FUR);
            s.Tube(0, .82f, .68f, 0, .78f, .90f, .11f, .08f, DARK);
            Eyes(s, .92f, .64f, .12f, .045f, true);

            s.Part("tail", 0, .74f, -.52f);
            s.Tube(0, .74f, -.52f, 0, .54f, -.92f, .06f, .03f, DARK);

            QuadLegs(s, .16f, .26f, -.30f, .60f, .085f, .058f, HOOF, true);
        }

        static void BuildCherenkovLeech(Sculptor s)
        {
            s.Part("body", 0, .20f, 0);
            for (int seg = -2; seg <= 2; seg++)
            {
                float r = .16f - Math.Abs(seg) * .018f;
                s.Ellipsoid(0, .20f + (seg % 2 == 0 ? .02f : 0f), seg * .16f, r * 1.2f, r * .85f, .13f, seg % 2 == 0 ? FUR : DARK);
                s.Ellipsoid(0, .30f, seg * .16f, .045f, .035f, .055f, GLOW);
            }

            s.Part("head", 0, .20f, .38f);
            s.Ellipsoid(0, .20f, .46f, .15f, .12f, .12f, DARK);
            s.Ring(0, .20f, .54f, .09f, .032f, HORN); // circular lamprey tooth maw
            s.Ellipsoid(0, .20f, .52f, .055f, .055f, .045f, GLOW);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wingL" : "wingR", side * .16f, .16f, 0);
                s.Ellipsoid(side * .23f, .15f, 0, .09f, .028f, .32f, GLOW);
            }
            s.Part("tail", 0, .18f, -.38f);
            s.Ellipsoid(0, .18f, -.50f, .12f, .035f, .16f, DARK);
        }

        static void BuildMutantBoar(Sculptor s)
        {
            s.Part("body", 0, .68f, 0);
            s.Ellipsoid(0, .72f, .16f, .36f, .36f, .42f, FUR);
            s.Ellipsoid(0, .64f, -.28f, .30f, .30f, .36f, FUR);
            s.Ellipsoid(-.16f, 1.02f, .18f, .11f, .09f, .14f, GLOW);
            s.Ellipsoid(.18f, .98f, .06f, .09f, .08f, .12f, GLOW);

            // Twin tusked mutant heads angled outward from neck!
            s.Part("head", 0, .74f, .50f);
            for (int side = -1; side <= 1; side += 2)
            {
                float hx = side * .17f;
                s.Ellipsoid(hx, .76f, .62f, .16f, .16f, .18f, FUR);
                s.Tube(hx, .72f, .74f, hx * 1.18f, .66f, .92f, .11f, .085f, DARK);
                s.Tube(hx - side * .06f, .65f, .86f, hx - side * .09f, .78f, .94f, .032f, .022f, HORN);
                s.Tube(hx + side * .08f, .65f, .86f, hx + side * .14f, .78f, .94f, .032f, .022f, HORN);
                s.Ellipsoid(hx + side * .08f, .80f, .70f, .042f, .042f, .042f, GLOW);
            }
            s.Part("tail", 0, .70f, -.60f);
            s.Tube(0, .70f, -.60f, 0, .58f, -.78f, .055f, .040f, GLOW);

            QuadLegs(s, .22f, .28f, -.34f, .50f, .105f, .075f, HOOF, false);
        }

        static void BuildSlagCrab(Sculptor s)
        {
            s.Part("body", 0, .36f, 0);
            s.Ellipsoid(0, .38f, 0, .42f, .20f, .34f, FUR);
            s.Ellipsoid(0, .48f, -.02f, .24f, .12f, .20f, DARK);
            s.Ellipsoid(0, .52f, 0, .14f, .08f, .12f, GLOW);

            s.Part("head", 0, .36f, .28f);
            s.Ellipsoid(0, .35f, .32f, .20f, .10f, .10f, DARK);
            for (int side = -1; side <= 1; side += 2)
            {
                s.Tube(side * .10f, .42f, .34f, side * .13f, .52f, .38f, .032f, .032f, DARK);
                s.Ellipsoid(side * .13f, .54f, .40f, .042f, .042f, .042f, GLOW);
                // Asymmetric giant slag crusher claw on right, cutter claw on left
                float cl = side > 0 ? 1.25f : .95f;
                s.Tube(side * .28f, .32f, .24f, side * .44f, .30f, .48f, .06f, .055f, DARK);
                s.Ellipsoid(side * .42f, .30f, .60f, .10f * cl, .085f * cl, .14f * cl, FUR);
            }

            float[] zPos = { -.18f, 0f, .16f };
            for (int row = 0; row < 3; row++)
                for (int side = 0; side < 2; side++)
                {
                    int idx = row * 2 + side;
                    float sx = side == 0 ? -1f : 1f;
                    s.Part("leg" + idx, sx * .34f, .30f, zPos[row]);
                    s.Tube(sx * .34f, .30f, zPos[row], sx * .54f, .36f, zPos[row], .048f, .042f, DARK);
                    s.Tube(sx * .54f, .36f, zPos[row], sx * .62f, .04f, zPos[row], .042f, .032f, HOOF);
                }
        }

        static void BuildFalloutCrow(Sculptor s)
        {
            s.Part("body", 0, .42f, 0);
            s.Ellipsoid(0, .42f, .02f, .16f, .19f, .24f, FUR);
            s.Ellipsoid(0, .52f, .10f, .14f, .11f, .13f, DARK);

            s.Part("head", 0, .58f, .16f);
            s.Ellipsoid(0, .62f, .20f, .115f, .11f, .13f, FUR);
            s.Tube(0, .60f, .30f, 0, .56f, .48f, .048f, .024f, HOOF);
            Eyes(s, .64f, .25f, .082f, .036f, true);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wingL" : "wingR", side * .15f, .48f, .04f);
                s.Ellipsoid(side * .36f, .46f, -.02f, .22f, .036f, .15f, FUR);
                s.Tube(side * .20f, .49f, .08f, side * .56f, .50f, -.08f, .045f, .028f, DARK);
            }
            s.Part("tail", 0, .34f, -.22f);
            s.Ellipsoid(0, .28f, -.36f, .11f, .032f, .18f, DARK);

            for (int i = 0; i < 2; i++)
            {
                float lx = (i == 0 ? -1f : 1f) * .08f;
                s.Part("leg" + i, lx, .24f, .02f);
                s.Tube(lx, .24f, .02f, lx, .04f, .04f, .038f, .032f, HOOF);
                s.Ellipsoid(lx, .032f, .07f, .048f, .028f, .07f, HOOF);
            }
        }

        static void BuildBoneStalker(Sculptor s)
        {
            s.Part("body", 0, 1.16f, 0);
            s.Tube(0, .92f, -.10f, 0, 1.48f, .18f, .18f, .24f, FUR);
            for (int r = 0; r < 4; r++)
            {
                float ry = 1.06f + r * .11f, rz = -.02f + r * .05f;
                for (int side = -1; side <= 1; side += 2)
                    s.Tube(0, ry, rz, side * .26f, ry - .04f, rz + .14f, .045f, .035f, HORN);
            }
            s.Ellipsoid(0, 1.24f, .08f, .11f, .14f, .10f, GLOW);

            s.Part("head", 0, 1.56f, .24f);
            s.Ellipsoid(0, 1.62f, .32f, .18f, .18f, .22f, HORN);
            s.Tube(0, 1.54f, .44f, 0, 1.48f, .62f, .11f, .08f, HORN);
            Eyes(s, 1.64f, .44f, .11f, .048f, true);
            for (int side = -1; side <= 1; side += 2)
                s.Tube(side * .13f, 1.72f, .28f, side * .26f, 1.96f, .10f, .05f, .030f, DARK);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wingL" : "wingR", side * .26f, 1.42f, .14f);
                s.Tube(side * .26f, 1.42f, .14f, side * .42f, 1.02f, .32f, .07f, .055f, FUR);
                s.Tube(side * .42f, 1.02f, .32f, side * .34f, .66f, .54f, .055f, .035f, HORN); // scythe bone blade
            }
            s.Part("tail", 0, .92f, -.16f);
            s.Tube(0, .92f, -.16f, 0, .52f, -.68f, .075f, .038f, HORN);

            for (int i = 0; i < 2; i++)
            {
                float lx = (i == 0 ? -1f : 1f) * .20f;
                s.Part("leg" + i, lx, .92f, -.06f);
                s.Tube(lx, .92f, -.06f, lx, .48f, -.18f, .10f, .075f, FUR);
                s.Tube(lx, .48f, -.18f, lx, .06f, .04f, .075f, .060f, HORN);
                s.Ellipsoid(lx, .05f, .10f, .09f, .05f, .14f, HOOF);
            }
        }

        static void BuildVitrifiedGolem(Sculptor s)
        {
            s.Part("body", 0, 1.24f, 0);
            s.Ellipsoid(0, .92f, 0, .38f, .22f, .28f, DARK);
            s.Ellipsoid(0, 1.34f, -.02f, .48f, .42f, .34f, FUR);
            s.Ellipsoid(0, 1.36f, .24f, .18f, .20f, .14f, GLOW); // Cherenkov reactor chest
            for (int side = -1; side <= 1; side += 2)
            {
                s.Tube(side * .40f, 1.36f, .10f, side * .16f, 1.36f, .32f, .065f, .05f, DARK);
                s.Tube(side * .34f, 1.68f, -.06f, side * .50f, 2.04f, -.16f, .085f, .04f, GLOW);
            }

            s.Part("head", 0, 1.78f, .04f);
            s.Ellipsoid(0, 1.92f, .08f, .24f, .20f, .22f, DARK);
            Eyes(s, 1.94f, .24f, .10f, .048f, true);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wingL" : "wingR", side * .54f, 1.56f, 0);
                s.Ellipsoid(side * .56f, 1.60f, 0, .20f, .16f, .18f, DARK);
                s.Tube(side * .56f, 1.56f, 0, side * .66f, 1.04f, .10f, .15f, .13f, FUR);
                s.Tube(side * .66f, 1.04f, .10f, side * .68f, .66f, .22f, .14f, .16f, DARK);
                s.Ellipsoid(side * .68f, .58f, .26f, .15f, .13f, .16f, GLOW);
            }
            for (int i = 0; i < 2; i++)
            {
                float lx = (i == 0 ? -1f : 1f) * .28f;
                s.Part("leg" + i, lx, .80f, 0);
                s.Tube(lx, .80f, 0, lx * 1.12f, .42f, .06f, .17f, .15f, DARK);
                s.Tube(lx * 1.12f, .42f, .06f, lx * 1.05f, .08f, .02f, .15f, .16f, FUR);
                s.Ellipsoid(lx * 1.05f, .06f, .06f, .17f, .06f, .20f, HOOF);
            }
        }

        static void BuildSpiralWraith(Sculptor s)
        {
            s.Part("body", 0, 1.18f, 0);
            s.Tube(0, .74f, 0, 0, 1.56f, .04f, .20f, .32f, FUR);
            s.Ring(0, 1.32f, .04f, .28f, .055f, GLOW);

            s.Part("head", 0, 1.62f, .06f);
            s.Ellipsoid(0, 1.76f, .08f, .20f, .22f, .20f, DARK);
            s.Ellipsoid(0, 1.76f, .24f, .09f, .09f, .06f, GLOW);
            for (int side = -1; side <= 1; side += 2)
                s.Tube(side * .15f, 1.90f, .06f, side * .26f, 2.16f, -.04f, .05f, .03f, HORN);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wingL" : "wingR", side * .32f, 1.48f, .04f);
                s.Tube(side * .32f, 1.48f, .04f, side * .54f, .92f, .22f, .09f, .05f, DARK);
            }
            s.Part("tail", 0, .72f, -.12f);
            s.Tube(0, .72f, -.12f, 0, .28f, -.36f, .12f, .05f, DARK);

            for (int i = 0; i < 2; i++)
            {
                float lx = (i == 0 ? -1f : 1f) * .15f;
                s.Part("leg" + i, lx, .74f, 0);
                s.Tube(lx, .74f, 0, lx * 1.2f, .10f, -.08f, .10f, .045f, FUR);
            }
        }

        static void BuildGlassSpider(Sculptor s)
        {
            // Expected by UltraGraphicsSpaceAndWildlifeTests: Legs.Count == 4 (8 arachnid legs grouped in 4 pairs!)
            s.Part("body", 0, .22f, 0);
            s.Ellipsoid(0, .22f, .10f, .13f, .10f, .13f, DARK); // cephalothorax
            s.Ellipsoid(0, .26f, -.16f, .19f, .16f, .22f, FUR); // faceted crystal opisthosoma
            s.Ellipsoid(0, .34f, -.16f, .11f, .08f, .14f, HORN);

            s.Part("head", 0, .22f, .22f);
            s.Ellipsoid(0, .21f, .26f, .10f, .085f, .085f, DARK);
            Eyes(s, .24f, .31f, .048f, .030f);
            for (int side = -1; side <= 1; side += 2)
                s.Tube(side * .05f, .18f, .31f, side * .035f, .12f, .37f, .030f, .022f, HORN); // chelicerae

            s.Part("tail", 0, .20f, -.36f);
            s.Ellipsoid(0, .20f, -.40f, .055f, .045f, .055f, HORN); // spinnerets

            // 8 jointed arachnid legs paired cleanly onto leg0 (BL pair), leg1 (BR pair), leg2 (FL pair), leg3 (FR pair)
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f;
                bool hind = i < 2;
                float baseZ = hind ? .02f : .16f;
                s.Part("leg" + i, sx * .12f, .21f, baseZ);
                for (int sub = 0; sub < 2; sub++)
                {
                    float zOff = baseZ + (sub == 0 ? -.06f : .06f);
                    float spreadZ = zOff + (hind ? (sub == 0 ? -.16f : -.04f) : (sub == 0 ? .04f : .18f));
                    s.Tube(sx * .12f, .21f, zOff, sx * .28f, .34f, (zOff + spreadZ) * .5f, .030f, .026f, DARK);
                    s.Tube(sx * .28f, .34f, (zOff + spreadZ) * .5f, sx * .38f, .03f, spreadZ, .026f, .022f, HORN);
                }
            }
        }

        static void BuildMushroomBeetle(Sculptor s)
        {
            // Expected by UltraGraphicsSpaceAndWildlifeTests: Legs.Count == 4 (6 beetle legs grouped onto leg0..leg3!)
            s.Part("body", 0, .20f, 0);
            s.Ellipsoid(0, .21f, -.04f, .20f, .14f, .25f, FUR);
            s.Ellipsoid(0, .20f, .16f, .15f, .11f, .11f, DARK); // pronotum shield
            // Three spotted toadstool mushrooms sprouting from the beetle's shell!
            float[,] shrooms = { { 0f, .32f, -.08f, .11f }, { -.10f, .29f, -.18f, .075f }, { .10f, .30f, .02f, .075f } };
            for (int m = 0; m < 3; m++)
            {
                float mx = shrooms[m, 0], my = shrooms[m, 1], mz = shrooms[m, 2], mr = shrooms[m, 3];
                s.Tube(mx, my - .04f, mz, mx, my + .08f, mz, mr * .38f, mr * .34f, UNDER);
                s.Ellipsoid(mx, my + .10f, mz, mr, mr * .52f, mr, ACCENT);
                s.Ellipsoid(mx, my + .14f, mz, mr * .38f, mr * .25f, mr * .38f, UNDER);
            }

            s.Part("head", 0, .19f, .25f);
            s.Ellipsoid(0, .19f, .30f, .11f, .085f, .09f, HOOF);
            // Forked rhinoceros beetle horn!
            s.Tube(0, .21f, .35f, 0, .34f, .46f, .038f, .028f, HORN);
            for (int side = -1; side <= 1; side += 2)
                s.Tube(0, .33f, .45f, side * .055f, .38f, .48f, .026f, .020f, HORN);
            Eyes(s, .21f, .33f, .085f, .030f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .07f, .22f, .34f);
                s.Tube(side * .07f, .22f, .34f, side * .15f, .26f, .44f, .024f, .020f, DARK);
            }
            s.Part("tail", 0, .16f, -.28f);
            s.Ellipsoid(0, .16f, -.31f, .06f, .045f, .05f, DARK);

            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f;
                bool hind = i < 2;
                float baseZ = hind ? -.10f : .12f;
                s.Part("leg" + i, sx * .16f, .15f, baseZ);
                s.Tube(sx * .16f, .15f, baseZ, sx * .30f, .03f, baseZ + (hind ? -.06f : .06f), .034f, .026f, HOOF);
                if (hind)
                    s.Tube(sx * .17f, .15f, 0f, sx * .31f, .03f, 0f, .032f, .026f, HOOF);
            }
        }

        static void BuildSkyWhale(Sculptor s)
        {
            // Expected: 0 legs, 2 wings (wingL, wingR), head, body, tail!
            s.Part("body", 0, .96f, 0);
            s.Ellipsoid(0, .98f, .12f, .46f, .38f, .68f, FUR);
            s.Ellipsoid(0, .82f, .16f, .38f, .22f, .60f, UNDER); // pleated ivory belly
            s.Tube(0, 1.30f, -.18f, 0, 1.54f, -.34f, .09f, .045f, DARK); // dorsal fin

            s.Part("head", 0, .96f, .68f);
            s.Ellipsoid(0, .98f, .86f, .38f, .30f, .38f, FUR);
            s.Ellipsoid(0, .82f, .86f, .34f, .16f, .34f, UNDER);
            Eyes(s, .96f, .88f, .35f, .052f);

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "earL" : "earR", side * .26f, .86f, -.42f);
                s.Ellipsoid(side * .34f, .84f, -.46f, .14f, .045f, .14f, DARK); // pelvic stabilizer fins
            }

            for (int side = -1; side <= 1; side += 2)
            {
                s.Part(side < 0 ? "wingL" : "wingR", side * .42f, .86f, .24f);
                s.Path(new[]
                {
                    side * .42f, .86f, .26f,
                    side * .78f, .80f, .14f,
                    side * 1.12f, .72f, -.08f
                }, new[] { .14f, .10f, .055f }, FUR);
                s.Ellipsoid(side * .78f, .80f, .08f, .32f, .05f, .18f, DARK);
                s.Ellipsoid(side * 1.08f, .73f, -.08f, .10f, .045f, .09f, GLOW);
            }

            s.Part("tail", 0, .94f, -.56f);
            s.Tube(0, .94f, -.56f, 0, .92f, -1.16f, .24f, .10f, FUR);
            // Wide horizontal whale flukes
            for (int side = -1; side <= 1; side += 2)
                s.Ellipsoid(side * .28f, .92f, -1.24f, .28f, .05f, .16f, DARK);
        }
    }
}
