using System;
using System.Collections.Generic;
using UnityEngine;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Procedural 64x64 voxel-hero portrait generator & local fallback profile preferences.
    /// Produces crisp, vibrant character avatars across 12 archetypes (used on the Main Menu,
    /// Player Profile, and Friends list) with zero external asset dependencies.
    /// </summary>
    public static class ProfileAvatar
    {
        public struct Archetype
        {
            public string Id, Title;
            public Color BgTop, BgBottom, Skin, Hair, Outfit, Trim, Eyes;
        }

        public static readonly Archetype[] Catalog =
        {
            new Archetype { Id = "pioneer",    Title = "Первопроходец",     BgTop = Hex(0x2A8FE8), BgBottom = Hex(0x13477A), Skin = Hex(0xF6C69F), Hair = Hex(0x6C3F23), Outfit = Hex(0x2D8E5B), Trim = Hex(0xFFD043), Eyes = Hex(0x1E6BB8) },
            new Archetype { Id = "sakura",     Title = "Хранитель Сакуры",  BgTop = Hex(0xF57CA8), BgBottom = Hex(0x7B2B5C), Skin = Hex(0xFBE0CC), Hair = Hex(0xF9A8C6), Outfit = Hex(0xFFF3F7), Trim = Hex(0xE84C82), Eyes = Hex(0x8C2D63) },
            new Archetype { Id = "knight",     Title = "Страж Королевства", BgTop = Hex(0x4A78D6), BgBottom = Hex(0x1B2D5E), Skin = Hex(0xECC19C), Hair = Hex(0xCBD6E6), Outfit = Hex(0x9BAEC8), Trim = Hex(0xF7C137), Eyes = Hex(0x2986FF) },
            new Archetype { Id = "alchemist",  Title = "Алхимик Эфира",     BgTop = Hex(0x7D54DE), BgBottom = Hex(0x2A185C), Skin = Hex(0xF2CBB0), Hair = Hex(0x472866), Outfit = Hex(0x5E35B1), Trim = Hex(0x44F2D8), Eyes = Hex(0x29E8C8) },
            new Archetype { Id = "skycaptain", Title = "Капитан Небес",     BgTop = Hex(0x38BDF8), BgBottom = Hex(0x0E5A8A), Skin = Hex(0xEAB78E), Hair = Hex(0x8A5229), Outfit = Hex(0x1E66A8), Trim = Hex(0xF59E0B), Eyes = Hex(0x0EA5E9) },
            new Archetype { Id = "fox",        Title = "Солнечный Лис",     BgTop = Hex(0xFB923C), BgBottom = Hex(0x7C2D12), Skin = Hex(0xFFF3DF), Hair = Hex(0xEA580C), Outfit = Hex(0xC2410C), Trim = Hex(0xFDE047), Eyes = Hex(0x16A34A) },
            new Archetype { Id = "astronaut",  Title = "Звездоплаватель",   BgTop = Hex(0x312E81), BgBottom = Hex(0x090D24), Skin = Hex(0xF8FAFC), Hair = Hex(0xE2E8F0), Outfit = Hex(0xF1F5F9), Trim = Hex(0xFBBF24), Eyes = Hex(0x38BDF8) },
            new Archetype { Id = "smith",      Title = "Мастер Горна",      BgTop = Hex(0xDC5626), BgBottom = Hex(0x4A180D), Skin = Hex(0xDEA37B), Hair = Hex(0x3D2318), Outfit = Hex(0x6D3B24), Trim = Hex(0xFF9F2E), Eyes = Hex(0xD97706) },
            new Archetype { Id = "druid",      Title = "Друид Водопадов",   BgTop = Hex(0x22C55E), BgBottom = Hex(0x115E38), Skin = Hex(0xEFCCA8), Hair = Hex(0x2F7D32), Outfit = Hex(0x1F6E43), Trim = Hex(0x67E8F9), Eyes = Hex(0x10B981) },
            new Archetype { Id = "monarch",    Title = "Владыка Лестингии", BgTop = Hex(0xEAB308), BgBottom = Hex(0x6B2137), Skin = Hex(0xF7CEAA), Hair = Hex(0x52301C), Outfit = Hex(0xB91C47), Trim = Hex(0xFACC15), Eyes = Hex(0x2563EB) },
            new Archetype { Id = "shadow",     Title = "Странник Затмения", BgTop = Hex(0x334155), BgBottom = Hex(0x0B1120), Skin = Hex(0xCBD5E1), Hair = Hex(0x1E293B), Outfit = Hex(0x1E1B4B), Trim = Hex(0xA855F7), Eyes = Hex(0x22D3EE) },
            new Archetype { Id = "cat",        Title = "Небесный Кот",      BgTop = Hex(0x34D399), BgBottom = Hex(0x0F5257), Skin = Hex(0xFDE6C4), Hair = Hex(0xF59E0B), Outfit = Hex(0x2563EB), Trim = Hex(0xFDE047), Eyes = Hex(0x059669) },
        };

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        const string PrefAvatar = "LC.LocalAvatar";
        const string PrefNick   = "LC.LocalNickname";
        const string PrefBio    = "LC.LocalBio";

        public static string LocalAvatar
        {
            get => ResolveId(PlayerPrefs.GetString(PrefAvatar, "pioneer"), null);
            set { PlayerPrefs.SetString(PrefAvatar, ResolveId(value, null)); PlayerPrefs.Save(); }
        }

        public static string LocalNickname
        {
            get
            {
                string v = PlayerPrefs.GetString(PrefNick, "").Trim();
                return string.IsNullOrEmpty(v) ? "Explorer" : v;
            }
            set
            {
                string clean = SanitizeLocalNick(value);
                if (!string.IsNullOrEmpty(clean))
                {
                    PlayerPrefs.SetString(PrefNick, clean);
                    PlayerPrefs.Save();
                }
            }
        }

        public static string LocalBio
        {
            get => PlayerPrefs.GetString(PrefBio, "Исследую парящие острова и долины Лестингии!");
            set
            {
                string clean = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                if (clean.Length > 80) clean = clean.Substring(0, 80);
                PlayerPrefs.SetString(PrefBio, clean);
                PlayerPrefs.Save();
            }
        }

        public static string SanitizeLocalNick(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var chars = new List<char>(24);
            foreach (char c in raw.Trim())
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')
                    chars.Add(c);
                if (chars.Count >= 24) break;
            }
            return chars.Count >= 3 ? new string(chars.ToArray()) : "";
        }

        public static int IndexOf(string id)
        {
            if (!string.IsNullOrEmpty(id))
                for (int i = 0; i < Catalog.Length; i++)
                    if (string.Equals(Catalog[i].Id, id, StringComparison.OrdinalIgnoreCase))
                        return i;
            return -1;
        }

        public static string ResolveId(string id, string seedFallback)
        {
            int idx = IndexOf(id);
            if (idx >= 0) return Catalog[idx].Id;
            if (!string.IsNullOrEmpty(seedFallback))
            {
                uint h = 2166136261u;
                for (int i = 0; i < seedFallback.Length; i++)
                    h = (h ^ char.ToLowerInvariant(seedFallback[i])) * 16777619u;
                return Catalog[(int)(h % (uint)Catalog.Length)].Id;
            }
            return Catalog[0].Id;
        }

        public static Archetype GetArchetype(string id, string seedFallback = null)
        {
            string resolved = ResolveId(id, seedFallback);
            int idx = IndexOf(resolved);
            return Catalog[idx >= 0 ? idx : 0];
        }

        public static string TitleOf(string id, string seedFallback = null) => GetArchetype(id, seedFallback).Title;
        public static Color AccentOf(string id, string seedFallback = null) => GetArchetype(id, seedFallback).Trim;

        public static string NextId(string currentId, int delta = 1)
        {
            int idx = IndexOf(currentId);
            if (idx < 0) idx = 0;
            int next = (idx + delta % Catalog.Length + Catalog.Length) % Catalog.Length;
            return Catalog[next].Id;
        }

        public static Sprite GetSprite(string id, string seedFallback = null)
        {
            string key = ResolveId(id, seedFallback);
            if (cache.TryGetValue(key, out var existing) && existing)
                return existing;

            var arch = GetArchetype(key);
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                name = "Avatar_" + key,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.DontSave
            };

            var px = new Color32[S * S];

            // 1. Rounded sunlit gradient background + crisp double frame
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    if (CornerClip(x, y, S, 7))
                    {
                        px[y * S + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    float t = y / (float)(S - 1);
                    float radial = 1f - Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(32f, 40f)) / 42f);
                    Color c = Color.Lerp(arch.BgBottom, arch.BgTop, Mathf.SmoothStep(0f, 1f, t));
                    c = Color.Lerp(c, Color.white, radial * 0.16f);
                    px[y * S + x] = c;
                }

            // Soft sun halo behind character head
            FillCircle(px, S, 32, 36, 20, Color.Lerp(arch.BgTop, Color.white, 0.28f), 0.35f);

            // 2. Voxel shoulders, tunic & cape/scarf (rows 4..21)
            FillRect(px, S, 12, 4, 40, 16, Darken(arch.Outfit, 0.84f));
            FillRect(px, S, 15, 4, 34, 15, arch.Outfit);
            // Center collar / scarf / medallion
            FillRect(px, S, 24, 12, 16, 8, arch.Trim);
            FillRect(px, S, 28, 7, 8, 6, Lighten(arch.Trim, 0.25f));

            // 3. Neck & Voxel Head (rows 20..48, cols 17..47)
            FillRect(px, S, 26, 18, 12, 5, Darken(arch.Skin, 0.88f));
            FillRect(px, S, 17, 21, 30, 27, arch.Skin);
            // Subtle voxel cheek shading & warm blush
            FillRect(px, S, 17, 21, 3, 25, Darken(arch.Skin, 0.91f));
            FillRect(px, S, 44, 21, 3, 25, Darken(arch.Skin, 0.91f));

            if (key == "astronaut")
            {
                // Sleek white-gold space helmet + solar reflective visor!
                FillRect(px, S, 15, 19, 34, 31, Hex(0xF1F5F9));
                FillRect(px, S, 17, 21, 30, 27, Hex(0xE2E8F0));
                FillRect(px, S, 19, 25, 26, 17, Hex(0xD97706));
                FillRect(px, S, 20, 27, 24, 13, Hex(0xFBBF24));
                FillRect(px, S, 22, 34, 10, 4, Hex(0xFEF08A));
                FillRect(px, S, 28, 49, 8, 4, arch.Trim);
            }
            else if (key == "knight")
            {
                // Silver-gold royal helmet with T-visor & azure crest!
                FillRect(px, S, 16, 20, 32, 29, Hex(0xCBD5E1));
                FillRect(px, S, 18, 22, 28, 25, Hex(0x94A3B8));
                FillRect(px, S, 20, 29, 24, 7, Hex(0x1E293B));
                FillRect(px, S, 30, 22, 4, 15, Hex(0x1E293B));
                FillRect(px, S, 23, 31, 5, 3, arch.Eyes);
                FillRect(px, S, 36, 31, 5, 3, arch.Eyes);
                FillRect(px, S, 16, 37, 32, 3, arch.Trim);
                FillRect(px, S, 28, 49, 8, 9, Hex(0x3B82F6));
            }
            else if (key == "fox" || key == "cat")
            {
                // Cute voxel animal folk: pointy ears, muzzle, bright eyes & whiskers!
                Color fur = arch.Hair;
                FillRect(px, S, 17, 21, 30, 26, fur);
                FillRect(px, S, 21, 21, 22, 14, arch.Skin); // cream muzzle & cheeks
                // Pointy left & right ears
                FillRect(px, S, 17, 47, 9, 9, fur);
                FillRect(px, S, 19, 48, 5, 6, Hex(0xFDA4AF));
                FillRect(px, S, 38, 47, 9, 9, fur);
                FillRect(px, S, 40, 48, 5, 6, Hex(0xFDA4AF));
                // Eyes + nose
                DrawEyes(px, S, arch.Eyes);
                FillRect(px, S, 30, 27, 4, 3, Hex(0x1F2937));
                if (key == "cat")
                {
                    FillRect(px, S, 15, 27, 5, 1, Hex(0xFFFBEB));
                    FillRect(px, S, 15, 25, 5, 1, Hex(0xFFFBEB));
                    FillRect(px, S, 44, 27, 5, 1, Hex(0xFFFBEB));
                    FillRect(px, S, 44, 25, 5, 1, Hex(0xFFFBEB));
                    FillCircle(px, S, 32, 14, 4, Hex(0xFACC15), 1f); // Golden bell!
                }
            }
            else
            {
                // Expressive eyes, warm blush, smile & archetype hairstyle/headgear!
                FillRect(px, S, 19, 27, 4, 2, new Color(0.96f, 0.52f, 0.56f, 0.65f));
                FillRect(px, S, 41, 27, 4, 2, new Color(0.96f, 0.52f, 0.56f, 0.65f));
                DrawEyes(px, S, arch.Eyes);
                // Friendly smile
                FillRect(px, S, 28, 24, 8, 2, Darken(arch.Skin, 0.62f));
                FillRect(px, S, 27, 25, 2, 1, Darken(arch.Skin, 0.68f));
                FillRect(px, S, 35, 25, 2, 1, Darken(arch.Skin, 0.68f));

                // Hair crown & side locks
                FillRect(px, S, 16, 41, 32, 9, arch.Hair);
                FillRect(px, S, 15, 32, 4, 14, arch.Hair);
                FillRect(px, S, 45, 32, 4, 14, arch.Hair);
                FillRect(px, S, 20, 39, 10, 3, arch.Hair);

                if (key == "sakura")
                {
                    // Blossom floral crown
                    FillRect(px, S, 18, 46, 6, 5, Hex(0xFB7185));
                    FillRect(px, S, 29, 47, 6, 5, Hex(0xFFF1F2));
                    FillRect(px, S, 40, 46, 6, 5, Hex(0xFB7185));
                }
                else if (key == "skycaptain" || key == "alchemist")
                {
                    // Brass / Aether goggles on forehead!
                    FillRect(px, S, 17, 40, 30, 5, Darken(arch.Trim, 0.72f));
                    FillRect(px, S, 20, 39, 9, 7, arch.Trim);
                    FillRect(px, S, 35, 39, 9, 7, arch.Trim);
                    FillRect(px, S, 22, 41, 5, 3, Hex(0xBAE6FD));
                    FillRect(px, S, 37, 41, 5, 3, Hex(0xBAE6FD));
                }
                else if (key == "monarch")
                {
                    // Jeweled golden crown!
                    FillRect(px, S, 18, 47, 28, 6, Hex(0xFACC15));
                    FillRect(px, S, 18, 53, 4, 4, Hex(0xFDE047));
                    FillRect(px, S, 30, 53, 4, 5, Hex(0xFDE047));
                    FillRect(px, S, 42, 53, 4, 4, Hex(0xFDE047));
                    FillRect(px, S, 31, 49, 2, 3, Hex(0xEF4444));
                }
                else if (key == "druid")
                {
                    // Sunlit antlers & river crystal!
                    FillRect(px, S, 14, 47, 4, 9, Hex(0xA16207));
                    FillRect(px, S, 11, 52, 4, 4, Hex(0x4ADE80));
                    FillRect(px, S, 46, 47, 4, 9, Hex(0xA16207));
                    FillRect(px, S, 49, 52, 4, 4, Hex(0x4ADE80));
                    FillRect(px, S, 30, 44, 4, 4, arch.Trim);
                }
                else if (key == "smith")
                {
                    // Rugged beard + ember headband
                    FillRect(px, S, 19, 20, 26, 6, arch.Hair);
                    FillRect(px, S, 23, 17, 18, 4, arch.Hair);
                    FillRect(px, S, 16, 42, 32, 4, arch.Trim);
                }
                else if (key == "shadow")
                {
                    // Deep mystic hood
                    FillRect(px, S, 14, 22, 5, 28, arch.Outfit);
                    FillRect(px, S, 45, 22, 5, 28, arch.Outfit);
                    FillRect(px, S, 15, 44, 34, 9, arch.Outfit);
                    FillRect(px, S, 30, 44, 4, 4, arch.Trim);
                }
                else // pioneer
                {
                    // Sunlit explorer bandanna crest
                    FillRect(px, S, 16, 42, 32, 3, arch.Trim);
                }
            }

            // 4. Crisp outer rounded border
            DrawRoundedBorder(px, S, 7, Color.Lerp(arch.Trim, Color.white, 0.32f));

            tex.SetPixels32(px);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "AvatarSprite_" + key;
            sprite.hideFlags = HideFlags.DontSave;
            cache[key] = sprite;
            return sprite;
        }

        static void DrawEyes(Color32[] px, int S, Color iris)
        {
            FillRect(px, S, 21, 30, 6, 6, Color.white);
            FillRect(px, S, 37, 30, 6, 6, Color.white);
            FillRect(px, S, 23, 30, 4, 5, iris);
            FillRect(px, S, 37, 30, 4, 5, iris);
            FillRect(px, S, 24, 31, 2, 3, Hex(0x0F172A));
            FillRect(px, S, 38, 31, 2, 3, Hex(0x0F172A));
            // Specular catchlight!
            FillRect(px, S, 22, 34, 2, 2, Color.white);
            FillRect(px, S, 37, 34, 2, 2, Color.white);
        }

        static bool CornerClip(int x, int y, int s, int r)
        {
            int dx = x < r ? r - 1 - x : (x >= s - r ? x - (s - r) : 0);
            int dy = y < r ? r - 1 - y : (y >= s - r ? y - (s - r) : 0);
            return dx > 0 && dy > 0 && (dx * dx + dy * dy) > r * r;
        }

        static void FillRect(Color32[] px, int s, int x0, int y0, int w, int h, Color c)
        {
            Color32 c32 = c;
            int x1 = Mathf.Min(s, x0 + w), y1 = Mathf.Min(s, y0 + h);
            for (int y = Mathf.Max(0, y0); y < y1; y++)
                for (int x = Mathf.Max(0, x0); x < x1; x++)
                    if (!CornerClip(x, y, s, 7))
                        px[y * s + x] = c32;
        }

        static void FillCircle(Color32[] px, int s, int cx, int cy, int r, Color c, float alpha)
        {
            for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(s - 1, cy + r); y++)
                for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(s - 1, cx + r); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > r || CornerClip(x, y, s, 7)) continue;
                    Color cur = px[y * s + x];
                    px[y * s + x] = Color.Lerp(cur, c, alpha * (1f - d / (r + 1f)));
                }
        }

        static void DrawRoundedBorder(Color32[] px, int s, int r, Color border)
        {
            Color32 b32 = border;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    if (CornerClip(x, y, s, r)) continue;
                    bool edge = x <= 1 || x >= s - 2 || y <= 1 || y >= s - 2
                             || CornerClip(x - 2, y, s, r) || CornerClip(x + 2, y, s, r)
                             || CornerClip(x, y - 2, s, r) || CornerClip(x, y + 2, s, r);
                    if (edge) px[y * s + x] = b32;
                }
        }

        static Color Hex(uint rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        static Color Darken(Color c, float factor) => new Color(c.r * factor, c.g * factor, c.b * factor, 1f);
        static Color Lighten(Color c, float amount) => Color.Lerp(c, Color.white, amount);
    }
}
