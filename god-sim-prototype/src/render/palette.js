/* SLATE Phase 0 — the atlas palette. One committed visual world:
   aged parchment, walnut ink, celadon sea, gilded gold for acts of god,
   rust red for doom. All map art derives from these values. */
(function (S) {
  'use strict';

  const P = {
    paper: '#e8dcc0',
    paperDeep: '#d9c9a3',
    paperShadow: '#c9b993',
    ink: '#3a2f1e',
    inkFaded: '#6e5f42',
    sea: '#aabfb2',
    seaDeep: '#8aa79a',
    river: '#5f7a6e',
    gold: '#b08a2e',
    goldBright: '#d4aa3c',
    blood: '#8a3b2a',
    stormGray: '#5b6470',
    biomes: {},
    cultureInk: (id) => S.CULTURES[id] ? S.CULTURES[id].color : '#666',
  };

  const B = S.B;
  P.biomes[B.OCEAN] = '#aabfb2';
  P.biomes[B.SHALLOW] = '#b6c8ba';
  P.biomes[B.PLAINS] = '#cfc194';
  P.biomes[B.FOREST] = '#98a672';
  P.biomes[B.HILLS] = '#c3ad80';
  P.biomes[B.MOUNTAIN] = '#b7a88c';
  P.biomes[B.DESERT] = '#ddca97';
  P.biomes[B.SNOW] = '#e9e4d3';
  P.biomes[B.MARSH] = '#a6ad81';

  P.BIOME_NAMES = {
    [B.OCEAN]: 'open water', [B.SHALLOW]: 'shallows', [B.PLAINS]: 'plains',
    [B.FOREST]: 'forest', [B.HILLS]: 'hills', [B.MOUNTAIN]: 'mountains',
    [B.DESERT]: 'desert', [B.SNOW]: 'snowfields', [B.MARSH]: 'marshland',
  };

  // --- small color helpers ---
  function hexToRgb(h) {
    const n = parseInt(h.slice(1), 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  }
  function rgbStr(r, g, b, a) {
    return a === undefined ? `rgb(${r | 0},${g | 0},${b | 0})` : `rgba(${r | 0},${g | 0},${b | 0},${a})`;
  }
  function mix(h1, h2, t) {
    const a = hexToRgb(h1), b = hexToRgb(h2);
    return rgbStr(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t);
  }
  function shade(h, amt) { // amt -1..1 (darken..lighten)
    const [r, g, b] = hexToRgb(h);
    const t = amt < 0 ? 0 : 255, k = Math.abs(amt);
    return rgbStr(r + (t - r) * k, g + (t - g) * k, b + (t - b) * k);
  }
  function alpha(h, a) {
    const [r, g, b] = hexToRgb(h);
    return rgbStr(r, g, b, a);
  }

  P.hexToRgb = hexToRgb; P.mix = mix; P.shade = shade; P.alpha = alpha;
  P.SERIF = 'Georgia, "Iowan Old Style", "Palatino Linotype", "Book Antiqua", serif';

  S.palette = P;
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
