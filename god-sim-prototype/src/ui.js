/* SLATE Phase 0 — UI chrome: atlas margins around the living map.
   Cartouche title bar, wax-seal power rail, ledger-style chronicle,
   legend, tooltip, toast, and the first-run intro. */
(function (S) {
  'use strict';

  const ICONS = {
    inspect: '<svg viewBox="0 0 24 24"><path d="M10 4v9m0-9c0-1.2 1.8-1.2 1.8 0v8m0-7.4c0-1.2 1.8-1.2 1.8 0v7.6m0-6.4c0-1.2 1.8-1.2 1.8 0V15c0 3.5-2 5.5-5.2 5.5-2.6 0-3.8-1.3-4.8-3.2L4 14.6c-.6-1.1.8-2.1 1.7-1.2L7 14.8V6.2C7 5 8.4 5 8.6 6" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    gold: '<svg viewBox="0 0 24 24"><circle cx="8.5" cy="15.5" r="3" fill="none" stroke="currentColor" stroke-width="1.5"/><circle cx="15" cy="16" r="2.5" fill="none" stroke="currentColor" stroke-width="1.5"/><circle cx="12" cy="10.5" r="2.7" fill="none" stroke="currentColor" stroke-width="1.5"/><path d="M12 3.5v3M10.6 5h2.8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
    curse: '<svg viewBox="0 0 24 24"><path d="M6.5 13a4 4 0 1 1 .6-7.9A5 5 0 0 1 16.8 6 3.5 3.5 0 0 1 17 13z" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round"/><path d="M12.5 14.5 10 18h3l-2.2 3.5M7 15.5l-1 2M16.5 15.5l-1 2" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" fill="none"/></svg>',
    bless: '<svg viewBox="0 0 24 24"><circle cx="12" cy="9" r="3.2" fill="none" stroke="currentColor" stroke-width="1.5"/><path d="M12 2.5v2M17 4.5l-1.4 1.4M21 9.5h-2M7 4.5l1.4 1.4M3 9.5h2M12 15.5c0 2.5-1.5 3-1.5 5M12 15.5c0 2.5 1.5 3 1.5 5M12 15.5V21" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" fill="none"/></svg>',
    export: '<svg viewBox="0 0 24 24"><path d="M12 4v10m0 0 -3.5-3.5M12 14l3.5-3.5M5 17v2.5h14V17" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    pause: '<svg viewBox="0 0 24 24"><path d="M8.5 5.5v13M15.5 5.5v13" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"/></svg>',
    play: '<svg viewBox="0 0 24 24"><path d="M8 5.2v13.6L19 12z" fill="currentColor"/></svg>',
  };

  const POWERS = [
    { id: 'inspect', label: 'Inspect', hint: 'The open hand. Hover to read the world; click to mark a place.' },
    { id: 'gold', label: 'Seed Gold', hint: 'Bloom a vein of gold in mountains or hills. Miners will come. So might other things.' },
    { id: 'curse', label: 'Curse Skies', hint: 'An unending storm, five leagues wide. Nothing will grow beneath it.' },
    { id: 'bless', label: 'Bless Land', hint: 'Gentle rain and kind soil. Settlers will find it.' },
  ];

  function el(tag, cls, html) {
    const e = document.createElement(tag);
    if (cls) e.className = cls;
    if (html !== undefined) e.innerHTML = html;
    return e;
  }

  function build(app) {
    const root = document.getElementById('app');

    // --- Top bar.
    const bar = el('header', '', `
      <div class="cartouche">
        <h1>Slate</h1><span class="sub">The Living Atlas</span>
      </div>
      <div id="date" aria-live="off"><span id="date-year">Year 0</span><span id="date-season">Thaw</span></div>
      <div id="timectl" role="group" aria-label="Time controls">
        <button id="btn-pause" class="ctl icon" title="Pause / resume (Space)">${ICONS.pause}</button>
        <span class="speeds">
          <button class="ctl speed active" data-speed="1" title="One year per second (1)">I</button>
          <button class="ctl speed" data-speed="6" title="Six years per second (2)">II</button>
          <button class="ctl speed" data-speed="24" title="A generation per second (3)">III</button>
        </span>
        <button id="btn-new" class="ctl text" title="Generate a fresh world">New world</button>
      </div>`);
    bar.id = 'topbar';
    root.appendChild(bar);

    // --- Powers rail.
    const rail = el('nav', '', '');
    rail.id = 'powers';
    rail.setAttribute('aria-label', 'Divine powers');
    for (const p of POWERS) {
      const b = el('button', 'seal' + (p.id === 'inspect' ? ' active' : ''),
        `${ICONS[p.id]}<span class="seal-label">${p.label}</span>`);
      b.dataset.power = p.id;
      b.title = p.hint;
      b.addEventListener('click', () => setPower(app, p.id));
      rail.appendChild(b);
    }
    root.appendChild(rail);

    // --- Chronicle ledger.
    const chron = el('aside', '', `
      <header>
        <h2>Chronicle</h2>
        <select id="chron-filter" title="Which events to show">
          <option value="1">All</option>
          <option value="2" selected>Notable</option>
          <option value="3">Major</option>
        </select>
        <button id="btn-export" class="ctl icon" title="Download the full chronicle as Markdown">${ICONS.export}</button>
        <button id="btn-chron-toggle" class="ctl icon" title="Collapse the chronicle" aria-expanded="true">›</button>
      </header>
      <ol id="chron-entries" data-min="2"></ol>`);
    chron.id = 'chronicle';
    root.appendChild(chron);
    chron.querySelector('#chron-filter').addEventListener('change', (ev) => {
      chron.querySelector('#chron-entries').dataset.min = ev.target.value;
    });
    chron.querySelector('#btn-chron-toggle').addEventListener('click', () => {
      const collapsed = chron.classList.toggle('collapsed');
      const btn = chron.querySelector('#btn-chron-toggle');
      btn.setAttribute('aria-expanded', String(!collapsed));
      btn.title = collapsed ? 'Open the chronicle' : 'Collapse the chronicle';
    });
    chron.querySelector('#btn-export').addEventListener('click', () => exportChronicle(app));

    // --- Legend.
    const legend = el('details', '', `<summary>Legend</summary><div id="legend-body"></div>
      <p class="hint">Drag to pan · scroll to zoom · pick a power, then click the land</p>`);
    legend.id = 'legend';
    if (window.innerWidth > 900) legend.open = true;
    root.appendChild(legend);
    buildLegend(legend.querySelector('#legend-body'));

    // --- Tooltip & toast.
    const tip = el('div', '', ''); tip.id = 'tooltip'; tip.hidden = true; root.appendChild(tip);
    const toastEl = el('div', '', ''); toastEl.id = 'toast'; toastEl.hidden = true; root.appendChild(toastEl);

    // --- Intro.
    const intro = el('div', '', `
      <div class="card">
        <p class="over">A god-simulation prototype · Phase 0</p>
        <h1>Slate</h1>
        <p class="tag">The Living Atlas</p>
        <p>You are the god of an empty year. The world below will live, spread,
           and remember — with you or without you.</p>
        <p>Seed gold in the mountains and watch boomtowns rise. Curse the skies
           and watch the roads empty. Everything is written down.</p>
        <div class="row seedrow">
          <label for="intro-seed">World seed</label>
          <input id="intro-seed" inputmode="numeric" value="">
          <button id="intro-reroll" class="ctl text" title="Random seed">Reroll</button>
        </div>
        <div class="row">
          <button id="btn-watch" class="big">Only watch</button>
          <button id="btn-shape" class="big primary">Begin</button>
        </div>
      </div>`);
    intro.id = 'intro';
    root.appendChild(intro);

    app.ui = {
      bar, rail, chron, tip, toastEl, intro,
      entriesEl: chron.querySelector('#chron-entries'),
      yearEl: bar.querySelector('#date-year'),
      seasonEl: bar.querySelector('#date-season'),
      toastTimer: 0,
    };
    return app.ui;
  }

  function setPower(app, id) {
    app.power = id;
    document.querySelectorAll('#powers .seal').forEach((b) => {
      b.classList.toggle('active', b.dataset.power === id);
    });
    document.getElementById('map').style.cursor = id === 'inspect' ? 'grab' : 'crosshair';
  }

  function buildLegend(body) {
    const P = S.palette;
    const rows = [
      ['hamlet', (ctx) => S.features.drawSettlement(ctx, P, 11, 10, 4.5, { tier: 0, gold: false, culture: 0 })],
      ['village', (ctx) => S.features.drawSettlement(ctx, P, 11, 10, 4.5, { tier: 1, gold: false, culture: 0 })],
      ['town', (ctx) => S.features.drawSettlement(ctx, P, 11, 11, 4.2, { tier: 2, gold: false, culture: 0 })],
      ['city', (ctx) => S.features.drawSettlement(ctx, P, 11, 12, 4, { tier: 3, gold: false, culture: 0 })],
      ['ruins', (ctx) => S.features.drawRuin(ctx, P, 11, 10, 5)],
      ['gold vein', (ctx) => S.features.drawVein(ctx, P, 11, 11, 3.4, true)],
      ['dragon lair', (ctx) => S.features.drawLair(ctx, P, 11, 10, 5, false)],
    ];
    for (const [name, fn] of rows) {
      const row = document.createElement('div');
      row.className = 'legend-row';
      const c = document.createElement('canvas');
      c.width = 44; c.height = 40; c.style.width = '22px'; c.style.height = '20px';
      const ctx = c.getContext('2d');
      ctx.scale(2, 2);
      fn(ctx);
      row.appendChild(c);
      const label = document.createElement('span');
      label.textContent = name;
      row.appendChild(label);
      body.appendChild(row);
    }
  }

  function addEntry(app, e) {
    const ui = app.ui;
    const li = document.createElement('li');
    li.dataset.imp = e.imp;
    li.className = 'tone-' + e.tone;
    li.innerHTML = `<span class="yr">${e.year}</span><p>${e.tone === 'god' ? '✦ ' : e.tone === 'doom' ? '† ' : ''}${e.text}</p>`;
    if (e.x !== undefined) {
      li.tabIndex = 0;
      li.title = 'Show on the map';
      const go = () => {
        app.renderer.cam.flyTo((e.x + 0.5) * 8, (e.y + 0.5) * 8, Math.max(app.renderer.cam.zoom, 1.6));
        S.effects.addPing(e.x, e.y, e.tone);
      };
      li.addEventListener('click', go);
      li.addEventListener('keydown', (ev) => { if (ev.key === 'Enter') go(); });
    }
    ui.entriesEl.prepend(li);
    while (ui.entriesEl.children.length > 400) ui.entriesEl.lastChild.remove();
  }

  function setDate(app) {
    const w = app.world;
    app.ui.yearEl.textContent = 'Year ' + w.year;
    app.ui.seasonEl.textContent = w.monthName();
  }

  function toast(app, msg, tone) {
    const t = app.ui.toastEl;
    t.textContent = msg;
    t.className = tone ? 'tone-' + tone : '';
    t.hidden = false;
    clearTimeout(app.ui.toastTimer);
    app.ui.toastTimer = setTimeout(() => { t.hidden = true; }, 2800);
  }

  function tooltip(app, html, px, py) {
    const t = app.ui.tip;
    if (!html) { t.hidden = true; return; }
    t.innerHTML = html;
    t.hidden = false;
    const pad = 14;
    const r = t.getBoundingClientRect();
    let x = px + pad, y = py + pad;
    if (x + r.width > window.innerWidth - 8) x = px - r.width - pad;
    if (y + r.height > window.innerHeight - 8) y = py - r.height - pad;
    t.style.left = x + 'px';
    t.style.top = y + 'px';
  }

  function exportChronicle(app) {
    const md = S.chronicle.toMarkdown(app.world);
    const blob = new Blob([md], { type: 'text/markdown' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `slate-chronicle-seed${app.world.seed}-year${app.world.year}.md`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 5000);
    toast(app, 'The chronicle has been set down — ' + app.world.chronicle.events.length + ' entries.');
  }

  function updatePauseButton(app) {
    document.getElementById('btn-pause').innerHTML = app.paused ? ICONS.play : ICONS.pause;
  }

  S.ui = { build, addEntry, setDate, toast, tooltip, setPower, updatePauseButton, exportChronicle };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
