/* SLATE Phase 0 — terrain painting: the static geography rendered once
   into an offscreen canvas as an illustrated atlas plate — watercolor
   biome washes, traced-and-smoothed ink coastlines, engraved sea hatching,
   hachured mountains, stippled forests. Divine weather (curses/blessings)
   is composited over the static base so re-tinting is cheap. */
(function (S) {
  'use strict';

  const CS = 8;          // world units per cell
  const SS = 2;          // supersample factor for the cached plate
  const cache = new WeakMap();

  // --- coastline extraction: boundary edges → loops → Chaikin smoothing ---
  function coastLoops(w) {
    const { W, H } = w;
    const land = (x, y) => x >= 0 && y >= 0 && x < W && y < H && w.height[w.idx(x, y)] > w.sea;
    // Collect directed boundary edges (land on the left). A vertex can carry
    // TWO outgoing edges where land cells touch diagonally, so store lists
    // and resolve the fork by taking the sharpest clockwise turn — that keeps
    // each island's own interior on the left instead of jumping tracks.
    const segs = new Map(); // "x,y" -> [[ex,ey], ...]
    const key = (x, y) => x + ',' + y;
    const addSeg = (sx, sy, ex, ey) => {
      const k = key(sx, sy);
      let list = segs.get(k);
      if (!list) { list = []; segs.set(k, list); }
      list.push([ex, ey]);
    };
    let edgeCount = 0;
    for (let y = 0; y < H; y++) {
      for (let x = 0; x < W; x++) {
        if (!land(x, y)) continue;
        if (!land(x, y - 1)) { addSeg(x, y, x + 1, y); edgeCount++; }
        if (!land(x + 1, y)) { addSeg(x + 1, y, x + 1, y + 1); edgeCount++; }
        if (!land(x, y + 1)) { addSeg(x + 1, y + 1, x, y + 1); edgeCount++; }
        if (!land(x - 1, y)) { addSeg(x, y + 1, x, y); edgeCount++; }
      }
    }
    const takeNext = (from, inDir) => {
      const k = key(from[0], from[1]);
      const list = segs.get(k);
      if (!list || !list.length) return null;
      let bestI = 0;
      if (list.length > 1 && inDir) {
        let bestCross = -Infinity;
        for (let i = 0; i < list.length; i++) {
          const ox = list[i][0] - from[0], oy = list[i][1] - from[1];
          const cross = inDir[0] * oy - inDir[1] * ox;
          if (cross > bestCross) { bestCross = cross; bestI = i; }
        }
      }
      const next = list.splice(bestI, 1)[0];
      if (!list.length) segs.delete(k);
      return next;
    };
    const loops = [];
    while (segs.size) {
      const startKey = segs.keys().next().value;
      const start = startKey.split(',').map(Number);
      const first = takeNext(start, null);
      const loop = [start, first];
      let prev = start, cur = first;
      for (let guard = 0; guard < edgeCount + 8; guard++) {
        if (cur[0] === start[0] && cur[1] === start[1]) break;
        const nxt = takeNext(cur, [cur[0] - prev[0], cur[1] - prev[1]]);
        if (!nxt) break;
        loop.push(nxt);
        prev = cur; cur = nxt;
      }
      if (loop.length >= 4) loops.push(loop.map(([x, y]) => [x * CS, y * CS]));
    }
    // Chaikin corner cutting ×2 for soft, hand-drawn coasts.
    const smooth = (pts) => {
      const out = [];
      for (let i = 0; i < pts.length; i++) {
        const a = pts[i], b = pts[(i + 1) % pts.length];
        out.push([a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25]);
        out.push([a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75]);
      }
      return out;
    };
    return loops.map((l) => smooth(smooth(l)));
  }

  function loopsToPath(loops) {
    const p = new Path2D();
    for (const l of loops) {
      p.moveTo(l[0][0], l[0][1]);
      for (let i = 1; i < l.length; i++) p.lineTo(l[i][0], l[i][1]);
      p.closePath();
    }
    return p;
  }

  function smoothOpen(pts) {
    if (pts.length < 3) return pts;
    const out = [pts[0]];
    for (let i = 0; i < pts.length - 1; i++) {
      const a = pts[i], b = pts[i + 1];
      out.push([a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25]);
      out.push([a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75]);
    }
    out.push(pts[pts.length - 1]);
    return out;
  }

  function makeCanvas(w, h) {
    if (typeof OffscreenCanvas !== 'undefined') return new OffscreenCanvas(w, h);
    const c = document.createElement('canvas');
    c.width = w; c.height = h;
    return c;
  }

  // --- the static plate ---
  function buildBase(w) {
    const P = S.palette, B = S.B;
    const pw = w.W * CS * SS, ph = w.H * CS * SS;
    const cv = makeCanvas(pw, ph);
    const ctx = cv.getContext('2d');
    ctx.scale(SS, SS);
    const jit = (x, y, s) => S.rng.hash2(x, y, s) - 0.5;

    // Paper ground (sea will cover most of it; land wash sits above).
    ctx.fillStyle = P.sea;
    ctx.fillRect(0, 0, w.W * CS, w.H * CS);

    // Sea depth: darker away from shore. Painted at cell resolution and
    // upscaled with smoothing so it grades softly instead of showing a grid.
    {
      const lo = makeCanvas(w.W, w.H);
      const lctx = lo.getContext('2d');
      for (let y = 0; y < w.H; y++) {
        for (let x = 0; x < w.W; x++) {
          const i = w.idx(x, y);
          if (w.height[i] > w.sea) continue;
          const d = Math.min(w.landDist[i], 10) / 10;
          if (d > 0.1) {
            lctx.fillStyle = P.alpha(P.seaDeep, Math.min(0.5, (d - 0.1) * 0.5));
            lctx.fillRect(x, y, 1, 1);
          }
        }
      }
      ctx.imageSmoothingEnabled = true;
      ctx.drawImage(lo, 0, 0, w.W, w.H, 0, 0, w.W * CS, w.H * CS);
    }
    // Engraved wave hatching near coasts.
    ctx.strokeStyle = P.alpha(P.ink, 0.10);
    ctx.lineWidth = 0.45;
    for (let y = 0; y < w.H; y++) {
      for (let x = 0; x < w.W; x++) {
        const i = w.idx(x, y);
        if (w.height[i] > w.sea) continue;
        const d = w.landDist[i];
        if (d < 2 || d > 5 || S.rng.hash2(x, y, 11) > 0.4) continue;
        const cx = (x + 0.5 + jit(x, y, 12)) * CS, cy = (y + 0.5 + jit(x, y, 13)) * CS;
        const len = CS * (0.5 + S.rng.hash2(x, y, 14) * 0.5);
        ctx.beginPath();
        ctx.moveTo(cx - len / 2, cy);
        ctx.quadraticCurveTo(cx, cy - 1.2, cx + len / 2, cy);
        ctx.stroke();
      }
    }

    // Land: clip to smoothed coast, paint watercolor biome blobs.
    const loops = coastLoops(w);
    const coast = loopsToPath(loops);
    ctx.save();
    ctx.clip(coast);
    ctx.fillStyle = P.biomes[B.PLAINS];
    ctx.fill(coast);
    for (let y = 0; y < w.H; y++) {
      for (let x = 0; x < w.W; x++) {
        const i = w.idx(x, y);
        if (w.height[i] <= w.sea) continue;
        const b = w.biome[i];
        let col = P.biomes[b];
        // Fertile land leans green, poor land leans tawny — quiet but legible.
        if (b === B.PLAINS || b === B.HILLS) {
          col = P.mix(col, '#adbd85', Math.max(0, Math.min(1, w.fertBase[i])) * 0.4);
        }
        const l = jit(x, y, 21) * 0.09;
        ctx.fillStyle = col;
        ctx.globalAlpha = 0.92;
        const cx = (x + 0.5 + jit(x, y, 22) * 0.5) * CS;
        const cy = (y + 0.5 + jit(x, y, 23) * 0.5) * CS;
        const r = CS * (0.75 + S.rng.hash2(x, y, 24) * 0.45);
        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.fill();
        if (l !== 0) {
          ctx.fillStyle = l > 0 ? P.alpha('#ffffff', l) : P.alpha('#000000', -l * 0.7);
          ctx.beginPath();
          ctx.arc(cx, cy, r, 0, Math.PI * 2);
          ctx.fill();
        }
      }
    }
    ctx.globalAlpha = 1;

    // Rivers (drawn on land, under relief glyphs).
    ctx.strokeStyle = P.alpha(P.river, 0.8);
    ctx.lineCap = 'round'; ctx.lineJoin = 'round';
    for (const rp of w.riverPaths) {
      const pts = smoothOpen(smoothOpen(rp.pts.map(([x, y]) => [(x + 0.5) * CS, (y + 0.5) * CS])));
      ctx.lineWidth = 0.7 + rp.flow * 0.5;
      ctx.beginPath();
      ctx.moveTo(pts[0][0], pts[0][1]);
      for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i][0], pts[i][1]);
      ctx.stroke();
    }
    ctx.restore(); // unclip

    // Relief & vegetation glyphs (sorted north→south so overlaps stack).
    const glyphs = [];
    for (let y = 0; y < w.H; y++) {
      for (let x = 0; x < w.W; x++) {
        const i = w.idx(x, y);
        if (w.height[i] <= w.sea) continue;
        glyphs.push([x, y, w.biome[i], i]);
      }
    }
    for (const [x, y, b, i] of glyphs) {
      const cx = (x + 0.5 + jit(x, y, 31) * 0.6) * CS;
      const cy = (y + 0.5 + jit(x, y, 32) * 0.6) * CS;
      if (b === B.MOUNTAIN) {
        const s = CS * (0.5 + (w.height[i] - w.sea) * 0.9);
        drawMountain(ctx, P, cx, cy, s, w.temp[i] < 0.3 || w.height[i] > w.sea + 0.4);
      } else if (b === B.HILLS) {
        if (S.rng.hash2(x, y, 33) < 0.75) drawHill(ctx, P, cx, cy, CS * 0.34);
      } else if (b === B.FOREST) {
        const n = 2 + (S.rng.hash2(x, y, 34) * 2 | 0);
        for (let k = 0; k < n; k++) {
          const tx = cx + jit(x * 7 + k, y, 35 + k) * CS * 1.1;
          const ty = cy + jit(x, y * 7 + k, 36 + k) * CS * 1.1;
          drawTree(ctx, P, tx, ty, CS * 0.30, w.temp[i] < 0.42);
        }
      } else if (b === B.MARSH) {
        if (S.rng.hash2(x, y, 37) < 0.6) drawMarsh(ctx, P, cx, cy, CS * 0.4);
      } else if (b === B.DESERT) {
        if (S.rng.hash2(x, y, 38) < 0.14) {
          ctx.fillStyle = P.alpha(P.ink, 0.18);
          ctx.beginPath(); ctx.arc(cx, cy, 0.5, 0, Math.PI * 2); ctx.fill();
        }
      }
    }

    // Coast ink: an outer haze, then the pen line.
    ctx.strokeStyle = P.alpha(P.seaDeep, 0.5);
    ctx.lineWidth = 3.2;
    ctx.stroke(coast);
    ctx.strokeStyle = P.alpha(P.ink, 0.75);
    ctx.lineWidth = 0.9;
    ctx.stroke(coast);

    // Paper grain over everything: blotches, fibers, vignette.
    const grainRng = S.rng.makeRng(w.seed, 'grain');
    for (let k = 0; k < 240; k++) {
      const gx = grainRng.range(0, w.W * CS), gy = grainRng.range(0, w.H * CS);
      const gr = grainRng.range(8, 60);
      const g = ctx.createRadialGradient(gx, gy, 0, gx, gy, gr);
      const dark = grainRng.chance(0.6);
      g.addColorStop(0, P.alpha(dark ? P.paperShadow : '#fffbe8', 0.05));
      g.addColorStop(1, P.alpha(dark ? P.paperShadow : '#fffbe8', 0));
      ctx.fillStyle = g;
      ctx.fillRect(gx - gr, gy - gr, gr * 2, gr * 2);
    }
    ctx.fillStyle = P.alpha(P.ink, 0.028);
    for (let k = 0; k < 2600; k++) {
      ctx.fillRect(grainRng.range(0, w.W * CS), grainRng.range(0, w.H * CS), 0.55, 0.55);
    }
    const vg = ctx.createRadialGradient(w.W * CS / 2, w.H * CS / 2, w.W * CS * 0.32, w.W * CS / 2, w.H * CS / 2, w.W * CS * 0.62);
    vg.addColorStop(0, 'rgba(90,70,40,0)');
    vg.addColorStop(1, 'rgba(90,70,40,0.14)');
    ctx.fillStyle = vg;
    ctx.fillRect(0, 0, w.W * CS, w.H * CS);

    return { canvas: cv, coast };
  }

  function drawMountain(ctx, P, cx, cy, s, snowcap) {
    ctx.strokeStyle = P.alpha(P.ink, 0.6);
    ctx.lineWidth = 0.55;
    ctx.lineJoin = 'round';
    ctx.beginPath();
    ctx.moveTo(cx - s * 0.9, cy + s * 0.55);
    ctx.lineTo(cx - s * 0.15, cy - s * 0.75);
    ctx.lineTo(cx + s * 0.2, cy - s * 0.1);
    ctx.lineTo(cx + s * 0.45, cy - s * 0.45);
    ctx.lineTo(cx + s * 0.95, cy + s * 0.55);
    ctx.stroke();
    // Shadow hatch on the eastern face.
    ctx.strokeStyle = P.alpha(P.ink, 0.28);
    ctx.beginPath();
    ctx.moveTo(cx - s * 0.15, cy - s * 0.7);
    ctx.lineTo(cx + s * 0.28, cy + s * 0.5);
    ctx.stroke();
    if (snowcap) {
      ctx.strokeStyle = 'rgba(255,252,240,0.9)';
      ctx.lineWidth = 0.8;
      ctx.beginPath();
      ctx.moveTo(cx - s * 0.32, cy - s * 0.42);
      ctx.lineTo(cx - s * 0.15, cy - s * 0.72);
      ctx.lineTo(cx + s * 0.02, cy - s * 0.5);
      ctx.stroke();
    }
  }

  function drawHill(ctx, P, cx, cy, s) {
    ctx.strokeStyle = P.alpha(P.ink, 0.35);
    ctx.lineWidth = 0.5;
    ctx.beginPath();
    ctx.arc(cx, cy, s, Math.PI * 1.05, Math.PI * 1.95);
    ctx.stroke();
  }

  function drawTree(ctx, P, cx, cy, s, conifer) {
    if (conifer) {
      ctx.fillStyle = P.alpha('#5d6f4a', 0.85);
      ctx.beginPath();
      ctx.moveTo(cx, cy - s * 1.5);
      ctx.lineTo(cx - s * 0.55, cy + s * 0.4);
      ctx.lineTo(cx + s * 0.55, cy + s * 0.4);
      ctx.closePath();
      ctx.fill();
    } else {
      ctx.fillStyle = P.alpha('#6d7f52', 0.8);
      ctx.beginPath();
      ctx.arc(cx, cy - s * 0.5, s * 0.72, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.strokeStyle = P.alpha(P.ink, 0.5);
    ctx.lineWidth = 0.45;
    ctx.beginPath();
    ctx.moveTo(cx, cy - s * 0.2);
    ctx.lineTo(cx, cy + s * 0.55);
    ctx.stroke();
  }

  function drawMarsh(ctx, P, cx, cy, s) {
    ctx.strokeStyle = P.alpha(P.ink, 0.3);
    ctx.lineWidth = 0.45;
    ctx.beginPath();
    ctx.moveTo(cx - s, cy); ctx.lineTo(cx + s, cy);
    ctx.moveTo(cx - s * 0.5, cy - s * 0.4); ctx.lineTo(cx + s * 0.5, cy - s * 0.4);
    ctx.moveTo(cx - s * 0.2, cy - s * 0.4); ctx.lineTo(cx - s * 0.2, cy - s * 0.9);
    ctx.stroke();
  }

  // --- divine weather composited over the base ---
  function composite(w, entry) {
    const P = S.palette;
    const pw = w.W * CS * SS, ph = w.H * CS * SS;
    if (!entry.composite) entry.composite = makeCanvas(pw, ph);
    const ctx = entry.composite.getContext('2d');
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, pw, ph);
    ctx.drawImage(entry.base.canvas, 0, 0);
    ctx.scale(SS, SS);
    for (const z of w.blesses) {
      const g = ctx.createRadialGradient(z.x * CS, z.y * CS, 0, z.x * CS, z.y * CS, z.r * CS * 1.2);
      g.addColorStop(0, P.alpha(P.goldBright, 0.16));
      g.addColorStop(0.7, P.alpha(P.goldBright, 0.07));
      g.addColorStop(1, P.alpha(P.goldBright, 0));
      ctx.fillStyle = g;
      ctx.beginPath(); ctx.arc(z.x * CS, z.y * CS, z.r * CS * 1.25, 0, Math.PI * 2); ctx.fill();
    }
    for (const z of w.storms) {
      const g = ctx.createRadialGradient(z.x * CS, z.y * CS, 0, z.x * CS, z.y * CS, z.r * CS * 1.3);
      g.addColorStop(0, P.alpha(P.stormGray, 0.34));
      g.addColorStop(0.75, P.alpha(P.stormGray, 0.18));
      g.addColorStop(1, P.alpha(P.stormGray, 0));
      ctx.fillStyle = g;
      ctx.beginPath(); ctx.arc(z.x * CS, z.y * CS, z.r * CS * 1.35, 0, Math.PI * 2); ctx.fill();
    }
    ctx.setTransform(1, 0, 0, 1, 0, 0);
  }

  function get(w) {
    let entry = cache.get(w);
    if (!entry) {
      entry = { base: buildBase(w), composite: null, zonesKey: '' };
      cache.set(w, entry);
    }
    const zk = w.storms.length + '/' + w.blesses.length;
    if (!entry.composite || entry.zonesKey !== zk) {
      composite(w, entry);
      entry.zonesKey = zk;
    }
    return entry;
  }

  S.terrain = { get, CS, SS };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
