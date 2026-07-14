# Feasibility, engine & art — the honest assessment

## Verdict up front

**The full vision as written — Manor Lords fidelity + orbit-to-ground zoom + living history over 4,000 years + modern-day presets + multiplayer + realistic art — is a AAA-studio, ~$20M+, multi-year project. Nobody should build that version first, including a AAA studio.**

**The correctly-sliced version is an ambitious-but-genuine indie project**, because of two structural tricks:

1. **The Theater Principle (doc 02):** we never simulate what we merely display. The expensive-looking part (ground-level life) is presentation over a cheap statistical sim. This converts the impossible part into a known-hard part.
2. **The ladder of shippable rungs (doc 07):** map-maker → living-map sim → faith systems → beauty pass → multiplayer → modern age. *Every rung is a real product someone would pay for* (Azgaar's popularity proves even rung 1 has an audience). We are never more than one rung from something releasable, which is the difference between ambitious and doomed.

Calibration from the real world: Manor Lords ≈ 7 years, one (exceptional) dev + outsourced art, for *one village* with no deep time. WorldBox took years of iteration at pixel fidelity. The Universim spent 6+ years and landed shallow. Respect the gravity: **prototype in months, vertical slice in ~a year, Early Access in 2–4 years** of consistent effort — faster only if scope is held brutally.

## Difficulty map

| Pillar | Difficulty | Notes |
|---|---|---|
| Macro civ/history sim | Medium | Well-trodden (DF, CK, WorldBox). Mostly careful data design + tuning for interesting-not-degenerate histories |
| Chronicle / event sourcing / exports | Medium | Discipline, not research. Biggest risk is UI/UX for browsing deep history |
| Interpretation Engine & faith economy | Medium-hard | Novel design (our moat), but it's classification + bookkeeping, not exotic tech |
| Band-based zoom (Atlas↔Ground) | Hard but bounded | 4 crafted bands + transitions (doc 04), NOT one seamless continuous zoom — that reframing is what makes it shippable |
| Theater layer (crowds, settlements) | Hard | Procedural settlement dressing + crowd LODs; known techniques, lots of craft |
| "Realistic" art bar | **Highest risk** | See art pipeline below — solvable with the right constraints |
| Multiplayer | Hard, deferred | Architecture paid for up front (determinism), implementation deferred to post-fun |
| Modern-day preset | Out of v1 | Expansion-scale asset universe (doc 04's tech dial contains it) |

## Engine

**Recommendation: engine-agnostic simulation core + Unity presentation layer.** *(Updated 2026-07-14: the founder clarified that maximizing AI-copilot leverage is a primary criterion, not a tiebreaker — that flips the earlier UE5 lean to Unity as the production default, with UE5 as the fallback if the visual bar demonstrably can't be hit in HDRP.)*

**The non-negotiable part is the first half.** The macro sim (the actual game) should be a headless, deterministic, engine-free library (C# or C++/Rust) with its own test suite that can integrate 4,000-year histories in CI. The engine renders and edits; it never owns truth. This de-risks the engine choice itself (a pivot stays affordable), enables the chronicle/multiplayer/scrubbing features, and means the sim can be built and *proven fun* before a single expensive asset exists.

| | **UE5** | **Unity 6** | **Godot 4** |
|---|---|---|---|
| Hitting "Manor Lords look" | **Best** — Lumen/Nanite/World Partition; Manor Lords is literally Unreal; MetaHuman for faces; Fab/Megascans scan library (licensing shifted post-2024 — verify current terms) | Achievable (HDRP) but you work harder for it | Not at this bar; fine for prototypes |
| Fit for our sim | Sim lives in our own C++ module — fine. Avoid gameplay-in-Blueprints sprawl | **Best language story:** one C# codebase shared with the sim core; DOTS for crowds | C# or GDExtension; crowds need hand-rolling |
| Iteration speed | Slowest (C++ compile, editor weight) | Good | **Fastest** |
| Crowd/agent rendering | Mass Entity + Nanite instancing | DOTS — proven at scale (Cities: Skylines 2's *sim* scaled; its launch woes were mostly rendering/LOD choices) | MultiMesh, more manual |
| Netcode for our shape | Battle-tested replication (though our command-stream model is mostly engine-independent anyway) | NGO/Fishnet/Photon — fine | Adequate |
| Asset ecosystem for realism | **Decisive advantage** (Fab/Megascans, MetaHuman) | Big store, less scan-library gravity | Thinnest |
| AI-copilot leverage (honest note) | I'm strongest in plain code — the C++ sim/module work suits me; editor-GUI-heavy and Blueprint-heavy workflows dilute my usefulness (UE's Python editor scripting recovers some of it) | Strong — C# end-to-end, and scenes/prefabs are text (YAML), so I can operate on more of the project directly | Strong — everything is text; best prototype partner |
| Cost | 5% royalty > $1M rev | Per-seat (Runtime-fee fiasco was walked back, but it happened — mild trust tax) | Free/MIT |

**Why Unity gets the nod for production:** one C# codebase from sim core to UI means the AI copilot can write the large majority of the project end-to-end — sim, gameplay, DOTS crowd rendering, shaders, and crucially *editor tooling and scene/prefab generation scripts*, so "clicking in the editor" is largely replaced by "running a tool the copilot wrote." A shipped game at ~85% of the visual ceiling beats an unshipped one at 100%, and our distance-first art direction (lighting and atmosphere carry the look; close-up faces are rare by design) shrinks UE5's fidelity advantage exactly where it's biggest.

**When you'd pick UE5 instead:** if at vertical-slice time the Settlement-zoom beauty shot demonstrably can't hit the bar in HDRP, *and* the founder is willing to personally drive UE's editor-centric workflows (Blueprints, binary .uassets, MetaHuman) where copilot coverage drops substantially — much of a UE project lives in binary assets an AI can't read or write, so the human's share of manual work grows. The engine-free sim core keeps that pivot measured in weeks, not a rewrite. Decide at the end of Phase 1 with the prototype in hand.

**Prototype phase needs no engine decision at all:** headless C# sim + a cheap visualization (Godot, or even a web/three.js map viewer) proves the fun. **Do not spend art or engine effort before the sim is fun to watch.**

Custom engine: no. Life is short.

## Art pipeline — can Claude + Blender MCP cover "realistic"? (direct answer)

**Partially — and the gap is predictable, so we design around it.** Honest split:

**What I can genuinely produce/drive (high confidence):**
- All procedural systems: terrain/biomes/hydrology, settlement & road layout generators, scattering, the Atlas map renderer (the illustrated living map is *code*, not art — and I can make it gorgeous).
- Blender via scripting/geometry nodes (MCP or plain Python): parametric medieval architecture kits (walls/roofs/timbering with regional + faith-driven variation), props, terrain assets, LOD/export pipelines. Kit-bashed architecture is a scripting problem, and it's most of what's on screen at Settlement zoom.
- Shaders, post-processing, palettes (the faith-tone system), VFX for miracles, UI, icons, heraldry generators.
- Materials from PBR/scan libraries (Fab/Megascans, ambientCG CC0, etc.) — selection and assembly, not authorship.

**What I can't do to a "realistic" bar (plan for money or scope here):**
- Hero organics: humans, animals, dragons — sculpted, rigged, *animated* to realistic quality. This is skilled-human craft; AI 3D generators (Meshy/Tripo/TRELLIS-class, as of 2026) yield usable background props after cleanup, not realistic rigged characters.
- Mitigations, in order: (1) **art direction that keeps organics small on screen** — at Settlement zoom a villager is 40px of silhouette + animation, and Ground zoom is deliberately vignette-framed (doc 04); (2) purchased character/animation packs + retargeting (Mixamo-class); (3) UE's MetaHuman for the rare close-up face; (4) *one* contracted creature artist for the dragon — the single asset that must be spectacular.

**Set the expectation now:** the achievable look solo-with-AI is **"grounded painterly-realism at distance"** — Total-War-from-altitude, sold by lighting, atmosphere, and coherent art direction (which is ~70% of what makes Manor Lords screenshots read as real). It is *not* AAA close-up character fidelity. Lighting is cheap and sells realism; character faces are expensive and sell it only in close-up — so the camera design (doc 04) is quietly also the art budget's bodyguard.

## Risk register

| Risk | Mitigation |
|---|---|
| Wide-but-shallow (the Universim trap) | Pillars + phase gates; faith/chronicle depth before power breadth; the Worldbuilder persona is the tiebreaker |
| Sim degenerates over 4,000 years (everyone dies / one blob wins / NaN weather) | Headless CI runs full histories from day 1; tuning is a permanent workstream, treat it like tests |
| Seamless-zoom rabbit hole | Banded zoom is the spec (doc 04); "seamless" is a polish stretch, never a blocker |
| Art bar mismatch | Distance-first art direction; expectation set in writing (above); budget line for 1–2 contracted hero assets |
| Multiplayer too early | Ladder (doc 05); determinism paid up front, netcode deferred until solo is fun |
| Modern-day scope bomb | Contained in the tech-dial expansion (doc 04); zero v1 art dependency |
| Solo-dev burnout | Every phase ends in something playable/shareable; chill covenant applies to development too |
