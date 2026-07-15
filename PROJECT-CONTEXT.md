# PROJECT-CONTEXT.md — everything that matters about SLATE

*Handoff document written 2026-07-15 at the end of the founding session (Claude Code
on the web, branch `claude/god-simulator-game-idea-j36tmt` on `Ayrtonalec/slate`).
Purpose: let the founder — or any future Claude session, local or remote — pick this
project up with zero missing context. If something here conflicts with the design
bible in `god-sim-design/`, the bible wins; this file adds the story and the why.*

---

## 1. What SLATE is

A god-simulation worldbuilding sandbox. Manor Lords-style presentation ambitions,
zoom from a living illustrated world map down toward ground level. You are a god
from Year 0. **No opponent, no win state** — the game is watching a fully
autonomous world live, intervening however you like, and reading the history you
caused. Faith is the core system (not a simple mana bar), the multi-thousand-year
chronicle is the reward, and the export of that chronicle as a D&D campaign
setting is the killer feature for the primary audience.

Elevator pitch used throughout: *WorldBox's sandbox × Dwarf Fortress's chronicle ×
Black & White's being-worshipped fantasy × Manor Lords' presentation.*

- Codename: **SLATE** (repo name coincidence, kept — "blank slate").
- Title candidates parked: Fiat Lux, Demiurge, Providence, Year Zero, Aeons,
  The Shaping, Godscape. No trademark checks done.
- Positioning: premium indie ($25–35), no F2P, expansion-friendly, mod-friendly,
  chill ("Bob Ross of god games"). Twitch chat-as-citizens planned for streamability.

## 2. The founder & working style (important for any assistant)

- Communicates in **Dutch**; wants replies in Dutch. Code/docs/commits in English.
- Vision/ideas person; expects Claude to carry the large majority of implementation.
  Asked explicitly: "kan jij mij daar enorm veel mee helpen?" → yes, with the honest
  split documented in design doc 06 (sim/code ~90%, procedural art yes, realistic
  rigged organics no — buy packs / MetaHuman / one contracted dragon artist).
- Wants visible results immediately: greenlit Phase 0 with "doe wel meteen assets
  enzo dan anders is het meteen nutteloos" → hence the prototype shipped with the
  full illustrated-atlas art layer, not programmer graphics.
- Values honesty about feasibility over hype. Timeline expectations set: prototype
  weeks, vertical slice ~a year, Early Access 2–4 years of consistent effort.
- Target audience (founder's emphasis): nerdy D&D-loving map-building
  creative-writing people first; co-op multiplayer and streamability second;
  "chill game" always.

## 3. Session history — what happened, in order

1. **Braindump captured** (verbatim in `god-sim-design/00-original-notes.md`).
2. **Design bible v0 written** — 9 docs in `god-sim-design/` (see §4).
3. **Founder clarification #1:** the world must be 100% alive on its own AND every
   run must be able to diverge into an unrecognizably different history. Both were
   locked in as *hard guarantees* with an engineering plan and CI-style divergence
   metrics (doc 02, "Autonomy & divergence"). Corollary added: a god with no deeds
   has no religion — mortals invent their own gods until you act (doc 03).
4. **Founder clarification #2:** engine choice must also maximize how much Claude
   can do. This **flipped the production-engine default from UE5 to Unity**
   (one C# codebase end-to-end, text-based/scriptable project, editor tooling can
   be code-generated). UE5 remains the fallback if Unity/HDRP can't hit the visual
   bar at the Phase 1 gate. Prototype phase needs no engine at all. (Doc 06.)
5. **Phase 0 greenlit and built** ("The Living Atlas", `god-sim-prototype/`) —
   playable, tested, pushed; published as a Claude artifact:
   https://claude.ai/code/artifact/624d3e05-417a-44b5-a743-5a11e9fa55d0
6. **Founder is moving to local development** on their PC (Claude Code desktop/CLI,
   cloned repo). This document is the handoff.
7. **Founder clarification #3 (2026-07-15): the aliveness contract.** Wars, clans/
   factions, trade, and farming are **part of the "world lives on its own"
   guarantee**, not optional later-phase flavor. Locked into design doc 02
   ("aliveness contract") and Phase 1 scope in doc 07. In the same breath the
   founder greenlit **starting the real game** — the active work queue is now
   **`TODO.md`** (repo root): engine decision validated against performance /
   our-game fit / dual-coding / multiplayer criteria, C# sim-core port,
   living-world v1, faith v0.1, dual-dev setup.
8. **The founder codes too.** "Dual code van Claude en ik" — the founder wants to
   actively co-develop, not only direct. Weigh tech and project-shape choices for
   a newer programmer (readable C#, visual editor, great tutorials) AND for AI
   leverage (text-serialized assets, code-generated content). Explain code when
   walking the founder through it.

## 4. The design bible — one-line map of each doc

All in `god-sim-design/`:

- **README.md** — pitch, four pillars, doc index, status log, title candidates.
- **00-original-notes.md** — founder braindump, source of truth for intent.
- **01-vision-and-audience.md** — market gap table (WorldBox/DF/B&W/Manor
  Lords/Azgaar/Universim), personas (Worldbuilder = primary and design tiebreaker,
  Gazer, Co-op group, Streamer), campaign-setting-forge positioning, business notes.
- **02-simulation-architecture.md** — **the Theater Principle** (macro sim is truth;
  ground level is theater performing it — the single most important architectural
  decision); three layers (Chronicle/Province/Theater); time model (tick = week at
  macro in the full design; the prototype uses months); **event sourcing** as the
  one architecture behind saves/scrubbing/multiplayer/chronicle/mods; autonomy &
  divergence guarantees; systems inventory; the worked "gold cascade" example;
  performance envelope; determinism discipline from day 1.
- **03-faith-religion-morality.md** — three faith currencies (**Awe** spiky/decaying,
  **Devotion** slow/compounding, **Dread** cheap/corrosive) + **Doubt** as
  anti-currency; **the Interpretation Engine** (you never pick an alignment — each
  culture classifies you from local evidence and builds its religion around its
  conclusion; different cultures can worship the same god as different gods);
  prayer-answering as the ground-zoom moment-to-moment loop; three rival-religion
  threats (schisms of you / foreign gods / late-game secularism with the
  "become myth" retirement ending); mundanification & wonder upkeep; regional
  (never global) tone shifts of the world by faith mix; parked: player-written
  scripture, afterlife layer, AI pantheon.
- **04-god-powers-and-world.md** — blank-canvas map painting + presets; seven power
  verbs (**Shape, Seed, Touch, Sign, Wrath, Wonder, Avatar**), all emitting
  chronicle events + interpretation tags; powers have consequences, not cooldowns;
  **the tech-pace dial** (progress speed is a divine lever — resolves
  fantasy-medieval-forever vs 2026-dragon-in-NYC; Modern Age = expansion, not v1);
  four zoom bands (**Atlas** = living illustrated map that draws itself,
  **Province**, **Settlement**, **Ground**) mapped to sim layers, crafted
  transitions instead of one lying seamless zoom; the dynamic-world checklist.
- **05-multiplayer-and-streamability.md** — multiplayer ladder: share codes →
  async "archaeology mode" (visit a friend's dead world, leave one artifact) →
  2–4 god pantheon co-op (first contact between differently-shaped cultures is the
  drama engine; co-authored miracles) → soft-rival pantheons (faith share, mortals
  war, gods never directly fight) → stretch: DM mode (friends as mortal Heroes).
  Sparse command inputs + deterministic event-sourced sim = cheap netcode shape,
  but multiplayer ships only after solo is fun. Twitch: chat-as-citizens, miracle
  votes, session chronicle auto-summary. The chill covenant: no fail states, pause
  sacred, ≤4 players, no god-vs-god damage.
- **06-feasibility-engine-art.md** — verdict: full vision = AAA-decade; correctly
  sliced = ambitious indie, doable because of the Theater Principle + every roadmap
  rung being shippable. Difficulty map. Engine matrix and the **Unity-default
  decision** (see §3.4 above) with the honest AI-leverage table. Art pipeline
  reality: Claude + Blender scripting covers procedural/environment/architecture/
  shaders/UI; NOT realistic rigged organics — mitigations: distance-first art
  direction (a villager is 40px of silhouette; lighting sells realism), purchased
  character/animation packs, MetaHuman for rare close-ups, one contracted hero
  asset (the dragon). Achievable look solo+AI: "grounded painterly realism at
  distance." Risk register (Universim wide-but-shallow trap, sim degeneration,
  seamless-zoom rabbit hole, multiplayer-too-early, modern-age scope bomb, burnout).
- **07-roadmap.md** — Phase 0 Proof of Life ✓ → 1 vertical slice "The First
  Miracle" (5 powers, faith v1, Figures v1, Atlas↔Settlement zoom, placeholder 3D;
  **engine decision at the end of this phase**) → 2 "Fear & Faith" (the soul of the
  game, protected from cuts) → 3 "The Chronicle" (history UI + campaign-setting
  export; first community test) → 4 "Beautiful World" (the art push) → 5 Early
  Access → 6 Pantheon multiplayer + Twitch → 7 Modern Age expansion. Rule: every
  phase ends with something playable/shareable.

## 5. Phase 0 prototype — what exists and how it works

**`god-sim-prototype/` — "The Living Atlas."** Zero-install browser app; the whole
thing (sim + art + UI) is plain JS under a `SLATE` global namespace so it runs from
`file://`, inlines into one HTML file, and executes headless in Node for tests.
`dist/slate-atlas.html` is the double-clickable build; `node build.js` rebuilds it.

**Deliberate stack choice:** JS instead of the design bible's C# so the founder
could play it instantly in a browser/artifact with zero installs. The JS sim is
the *executable spec*; port to C# at the Phase 1 gate (structures, tick order,
constants, and tests all translate mechanically).

### Sim (all deterministic; tick = 1 month)

- **World:** 192×120 cells (cell = 8 world units). Warped fBm + ridge noise
  heightfield; sea level binary-searched to a fixed **36% land fraction** so every
  seed is usable. Latitude+altitude temperature, coast-biased moisture, 9 biomes,
  rivers descending from wet springs (merge/flag flow), fertility (biome + river
  + jitter), fishing grounds on coasts, ~10 hidden natural gold veins, named
  regions via flood fill (ranges ≥14 cells, forests ≥40, deserts ≥50, seas ≥160,
  one ocean) with an adjective+ending namer.
- **Cultures:** 4 (Aldish/anglo, Vasker/norse, Serai/desert, Tessian/latin), each
  with phoneme banks for places, gold-places, people. Homelands = 4 fertile
  coastal sites chosen greedy-max-min-distance. 2 hamlets land per culture, Year 0.
- **Settlements:** logistic growth toward a food cap:
  `cap = fertAround(r3)×26 (+170 fish) × (1 + 0.7×max(0, prosperity−1)) (×0.85 lean years)`.
  `prosperity = 1 + 0.15×roadDegree + 2.5×gold + 0.3×fish + 0.15×tier + 1×boom`.
  The 0.7 trade multiplier is what lets **cities** exist at all (fields alone cap
  ~1700; city threshold 3200) — cities are rare (0–2/world) and gold/trade-driven,
  which is intended. Tiers: hamlet <450 < village <1300 < town <3200 ≤ city; tier
  events fire only on first reach (`maxTier`), decline is silent until ruin.
- **Colonization:** fission when pop >0.7×cap and >150, chance 0.005/mo, damped by
  regional crowding (count within r8; skip chance `(crowd−2)/10`), min site score
  8.5, min spacing 4 cells → wilderness stays wild, waves look organic.
- **Famine:** only when pop >1.2×cap for ≥15 months, ≥15y cooldown per settlement,
  streak resets after firing (was famine-spam before this fix). Lean years:
  world-scale RNG, cap ×0.85.
- **Gold:** vein near a settlement → discovery chance → boom (40y growth bonus,
  migrant pull); vein in the wilds → mining camp founded (via `campSiteNear`,
  which tolerates rough terrain); god-seeded veins are pre-revealed → the full
  founder cascade (camp → boomtown → wealth → dragon) works from one click.
- **Dragons:** spawn when a settlement's wealth >70 with mountains within r6,
  year ≥60, max 2 alive, **25-year world cooldown after any dragon dies** (the
  merry-go-round fix); raid the richest target in r10 every 60–140 months; terror
  radius r8 suppresses growth; razing possible under pop 400 (ruin gets an alt
  name: Dragonfall/Wyrmscar/…); slain 10%/yr when a town+ is within r10 (named
  hero, hoard windfall), depart if nothing worth coveting. Wealth accrual:
  `prosp×pop/200000` per month.
- **Chronicle:** append-only event log; ~20 templated prose event types with
  importance 1–3 and tone plain/god/doom; Markdown export grouped by century.
- **Determinism:** labeled RNG streams (`mulberry32(hash(seed+'/'+label))`), fixed
  tick order climate → settlements → colonization → gold → dragons → roads →
  claims; occupancy grid (`settleGrid`) for all small-radius settlement queries
  (this took 500y from 22s → ~0.9s).

### Renderer ("the assets" — all procedural ink-and-paper vector art)

- Committed single visual world: parchment `#e8dcc0`, walnut ink `#3a2f1e`,
  celadon sea `#aabfb2`, gold `#b08a2e` (divine), blood `#8a3b2a` (doom); serif
  stack (Georgia/Iowan/Palatino).
- Terrain baked once to a 2× supersampled offscreen plate: watercolor biome blobs
  (fertility tints the wash), sea-depth shading (painted at cell resolution then
  smoothed by upscale — the grid-artifact fix), engraved wave hatching, rivers,
  **coastlines traced from boundary edges and Chaikin-smoothed ×2** — NOTE: at
  vertices where land cells touch diagonally there are two outgoing edges; resolve
  by max cross product (sharpest clockwise turn) or you get the diagonal-slash
  artifact this session already fixed once. Mountain hachures with snowcaps,
  conifer/deciduous stipples, marsh dashes, paper blotches/fibers/vignette.
  Divine weather (storm gray / bless gold washes) composites over the static base.
- Per frame: culture tint + dashed borders (from the claims grid), dashed roads,
  settlement glyphs that grow hamlet→hut, village→huts, town→walls+tower,
  city→double walls+culture banner; ruins, gold veins (divine ones get a cross),
  dragon lairs (smoke curl + red eye), dead lairs faded.
- Labels with greedy collision layout, priority: towns/cities → regions
  (letterspaced smallcaps; water names italic) → villages/hamlets (zoom-gated:
  2.2/1.1/0.5/0) → ruins ("ruins of X", italic).
- Effects layer: rotating storm spirals + rain flecks, blessing motes, gold
  glints, pulsing dragon menace rings, event pings, animated migration/raid
  arrows. Respects `prefers-reduced-motion`.
- Atlas furniture: double-rule plate frame, 8-point compass rose, league scale bar.
- UI: cartouche title bar, Year/season display, pause + speeds I/II/III (1/6/24
  y/s), New World, wax-seal power rail (Inspect/Seed Gold/Curse Skies/Bless Land,
  hotkeys i/g/c/b, space = pause), ledger-style chronicle panel (filter All/
  Notable/Major, click-to-fly, Markdown download), legend with live-drawn glyphs,
  tooltips (settlement/ruin/dragon/wilderness), toast for rejected powers, intro
  card ("Only watch" observer mode vs "Begin").
- URL params: `?seed=N&run=YEARS&speed=N&nointro`; `window.__slateReady` flags
  readiness for automation.

### Tests & tooling

- `node test/headless.js` — 5 seeds × 500 years (~0.9 s each): sanity ranges,
  no-NaN, ≥2 cultures survive, chronicle alive, cross-seed divergence spread,
  determinism replay (seed 7 twice → identical chronicle), gold cascade
  (seed→camp/strike→inhabited), curse cascade (thriving town → collapse), bless
  cascade (empty livable plains → settled), Markdown export. **ALL PASS** as of
  handoff.
- `node build.js` — emits `dist/slate-atlas.html` (full document) and
  `dist/slate-atlas-artifact.html` (same minus doctype/html/head/body wrappers,
  for artifact hosts that add their own).
- `node test/shot.js` — playwright-core (npm i --no-save playwright-core) +
  preinstalled Chromium (path auto-detected under /opt/pw-browsers; never
  `playwright install`): overview / city zoom / seeded-gold-after-80y / cursed
  town screenshots into `shots/` (gitignored; curated copies in `media/`).

### Deliberate Phase 0 cuts (do not mistake for oversights)

Faith/prayer, Figures (named people with relationships — the wyrmslayers are
name-drops, not entities), wars, myth-vs-fact drift, time scrubbing, save/load,
sound, 3D, multiplayer. All designed (docs 02–05) and staged (doc 07).

## 6. Where everything lives

- Repo: `Ayrtonalec/slate`, branch **`claude/god-simulator-game-idea-j36tmt`**
  (all work committed and pushed there; master untouched).
- Design bible: `god-sim-design/` · Prototype: `god-sim-prototype/` ·
  Playable single file: `god-sim-prototype/dist/slate-atlas.html`.
- Published artifact (private to founder's account):
  https://claude.ai/code/artifact/624d3e05-417a-44b5-a743-5a11e9fa55d0
- Auto-loaded assistant context for local sessions: `CLAUDE.md` (repo root).

## 7. Glossary (project language — use it consistently)

**Theater Principle** (macro sim is truth, visible world performs it) ·
**Chronicle** (event-sourced history = save = lore = netcode backbone) ·
**Interpretation Engine** (mortals classify the god from evidence) ·
**Awe / Devotion / Dread / Doubt** (faith currencies / anti-currency) ·
**Zoom bands: Atlas / Province / Settlement / Ground** ·
**Power verbs: Shape / Seed / Touch / Sign / Wrath / Wonder / Avatar** ·
**Tech-pace dial** (progress speed as divine lever; Modern Age = expansion) ·
**Observer Mode** (zero-intervention run; also the test harness) ·
**Chill covenant** (no fail states, pause sacred, no god-vs-god damage) ·
**Campaign-setting forge** (chronicle/map export for D&D use).

## 8. State of play & next steps

**Now:** the real game has been greenlit (2026-07-15). **The active work queue is
`TODO.md` in the repo root** — engine decision (T1, validated against performance /
our-game fit / dual-coding / multiplayer criteria), C# sim-core port (T2),
living-world v1 per the aliveness contract (T3: wars, clans/factions, trade,
farming, culture drift), faith v0.1 (T4), dual-dev setup (T5). Suggested order:
T2 → T1 → T5 → T3/T4. The Phase 0 prototype stays alive as the cheap design
playground (T6).

**T2/T5 update (2026-07-15, same local session, after the founder installed
Unity 6000.5.4f1):** the C# port and the first playable Unity build both landed.
`SlateSim/` (repo root) is the engine-agnostic sim core as a Unity local package
(`com.slate.sim`, `noEngineReferences: true` so the compiler enforces the law);
all Phase 0 systems ported 1:1 with JS quirks preserved (stable sorts, JS
`Math.round`, float32 heightmap); NUnit battery 6/6 green in ~4s. `unity/SlateWorld`
is the Unity 6 URP project: procedural terrain mesh (vertex-color painterly wash,
snowcaps, river stains, roads painted as history happens), depth-tinted sea with
shore foam, instanced forests, settlements as procedural architecture (roof color
= culture; walls/towers at town+), villagers walking settlements at close zoom
(pure theater), zone effects (storms/blessings/dragon menace/event pings), one
smooth Atlas↔Settlement zoom camera, live chronicle feed HUD. Everything builds
headless (`ProjectSetup.BuildAll`) and screenshots via `ScreenshotRunner.Run`.
Founder onboarding: `unity/README.md`. The founder's Unity Hub scratch project
(`unity/My project`, HDRP template) is git-ignored — superseded by SlateWorld.

**Living-world feedback loop (2026-07-15, founder playtesting live):** the founder
played the first Unity build and drove three rounds of fixes, all shipped same-day:
(1) **wars v1** in the C# core (tension → visible marching warband → conquest with
culture-flipped roofs / sack / held walls; own "war" rng stream; tick order now
… dragons → wars → roads …) plus visible farmland with seasonal crop colors,
farmer/hunter villager roles, and road caravans; (2) **Manor Lords pacing** —
speed I = 1 month ≈ 12 real seconds (armies get per-frame visual interpolation so
they march instead of teleport), II = 1 y/s, III = 12 y/s; (3) **settlement
heterogeneity** — SoilLuck at founding, market pull (hamlet belts around district
centers, Zipf-ish size spread), smaller daughter colonies, and walls as a paid
choice driven by threat + wealth (palisade → stone; open towns exist and fall
easier in war; renderer draws palisades vs stone and gives every settlement its
own footprint axis/density/roof shade). Battery grew to 9 tests. The founder's
"chaotic warlord burns it down" idea is parked in TODO T3 for Figures v1.

**T1 update (2026-07-15, first local session):** the founder re-weighted the
engine criteria (multiplayer, asset creation with Claude Code, Claude Code
compatibility, performance, and *no revenue-percentage royalties*) and asked for
the validation to run. Done with July-2026 sources — **Unity confirmed** as the
decision; evidence table in design doc 06 ("T1 engine validation"). Key facts
found: Unity moved **HDRP to maintenance mode** (URP is the visual path now, with
realtime GI/physical sky/SSR landing through 6.5–6.7); Unity ships an **official
MCP integration** supporting Claude Code while UE 5.8's MCP is experimental and
still fights binary assets; UE royalty is 5% of lifetime gross > $1M (3.5% only
with Epic-first launch) vs Unity's fixed $2,310/seat/yr above $200k revenue —
never a percentage. The T1.2 hands-on spike (after the T2 C# port) remains the
only thing that can reopen the decision.

**Standing engineering rules:** determinism is law (labeled RNG streams, fixed
tick order, no wall-clock/Math.random in sim); headless tests must pass before
any commit that touches the sim; every new event type gets chronicle prose + an
importance + a tone; balance changes get verified against the 5-seed battery
(watch for: settlement counts 100ish–450, cities stay rare, dragons are sagas
not spam, famine is occasional weather not white noise).

**Parked questions:** player-written scripture; afterlife layer; AI rival
pantheon personalities; final title + trademark check; monetization details;
whether prototype-JS survives into the tooling layer (map viewer) after the C#
port.
