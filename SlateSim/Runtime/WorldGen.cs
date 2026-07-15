// SLATE — deterministic world generation.
// Port of god-sim-prototype/src/worldgen.js. Produces the geography a run
// lives on: continents, climate, biomes, rivers, fertility, resources, and
// named regions. Same seed -> same world, always.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Slate.Sim
{
    public static class WorldGen
    {
        public const int W = 192, H = 120;

        private sealed class ValueNoise
        {
            private readonly int _seed;
            public ValueNoise(uint seed) { _seed = unchecked((int)seed); }

            private double Rnd(int ix, int iy)
            {
                unchecked
                {
                    int n = (ix * 127413) ^ (iy * 335545) ^ _seed;
                    n = (n ^ (int)((uint)n >> 13)) * 1274126177;
                    uint r = (uint)(n ^ (int)((uint)n >> 16));
                    return r / 4294967296.0;
                }
            }

            public double Sample(double x, double y)
            {
                int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
                double fx = x - ix, fy = y - iy;
                double a = Rnd(ix, iy), b = Rnd(ix + 1, iy), c = Rnd(ix, iy + 1), d = Rnd(ix + 1, iy + 1);
                double u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
                return a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
            }
        }

        private sealed class Fbm
        {
            private readonly ValueNoise _n;
            private readonly int _oct;
            private readonly double _lac, _gain;
            public Fbm(ValueNoise n, int oct, double lac, double gain) { _n = n; _oct = oct; _lac = lac; _gain = gain; }

            public double Sample(double x, double y)
            {
                double s = 0, a = 1, f = 1, norm = 0;
                for (int i = 0; i < _oct; i++) { s += a * _n.Sample(x * f, y * f); norm += a; a *= _gain; f *= _lac; }
                return s / norm;
            }
        }

        public static void Generate(int seed, World w)
        {
            uint hseed = SlateRng.HashStr(seed.ToString());
            var nBase = new Fbm(new ValueNoise(hseed ^ 0x1111), 5, 2.0, 0.5);
            var nWarp = new Fbm(new ValueNoise(hseed ^ 0x2222), 4, 2.0, 0.5);
            var nRidge = new Fbm(new ValueNoise(hseed ^ 0x3333), 4, 2.1, 0.5);
            var nMoist = new Fbm(new ValueNoise(hseed ^ 0x4444), 4, 2.0, 0.5);
            var nJit = new ValueNoise(hseed ^ 0x5555);
            var rng = new Rng(seed, "worldgen");
            var namer = new Namer(seed);

            const int N = W * H;
            var height = new float[N];
            var temp = new float[N];
            var moist = new float[N];
            var fertBase = new float[N];
            var fert = new float[N];
            var biome = new byte[N];
            var river = new byte[N];
            var resource = new byte[N];
            var seaDist = new byte[N];
            var landDist = new byte[N];
            var regionOf = new short[N];
            for (int i = 0; i < N; i++) regionOf[i] = -1;
            int Idx(int x, int y) => y * W + x;

            // --- Height field: warped fBm + ridged component, ocean frame at map edges.
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    double u = (double)x / W * 7, v = (double)y / H * 4.4;
                    double wx = u + (nWarp.Sample(u * 0.9 + 40, v * 0.9) - 0.5) * 1.6;
                    double wy = v + (nWarp.Sample(u * 0.9, v * 0.9 + 40) - 0.5) * 1.6;
                    double h = nBase.Sample(wx, wy) * 0.72;
                    double r = nRidge.Sample(u * 1.15 + 9, v * 1.15 + 9);
                    double ridge = Math.Pow(1 - Math.Abs(2 * r - 1), 2.2);
                    h += ridge * 0.34;
                    double ex = Math.Min(x, W - 1 - x) / 14.0, ey = Math.Min(y, H - 1 - y) / 12.0;
                    double edge = Math.Min(1, Math.Min(ex, ey));
                    h *= 0.35 + 0.65 * (edge < 1 ? edge * edge * (3 - 2 * edge) : 1);
                    height[Idx(x, y)] = (float)h;
                }
            }

            // --- Sea level: binary search to a fixed land fraction (keeps every seed usable).
            const double LAND_TARGET = 0.36;
            double lo = 0.1, hi = 0.9, sea = 0.5;
            for (int it = 0; it < 22; it++)
            {
                sea = (lo + hi) / 2;
                int land = 0;
                for (int i = 0; i < N; i++) if (height[i] > sea) land++;
                if ((double)land / N > LAND_TARGET) lo = sea; else hi = sea;
            }
            bool IsWater(int i) => height[i] <= sea;

            // --- Distance fields (BFS), for climate, shallows and depth shading.
            {
                var q = new List<int>();
                for (int i = 0; i < N; i++) { seaDist[i] = 255; landDist[i] = 255; }
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int i = Idx(x, y);
                        if (IsWater(i))
                        {
                            bool coast = false;
                            if (x > 0 && !IsWater(i - 1)) coast = true;
                            if (x < W - 1 && !IsWater(i + 1)) coast = true;
                            if (y > 0 && !IsWater(i - W)) coast = true;
                            if (y < H - 1 && !IsWater(i + W)) coast = true;
                            landDist[i] = coast ? (byte)1 : (byte)255;
                            if (coast) q.Add(i);
                        }
                        else
                        {
                            bool shore = false;
                            if (x > 0 && IsWater(i - 1)) shore = true;
                            if (x < W - 1 && IsWater(i + 1)) shore = true;
                            if (y > 0 && IsWater(i - W)) shore = true;
                            if (y < H - 1 && IsWater(i + W)) shore = true;
                            seaDist[i] = shore ? (byte)1 : (byte)255;
                            if (shore) q.Add(i);
                        }
                    }
                int head = 0;
                while (head < q.Count)
                {
                    int i = q[head++];
                    int x = i % W, y = i / W;
                    var field = IsWater(i) ? landDist : seaDist;
                    byte d = field[i];
                    if (d >= 30) continue;
                    void Push(int j)
                    {
                        if (IsWater(j) != IsWater(i)) return;
                        var f = IsWater(j) ? landDist : seaDist;
                        if (f[j] > d + 1) { f[j] = (byte)(d + 1); q.Add(j); }
                    }
                    if (x > 0) Push(i - 1);
                    if (x < W - 1) Push(i + 1);
                    if (y > 0) Push(i - W);
                    if (y < H - 1) Push(i + W);
                }
            }

            // --- Climate.
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = Idx(x, y);
                    double lat = Math.Abs((double)y / H - 0.5) * 2; // 0 equator .. 1 poles
                    double t = 1 - lat * 1.05 - Math.Max(0, height[i] - sea) * 1.1 + (nJit.Sample(x * 0.3, y * 0.3) - 0.5) * 0.14;
                    temp[i] = (float)Math.Max(0, Math.Min(1, t));
                    double coastal = Math.Max(0, 1 - Math.Min(seaDist[i], (byte)30) / 26.0);
                    double m = nMoist.Sample((double)x / W * 5 + 3, (double)y / H * 3 + 3) * 0.78 + coastal * 0.3;
                    moist[i] = (float)Math.Max(0, Math.Min(1, m));
                }
            }

            // --- Rivers: springs in high wet ground, descend to the sea.
            var riverPaths = new List<RiverPath>();
            {
                var springs = new List<(int X, int Y, double Hh)>();
                for (int y = 2; y < H - 2; y++)
                    for (int x = 2; x < W - 2; x++)
                    {
                        int i = Idx(x, y);
                        if (!IsWater(i) && height[i] > sea + 0.16 && moist[i] > 0.42) springs.Add((x, y, height[i]));
                    }
                var sorted = springs.OrderByDescending(s => s.Hh).ToList(); // stable, like JS sort
                var chosen = new List<(int X, int Y, double Hh)>();
                foreach (var s in sorted)
                {
                    if (chosen.Count >= 30) break;
                    bool farEnough = true;
                    foreach (var c in chosen)
                    {
                        if ((c.X - s.X) * (c.X - s.X) + (c.Y - s.Y) * (c.Y - s.Y) < 49) { farEnough = false; break; }
                    }
                    if (farEnough) chosen.Add(s);
                }
                foreach (var (sx, sy, _) in chosen)
                {
                    int x = sx, y = sy;
                    var pts = new List<(int X, int Y)> { (x, y) };
                    var visited = new HashSet<int> { Idx(x, y) };
                    bool merged = false;
                    for (int step = 0; step < 500; step++)
                    {
                        int bx = -1, by = -1;
                        double bh = double.PositiveInfinity;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                                int j = Idx(nx, ny);
                                if (visited.Contains(j)) continue;
                                double hh = height[j] + (SlateRng.Hash2(nx, ny, hseed) - 0.5) * 0.02;
                                if (hh < bh) { bh = hh; bx = nx; by = ny; }
                            }
                        if (bx < 0 || bh > height[Idx(x, y)] + 0.015) break; // stuck in a pit
                        x = bx; y = by;
                        int jj = Idx(x, y);
                        pts.Add((x, y));
                        if (IsWater(jj)) break;
                        if (river[jj] != 0) { river[jj] = (byte)Math.Min(3, river[jj] + 1); merged = true; break; }
                        visited.Add(jj);
                    }
                    if (pts.Count >= 7)
                    {
                        foreach (var (px, py) in pts)
                        {
                            int j = Idx(px, py);
                            if (!IsWater(j)) river[j] = Math.Max(river[j], (byte)1);
                        }
                        riverPaths.Add(new RiverPath { Pts = pts, Flow = merged ? 2 : 1 });
                    }
                }
            }

            // --- Biomes.
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = Idx(x, y);
                    if (IsWater(i)) { biome[i] = landDist[i] <= 2 ? B.SHALLOW : B.OCEAN; continue; }
                    double h = height[i], t = temp[i], m = moist[i];
                    if (h > sea + 0.30) biome[i] = B.MOUNTAIN;
                    else if (t < 0.18) biome[i] = B.SNOW;
                    else if (h > sea + 0.20) biome[i] = B.HILLS;
                    else if (t > 0.72 && m < 0.34) biome[i] = B.DESERT;
                    else if (m > 0.78 && h < sea + 0.08 && t > 0.3) biome[i] = B.MARSH;
                    else if (m > 0.5 && t > 0.26) biome[i] = B.FOREST;
                    else biome[i] = B.PLAINS;
                }
            }

            // --- Fertility & natural resources.
            double FertByBiome(byte b)
            {
                switch (b)
                {
                    case B.PLAINS: return 0.75;
                    case B.FOREST: return 0.55;
                    case B.HILLS: return 0.42;
                    case B.MARSH: return 0.5;
                    case B.DESERT: return 0.07;
                    case B.SNOW: return 0.05;
                    case B.MOUNTAIN: return 0.1;
                    default: return 0.3;
                }
            }
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = Idx(x, y);
                    if (IsWater(i)) { fertBase[i] = 0; continue; }
                    double f = FertByBiome(biome[i]);
                    bool nearRiver = river[i] != 0;
                    if (!nearRiver)
                    {
                        if (x > 0 && river[i - 1] != 0) nearRiver = true;
                        else if (x < W - 1 && river[i + 1] != 0) nearRiver = true;
                        else if (y > 0 && river[i - W] != 0) nearRiver = true;
                        else if (y < H - 1 && river[i + W] != 0) nearRiver = true;
                    }
                    if (nearRiver) f += 0.28;
                    f += (SlateRng.Hash2(x, y, hseed ^ 0x77) - 0.5) * 0.14;
                    fertBase[i] = (float)Math.Max(0, Math.Min(1.2, f));
                    if (seaDist[i] == 1 && biome[i] != B.SNOW && SlateRng.Hash2(x, y, hseed ^ 0x88) < 0.16)
                        resource[i] = 2; // fishing grounds
                }
            }
            Array.Copy(fertBase, fert, N);

            // --- Hidden natural gold veins.
            var veins = new List<Vein>();
            {
                var cand = new List<(int X, int Y)>();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int i = Idx(x, y);
                        if (biome[i] == B.MOUNTAIN || biome[i] == B.HILLS) cand.Add((x, y));
                    }
                var shuffled = rng.Shuffle(cand);
                foreach (var (x, y) in shuffled)
                {
                    if (veins.Count >= 10) break;
                    bool farEnough = true;
                    foreach (var v in veins)
                    {
                        if ((v.X - x) * (v.X - x) + (v.Y - y) * (v.Y - y) < 100) { farEnough = false; break; }
                    }
                    if (farEnough) veins.Add(new Vein { X = x, Y = y, Revealed = false, Divine = false });
                }
            }

            // --- Named regions (flood fills).
            var regions = new List<Region>();
            {
                var seen = new byte[N];
                List<int> Flood(int start, Func<int, bool> match)
                {
                    var cells = new List<int> { start };
                    seen[start] = 1;
                    for (int h2 = 0; h2 < cells.Count; h2++)
                    {
                        int i = cells[h2];
                        int x = i % W, y = i / W;
                        void TryPush(int j) { if (seen[j] == 0 && match(j)) { seen[j] = 1; cells.Add(j); } }
                        if (x > 0) TryPush(i - 1);
                        if (x < W - 1) TryPush(i + 1);
                        if (y > 0) TryPush(i - W);
                        if (y < H - 1) TryPush(i + W);
                    }
                    return cells;
                }
                void RegisterRegion(List<int> cells, string kind)
                {
                    double cx = 0, cy = 0;
                    foreach (int i in cells) { cx += i % W; cy += i / W; }
                    cx /= cells.Count; cy /= cells.Count;
                    // Anchor the label on a member cell nearest the centroid.
                    int best = cells[0];
                    double bd = double.PositiveInfinity;
                    foreach (int i in cells)
                    {
                        double d = (i % W - cx) * (i % W - cx) + (i / W - cy) * (i / W - cy);
                        if (d < bd) { bd = d; best = i; }
                    }
                    int id = regions.Count;
                    foreach (int i in cells) regionOf[i] = (short)id;
                    regions.Add(new Region { Id = id, Kind = kind, Name = namer.Region(kind), Cx = best % W, Cy = best / W, Size = cells.Count });
                }
                string KindOf(byte b) => b == B.MOUNTAIN ? "range" : b == B.FOREST ? "forest" : b == B.DESERT ? "desert" : null;
                int MinSize(string kind) => kind == "range" ? 14 : kind == "forest" ? 40 : 50;
                for (int i = 0; i < N; i++)
                {
                    if (seen[i] != 0 || IsWater(i)) continue;
                    string kind = KindOf(biome[i]);
                    if (kind == null) { seen[i] = 1; continue; }
                    byte want = biome[i];
                    var cells = Flood(i, j => biome[j] == want);
                    if (cells.Count >= MinSize(kind)) RegisterRegion(cells, kind);
                }
                // Water bodies: the largest is the ocean; other big ones are seas.
                var bodies = new List<List<int>>();
                for (int i = 0; i < N; i++)
                {
                    if (seen[i] != 0 || !IsWater(i)) continue;
                    bodies.Add(Flood(i, IsWater));
                }
                var bodiesSorted = bodies.OrderByDescending(c => c.Count).ToList(); // stable, like JS sort
                for (int k = 0; k < bodiesSorted.Count; k++)
                {
                    if (k == 0) RegisterRegion(bodiesSorted[k], "ocean");
                    else if (bodiesSorted[k].Count >= 160) RegisterRegion(bodiesSorted[k], "sea");
                }
            }

            // --- Culture homelands: 4 fertile coastal sites, far apart.
            var homelands = new List<Homeland>();
            {
                var cand = new List<Homeland>();
                for (int y = 3; y < H - 3; y++)
                    for (int x = 3; x < W - 3; x++)
                    {
                        int i = Idx(x, y);
                        if (IsWater(i) || seaDist[i] > 2 || temp[i] < 0.24) continue;
                        double f = 0;
                        for (int dy = -2; dy <= 2; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                                f += fertBase[Idx(nx, ny)];
                            }
                        if (f > 8) cand.Add(new Homeland { X = x, Y = y, F = f });
                    }
                var top = cand.OrderByDescending(c => c.F).Take(400).ToList(); // stable, like JS sort
                if (top.Count > 0)
                {
                    homelands.Add(top[0]);
                    while (homelands.Count < 4 && top.Count > 0)
                    {
                        Homeland best = null;
                        double bestD = -1;
                        foreach (var c in top)
                        {
                            double dmin = double.PositiveInfinity;
                            foreach (var hpt in homelands)
                                dmin = Math.Min(dmin, (double)(hpt.X - c.X) * (hpt.X - c.X) + (double)(hpt.Y - c.Y) * (hpt.Y - c.Y));
                            double score = dmin + c.F * 4;
                            if (dmin > 120 && score > bestD) { bestD = score; best = c; }
                        }
                        if (best == null) break;
                        homelands.Add(best);
                    }
                }
            }

            w.Seed = seed;
            w.W = W; w.H = H; w.Sea = sea;
            w.HeightMap = height; w.Temp = temp; w.Moist = moist;
            w.FertBase = fertBase; w.Fert = fert;
            w.Biome = biome; w.River = river; w.Resource = resource;
            w.SeaDist = seaDist; w.LandDist = landDist;
            w.RegionOf = regionOf; w.Regions = regions;
            w.RiverPaths = riverPaths; w.Veins = veins; w.Homelands = homelands;
            w.Namer = namer;
        }
    }
}
