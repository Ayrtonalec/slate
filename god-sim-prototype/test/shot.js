/* SLATE Phase 0 — visual check: load the built atlas in headless Chromium,
   fast-forward a few centuries, take screenshots at several zooms, and do
   one act of god to photograph the cascade. Usage: node test/shot.js */
'use strict';
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright-core');

function findChrome() {
  const cands = [];
  const base = '/opt/pw-browsers';
  if (process.env.CHROME_PATH) cands.push(process.env.CHROME_PATH);
  cands.push(path.join(base, 'chromium'));
  for (const d of fs.existsSync(base) ? fs.readdirSync(base) : []) {
    cands.push(path.join(base, d, 'chrome-linux', 'chrome'));
    cands.push(path.join(base, d, 'chrome-linux', 'headless_shell'));
  }
  for (const c of cands) {
    try { if (fs.statSync(c).isFile()) return c; } catch (e) { /* keep looking */ }
  }
  throw new Error('no chromium found under ' + base);
}

(async () => {
  const outDir = path.join(__dirname, '..', 'shots');
  fs.mkdirSync(outDir, { recursive: true });
  const file = 'file://' + path.join(__dirname, '..', 'dist', 'slate-atlas.html');

  const browser = await chromium.launch({ executablePath: findChrome() });
  const page = await browser.newPage({ viewport: { width: 1680, height: 1000 } });
  page.on('pageerror', (e) => console.log('PAGE ERROR:', e.message));
  page.on('console', (m) => { if (m.type() === 'error') console.log('CONSOLE ERROR:', m.text()); });

  console.log('loading', file);
  await page.goto(file + '?seed=7&run=350');
  await page.waitForFunction('window.__slateReady === true', null, { timeout: 90000 });
  await page.waitForTimeout(400);

  await page.screenshot({ path: path.join(outDir, '1-overview.png') });
  console.log('shot 1-overview');

  // Zoom to the biggest city.
  await page.evaluate(() => {
    const S = window.SLATE, app = S.app;
    const best = app.world.aliveSettlements().sort((a, b) => b.pop - a.pop)[0];
    app.renderer.cam.flyTo((best.x + 0.5) * 8, (best.y + 0.5) * 8, 2.3);
  });
  await page.waitForTimeout(900);
  await page.screenshot({ path: path.join(outDir, '2-city.png') });
  console.log('shot 2-city');

  // Act of god: seed gold in wild mountains, run 80 years, photograph the boom.
  const spot = await page.evaluate(() => {
    const S = window.SLATE, app = S.app, w = app.world;
    let spot = null;
    for (const rNear of [6, 5, 4]) {
      for (let y = 4; y < w.H - 4 && !spot; y++) for (let x = 4; x < w.W - 4; x++) {
        if (w.biome[w.idx(x, y)] !== S.B.MOUNTAIN) continue;
        if (w.settlementNear(x, y, rNear)) continue;
        if (!w.settlementNear(x, y, 20)) continue;
        spot = { x, y }; break;
      }
      if (spot) break;
    }
    if (!spot) return null;
    S.powers.seedGold(w, spot.x, spot.y);
    S.sim.runYears(w, 80);
    app.renderer.cam.flyTo((spot.x + 0.5) * 8, (spot.y + 0.5) * 8, 1.9);
    return spot;
  });
  await page.waitForTimeout(900);
  await page.screenshot({ path: path.join(outDir, '3-goldrush.png') });
  console.log('shot 3-goldrush', spot);

  // Curse a coastal town and fast-forward: photograph the exodus.
  await page.evaluate(() => {
    const S = window.SLATE, app = S.app, w = app.world;
    const t = app.world.aliveSettlements().sort((a, b) => b.pop - a.pop)[2];
    S.powers.curseWeather(w, t.x, t.y);
    S.sim.runYears(w, 45);
    app.renderer.cam.flyTo((t.x + 0.5) * 8, (t.y + 0.5) * 8, 1.6);
  });
  await page.waitForTimeout(900);
  await page.screenshot({ path: path.join(outDir, '4-curse.png') });
  console.log('shot 4-curse');

  await browser.close();
  console.log('done → shots/');
})().catch((e) => { console.error(e); process.exit(1); });
