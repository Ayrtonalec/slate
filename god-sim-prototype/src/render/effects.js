/* SLATE Phase 0 — the living layer: storm spirals, gold glints, dragon
   menace, event pings, migration arrows. Drawn fresh every frame. */
(function (S) {
  'use strict';
  const CS = 8;
  const pings = [];
  const arrows = [];
  let reduced = false;
  if (typeof matchMedia !== 'undefined') {
    try { reduced = matchMedia('(prefers-reduced-motion: reduce)').matches; } catch (e) { /* headless */ }
  }

  function addPing(x, y, tone) {
    pings.push({ x: (x + 0.5) * CS, y: (y + 0.5) * CS, t: 0, tone: tone || 'ink' });
    if (pings.length > 24) pings.shift();
  }
  function addArrow(fx, fy, tx, ty, tone) {
    arrows.push({ fx: (fx + 0.5) * CS, fy: (fy + 0.5) * CS, tx: (tx + 0.5) * CS, ty: (ty + 0.5) * CS, t: 0, tone: tone || 'ink' });
    if (arrows.length > 12) arrows.shift();
  }

  function toneColor(P, tone) {
    return tone === 'god' ? P.gold : tone === 'doom' ? P.blood : P.ink;
  }

  function draw(ctx, w, cam, t, dt) {
    const P = S.palette;
    const lw = (v) => v / Math.sqrt(cam.zoom);

    // Storm spirals + rain.
    for (const z of w.storms) {
      const cx = (z.x + 0.5) * CS, cy = (z.y + 0.5) * CS, R = z.r * CS;
      ctx.strokeStyle = P.alpha(P.stormGray, 0.55);
      ctx.lineWidth = lw(1.1);
      const spin = reduced ? 0 : t * 0.45;
      for (let k = 0; k < 3; k++) {
        const a0 = spin + k * (Math.PI * 2 / 3);
        ctx.beginPath();
        for (let u = 0; u <= 1; u += 0.08) {
          const ang = a0 + u * 2.6;
          const rr = R * (0.25 + u * 0.65);
          const px = cx + Math.cos(ang) * rr, py = cy + Math.sin(ang) * rr * 0.8;
          if (u === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
        }
        ctx.stroke();
      }
      ctx.strokeStyle = P.alpha(P.stormGray, 0.5);
      ctx.lineWidth = lw(0.8);
      for (let k = 0; k < 7; k++) {
        const hx = S.rng.hash2(k, z.x, 41), hy = S.rng.hash2(z.y, k, 42);
        const ph = reduced ? 0.5 : (t * 1.6 + hx * 7) % 1;
        if (ph > 0.75) continue;
        const px = cx + (hx - 0.5) * R * 1.7, py = cy + (hy - 0.5) * R * 1.4 + ph * 6;
        ctx.beginPath();
        ctx.moveTo(px, py);
        ctx.lineTo(px - 1.6, py + 4);
        ctx.stroke();
      }
    }

    // Blessing motes.
    for (const z of w.blesses) {
      const cx = (z.x + 0.5) * CS, cy = (z.y + 0.5) * CS, R = z.r * CS;
      ctx.fillStyle = P.alpha(P.goldBright, 0.7);
      for (let k = 0; k < 6; k++) {
        const hx = S.rng.hash2(k, z.x, 43) - 0.5, hy = S.rng.hash2(z.y, k, 44) - 0.5;
        const drift = reduced ? 0 : Math.sin(t * 0.9 + k * 1.7) * 2.5;
        const px = cx + hx * R * 1.5, py = cy + hy * R * 1.2 + drift;
        const a = reduced ? 0.5 : 0.35 + 0.35 * Math.sin(t * 1.3 + k * 2.1);
        if (a <= 0.05) continue;
        ctx.globalAlpha = a;
        ctx.beginPath();
        ctx.arc(px, py, lw(1.1), 0, Math.PI * 2);
        ctx.fill();
      }
      ctx.globalAlpha = 1;
    }

    // Gold glints on revealed veins.
    for (const v of w.veins) {
      if (!v.revealed) continue;
      const a = reduced ? 0.5 : Math.max(0, Math.sin(t * 2 + S.rng.hash2(v.x, v.y, 45) * 6.28)) * 0.8;
      if (a < 0.08) continue;
      const cx = (v.x + 0.5) * CS, cy = (v.y + 0.1) * CS;
      const s = lw(3);
      ctx.strokeStyle = P.alpha(P.goldBright, a);
      ctx.lineWidth = lw(0.7);
      ctx.beginPath();
      ctx.moveTo(cx - s, cy); ctx.lineTo(cx + s, cy);
      ctx.moveTo(cx, cy - s); ctx.lineTo(cx, cy + s);
      ctx.stroke();
    }

    // Dragon menace rings.
    for (const d of w.dragons) {
      const cx = (d.x + 0.5) * CS, cy = (d.y + 0.5) * CS;
      const pulse = reduced ? 0.09 : 0.06 + 0.04 * Math.sin(t * 1.1);
      ctx.strokeStyle = P.alpha(P.blood, pulse * 3);
      ctx.fillStyle = P.alpha(P.blood, pulse * 0.55);
      ctx.lineWidth = lw(1);
      ctx.beginPath();
      ctx.arc(cx, cy, 8 * CS, 0, Math.PI * 2);
      ctx.fill();
      ctx.setLineDash([4, 3]);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    // Pings.
    for (let i = pings.length - 1; i >= 0; i--) {
      const p = pings[i];
      p.t += dt;
      if (p.t > 1.6) { pings.splice(i, 1); continue; }
      const u = p.t / 1.6;
      const col = toneColor(P, p.tone);
      ctx.strokeStyle = P.alpha(col, (1 - u) * 0.85);
      ctx.lineWidth = lw(1.4);
      ctx.beginPath();
      ctx.arc(p.x, p.y, 3 + u * 26 / Math.sqrt(cam.zoom), 0, Math.PI * 2);
      ctx.stroke();
      ctx.strokeStyle = P.alpha(col, (1 - u) * 0.45);
      ctx.beginPath();
      ctx.arc(p.x, p.y, 3 + u * 40 / Math.sqrt(cam.zoom), 0, Math.PI * 2);
      ctx.stroke();
    }

    // Migration / raid arrows: draw in, then fade.
    for (let i = arrows.length - 1; i >= 0; i--) {
      const a = arrows[i];
      a.t += dt;
      if (a.t > 2.4) { arrows.splice(i, 1); continue; }
      const drawU = Math.min(1, a.t / 0.9);
      const fade = a.t < 1.4 ? 1 : 1 - (a.t - 1.4) / 1;
      const col = toneColor(P, a.tone);
      const mx = (a.fx + a.tx) / 2 + (a.fy - a.ty) * 0.25;
      const my = (a.fy + a.ty) / 2 + (a.tx - a.fx) * 0.25;
      ctx.strokeStyle = P.alpha(col, 0.65 * fade);
      ctx.lineWidth = lw(1.1);
      ctx.setLineDash([3, 2.4]);
      ctx.beginPath();
      ctx.moveTo(a.fx, a.fy);
      // Trace the quadratic partially for the draw-in.
      const steps = 24;
      let px = a.fx, py = a.fy;
      for (let k = 1; k <= steps * drawU; k++) {
        const u = k / steps;
        px = (1 - u) * (1 - u) * a.fx + 2 * (1 - u) * u * mx + u * u * a.tx;
        py = (1 - u) * (1 - u) * a.fy + 2 * (1 - u) * u * my + u * u * a.ty;
        ctx.lineTo(px, py);
      }
      ctx.stroke();
      ctx.setLineDash([]);
      if (drawU >= 1) {
        const ang = Math.atan2(a.ty - my, a.tx - mx);
        ctx.fillStyle = P.alpha(col, 0.7 * fade);
        ctx.beginPath();
        ctx.moveTo(a.tx, a.ty);
        ctx.lineTo(a.tx - Math.cos(ang - 0.45) * lw(5), a.ty - Math.sin(ang - 0.45) * lw(5));
        ctx.lineTo(a.tx - Math.cos(ang + 0.45) * lw(5), a.ty - Math.sin(ang + 0.45) * lw(5));
        ctx.closePath();
        ctx.fill();
      }
    }
  }

  S.effects = { draw, addPing, addArrow };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
