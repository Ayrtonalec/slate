# SLATE — Unity project (founder onboarding)

The real game lives here. `SlateWorld/` is the Unity project; the simulation
core is NOT in this folder — it lives in `../SlateSim/` as a local package, so
the sim stays engine-agnostic and headless-testable (the law from the design
bible).

## Opening the project

1. Open **Unity Hub** → **Add** → **Add project from disk** →
   pick `slate/unity/SlateWorld`.
2. Open it with Unity **6000.5.4f1**. First open takes a few minutes
   (package import).
3. Open the scene `Assets/Scenes/World.unity` (double-click it in the
   Project panel).
4. Press **Play** (▶ at the top). You are looking at a living world.

> The old `unity/My project` folder was a scratch project from Unity Hub
> (HDRP template — wrong pipeline for us). It is git-ignored; you can delete it.

## Controls in Play mode

| Input | Does |
|---|---|
| Scroll | Zoom (Atlas ↔ Settlement; dives toward your cursor) |
| WASD / arrows | Pan |
| Right-mouse drag | Grab the map |
| Q / E | Rotate |
| Space | Pause |
| 1 / 2 / 3 | Speed I / II / III (1 / 6 / 24 years per second) |
| N | New world (new seed) |

Zoom in on any town and you'll see villagers walking between the houses.
Roof colors = culture. Walls appear when a settlement becomes a town.
The feed top-right is the Chronicle, live.

## Where things live

- `../SlateSim/Runtime/` — **the simulation** (pure C#, no Unity). Rng, WorldGen,
  World, Sim, Chronicle, Powers. Ported 1:1 from the JS prototype, which remains
  the executable spec (`../god-sim-prototype/`).
- `../SlateSim/Tests/Editor/` — the headless test battery (5 seeds × 500 years,
  determinism replay, god-power cascades). Run in Unity:
  **Window → General → Test Runner → EditMode → Run All**. Must be green before
  committing sim changes.
- `Assets/Slate/Runtime/` — the presentation layer (Theater Principle: it only
  *performs* what the sim decides): terrain mesh, sea, forests, settlements,
  crowds, camera, HUD.
- `Assets/Slate/Shaders/` — hand-written URP shaders (terrain, water, zones).
- `Assets/Slate/Editor/ProjectSetup.cs` — one-shot scene/pipeline setup
  (menu: **Slate → Setup Project**), also runs headless for CI.

## Rules that keep us safe

- **Never** use `UnityEngine` inside `SlateSim` (the asmdef enforces it).
- **Never** use `Math.random`/wall-clock inside sim logic — labeled RNG streams
  only (`new Rng(seed, "label")`).
- Sim changes → run the EditMode tests before committing.
- Everything visual samples the ground through `TerrainSampler` so all systems
  agree where the terrain is.

## Headless commands (what Claude runs; you can too)

```
# compile + (re)build scene, no editor UI:
Unity.exe -batchmode -quit -projectPath unity/SlateWorld ^
  -executeMethod Slate.Game.Editor.ProjectSetup.BuildAll -logFile unity/setup.log

# run the sim test battery:
Unity.exe -batchmode -projectPath unity/SlateWorld -runTests ^
  -testPlatform EditMode -testResults unity/results.xml -logFile unity/tests.log
```
