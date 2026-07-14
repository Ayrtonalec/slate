/* SLATE Phase 0 — acts of god. Three powers, each a perturbation the
   living world will interpret in its own time. Every act is chronicled;
   nothing a god does is forgotten. */
(function (S) {
  'use strict';

  function seedGold(w, x, y) {
    x = Math.round(x); y = Math.round(y);
    if (!w.inB(x, y)) return { ok: false, msg: 'Beyond the edge of the map.' };
    const b = w.biome[w.idx(x, y)];
    if (b !== S.B.MOUNTAIN && b !== S.B.HILLS) {
      return { ok: false, msg: 'The vein must be seeded in high stone — choose mountains or hills.' };
    }
    const v = { x, y, revealed: true, divine: true };
    w.veins.push(v);
    S.chronicle.add(w, 'goldSeed', { region: w.regionNameAt(x, y, 'the high stone'), x, y });
    w.dirty.features = true;
    return { ok: true, x, y };
  }

  function curseWeather(w, x, y) {
    x = Math.round(x); y = Math.round(y);
    if (!w.inB(x, y)) return { ok: false, msg: 'Beyond the edge of the map.' };
    let anyLand = false;
    for (let dy = -3; dy <= 3 && !anyLand; dy++) for (let dx = -3; dx <= 3 && !anyLand; dx++) {
      if (w.isLandAt(x + dx, y + dy)) anyLand = true;
    }
    if (!anyLand) return { ok: false, msg: 'Curse the living land — the open sea cares nothing for weather.' };
    if (w.stormAt(x, y)) return { ok: false, msg: 'That sky is already yours.' };
    w.storms.push({ x, y, r: 5, start: w.tick });
    w.recomputeFert();
    const near = w.settlementNear(x, y, 8);
    S.chronicle.add(w, 'curse', { place: near ? near.name : w.regionNameAt(x, y, 'the open country'), x, y });
    w.dirty.features = true;
    return { ok: true, x, y };
  }

  function blessLand(w, x, y) {
    x = Math.round(x); y = Math.round(y);
    if (!w.inB(x, y)) return { ok: false, msg: 'Beyond the edge of the map.' };
    if (!w.isLandAt(x, y)) return { ok: false, msg: 'Bless the soil, not the sea.' };
    if (w.stormAt(x, y)) return { ok: false, msg: 'The storm would devour the blessing — this sky is cursed.' };
    if (w.blessAt(x, y)) return { ok: false, msg: 'This land already carries your favor.' };
    w.blesses.push({ x, y, r: 5, start: w.tick });
    w.recomputeFert();
    const near = w.settlementNear(x, y, 8);
    S.chronicle.add(w, 'bless', { place: near ? near.name : w.regionNameAt(x, y, 'the open country'), x, y });
    w.dirty.features = true;
    return { ok: true, x, y };
  }

  S.powers = { seedGold, curseWeather, blessLand };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
