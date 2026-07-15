// SLATE — expeditions into the fog. Restless, wealthy ports fit out ships
// that sail past the last known water. Most come back with nothing; some
// never come back; a few return with riches and tales of land beyond the
// fog. Today the tales are rumors and trade booms — when the Archipelago
// phase lands (design doc 08 §4b), the same ships will materialize real
// discovered regions. Own labeled rng stream: "exploration".
using System;
using System.Collections.Generic;

namespace Slate.Sim
{
    public static class Expeditions
    {
        private const int MaxShips = 2;
        private const double ShipSpeed = 1.2;    // cells per month
        private const int CooldownYears = 40;    // per port

        public static void Yearly(World w)
        {
            var rng = w.RngSea;
            if (w.Ships.Count >= MaxShips) return;

            foreach (var s in w.AliveSettlements())
            {
                // Only a thriving coastal town fits out a ship for the outer dark.
                if (!s.Fish || s.Pop < 800 || s.Wealth < 12) continue;
                if (w.ExpeditionCooldown.TryGetValue(s.Id, out int until) && w.Tick < until) continue;
                if (!rng.Chance(0.02)) continue;

                var route = FindSeaRoute(w, s);
                if (route == null) continue;

                s.Wealth -= 8; // ships, salt meat, and men who must be paid
                w.ExpeditionCooldown[s.Id] = w.Tick + CooldownYears * 12;
                string captain = "Captain " + w.Namer.Person(s.Culture);
                w.Ships.Add(new Ship
                {
                    Id = w.NextShipId++,
                    HomeId = s.Id, HomeName = s.Name, Captain = captain,
                    Culture = s.Culture,
                    X = route.Value.startX, Y = route.Value.startY,
                    LaunchX = route.Value.startX, LaunchY = route.Value.startY,
                    TargetX = route.Value.edgeX, TargetY = route.Value.edgeY,
                    State = Ship.Outbound,
                });
                w.Chronicle.Add(w, "expedition", new EvData { Name = s.Name, Hero = captain, X = s.X, Y = s.Y });
                w.Dirty.Features = true;
                break; // one launch a year, worldwide — sagas, not ferry service
            }
        }

        public static void Monthly(World w)
        {
            var rng = w.RngSea;
            foreach (var ship in w.Ships.ToArray())
            {
                if (ship.State == Ship.InFog)
                {
                    if (w.Tick < ship.FogUntil) continue;
                    if (ship.WillReturn)
                    {
                        ship.State = Ship.Homebound;
                        ship.TargetX = ship.LaunchX; ship.TargetY = ship.LaunchY;
                    }
                    else
                    {
                        w.Chronicle.Add(w, "expLost", new EvData
                        {
                            Name = ship.HomeName, Hero = ship.Captain,
                            X = ship.X, Y = ship.Y,
                        });
                        w.Ships.Remove(ship);
                        w.Dirty.Features = true;
                    }
                    continue;
                }

                double dx = ship.TargetX - ship.X, dy = ship.TargetY - ship.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > 1.0)
                {
                    ship.X += dx / dist * ShipSpeed;
                    ship.Y += dy / dist * ShipSpeed;
                    continue;
                }

                if (ship.State == Ship.Outbound)
                {
                    // Into the fog. The sea decides, in its own time.
                    ship.State = Ship.InFog;
                    ship.FogUntil = w.Tick + rng.Int(8, 26);
                    ship.WillReturn = rng.Chance(0.65);
                }
                else // Homebound, arrived
                {
                    w.SettlementsById.TryGetValue(ship.HomeId, out var home);
                    if (home != null && !home.Ruined)
                    {
                        home.Wealth += rng.Range(10, 25);
                        home.BoomUntil = Math.Max(home.BoomUntil, w.Tick + 12 * 12);
                        w.Chronicle.Add(w, "expReturn", new EvData
                        {
                            Name = home.Name, Hero = ship.Captain,
                            X = home.X, Y = home.Y,
                        });
                    }
                    w.Ships.Remove(ship);
                    w.Dirty.Features = true;
                }
            }
        }

        // A straight sea lane from the port's coast to the nearest open edge of
        // the map. Ships don't sail over land; inland-sea ports stay home.
        private static (double startX, double startY, double edgeX, double edgeY)? FindSeaRoute(World w, Settlement s)
        {
            // Start just off the coast: the nearest water cell beside the town.
            double sx = s.X, sy = s.Y;
            bool found = false;
            for (int r = 1; r <= 2 && !found; r++)
                for (int dy = -r; dy <= r && !found; dy++)
                    for (int dx = -r; dx <= r && !found; dx++)
                    {
                        int nx = s.X + dx, ny = s.Y + dy;
                        if (w.InB(nx, ny) && w.HeightMap[ny * w.W + nx] <= w.Sea)
                        {
                            sx = nx; sy = ny; found = true;
                        }
                    }
            if (!found) return null;

            // Candidate exits: nearest point on each border, in a fixed order.
            var candidates = new (double x, double y)[]
            {
                (sx, 1), (sx, w.H - 2), (1, sy), (w.W - 2, sy),
            };
            foreach (var (ex, ey) in candidates)
            {
                if (SeaLaneOpen(w, sx, sy, ex, ey)) return (sx, sy, ex, ey);
            }
            return null;
        }

        private static bool SeaLaneOpen(World w, double x0, double y0, double x1, double y1)
        {
            int steps = (int)Math.Ceiling(Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) * 2);
            if (steps == 0) return false;
            int land = 0;
            for (int k = 0; k <= steps; k++)
            {
                double t = (double)k / steps;
                int cx = (int)Math.Floor(x0 + (x1 - x0) * t + 0.5);
                int cy = (int)Math.Floor(y0 + (y1 - y0) * t + 0.5);
                if (!w.InB(cx, cy)) continue;
                if (w.HeightMap[cy * w.W + cx] > w.Sea) land++;
                if (land > 1) return false;
            }
            return true;
        }
    }
}
