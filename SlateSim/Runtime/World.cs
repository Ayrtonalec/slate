// SLATE — world state container + shared queries.
// Port of god-sim-prototype/src/world.js. Wraps the generated geography with
// runtime state (settlements, storms, dragons, roads, chronicle) and the
// helper queries every system uses.
using System;
using System.Collections.Generic;

namespace Slate.Sim
{
    public static class B
    {
        public const byte OCEAN = 0, SHALLOW = 1, PLAINS = 2, FOREST = 3, HILLS = 4,
            MOUNTAIN = 5, DESERT = 6, SNOW = 7, MARSH = 8;
    }

    public sealed class Settlement
    {
        public int Id;
        public string Name;
        public int X, Y;
        public int Culture;
        public double Pop;
        public int Tier, MaxTier;
        public double Prosp = 1;
        public double Wealth;
        public bool Gold;
        public int BoomUntil;
        public int DeclineStreak, HungerStreak;
        public int FoundedYear;
        public int LastFamineYear = -99;
        public double Cap = 100;
        public bool Fish;
        public bool Ruined;
        public int RuinedYear;
    }

    public sealed class Ruin
    {
        public int X, Y;
        public string Name;
        public int Year;
        public int Tier;
        public string Cause;
        public string Alt; // alternate doom-name (Dragonfall, ...) or null
    }

    public sealed class Zone
    {
        public int X, Y;
        public double R;
        public int Start;
    }

    public sealed class Dragon
    {
        public string Name;
        public int X, Y;
        public int BornYear;
        public int LastRaid;
        public int RaidEvery;
        public string RegionName;
    }

    public sealed class Road
    {
        public int A, B;
        public List<(double X, double Y)> Pts;
    }

    public sealed class Region
    {
        public int Id;
        public string Kind; // range | forest | desert | sea | ocean
        public string Name;
        public int Cx, Cy;
        public int Size;
    }

    public sealed class Vein
    {
        public int X, Y;
        public bool Revealed;
        public bool Divine;
    }

    public sealed class RiverPath
    {
        public List<(int X, int Y)> Pts;
        public int Flow;
    }

    public sealed class Homeland
    {
        public int X, Y;
        public double F;
    }

    public sealed class Site
    {
        public int X, Y;
        public double Score;
    }

    // Render hints. The sim writes these; a renderer may read and clear them.
    // Nothing in the sim's history depends on when (or whether) they are cleared.
    public sealed class DirtyFlags
    {
        public bool Terrain = true, Features = true, Borders = true, Labels = true;
    }

    public sealed class World
    {
        public static readonly string[] Months =
        {
            "Thaw", "Seedtime", "Bloom", "Highsun", "Longsun", "Harvest",
            "Gleaning", "Mistfall", "Frostgate", "Deepwinter", "Icewane", "Stirring",
        };
        public static readonly string[] TierNames = { "hamlet", "village", "town", "city" };
        public static readonly int[] TierPop = { 0, 450, 1300, 3200 };
        private static readonly string[] RuinNames = { "Dragonfall", "Wyrmscar", "Ashmark", "Cinderrest" };

        // --- Geography (filled by WorldGen.Generate; float[] mirrors the JS Float32Arrays).
        public int Seed;
        public int W, H;
        public double Sea;
        public float[] HeightMap, Temp, Moist, FertBase, Fert;
        public byte[] Biome, River, Resource, SeaDist, LandDist;
        public short[] RegionOf;
        public List<Region> Regions;
        public List<RiverPath> RiverPaths;
        public List<Vein> Veins;
        public List<Homeland> Homelands;
        public Namer Namer;

        // --- Runtime state.
        public List<Settlement> Settlements = new List<Settlement>();
        public List<Ruin> Ruins = new List<Ruin>();
        public List<Zone> Storms = new List<Zone>();
        public List<Zone> Blesses = new List<Zone>();
        public List<Dragon> Dragons = new List<Dragon>();
        public List<(int X, int Y)> DeadLairs = new List<(int, int)>();
        public List<Road> Roads = new List<Road>();
        public Dictionary<int, int> RoadDeg = new Dictionary<int, int>();
        public short[] Claims;
        public short[] SettleGrid; // cell -> alive settlement id, or -1
        public Dictionary<int, Settlement> SettlementsById = new Dictionary<int, Settlement>();
        public int Tick, Month, Year;
        public Chronicle Chronicle;
        public Rng RngSim;
        public int NextSettlementId = 1;
        public int LeanYears;
        public bool FertDirty;
        public int ClaimsDirtyTick;
        public int DragonCooldownUntil;
        public DirtyFlags Dirty = new DirtyFlags();
        public string Title;
        public Action<ChronicleEvent> OnEvent;

        public int Idx(int x, int y) => y * W + x;
        public bool InB(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public bool IsWaterCell(int i) => HeightMap[i] <= Sea;
        public bool IsLandAt(int x, int y) => InB(x, y) && HeightMap[Idx(x, y)] > Sea;
        public string MonthName() => Months[Month];
        public string TierName(int t) => TierNames[t];
        public int TierOf(double pop) => pop >= TierPop[3] ? 3 : pop >= TierPop[2] ? 2 : pop >= TierPop[1] ? 1 : 0;

        public static World Create(int seed)
        {
            var w = new World();
            WorldGen.Generate(seed, w);
            w.Claims = new short[w.W * w.H];
            w.SettleGrid = new short[w.W * w.H];
            for (int i = 0; i < w.Claims.Length; i++) { w.Claims[i] = -1; w.SettleGrid[i] = -1; }
            w.Chronicle = new Chronicle();
            w.RngSim = new Rng(seed, "sim");
            w.Title = "Seed " + seed;

            // Founding landings: each culture puts two hamlets ashore near its homeland.
            for (int ci = 0; ci < w.Homelands.Count && ci < Cultures.All.Length; ci++)
            {
                var h = w.Homelands[ci];
                var first = w.BestSiteNear(h.X, h.Y, 0, 5);
                if (first == null) continue;
                var a = w.AddSettlement(first.X, first.Y, ci, w.RngSim.Int(90, 150));
                w.Chronicle.Add(w, "landing", new EvData { Culture = ci, Name = a.Name, X = a.X, Y = a.Y });
                var second = w.BestSiteNear(h.X, h.Y, 3, 8);
                if (second != null)
                {
                    var b2 = w.AddSettlement(second.X, second.Y, ci, w.RngSim.Int(60, 110));
                    w.Chronicle.Add(w, "found", new EvData { Name = b2.Name, Parent = a.Name, X = b2.X, Y = b2.Y });
                }
            }
            w.RecomputeClaims();
            return w;
        }

        public double FertAround(int x, int y, int r)
        {
            double f = 0;
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (InB(nx, ny)) f += Fert[Idx(nx, ny)];
                }
            return f;
        }

        public Zone ZoneAt(List<Zone> zones, int x, int y)
        {
            foreach (var z in zones)
            {
                double d2 = (double)(z.X - x) * (z.X - x) + (double)(z.Y - y) * (z.Y - y);
                if (d2 <= z.R * z.R) return z;
            }
            return null;
        }

        public Zone StormAt(int x, int y) => ZoneAt(Storms, x, y);
        public Zone BlessAt(int x, int y) => ZoneAt(Blesses, x, y);

        public void RecomputeFert()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = Idx(x, y);
                    double f = FertBase[i];
                    if (BlessAt(x, y) != null) f *= 1.7;
                    if (StormAt(x, y) != null) f *= 0.12;
                    Fert[i] = (float)f;
                }
            FertDirty = true;
            Dirty.Terrain = true;
        }

        public Settlement SettlementNear(int x, int y, double r)
        {
            Settlement best = null;
            double bd = (r + 0.01) * (r + 0.01);
            if (r <= 6.5)
            {
                // Small radius: scan the occupancy grid, not the settlement list.
                int R = (int)Math.Ceiling(r);
                for (int dy = -R; dy <= R; dy++)
                    for (int dx = -R; dx <= R; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (!InB(nx, ny)) continue;
                        int id = SettleGrid[Idx(nx, ny)];
                        if (id < 0) continue;
                        var s = SettlementsById[id];
                        double d2 = (double)(s.X - x) * (s.X - x) + (double)(s.Y - y) * (s.Y - y);
                        if (d2 < bd) { bd = d2; best = s; }
                    }
                return best;
            }
            foreach (var s in Settlements)
            {
                if (s.Ruined) continue;
                double d2 = (double)(s.X - x) * (s.X - x) + (double)(s.Y - y) * (s.Y - y);
                if (d2 < bd) { bd = d2; best = s; }
            }
            return best;
        }

        public bool HasFishAdj(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (InB(nx, ny) && Resource[Idx(nx, ny)] == 2) return true;
                }
            return false;
        }

        public Vein GoldVeinNear(int x, int y, double r, bool revealedOnly)
        {
            foreach (var v in Veins)
            {
                if (revealedOnly && !v.Revealed) continue;
                double d2 = (double)(v.X - x) * (v.X - x) + (double)(v.Y - y) * (v.Y - y);
                if (d2 <= r * r) return v;
            }
            return null;
        }

        public double SiteScore(int x, int y, List<Settlement> nearList)
        {
            if (!IsLandAt(x, y)) return double.NegativeInfinity;
            int i = Idx(x, y);
            byte b = Biome[i];
            if (b == B.MOUNTAIN || b == B.SNOW) return double.NegativeInfinity;
            if (nearList != null)
            {
                foreach (var s in nearList)
                {
                    double d2 = (double)(s.X - x) * (s.X - x) + (double)(s.Y - y) * (s.Y - y);
                    if (d2 <= 16) return double.NegativeInfinity; // 4 cell spacing
                }
            }
            else if (SettlementNear(x, y, 4) != null) return double.NegativeInfinity;
            if (StormAt(x, y) != null) return double.NegativeInfinity;
            double score = FertAround(x, y, 2) * 1.0;
            if (BlessAt(x, y) != null) score += 3; // the land feels kind
            if (River[i] != 0) score += 2.5;
            if (SeaDist[i] <= 1) score += 1.5;
            if (HasFishAdj(x, y)) score += 2;
            if (GoldVeinNear(x, y, 3, true) != null) score += 9;
            return score;
        }

        public Site BestSiteNear(int x, int y, int rmin, int rmax)
        {
            // Gather nearby settlements once; per-cell spacing checks use this short list.
            var nearList = new List<Settlement>();
            double R2 = (double)(rmax + 4) * (rmax + 4);
            foreach (var s in Settlements)
            {
                if (s.Ruined) continue;
                double d2 = (double)(s.X - x) * (s.X - x) + (double)(s.Y - y) * (s.Y - y);
                if (d2 <= R2) nearList.Add(s);
            }
            Site best = null;
            double bs = 8.5; // minimum livable score — marginal land stays wild
            for (int dy = -rmax; dy <= rmax; dy++)
                for (int dx = -rmax; dx <= rmax; dx++)
                {
                    int d2 = dx * dx + dy * dy;
                    if (d2 < rmin * rmin || d2 > rmax * rmax) continue;
                    int nx = x + dx, ny = y + dy;
                    if (!InB(nx, ny)) continue;
                    double sc = SiteScore(nx, ny, nearList) - Math.Sqrt(d2) * 0.15;
                    if (sc > bs) { bs = sc; best = new Site { X = nx, Y = ny, Score = sc }; }
                }
            return best;
        }

        public (int X, int Y)? CampSiteNear(int x, int y)
        {
            // Miners settle rough country: pick the best non-mountain cell beside
            // the vein; if the range is solid stone, they camp on the stone itself.
            (int X, int Y)? best = null, fallback = null;
            double bs = double.NegativeInfinity;
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!InB(nx, ny) || !IsLandAt(nx, ny)) continue;
                    if (SettlementNear(nx, ny, 2.5) != null || StormAt(nx, ny) != null) continue;
                    byte b = Biome[Idx(nx, ny)];
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (b == B.MOUNTAIN || b == B.SNOW)
                    {
                        if (fallback == null && d <= 2) fallback = (nx, ny);
                        continue;
                    }
                    double sc = FertAround(nx, ny, 1) - d * 0.3;
                    if (sc > bs) { bs = sc; best = (nx, ny); }
                }
            return best ?? fallback;
        }

        public (int X, int Y)? NearestMountain(int x, int y, int rmax)
        {
            (int X, int Y)? best = null;
            double bd = double.PositiveInfinity;
            for (int dy = -rmax; dy <= rmax; dy++)
                for (int dx = -rmax; dx <= rmax; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!InB(nx, ny) || Biome[Idx(nx, ny)] != B.MOUNTAIN) continue;
                    int d2 = dx * dx + dy * dy;
                    if (d2 < bd) { bd = d2; best = (nx, ny); }
                }
            return best;
        }

        public string RegionNameAt(int x, int y, string fallback)
        {
            int rid = RegionOf[Idx(x, y)];
            if (rid >= 0) return Regions[rid].Name;
            // Look a little around for a named region (labels feel better than "the wilds").
            for (int r = 1; r <= 4; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (!InB(nx, ny)) continue;
                        int rr = RegionOf[Idx(nx, ny)];
                        if (rr >= 0 && Regions[rr].Kind != "ocean" && Regions[rr].Kind != "sea") return Regions[rr].Name;
                    }
            }
            return fallback ?? "the wild country";
        }

        public Settlement AddSettlement(int x, int y, int culture, double pop, string name = null, bool gold = false)
        {
            var s = new Settlement
            {
                Id = NextSettlementId++,
                Name = name ?? Namer.Place(culture),
                X = x, Y = y, Culture = culture,
                Pop = pop, Tier = TierOf(pop), MaxTier = TierOf(pop),
                Gold = gold,
                FoundedYear = Year,
                Fish = HasFishAdj(x, y),
            };
            Settlements.Add(s);
            SettlementsById[s.Id] = s;
            SettleGrid[Idx(x, y)] = (short)s.Id;
            Dirty.Features = true; Dirty.Borders = true; Dirty.Labels = true;
            return s;
        }

        public string RuinSettlement(Settlement s, string cause, bool altName = false)
        {
            s.Ruined = true;
            s.RuinedYear = Year;
            SettleGrid[Idx(s.X, s.Y)] = -1;
            int usedAlt = 0;
            foreach (var r2 in Ruins) if (r2.Alt != null) usedAlt++;
            string alt = altName ? RuinNames[usedAlt % RuinNames.Length] : null;
            Ruins.Add(new Ruin { X = s.X, Y = s.Y, Name = s.Name, Year = Year, Tier = s.Tier, Cause = cause, Alt = alt });
            Dirty.Features = true; Dirty.Borders = true; Dirty.Labels = true;
            return alt;
        }

        public void RecomputeClaims()
        {
            for (int i = 0; i < Claims.Length; i++) Claims[i] = -1;
            var strength = new float[Claims.Length];
            int[] influence = { 4, 6, 9, 12 };
            foreach (var s in Settlements)
            {
                if (s.Ruined) continue;
                int R = influence[s.Tier];
                for (int dy = -R; dy <= R; dy++)
                    for (int dx = -R; dx <= R; dx++)
                    {
                        int nx = s.X + dx, ny = s.Y + dy;
                        if (!InB(nx, ny) || !IsLandAt(nx, ny)) continue;
                        double d = Math.Sqrt(dx * dx + dy * dy);
                        if (d > R) continue;
                        float str = (float)((s.Tier + 1.5) * (1 - d / (R + 1)));
                        int i = Idx(nx, ny);
                        if (str > strength[i]) { strength[i] = str; Claims[i] = (short)s.Culture; }
                    }
            }
            Dirty.Borders = true;
        }

        public List<Settlement> AliveSettlements()
        {
            var list = new List<Settlement>();
            foreach (var s in Settlements) if (!s.Ruined) list.Add(s);
            return list;
        }
    }
}
