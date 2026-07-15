// SLATE — acts of god. Port of god-sim-prototype/src/powers.js.
// Three powers, each a perturbation the living world will interpret in its
// own time. Every act is chronicled; nothing a god does is forgotten.
using System;

namespace Slate.Sim
{
    public struct PowerResult
    {
        public bool Ok;
        public string Msg;
        public int X, Y;
    }

    public static class Powers
    {
        // JS Math.round rounds half toward +infinity.
        private static int RoundJs(double v) => (int)Math.Floor(v + 0.5);

        public static PowerResult SeedGold(World w, double xIn, double yIn)
        {
            int x = RoundJs(xIn), y = RoundJs(yIn);
            if (!w.InB(x, y)) return new PowerResult { Ok = false, Msg = "Beyond the edge of the map." };
            byte b = w.Biome[w.Idx(x, y)];
            if (b != B.MOUNTAIN && b != B.HILLS)
                return new PowerResult { Ok = false, Msg = "The vein must be seeded in high stone — choose mountains or hills." };
            w.Veins.Add(new Vein { X = x, Y = y, Revealed = true, Divine = true });
            w.Chronicle.Add(w, "goldSeed", new EvData { Region = w.RegionNameAt(x, y, "the high stone"), X = x, Y = y });
            w.Dirty.Features = true;
            return new PowerResult { Ok = true, X = x, Y = y };
        }

        public static PowerResult CurseWeather(World w, double xIn, double yIn)
        {
            int x = RoundJs(xIn), y = RoundJs(yIn);
            if (!w.InB(x, y)) return new PowerResult { Ok = false, Msg = "Beyond the edge of the map." };
            bool anyLand = false;
            for (int dy = -3; dy <= 3 && !anyLand; dy++)
                for (int dx = -3; dx <= 3 && !anyLand; dx++)
                    if (w.IsLandAt(x + dx, y + dy)) anyLand = true;
            if (!anyLand) return new PowerResult { Ok = false, Msg = "Curse the living land — the open sea cares nothing for weather." };
            if (w.StormAt(x, y) != null) return new PowerResult { Ok = false, Msg = "That sky is already yours." };
            w.Storms.Add(new Zone { X = x, Y = y, R = 5, Start = w.Tick });
            w.RecomputeFert();
            var near = w.SettlementNear(x, y, 8);
            w.Chronicle.Add(w, "curse", new EvData { Place = near != null ? near.Name : w.RegionNameAt(x, y, "the open country"), X = x, Y = y });
            w.Dirty.Features = true;
            return new PowerResult { Ok = true, X = x, Y = y };
        }

        public static PowerResult BlessLand(World w, double xIn, double yIn)
        {
            int x = RoundJs(xIn), y = RoundJs(yIn);
            if (!w.InB(x, y)) return new PowerResult { Ok = false, Msg = "Beyond the edge of the map." };
            if (!w.IsLandAt(x, y)) return new PowerResult { Ok = false, Msg = "Bless the soil, not the sea." };
            if (w.StormAt(x, y) != null) return new PowerResult { Ok = false, Msg = "The storm would devour the blessing — this sky is cursed." };
            if (w.BlessAt(x, y) != null) return new PowerResult { Ok = false, Msg = "This land already carries your favor." };
            w.Blesses.Add(new Zone { X = x, Y = y, R = 5, Start = w.Tick });
            w.RecomputeFert();
            var near = w.SettlementNear(x, y, 8);
            w.Chronicle.Add(w, "bless", new EvData { Place = near != null ? near.Name : w.RegionNameAt(x, y, "the open country"), X = x, Y = y });
            w.Dirty.Features = true;
            return new PowerResult { Ok = true, X = x, Y = y };
        }
    }
}
