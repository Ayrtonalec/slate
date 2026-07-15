// SLATE — shared terrain sampling. Every visual system (mesh, buildings,
// villagers, camera) asks this class where the ground is, so they always
// agree. Purely visual: adds painterly detail noise on top of the sim's
// heightfield, never feeds back into the sim.
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public static class TerrainSampler
    {
        public const float CellSize = 8f;      // world units per sim cell
        public const float HeightScale = 150f; // land relief
        public const float DepthScale = 70f;   // sea floor drop

        private static World _w;
        private static uint _hseed;

        public static void Bind(World w)
        {
            _w = w;
            _hseed = SlateRng.HashStr(w.Seed.ToString() + "/visual");
        }

        public static World BoundWorld => _w;
        public static float WorldWidth => _w.W * CellSize;
        public static float WorldDepth => _w.H * CellSize;

        public static Vector3 CellToWorld(double cx, double cy, float y = 0)
            => new Vector3((float)((cx + 0.5) * CellSize), y, (float)((cy + 0.5) * CellSize));

        public static (int x, int y) WorldToCell(Vector3 p)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(p.x / CellSize), 0, _w.W - 1);
            int cy = Mathf.Clamp(Mathf.FloorToInt(p.z / CellSize), 0, _w.H - 1);
            return (cx, cy);
        }

        private static float Cell01(int x, int y)
        {
            x = Mathf.Clamp(x, 0, _w.W - 1);
            y = Mathf.Clamp(y, 0, _w.H - 1);
            return _w.HeightMap[y * _w.W + x];
        }

        // Bilinear sim height (0..1) at a world position; cell centers are the knots.
        public static float Height01(float wx, float wz)
        {
            float gx = wx / CellSize - 0.5f;
            float gz = wz / CellSize - 0.5f;
            int x0 = Mathf.FloorToInt(gx), z0 = Mathf.FloorToInt(gz);
            float fx = gx - x0, fz = gz - z0;
            fx = fx * fx * (3f - 2f * fx);
            fz = fz * fz * (3f - 2f * fz);
            float a = Cell01(x0, z0), b = Cell01(x0 + 1, z0);
            float c = Cell01(x0, z0 + 1), d = Cell01(x0 + 1, z0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }

        private static float CellTemp(int x, int y)
        {
            x = Mathf.Clamp(x, 0, _w.W - 1);
            y = Mathf.Clamp(y, 0, _w.H - 1);
            return _w.Temp[y * _w.W + x];
        }

        // Bilinear temperature — smooth snowlines instead of blocky cell edges.
        public static float Temp01(float wx, float wz)
        {
            float gx = wx / CellSize - 0.5f;
            float gz = wz / CellSize - 0.5f;
            int x0 = Mathf.FloorToInt(gx), z0 = Mathf.FloorToInt(gz);
            float fx = Mathf.Clamp01(gx - x0), fz = Mathf.Clamp01(gz - z0);
            float a = CellTemp(x0, z0), b = CellTemp(x0 + 1, z0);
            float c = CellTemp(x0, z0 + 1), d = CellTemp(x0 + 1, z0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }

        // Cheap 3-octave value noise for visual relief detail.
        private static float DetailNoise(float wx, float wz)
        {
            float sum = 0f, amp = 1f, freq = 0.055f, norm = 0f;
            for (int o = 0; o < 3; o++)
            {
                float x = wx * freq, z = wz * freq;
                int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
                float fx = x - ix, fz = z - iz;
                fx = fx * fx * (3f - 2f * fx);
                fz = fz * fz * (3f - 2f * fz);
                float a = (float)SlateRng.Hash2(ix, iz, _hseed + (uint)o);
                float b = (float)SlateRng.Hash2(ix + 1, iz, _hseed + (uint)o);
                float c = (float)SlateRng.Hash2(ix, iz + 1, _hseed + (uint)o);
                float d = (float)SlateRng.Hash2(ix + 1, iz + 1, _hseed + (uint)o);
                sum += amp * Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
                norm += amp;
                amp *= 0.5f; freq *= 2.1f;
            }
            return sum / norm - 0.5f;
        }

        // Ground height in world units. Water surface sits at y = 0.
        public static float GroundY(float wx, float wz)
        {
            float rel = Height01(wx, wz) - (float)_w.Sea;
            if (rel > 0f)
            {
                // Detail fades in away from the coast so the waterline stays clean;
                // mountains get extra ruggedness.
                float coastFade = Mathf.Clamp01(rel * 14f);
                float rugged = 1f + Mathf.Clamp01((rel - 0.22f) * 6f) * 2.2f;
                return rel * HeightScale + DetailNoise(wx, wz) * 2.0f * coastFade * rugged;
            }
            return rel * DepthScale;
        }
    }
}
