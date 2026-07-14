/* SLATE Phase 0 — deterministic seeded RNG streams.
   Every system pulls from its own labeled stream so systems can be
   added/removed without reshuffling everyone else's randomness. */
(function (S) {
  'use strict';

  function hashStr(s) {
    let h = 2166136261 >>> 0;
    for (let i = 0; i < s.length; i++) {
      h ^= s.charCodeAt(i);
      h = Math.imul(h, 16777619);
    }
    return h >>> 0;
  }

  function mulberry32(a) {
    return function () {
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  function makeRng(seed, label) {
    const fn = mulberry32(hashStr(String(seed) + '/' + label));
    return {
      next: fn,
      range: (a, b) => a + fn() * (b - a),
      int: (a, b) => a + Math.floor(fn() * (b - a + 1)),
      chance: (p) => fn() < p,
      pick: (arr) => arr[Math.floor(fn() * arr.length)],
      shuffle: (arr) => {
        const a = arr.slice();
        for (let i = a.length - 1; i > 0; i--) {
          const j = Math.floor(fn() * (i + 1));
          [a[i], a[j]] = [a[j], a[i]];
        }
        return a;
      },
    };
  }

  // Stateless position hash in [0,1) — for per-cell visual jitter.
  function hash2(x, y, seed) {
    let n = Math.imul(x, 374761393) ^ Math.imul(y, 668265263) ^ (seed | 0);
    n = Math.imul(n ^ (n >>> 13), 1274126177);
    return ((n ^ (n >>> 16)) >>> 0) / 4294967296;
  }

  S.rng = { makeRng, hashStr, mulberry32, hash2 };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
