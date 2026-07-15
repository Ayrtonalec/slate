// SLATE — procedural naming: cultures, places, people, regions.
// Port of god-sim-prototype/src/names.js. Four founding cultures, each with
// its own phoneme flavor so the map reads as a world with distinct peoples.
using System;
using System.Collections.Generic;

namespace Slate.Sim
{
    public sealed class Culture
    {
        public int Id;
        public string Name;
        public string ColorHex;       // presentation hint only; the sim never reads it
        public string Demonym;
        public string[] Starts;
        public string[] Ends;
        public string[] GoldEnds;
        public string[] People;
    }

    public static class Cultures
    {
        public static readonly Culture[] All =
        {
            new Culture
            {
                Id = 0, Name = "Aldish", ColorHex = "#55663B", Demonym = "the Aldish",
                Starts = new[] { "Ash", "Thorn", "Wex", "Bram", "Hart", "Elm", "Grim", "Wether", "Ryd", "Col", "Marl", "Oat", "Fenn", "Bar" },
                Ends = new[] { "ford", "stead", "hollow", "wick", "bury", "combe", "field", "mere", "don", "leigh", "thorpe", "bridge" },
                GoldEnds = new[] { "delve", "hollow", "wick", "ford" },
                People = new[] { "Osric", "Aldwyn", "Berta", "Cedd", "Godgifu", "Hild", "Leof", "Wulfa", "Eda", "Sigred", "Oswin", "Merewen" },
            },
            new Culture
            {
                Id = 1, Name = "Vasker", ColorHex = "#4A6274", Demonym = "the Vasker",
                Starts = new[] { "Skjal", "Hrafn", "Ulf", "Gren", "Kald", "Bjor", "Stein", "Varg", "Eld", "Snor", "Hval", "Jarn" },
                Ends = new[] { "vik", "heim", "fjall", "strand", "nes", "dal", "holm", "gard", "foss", "havn" },
                GoldEnds = new[] { "gruva", "delv", "heim", "gard" },
                People = new[] { "Ragna", "Torvald", "Sigrun", "Eirik", "Halla", "Bjarke", "Yrsa", "Knut", "Solveig", "Orm", "Astrid", "Geir" },
            },
            new Culture
            {
                Id = 2, Name = "Serai", ColorHex = "#A2652F", Demonym = "the Serai",
                Starts = new[] { "Al-Qas", "Zafir", "Mira", "Sahl", "Dar", "Kal", "Azar", "Nur", "Rasha", "Tal", "Zeyd", "Har" },
                Ends = new[] { "ir", "aba", "oun", "esh", "ara", "im", "at", "ez", "ula", "an" },
                GoldEnds = new[] { "-dhahab", "ara", "ir", "esh" },
                People = new[] { "Zahra", "Idris", "Layl", "Basim", "Naima", "Tariq", "Suheir", "Omar", "Yasmin", "Khalid", "Farah", "Nadim" },
            },
            new Culture
            {
                Id = 3, Name = "Tessian", ColorHex = "#6E4A5E", Demonym = "the Tessians",
                Starts = new[] { "Val", "Cor", "Aur", "Sept", "Mar", "Luc", "Tarr", "Vin", "Cael", "Ost", "Pell", "Riv" },
                Ends = new[] { "ium", "ora", "essa", "anum", "ola", "is", "urnum", "atia", "ento", "aris" },
                GoldEnds = new[] { "aurum", "ora", "ium" },
                People = new[] { "Livia", "Cassian", "Aurel", "Petra", "Marcus", "Octavia", "Loran", "Vitus", "Sabina", "Tullo", "Camilla", "Renz" },
            },
        };
    }

    public sealed class Namer
    {
        private static readonly string[] RegionAdj =
        {
            "Gray", "Amber", "Ashen", "Whispering", "Sundered", "Elder", "Silent", "Golden", "Thorn",
            "Mist", "Winter", "Red", "Hollow", "Storm", "Far", "Sleeping", "Broken", "Pale", "Iron", "Weeping",
        };
        private static readonly string[] RangeEnd = { "Peaks", "Spine", "Crowns", "Reach", "Teeth", "Fells", "Wall" };
        private static readonly string[] ForestEnd = { "Weald", "Wood", "Deepwood", "Tangle", "Wilds" };
        private static readonly string[] SeaEnd = { "Deep", "Main", "Expanse", "Mirror", "Gulf" };
        private static readonly string[] DesertEnd = { "Waste", "Sands", "Reach" };
        private static readonly string[] DragonNames = { "Vharax", "Skorn", "Ymmeth", "Cauldrax", "Nyr", "Vellumbra", "Oroth", "Kazmyre", "Sarquel", "Draumr" };
        private static readonly string[] ForestSuffixLower = { "wood", "weald" };
        private static readonly string[] OceanAdj = { "Endless", "Outer", "Sunless", "Wide" };
        private static readonly string[] OceanEnd = { "Ocean", "Main" };

        private readonly Rng _rng;
        private readonly HashSet<string> _used = new HashSet<string>();

        public Namer(int seed)
        {
            _rng = new Rng(seed, "names");
        }

        private string Unique(Func<string> make)
        {
            for (int i = 0; i < 40; i++)
            {
                var n = make();
                if (!_used.Contains(n)) { _used.Add(n); return n; }
            }
            // Give up on uniqueness gracefully rather than looping forever.
            var fallback = make() + " " + _rng.Int(2, 9);
            _used.Add(fallback);
            return fallback;
        }

        private static string JoinName(string a, string b)
        {
            if (b.StartsWith("-")) return a + b.Substring(1);
            // Avoid awkward duplicate letters at the seam ("Thornnes" -> "Thornes").
            if (a[a.Length - 1] == b[0]) return a + b.Substring(1);
            return a + b;
        }

        public string Place(int cultureId)
        {
            var c = Cultures.All[cultureId];
            return Unique(() => JoinName(_rng.Pick(c.Starts), _rng.Pick(c.Ends)));
        }

        public string GoldPlace(int cultureId)
        {
            var c = Cultures.All[cultureId];
            return Unique(() =>
            {
                if (_rng.Chance(0.5)) return JoinName("Gold", _rng.Pick(c.GoldEnds));
                return JoinName(_rng.Pick(c.Starts), _rng.Pick(c.GoldEnds));
            });
        }

        public string Person(int cultureId) => _rng.Pick(Cultures.All[cultureId].People);

        public string Region(string kind)
        {
            return Unique(() =>
            {
                switch (kind)
                {
                    case "range": return "The " + _rng.Pick(RegionAdj) + " " + _rng.Pick(RangeEnd);
                    case "forest":
                        return _rng.Chance(0.5)
                            ? "The " + _rng.Pick(RegionAdj) + " " + _rng.Pick(ForestEnd)
                            : _rng.Pick(RegionAdj) + _rng.Pick(ForestSuffixLower);
                    case "sea": return "The " + _rng.Pick(RegionAdj) + " " + _rng.Pick(SeaEnd);
                    case "ocean": return "The " + _rng.Pick(OceanAdj) + " " + _rng.Pick(OceanEnd);
                    case "desert": return "The " + _rng.Pick(RegionAdj) + " " + _rng.Pick(DesertEnd);
                    default: return "The " + _rng.Pick(RegionAdj) + " Land";
                }
            });
        }

        public string Dragon() => _rng.Pick(DragonNames);

        private static readonly string[] BattlefieldForms =
        {
            "the Field of {0}", "{0} Field", "the Red Meadow of {0}",
            "the Ford of {0}", "the Bonefield of {0}", "the Weeping of {0}",
        };

        // Battlefields are named for the place they nearly destroyed.
        public string Battlefield(string near)
            => string.Format(_rng.Pick(BattlefieldForms), near);
    }
}
