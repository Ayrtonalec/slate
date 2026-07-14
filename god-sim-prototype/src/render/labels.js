/* SLATE Phase 0 — atlas typography: settlement names, region names in
   letterspaced small caps, italic water names. Greedy collision layout,
   redone per frame in screen space (cheap at these counts). */
(function (S) {
  'use strict';
  const CS = 8;
  const measureCache = new Map();

  function measure(ctx, text, font) {
    const k = font + '|' + text;
    let v = measureCache.get(k);
    if (v === undefined) { ctx.font = font; v = ctx.measureText(text).width; measureCache.set(k, v); }
    return v;
  }

  function spacedText(ctx, text, x, y, spacing, fill, halo) {
    const t = text.toUpperCase();
    let total = 0;
    for (const ch of t) total += ctx.measureText(ch).width + spacing;
    let cx = x - total / 2;
    for (const ch of t) {
      if (halo) { ctx.strokeText(ch, cx, y); }
      ctx.fillStyle = fill;
      ctx.fillText(ch, cx, y);
      cx += ctx.measureText(ch).width + spacing;
    }
    return total;
  }

  function draw(ctx, w, cam) {
    const P = S.palette;
    const z = cam.zoom;
    const placed = [];
    const collide = (x, y, wd, ht) => {
      for (const r of placed) {
        if (x < r.x + r.w && x + wd > r.x && y < r.y + r.h && y + ht > r.y) return true;
      }
      return false;
    };
    const place = (x, y, wd, ht) => placed.push({ x, y, w: wd, h: ht });

    ctx.textBaseline = 'middle';
    ctx.textAlign = 'left';
    ctx.lineJoin = 'round';

    const minZoomForTier = [2.2, 1.1, 0.5, 0];
    const sizeForTier = [9.5, 10.5, 12.5, 15];
    const drawSettlementLabel = (s) => {
      const [sx, sy] = cam.worldToScreen((s.x + 0.5) * CS, (s.y + 0.5) * CS);
      if (sx < -150 || sy < -30 || sx > cam.vw + 150 || sy > cam.vh + 30) return;
      const size = sizeForTier[s.tier];
      const font = `${s.tier >= 2 ? '600 ' : ''}${size}px ${P.SERIF}`;
      const wd = measure(ctx, s.name, font);
      const gy = sy + 10 + s.tier * 3.5;
      if (collide(sx - wd / 2 - 2, gy - size / 2 - 1, wd + 4, size + 2)) return;
      place(sx - wd / 2 - 2, gy - size / 2 - 1, wd + 4, size + 2);
      ctx.font = font;
      ctx.strokeStyle = P.alpha(P.paper, 0.85);
      ctx.lineWidth = 3.2;
      ctx.strokeText(s.name, sx - wd / 2, gy);
      ctx.fillStyle = P.alpha(P.ink, 0.92);
      ctx.fillText(s.name, sx - wd / 2, gy);
    };
    const byPriority = w.settlements.filter((s) => !s.ruined).sort((a, b) => b.tier - a.tier || b.pop - a.pop);

    // Cities and towns claim their space before region names.
    for (const s of byPriority) {
      if (s.tier < 2 || z < minZoomForTier[s.tier]) continue;
      drawSettlementLabel(s);
    }

    // --- Region names.
    if (z < 3.2) {
      for (const r of w.regions) {
        if (r.kind === 'ocean' && z > 1.4) continue;
        if ((r.kind === 'sea' || r.kind === 'ocean') && z > 2.2) continue;
        if (r.kind !== 'sea' && r.kind !== 'ocean' && (z < 0.45 || z > 2.6)) continue;
        const [sx, sy] = cam.worldToScreen((r.cx + 0.5) * CS, (r.cy + 0.5) * CS);
        if (sx < -200 || sy < -40 || sx > cam.vw + 200 || sy > cam.vh + 40) continue;
        const water = r.kind === 'sea' || r.kind === 'ocean';
        const size = Math.min(21, (water ? 13 : 10.5) + Math.sqrt(r.size) * 0.55);
        const font = `${water ? 'italic ' : ''}${size}px ${P.SERIF}`;
        ctx.font = font;
        const spacing = size * 0.18;
        const t = r.name.toUpperCase();
        let total = 0;
        for (const ch of t) total += ctx.measureText(ch).width + spacing;
        if (collide(sx - total / 2, sy - size / 2, total, size)) continue;
        place(sx - total / 2, sy - size / 2, total, size);
        ctx.strokeStyle = P.alpha(P.paper, 0.75);
        ctx.lineWidth = 3;
        spacedText(ctx, r.name, sx, sy, spacing, P.alpha(water ? '#51695f' : P.inkFaded, 0.75), true);
      }
    }

    // --- Villages and hamlets fill in what space remains.
    for (const s of byPriority) {
      if (s.tier >= 2 || z < minZoomForTier[s.tier]) continue;
      drawSettlementLabel(s);
    }

    // --- Ruins, when close.
    if (z > 1.5) {
      ctx.font = `italic 9.5px ${P.SERIF}`;
      for (const r of w.ruins) {
        const [sx, sy] = cam.worldToScreen((r.x + 0.5) * CS, (r.y + 0.5) * CS);
        if (sx < -100 || sy < -20 || sx > cam.vw + 100 || sy > cam.vh + 20) continue;
        const label = r.alt ? r.alt : 'ruins of ' + r.name;
        const wd = measure(ctx, label, `italic 9.5px ${P.SERIF}`);
        if (collide(sx - wd / 2, sy + 6, wd, 10)) continue;
        place(sx - wd / 2, sy + 6, wd, 10);
        ctx.strokeStyle = P.alpha(P.paper, 0.8);
        ctx.lineWidth = 2.6;
        ctx.strokeText(label, sx - wd / 2, sy + 11);
        ctx.fillStyle = P.alpha(P.inkFaded, 0.85);
        ctx.fillText(label, sx - wd / 2, sy + 11);
      }
    }
  }

  S.labels = { draw };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
