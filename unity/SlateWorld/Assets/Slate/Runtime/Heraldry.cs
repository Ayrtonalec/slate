// SLATE — procedural heraldry: every settlement bears its own arms, built
// deterministically from the world seed and its id, in its culture's
// tinctures. Divisions (per pale, per fess, quarterly, chevron), simple
// charges (roundel, cross, star, bar). Banners, army standards and — later —
// the D&D export all draw from here. Pure code; no two towns match.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public static class Heraldry
    {
        private static readonly Dictionary<long, Texture2D> Cache = new Dictionary<long, Texture2D>();

        public static void ClearCache() => Cache.Clear();

        // Culture tincture palettes: field color pairs + a metal.
        private static Color[] Tinctures(int culture)
        {
            Color metalGold = new Color(0.85f, 0.70f, 0.30f);
            Color metalWhite = new Color(0.90f, 0.88f, 0.80f);
            switch (culture)
            {
                case 0: return new[] { new Color(0.33f, 0.42f, 0.22f), new Color(0.20f, 0.26f, 0.16f), metalGold, metalWhite };
                case 1: return new[] { new Color(0.24f, 0.33f, 0.42f), new Color(0.14f, 0.17f, 0.22f), metalWhite, new Color(0.55f, 0.20f, 0.16f) };
                case 2: return new[] { new Color(0.62f, 0.38f, 0.16f), new Color(0.30f, 0.18f, 0.10f), metalGold, metalWhite };
                default: return new[] { new Color(0.40f, 0.25f, 0.34f), new Color(0.22f, 0.14f, 0.20f), metalGold, metalWhite };
            }
        }

        public static Texture2D Arms(int worldSeed, int settlementId, int culture)
        {
            long key = (long)worldSeed * 100000 + settlementId;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            uint h = SlateRng.HashStr(worldSeed + "/arms/" + settlementId);
            var tinct = Tinctures(culture);
            Color field = tinct[h % 2];
            Color second = tinct[(h >> 2) % 4];
            if (Same(second, field)) second = tinct[2];
            Color charge = tinct[2 + (int)((h >> 4) % 2)];
            if (Same(charge, field) || Same(charge, second)) charge = tinct[3];

            int division = (int)((h >> 6) % 5);   // 0 solid, 1 pale, 2 fess, 3 quarterly, 4 chevron
            int emblem = (int)((h >> 9) % 5);     // 0 none, 1 roundel, 2 cross, 3 star, 4 bar

            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "arms-" + settlementId,
            };

            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                    Color c = field;
                    switch (division)
                    {
                        case 1: if (u > 0.5f) c = second; break;
                        case 2: if (v > 0.5f) c = second; break;
                        case 3: if ((u > 0.5f) != (v > 0.5f)) c = second; break;
                        case 4: if (v < Mathf.Abs(u - 0.5f) * 1.6f + 0.18f) c = second; break;
                    }

                    switch (emblem)
                    {
                        case 1: // roundel
                            if ((u - 0.5f) * (u - 0.5f) + (v - 0.55f) * (v - 0.55f) < 0.030f) c = charge;
                            break;
                        case 2: // cross
                            if (Mathf.Abs(u - 0.5f) < 0.09f || Mathf.Abs(v - 0.52f) < 0.09f) c = charge;
                            break;
                        case 3: // four-point star
                            if (Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.55f) < 0.17f
                                && Mathf.Min(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.55f)) < 0.055f) c = charge;
                            break;
                        case 4: // bend bar
                            if (Mathf.Abs((u - 0.5f) + (v - 0.5f)) < 0.10f) c = charge;
                            break;
                    }

                    // Woven-cloth shading so the flag doesn't read as plastic.
                    float weave = ((x + y) & 1) == 0 ? 1.0f : 0.94f;
                    t.SetPixel(x, y, new Color(c.r * weave, c.g * weave, c.b * weave, 1f));
                }
            t.Apply();
            Cache[key] = t;
            return t;
        }

        private static bool Same(Color a, Color b)
            => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.05f;
    }
}
