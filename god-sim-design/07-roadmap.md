# Roadmap

Rule: **every phase ends with something you can play, watch, or share.** No phase bets on a later phase to become worthwhile.

## Phase 0 — "Proof of Life" (the fun test)

*Target: weeks-to-a-couple-months of part-time work, with AI pairing.*

- Headless, deterministic sim core (C#): terrain/biome grid, settlement founding & growth, statistical pops, site scoring, basic events → chronicle log.
- Cheap visualization — a 2D living-map view (Godot or web/three.js): watch settlements sprout, roads connect, borders creep across 500 years in minutes.
- Two god inputs only: **Seed gold** and **Curse weather** — the two founder-note examples — cascading through site scores and migration.
- **Success criterion (the only one):** you watch 500 years happen and *want to keep watching*. If poking the world isn't compelling as a text-log-plus-map, no amount of UE5 will save it. If it is — everything after this is amplification.

## Phase 1 — Vertical slice: "The First Miracle"

- One 8×8 km fantasy region, ~Bronze-to-Iron span, a few centuries.
- **Living-world v1 (the aliveness contract, doc 02):** culture value drift, wars &
  raids between cultures, clans/factions/dynasties via Figures, visible trade flows
  (caravans/ships), farming/production visible at close zoom, migration. Founder
  decision 2026-07-15: these are part of "the world lives on its own," not later flavor.
- Powers: one from each verb family (Shape, Seed, Touch, Sign, Wrath) — five total, deep cascades > wide kit.
- Faith v1: Awe/Devotion/Dread currencies, prayer inbox, first Interpretation Engine pass (they name you, build the first shrine, hold the first festival).
- Figures v1: notability promotion, deeds, epithets. Chronicle browsable in-game.
- Zoom: Atlas ↔ Province ↔ Settlement (Ground/Avatar deferred; it's polish, not proof).
- Placeholder-tier 3D art on purpose.
- **Gate at end of phase:** engine decision (UE5 vs Unity) made *now*, with a real sim in hand — not before.

## Phase 2 — "Fear & Faith"

Full doc-03 systems: Doubt, schisms, the faceless rival god, mundanification/wonder upkeep, regional tone shifts (the world visibly wearing your reputation). This phase is the game's soul — protected from cuts.

## Phase 3 — "The Chronicle"

History UI done right: timeline scrubbing, dynasty/schism trees, myth-vs-fact views, and the **campaign-setting export** (maps + gazetteer + figures + timelines → markdown/PDF/web). First closed community test aimed at worldbuilder/D&D communities — they'll tell us if the killer feature kills.

## Phase 4 — "The Beautiful World"

The chosen engine earns its keep: Settlement-zoom beauty pass, architecture kits (faith-variant trims), crowd theater, Atlas map art, miracle VFX, Ground zoom + Avatar v1. Trailer built from this phase.

## Phase 5 — Early Access

One big continent, Year 0 → medieval cap, full solo loop. Share codes + chronicle export at launch (community content from day 1). Chill positioning in all materials. EA because this genre is tuned in public — with a roadmap that promises the two headliners by name (below).

## Phase 6 — Pantheon multiplayer

The doc-05 ladder: archaeology mode first (cheap, on-brand), then 2–4 god co-op/rivalry. Twitch integration (chat-as-citizens) lands here too — it's multiplayer with extra steps.

## Phase 7 — "The Modern Age" (expansion)

Tech dial uncapped, industrial → 2026 presets, cities, media-era rumor dynamics. The dragon finally gets its Manhattan. Priced as the expansion it is.

---

## Immediate next actions

1. ~~Capture the braindump and design bible~~ ✓
2. ~~Greenlight Phase 0~~ ✓ (founder: "doe die fase 0, maar doe wel meteen assets" — hence the illustrated-atlas presentation from day one)
3. ~~Build Phase 0~~ ✓ — see [`../god-sim-prototype/`](../god-sim-prototype/README.md): deterministic sim core + the Living Atlas in one self-contained HTML file. Headless tests cover autonomy, determinism, divergence, and all three power cascades. *Stack note: prototype sim is JavaScript so it runs anywhere with zero installs; it is the executable spec for the C# core at the Phase 1 gate.*
4. **Now: play it.** The Phase 0 success criterion is a feel test — watch 500 years, poke it, and decide what the loop is missing. Candidate next steps: prayer/faith v0.1, named Figures, wars, or straight to Phase 1 slice planning.
5. Parallel slow-burn: collect visual reference (Manor Lords, medieval cartography, illustrated atlases) into a `reference/` moodboard; start the working-title shortlist conversation.
