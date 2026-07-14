/* SLATE Phase 0 — world state container + shared queries.
   Wraps the generated geography with runtime state (settlements, storms,
   dragons, roads, chronicle) and the helper functions every system uses. */
(function (S) {
  'use strict';
  const B = () => S.B;

  const MONTHS = ['Thaw', 'Seedtime', 'Bloom', 'Highsun', 'Longsun', 'Harvest',
    'Gleaning', 'Mistfall', 'Frostgate', 'Deepwinter', 'Icewane', 'Stirring'];
  const TIER_NAMES = ['hamlet', 'village', 'town', 'city'];
  const TIER_POP = [0, 450, 1300, 3200];
  const RUIN_NAMES = ['Dragonfall', 'Wyrmscar', 'Ashmark', 'Cinderrest'];

  function create(seed) {
    const w = S.worldgen.generate(seed);
    w.settlements = [];
    w.ruins = [];        // {x,y,name,year,tier,cause}
    w.storms = [];       // {x,y,r}
    w.blesses = [];      // {x,y,r}
    w.dragons = [];      // {name,x,y,bornYear,lastRaid,raidEvery,regionName}
    w.deadLairs = [];
    w.roads = [];        // {a,b,pts}
    w.roadDeg = {};      // settlement id -> degree
    w.claims = new Int16Array(w.W * w.H).fill(-1);
    w.settleGrid = new Int16Array(w.W * w.H).fill(-1); // cell -> alive settlement id
    w.settlementsById = [];
    w.tick = 0; w.month = 0; w.year = 0;
    w.chronicle = S.chronicle.create();
    w.rngSim = S.rng.makeRng(seed, 'sim');
    w.nextSettlementId = 1;
    w.leanYears = 0;
    w.fertDirty = false;
    w.dirty = { terrain: true, features: true, borders: true, labels: true };
    w.title = 'Seed ' + seed;

    attachHelpers(w);

    // Founding landings: each culture puts two hamlets ashore near its homeland.
    w.homelands.forEach((h, ci) => {
      if (ci >= S.CULTURES.length) return;
      const first = w.bestSiteNear(h.x, h.y, 0, 5);
      if (!first) return;
      const a = w.addSettlement({ x: first.x, y: first.y, culture: ci, pop: w.rngSim.int(90, 150) });
      S.chronicle.add(w, 'landing', { culture: ci, name: a.name, x: a.x, y: a.y });
      const second = w.bestSiteNear(h.x, h.y, 3, 8);
      if (second) {
        const b2 = w.addSettlement({ x: second.x, y: second.y, culture: ci, pop: w.rngSim.int(60, 110) });
        S.chronicle.add(w, 'found', { name: b2.name, parent: a.name, x: b2.x, y: b2.y });
      }
    });
    w.recomputeClaims();
    return w;
  }

  function attachHelpers(w) {
    const { W, H, idx } = w;
    w.inB = (x, y) => x >= 0 && y >= 0 && x < W && y < H;
    w.isLandAt = (x, y) => w.inB(x, y) && w.height[idx(x, y)] > w.sea;
    w.monthName = () => MONTHS[w.month];
    w.tierName = (t) => TIER_NAMES[t];
    w.tierOf = (pop) => pop >= TIER_POP[3] ? 3 : pop >= TIER_POP[2] ? 2 : pop >= TIER_POP[1] ? 1 : 0;

    w.fertAround = (x, y, r) => {
      let f = 0;
      for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
        const nx = x + dx, ny = y + dy;
        if (w.inB(nx, ny)) f += w.fert[idx(nx, ny)];
      }
      return f;
    };
    w.zoneAt = (zones, x, y) => {
      for (const z of zones) {
        const d2 = (z.x - x) ** 2 + (z.y - y) ** 2;
        if (d2 <= z.r * z.r) return z;
      }
      return null;
    };
    w.stormAt = (x, y) => w.zoneAt(w.storms, x, y);
    w.blessAt = (x, y) => w.zoneAt(w.blesses, x, y);

    w.recomputeFert = () => {
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const i = idx(x, y);
        let f = w.fertBase[i];
        if (w.blessAt(x, y)) f *= 1.7;
        if (w.stormAt(x, y)) f *= 0.12;
        w.fert[i] = f;
      }
      w.fertDirty = true;
      w.dirty.terrain = true;
    };

    w.settlementNear = (x, y, r) => {
      let best = null, bd = (r + 0.01) ** 2;
      if (r <= 6.5) {
        // Small radius: scan the occupancy grid, not the settlement list.
        const R = Math.ceil(r);
        for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) {
          const nx = x + dx, ny = y + dy;
          if (!w.inB(nx, ny)) continue;
          const id = w.settleGrid[idx(nx, ny)];
          if (id < 0) continue;
          const s = w.settlementsById[id];
          const d2 = (s.x - x) ** 2 + (s.y - y) ** 2;
          if (d2 < bd) { bd = d2; best = s; }
        }
        return best;
      }
      for (const s of w.settlements) {
        if (s.ruined) continue;
        const d2 = (s.x - x) ** 2 + (s.y - y) ** 2;
        if (d2 < bd) { bd = d2; best = s; }
      }
      return best;
    };

    w.hasFishAdj = (x, y) => {
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
        const nx = x + dx, ny = y + dy;
        if (w.inB(nx, ny) && w.resource[idx(nx, ny)] === 2) return true;
      }
      return false;
    };

    w.goldVeinNear = (x, y, r, revealedOnly) => {
      for (const v of w.veins) {
        if (revealedOnly && !v.revealed) continue;
        if ((v.x - x) ** 2 + (v.y - y) ** 2 <= r * r) return v;
      }
      return null;
    };

    w.siteScore = (x, y, nearList) => {
      if (!w.isLandAt(x, y)) return -Infinity;
      const i = idx(x, y);
      const b = w.biome[i];
      if (b === S.B.MOUNTAIN || b === S.B.SNOW) return -Infinity;
      if (nearList) {
        for (const s of nearList) {
          if ((s.x - x) ** 2 + (s.y - y) ** 2 <= 16) return -Infinity; // 4 cell spacing
        }
      } else if (w.settlementNear(x, y, 4)) return -Infinity;
      if (w.stormAt(x, y)) return -Infinity;
      let score = w.fertAround(x, y, 2) * 1.0;
      if (w.blessAt(x, y)) score += 3; // the land feels kind
      if (w.river[i]) score += 2.5;
      if (w.seaDist[i] <= 1) score += 1.5;
      if (w.hasFishAdj(x, y)) score += 2;
      if (w.goldVeinNear(x, y, 3, true)) score += 9;
      return score;
    };

    w.bestSiteNear = (x, y, rmin, rmax) => {
      // Gather nearby settlements once; per-cell spacing checks use this short list.
      const nearList = [];
      const R2 = (rmax + 4) ** 2;
      for (const s of w.settlements) {
        if (!s.ruined && (s.x - x) ** 2 + (s.y - y) ** 2 <= R2) nearList.push(s);
      }
      let best = null, bs = 8.5; // minimum livable score — marginal land stays wild
      for (let dy = -rmax; dy <= rmax; dy++) for (let dx = -rmax; dx <= rmax; dx++) {
        const d2 = dx * dx + dy * dy;
        if (d2 < rmin * rmin || d2 > rmax * rmax) continue;
        const nx = x + dx, ny = y + dy;
        if (!w.inB(nx, ny)) continue;
        const sc = w.siteScore(nx, ny, nearList) - Math.sqrt(d2) * 0.15;
        if (sc > bs) { bs = sc; best = { x: nx, y: ny, score: sc }; }
      }
      return best;
    };

    w.campSiteNear = (x, y) => {
      // Miners settle rough country: pick the best non-mountain cell beside
      // the vein; if the range is solid stone, they camp on the stone itself.
      let best = null, bs = -Infinity, fallback = null;
      for (let dy = -3; dy <= 3; dy++) for (let dx = -3; dx <= 3; dx++) {
        const nx = x + dx, ny = y + dy;
        if (!w.inB(nx, ny) || !w.isLandAt(nx, ny)) continue;
        if (w.settlementNear(nx, ny, 2.5) || w.stormAt(nx, ny)) continue;
        const b = w.biome[idx(nx, ny)];
        const d = Math.sqrt(dx * dx + dy * dy);
        if (b === S.B.MOUNTAIN || b === S.B.SNOW) {
          if (!fallback && d <= 2) fallback = { x: nx, y: ny };
          continue;
        }
        const sc = w.fertAround(nx, ny, 1) - d * 0.3;
        if (sc > bs) { bs = sc; best = { x: nx, y: ny }; }
      }
      return best || fallback;
    };

    w.nearestMountain = (x, y, rmax) => {
      let best = null, bd = Infinity;
      for (let dy = -rmax; dy <= rmax; dy++) for (let dx = -rmax; dx <= rmax; dx++) {
        const nx = x + dx, ny = y + dy;
        if (!w.inB(nx, ny) || w.biome[idx(nx, ny)] !== S.B.MOUNTAIN) continue;
        const d2 = dx * dx + dy * dy;
        if (d2 < bd) { bd = d2; best = { x: nx, y: ny }; }
      }
      return best;
    };

    w.regionNameAt = (x, y, fallback) => {
      const rid = w.regionOf[idx(x, y)];
      if (rid >= 0) return w.regions[rid].name;
      // Look a little around for a named region (labels feel better than "the wilds").
      for (let r = 1; r <= 4; r++) {
        for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
          const nx = x + dx, ny = y + dy;
          if (!w.inB(nx, ny)) continue;
          const rr = w.regionOf[idx(nx, ny)];
          if (rr >= 0 && w.regions[rr].kind !== 'ocean' && w.regions[rr].kind !== 'sea') return w.regions[rr].name;
        }
      }
      return fallback || 'the wild country';
    };

    w.addSettlement = ({ x, y, culture, pop, name, gold }) => {
      const s = {
        id: w.nextSettlementId++,
        name: name || w.namer.place(culture),
        x, y, culture,
        pop, tier: w.tierOf(pop), maxTier: w.tierOf(pop),
        prosp: 1, wealth: 0,
        gold: !!gold, boomUntil: 0,
        declineStreak: 0, hungerStreak: 0,
        foundedYear: w.year, lastFamineYear: -99,
        cap: 100, fish: w.hasFishAdj(x, y),
        ruined: false,
      };
      w.settlements.push(s);
      w.settlementsById[s.id] = s;
      w.settleGrid[idx(x, y)] = s.id;
      w.dirty.features = true; w.dirty.borders = true; w.dirty.labels = true;
      return s;
    };

    w.ruinSettlement = (s, cause, altName) => {
      s.ruined = true;
      s.ruinedYear = w.year;
      w.settleGrid[idx(s.x, s.y)] = -1;
      const usedAlt = w.ruins.filter((r2) => r2.alt).length;
      const alt = altName ? (RUIN_NAMES[usedAlt % RUIN_NAMES.length]) : null;
      w.ruins.push({ x: s.x, y: s.y, name: s.name, year: w.year, tier: s.tier, cause, alt });
      w.dirty.features = true; w.dirty.borders = true; w.dirty.labels = true;
      return alt;
    };

    w.recomputeClaims = () => {
      w.claims.fill(-1);
      const strength = new Float32Array(w.claims.length);
      const INFLUENCE = [4, 6, 9, 12];
      for (const s of w.settlements) {
        if (s.ruined) continue;
        const R = INFLUENCE[s.tier];
        for (let dy = -R; dy <= R; dy++) for (let dx = -R; dx <= R; dx++) {
          const nx = s.x + dx, ny = s.y + dy;
          if (!w.inB(nx, ny) || !w.isLandAt(nx, ny)) continue;
          const d = Math.sqrt(dx * dx + dy * dy);
          if (d > R) continue;
          const str = (s.tier + 1.5) * (1 - d / (R + 1));
          const i = idx(nx, ny);
          if (str > strength[i]) { strength[i] = str; w.claims[i] = s.culture; }
        }
      }
      w.dirty.borders = true;
    };

    w.aliveSettlements = () => w.settlements.filter((s) => !s.ruined);
  }

  S.world = { create, MONTHS, TIER_NAMES };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
