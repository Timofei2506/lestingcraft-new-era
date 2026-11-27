using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.World;
using LestingCraft.Gameplay;
using LestingCraft.Runtime;

namespace LestingCraft.Presentation
{
    public enum SurfaceSoundKind
    {
        Stone = 0,
        Grass = 1,
        Dirt = 2,
        Wood = 3,
        Snow = 4,
        Water = 5,
        Metal = 6
    }

    /// <summary>
    /// Zero-asset procedural foley & dynamic biome soundscape synthesizer.
    /// Generates organic multi-layered AudioClips once at 22.05 kHz and plays them with zero runtime GC.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        const int SampleRate = 22050;
        public static GameAudio Instance { get; private set; }

        AudioSource sfxSource;
        AudioSource uiSource;
        AudioSource ambientPrimary;
        AudioSource ambientSecondary;

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        float lastMineTickAt;
        float lastUiHoverAt;
        float detailTimer = 0.4f;
        uint rngState = 0x9E3779B9u;

        public static GameAudio Ensure(Transform parent = null)
        {
            if (Instance) return Instance;
            var go = new GameObject("Procedural Game Audio");
            if (parent) go.transform.SetParent(parent, false);
            Instance = go.AddComponent<GameAudio>();
            Instance.Setup();
            return Instance;
        }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
                return;
            }
            Instance = this;
            Setup();
        }

        void Setup()
        {
            if (sfxSource) return;
            sfxSource = CreateSource("SFX", false, .85f);
            uiSource = CreateSource("UI", false, .65f);
            ambientPrimary = CreateSource("Ambient Primary", true, 0f);
            ambientSecondary = CreateSource("Ambient Secondary", true, 0f);
            BuildClipBank();
        }

        AudioSource CreateSource(string label, bool loop, float vol)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            src.volume = vol;
            return src;
        }

        float NextRand()
        {
            rngState ^= rngState << 13;
            rngState ^= rngState >> 17;
            rngState ^= rngState << 5;
            return ((rngState & 0xFFFFFF) / 8388608f) - 1f;
        }

        public static float SfxGain
        {
            get { return VisualSettings.Current != null ? Mathf.Clamp01(VisualSettings.Current.SfxVolume) : .8f; }
        }

        public static float AmbientGain
        {
            get { return VisualSettings.Current != null ? Mathf.Clamp01(VisualSettings.Current.AmbientVolume) : .65f; }
        }

        public static SurfaceSoundKind ClassifyBlock(ushort blockId)
        {
            if (blockId == 7 || blockId == 8) return SurfaceSoundKind.Water;
            if (blockId == 3 || blockId == 5 || blockId == 44 || blockId == 23 || blockId == 24)
                return SurfaceSoundKind.Grass;
            if (blockId == 2 || blockId == 6 || blockId == 13 || blockId == 27 || blockId == 34)
                return SurfaceSoundKind.Dirt;
            if (blockId == 4 || blockId == 12 || blockId == 16 || blockId == 18)
                return SurfaceSoundKind.Wood;
            if (blockId == 14 || blockId == 32 || blockId == 33)
                return SurfaceSoundKind.Snow;
            if (blockId >= 54 && blockId <= 69)
                return SurfaceSoundKind.Metal;
            return SurfaceSoundKind.Stone;
        }

        void BuildClipBank()
        {
            if (clips.Count > 0) return;
            // Footsteps per material
            clips["step_grass"] = SynthFootstep(0.11f, 150f, 0.22f, 0.78f, 0.28f);
            clips["step_dirt"]  = SynthFootstep(0.12f, 130f, 0.30f, 0.70f, 0.36f);
            clips["step_stone"] = SynthFootstep(0.09f, 340f, 0.58f, 0.35f, 0.55f);
            clips["step_wood"]  = SynthFootstep(0.11f, 210f, 0.65f, 0.25f, 0.42f);
            clips["step_snow"]  = SynthFootstep(0.14f, 420f, 0.16f, 0.85f, 0.62f);
            clips["step_water"] = SynthSplash(0.18f, 280f, 0.55f);
            clips["step_metal"] = SynthMetallicTap(0.12f, 520f, 890f);

            // Jump, land, splash
            clips["jump"]   = SynthFootstep(0.11f, 190f, 0.35f, 0.55f, 0.30f);
            clips["land"]   = SynthFootstep(0.18f, 95f,  0.65f, 0.55f, 0.24f);
            clips["splash"] = SynthSplash(0.32f, 210f, 0.85f);

            // Mining ticks, block break & block place
            clips["mine_stone"] = SynthMetallicTap(0.085f, 680f, 1140f);
            clips["mine_wood"]  = SynthFootstep(0.095f, 240f, 0.72f, 0.28f, 0.40f);
            clips["mine_soft"]  = SynthFootstep(0.095f, 165f, 0.28f, 0.72f, 0.34f);

            clips["break_stone"] = SynthBreak(0.28f, 260f, 0.50f, 0.75f);
            clips["break_wood"]  = SynthBreak(0.26f, 185f, 0.65f, 0.55f);
            clips["break_soft"]  = SynthBreak(0.24f, 140f, 0.25f, 0.85f);
            clips["place_block"] = SynthFootstep(0.12f, 175f, 0.68f, 0.38f, 0.38f);

            // Combat, bow, survival, UI
            clips["swing"]       = SynthWhoosh(0.16f, 420f, 160f);
            clips["hit_flesh"]   = SynthImpact(0.16f, 135f, 0.75f);
            clips["hit_kill"]    = SynthKillChord(0.28f);
            clips["bow_shot"]    = SynthBowTwang(0.22f);
            clips["player_hurt"] = SynthImpact(0.22f, 92f, 0.90f);
            clips["heal_eat"]    = SynthChime(0.26f, 440f, 660f);
            clips["craft_done"]  = SynthChime(0.32f, 523.25f, 783.99f);
            clips["ui_hover"]    = SynthUiTick(0.032f, 920f, 0.22f);
            clips["ui_click"]    = SynthUiTick(0.055f, 640f, 0.45f);
            clips["bag_toggle"]  = SynthWhoosh(0.13f, 280f, 440f);

            // Nature & dimension one-shots + seamless ambient loops
            clips["nature_bird"]    = SynthBirdChirp();
            clips["nature_cricket"] = SynthCricketTrill();
            clips["radia_geiger"]   = SynthGeigerBurst();

            clips["amb_wind"]  = SynthSeamlessLoop("amb_wind", 4.0f, 0.06f, 0.14f, 0f);
            clips["amb_rain"]  = SynthSeamlessLoop("amb_rain", 3.5f, 0.28f, 0.55f, 0f);
            clips["amb_radia"] = SynthSeamlessLoop("amb_radia", 4.0f, 0.04f, 0.09f, 58f);
            clips["amb_space"] = SynthSeamlessLoop("amb_space", 4.0f, 0.03f, 0.07f, 82f);

            // Deep Frontier ambience (2.0): one pair of layers per world so the six new
            // dimensions stop borrowing the overworld wind. Every key here must also appear in
            // RealmSoundscape.LoopKeys / DetailKeys - check_realm_soundscape_core enforces it.
            clips["amb_crystal"] = SynthCrystalAir();
            clips["amb_ash"]     = SynthSeamlessLoop("amb_ash", 4.5f, 0.10f, 0.30f, 41f);
            clips["amb_ocean"]   = SynthOceanSurf();
            clips["amb_storm"]   = SynthSeamlessLoop("amb_storm", 5.0f, 0.16f, 0.34f, 34f);
            clips["amb_fungal"]  = SynthSeamlessLoop("amb_fungal", 5.0f, 0.05f, 0.12f, 63f);
            clips["amb_void"]    = SynthVoidDrone();
            clips["amb_paradise"]= SynthSeamlessLoop("amb_paradise", 4.0f, 0.07f, 0.10f, 0f);
            clips["amb_hum"]     = SynthSeamlessLoop("amb_hum", 4.0f, 0.02f, 0.04f, 120f);

            clips["ice_tick"]    = SynthIceTick();
            clips["ember_pop"]   = SynthEmberPop();
            clips["wave_crash"]  = SynthWaveCrash();
            clips["thunder"]     = SynthThunder();
            clips["spore_tick"]  = SynthSporeTick();
            clips["void_pulse"]  = SynthVoidPulse();
        }

        public AudioClip GetClip(string key)
        {
            BuildClipBank();
            AudioClip c;
            return clips.TryGetValue(key, out c) ? c : null;
        }

        AudioClip MakeClip(string name, float[] data)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                if (float.IsNaN(data[i]) || float.IsInfinity(data[i])) data[i] = 0f;
                float a = Mathf.Abs(data[i]);
                if (a > peak) peak = a;
            }
            if (peak > 0.92f)
            {
                float inv = 0.92f / peak;
                for (int i = 0; i < data.Length; i++) data[i] *= inv;
            }
            var clip = AudioClip.Create("LC_" + name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip SynthFootstep(float sec, float bodyHz, float toneWeight, float noiseWeight, float lpAlpha)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(sec * SampleRate));
            var buf = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * (26f / Mathf.Max(.07f, sec * 8f)));
                float sweep = bodyHz * (1f + 0.45f * Mathf.Exp(-t * 48f));
                float tone = Mathf.Sin(t * sweep * 6.2831853f) * Mathf.Exp(-t * 34f);
                lp += lpAlpha * (NextRand() - lp);
                buf[i] = (tone * toneWeight + lp * noiseWeight) * env * 0.65f;
            }
            return MakeClip("footstep", buf);
        }

        AudioClip SynthMetallicTap(float sec, float f1, float f2)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(sec * SampleRate));
            var buf = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.45f * (NextRand() - lp);
                float m1 = Mathf.Sin(t * f1 * 6.2831853f) * Mathf.Exp(-t * 36f);
                float m2 = Mathf.Sin(t * f2 * 6.2831853f) * Mathf.Exp(-t * 54f) * 0.55f;
                float transient = lp * Mathf.Exp(-t * 85f) * 0.7f;
                buf[i] = (m1 + m2 + transient) * 0.55f;
            }
            return MakeClip("metal_tap", buf);
        }

        AudioClip SynthSplash(float sec, float baseHz, float amp)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(sec * SampleRate));
            var buf = new float[n];
            float lp = 0f, bp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float mod = 0.22f + 0.16f * Mathf.Sin(t * 34f);
                lp += mod * (NextRand() - lp);
                bp += 0.08f * (lp - bp);
                float bubble = Mathf.Sin(t * (baseHz + 180f * t) * 6.2831853f) * Mathf.Exp(-t * 16f) * 0.35f;
                float env = Mathf.Sin(Mathf.Clamp01(t / sec) * Mathf.PI) * Mathf.Exp(-t * 5.5f);
                buf[i] = ((lp - bp) * 0.85f + bubble) * env * amp;
            }
            return MakeClip("splash", buf);
        }

        AudioClip SynthBreak(float sec, float thudHz, float toneAmt, float crunchAmt)
        {
            int n = Mathf.Max(128, Mathf.RoundToInt(sec * SampleRate));
            var buf = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.42f * (NextRand() - lp);
                // 3 staggered micro-impacts so it sounds like crumbling chunks, not one flat click
                float e1 = Mathf.Exp(-t * 22f);
                float e2 = t > 0.045f ? Mathf.Exp(-(t - 0.045f) * 28f) * 0.65f : 0f;
                float e3 = t > 0.095f ? Mathf.Exp(-(t - 0.095f) * 32f) * 0.45f : 0f;
                float thud = Mathf.Sin(t * thudHz * 6.2831853f) * Mathf.Exp(-t * 26f) * toneAmt;
                buf[i] = thud + lp * (e1 + e2 + e3) * crunchAmt * 0.72f;
            }
            return MakeClip("break", buf);
        }

        AudioClip SynthWhoosh(float sec, float fStart, float fEnd)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(sec * SampleRate));
            var buf = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float alpha = Mathf.Lerp(fStart, fEnd, u) / (SampleRate * 0.35f);
                lp += Mathf.Clamp(alpha, 0.04f, 0.65f) * (NextRand() - lp);
                float env = Mathf.Sin(u * Mathf.PI);
                buf[i] = lp * env * 0.60f;
            }
            return MakeClip("whoosh", buf);
        }

        AudioClip SynthImpact(float sec, float punchHz, float amp)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(sec * SampleRate));
            var buf = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.35f * (NextRand() - lp);
                float freq = punchHz * (1f + 1.4f * Mathf.Exp(-t * 50f));
                float body = Mathf.Sin(t * freq * 6.2831853f) * Mathf.Exp(-t * 20f);
                float slap = lp * Mathf.Exp(-t * 48f) * 0.65f;
                buf[i] = (body * 0.85f + slap) * amp;
            }
            return MakeClip("impact", buf);
        }

        AudioClip SynthKillChord(float sec)
        {
            int n = Mathf.Max(128, Mathf.RoundToInt(sec * SampleRate));
            var buf = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.4f * (NextRand() - lp);
                float impact = Mathf.Sin(t * 125f * 6.2831853f) * Mathf.Exp(-t * 22f) * 0.6f;
                float ring = (Mathf.Sin(t * 587.33f * 6.2831853f) + 0.6f * Mathf.Sin(t * 880f * 6.2831853f)) * Mathf.Exp(-t * 11f) * 0.25f;
                buf[i] = impact + ring + lp * Mathf.Exp(-t * 55f) * 0.35f;
            }
            return MakeClip("kill", buf);
        }

        AudioClip SynthBowTwang(float sec)
        {
            int n = Mathf.Max(128, Mathf.RoundToInt(sec * SampleRate));
            var buf = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.45f * (NextRand() - lp);
                float f = 195f + 55f * Mathf.Exp(-t * 18f);
                float twang = (Mathf.Sin(t * f * 6.2831853f) + 0.4f * Mathf.Sin(t * f * 2f * 6.2831853f)) * Mathf.Exp(-t * 14f);
                float zip = lp * Mathf.Exp(-t * 36f) * 0.45f;
                buf[i] = (twang * 0.55f + zip) * 0.75f;
            }
            return MakeClip("bow", buf);
        }

        AudioClip SynthChime(float sec, float f1, float f2)
        {
            int n = Mathf.Max(128, Mathf.RoundToInt(sec * SampleRate));
            var buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float a = Mathf.Sin(t * f1 * 6.2831853f) * Mathf.Exp(-t * 11f) * 0.42f;
                float b = t > 0.06f ? Mathf.Sin((t - 0.06f) * f2 * 6.2831853f) * Mathf.Exp(-(t - 0.06f) * 10f) * 0.42f : 0f;
                buf[i] = a + b;
            }
            return MakeClip("chime", buf);
        }

        AudioClip SynthUiTick(float sec, float hz, float amp)
        {
            int n = Mathf.Max(32, Mathf.RoundToInt(sec * SampleRate));
            var buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                buf[i] = Mathf.Sin(t * hz * 6.2831853f) * Mathf.Exp(-t * 95f) * amp;
            }
            return MakeClip("ui_tick", buf);
        }

        AudioClip SynthBirdChirp()
        {
            int n = Mathf.RoundToInt(0.34f * SampleRate);
            var buf = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float env = t < 0.13f ? Mathf.Sin((t / 0.13f) * Mathf.PI) :
                            (t > 0.17f && t < 0.32f ? Mathf.Sin(((t - 0.17f) / 0.15f) * Mathf.PI) * 0.85f : 0f);
                float f = t < 0.15f ? Mathf.Lerp(2100f, 2950f, t / 0.13f) : Mathf.Lerp(2450f, 1950f, (t - 0.17f) / 0.15f);
                phase += f / SampleRate * 6.2831853f;
                buf[i] = Mathf.Sin(phase) * env * 0.24f;
            }
            return MakeClip("bird", buf);
        }

        AudioClip SynthCricketTrill()
        {
            int n = Mathf.RoundToInt(0.38f * SampleRate);
            var buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float pulse = Mathf.Max(0f, Mathf.Sin(t * 28f * 6.2831853f));
                float fade = Mathf.Sin(Mathf.Clamp01(t / 0.38f) * Mathf.PI);
                buf[i] = Mathf.Sin(t * 4150f * 6.2831853f) * pulse * fade * 0.14f;
            }
            return MakeClip("cricket", buf);
        }

        AudioClip SynthGeigerBurst()
        {
            int n = Mathf.RoundToInt(0.18f * SampleRate);
            var buf = new float[n];
            float[] clicks = { 0.005f, 0.048f, 0.115f };
            for (int c = 0; c < clicks.Length; c++)
            {
                int start = Mathf.RoundToInt(clicks[c] * SampleRate);
                for (int k = 0; k < 44 && start + k < n; k++)
                {
                    float e = Mathf.Exp(-k * 0.16f);
                    buf[start + k] += (k % 2 == 0 ? 0.65f : -0.52f) * e;
                }
            }
            return MakeClip("geiger", buf);
        }

        AudioClip SynthSeamlessLoop(string name, float sec, float lpCut, float noiseAmt, float droneHz)
        {
            int n = Mathf.RoundToInt(sec * SampleRate);
            var raw = new float[n];
            float lp = 0f, hp = 0f;
            int droneCycles = droneHz > 0f ? Mathf.Max(1, Mathf.RoundToInt(droneHz * sec)) : 0;
            float exactHz = droneCycles / sec;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += lpCut * (NextRand() - lp);
                hp += 0.008f * (lp - hp);
                float sample = (lp - hp) * noiseAmt;
                if (exactHz > 0f)
                {
                    sample += (Mathf.Sin(t * exactHz * 6.2831853f) * 0.14f +
                               Mathf.Sin(t * exactHz * 1.5f * 6.2831853f) * 0.07f);
                }
                raw[i] = sample;
            }
            // Crossfade endpoints over 15% window so loop has zero seam click
            int fade = n / 7;
            var outBuf = new float[n];
            Array.Copy(raw, outBuf, n);
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                outBuf[i] = Mathf.Lerp(raw[n - fade + i], raw[i], w);
                outBuf[n - fade + i] = Mathf.Lerp(raw[n - fade + i], raw[i], 1f - w);
            }
            return MakeClip(name, outBuf);
        }

        /// <summary>Airless crystal realm: no air noise at all, just a thin high shimmer.</summary>
        AudioClip SynthCrystalAir()
        {
            int n = Mathf.RoundToInt(4.0f * SampleRate);
            var buf = new float[n];
            // 3 cycles/second => whole number of cycles per loop, so no seam click.
            float f1 = 3f / 4.0f, f2 = 7f / 4.0f, f3 = 11f / 4.0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float a = Mathf.Sin(t * f1 * 6.2831853f);
                float b = Mathf.Sin(t * f2 * 6.2831853f) * 0.5f;
                float c = Mathf.Sin(t * f3 * 6.2831853f) * 0.28f;
                // Slow beating between the partials reads as "resonating ice", not a tone.
                buf[i] = (a * 0.10f + b * 0.07f + c * 0.05f) * (0.75f + 0.25f * Mathf.Sin(t * 0.5f * 6.2831853f));
            }
            return MakeClip("crystal_air", buf);
        }

        /// <summary>Aquaria: broad surf wash with a slower swell underneath, both loop-exact.</summary>
        AudioClip SynthOceanSurf()
        {
            int n = Mathf.RoundToInt(5.0f * SampleRate);
            var buf = new float[n];
            float lp = 0f, hp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.30f * (NextRand() - lp);
                hp += 0.010f * (lp - hp);
                // Two whole-number swell cycles across the loop keep the wash seamless.
                float swell = 0.55f + 0.45f * Mathf.Sin(t / 5f * 2f * 6.2831853f);
                float wash = (lp - hp) * swell * 0.42f;
                float body = Mathf.Sin(t / 5f * 1f * 6.2831853f) * 0.05f;
                buf[i] = wash + body;
            }
            return MakeClip("ocean_surf", buf);
        }

        /// <summary>Nihility: sub-bass drone plus an inverted, slowly detuning shimmer.</summary>
        AudioClip SynthVoidDrone()
        {
            int n = Mathf.RoundToInt(6.0f * SampleRate);
            var buf = new float[n];
            float f1 = 2f / 6.0f, f2 = 5f / 6.0f, f3 = 9f / 6.0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float sub = Mathf.Sin(t * f1 * 6.2831853f) * 0.16f;                       // 33 Hz
                float mid = Mathf.Sin(t * (f2 * 12f) * 6.2831853f) * 0.06f;               // hollow fifth
                float hiss = Mathf.Sin(t * (f2 * 96f) * 6.2831853f) * 0.02f;              // thin edge
                buf[i] = sub + mid + hiss + Mathf.Sin(t * f3 * 6.2831853f) * 0.03f;
            }
            return MakeClip("void_drone", buf);
        }

        /// <summary>Crystalia detail: a single glacial crack.</summary>
        AudioClip SynthIceTick()
        {
            int n = Mathf.RoundToInt(0.26f * SampleRate);
            var buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float ping = Mathf.Sin(t * (2400f - 900f * t / 0.26f) * 6.2831853f) * Mathf.Exp(-t * 26f);
                float crack = NextRand() * Mathf.Exp(-t * 90f) * 0.5f;
                buf[i] = ping * 0.20f + crack * 0.16f;
            }
            return MakeClip("ice_tick", buf);
        }

        /// <summary>Emberfall detail: a popping cinder, low and dry.</summary>
        AudioClip SynthEmberPop()
        {
            int n = Mathf.RoundToInt(0.20f * SampleRate);
            var buf = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += 0.18f * (NextRand() - lp);
                float thump = Mathf.Sin(t * (150f + 240f * Mathf.Exp(-t * 40f)) * 6.2831853f) * Mathf.Exp(-t * 34f);
                buf[i] = thump * 0.26f + lp * Mathf.Exp(-t * 55f) * 0.22f;
            }
            return MakeClip("ember_pop", buf);
        }

        /// <summary>Aquaria detail: a wave breaking over reef shelf.</summary>
        AudioClip SynthWaveCrash()
        {
            int n = Mathf.RoundToInt(1.1f * SampleRate);
            var buf = new float[n];
            float lp = 0f, hp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / 1.1f;
                lp += 0.34f * (NextRand() - lp);
                hp += 0.02f * (lp - hp);
                float env = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                buf[i] = (lp - hp) * env * env * 0.55f;
            }
            return MakeClip("wave_crash", buf);
        }

        /// <summary>Stormreach detail: thunder that rolls after the visible strike.</summary>
        AudioClip SynthThunder()
        {
            float sec = 2.6f;
            int n = Mathf.RoundToInt(sec * SampleRate);
            var buf = new float[n];
            float lp = 0f, rumble = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                // Attack crack, then a long decaying roll with two late secondary rumbles.
                float crack = NextRand() * Mathf.Exp(-t * 34f) * 0.55f;
                lp += 0.05f * (NextRand() - lp);
                rumble += 0.020f * (lp - rumble);
                float roll = Mathf.Exp(-t * 1.25f);
                float swell = roll * (0.65f + 0.35f * Mathf.Sin(t * 2.1f * 6.2831853f));
                float sub = Mathf.Sin(t * (52f - 14f * Mathf.Clamp01(t / sec)) * 6.2831853f) * roll * 0.30f;
                buf[i] = crack + rumble * 9f * swell * 0.55f + sub;
            }
            return MakeClip("thunder", buf);
        }

        /// <summary>Verdant detail: a wet spore sac releasing.</summary>
        AudioClip SynthSporeTick()
        {
            int n = Mathf.RoundToInt(0.34f * SampleRate);
            var buf = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / 0.34f;
                lp += 0.09f * (NextRand() - lp);
                float puff = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * (1f - u * 0.5f);
                float wet = Mathf.Sin(t * (320f - 160f * u) * 6.2831853f) * Mathf.Exp(-t * 22f);
                buf[i] = lp * puff * 0.48f + wet * 0.14f;
            }
            return MakeClip("spore_tick", buf);
        }

        /// <summary>Nihility detail: a distant pulse of raw emptiness, slightly wrong pitch.</summary>
        AudioClip SynthVoidPulse()
        {
            int n = Mathf.RoundToInt(1.4f * SampleRate);
            var buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = t / 1.4f;
                float env = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                // Deliberately non-integer ratio: the two partials beat against each other.
                float a = Mathf.Sin(t * 44f * 6.2831853f);
                float b = Mathf.Sin(t * 66.7f * 6.2831853f) * 0.6f;
                buf[i] = (a + b) * env * 0.22f;
            }
            return MakeClip("void_pulse", buf);
        }

        void PlaySfx(string key, float volume = 1f, float pitchMin = .93f, float pitchMax = 1.07f)
        {
            float gain = SfxGain * volume;
            if (gain <= 0.001f || !sfxSource) return;
            var clip = GetClip(key);
            if (!clip) return;
            sfxSource.pitch = Mathf.Lerp(pitchMin, pitchMax, (NextRand() + 1f) * .5f);
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(gain));
        }

        public static void PlayFootstep(ushort belowBlock, bool sprinting, bool crouching, bool inStation)
        {
            if (!Instance) return;
            float vol = crouching ? 0.22f : sprinting ? 0.62f : 0.44f;
            if (inStation) { Instance.PlaySfx("step_metal", vol, .94f, 1.06f); return; }
            switch (ClassifyBlock(belowBlock))
            {
                case SurfaceSoundKind.Grass: Instance.PlaySfx("step_grass", vol, .92f, 1.08f); break;
                case SurfaceSoundKind.Dirt:  Instance.PlaySfx("step_dirt",  vol, .92f, 1.08f); break;
                case SurfaceSoundKind.Wood:  Instance.PlaySfx("step_wood",  vol, .94f, 1.06f); break;
                case SurfaceSoundKind.Snow:  Instance.PlaySfx("step_snow",  vol, .93f, 1.09f); break;
                case SurfaceSoundKind.Water: Instance.PlaySfx("step_water", vol * .95f, .90f, 1.10f); break;
                case SurfaceSoundKind.Metal: Instance.PlaySfx("step_metal", vol, .94f, 1.06f); break;
                default:                     Instance.PlaySfx("step_stone", vol, .93f, 1.07f); break;
            }
        }

        public static void PlayJump(bool swimming)
        {
            if (!Instance) return;
            Instance.PlaySfx(swimming ? "step_water" : "jump", .48f, .95f, 1.08f);
        }

        public static void PlayLand(float impactSpeed, bool intoWater)
        {
            if (!Instance) return;
            if (intoWater) { Instance.PlaySfx("splash", .72f, .92f, 1.06f); return; }
            float vol = Mathf.Clamp(impactSpeed / 14f, .25f, .85f);
            Instance.PlaySfx("land", vol, .88f, 1.04f);
        }

        public static void PlayMineTick(ushort blockId, float progress)
        {
            if (!Instance || Time.unscaledTime - Instance.lastMineTickAt < 0.21f) return;
            Instance.lastMineTickAt = Time.unscaledTime;
            var kind = ClassifyBlock(blockId);
            string key = kind == SurfaceSoundKind.Wood ? "mine_wood" :
                         (kind == SurfaceSoundKind.Grass || kind == SurfaceSoundKind.Dirt || kind == SurfaceSoundKind.Snow) ? "mine_soft" :
                         "mine_stone";
            float p = 0.92f + Mathf.Clamp01(progress) * 0.26f;
            Instance.PlaySfx(key, .52f, p - .03f, p + .03f);
        }

        public static void PlayBlockBreak(ushort blockId)
        {
            if (!Instance) return;
            var kind = ClassifyBlock(blockId);
            string key = kind == SurfaceSoundKind.Wood ? "break_wood" :
                         (kind == SurfaceSoundKind.Grass || kind == SurfaceSoundKind.Dirt || kind == SurfaceSoundKind.Snow) ? "break_soft" :
                         "break_stone";
            Instance.PlaySfx(key, .78f, .92f, 1.08f);
        }

        public static void PlayBlockPlace(ushort blockId)
        {
            if (!Instance) return;
            Instance.PlaySfx(ClassifyBlock(blockId) == SurfaceSoundKind.Metal ? "step_metal" : "place_block", .66f, .94f, 1.06f);
        }

        public static void PlaySwing()        { if (Instance) Instance.PlaySfx("swing", .48f, .92f, 1.08f); }
        public static void PlayMobHit(bool killed)
        {
            if (!Instance) return;
            Instance.PlaySfx(killed ? "hit_kill" : "hit_flesh", killed ? .82f : .72f, .94f, 1.06f);
        }
        public static void PlayBowShot()      { if (Instance) Instance.PlaySfx("bow_shot", .72f, .95f, 1.05f); }
        public static void PlayPlayerHurt()   { if (Instance) Instance.PlaySfx("player_hurt", .80f, .92f, 1.04f); }
        public static void PlayHealOrEat()    { if (Instance) Instance.PlaySfx("heal_eat", .68f, .96f, 1.04f); }
        public static void PlayCraftDone()    { if (Instance) Instance.PlaySfx("craft_done", .70f, .98f, 1.02f); }
        public static void PlayBagToggle()    { if (Instance) Instance.PlaySfx("bag_toggle", .42f, .94f, 1.06f); }

        public static void PlayUiHover()
        {
            if (!Instance || !Instance.uiSource || SfxGain <= 0.001f) return;
            if (Time.unscaledTime - Instance.lastUiHoverAt < 0.055f) return;
            Instance.lastUiHoverAt = Time.unscaledTime;
            var clip = Instance.GetClip("ui_hover");
            if (clip) { Instance.uiSource.pitch = 1f; Instance.uiSource.PlayOneShot(clip, SfxGain * .35f); }
        }

        public static void PlayUiClick()
        {
            if (!Instance || !Instance.uiSource || SfxGain <= 0.001f) return;
            var clip = Instance.GetClip("ui_click");
            if (clip) { Instance.uiSource.pitch = 1f; Instance.uiSource.PlayOneShot(clip, SfxGain * .55f); }
        }

        public void TickSoundscape(PrototypeGame game)
        {
            if (!ambientPrimary || !ambientSecondary) return;
            float masterAmb = AmbientGain;
            var session = game ? game.Session : null;
            var world = session ? session.World : null;
            bool playingWorld = session != null && world != null && world.Ready && !game.Paused;
            if (!playingWorld || masterAmb <= 0.001f)
            {
                ambientPrimary.volume = Mathf.MoveTowards(ambientPrimary.volume, 0f, Time.unscaledDeltaTime * 1.5f);
                ambientSecondary.volume = Mathf.MoveTowards(ambientSecondary.volume, 0f, Time.unscaledDeltaTime * 1.5f);
                return;
            }

            Dimension dim = world.Dimension;
            bool inSpace = game.DockedAtOrbitalStation || game.InMoonFlight || (game.SpaceHelm != null && game.SpaceHelm.Active);
            bool raining = !inSpace && session.Data != null && RealmSoundscape.WeatherReaches(dim) &&
                           WeatherSample.At(session.Data.seed, (int)dim, session.Data.clock).Precipitation > 0.15f;

            // Ambience is table-driven: RealmSoundscape owns which layers a world uses and how
            // loud they sit, this method only crossfades towards that mix.
            var mix = inSpace ? RealmSoundscape.Space : RealmSoundscape.For(dim);
            var wantClip = GetClip(mix.Primary);
            if (ambientPrimary.clip != wantClip)
            {
                ambientPrimary.clip = wantClip;
                if (wantClip) ambientPrimary.Play();
            }
            float shelterScale = session.Sheltered ? 0.45f : 1f;
            ambientPrimary.volume = Mathf.MoveTowards(ambientPrimary.volume,
                masterAmb * mix.PrimaryVolume * shelterScale, Time.deltaTime * 0.8f);

            // Rain overlays the overworld mix (and any world whose weather is wet) instead of
            // replacing the realm layer, so a downpour in Lestingia still sits over the wind.
            string secondaryKey = raining && !mix.HasSecondary ? "amb_rain" : (mix.HasSecondary ? mix.Secondary : null);
            float secondaryVolume = raining && !mix.HasSecondary ? 0.48f : mix.SecondaryVolume;
            var secondaryClip = string.IsNullOrEmpty(secondaryKey) ? null : GetClip(secondaryKey);
            if (secondaryClip != null)
            {
                if (ambientSecondary.clip != secondaryClip) { ambientSecondary.clip = secondaryClip; ambientSecondary.Play(); }
                ambientSecondary.volume = Mathf.MoveTowards(ambientSecondary.volume,
                    masterAmb * secondaryVolume * shelterScale, Time.deltaTime * 0.7f);
            }
            else
            {
                ambientSecondary.volume = Mathf.MoveTowards(ambientSecondary.volume, 0f, Time.deltaTime * 0.9f);
            }

            // Realm detail one-shots. Stormreach does not use a timer at all: its thunder is
            // edge-triggered from the same clock+seed the sky shader uses for the flash, so the
            // clap lands with the visible bolt (plus a short travel lag) and ahead of the flash
            // only when the player switches realms mid-storm.
            if (mix.HasDetail)
            {
                if (mix.DetailFollowsLightning)
                {
                    if (session.Data != null && RealmSoundscape.StrikeBegins(session.Data.clock - RealmSoundscape.ThunderLag, dim))
                        PlaySfx(mix.Detail, 0.55f * AmbientGain * shelterScale, .92f, 1.04f);
                }
                else
                {
                    detailTimer -= Time.deltaTime;
                    if (detailTimer <= 0f)
                    {
                        // +/-35% jitter keeps the cadence from sounding like a metronome.
                        detailTimer = mix.DetailPeriod * (0.65f + (NextRand() + 1f) * 0.35f);
                        string key = mix.Detail;
                        if (key == "nature_bird" && dim == Dimension.Lestingia && session.Data != null)
                        {
                            // Overworld nature swaps voice by time of day instead of a second key.
                            double dayFrac = ((session.Data.clock % 1440.0) + 1440.0) % 1440.0;
                            bool isNight = dayFrac < 330.0 || dayFrac > 1170.0;
                            key = isNight ? "nature_cricket" : "nature_bird";
                        }
                        PlaySfx(key, (dim == Dimension.Radia ? 0.28f : 0.34f) * AmbientGain * shelterScale, .90f, 1.14f);
                    }
                }
            }
            if (!mix.HasDetail) detailTimer = 0f;
        }

        void OnDestroy()
        {
            foreach (var kv in clips)
            {
                if (!kv.Value) continue;
                if (Application.isPlaying) Destroy(kv.Value);
                else DestroyImmediate(kv.Value);
            }
            clips.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
