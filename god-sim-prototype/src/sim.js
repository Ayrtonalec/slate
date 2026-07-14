/* SLATE Phase 0 — the simulation tick. One tick = one month.
   Order is fixed and deterministic: climate → settlements → colonization
   → gold → dragons → roads → claims. The world runs with zero divine
   input; god powers only perturb it. */
(function (S) {
  'use strict';

  const FEAR_R = 8;      // dragon terror radius
  const RAID_R = 10;

  function tick(w) {
    const rng = w.rngSim;
    w.tick++;
    w.month = w.tick % 12;
    w.year = Math.floor(w.tick / 12);

    if (w.month === 0) yearly(w, rng);

    // --- Per-settlement dragon fear cache (cheap: few dragons).
    const feared = new Set();
    for (const d of w.dragons) {
      for (const s of w.settlements) {
        if (s.ruined) continue;
        if ((s.x - d.x) ** 2 + (s.y - d.y) ** 2 <= FEAR_R * FEAR_R) feared.add(s.id);
      }
    }

    // --- Settlements.
    for (const s of w.settlements) {
      if (s.ruined) continue;
      const storm = w.stormAt(s.x, s.y);

      // Food capacity: staggered recompute, or forced when fertility changed.
      const deg = w.roadDeg[s.id] || 0;
      const boom = w.tick < s.boomUntil;
      s.prosp = 1 + 0.15 * deg + (s.gold ? 2.5 : 0) + (s.fish ? 0.3 : 0) + s.tier * 0.15 + (boom ? 1 : 0);

      if (w.fertDirty || (w.tick + s.id) % 24 === 0) {
        let cap = w.fertAround(s.x, s.y, 3) * 26;
        if (s.fish && !storm) cap += 170;
        cap *= 1 + 0.7 * Math.max(0, s.prosp - 1); // trade feeds cities beyond what fields carry
        if (w.leanYears > 0) cap *= 0.85;
        s.cap = Math.max(25, cap);
      }

      const prev = s.pop;
      if (storm) {
        s.pop *= 0.972; // the sky itself is against them
      } else {
        let r = 0.011 * (0.75 + 0.25 * Math.min(2.2, s.prosp));
        if (boom) r *= 1.6;
        if (feared.has(s.id)) r *= 0.25;
        s.pop += s.pop * r * (1 - s.pop / s.cap);
      }

      s.declineStreak = s.pop < prev - 0.01 ? s.declineStreak + 1 : 0;

      // Hunger and famine: chronicled only when the deficit is deep and rare.
      if (s.pop > s.cap * 1.2) {
        s.hungerStreak++;
        if (s.hungerStreak >= 15 && s.pop > 250 && w.year - s.lastFamineYear > 15) {
          s.lastFamineYear = w.year;
          s.hungerStreak = 0;
          S.chronicle.add(w, 'famine', { name: s.name, x: s.x, y: s.y });
          s.pop *= 0.82;
          const dest = bestNeighbor(w, s);
          if (dest) { dest.pop += s.pop * 0.06; s.pop *= 0.94; }
        }
      } else if (s.hungerStreak > 0) s.hungerStreak = Math.max(0, s.hungerStreak - 2);

      // Tier transitions: chronicled only the first time a rank is reached.
      const t = w.tierOf(s.pop);
      if (t > s.tier) {
        s.tier = t;
        if (t > s.maxTier) {
          s.maxTier = t;
          const data = { name: s.name, pop: Math.round(s.pop / 50) * 50, culture: s.culture, x: s.x, y: s.y };
          S.chronicle.add(w, t === 1 ? 'village' : t === 2 ? 'town' : 'city', data);
        }
        w.dirty.features = w.dirty.borders = w.dirty.labels = true;
      } else if (t < s.tier) {
        s.tier = t; // quiet decline; the chronicle notices only the fall to ruin
        w.dirty.features = w.dirty.labels = true;
      }

      // Abandonment.
      if ((s.pop < 35 && s.declineStreak > 24) || (storm && s.pop < 55)) {
        const why = storm ? null : (feared.has(s.id) ? 'its people fled the wyrm' : null);
        if (storm) S.chronicle.add(w, 'stormExodus', { name: s.name, x: s.x, y: s.y });
        else S.chronicle.add(w, 'abandon', { name: s.name, tier: s.tier, why, x: s.x, y: s.y });
        w.ruinSettlement(s, storm ? 'storm' : 'decline');
        const dest = bestNeighbor(w, s);
        if (dest) {
          dest.pop += s.pop * 0.6;
          S.chronicle.add(w, 'migration', { from: s.name, to: dest.name, x: dest.x, y: dest.y, fx: s.x, fy: s.y });
        }
        continue;
      }

      // Wealth accrues from prosperity; this is what dragons smell.
      s.wealth += s.prosp * s.pop / 200000;
    }
    w.fertDirty = false;

    // --- Colonization (fission), damped by regional crowding.
    const aliveNow = w.aliveSettlements();
    for (const s of aliveNow) {
      if (s.pop > s.cap * 0.7 && s.pop > 150 && rng.chance(0.005)) {
        let crowd = 0;
        for (const o of aliveNow) {
          if ((o.x - s.x) ** 2 + (o.y - s.y) ** 2 <= 64) crowd++;
        }
        if (rng.next() < (crowd - 2) / 10) continue; // packed regions stop spilling outward
        const site = w.bestSiteNear(s.x, s.y, 5, 16);
        if (site) {
          const emig = Math.max(40, s.pop * 0.28);
          s.pop -= emig * 0.9;
          const child = w.addSettlement({ x: site.x, y: site.y, culture: s.culture, pop: emig });
          S.chronicle.add(w, 'found', { name: child.name, parent: s.name, x: child.x, y: child.y, fx: s.x, fy: s.y });
        }
      }
    }

    // --- Gold: discovery near settlements, mining camps in the wilds.
    for (const v of w.veins) {
      if (!v.revealed) {
        const near = w.settlementNear(v.x, v.y, 4.5);
        if (near && rng.chance(0.02)) {
          v.revealed = true;
          near.gold = true;
          near.boomUntil = w.tick + 40 * 12;
          S.chronicle.add(w, 'goldFound', { name: near.name, x: v.x, y: v.y });
          S.chronicle.add(w, 'boom', { name: near.name, x: near.x, y: near.y });
          pullMigrants(w, near, 0.12);
          w.dirty.features = true;
        }
      } else if (!w.settlementNear(v.x, v.y, 4)) {
        // A known vein with nobody working it draws the desperate.
        const parent = w.settlementNear(v.x, v.y, 16);
        const chance = parent ? 0.012 : 0.004;
        if (rng.chance(chance)) {
          const spot = w.campSiteNear(v.x, v.y);
          if (spot) {
            const culture = parent ? parent.culture : (w.settlementNear(v.x, v.y, 60) || { culture: rng.int(0, S.CULTURES.length - 1) }).culture;
            const name = w.namer.goldPlace(culture);
            const camp = w.addSettlement({ x: spot.x, y: spot.y, culture, pop: parent ? 55 : 40, name, gold: true });
            camp.boomUntil = w.tick + 40 * 12;
            S.chronicle.add(w, 'camp', { name, region: w.regionNameAt(v.x, v.y, 'the high stone'), x: spot.x, y: spot.y });
            if (parent) { parent.pop *= 0.96; }
          }
        }
      }
    }

    // --- Dragons: raids.
    for (const d of w.dragons.slice()) {
      if (w.tick - d.lastRaid > d.raidEvery) {
        let target = null, tw = 8; // only bother with somewhere worth burning
        for (const s of w.aliveSettlements()) {
          if ((s.x - d.x) ** 2 + (s.y - d.y) ** 2 <= RAID_R * RAID_R && s.wealth + s.pop / 400 > tw) {
            tw = s.wealth + s.pop / 400; target = s;
          }
        }
        d.lastRaid = w.tick;
        d.raidEvery = rng.int(60, 140);
        if (target) {
          S.chronicle.add(w, 'raid', { dragon: d.name, name: target.name, x: target.x, y: target.y, fx: d.x, fy: d.y });
          target.pop *= 0.85;
          target.wealth *= 0.75;
          if (target.pop < 400 && rng.chance(0.5)) {
            const alt = w.ruinSettlement(target, 'dragon', true);
            S.chronicle.add(w, 'razed', { name: target.name, ruinName: alt, x: target.x, y: target.y });
            const dest = bestNeighbor(w, target);
            if (dest) {
              dest.pop += target.pop * 0.5;
              S.chronicle.add(w, 'migration', { from: target.name, to: dest.name, x: dest.x, y: dest.y, fx: target.x, fy: target.y });
            }
          }
        }
      }
    }

    if (w.claimsDirtyTick && w.tick - w.claimsDirtyTick > 24) {
      w.recomputeClaims();
      w.claimsDirtyTick = 0;
    } else if (w.dirty.borders && !w.claimsDirtyTick) {
      w.claimsDirtyTick = w.tick;
    }
  }

  function yearly(w, rng) {
    // Lean years: a world-scale harvest cycle.
    if (w.leanYears > 0) w.leanYears--;
    else if (rng.chance(0.10)) w.leanYears = rng.int(1, 2);

    // Dragon spawning: wealth near mountains breeds trouble.
    if (w.dragons.length < 2 && w.year >= 60 && w.tick >= (w.dragonCooldownUntil || 0)) {
      for (const s of w.aliveSettlements()) {
        if (s.wealth < 70) continue;
        const m = w.nearestMountain(s.x, s.y, 6);
        if (!m) continue;
        if (rng.chance(Math.min(0.05, 0.015 + s.wealth / 4000))) {
          const d = {
            name: 'the wyrm ' + w.namer.dragon(),
            x: m.x, y: m.y, bornYear: w.year,
            lastRaid: w.tick, raidEvery: rng.int(48, 110),
            regionName: w.regionNameAt(m.x, m.y, 'the high peaks'),
          };
          w.dragons.push(d);
          S.chronicle.add(w, 'dragon', { dragon: d.name, region: d.regionName, name: s.name, x: m.x, y: m.y });
          w.dirty.features = true;
          break;
        }
      }
    }

    // Dragon slaying & departure.
    for (const d of w.dragons.slice()) {
      const heroes = w.aliveSettlements().filter((s) => s.tier >= 2 && (s.x - d.x) ** 2 + (s.y - d.y) ** 2 <= RAID_R * RAID_R);
      const anyNear = w.settlementNear(d.x, d.y, 12);
      if (heroes.length && rng.chance(0.10)) {
        const home = rng.pick(heroes);
        const hero = 'Ser ' + w.namer.person(home.culture) + ' the Wyrmslayer';
        S.chronicle.add(w, 'slain', { hero, name: home.name, dragon: d.name, region: d.regionName, x: d.x, y: d.y });
        home.wealth += 35;
        home.boomUntil = Math.max(home.boomUntil, w.tick + 10 * 12);
        w.dragons.splice(w.dragons.indexOf(d), 1);
        w.deadLairs.push({ x: d.x, y: d.y });
        w.dragonCooldownUntil = w.tick + 300; // a generation of peace
        w.dirty.features = true;
      } else if (!anyNear && rng.chance(0.3)) {
        S.chronicle.add(w, 'dragonGone', { dragon: d.name, x: d.x, y: d.y });
        w.dragons.splice(w.dragons.indexOf(d), 1);
        w.deadLairs.push({ x: d.x, y: d.y });
        w.dragonCooldownUntil = w.tick + 300;
        w.dirty.features = true;
      }
    }

    // Roads: nearby sizable settlements link up.
    let built = 0;
    const alive = w.aliveSettlements().filter((s) => s.tier >= 1);
    for (let i = 0; i < alive.length && built < 2; i++) {
      for (let j = i + 1; j < alive.length && built < 2; j++) {
        const a = alive[i], b = alive[j];
        const d2 = (a.x - b.x) ** 2 + (a.y - b.y) ** 2;
        if (d2 > 13 * 13) continue;
        if ((w.roadDeg[a.id] || 0) >= 4 || (w.roadDeg[b.id] || 0) >= 4) continue;
        if (w.roads.some((r) => (r.a === a.id && r.b === b.id) || (r.a === b.id && r.b === a.id))) continue;
        if (!rng.chance(0.25)) continue;
        const pts = roadPath(w, a, b);
        if (!pts) continue;
        w.roads.push({ a: a.id, b: b.id, pts });
        w.roadDeg[a.id] = (w.roadDeg[a.id] || 0) + 1;
        w.roadDeg[b.id] = (w.roadDeg[b.id] || 0) + 1;
        S.chronicle.add(w, 'road', { a: a.name, b: b.name, x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 });
        w.dirty.features = true;
        built++;
      }
    }
  }

  function roadPath(w, a, b) {
    // Sampled straight-ish path; reject if it wades through open water.
    const steps = Math.ceil(Math.hypot(b.x - a.x, b.y - a.y) * 2);
    const pts = [];
    let wet = 0;
    for (let k = 0; k <= steps; k++) {
      const t = k / steps;
      const x = a.x + (b.x - a.x) * t;
      const y = a.y + (b.y - a.y) * t;
      if (!w.isLandAt(Math.round(x), Math.round(y))) wet++;
      if (wet > 1) return null;
      pts.push([x, y]);
    }
    return pts;
  }

  function bestNeighbor(w, s) {
    let best = null, bd = Infinity;
    for (const o of w.settlements) {
      if (o.ruined || o === s) continue;
      if (w.stormAt(o.x, o.y)) continue;
      const d2 = (o.x - s.x) ** 2 + (o.y - s.y) ** 2;
      if (d2 < bd) { bd = d2; best = o; }
    }
    return best;
  }

  function pullMigrants(w, target, frac) {
    const near = w.aliveSettlements()
      .filter((s) => s !== target && (s.x - target.x) ** 2 + (s.y - target.y) ** 2 < 400)
      .sort((a, b2) => b2.pop - a.pop)
      .slice(0, 2);
    for (const s of near) {
      const moved = s.pop * frac * 0.5;
      s.pop -= moved;
      target.pop += moved;
    }
  }

  function runYears(w, years, onYear) {
    for (let i = 0; i < years * 12; i++) {
      tick(w);
      if (onYear && w.tick % 12 === 0) onYear(w);
    }
  }

  S.sim = { tick, runYears };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
