/* SLATE Phase 0 — frame orchestration: terrain plate → borders → roads →
   features → effects → labels → atlas furniture (plate frame, compass,
   scale bar). */
(function (S) {
  'use strict';
  const CS = 8;

  function create(canvas, world) {
    const ctx = canvas.getContext('2d');
    const cam = new S.Camera(world.W * CS, world.H * CS);
    const R = { canvas, ctx, cam, world, time: 0 };

    R.resize = () => {
      const dpr = Math.min(2, window.devicePixelRatio || 1);
      const vw = canvas.clientWidth, vh = canvas.clientHeight;
      canvas.width = vw * dpr; canvas.height = vh * dpr;
      cam.resize(vw, vh, dpr);
    };

    R.setWorld = (w) => {
      R.world = w;
      cam.ww = w.W * CS; cam.wh = w.H * CS;
      R.resize();
      cam.x = cam.tx = cam.ww / 2;
      cam.y = cam.ty = cam.wh / 2;
      cam.zoom = cam.tzoom = cam.minZoom * 1.02;
    };

    R.draw = (dt) => {
      const w = R.world, P = S.palette;
      R.time += dt;
      cam.update(dt);

      const dpr = cam.dpr;
      ctx.setTransform(1, 0, 0, 1, 0, 0);
      ctx.fillStyle = P.paper;
      ctx.fillRect(0, 0, canvas.width, canvas.height);

      // Subtle page grain outside the plate.
      const t = S.terrain.get(w);
      cam.apply(ctx);
      ctx.imageSmoothingEnabled = true;
      ctx.imageSmoothingQuality = 'high';
      ctx.drawImage(t.composite || t.base.canvas, 0, 0,
        w.W * CS * S.terrain.SS, w.H * CS * S.terrain.SS,
        0, 0, w.W * CS, w.H * CS);

      S.features.draw(ctx, w, cam);
      S.effects.draw(ctx, w, cam, R.time, dt);

      // Screen-space passes.
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      S.labels.draw(ctx, w, cam);
      furniture(ctx, w, cam, P);
    };

    return R;
  }

  function furniture(ctx, w, cam, P) {
    // Plate frame: double rule around the map rectangle.
    const [x0, y0] = cam.worldToScreen(0, 0);
    const [x1, y1] = cam.worldToScreen(w.W * CS, w.H * CS);
    ctx.strokeStyle = P.alpha(P.ink, 0.8);
    ctx.lineWidth = 1.6;
    ctx.strokeRect(x0 - 8, y0 - 8, (x1 - x0) + 16, (y1 - y0) + 16);
    ctx.lineWidth = 0.7;
    ctx.strokeRect(x0 - 3.5, y0 - 3.5, (x1 - x0) + 7, (y1 - y0) + 7);

    // Compass rose, bottom-right of the viewport.
    const cx = cam.vw - 64, cy = cam.vh - 72, r = 24;
    ctx.save();
    ctx.translate(cx, cy);
    ctx.strokeStyle = P.alpha(P.ink, 0.75);
    ctx.fillStyle = P.alpha(P.paper, 0.55);
    ctx.beginPath(); ctx.arc(0, 0, r + 7, 0, Math.PI * 2); ctx.fill();
    ctx.lineWidth = 0.8;
    ctx.beginPath(); ctx.arc(0, 0, r * 0.55, 0, Math.PI * 2); ctx.stroke();
    for (let k = 0; k < 8; k++) {
      const long = k % 2 === 0;
      const ang = k * Math.PI / 4 - Math.PI / 2;
      const len = long ? r : r * 0.5;
      const half = long ? 3.2 : 2;
      ctx.fillStyle = P.alpha(P.ink, long ? 0.85 : 0.5);
      ctx.beginPath();
      ctx.moveTo(Math.cos(ang) * len, Math.sin(ang) * len);
      ctx.lineTo(Math.cos(ang + Math.PI / 2) * half, Math.sin(ang + Math.PI / 2) * half);
      ctx.lineTo(Math.cos(ang + Math.PI) * half * 1.4, Math.sin(ang + Math.PI) * half * 1.4);
      ctx.lineTo(Math.cos(ang - Math.PI / 2) * half, Math.sin(ang - Math.PI / 2) * half);
      ctx.closePath();
      ctx.fill();
    }
    ctx.font = `600 10px ${P.SERIF}`;
    ctx.fillStyle = P.alpha(P.ink, 0.9);
    ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.fillText('N', 0, -r - 12);
    ctx.restore();

    // Scale bar, bottom-left: 20 cells ≈ 200 leagues.
    const segWorld = 20 * CS;
    const segPx = segWorld * cam.zoom;
    if (segPx > 40 && segPx < 400) {
      const bx = 20, by = cam.vh - 26;
      ctx.strokeStyle = P.alpha(P.ink, 0.8);
      ctx.fillStyle = P.alpha(P.ink, 0.8);
      ctx.lineWidth = 1;
      for (let k = 0; k < 4; k++) {
        const sx = bx + (segPx / 4) * k;
        if (k % 2 === 0) ctx.fillRect(sx, by, segPx / 4, 4);
        else ctx.strokeRect(sx, by, segPx / 4, 4);
      }
      ctx.font = `9.5px ${S.palette.SERIF}`;
      ctx.textAlign = 'left'; ctx.textBaseline = 'bottom';
      ctx.fillText('200 leagues', bx, by - 3);
    }
    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }

  S.renderer = { create, CS };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
