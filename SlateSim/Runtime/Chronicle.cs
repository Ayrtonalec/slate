// SLATE — the Chronicle: an append-only log of everything that happens,
// with prose templates. Port of god-sim-prototype/src/chronicle.js.
// The sim emits facts; the chronicle renders them as history.
using System;
using System.Collections.Generic;
using System.Text;

namespace Slate.Sim
{
    public sealed class ChronicleEvent
    {
        public int Id;
        public string Type;
        public int Imp;      // 1 minor, 2 notable, 3 major
        public string Tone;  // plain | god | doom
        public string Text;
        public int Year, Month;
        public double X, Y;
        public double Fx = double.NaN, Fy = double.NaN; // optional origin, for renderer arrows
    }

    // Loose bag of template arguments, mirroring the JS data object.
    public sealed class EvData
    {
        public string Name, Parent, Region, Dragon, Hero, A, B, From, To, Place, Why, RuinName;
        public int Culture = -1;
        public int Tier;
        public int Pop;
        public double X, Y;
        public double Fx = double.NaN, Fy = double.NaN;
    }

    public sealed class Chronicle
    {
        public List<ChronicleEvent> Events = new List<ChronicleEvent>();
        public int NextId = 1;

        private static (int imp, string tone, string text) Render(string type, EvData d)
        {
            switch (type)
            {
                case "landing": return (2, "plain", $"{Cultures.All[d.Culture].Demonym} come ashore; {d.Name} is raised where the boats were drawn up.");
                case "found": return (1, "plain", $"{d.Name} founded by settlers out of {d.Parent}.");
                case "camp": return (2, "plain", $"A mining camp takes root beneath {d.Region}; they call it {d.Name}.");
                case "village": return (1, "plain", $"{d.Name} grows into a proper village.");
                case "town": return (2, "plain", $"{d.Name} has grown into a town of some {d.Pop} souls."); // walls are earned separately now
                case "city": return (3, "plain", $"{d.Name} is now a city, greatest of {Cultures.All[d.Culture].Demonym}' holdings.");
                case "goldSeed": return (3, "god", $"A vein of gold blooms in the stone of {d.Region}. The mountain did not hold it yesterday.");
                case "goldFound": return (2, "plain", $"Prospectors strike gold near {d.Name}. Hungry men arrive within the season.");
                case "boom": return (2, "plain", $"{d.Name} swells with fortune-seekers and grows rich on the diggings.");
                case "famine": return (2, "plain", $"Lean years strike {d.Name}; the granaries echo.");
                case "abandon": return (d.Tier >= 2 ? 3 : 2, "plain", $"{d.Name} stands empty{(d.Why != null ? " — " + d.Why : "")}. The roads grow over.");
                case "curse": return (3, "god", $"The skies over {d.Place} turn against the living. The storm does not pass. The priests say something is angry.");
                case "stormExodus": return (2, "doom", $"The people of {d.Name} abandon their homes to the endless storm.");
                case "bless": return (3, "god", $"A gentleness settles on the land near {d.Place}; seeds take root wherever they fall. Shrines appear by the field-edges.");
                case "dragon": return (3, "doom", $"A wyrm has nested in {d.Region}. They name it {d.Dragon}. Its shadow falls across {d.Name}.");
                case "raid": return (2, "doom", $"{d.Dragon} descends upon {d.Name}; the granaries burn.");
                case "razed": return (3, "doom", $"{d.Name} is no more. Men will call that place {d.RuinName}.");
                case "slain": return (3, "plain", $"{d.Hero} of {d.Name} slays {d.Dragon} in {d.Region}. The hoard comes down the mountain in carts.");
                case "dragonGone": return (2, "plain", $"{d.Dragon}, finding nothing left worth coveting, flies beyond the map's edge.");
                case "road": return (1, "plain", $"A road now runs between {d.A} and {d.B}.");
                case "migration": return (1, "plain", $"Refugees out of {d.From} settle in {d.To}.");
                case "warMarch": return (3, "doom", $"War: {Cultures.All[d.Culture].Demonym} of {d.From} raise {d.Pop} spears and march on {d.Name}.");
                case "conquest": return (3, "doom", $"{d.Name} falls to {Cultures.All[d.Culture].Demonym}; new banners fly from its rooftops.");
                case "sacked": return (3, "doom", $"{d.Name} is put to the torch by {Cultures.All[d.Culture].Demonym}. The survivors scatter into the hills.");
                case "defended": return (2, "plain", $"{d.Name} throws back {Cultures.All[d.Culture].Demonym}; the fields are red, but the walls hold.");
                case "warOver": return (1, "plain", $"The warband out of {d.From} breaks up and drifts home; there was nothing left to fight for.");
                case "palisade": return (1, "plain", $"{d.Name} rings itself with a timber palisade; the times demand it.");
                case "stonewalls": return (2, "plain", $"{d.Name} raises walls of dressed stone. Masons eat well for a decade.");
                case "plagueStart": return (3, "doom", $"The pest comes to {d.Name}; by market-day it is in every street.");
                case "plagueTown": return (2, "doom", $"The pest reaches {d.Name}; the bells toll until the ringers sicken.");
                case "plagueEnd": return (3, "plain", $"After {d.Pop} years the pestilence burns out. The dead are beyond counting; the living inherit their fields.");
                case "wildfire": return (3, "doom", $"Fire takes {d.Region}; for a season the sun sets brown.");
                case "burnedTown": return (2, "doom", $"{d.Name} burns to the ground as the fire passes.");
                case "flood": return (2, "doom", $"The river rises at {d.Name}; boats row down the high street. The silt will feed a generation.");
                case "earthquake": return (3, "doom", $"The earth heaves under {d.Region}. Bells ring with no hands on the ropes.");
                case "wallsFell": return (2, "doom", $"The walls of {d.Name} split and slump. Masons will eat well; so will wolves.");
                case "locusts": return (2, "doom", $"A darkness of wings crosses {d.Region}, eating the year down to stubble.");
                default: throw new ArgumentException("unknown chronicle type: " + type);
            }
        }

        public ChronicleEvent Add(World world, string type, EvData data)
        {
            var (imp, tone, text) = Render(type, data);
            var e = new ChronicleEvent
            {
                Id = NextId++,
                Type = type, Imp = imp, Tone = tone, Text = text,
                Year = world.Year, Month = world.Month,
                X = data.X, Y = data.Y,
                Fx = data.Fx, Fy = data.Fy,
            };
            Events.Add(e);
            world.OnEvent?.Invoke(e);
            return e;
        }

        public string ToMarkdown(World world)
        {
            var sb = new StringBuilder();
            sb.Append("# The Chronicle of ").Append(world.Title ?? "an Unnamed World").Append('\n');
            sb.Append('\n');
            sb.Append("*World seed `").Append(world.Seed).Append("` · years 0–").Append(world.Year)
              .Append(" · set down by no mortal hand.*\n");
            sb.Append('\n');
            int century = -1;
            foreach (var e in Events)
            {
                int c = e.Year / 100;
                if (c != century)
                {
                    century = c;
                    sb.Append("## Years ").Append(c * 100).Append('–').Append(c * 100 + 99).Append('\n');
                    sb.Append('\n');
                }
                string mark = e.Tone == "god" ? " ✦" : e.Tone == "doom" ? " †" : "";
                sb.Append("- **Year ").Append(e.Year).Append("**").Append(mark).Append(" — ").Append(e.Text).Append('\n');
            }
            sb.Append('\n');
            sb.Append("---\n");
            int alive = 0;
            foreach (var s in world.Settlements) if (!s.Ruined) alive++;
            sb.Append("*As of Year ").Append(world.Year).Append(": ").Append(alive).Append(" living settlements, ")
              .Append(world.Ruins.Count).Append(" ruins, ").Append(Events.Count).Append(" recorded events.*");
            return sb.ToString();
        }
    }
}
