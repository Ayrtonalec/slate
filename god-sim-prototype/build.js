/* SLATE Phase 0 — build: inline every script and stylesheet referenced by
   index.html into one self-contained HTML file (works from file://, and
   publishable anywhere). Usage: node build.js */
'use strict';
const fs = require('fs');
const path = require('path');

const root = __dirname;
let html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');

html = html.replace(/<link rel="stylesheet" href="([^"]+)">/g, (m, href) => {
  const css = fs.readFileSync(path.join(root, href), 'utf8');
  return '<style>\n' + css + '\n</style>';
});

html = html.replace(/<script src="([^"]+)"><\/script>/g, (m, src) => {
  const js = fs.readFileSync(path.join(root, src), 'utf8');
  return '<script>\n' + js + '\n</script>';
});

fs.mkdirSync(path.join(root, 'dist'), { recursive: true });
const out = path.join(root, 'dist', 'slate-atlas.html');
fs.writeFileSync(out, html);
console.log('built', out, Math.round(html.length / 1024) + ' KiB');

// Artifact variant: same content without the document wrapper tags
// (publishing hosts add their own doctype/head/body skeleton).
const inner = html
  .replace(/<!DOCTYPE html>\s*/i, '')
  .replace(/<\/?html[^>]*>\s*/gi, '')
  .replace(/<\/?head>\s*/gi, '')
  .replace(/<\/?body>\s*/gi, '');
const outA = path.join(root, 'dist', 'slate-atlas-artifact.html');
fs.writeFileSync(outA, inner);
console.log('built', outA, Math.round(inner.length / 1024) + ' KiB');
