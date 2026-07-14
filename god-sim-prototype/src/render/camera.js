/* SLATE Phase 0 — camera: pan/zoom over the atlas with smooth easing. */
(function (S) {
  'use strict';

  class Camera {
    constructor(worldW, worldH) {
      this.ww = worldW; this.wh = worldH;
      this.vw = 800; this.vh = 600; this.dpr = 1;
      this.x = worldW / 2; this.y = worldH / 2;
      this.zoom = 0.5;
      this.tx = this.x; this.ty = this.y; this.tzoom = this.zoom;
      this.minZoom = 0.3; this.maxZoom = 7;
    }
    resize(vw, vh, dpr) {
      this.vw = vw; this.vh = vh; this.dpr = dpr;
      // Fit the whole plate with a paper margin.
      this.minZoom = Math.min(vw / this.ww, vh / this.wh) * 0.82;
      if (this.zoom < this.minZoom) { this.zoom = this.tzoom = this.minZoom; }
    }
    fit() {
      this.tzoom = this.minZoom * 1.02;
      this.tx = this.ww / 2; this.ty = this.wh / 2;
    }
    apply(ctx) {
      const z = this.zoom * this.dpr;
      ctx.setTransform(z, 0, 0, z,
        this.dpr * this.vw / 2 - this.x * z,
        this.dpr * this.vh / 2 - this.y * z);
    }
    screenToWorld(px, py) {
      return [
        (px - this.vw / 2) / this.zoom + this.x,
        (py - this.vh / 2) / this.zoom + this.y,
      ];
    }
    worldToScreen(wx, wy) {
      return [
        (wx - this.x) * this.zoom + this.vw / 2,
        (wy - this.y) * this.zoom + this.vh / 2,
      ];
    }
    zoomAt(px, py, factor) {
      const [wx, wy] = this.screenToWorld(px, py);
      this.tzoom = Math.max(this.minZoom, Math.min(this.maxZoom, this.tzoom * factor));
      // Keep the point under the cursor fixed.
      this.tx = wx - (px - this.vw / 2) / this.tzoom;
      this.ty = wy - (py - this.vh / 2) / this.tzoom;
    }
    panBy(dx, dy) {
      this.tx -= dx / this.zoom; this.ty -= dy / this.zoom;
      this.x -= dx / this.zoom; this.y -= dy / this.zoom; // pan feels 1:1, no easing lag
    }
    flyTo(wx, wy, zoom) {
      this.tx = wx; this.ty = wy;
      if (zoom) this.tzoom = Math.max(this.minZoom, Math.min(this.maxZoom, zoom));
    }
    clamp() {
      const mx = this.ww * 0.15, my = this.wh * 0.15;
      this.tx = Math.max(-mx + this.ww * 0, Math.min(this.ww + mx - 0, this.tx));
      this.ty = Math.max(-my, Math.min(this.wh + my, this.ty));
      this.x = Math.max(-mx, Math.min(this.ww + mx, this.x));
      this.y = Math.max(-my, Math.min(this.wh + my, this.y));
    }
    update(dt) {
      const k = 1 - Math.exp(-dt * 8);
      this.x += (this.tx - this.x) * k;
      this.y += (this.ty - this.y) * k;
      this.zoom += (this.tzoom - this.zoom) * k;
      this.clamp();
    }
  }

  S.Camera = Camera;
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
