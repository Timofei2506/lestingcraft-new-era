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
        float natureEventTimer = 3.5f;
        float geigerTimer = 0.4f;
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
            bool playingWorld = game && game.Session != null && game.World != null && game.World.Ready && !game.Paused;
            if (!playingWorld || masterAmb <= 0.001f)
            {
                ambientPrimary.volume = Mathf.MoveTowards(ambientPrimary.volume, 0f, Time.unscaledDeltaTime * 1.5f);
                ambientSecondary.volume = Mathf.MoveTowards(ambientSecondary.volume, 0f, Time.unscaledDeltaTime * 1.5f);
                return;
            }

            Dimension dim = game.World.Dimension;
            bool inSpace = game.DockedAtOrbitalStation || game.InMoonFlight || (game.SpaceHelm != null && game.SpaceHelm.Active);
            bool inRadia = dim == Dimension.Radia;
            bool raining = !inSpace && !inRadia && dim == Dimension.Lestingia && game.Session.Weather && AtmosphereProfile.Density(game.Session.Data.weather) > AtmosphereProfile.Density(0) * 1.15f;

            string targetLoop = inSpace ? "amb_space" : inRadia ? "amb_radia" : "amb_wind";
            var wantClip = GetClip(targetLoop);
            if (ambientPrimary.clip != wantClip)
            {
                ambientPrimary.clip = wantClip;
                if (wantClip) ambientPrimary.Play();
            }
            float shelterScale = game.Session.Sheltered ? 0.45f : 1f;
            ambientPrimary.volume = Mathf.MoveTowards(ambientPrimary.volume, masterAmb * 0.42f * shelterScale, Time.deltaTime * 0.8f);

            var rainClip = GetClip("amb_rain");
            if (raining)
            {
                if (ambientSecondary.clip != rainClip) { ambientSecondary.clip = rainClip; ambientSecondary.Play(); }
                ambientSecondary.volume = Mathf.MoveTowards(ambientSecondary.volume, masterAmb * 0.48f * shelterScale, Time.deltaTime * 0.7f);
            }
            else
            {
                ambientSecondary.volume = Mathf.MoveTowards(ambientSecondary.volume, 0f, Time.deltaTime * 0.9f);
            }

            // Procedural intermittent nature / radiation details
            if (inRadia)
            {
                geigerTimer -= Time.deltaTime;
                if (geigerTimer <= 0f)
                {
                    geigerTimer = 0.8f + ((NextRand() + 1f) * 1.4f);
                    PlaySfx("radia_geiger", 0.28f * AmbientGain, .92f, 1.12f);
                }
            }
            else if (!inSpace && dim == Dimension.Lestingia && !raining)
            {
                natureEventTimer -= Time.deltaTime;
                if (natureEventTimer <= 0f)
                {
                    natureEventTimer = 5.5f + (NextRand() + 1f) * 4.5f;
                    double dayFrac = ((game.Session.Data.clock % 1440.0) + 1440.0) % 1440.0;
                    bool isNight = dayFrac < 330.0 || dayFrac > 1170.0;
                    PlaySfx(isNight ? "nature_cricket" : "nature_bird", 0.34f * AmbientGain * shelterScale, .90f, 1.14f);
                }
            }
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
