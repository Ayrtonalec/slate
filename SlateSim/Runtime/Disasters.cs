// SLATE — the world bites back: plague along the trade routes, wildfire,
// flood, earthquake, locusts. Nature is the other author of history.
// Design rules: rare and consequential (sagas, not noise); every disaster is
// an input to existing systems (walls crack -> wars of opportunity; trade
// carries the pest; ash starves and silt feeds); own labeled rng streams so
// adding these never reshuffles older systems' randomness.
using System;
using System.Collections.Generic;

namespace Slate.Sim
{
    public static class Disasters
    {
        // Plague tuning: roughly once a century somewhere, 25-45% mortality in
        // connected places over ~2 years, isolated hamlets often spared.
        private const double PlagueYearlyChance = 0.012;
        private const int PlagueMinYear = 80;

        public static void Yearly(World w)
        {
            TryStartPlague(w);
            TryWildfire(w);
            TryFlood(w);
            TryEarthquake(w);
            TryLocusts(w);
            RegrowBurnedForest(w);
        }

        public static void Monthly(World w)
        {
            PlagueTick(w);
            FireTick(w);
            LocustTick(w);
            ExpireSilt(w);
        }

        // ---------------- Plague ----------------

        private static void TryStartPlague(World w)
        {
            var rng = w.RngPlague;
            if (w.PlagueActive || w.Tick < w.PlagueCooldownUntil || w.Year < PlagueMinYear) return;
            if (!rng.Chance(PlagueYearlyChance)) return;

            // It enters through a gateway of trade: a port or a well-roaded town.
            var gateways = new List<Settlement>();
            foreach (var s in w.AliveSettlements())
            {
                int deg = w.RoadDeg.TryGetValue(s.Id, out int d) ? d : 0;
                if (s.Pop > 400 && (s.Fish || deg >= 2)) gateways.Add(s);
            }
            if (gateways.Count == 0) return;
            var s0 = rng.Pick(gateways);
            Infect(w, s0);
            w.PlagueActive = true;
            w.PlagueStartYear = w.Year;
            w.Chronicle.Add(w, "plagueStart", new EvData { Name = s0.Name, X = s0.X, Y = s0.Y });
            w.Dirty.Features = true;
        }

        private static void Infect(World w, Settlement s)
        {
            s.PlagueState = 1;
            s.PlagueSickUntil = w.Tick + w.RngPlague.Int(14, 26);
        }

        // Called from the war system: a victorious army at a sick town brings it home.
        public static void InfectFromWar(World w, Settlement home)
        {
            if (!w.PlagueActive || home == null || home.Ruined || home.PlagueState != 0) return;
            Infect(w, home);
            if (home.Tier >= 2)
                w.Chronicle.Add(w, "plagueTown", new EvData { Name = home.Name, X = home.X, Y = home.Y });
        }

        private static void PlagueTick(World w)
        {
            if (!w.PlagueActive) return;
            var rng = w.RngPlague;
            int sick = 0;
            var toInfect = new List<Settlement>();
            var alive = w.AliveSettlements();

            foreach (var s in alive)
            {
                if (s.PlagueState != 1) continue;
                sick++;

                // Mortality scales with crowding: the market town dies hardest.
                double density = Math.Min(1.5, s.Pop / Math.Max(200.0, s.Cap));
                s.Pop *= 1 - (0.012 + 0.010 * density);

                // Spread along the roads first — trade is the vector.
                foreach (var road in w.Roads)
                {
                    int otherId = road.A == s.Id ? road.B : road.B == s.Id ? road.A : -1;
                    if (otherId < 0) continue;
                    if (!w.SettlementsById.TryGetValue(otherId, out var other) || other == null || other.Ruined) continue;
                    if (other.PlagueState == 0 && rng.Chance(0.09)) toInfect.Add(other);
                }
                // Then overland to close neighbors.
                foreach (var o in alive)
                {
                    if (o.PlagueState != 0) continue;
                    double d2 = (double)(o.X - s.X) * (o.X - s.X) + (double)(o.Y - s.Y) * (o.Y - s.Y);
                    if (d2 <= 16 && rng.Chance(0.05)) toInfect.Add(o);
                }

                if (w.Tick >= s.PlagueSickUntil)
                {
                    s.PlagueState = 2;
                    // Survivors inherit: labor scarcity reads as a prosperity decade.
                    s.BoomUntil = Math.Max(s.BoomUntil, w.Tick + 8 * 12);
                }
            }

            foreach (var t in toInfect)
            {
                if (t.PlagueState != 0) continue;
                Infect(w, t);
                if (t.Tier >= 2)
                    w.Chronicle.Add(w, "plagueTown", new EvData { Name = t.Name, X = t.X, Y = t.Y });
            }

            if (sick == 0 && toInfect.Count == 0)
            {
                w.PlagueActive = false;
                w.PlagueCooldownUntil = w.Tick + rng.Int(70, 140) * 12;
                foreach (var s in w.Settlements) s.PlagueState = 0;
                w.Chronicle.Add(w, "plagueEnd", new EvData
                {
                    Pop = Math.Max(1, w.Year - w.PlagueStartYear),
                    X = w.W / 2.0, Y = w.H / 2.0,
                });
                w.Dirty.Features = true;
            }
        }

        // ---------------- Wildfire ----------------

        private static void TryWildfire(World w)
        {
            var rng = w.RngDisaster;
            if (w.BurningCells.Count > 0) return; // one great fire at a time
            double chance = w.LeanYears > 0 ? 0.10 : 0.03; // droughts breed fire
            if (!rng.Chance(chance)) return;

            // Dry forest is the tinder.
            var tinder = new List<int>();
            for (int i = 0; i < w.W * w.H; i++)
                if (w.Biome[i] == B.FOREST && w.Burned[i] == 0 && w.Temp[i] > 0.5f && w.Moist[i] < 0.62f)
                    tinder.Add(i);
            if (tinder.Count < 30) return;
            int start = tinder[rng.Int(0, tinder.Count - 1)];
            IgniteCell(w, start);
            w.Chronicle.Add(w, "wildfire", new EvData
            {
                Region = w.RegionNameAt(start % w.W, start / w.W, "the deep woods"),
                X = start % w.W, Y = start / w.W,
            });
        }

        private static void IgniteCell(World w, int i)
        {
            w.Burned[i] = 1;
            w.BurnCellTick[i] = w.Tick;
            w.BurningCells.Add(i);
            w.BurnedVersion++;
        }

        private static void FireTick(World w)
        {
            if (w.BurningCells.Count == 0) return;
            var rng = w.RngDisaster;
            bool changed = false;

            // Spread to adjacent unburned forest; a cell burns for two months.
            var front = new List<int>(w.BurningCells);
            foreach (int i in front)
            {
                int x = i % w.W, y = i / w.W;
                void TrySpread(int j)
                {
                    if (j < 0 || j >= w.W * w.H) return;
                    if (w.Biome[j] == B.FOREST && w.Burned[j] == 0 && rng.Chance(0.45)) { IgniteCell(w, j); changed = true; }
                }
                if (x > 0) TrySpread(i - 1);
                if (x < w.W - 1) TrySpread(i + 1);
                if (y > 0) TrySpread(i - w.W);
                if (y < w.H - 1) TrySpread(i + w.W);
                if (w.Tick - w.BurnCellTick[i] >= 2)
                {
                    w.Burned[i] = 2;
                    w.BurnCellTick[i] = w.Tick;
                    w.BurningCells.Remove(i);
                    w.BurnedVersion++;
                    changed = true;
                }

                // The fire reaches for anything wooden nearby.
                var s = w.SettlementNear(x, y, 1.5);
                if (s != null && !s.Ruined)
                {
                    s.Pop *= 0.96;
                    if (s.Walls == 1 && rng.Chance(0.25)) { s.Walls = 0; w.Dirty.Features = true; } // palisades burn
                    if (s.Pop < 120 && rng.Chance(0.1))
                    {
                        w.Chronicle.Add(w, "burnedTown", new EvData { Name = s.Name, X = s.X, Y = s.Y });
                        w.RuinSettlement(s, "fire");
                    }
                }
            }
            if (changed) w.RecomputeFert();
        }

        private static void RegrowBurnedForest(World w)
        {
            bool changed = false;
            for (int i = 0; i < w.W * w.H; i++)
            {
                if (w.Burned[i] != 2) continue;
                int regrowTicks = (int)((38 + SlateRng.Hash2(i % w.W, i / w.W, 0x9E37) * 14) * 12);
                if (w.Tick - w.BurnCellTick[i] > regrowTicks)
                {
                    w.Burned[i] = 0;
                    w.BurnedVersion++;
                    changed = true;
                }
            }
            if (changed) w.RecomputeFert();
        }

        // ---------------- Flood ----------------

        private static void TryFlood(World w)
        {
            var rng = w.RngDisaster;
            if (w.LeanYears > 0 || w.RiverPaths.Count == 0) return; // wet years flood
            if (!rng.Chance(0.04)) return;
            var path = w.RiverPaths[rng.Int(0, w.RiverPaths.Count - 1)];

            Settlement worst = null;
            foreach (var s in w.AliveSettlements())
            {
                foreach (var (px, py) in path.Pts)
                {
                    double d2 = (double)(s.X - px) * (s.X - px) + (double)(s.Y - py) * (s.Y - py);
                    if (d2 <= 4)
                    {
                        s.Pop *= 0.93;
                        s.Wealth *= 0.85;
                        if (worst == null || s.Pop > worst.Pop) worst = s;
                        break;
                    }
                }
            }
            if (worst == null) return; // a river flooding wilderness is not history

            var mid = path.Pts[path.Pts.Count / 2];
            w.SiltZones.Add(new Zone { X = mid.X, Y = mid.Y, R = 6, Start = w.Tick, Until = w.Tick + 12 * 12 });
            w.RecomputeFert();
            w.Chronicle.Add(w, "flood", new EvData { Name = worst.Name, X = worst.X, Y = worst.Y });
        }

        private static void ExpireSilt(World w)
        {
            bool changed = false;
            for (int i = w.SiltZones.Count - 1; i >= 0; i--)
                if (w.SiltZones[i].Until != 0 && w.Tick >= w.SiltZones[i].Until)
                {
                    w.SiltZones.RemoveAt(i);
                    changed = true;
                }
            if (changed) w.RecomputeFert();
        }

        // ---------------- Earthquake ----------------

        private static void TryEarthquake(World w)
        {
            var rng = w.RngDisaster;
            if (!rng.Chance(0.02)) return;

            // Epicenters live where the world was folded: the mountain ridges.
            var faults = new List<int>();
            for (int i = 0; i < w.W * w.H; i += 3)
                if (w.Biome[i] == B.MOUNTAIN) faults.Add(i);
            if (faults.Count == 0) return;
            int epi = faults[rng.Int(0, faults.Count - 1)];
            int ex = epi % w.W, ey = epi / w.W;

            bool anyoneFelt = false;
            foreach (var s in w.AliveSettlements())
            {
                double d2 = (double)(s.X - ex) * (s.X - ex) + (double)(s.Y - ey) * (s.Y - ey);
                if (d2 > 9 * 9) continue;
                anyoneFelt = true;
                s.Pop *= 0.95;
                if (s.Walls == 2 && rng.Chance(0.6))
                {
                    s.Walls = 1; // dressed stone becomes rubble; the palisade line remains
                    w.Chronicle.Add(w, "wallsFell", new EvData { Name = s.Name, X = s.X, Y = s.Y });
                    w.Dirty.Features = true;
                }
            }
            if (!anyoneFelt) return; // unfelt earthquakes are geology, not history

            w.Chronicle.Add(w, "earthquake", new EvData
            {
                Region = w.RegionNameAt(ex, ey, "the high country"),
                X = ex, Y = ey,
            });
            w.Scars.Add(new Scar { X = ex, Y = ey, Kind = "quake", Name = null, Year = w.Year });
        }

        // ---------------- Locusts ----------------

        private static void TryLocusts(World w)
        {
            var rng = w.RngDisaster;
            if (w.Locusts.Count > 0 || !rng.Chance(0.03)) return;

            // Swarms rise off the hot dry lands and drift across the farms.
            var edges = new List<int>();
            for (int i = 0; i < w.W * w.H; i += 2)
                if (w.Biome[i] == B.DESERT || (w.Biome[i] == B.PLAINS && w.Temp[i] > 0.62f && w.Moist[i] < 0.45f))
                    edges.Add(i);
            if (edges.Count < 20) return;
            int start = edges[rng.Int(0, edges.Count - 1)];
            double ang = rng.Range(0, Math.PI * 2);
            w.Locusts.Add(new LocustCloud
            {
                X = start % w.W, Y = start / w.W,
                Dx = Math.Cos(ang) * 1.4, Dy = Math.Sin(ang) * 1.4,
                Until = w.Tick + rng.Int(10, 18),
            });
            w.Chronicle.Add(w, "locusts", new EvData
            {
                Region = w.RegionNameAt(start % w.W, start / w.W, "the dry country"),
                X = start % w.W, Y = start / w.W,
            });
        }

        private static void LocustTick(World w)
        {
            for (int i = w.Locusts.Count - 1; i >= 0; i--)
            {
                var c = w.Locusts[i];
                c.X += c.Dx; c.Y += c.Dy;
                bool gone = w.Tick >= c.Until || c.X < 0 || c.Y < 0 || c.X >= w.W || c.Y >= w.H;
                if (gone) { w.Locusts.RemoveAt(i); continue; }
                foreach (var s in w.AliveSettlements())
                {
                    double d2 = (s.X - c.X) * (s.X - c.X) + (s.Y - c.Y) * (s.Y - c.Y);
                    if (d2 <= 9) s.Pop *= 0.985; // the swarm eats the margin people live on
                }
            }
        }
    }
}
