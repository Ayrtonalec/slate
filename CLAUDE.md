# CLAUDE.md — Project SLATE

This repo (a fork of the Slate docs tool, reused as a project home) hosts **SLATE**:
a god-simulation worldbuilding game. The Ruby/Middleman files in the root belong to
the original docs tool and are irrelevant — all project work lives in:

- `PROJECT-CONTEXT.md` — **read this first**: full session history, every design and
  technical decision with rationale, prototype internals, tuned constants, next steps.
- `TODO.md` — the active work queue (engine decision, C# core port, living-world v1,
  faith v0.1, dual-dev setup). Pick work from here top-to-bottom unless told otherwise.
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
- **Sim determinism is law**: no `Math.random`/`Date.now`/wall-clock inside sim
  logic — use the labeled RNG streams (JS: `S.rng.makeRng(seed, label)`; C#:
  `new Rng(seed, "label")`); keep the fixed tick order (climate → settlements →
  colonization → gold → dragons → **wars** → **disasters** → **expeditions** →
  roads → claims; wars/disasters/expeditions exist in the C# core only).
  Same seed must replay the same history (tests enforce this).
- The sim core stays **engine-agnostic and headless-testable**. **The C# core
  (`SlateSim/`, a Unity local package with `noEngineReferences`) is now the
  leading core** (2026-07-15); the JS prototype is frozen as the Phase 0
  reference spec and cheap design playground.
- Production engine: **Unity** (T1 decision settled 2026-07-15, see design doc 06
  "T1 engine validation"); URP, not HDRP (HDRP is in maintenance mode). UE5 is
  the fallback only if URP misses the visual bar at the Phase 1 gate.

## Commands — C# core + Unity game (the live codebase)

- Sim tests (must be green before committing sim changes; needs the Unity editor
  closed, or run against a throwaway copy of the project):
  `Unity.exe -batchmode -projectPath unity/SlateWorld -runTests -testPlatform
  EditMode -testResults out.xml -logFile tests.log`
- Scene/pipeline setup (idempotent, headless):
  `Unity.exe -batchmode -quit -projectPath unity/SlateWorld -executeMethod
  Slate.Game.Editor.ProjectSetup.BuildAll -logFile setup.log`
- Screenshots (needs rendering, so no `-batchmode`):
  `Unity.exe -projectPath unity/SlateWorld -executeMethod
  Slate.Game.Editor.ScreenshotRunner.Run -logFile shots.log` → `unity/shots/`
- Unity path: `C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe`.
  If the founder has the project open in the editor, batch runs can't get the
  lock — clone Assets/Packages/ProjectSettings to a scratch project instead.

## Commands — JS prototype (frozen reference, run in `god-sim-prototype/`)

- `node test/headless.js` — Phase 0 sim suite (no wars; the C# core has moved on).
- `node build.js` — rebuild the dist HTML atlas.
- `node test/shot.js` — headless screenshots (playwright-core; never `playwright install`).

After sim changes: tests → build → screenshot → eyeball before shipping.
