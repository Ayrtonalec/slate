// SLATE — the simulation tick. One tick = one month.
// Port of god-sim-prototype/src/sim.js. Order is fixed and deterministic:
// climate -> settlements -> colonization -> gold -> dragons -> roads -> claims.
// The world runs with zero divine input; god powers only perturb it.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Slate.Sim
{
    // Named "Simulation" (not "Sim") so it never collides with the Slate.Sim namespace.
    public static class Simulation
    {
        private const int FEAR_R = 8;  // dragon terror radius
        private const int RAID_R = 10;

        // Warband tuning: sagas, not white noise — a few wars per century, worldwide.
        private const int MaxArmies = 2;
        public const double ArmySpeedCellsPerMonth = 0.5; // public: renderers pace their interpolation to it
        private const int TruceYears = 30;         // peace between the same two cultures

        public static void Tick(World w)
        {
            var rng = w.RngSim;
            w.Tick++;
            w.Month = w.Tick % 12;
            w.Year = w.Tick / 12;

            if (w.Month == 0) Yearly(w, rng);

            // --- Per-settlement dragon fear cache (cheap: few dragons).
            var feared = new HashSet<int>();
            foreach (var d in w.Dragons)
            {
                foreach (var s in w.Settlements)
                {
                    if (s.Ruined) continue;
                    double d2 = (double)(s.X - d.X) * (s.X - d.X) + (double)(s.Y - d.Y) * (s.Y - d.Y);
                    if (d2 <= FEAR_R * FEAR_R) feared.Add(s.Id);
                }
            }

            // --- Settlements.
            foreach (var s in w.Settlements)
            {
                if (s.Ruined) continue;
                var storm = w.StormAt(s.X, s.Y);

                int deg = w.RoadDeg.TryGetValue(s.Id, out int dv) ? dv : 0;
                bool boom = w.Tick < s.BoomUntil;
                // A market town is fed by its hinterland: trade raises the ceiling
                // its own fields could never carry (how real centers outgrew villages).
                s.Prosp = 1 + 0.15 * deg + (s.Gold ? 2.5 : 0) + (s.Fish ? 0.3 : 0) + s.Tier * 0.15 + (boom ? 1 : 0)
                    + 0.06 * Math.Min(8, s.Hinterland);

                // Food capacity: staggered recompute, or forced when fertility changed.
                if (w.FertDirty || (w.Tick + s.Id) % 24 == 0)
                {
                    double cap = w.FertAround(s.X, s.Y, 3) * 26 * s.SoilLuck; // no two valleys are equal
                    if (s.Fish && storm == null) cap += 170;
                    cap *= 1 + 0.7 * Math.Max(0, s.Prosp - 1); // trade feeds cities beyond what fields carry
                    if (w.LeanYears > 0) cap *= 0.85;
                    s.Cap = Math.Max(25, cap);
                }

                double prev = s.Pop;
                if (storm != null)
                {
                    s.Pop *= 0.972; // the sky itself is against them
                }
                else
                {
                    double r = 0.011 * (0.75 + 0.25 * Math.Min(2.2, s.Prosp));
                    if (boom) r *= 1.6;
                    if (feared.Contains(s.Id)) r *= 0.25;
                    s.Pop += s.Pop * r * (1 - s.Pop / s.Cap);
                }

                s.DeclineStreak = s.Pop < prev - 0.01 ? s.DeclineStreak + 1 : 0;

                // Hunger and famine: chronicled only when the deficit is deep and rare.
                if (s.Pop > s.Cap * 1.2)
                {
                    s.HungerStreak++;
                    if (s.HungerStreak >= 15 && s.Pop > 250 && w.Year - s.LastFamineYear > 15)
                    {
                        s.LastFamineYear = w.Year;
                        s.HungerStreak = 0;
                        w.Chronicle.Add(w, "famine", new EvData { Name = s.Name, X = s.X, Y = s.Y });
                        s.Pop *= 0.82;
                        var dest = BestNeighbor(w, s);
                        if (dest != null) { dest.Pop += s.Pop * 0.06; s.Pop *= 0.94; }
                    }
                }
                else if (s.HungerStreak > 0) s.HungerStreak = Math.Max(0, s.HungerStreak - 2);

                // Tier transitions: chronicled only the first time a rank is reached.
                int t = w.TierOf(s.Pop);
                if (t > s.Tier)
                {
                    s.Tier = t;
                    if (t > s.MaxTier)
                    {
                        s.MaxTier = t;
                        int popRounded = (int)Math.Floor(s.Pop / 50 + 0.5) * 50; // JS Math.round
                        var data = new EvData { Name = s.Name, Pop = popRounded, Culture = s.Culture, X = s.X, Y = s.Y };
                        w.Chronicle.Add(w, t == 1 ? "village" : t == 2 ? "town" : "city", data);
                    }
                    w.Dirty.Features = w.Dirty.Borders = w.Dirty.Labels = true;
                }
                else if (t < s.Tier)
                {
                    s.Tier = t; // quiet decline; the chronicle notices only the fall to ruin
                    w.Dirty.Features = w.Dirty.Labels = true;
                }

                // Abandonment.
                if ((s.Pop < 35 && s.DeclineStreak > 24) || (storm != null && s.Pop < 55))
                {
                    string why = storm != null ? null : (feared.Contains(s.Id) ? "its people fled the wyrm" : null);
                    if (storm != null) w.Chronicle.Add(w, "stormExodus", new EvData { Name = s.Name, X = s.X, Y = s.Y });
                    else w.Chronicle.Add(w, "abandon", new EvData { Name = s.Name, Tier = s.Tier, Why = why, X = s.X, Y = s.Y });
                    w.RuinSettlement(s, storm != null ? "storm" : "decline");
                    var dest = BestNeighbor(w, s);
                    if (dest != null)
                    {
                        dest.Pop += s.Pop * 0.6;
                        w.Chronicle.Add(w, "migration", new EvData { From = s.Name, To = dest.Name, X = dest.X, Y = dest.Y, Fx = s.X, Fy = s.Y });
                    }
                    continue;
                }

                // Wealth accrues from prosperity; this is what dragons smell.
                s.Wealth += s.Prosp * s.Pop / 200000;
            }
            w.FertDirty = false;

            // --- Colonization (fission), damped by regional crowding.
            var aliveNow = w.AliveSettlements();
            foreach (var s in aliveNow)
            {
                if (s.Pop > s.Cap * 0.7 && s.Pop > 150 && rng.Chance(0.005))
                {
                    int crowd = 0;
                    foreach (var o in aliveNow)
                    {
                        double d2 = (double)(o.X - s.X) * (o.X - s.X) + (double)(o.Y - s.Y) * (o.Y - s.Y);
                        if (d2 <= 64) crowd++;
                    }
                    if (rng.Next() < (crowd - 2) / 10.0) continue; // packed regions stop spilling outward
                    var site = w.BestSiteNear(s.X, s.Y, 5, 16);
                    if (site != null)
                    {
                        // Daughter settlements start small — a few families with a cart,
                        // not half the town. Most stay hamlets in the mother's shadow.
                        double emig = Math.Max(35, s.Pop * 0.16);
                        s.Pop -= emig * 0.9;
                        var child = w.AddSettlement(site.X, site.Y, s.Culture, emig);
                        w.Chronicle.Add(w, "found", new EvData { Name = child.Name, Parent = s.Name, X = child.X, Y = child.Y, Fx = s.X, Fy = s.Y });
                    }
                }
            }

            // --- Gold: discovery near settlements, mining camps in the wilds.
            foreach (var v in w.Veins)
            {
                if (!v.Revealed)
                {
                    var near = w.SettlementNear(v.X, v.Y, 4.5);
                    if (near != null && rng.Chance(0.02))
                    {
                        v.Revealed = true;
                        near.Gold = true;
                        near.BoomUntil = w.Tick + 40 * 12;
                        w.Chronicle.Add(w, "goldFound", new EvData { Name = near.Name, X = v.X, Y = v.Y });
                        w.Chronicle.Add(w, "boom", new EvData { Name = near.Name, X = near.X, Y = near.Y });
                        PullMigrants(w, near, 0.12);
                        w.Dirty.Features = true;
                    }
                }
                else if (w.SettlementNear(v.X, v.Y, 4) == null)
                {
                    // A known vein with nobody working it draws the desperate.
                    var parent = w.SettlementNear(v.X, v.Y, 16);
                    double chance = parent != null ? 0.012 : 0.004;
                    if (rng.Chance(chance))
                    {
                        var spot = w.CampSiteNear(v.X, v.Y);
                        if (spot != null)
                        {
                            int culture;
                            if (parent != null) culture = parent.Culture;
                            else
                            {
                                var far = w.SettlementNear(v.X, v.Y, 60);
                                culture = far != null ? far.Culture : rng.Int(0, Cultures.All.Length - 1);
                            }
                            string name = w.Namer.GoldPlace(culture);
                            var camp = w.AddSettlement(spot.Value.X, spot.Value.Y, culture, parent != null ? 55 : 40, name, gold: true);
                            camp.BoomUntil = w.Tick + 40 * 12;
                            w.Chronicle.Add(w, "camp", new EvData { Name = name, Region = w.RegionNameAt(v.X, v.Y, "the high stone"), X = spot.Value.X, Y = spot.Value.Y });
                            if (parent != null) parent.Pop *= 0.96;
                        }
                    }
                }
            }

            // --- Dragons: raids.
            foreach (var d in w.Dragons.ToList())
            {
                if (w.Tick - d.LastRaid > d.RaidEvery)
                {
                    Settlement target = null;
                    double tw = 8; // only bother with somewhere worth burning
                    foreach (var s in w.AliveSettlements())
                    {
                        double d2 = (double)(s.X - d.X) * (s.X - d.X) + (double)(s.Y - d.Y) * (s.Y - d.Y);
                        if (d2 <= RAID_R * RAID_R && s.Wealth + s.Pop / 400 > tw)
                        {
                            tw = s.Wealth + s.Pop / 400; target = s;
                        }
                    }
                    d.LastRaid = w.Tick;
                    d.RaidEvery = rng.Int(60, 140);
                    if (target != null)
                    {
                        w.Chronicle.Add(w, "raid", new EvData { Dragon = d.Name, Name = target.Name, X = target.X, Y = target.Y, Fx = d.X, Fy = d.Y });
                        target.Pop *= 0.85;
                        target.Wealth *= 0.75;
                        if (target.Pop < 400 && rng.Chance(0.5))
                        {
                            string alt = w.RuinSettlement(target, "dragon", altName: true);
                            w.Chronicle.Add(w, "razed", new EvData { Name = target.Name, RuinName = alt, X = target.X, Y = target.Y });
                            var dest = BestNeighbor(w, target);
                            if (dest != null)
                            {
                                dest.Pop += target.Pop * 0.5;
                                w.Chronicle.Add(w, "migration", new EvData { From = target.Name, To = dest.Name, X = dest.X, Y = dest.Y, Fx = target.X, Fy = target.Y });
                            }
                        }
                    }
                }
            }

            // --- Wars: armies on the march (aliveness contract, design doc 02).
            MoveArmies(w);

            if (w.ClaimsDirtyTick != 0 && w.Tick - w.ClaimsDirtyTick > 24)
            {
                w.RecomputeClaims();
                w.ClaimsDirtyTick = 0;
            }
            else if (w.Dirty.Borders && w.ClaimsDirtyTick == 0)
            {
                w.ClaimsDirtyTick = w.Tick;
            }
        }

        private static int PairKey(int a, int b) => a < b ? a * 100 + b : b * 100 + a;

        // A straight campaign route must be walkable; armies don't swim.
        private static bool LandRouteOpen(World w, Settlement a, Settlement b)
        {
            int steps = (int)Math.Ceiling(Math.Sqrt((double)(b.X - a.X) * (b.X - a.X) + (double)(b.Y - a.Y) * (b.Y - a.Y)) * 2);
            if (steps == 0) return true;
            int wet = 0;
            for (int k = 0; k <= steps; k++)
            {
                double t = (double)k / steps;
                double x = a.X + (b.X - a.X) * t;
                double y = a.Y + (b.Y - a.Y) * t;
                if (!w.IsLandAt((int)Math.Floor(x + 0.5), (int)Math.Floor(y + 0.5))) wet++;
                if (wet > 2) return false;
            }
            return true;
        }

        // Yearly: pressure and envy between neighboring cultures spark wars.
        private static void DeclareWars(World w, Rng war)
        {
            if (w.Armies.Count >= MaxArmies) return;
            var alive = w.AliveSettlements();
            foreach (var s in alive)
            {
                if (s.Pop < 500) continue;
                // Find the nearest worthwhile enemy within campaign range.
                Settlement target = null;
                double bd = 18 * 18;
                foreach (var o in alive)
                {
                    if (o.Culture == s.Culture || o.Pop < 150) continue;
                    double d2 = (double)(o.X - s.X) * (o.X - s.X) + (double)(o.Y - s.Y) * (o.Y - s.Y);
                    if (d2 < bd) { bd = d2; target = o; }
                }
                if (target == null) continue;
                int key = PairKey(s.Culture, target.Culture);
                if (w.TruceUntil.TryGetValue(key, out int until) && w.Tick < until) continue;

                // Tension: crowding at home, lean harvests, and a rich neighbor to envy.
                double tension = 0.006;
                if (w.LeanYears > 0) tension *= 2.0;
                if (s.Pop > s.Cap * 0.9) tension *= 1.8;
                if (target.Wealth > s.Wealth * 1.5 + 5) tension *= 1.6;
                if (!war.Chance(Math.Min(0.05, tension))) continue;
                if (!LandRouteOpen(w, s, target)) continue;

                double size = Math.Max(150, s.Pop * 0.25);
                s.Pop -= size * 0.9; // the spears leave the fields
                w.Armies.Add(new Army
                {
                    Id = w.NextArmyId++,
                    Culture = s.Culture,
                    FromId = s.Id, TargetId = target.Id,
                    FromName = s.Name, TargetName = target.Name,
                    X = s.X, Y = s.Y, Size = size,
                });
                w.Chronicle.Add(w, "warMarch", new EvData
                {
                    Culture = s.Culture, From = s.Name, Name = target.Name,
                    Pop = (int)Math.Floor(size / 50 + 0.5) * 50,
                    X = s.X, Y = s.Y, Fx = target.X, Fy = target.Y,
                });
                w.Dirty.Features = true;
                break; // at most one new war a year — sagas, not noise
            }
        }

        // Monthly: march, and fight when the walls come into view.
        private static void MoveArmies(World w)
        {
            var war = w.RngWar;
            foreach (var a in w.Armies.ToList())
            {
                w.SettlementsById.TryGetValue(a.TargetId, out var target);
                if (target == null || target.Ruined)
                {
                    // Nothing left to fight for; the warband drifts home.
                    w.SettlementsById.TryGetValue(a.FromId, out var home);
                    if (home != null && !home.Ruined) home.Pop += a.Size * 0.8;
                    w.Chronicle.Add(w, "warOver", new EvData { From = a.FromName, X = a.X, Y = a.Y });
                    w.Armies.Remove(a);
                    w.Dirty.Features = true;
                    continue;
                }

                double dx = target.X - a.X, dy = target.Y - a.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > 1.2)
                {
                    a.X += dx / dist * ArmySpeedCellsPerMonth;
                    a.Y += dy / dist * ArmySpeedCellsPerMonth;
                    continue;
                }

                // Battle at the gates. Real walls count: an open town is easy meat,
                // a palisade helps, dressed stone doubles the defenders' worth.
                double defense = target.Pop * (0.5 + 0.25 * target.Walls);
                double attack = a.Size * 1.35;
                bool attackerWins = war.Next() < attack / (attack + defense);
                w.TruceUntil[PairKey(a.Culture, target.Culture)] = w.Tick + TruceYears * 12;

                if (attackerWins)
                {
                    if (target.Tier <= 1 || war.Chance(0.3))
                    {
                        // Put to the torch. (Later, a warlord's temperament decides this.)
                        double survivors = target.Pop * 0.3;
                        w.Chronicle.Add(w, "sacked", new EvData { Name = target.Name, Culture = a.Culture, X = target.X, Y = target.Y });
                        w.RuinSettlement(target, "war");
                        var dest = BestNeighbor(w, target);
                        if (dest != null)
                        {
                            dest.Pop += survivors;
                            w.Chronicle.Add(w, "migration", new EvData { From = target.Name, To = dest.Name, X = dest.X, Y = dest.Y, Fx = target.X, Fy = target.Y });
                        }
                        w.SettlementsById.TryGetValue(a.FromId, out var home);
                        if (home != null && !home.Ruined) { home.Pop += a.Size * 0.6; home.Wealth += target.Wealth * 0.4; }
                    }
                    else
                    {
                        // Conquest: the town lives on under new banners.
                        target.Culture = a.Culture;
                        target.Pop = target.Pop * 0.72 + a.Size * 0.4;
                        target.Wealth *= 0.6;
                        w.Chronicle.Add(w, "conquest", new EvData { Name = target.Name, Culture = a.Culture, X = target.X, Y = target.Y });
                    }
                }
                else
                {
                    target.Pop *= 0.90;
                    w.Chronicle.Add(w, "defended", new EvData { Name = target.Name, Culture = a.Culture, X = target.X, Y = target.Y });
                }
                w.Armies.Remove(a);
                w.Dirty.Features = true; w.Dirty.Borders = true; w.Dirty.Labels = true;
            }
        }

        private static void Yearly(World w, Rng rng)
        {
            // Lean years: a world-scale harvest cycle.
            if (w.LeanYears > 0) w.LeanYears--;
            else if (rng.Chance(0.10)) w.LeanYears = rng.Int(1, 2);

            // Dragon spawning: wealth near mountains breeds trouble.
            if (w.Dragons.Count < 2 && w.Year >= 60 && w.Tick >= w.DragonCooldownUntil)
            {
                foreach (var s in w.AliveSettlements())
                {
                    if (s.Wealth < 70) continue;
                    var m = w.NearestMountain(s.X, s.Y, 6);
                    if (m == null) continue;
                    if (rng.Chance(Math.Min(0.05, 0.015 + s.Wealth / 4000)))
                    {
                        var d = new Dragon
                        {
                            Name = "the wyrm " + w.Namer.Dragon(),
                            X = m.Value.X, Y = m.Value.Y, BornYear = w.Year,
                            LastRaid = w.Tick, RaidEvery = rng.Int(48, 110),
                            RegionName = w.RegionNameAt(m.Value.X, m.Value.Y, "the high peaks"),
                        };
                        w.Dragons.Add(d);
                        w.Chronicle.Add(w, "dragon", new EvData { Dragon = d.Name, Region = d.RegionName, Name = s.Name, X = m.Value.X, Y = m.Value.Y });
                        w.Dirty.Features = true;
                        break;
                    }
                }
            }

            // Dragon slaying & departure.
            foreach (var d in w.Dragons.ToList())
            {
                var heroes = new List<Settlement>();
                foreach (var s in w.AliveSettlements())
                {
                    double d2 = (double)(s.X - d.X) * (s.X - d.X) + (double)(s.Y - d.Y) * (s.Y - d.Y);
                    if (s.Tier >= 2 && d2 <= RAID_R * RAID_R) heroes.Add(s);
                }
                var anyNear = w.SettlementNear(d.X, d.Y, 12);
                if (heroes.Count > 0 && rng.Chance(0.10))
                {
                    var home = rng.Pick(heroes);
                    string hero = "Ser " + w.Namer.Person(home.Culture) + " the Wyrmslayer";
                    w.Chronicle.Add(w, "slain", new EvData { Hero = hero, Name = home.Name, Dragon = d.Name, Region = d.RegionName, X = d.X, Y = d.Y });
                    home.Wealth += 35;
                    home.BoomUntil = Math.Max(home.BoomUntil, w.Tick + 10 * 12);
                    w.Dragons.Remove(d);
                    w.DeadLairs.Add((d.X, d.Y));
                    w.DragonCooldownUntil = w.Tick + 300; // a generation of peace
                    w.Dirty.Features = true;
                }
                else if (anyNear == null && rng.Chance(0.3))
                {
                    w.Chronicle.Add(w, "dragonGone", new EvData { Dragon = d.Name, X = d.X, Y = d.Y });
                    w.Dragons.Remove(d);
                    w.DeadLairs.Add((d.X, d.Y));
                    w.DragonCooldownUntil = w.Tick + 300;
                    w.Dirty.Features = true;
                }
            }

            // Market pull: the district's center siphons folk from its satellites —
            // hamlet belts form around market towns and sizes spread out (Zipf).
            {
                var district = w.AliveSettlements();
                foreach (var s in district) s.Hinterland = 0;
                foreach (var s in district)
                {
                    if (s.Pop < 60) continue;
                    Settlement magnet = null;
                    foreach (var o in district)
                    {
                        if (ReferenceEquals(o, s) || o.Culture != s.Culture) continue;
                        double d2 = (double)(o.X - s.X) * (o.X - s.X) + (double)(o.Y - s.Y) * (o.Y - s.Y);
                        if (d2 <= 49 && o.Pop > s.Pop * 1.8 && (magnet == null || o.Pop > magnet.Pop)) magnet = o;
                    }
                    if (magnet != null)
                    {
                        double moved = s.Pop * 0.007;
                        s.Pop -= moved;
                        magnet.Pop += moved;
                        magnet.Hinterland++; // the market lives off its satellites
                    }
                }
            }

            // Walls: built when threat and wealth meet — never a free tier upgrade.
            // Open towns exist, and they are the ones that fall.
            foreach (var s in w.AliveSettlements())
            {
                if (s.Walls >= 2) continue;
                bool threat = false;
                foreach (var o in w.AliveSettlements())
                {
                    if (o.Culture == s.Culture) continue;
                    double d2 = (double)(o.X - s.X) * (o.X - s.X) + (double)(o.Y - s.Y) * (o.Y - s.Y);
                    if (d2 <= 12 * 12) { threat = true; break; }
                }
                if (!threat)
                    foreach (var d in w.Dragons)
                    {
                        double d2 = (double)(d.X - s.X) * (d.X - s.X) + (double)(d.Y - s.Y) * (d.Y - s.Y);
                        if (d2 <= 12 * 12) { threat = true; break; }
                    }

                if (s.Walls == 0 && s.Pop > 650 && (threat || s.Wealth > 25) && s.Wealth >= 8 && rng.Chance(0.15))
                {
                    s.Walls = 1;
                    s.Wealth -= 6;
                    w.Chronicle.Add(w, "palisade", new EvData { Name = s.Name, X = s.X, Y = s.Y });
                    w.Dirty.Features = true;
                }
                else if (s.Walls == 1 && s.Tier >= 2 && (threat || s.Wealth > 60) && s.Wealth >= 25 && rng.Chance(0.08))
                {
                    s.Walls = 2;
                    s.Wealth -= 20;
                    w.Chronicle.Add(w, "stonewalls", new EvData { Name = s.Name, X = s.X, Y = s.Y });
                    w.Dirty.Features = true;
                }
            }

            // Wars: pressure and envy between neighboring cultures (own rng stream).
            DeclareWars(w, w.RngWar);

            // Roads: nearby sizable settlements link up.
            int built = 0;
            var alive = new List<Settlement>();
            foreach (var s in w.AliveSettlements()) if (s.Tier >= 1) alive.Add(s);
            for (int i = 0; i < alive.Count && built < 2; i++)
            {
                for (int j = i + 1; j < alive.Count && built < 2; j++)
                {
                    var a = alive[i]; var b = alive[j];
                    double d2 = (double)(a.X - b.X) * (a.X - b.X) + (double)(a.Y - b.Y) * (a.Y - b.Y);
                    if (d2 > 13 * 13) continue;
                    int degA = w.RoadDeg.TryGetValue(a.Id, out int da) ? da : 0;
                    int degB = w.RoadDeg.TryGetValue(b.Id, out int db) ? db : 0;
                    if (degA >= 4 || degB >= 4) continue;
                    bool exists = false;
                    foreach (var r in w.Roads)
                        if ((r.A == a.Id && r.B == b.Id) || (r.A == b.Id && r.B == a.Id)) { exists = true; break; }
                    if (exists) continue;
                    if (!rng.Chance(0.25)) continue;
                    var pts = RoadPath(w, a, b);
                    if (pts == null) continue;
                    w.Roads.Add(new Road { A = a.Id, B = b.Id, Pts = pts });
                    w.RoadDeg[a.Id] = degA + 1;
                    w.RoadDeg[b.Id] = degB + 1;
                    w.Chronicle.Add(w, "road", new EvData { A = a.Name, B = b.Name, X = (a.X + b.X) / 2.0, Y = (a.Y + b.Y) / 2.0 });
                    w.Dirty.Features = true;
                    built++;
                }
            }
        }

        private static List<(double X, double Y)> RoadPath(World w, Settlement a, Settlement b)
        {
            // Sampled straight-ish path; reject if it wades through open water.
            int steps = (int)Math.Ceiling(Math.Sqrt((double)(b.X - a.X) * (b.X - a.X) + (double)(b.Y - a.Y) * (b.Y - a.Y)) * 2);
            var pts = new List<(double X, double Y)>();
            int wet = 0;
            for (int k = 0; k <= steps; k++)
            {
                double t = (double)k / steps;
                double x = a.X + (b.X - a.X) * t;
                double y = a.Y + (b.Y - a.Y) * t;
                // JS Math.round rounds half toward +infinity.
                if (!w.IsLandAt((int)Math.Floor(x + 0.5), (int)Math.Floor(y + 0.5))) wet++;
                if (wet > 1) return null;
                pts.Add((x, y));
            }
            return pts;
        }

        private static Settlement BestNeighbor(World w, Settlement s)
        {
            Settlement best = null;
            double bd = double.PositiveInfinity;
            foreach (var o in w.Settlements)
            {
                if (o.Ruined || ReferenceEquals(o, s)) continue;
                if (w.StormAt(o.X, o.Y) != null) continue;
                double d2 = (double)(o.X - s.X) * (o.X - s.X) + (double)(o.Y - s.Y) * (o.Y - s.Y);
                if (d2 < bd) { bd = d2; best = o; }
            }
            return best;
        }

        private static void PullMigrants(World w, Settlement target, double frac)
        {
            var near = w.AliveSettlements()
                .Where(s => !ReferenceEquals(s, target) &&
                    (double)(s.X - target.X) * (s.X - target.X) + (double)(s.Y - target.Y) * (s.Y - target.Y) < 400)
                .OrderByDescending(s => s.Pop) // stable, like JS sort
                .Take(2);
            foreach (var s in near)
            {
                double moved = s.Pop * frac * 0.5;
                s.Pop -= moved;
                target.Pop += moved;
            }
        }

        public static void RunYears(World w, int years, Action<World> onYear = null)
        {
            for (int i = 0; i < years * 12; i++)
            {
                Tick(w);
                if (onYear != null && w.Tick % 12 == 0) onYear(w);
            }
        }
    }
}
