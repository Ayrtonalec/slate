// SLATE — procedural, seamlessly-tiling terrain detail textures. These give
// the ground per-pixel material texture (grass blades, rock creases, snow
// grain) instead of vertex-color blur — the difference between "pixelated
// map" and ground that reads as real at every zoom. Grayscale around 1.0 so
// they multiply with the macro biome tint without shifting its color.
using UnityEngine;

namespace Slate.Game
{
    public static class TerrainTextures
    {
        private static Texture2D _grass, _rock, _snow;

        // Periodic (wrapping) value noise -> guaranteed seamless tiles.
        private static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            float v = Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f;
            return Mathf.Abs(v) % 1f;
        }

        private static float Noise(float x, float y, int period, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(
                Mathf.Lerp(Hash(ix, iy, period, seed), Hash(ix + 1, iy, period, seed), fx),
                Mathf.Lerp(Hash(ix, iy + 1, period, seed), Hash(ix + 1, iy + 1, period, seed), fx), fy);
        }

        // Octaves of periodic noise; base period doubles per octave so every
        // octave still wraps at the texture border.
        private static float Fbm(float u, float v, int basePeriod, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Noise(u * period, v * period, period, seed + o * 131);
                norm += amp;
                amp *= gain;
                period *= 2;
            }
            return sum / norm;
        }

        private static Texture2D Bake(string name, System.Func<float, float, float> lum)
        {
            const int S = 256;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { name = name };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                    float l = Mathf.Clamp(lum(u, v), 0.3f, 1.6f);
                    t.SetPixel(x, y, new Color(l, l, l, 1f));
                }
            t.Apply(true);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
            return t;
        }

        // Meadow grass: fine anisotropic blade noise over soft patchiness.
        public static Texture2D Grass()
        {
            if (_grass != null) return _grass;
            _grass = Bake("slate-grass", (u, v) =>
            {
                float patch = Fbm(u, v, 6, 3, 11);                       // soft light/dark patches
                float tufts = Fbm(u, v, 32, 2, 23);                      // isotropic tuft grain (no striping)
                float fleck = Noise(u * 96, v * 96, 96, 37);             // pinpoint variation
                return 0.80f + (patch - 0.5f) * 0.42f + (tufts - 0.5f) * 0.28f + (fleck - 0.5f) * 0.16f;
            });
            return _grass;
        }

        // Rock: ridged fBm — sharp creases and faces, strong contrast.
        public static Texture2D Rock()
        {
            if (_rock != null) return _rock;
            _rock = Bake("slate-rock", (u, v) =>
            {
                float r1 = 1f - Mathf.Abs(2f * Fbm(u, v, 5, 4, 51) - 1f);
                float r2 = 1f - Mathf.Abs(2f * Fbm(u + 0.31f, v + 0.7f, 11, 3, 67) - 1f);
                float ridge = Mathf.Pow(r1 * 0.65f + r2 * 0.35f, 1.7f);
                float grain = Noise(u * 128, v * 128, 128, 73);
                return 0.52f + ridge * 0.75f + (grain - 0.5f) * 0.12f;
            });
            return _rock;
        }

        // Snow: soft drifts with a faint sparkle.
        public static Texture2D Snow()
        {
            if (_snow != null) return _snow;
            _snow = Bake("slate-snow", (u, v) =>
            {
                float drift = Fbm(u, v, 5, 3, 91);
                float sparkle = Noise(u * 128, v * 128, 128, 97) > 0.985f ? 0.35f : 0f;
                return 0.92f + (drift - 0.5f) * 0.22f + sparkle;
            });
            return _snow;
        }
    }
}
