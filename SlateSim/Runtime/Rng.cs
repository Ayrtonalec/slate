// SLATE — deterministic seeded RNG streams. Port of god-sim-prototype/src/rng.js.
// Every system pulls from its own labeled stream so systems can be added or
// removed without reshuffling everyone else's randomness. All integer math is
// wrapped 32-bit to match the JS prototype bit-for-bit.
using System;
using System.Collections.Generic;

namespace Slate.Sim
{
    public static class SlateRng
    {
        // FNV-1a, identical to the JS hashStr (Math.imul == wrapped 32-bit multiply).
        public static uint HashStr(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619u;
                }
                return h;
            }
        }

        // Stateless position hash in [0,1) — for per-cell jitter.
        public static double Hash2(int x, int y, uint seed)
        {
            unchecked
            {
                int n = (x * 374761393) ^ (y * 668265263) ^ (int)seed;
                n = (n ^ (int)((uint)n >> 13)) * 1274126177;
                uint r = (uint)(n ^ (int)((uint)n >> 16));
                return r / 4294967296.0;
            }
        }
    }

    // mulberry32 stream, keyed "<seed>/<label>".
    public sealed class Rng
    {
        private int _a;

        public Rng(int seed, string label)
        {
            _a = unchecked((int)SlateRng.HashStr(seed.ToString() + "/" + label));
        }

        public double Next()
        {
            unchecked
            {
                _a += 0x6D2B79F5;
                int t = (_a ^ (int)((uint)_a >> 15)) * (1 | _a);
                t = (t + ((t ^ (int)((uint)t >> 7)) * (61 | t))) ^ t;
                uint r = (uint)(t ^ (int)((uint)t >> 14));
                return r / 4294967296.0;
            }
        }

        public double Range(double a, double b) => a + Next() * (b - a);

        public int Int(int a, int b) => a + (int)Math.Floor(Next() * (b - a + 1));

        public bool Chance(double p) => Next() < p;

        public T Pick<T>(IReadOnlyList<T> list) => list[(int)Math.Floor(Next() * list.Count)];

        public List<T> Shuffle<T>(IReadOnlyList<T> list)
        {
            var a = new List<T>(list);
            for (int i = a.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(Next() * (i + 1));
                (a[i], a[j]) = (a[j], a[i]);
            }
            return a;
        }
    }
}
