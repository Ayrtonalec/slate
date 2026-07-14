/* SLATE Phase 0 — map features drawn each frame in world space:
   settlements (glyphs grow hamlet → city), ruins, gold, dragon lairs,
   roads, and culture borders. Ink-and-paper vector art, no bitmaps. */
(function (S) {
  'use strict';
  const CS = 8;
  const cache = new WeakMap();

  function ensure(w) {
    let e = cache.get(w);
    if (!e) { e = { roadsKey: -1, roadsPath: null, bordersDirty: true, borderPaths: null, tint: null }; cache.set(w, e); }
    if (e.roadsKey !== w.roads.length) {
      const p = new Path2D();
      for (const r of w.roads) {
        const pts = r.pts;
        if (pts.length < 2) continue;
        p.moveTo((pts[0][0] + 0.5) * CS, (pts[0][1] + 0.5) * CS);
        for (let i = 1; i < pts.length - 1; i++) {
          const jx = (S.rng.hash2(i, r.a * 31 + r.b, 5) - 0.5) * CS * 0.7;
          const jy = (S.rng.hash2(r.a * 31 + r.b, i, 6) - 0.5) * CS * 0.7;
          p.lineTo((pts[i][0] + 0.5) * CS + jx, (pts[i][1] + 0.5) * CS + jy);
        }
        const last = pts[pts.length - 1];
        p.lineTo((last[0] + 0.5) * CS, (last[1] + 0.5) * CS);
      }
      e.roadsPath = p;
      e.roadsKey = w.roads.length;
    }
    if (w.dirty.borders || !e.borderPaths) {
      buildBorders(w, e);
      w.dirty.borders = false;
    }
    return e;
  }

  function buildBorders(w, e) {
    const P = S.palette;
    // Tint wash: one low-res canvas scaled up softly.
    const tc = (typeof OffscreenCanvas !== 'undefined') ? new OffscreenCanvas(w.W, w.H) : (() => { const c = document.createElement('canvas'); c.width = w.W; c.height = w.H; return c; })();
    const tctx = tc.getContext('2d');
    tctx.clearRect(0, 0, w.W, w.H);
    for (let y = 0; y < w.H; y++) {
      for (let x = 0; x < w.W; x++) {
        const c = w.claims[w.idx(x, y)];
        if (c < 0) continue;
        tctx.fillStyle = P.alpha(P.cultureInk(c), 0.14);
        tctx.fillRect(x, y, 1, 1);
      }
    }
    // Boundary strokes per culture.
    const paths = new Map();
    const get = (c) => { if (!paths.has(c)) paths.set(c, new Path2D()); return paths.get(c); };
    for (let y = 0; y < w.H; y++) {
      for (let x = 0; x < w.W; x++) {
        const i = w.idx(x, y);
        const c = w.claims[i];
        if (c < 0) continue;
        const right = x < w.W - 1 ? w.claims[i + 1] : -1;
        const below = y < w.H - 1 ? w.claims[i + w.W] : -1;
        if (right !== c && w.isLandAt(x + 1, y)) {
          const p = get(c);
          p.moveTo((x + 1) * CS, y * CS); p.lineTo((x + 1) * CS, (y + 1) * CS);
        }
        if (below !== c && w.isLandAt(x, y + 1)) {
          const p = get(c);
          p.moveTo(x * CS, (y + 1) * CS); p.lineTo((x + 1) * CS, (y + 1) * CS);
        }
      }
    }
    e.tint = tc;
    e.borderPaths = paths;
  }

  function draw(ctx, w, cam) {
    const P = S.palette;
    const e = ensure(w);
    const lw = (v) => v / Math.sqrt(cam.zoom); // ink keeps near-constant visual weight

    // Culture tint + borders.
    ctx.globalAlpha = 0.9;
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(e.tint, 0, 0, w.W, w.H, 0, 0, w.W * CS, w.H * CS);
    ctx.globalAlpha = 1;
    ctx.setLineDash([3, 2.4]);
    for (const [c, p] of e.borderPaths) {
      ctx.strokeStyle = P.alpha(P.cultureInk(c), 0.68);
      ctx.lineWidth = lw(0.9);
      ctx.stroke(p);
    }
    ctx.setLineDash([]);

    // Roads.
    ctx.strokeStyle = P.alpha(P.ink, 0.42);
    ctx.lineWidth = lw(0.75);
    ctx.setLineDash([2.6, 1.9]);
    ctx.stroke(e.roadsPath);
    ctx.setLineDash([]);

    // Symbol size: near-constant on screen, clamped in world units.
    const gs = Math.max(2.6, Math.min(9, 4.2 / Math.pow(cam.zoom, 0.62)));

    // Ruins first (under living settlements).
    for (const r of w.ruins) drawRuin(ctx, P, (r.x + 0.5) * CS, (r.y + 0.5) * CS, gs * 0.8);

    // Revealed gold veins not yet inside a settlement's glyph.
    for (const v of w.veins) {
      if (!v.revealed) continue;
      drawVein(ctx, P, (v.x + 0.5) * CS, (v.y + 0.5) * CS, gs * 0.55, v.divine);
    }

    // Dragon lairs.
    for (const l of w.deadLairs) drawLair(ctx, P, (l.x + 0.5) * CS, (l.y + 0.5) * CS, gs * 0.9, true);
    for (const d of w.dragons) drawLair(ctx, P, (d.x + 0.5) * CS, (d.y + 0.5) * CS, gs * 0.9, false);

    // Settlements, north to south so glyphs overlap like an engraving.
    const alive = w.settlements.filter((s) => !s.ruined).sort((a, b) => a.y - b.y);
    for (const s of alive) {
      drawSettlement(ctx, P, (s.x + 0.5) * CS, (s.y + 0.5) * CS, gs, s);
    }
  }

  function hut(ctx, P, x, y, s, fill) {
    ctx.beginPath();
    ctx.rect(x - s / 2, y - s * 0.35, s, s * 0.62);
    ctx.fillStyle = fill;
    ctx.fill();
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(x - s * 0.62, y - s * 0.32);
    ctx.lineTo(x, y - s * 0.85);
    ctx.lineTo(x + s * 0.62, y - s * 0.32);
    ctx.closePath();
    ctx.fillStyle = P.alpha(P.ink, 0.25);
    ctx.fill();
    ctx.stroke();
  }

  function drawSettlement(ctx, P, x, y, gs, s) {
    const tier = s.tier;
    // Paper halo so the glyph reads against any wash.
    ctx.fillStyle = P.alpha(P.paper, 0.5);
    ctx.beginPath();
    ctx.arc(x, y, gs * (0.9 + tier * 0.35), 0, Math.PI * 2);
    ctx.fill();

    ctx.strokeStyle = P.alpha(P.ink, 0.85);
    ctx.lineWidth = gs * 0.09;
    ctx.lineJoin = 'round';
    const paperFill = P.alpha('#f4ecd8', 0.9);

    if (tier === 0) {
      hut(ctx, P, x, y, gs * 0.75, paperFill);
    } else if (tier === 1) {
      hut(ctx, P, x - gs * 0.38, y + gs * 0.12, gs * 0.66, paperFill);
      hut(ctx, P, x + gs * 0.38, y - gs * 0.05, gs * 0.72, paperFill);
    } else if (tier === 2) {
      ctx.beginPath();
      ctx.arc(x, y, gs * 0.95, 0, Math.PI * 2);
      ctx.stroke();
      hut(ctx, P, x - gs * 0.42, y + gs * 0.2, gs * 0.6, paperFill);
      hut(ctx, P, x + gs * 0.38, y + gs * 0.14, gs * 0.55, paperFill);
      tower(ctx, P, x, y - gs * 0.3, gs * 0.55, paperFill);
    } else {
      ctx.beginPath();
      ctx.arc(x, y, gs * 1.25, 0, Math.PI * 2);
      ctx.stroke();
      ctx.beginPath();
      ctx.arc(x, y, gs * 0.95, 0, Math.PI * 2);
      ctx.strokeStyle = P.alpha(P.ink, 0.4);
      ctx.stroke();
      ctx.strokeStyle = P.alpha(P.ink, 0.85);
      hut(ctx, P, x - gs * 0.5, y + gs * 0.3, gs * 0.55, paperFill);
      hut(ctx, P, x + gs * 0.48, y + gs * 0.22, gs * 0.5, paperFill);
      tower(ctx, P, x - gs * 0.05, y - gs * 0.25, gs * 0.75, paperFill);
      // Banner in culture ink.
      const cc = P.cultureInk(s.culture);
      ctx.strokeStyle = P.alpha(P.ink, 0.9);
      ctx.beginPath();
      ctx.moveTo(x - gs * 0.05, y - gs * 1.05);
      ctx.lineTo(x - gs * 0.05, y - gs * 1.6);
      ctx.stroke();
      ctx.fillStyle = cc;
      ctx.beginPath();
      ctx.moveTo(x - gs * 0.05, y - gs * 1.6);
      ctx.lineTo(x + gs * 0.5, y - gs * 1.45);
      ctx.lineTo(x - gs * 0.05, y - gs * 1.3);
      ctx.closePath();
      ctx.fill();
    }

    if (s.gold) {
      ctx.fillStyle = P.gold;
      ctx.beginPath();
      ctx.arc(x + gs * 0.85, y - gs * 0.85, gs * 0.22, 0, Math.PI * 2);
      ctx.fill();
      ctx.strokeStyle = P.alpha(P.ink, 0.6);
      ctx.lineWidth = gs * 0.06;
      ctx.stroke();
    }
  }

  function tower(ctx, P, x, y, s, fill) {
    ctx.beginPath();
    ctx.rect(x - s * 0.28, y - s * 0.9, s * 0.56, s * 1.1);
    ctx.fillStyle = fill;
    ctx.fill();
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(x - s * 0.38, y - s * 0.9);
    ctx.lineTo(x, y - s * 1.35);
    ctx.lineTo(x + s * 0.38, y - s * 0.9);
    ctx.closePath();
    ctx.fillStyle = P.alpha(P.ink, 0.3);
    ctx.fill();
    ctx.stroke();
  }

  function drawRuin(ctx, P, x, y, s) {
    ctx.strokeStyle = P.alpha(P.ink, 0.45);
    ctx.lineWidth = s * 0.1;
    ctx.beginPath();
    ctx.moveTo(x - s * 0.6, y + s * 0.4); ctx.lineTo(x - s * 0.6, y - s * 0.25);
    ctx.moveTo(x - s * 0.6, y - s * 0.25); ctx.lineTo(x - s * 0.15, y - s * 0.45);
    ctx.moveTo(x + s * 0.35, y + s * 0.4); ctx.lineTo(x + s * 0.35, y - s * 0.1);
    ctx.stroke();
    ctx.fillStyle = P.alpha(P.ink, 0.3);
    for (let k = 0; k < 4; k++) {
      ctx.beginPath();
      ctx.arc(x + (S.rng.hash2(k, x | 0, 9) - 0.5) * s, y + s * 0.45, s * 0.07, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  function drawVein(ctx, P, x, y, s, divine) {
    ctx.fillStyle = P.goldBright;
    ctx.strokeStyle = P.alpha(P.ink, 0.55);
    ctx.lineWidth = s * 0.12;
    for (const [dx, dy, r] of [[-0.4, 0.25, 0.34], [0.35, 0.15, 0.28], [0, -0.3, 0.3]]) {
      ctx.beginPath();
      ctx.arc(x + dx * s, y + dy * s, r * s, 0, Math.PI * 2);
      ctx.fill(); ctx.stroke();
    }
    if (divine) {
      ctx.strokeStyle = P.alpha(P.goldBright, 0.9);
      ctx.lineWidth = s * 0.14;
      ctx.beginPath();
      ctx.moveTo(x, y - s * 1.15); ctx.lineTo(x, y - s * 0.55);
      ctx.moveTo(x - s * 0.3, y - s * 0.85); ctx.lineTo(x + s * 0.3, y - s * 0.85);
      ctx.stroke();
    }
  }

  function drawLair(ctx, P, x, y, s, dead) {
    const a = dead ? 0.3 : 0.85;
    ctx.fillStyle = P.alpha('#241a10', dead ? 0.25 : 0.7);
    ctx.beginPath();
    ctx.arc(x, y + s * 0.15, s * 0.55, Math.PI, 0);
    ctx.closePath();
    ctx.fill();
    ctx.strokeStyle = P.alpha(P.ink, a);
    ctx.lineWidth = s * 0.1;
    ctx.stroke();
    if (!dead) {
      // A curl of smoke and one baleful eye.
      ctx.strokeStyle = P.alpha(P.blood, 0.75);
      ctx.lineWidth = s * 0.09;
      ctx.beginPath();
      ctx.moveTo(x + s * 0.35, y - s * 0.1);
      ctx.quadraticCurveTo(x + s * 0.6, y - s * 0.5, x + s * 0.4, y - s * 0.75);
      ctx.quadraticCurveTo(x + s * 0.25, y - s * 0.95, x + s * 0.45, y - s * 1.05);
      ctx.stroke();
      ctx.fillStyle = P.blood;
      ctx.beginPath();
      ctx.arc(x - s * 0.12, y - s * 0.02, s * 0.09, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  S.features = { draw, ensure, drawSettlement, drawRuin, drawVein, drawLair, CS };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
