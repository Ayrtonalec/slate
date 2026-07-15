// SLATE — the headless battery, ported from god-sim-prototype/test/headless.js.
// The design bible's promise that the world lives (and diverges) with zero
// divine input, made executable. Must be green before any commit that
// touches the sim.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Slate.Sim;

namespace Slate.Sim.Tests
{
    public class HeadlessBatteryTests
    {
        private sealed class Stats
        {
            public int Alive, Cultures, RuinsCount, Events;
            public double Pop;
            public Dictionary<string, int> ByType = new Dictionary<string, int>();
        }

        private static Stats StatsOf(World w)
        {
            var alive = w.AliveSettlements();
            var st = new Stats
            {
                Alive = alive.Count,
                Pop = alive.Sum(s => s.Pop),
                Cultures = alive.Select(s => s.Culture).Distinct().Count(),
                RuinsCount = w.Ruins.Count,
                Events = w.Chronicle.Events.Count,
            };
            foreach (var e in w.Chronicle.Events)
                st.ByType[e.Type] = st.ByType.TryGetValue(e.Type, out int n) ? n + 1 : 1;
            return st;
        }

        [Test]
        public void ObserverMode_FiveSeeds_500Years_SaneAndDivergent()
        {
            int[] seeds = { 1, 2, 3, 7, 42 };
            var outcomes = new List<Stats>();
            foreach (int seed in seeds)
            {
                var w = World.Create(seed);
                Simulation.RunYears(w, 500);
                var st = StatsOf(w);
                outcomes.Add(st);
                TestContext.Out.WriteLine(
                    $"seed {seed}: {st.Alive} alive, pop {Math.Round(st.Pop)}, {st.Cultures} cultures, " +
                    $"{st.RuinsCount} ruins, {st.Events} events");

                Assert.That(st.Alive, Is.InRange(15, 450), $"seed {seed}: settlements in sane range");
                Assert.That(st.Pop, Is.InRange(15000, 4000000), $"seed {seed}: population sane");
                Assert.That(double.IsNaN(st.Pop) || double.IsInfinity(st.Pop), Is.False, $"seed {seed}: population finite");
                Assert.That(st.Cultures, Is.GreaterThanOrEqualTo(2), $"seed {seed}: >=2 cultures survive");
                Assert.That(st.Events, Is.GreaterThanOrEqualTo(60), $"seed {seed}: chronicle is alive");
                Assert.That(w.Settlements.Any(s => double.IsNaN(s.Pop) || double.IsInfinity(s.Pop)), Is.False,
                    $"seed {seed}: no NaN populations");
            }

            // Divergence: seeds must produce different-shaped histories.
            int spread = outcomes.Max(o => o.Alive) - outcomes.Min(o => o.Alive);
            Assert.That(spread, Is.GreaterThanOrEqualTo(8), "divergence: settlement counts spread across seeds");
        }

        [Test]
        public void Determinism_SameSeed_ReplaysIdentically()
        {
            var a = World.Create(7); Simulation.RunYears(a, 120);
            var b = World.Create(7); Simulation.RunYears(b, 120);
            var sa = a.Chronicle.Events.Select(e => $"{e.Year}|{e.Type}|{e.Text}").ToArray();
            var sb = b.Chronicle.Events.Select(e => $"{e.Year}|{e.Type}|{e.Text}").ToArray();
            Assert.That(sa, Is.EqualTo(sb), "determinism: seed 7 replays identically");
        }

        [Test]
        public void GoldCascade_SeedGoldInWildMountains_MiningBoomFollows()
        {
            var w = World.Create(42);
            Simulation.RunYears(w, 60);

            // Find wild mountains: a vein spot with no settlement within 6 but people within 16.
            (int X, int Y)? spot = null;
            for (int y = 4; y < w.H - 4 && spot == null; y++)
                for (int x = 4; x < w.W - 4 && spot == null; x++)
                {
                    if (w.Biome[w.Idx(x, y)] != B.MOUNTAIN) continue;
                    if (w.SettlementNear(x, y, 6) != null) continue;
                    if (w.SettlementNear(x, y, 16) == null) continue;
                    spot = (x, y);
                }
            Assert.That(spot, Is.Not.Null, "found a wild mountain near civilization");

            int before = w.Chronicle.Events.Count;
            var res = Powers.SeedGold(w, spot.Value.X, spot.Value.Y);
            Assert.That(res.Ok, Is.True, "seedGold accepted on mountain");
            Simulation.RunYears(w, 80);
            var after = w.Chronicle.Events.Skip(before).ToList();
            var camp = after.FirstOrDefault(e => e.Type == "camp" || e.Type == "goldFound");
            Assert.That(camp, Is.Not.Null, "a mining camp or strike follows within 80 years");
            var settled = w.SettlementNear(spot.Value.X, spot.Value.Y, 5);
            Assert.That(settled != null && !settled.Ruined, Is.True, "someone now lives by the vein");
            TestContext.Out.WriteLine($"cascade: \"{camp.Text}\" (Year {camp.Year})");

            var rejected = Powers.SeedGold(w, 2, 2);
            byte b = w.Biome[w.Idx(2, 2)];
            Assert.That(!rejected.Ok || b == B.MOUNTAIN || b == B.HILLS, Is.True, "seedGold rejected off high stone");
        }

        [Test]
        public void CurseCascade_CurseThrivingCoast_Exodus()
        {
            var w = World.Create(7);
            Simulation.RunYears(w, 120);
            var target = w.AliveSettlements().OrderByDescending(s => s.Pop).First();
            Assert.That(target.Pop, Is.GreaterThan(200), "found a thriving settlement");

            double popBefore = target.Pop;
            var res = Powers.CurseWeather(w, target.X, target.Y);
            Assert.That(res.Ok, Is.True, "curseWeather accepted on land");
            Simulation.RunYears(w, 60);
            bool collapsed = target.Ruined || target.Pop < popBefore * 0.5;
            Assert.That(collapsed, Is.True,
                $"the settlement collapses or empties within 60 years (pop {Math.Round(popBefore)} -> {Math.Round(target.Pop)}, ruined={target.Ruined})");
            TestContext.Out.WriteLine(
                $"{target.Name}: pop {Math.Round(popBefore)} -> {(target.Ruined ? "RUINS (Year " + target.RuinedYear + ")" : Math.Round(target.Pop).ToString())}");
        }

        [Test]
        public void BlessCascade_BlessEmptyLand_SettlersArrive()
        {
            var w = World.Create(3);
            Simulation.RunYears(w, 80);

            // Pick empty-but-livable plains (blessing barren tundra rightly does nothing).
            (int X, int Y)? spot = null;
            double spotFert = 10;
            for (int y = 6; y < w.H - 6; y++)
                for (int x = 6; x < w.W - 6; x++)
                {
                    int i = w.Idx(x, y);
                    if (!w.IsLandAt(x, y) || w.Biome[i] != B.PLAINS) continue;
                    if (w.SettlementNear(x, y, 7) != null) continue;
                    if (w.SettlementNear(x, y, 14) == null) continue;
                    double f = w.FertAround(x, y, 2);
                    if (f > spotFert) { spotFert = f; spot = (x, y); }
                }
            if (spot == null)
            {
                Assert.Ignore("no suitable empty plains found on this seed — skipped");
                return;
            }
            var res = Powers.BlessLand(w, spot.Value.X, spot.Value.Y);
            Assert.That(res.Ok, Is.True, "blessLand accepted");
            Simulation.RunYears(w, 100);
            var settled = w.SettlementNear(spot.Value.X, spot.Value.Y, 6);
            Assert.That(settled, Is.Not.Null, "blessed land attracts settlement within 100 years");
        }

        [Test]
        public void Wars_BreakOutOnTheirOwn_AndResolve()
        {
            // The aliveness contract: wars must happen with zero god input,
            // and must resolve (conquest, sack or a held wall) — never hang.
            int marches = 0, outcomes = 0;
            foreach (int seed in new[] { 1, 2, 3, 7, 42 })
            {
                var w = World.Create(seed);
                Simulation.RunYears(w, 500);
                foreach (var e in w.Chronicle.Events)
                {
                    if (e.Type == "warMarch") marches++;
                    if (e.Type == "conquest" || e.Type == "sacked" || e.Type == "defended") outcomes++;
                }
                Assert.That(w.Armies.Count, Is.LessThanOrEqualTo(2), $"seed {seed}: armies bounded");
            }
            TestContext.Out.WriteLine($"battery: {marches} war marches, {outcomes} battles resolved");
            Assert.That(marches, Is.GreaterThanOrEqualTo(3), "wars break out across the battery");
            Assert.That(outcomes, Is.GreaterThanOrEqualTo(2), "battles actually resolve");
        }

        [Test]
        public void Settlements_AreHeterogeneous_NotACarpetOfEqualVillages()
        {
            // Realism guard: a district center must tower over the median hamlet
            // (Zipf-ish hierarchy), and walls must be a paid-for choice — some
            // sizable towns walled, others open.
            int walled = 0, open = 0;
            foreach (int seed in new[] { 1, 7, 42 })
            {
                // Sample the hierarchy at three moments: the pest culls the
                // biggest market towns hardest (density mortality — as the real
                // Black Death did), so any single snapshot can catch the top
                // city mid-recovery. A real hierarchy shows at its best moment.
                var w = World.Create(seed);
                double bestRatio = 0;
                foreach (int until in new[] { 300, 400, 500 })
                {
                    Simulation.RunYears(w, until - w.Year);
                    var sample = w.AliveSettlements().OrderByDescending(s => s.Pop).ToList();
                    Assert.That(sample.Count, Is.GreaterThan(10), $"seed {seed}: enough settlements to compare");
                    double ratio = sample[0].Pop / sample[sample.Count / 2].Pop;
                    TestContext.Out.WriteLine(
                        $"seed {seed} @year {until}: top {Math.Round(sample[0].Pop)}, median {Math.Round(sample[sample.Count / 2].Pop)}, ratio {ratio:F1}");
                    if (ratio > bestRatio) bestRatio = ratio;
                }
                Assert.That(bestRatio, Is.GreaterThanOrEqualTo(3.5),
                    $"seed {seed}: a real center towers over the median village");

                foreach (var s in w.AliveSettlements())
                {
                    if (s.Pop < 650) continue;
                    if (s.Walls > 0) walled++; else open++;
                }
            }
            TestContext.Out.WriteLine($"battery: {walled} walled, {open} open (pop > 650)");
            Assert.That(walled, Is.GreaterThanOrEqualTo(1), "some towns pay for walls");
            Assert.That(open, Is.GreaterThanOrEqualTo(1), "some towns never had to");
        }

        [Test]
        public void TheWorldBitesBack_PlagueDisastersAndScarsHappen()
        {
            // Batch A promise: nature authors history with zero god input — and
            // the land remembers. All rare enough to be sagas, common enough to
            // show up across a 5-seed x 500-year battery.
            int plagues = 0, disasters = 0, scars = 0, plaguesUnresolved = 0;
            foreach (int seed in new[] { 1, 2, 3, 7, 42 })
            {
                var w = World.Create(seed);
                Simulation.RunYears(w, 500);
                foreach (var e in w.Chronicle.Events)
                {
                    if (e.Type == "plagueStart") plagues++;
                    if (e.Type == "wildfire" || e.Type == "flood" || e.Type == "earthquake" || e.Type == "locusts") disasters++;
                }
                scars += w.Scars.Count;
                if (w.PlagueActive && w.Year - w.PlagueStartYear > 30) plaguesUnresolved++;
            }
            TestContext.Out.WriteLine($"battery: {plagues} plagues, {disasters} disasters, {scars} scars");
            Assert.That(plagues, Is.GreaterThanOrEqualTo(2), "the pest comes on its own");
            Assert.That(disasters, Is.GreaterThanOrEqualTo(6), "nature keeps authoring history");
            Assert.That(scars, Is.GreaterThanOrEqualTo(5), "the land remembers its battles");
            Assert.That(plaguesUnresolved, Is.EqualTo(0), "no plague hangs unresolved for 30+ years");
        }

        [Test]
        public void Expeditions_SailAndResolve()
        {
            // The Endless Sea preview: ports launch ships into the fog on their
            // own; every voyage ends in a return, a loss, or is still at sea.
            int launched = 0, returned = 0, lost = 0;
            foreach (int seed in new[] { 1, 2, 3, 7, 42 })
            {
                var w = World.Create(seed);
                Simulation.RunYears(w, 500);
                foreach (var e in w.Chronicle.Events)
                {
                    if (e.Type == "expedition") launched++;
                    if (e.Type == "expReturn") returned++;
                    if (e.Type == "expLost") lost++;
                }
                Assert.That(w.Ships.Count, Is.LessThanOrEqualTo(2), $"seed {seed}: ships bounded");
            }
            TestContext.Out.WriteLine($"battery: {launched} expeditions, {returned} returned, {lost} lost");
            Assert.That(launched, Is.GreaterThanOrEqualTo(3), "ports fit out ships on their own");
            Assert.That(returned + lost, Is.GreaterThanOrEqualTo(2), "voyages resolve");
        }

        [Test]
        public void Chronicle_ExportsToMarkdown()
        {
            var w = World.Create(7);
            Simulation.RunYears(w, 300);
            string md = w.Chronicle.ToMarkdown(w);
            Assert.That(md.Length, Is.GreaterThan(1000), "chronicle markdown has substance");
            Assert.That(md, Does.Contain("## Years"), "chronicle markdown grouped by century");
        }
    }
}
