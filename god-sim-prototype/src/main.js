/* SLATE Phase 0 — boot, input, and the main loop. */
(function (S) {
  'use strict';
  const CS = 8;

  const app = {
    world: null, renderer: null, ui: null,
    speed: 6, paused: true, power: 'inspect',
  };
  S.app = app;

  function params() {
    const q = new URLSearchParams(location.search);
    return {
      seed: q.get('seed'),
      run: parseInt(q.get('run') || '0', 10),
      nointro: q.has('run') || q.has('nointro'),
      speed: parseFloat(q.get('speed') || '0'),
    };
  }

  function boot(seed) {
    const w = S.world.create(seed);
    app.world = w;
    w.onEvent = (e) => {
      S.ui.addEntry(app, e);
      const slowEnough = app.speed <= 6;
      if (e.imp >= 3 || (e.imp === 2 && slowEnough && !app.paused)) {
        if (e.x !== undefined) S.effects.addPing(e.x, e.y, e.tone);
      }
      if (e.fx !== undefined && e.x !== undefined && (slowEnough || e.imp >= 3)) {
        S.effects.addArrow(e.fx, e.fy, e.x, e.y, e.tone === 'doom' ? 'doom' : 'ink');
      }
    };
    // Backfill chronicle panel with founding events.
    for (const e of w.chronicle.events) S.ui.addEntry(app, e);
    app.renderer.setWorld(w);
    S.ui.setDate(app);
  }

  function newWorld(seed) {
    document.getElementById('chron-entries').innerHTML = '';
    boot(seed);
    S.ui.toast(app, 'A new world · seed ' + seed);
  }

  // --- fixed-step sim scheduler ---
  let acc = 0;
  function simStep(dt) {
    if (app.paused) return;
    acc += dt * app.speed * 12; // ticks (months) per second
    let n = Math.min(60, Math.floor(acc));
    acc -= Math.floor(acc);
    while (n-- > 0) S.sim.tick(app.world);
  }

  // --- input ---
  function wireInput(canvas) {
    const cam = () => app.renderer.cam;
    let dragging = false, moved = 0, lastX = 0, lastY = 0;
    const pointers = new Map();
    let pinchDist = 0;

    canvas.addEventListener('pointerdown', (e) => {
      canvas.setPointerCapture(e.pointerId);
      pointers.set(e.pointerId, [e.clientX, e.clientY]);
      if (pointers.size === 2) {
        const [a, b] = [...pointers.values()];
        pinchDist = Math.hypot(a[0] - b[0], a[1] - b[1]);
      }
      dragging = true; moved = 0;
      lastX = e.clientX; lastY = e.clientY;
    });
    canvas.addEventListener('pointermove', (e) => {
      if (pointers.has(e.pointerId)) pointers.set(e.pointerId, [e.clientX, e.clientY]);
      if (pointers.size === 2) {
        const [a, b] = [...pointers.values()];
        const d = Math.hypot(a[0] - b[0], a[1] - b[1]);
        if (pinchDist > 0) {
          cam().zoomAt((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, d / pinchDist);
        }
        pinchDist = d;
        moved = 99;
        return;
      }
      if (dragging) {
        const dx = e.clientX - lastX, dy = e.clientY - lastY;
        moved += Math.abs(dx) + Math.abs(dy);
        cam().panBy(dx, dy);
        lastX = e.clientX; lastY = e.clientY;
        S.ui.tooltip(app, null);
      } else {
        hover(e.clientX, e.clientY);
      }
    });
    const endPointer = (e) => {
      pointers.delete(e.pointerId);
      if (pointers.size < 2) pinchDist = 0;
      if (dragging && moved < 5) click(e.clientX, e.clientY);
      dragging = pointers.size > 0;
    };
    canvas.addEventListener('pointerup', endPointer);
    canvas.addEventListener('pointercancel', endPointer);
    canvas.addEventListener('pointerleave', () => S.ui.tooltip(app, null));
    canvas.addEventListener('wheel', (e) => {
      e.preventDefault();
      cam().zoomAt(e.clientX, e.clientY, Math.exp(-e.deltaY * 0.0014));
    }, { passive: false });

    window.addEventListener('keydown', (e) => {
      if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') return;
      if (e.code === 'Space') { e.preventDefault(); togglePause(); }
      else if (e.key === '1' || e.key === '2' || e.key === '3') setSpeed([1, 6, 24][+e.key - 1]);
      else if (e.key === 'i') S.ui.setPower(app, 'inspect');
      else if (e.key === 'g') S.ui.setPower(app, 'gold');
      else if (e.key === 'c') S.ui.setPower(app, 'curse');
      else if (e.key === 'b') S.ui.setPower(app, 'bless');
    });
  }

  function worldCell(px, py) {
    const [wx, wy] = app.renderer.cam.screenToWorld(px, py);
    return [wx / CS - 0.5, wy / CS - 0.5];
  }

  function click(px, py) {
    const [cx, cy] = worldCell(px, py);
    const w = app.world;
    if (app.power === 'inspect') {
      const s = w.settlementNear(Math.round(cx), Math.round(cy), 2);
      if (s) S.effects.addPing(s.x, s.y, 'ink');
      return;
    }
    const fn = app.power === 'gold' ? S.powers.seedGold
      : app.power === 'curse' ? S.powers.curseWeather
      : S.powers.blessLand;
    const res = fn(w, cx, cy);
    if (!res.ok) {
      S.ui.toast(app, res.msg, 'doom');
    } else {
      S.effects.addPing(res.x, res.y, app.power === 'curse' ? 'doom' : 'god');
      if (app.paused) togglePause(); // an act of god sets time moving
    }
  }

  function hover(px, py) {
    const w = app.world;
    if (!w) return;
    const [fx, fy] = worldCell(px, py);
    const x = Math.round(fx), y = Math.round(fy);
    if (!w.inB(x, y)) { S.ui.tooltip(app, null); return; }
    const P = S.palette;
    const s = w.settlementNear(x, y, 1.6);
    if (s) {
      const c = S.CULTURES[s.culture];
      const flags = [];
      if (s.gold) flags.push('<em class="f-gold">gold-blessed</em>');
      if (w.stormAt(s.x, s.y)) flags.push('<em class="f-doom">storm-cursed</em>');
      if (w.blessAt(s.x, s.y)) flags.push('<em class="f-god">favored</em>');
      for (const d of w.dragons) {
        if ((s.x - d.x) ** 2 + (s.y - d.y) ** 2 <= 64) { flags.push('<em class="f-doom">in the wyrm’s shadow</em>'); break; }
      }
      S.ui.tooltip(app,
        `<h3>${s.name}</h3>
         <p>${w.tierName(s.tier)} of the ${c.name} · pop ~${Math.round(s.pop / 10) * 10}</p>
         <p>founded Year ${s.foundedYear}${flags.length ? ' · ' + flags.join(' · ') : ''}</p>`,
        px, py);
      return;
    }
    for (const r of w.ruins) {
      if ((r.x - x) ** 2 + (r.y - y) ** 2 <= 2.6) {
        S.ui.tooltip(app, `<h3>${r.alt || 'Ruins of ' + r.name}</h3>
          <p>${r.alt ? 'once ' + r.name + ' · ' : ''}abandoned Year ${r.year}${r.cause === 'dragon' ? ' · dragonfire' : r.cause === 'storm' ? ' · the endless storm' : ''}</p>`, px, py);
        return;
      }
    }
    for (const d of w.dragons) {
      if ((d.x - x) ** 2 + (d.y - y) ** 2 <= 2.6) {
        S.ui.tooltip(app, `<h3>${d.name}</h3><p>nested here since Year ${d.bornYear}</p>`, px, py);
        return;
      }
    }
    const i = w.idx(x, y);
    const biome = P.BIOME_NAMES[w.biome[i]];
    const region = w.regionOf[i] >= 0 ? w.regions[w.regionOf[i]].name : null;
    const vein = w.goldVeinNear(x, y, 1.5, true);
    let extra = '';
    if (vein) extra = '<p><em class="f-gold">a gold vein glitters here</em></p>';
    else if (w.stormAt(x, y)) extra = '<p><em class="f-doom">cursed skies</em></p>';
    else if (w.blessAt(x, y)) extra = '<p><em class="f-god">blessed ground</em></p>';
    else if (w.isLandAt(x, y)) {
      const f = w.fert[i];
      extra = `<p>${f > 0.8 ? 'rich soil' : f > 0.45 ? 'fair soil' : 'thin soil'}</p>`;
    }
    S.ui.tooltip(app, `<h3>${region || (w.isLandAt(x, y) ? 'Open country' : 'Open water')}</h3><p>${biome}</p>${extra}`, px, py);
  }

  function togglePause() {
    app.paused = !app.paused;
    S.ui.updatePauseButton(app);
  }
  function setSpeed(v) {
    app.speed = v;
    document.querySelectorAll('#timectl .speed').forEach((b) => {
      b.classList.toggle('active', +b.dataset.speed === v);
    });
    if (app.paused) togglePause();
  }

  // --- fast-forward for screenshots / sharing (?run=400) ---
  function autorun(years, done) {
    const w = app.world;
    const overlay = document.createElement('div');
    overlay.id = 'ffwd';
    overlay.textContent = 'The chronicle is being written…';
    document.getElementById('app').appendChild(overlay);
    const target = years;
    (function chunk() {
      const step = Math.min(25, target - w.year);
      S.sim.runYears(w, step);
      overlay.textContent = `The chronicle is being written… Year ${w.year}`;
      S.ui.setDate(app);
      if (w.year < target) requestAnimationFrame(chunk);
      else { overlay.remove(); done(); }
    })();
  }

  function start() {
    const q = params();
    const canvas = document.getElementById('map');
    app.renderer = S.renderer.create(canvas, { W: 192, H: 120 });
    S.ui.build(app);
    wireInput(canvas);
    window.addEventListener('resize', () => app.renderer.resize());

    const seed = q.seed !== null ? q.seed : (Math.random() * 100000 | 0);
    boot(seed);

    // Intro wiring.
    const intro = document.getElementById('intro');
    const seedInput = document.getElementById('intro-seed');
    seedInput.value = seed;
    document.getElementById('intro-reroll').addEventListener('click', () => {
      seedInput.value = Math.random() * 100000 | 0;
    });
    const dismiss = (watch) => {
      const sv = seedInput.value.trim();
      if (sv && String(app.world.seed) !== sv) newWorld(sv);
      intro.remove();
      setSpeed(watch ? 24 : 1);
    };
    document.getElementById('btn-watch').addEventListener('click', () => dismiss(true));
    document.getElementById('btn-shape').addEventListener('click', () => dismiss(false));

    document.getElementById('btn-pause').addEventListener('click', togglePause);
    document.querySelectorAll('#timectl .speed').forEach((b) => {
      b.addEventListener('click', () => setSpeed(+b.dataset.speed));
    });
    document.getElementById('btn-new').addEventListener('click', () => {
      newWorld(Math.random() * 100000 | 0);
    });

    if (q.nointro) intro.remove();
    if (q.speed) setSpeed(q.speed);

    // Main loop.
    let last = performance.now();
    function frame(now) {
      const dt = Math.min(0.1, (now - last) / 1000);
      last = now;
      simStep(dt);
      if (app.world.tick !== app._shownTick) {
        app._shownTick = app.world.tick;
        S.ui.setDate(app);
      }
      app.renderer.draw(dt);
      requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);

    if (q.run > 0) {
      autorun(q.run, () => {
        app.paused = true;
        S.ui.updatePauseButton(app);
        requestAnimationFrame(() => requestAnimationFrame(() => { window.__slateReady = true; }));
      });
    } else {
      window.__slateReady = true;
    }
  }

  if (typeof document !== 'undefined') {
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
  }
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
