/* SLATE Phase 0 — deterministic world generation.
   Produces the geography a run lives on: continents, climate, biomes,
   rivers, fertility, resources, and named regions (ranges, forests,
   seas). Same seed → same world, always. */
(function (S) {
  'use strict';

  S.B = { OCEAN: 0, SHALLOW: 1, PLAINS: 2, FOREST: 3, HILLS: 4, MOUNTAIN: 5, DESERT: 6, SNOW: 7, MARSH: 8 };
  const B = S.B;

  const W = 192, H = 120;

  function valueNoise(seed) {
    function rnd(ix, iy) {
      let n = Math.imul(ix, 127413) ^ Math.imul(iy, 335545) ^ (seed | 0);
      n = Math.imul(n ^ (n >>> 13), 1274126177);
      return ((n ^ (n >>> 16)) >>> 0) / 4294967296;
    }
    const smooth = (t) => t * t * (3 - 2 * t);
    return function (x, y) {
      const ix = Math.floor(x), iy = Math.floor(y), fx = x - ix, fy = y - iy;
      const a = rnd(ix, iy), b = rnd(ix + 1, iy), c = rnd(ix, iy + 1), d = rnd(ix + 1, iy + 1);
      const u = smooth(fx), v = smooth(fy);
      return a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
    };
  }

  function fbm(noise, oct, lac, gain) {
    return (x, y) => {
      let s = 0, a = 1, f = 1, norm = 0;
      for (let i = 0; i < oct; i++) { s += a * noise(x * f, y * f); norm += a; a *= gain; f *= lac; }
      return s / norm;
    };
  }

  function generate(seed) {
    const hseed = S.rng.hashStr(String(seed));
    const nBase = fbm(valueNoise(hseed ^ 0x1111), 5, 2.0, 0.5);
    const nWarp = fbm(valueNoise(hseed ^ 0x2222), 4, 2.0, 0.5);
    const nRidge = fbm(valueNoise(hseed ^ 0x3333), 4, 2.1, 0.5);
    const nMoist = fbm(valueNoise(hseed ^ 0x4444), 4, 2.0, 0.5);
    const nJit = valueNoise(hseed ^ 0x5555);
    const rng = S.rng.makeRng(seed, 'worldgen');
    const namer = S.names.makeNamer(seed);

    const N = W * H;
    const height = new Float32Array(N);
    const temp = new Float32Array(N);
    const moist = new Float32Array(N);
    const fertBase = new Float32Array(N);
    const fert = new Float32Array(N);
    const biome = new Uint8Array(N);
    const river = new Uint8Array(N);
    const resource = new Uint8Array(N);
    const seaDist = new Uint8Array(N);   // land: distance to nearest water (capped)
    const landDist = new Uint8Array(N);  // water: distance to nearest land (capped)
    const regionOf = new Int16Array(N).fill(-1);
    const idx = (x, y) => y * W + x;

    // --- Height field: warped fBm + ridged component, ocean frame at map edges.
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        const u = x / W * 7, v = y / H * 4.4;
        const wx = u + (nWarp(u * 0.9 + 40, v * 0.9) - 0.5) * 1.6;
        const wy = v + (nWarp(u * 0.9, v * 0.9 + 40) - 0.5) * 1.6;
        let h = nBase(wx, wy) * 0.72;
        const r = nRidge(u * 1.15 + 9, v * 1.15 + 9);
        const ridge = Math.pow(1 - Math.abs(2 * r - 1), 2.2);
        h += ridge * 0.34;
        const ex = Math.min(x, W - 1 - x) / 14, ey = Math.min(y, H - 1 - y) / 12;
        const edge = Math.min(1, Math.min(ex, ey));
        h *= 0.35 + 0.65 * (edge < 1 ? edge * edge * (3 - 2 * edge) : 1);
        height[idx(x, y)] = h;
      }
    }

    // --- Sea level: binary search to a fixed land fraction (keeps every seed usable).
    const LAND_TARGET = 0.36;
    let lo = 0.1, hi = 0.9, sea = 0.5;
    for (let it = 0; it < 22; it++) {
      sea = (lo + hi) / 2;
      let land = 0;
      for (let i = 0; i < N; i++) if (height[i] > sea) land++;
      if (land / N > LAND_TARGET) lo = sea; else hi = sea;
    }
    const isWater = (i) => height[i] <= sea;

    // --- Distance fields (BFS), for climate, shallows and depth shading.
    {
      const q = [];
      seaDist.fill(255); landDist.fill(255);
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const i = idx(x, y);
        if (isWater(i)) {
          let coast = false;
          if (x > 0 && !isWater(i - 1)) coast = true;
          if (x < W - 1 && !isWater(i + 1)) coast = true;
          if (y > 0 && !isWater(i - W)) coast = true;
          if (y < H - 1 && !isWater(i + W)) coast = true;
          landDist[i] = coast ? 1 : 255;
          if (coast) q.push(i);
        } else {
          let shore = false;
          if (x > 0 && isWater(i - 1)) shore = true;
          if (x < W - 1 && isWater(i + 1)) shore = true;
          if (y > 0 && isWater(i - W)) shore = true;
          if (y < H - 1 && isWater(i + W)) shore = true;
          seaDist[i] = shore ? 1 : 255;
          if (shore) q.push(i);
        }
      }
      let head = 0;
      while (head < q.length) {
        const i = q[head++];
        const x = i % W, y = (i / W) | 0;
        const field = isWater(i) ? landDist : seaDist;
        const d = field[i];
        if (d >= 30) continue;
        const push = (j) => {
          if (isWater(j) !== isWater(i)) return;
          const f = isWater(j) ? landDist : seaDist;
          if (f[j] > d + 1) { f[j] = d + 1; q.push(j); }
        };
        if (x > 0) push(i - 1);
        if (x < W - 1) push(i + 1);
        if (y > 0) push(i - W);
        if (y < H - 1) push(i + W);
      }
    }

    // --- Climate.
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        const i = idx(x, y);
        const lat = Math.abs(y / H - 0.5) * 2; // 0 equator .. 1 poles
        temp[i] = Math.max(0, Math.min(1,
          1 - lat * 1.05 - Math.max(0, height[i] - sea) * 1.1 + (nJit(x * 0.3, y * 0.3) - 0.5) * 0.14));
        const coastal = Math.max(0, 1 - Math.min(seaDist[i], 30) / 26);
        moist[i] = Math.max(0, Math.min(1, nMoist(x / W * 5 + 3, y / H * 3 + 3) * 0.78 + coastal * 0.3));
      }
    }

    // --- Rivers: springs in high wet ground, descend to the sea.
    const riverPaths = [];
    {
      const springs = [];
      for (let y = 2; y < H - 2; y++) for (let x = 2; x < W - 2; x++) {
        const i = idx(x, y);
        if (!isWater(i) && height[i] > sea + 0.16 && moist[i] > 0.42) springs.push([x, y, height[i]]);
      }
      springs.sort((a, b) => b[2] - a[2]);
      const chosen = [];
      for (const s of springs) {
        if (chosen.length >= 30) break;
        if (chosen.every((c) => (c[0] - s[0]) ** 2 + (c[1] - s[1]) ** 2 >= 49)) chosen.push(s);
      }
      for (const [sx, sy] of chosen) {
        let x = sx, y = sy;
        const pts = [[x, y]];
        const visited = new Set([idx(x, y)]);
        let merged = false;
        for (let step = 0; step < 500; step++) {
          let bx = -1, by = -1, bh = Infinity;
          for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
            if (!dx && !dy) continue;
            const nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
            const j = idx(nx, ny);
            if (visited.has(j)) continue;
            const hh = height[j] + (S.rng.hash2(nx, ny, hseed) - 0.5) * 0.02;
            if (hh < bh) { bh = hh; bx = nx; by = ny; }
          }
          if (bx < 0 || bh > height[idx(x, y)] + 0.015) break; // stuck in a pit
          x = bx; y = by;
          const j = idx(x, y);
          pts.push([x, y]);
          if (isWater(j)) break;
          if (river[j]) { river[j] = Math.min(3, river[j] + 1); merged = true; break; }
          visited.add(j);
        }
        if (pts.length >= 7) {
          for (const [px, py] of pts) {
            const j = idx(px, py);
            if (!isWater(j)) river[j] = Math.max(river[j], 1);
          }
          riverPaths.push({ pts, flow: merged ? 2 : 1 });
        }
      }
    }

    // --- Biomes.
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        const i = idx(x, y);
        if (isWater(i)) { biome[i] = landDist[i] <= 2 ? B.SHALLOW : B.OCEAN; continue; }
        const h = height[i], t = temp[i], m = moist[i];
        if (h > sea + 0.30) biome[i] = B.MOUNTAIN;
        else if (t < 0.18) biome[i] = B.SNOW;
        else if (h > sea + 0.20) biome[i] = B.HILLS;
        else if (t > 0.72 && m < 0.34) biome[i] = B.DESERT;
        else if (m > 0.78 && h < sea + 0.08 && t > 0.3) biome[i] = B.MARSH;
        else if (m > 0.5 && t > 0.26) biome[i] = B.FOREST;
        else biome[i] = B.PLAINS;
      }
    }

    // --- Fertility & natural resources.
    const FERT_BY_BIOME = { [B.PLAINS]: 0.75, [B.FOREST]: 0.55, [B.HILLS]: 0.42, [B.MARSH]: 0.5, [B.DESERT]: 0.07, [B.SNOW]: 0.05, [B.MOUNTAIN]: 0.1 };
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        const i = idx(x, y);
        if (isWater(i)) { fertBase[i] = 0; continue; }
        let f = FERT_BY_BIOME[biome[i]] || 0.3;
        let nearRiver = river[i] > 0;
        if (!nearRiver) {
          if (x > 0 && river[i - 1]) nearRiver = true;
          else if (x < W - 1 && river[i + 1]) nearRiver = true;
          else if (y > 0 && river[i - W]) nearRiver = true;
          else if (y < H - 1 && river[i + W]) nearRiver = true;
        }
        if (nearRiver) f += 0.28;
        f += (S.rng.hash2(x, y, hseed ^ 0x77) - 0.5) * 0.14;
        fertBase[i] = Math.max(0, Math.min(1.2, f));
        if (seaDist[i] === 1 && biome[i] !== B.SNOW && S.rng.hash2(x, y, hseed ^ 0x88) < 0.16) resource[i] = 2; // fishing grounds
      }
    }
    fert.set(fertBase);

    // --- Hidden natural gold veins.
    const veins = [];
    {
      const cand = [];
      for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
        const i = idx(x, y);
        if (biome[i] === B.MOUNTAIN || biome[i] === B.HILLS) cand.push([x, y]);
      }
      const shuffled = rng.shuffle(cand);
      for (const [x, y] of shuffled) {
        if (veins.length >= 10) break;
        if (veins.every((v) => (v.x - x) ** 2 + (v.y - y) ** 2 >= 100)) {
          veins.push({ x, y, revealed: false, divine: false });
        }
      }
    }

    // --- Named regions (flood fills).
    const regions = [];
    {
      const seen = new Uint8Array(N);
      const MIN_SIZE = { range: 14, forest: 40, desert: 50 };
      const KIND_OF = { [B.MOUNTAIN]: 'range', [B.FOREST]: 'forest', [B.DESERT]: 'desert' };
      function flood(start, match) {
        const cells = [start];
        seen[start] = 1;
        for (let h2 = 0; h2 < cells.length; h2++) {
          const i = cells[h2];
          const x = i % W, y = (i / W) | 0;
          const tryPush = (j) => { if (!seen[j] && match(j)) { seen[j] = 1; cells.push(j); } };
          if (x > 0) tryPush(i - 1);
          if (x < W - 1) tryPush(i + 1);
          if (y > 0) tryPush(i - W);
          if (y < H - 1) tryPush(i + W);
        }
        return cells;
      }
      function registerRegion(cells, kind) {
        let cx = 0, cy = 0;
        for (const i of cells) { cx += i % W; cy += (i / W) | 0; }
        cx /= cells.length; cy /= cells.length;
        // Anchor the label on a member cell nearest the centroid.
        let best = cells[0], bd = Infinity;
        for (const i of cells) {
          const d = ((i % W) - cx) ** 2 + (((i / W) | 0) - cy) ** 2;
          if (d < bd) { bd = d; best = i; }
        }
        const id = regions.length;
        for (const i of cells) regionOf[i] = id;
        regions.push({ id, kind, name: namer.region(kind), cx: best % W, cy: (best / W) | 0, size: cells.length });
      }
      for (let i = 0; i < N; i++) {
        if (seen[i] || isWater(i)) continue;
        const kind = KIND_OF[biome[i]];
        if (!kind) { seen[i] = 1; continue; }
        const cells = flood(i, (j) => biome[j] === biome[i]);
        if (cells.length >= MIN_SIZE[kind]) registerRegion(cells, kind);
      }
      // Water bodies: the largest is the ocean; other big ones are seas.
      const bodies = [];
      for (let i = 0; i < N; i++) {
        if (seen[i] || !isWater(i)) continue;
        bodies.push(flood(i, (j) => isWater(j)));
      }
      bodies.sort((a, b) => b.length - a.length);
      bodies.forEach((cells, k) => {
        if (k === 0) registerRegion(cells, 'ocean');
        else if (cells.length >= 160) registerRegion(cells, 'sea');
      });
    }

    // --- Culture homelands: 4 fertile coastal sites, far apart.
    const homelands = [];
    {
      const cand = [];
      for (let y = 3; y < H - 3; y++) for (let x = 3; x < W - 3; x++) {
        const i = idx(x, y);
        if (isWater(i) || seaDist[i] > 2 || temp[i] < 0.24) continue;
        let f = 0;
        for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) {
          const nx = x + dx, ny = y + dy;
          if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
          f += fertBase[idx(nx, ny)];
        }
        if (f > 8) cand.push({ x, y, f });
      }
      cand.sort((a, b) => b.f - a.f);
      const top = cand.slice(0, 400);
      if (top.length) {
        homelands.push(top[0]);
        while (homelands.length < 4 && top.length) {
          let best = null, bestD = -1;
          for (const c of top) {
            let dmin = Infinity;
            for (const hpt of homelands) dmin = Math.min(dmin, (hpt.x - c.x) ** 2 + (hpt.y - c.y) ** 2);
            const score = dmin + c.f * 4;
            if (dmin > 120 && score > bestD) { bestD = score; best = c; }
          }
          if (!best) break;
          homelands.push(best);
        }
      }
    }

    return {
      seed, W, H, sea, idx,
      height, temp, moist, fertBase, fert, biome, river, resource,
      seaDist, landDist, regionOf, regions, riverPaths, veins, homelands,
      namer,
      isWater: (x, y) => height[idx(x, y)] <= sea,
    };
  }

  S.worldgen = { generate, W, H };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
