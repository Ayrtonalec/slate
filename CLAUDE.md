# CLAUDE.md — Project SLATE

This repo (a fork of the Slate docs tool, reused as a project home) hosts **SLATE**:
a god-simulation worldbuilding game. The Ruby/Middleman files in the root belong to
the original docs tool and are irrelevant — all project work lives in:

- `PROJECT-CONTEXT.md` — **read this first**: full session history, every design and
  technical decision with rationale, prototype internals, tuned constants, next steps.
- `god-sim-design/` — the design bible (9 docs). This is canon; don't contradict it
  without flagging the change to the founder.
- `god-sim-prototype/` — Phase 0 "The Living Atlas": a playable, deterministic,
  fully autonomous world sim rendered as an illustrated atlas in one HTML file.

## Working agreements

- The founder communicates in **Dutch** — reply in Dutch; keep code, comments, docs,
  and commit messages in English.
- The founder is the idea/vision person; Claude carries most implementation. Prefer
  showing something runnable over describing it ("assets meteen" — visible results
  from day one).
- **Sim determinism is law**: no `Math.random`/`Date.now` inside sim logic — use the
  labeled RNG streams (`S.rng.makeRng(seed, label)`); keep the fixed tick order
  (climate → settlements → colonization → gold → dragons → roads → claims).
  Same seed must replay the same history (tests enforce this).
- The sim core stays **engine-agnostic and headless-testable**. The JS prototype is
  the executable spec for a future C# core (decision gate: end of Phase 1).
- Production engine default: **Unity** (maximizes Claude leverage, one C# codebase);
  UE5 is the fallback if HDRP can't hit the visual bar. See design doc 06.

## Commands (run in `god-sim-prototype/`)

- `node test/headless.js` — full sim test suite (autonomy, determinism, divergence,
  power cascades). Must pass before committing sim changes.
- `node build.js` — rebuild `dist/slate-atlas.html` (single file, double-clickable)
  and `dist/slate-atlas-artifact.html` (same, without document wrapper tags).
- `node test/shot.js` — headless screenshots into `shots/` (playwright-core;
  Chromium path auto-detected; never run `playwright install`).

After sim changes: headless tests → rebuild → screenshot → eyeball before shipping.
