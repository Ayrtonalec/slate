/* SLATE Phase 0 — headless integration test.
   Runs whole histories with no renderer: the design bible's promise that
   the world lives (and diverges) with zero divine input, made executable.
   Usage: node test/headless.js */
'use strict';
const fs = require('fs');
const path = require('path');
const vm = require('vm');

globalThis.SLATE = {};
const SRC = ['rng.js', 'names.js', 'worldgen.js', 'chronicle.js', 'world.js', 'sim.js', 'powers.js'];
for (const f of SRC) {
  vm.runInThisContext(fs.readFileSync(path.join(__dirname, '..', 'src', f), 'utf8'), { filename: f });
}
const S = globalThis.SLATE;

let failures = 0;
function check(label, cond, detail) {
  if (cond) console.log(`  ok    ${label}`);
  else { failures++; console.log(`  FAIL  ${label}${detail ? ' — ' + detail : ''}`); }
}

function stats(w) {
  const alive = w.aliveSettlements();
  const pop = alive.reduce((a, s) => a + s.pop, 0);
  const cultures = new Set(alive.map((s) => s.culture)).size;
  const byType = {};
  for (const e of w.chronicle.events) byType[e.type] = (byType[e.type] || 0) + 1;
  return { alive: alive.length, pop: Math.round(pop), cultures, ruins: w.ruins.length, events: w.chronicle.events.length, byType };
}

function asciiMap(w) {
  const rows = [];
  for (let y = 0; y < w.H; y += 3) {
    let row = '';
    for (let x = 0; x < w.W; x += 2) {
      const i = w.idx(x, y);
      const s = w.settlementNear(x, y, 1.4);
      if (s && !s.ruined) row += ['o', 'v', 'T', 'C'][s.tier];
      else if (w.height[i] <= w.sea) row += ' ';
      else if (w.biome[i] === S.B.MOUNTAIN) row += '^';
      else if (w.biome[i] === S.B.FOREST) row += '"';
      else row += '·';
    }
    rows.push(row);
  }
  return rows.join('\n');
}

// ---------- 1. Observer mode: five god-less 500-year histories ----------
console.log('\n=== Observer mode: 500 years, no god, seeds 1/2/3/7/42 ===');
const seeds = [1, 2, 3, 7, 42];
const outcomes = [];
for (const seed of seeds) {
  const t0 = Date.now();
  const w = S.world.create(seed);
  S.sim.runYears(w, 500);
  const ms = Date.now() - t0;
  const st = stats(w);
  outcomes.push(st);
  console.log(`seed ${String(seed).padStart(2)}: ${st.alive} alive, pop ${st.pop}, ` +
    `${st.cultures} cultures, ${st.ruins} ruins, ${st.events} events, ${ms}ms ` +
    `(dragons:${st.byType.dragon || 0} razed:${st.byType.razed || 0} gold:${st.byType.goldFound || 0} famine:${st.byType.famine || 0})`);
  check(`seed ${seed}: settlements in sane range`, st.alive >= 15 && st.alive <= 450, `got ${st.alive}`);
  check(`seed ${seed}: population sane`, st.pop >= 15000 && st.pop <= 4000000 && Number.isFinite(st.pop), `got ${st.pop}`);
  check(`seed ${seed}: >=2 cultures survive`, st.cultures >= 2, `got ${st.cultures}`);
  check(`seed ${seed}: chronicle is alive`, st.events >= 60, `got ${st.events}`);
  const napops = w.settlements.some((s) => !Number.isFinite(s.pop));
  check(`seed ${seed}: no NaN populations`, !napops);
}

// Divergence: seeds must produce different-shaped histories.
const aliveSpread = Math.max(...outcomes.map((o) => o.alive)) - Math.min(...outcomes.map((o) => o.alive));
check('divergence: settlement counts spread across seeds', aliveSpread >= 8, `spread ${aliveSpread}`);

// Determinism: same seed twice → identical history.
{
  const a = S.world.create(7); S.sim.runYears(a, 120);
  const b = S.world.create(7); S.sim.runYears(b, 120);
  const sa = JSON.stringify(a.chronicle.events.map((e) => [e.year, e.type, e.text]));
  const sb = JSON.stringify(b.chronicle.events.map((e) => [e.year, e.type, e.text]));
  check('determinism: seed 7 replays identically', sa === sb);
}

// ---------- 2. The gold cascade (founder example #1) ----------
console.log('\n=== Cascade: seed gold in empty mountains, expect a mining boom ===');
{
  const w = S.world.create(42);
  S.sim.runYears(w, 60);
  // Find wild mountains: a vein spot with no settlement within 6 but people within 16.
  let spot = null;
  outer:
  for (let y = 4; y < w.H - 4; y++) for (let x = 4; x < w.W - 4; x++) {
    if (w.biome[w.idx(x, y)] !== S.B.MOUNTAIN) continue;
    if (w.settlementNear(x, y, 6)) continue;
    if (!w.settlementNear(x, y, 16)) continue;
    spot = { x, y }; break outer;
  }
  check('found a wild mountain near civilization', !!spot);
  if (spot) {
    const before = w.chronicle.events.length;
    const res = S.powers.seedGold(w, spot.x, spot.y);
    check('seedGold accepted on mountain', res.ok);
    S.sim.runYears(w, 80);
    const after = w.chronicle.events.slice(before);
    const camp = after.find((e) => e.type === 'camp' || e.type === 'goldFound');
    check('a mining camp or strike follows within 80 years', !!camp, 'no camp/goldFound event');
    if (camp) {
      const settled = w.settlementNear(spot.x, spot.y, 5);
      check('someone now lives by the vein', !!settled && !settled.ruined);
      console.log(`  cascade: "${camp.text}" (Year ${camp.year})`);
      const drama = after.filter((e) => ['boom', 'town', 'dragon', 'raid', 'razed', 'slain'].includes(e.type));
      console.log(`  downstream drama events: ${drama.length}`);
    }
  }
  const rejected = S.powers.seedGold(w, 2, 2);
  check('seedGold rejected off high stone', !rejected.ok || (w.biome[w.idx(2, 2)] === S.B.MOUNTAIN || w.biome[w.idx(2, 2)] === S.B.HILLS));
}

// ---------- 3. The curse cascade (founder example #2) ----------
console.log('\n=== Cascade: curse a thriving coast, expect exodus ===');
{
  const w = S.world.create(7);
  S.sim.runYears(w, 120);
  const target = w.aliveSettlements().sort((a, b) => b.pop - a.pop)[0];
  check('found a thriving settlement', !!target && target.pop > 200, target && `pop ${Math.round(target.pop)}`);
  if (target) {
    const popBefore = target.pop;
    const res = S.powers.curseWeather(w, target.x, target.y);
    check('curseWeather accepted on land', res.ok);
    S.sim.runYears(w, 60);
    const collapsed = target.ruined || target.pop < popBefore * 0.5;
    check('the settlement collapses or empties within 60 years', collapsed,
      `pop ${Math.round(popBefore)} → ${Math.round(target.pop)}, ruined=${target.ruined}`);
    console.log(`  ${target.name}: pop ${Math.round(popBefore)} → ${target.ruined ? 'RUINS (Year ' + target.ruinedYear + ')' : Math.round(target.pop)}`);
  }
}

// ---------- 4. Blessing does something ----------
console.log('\n=== Cascade: bless empty land, expect settlers ===');
{
  const w = S.world.create(3);
  S.sim.runYears(w, 80);
  // Pick empty-but-livable plains (blessing barren tundra rightly does nothing).
  let spot = null, spotFert = 10;
  for (let y = 6; y < w.H - 6; y++) for (let x = 6; x < w.W - 6; x++) {
    const i = w.idx(x, y);
    if (!w.isLandAt(x, y) || w.biome[i] !== S.B.PLAINS) continue;
    if (w.settlementNear(x, y, 7)) continue;
    if (!w.settlementNear(x, y, 14)) continue;
    const f = w.fertAround(x, y, 2);
    if (f > spotFert) { spotFert = f; spot = { x, y }; }
  }
  if (spot) {
    const res = S.powers.blessLand(w, spot.x, spot.y);
    check('blessLand accepted', res.ok);
    S.sim.runYears(w, 100);
    const settled = w.settlementNear(spot.x, spot.y, 6);
    check('blessed land attracts settlement within 100 years', !!settled, 'still empty');
  } else {
    console.log('  (no suitable empty plains found on this seed — skipped)');
  }
}

// ---------- 5. Sample chronicle + map for eyeballing ----------
{
  const w = S.world.create(7);
  S.sim.runYears(w, 300);
  console.log('\n=== Seed 7, Year 300 — map ===');
  console.log(asciiMap(w));
  console.log('\n=== Seed 7 — chronicle excerpt (major events) ===');
  for (const e of w.chronicle.events.filter((e2) => e2.imp >= 2).slice(0, 25)) {
    console.log(`  Year ${String(e.year).padStart(4)} — ${e.text}`);
  }
  const md = S.chronicle.toMarkdown(w);
  check('\nchronicle exports to markdown', md.length > 1000 && md.includes('## Years'));
}

console.log(failures === 0 ? '\nALL CHECKS PASSED' : `\n${failures} CHECK(S) FAILED`);
process.exit(failures === 0 ? 0 : 1);
