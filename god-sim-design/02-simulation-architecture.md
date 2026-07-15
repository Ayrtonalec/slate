# Simulation architecture

The single most important technical/design decision in the project. Get this right and "4,000 years, Manor Lords zoom, feels alive" becomes feasible for a small team. Get it wrong and the game is impossible.

## The Theater Principle

**The zoomed-out simulation is the truth. The zoomed-in world is theater performing that truth.**

We never simulate a million individual people. We simulate populations, cultures, economies, and faiths *statistically*, plus a small cast of named individuals. When the player zooms to ground level, the game *instantiates* a convincing scene from the statistical state — villagers, market stalls, a funeral if the plague stat is high — using deterministic seeds so the same place looks consistent on revisit. When they zoom out, the theater despawns; the numbers keep ticking.

This is how Dwarf Fortress, Crusader Kings, and every functioning grand-scale sim actually works underneath. The zoom isn't a graphics gimmick — it's the sim's level-of-detail system made visible.

## Three layers

| Layer | Scope | Source of truth? | Runs when |
|---|---|---|---|
| **Chronicle (macro)** | Whole world: climate, populations, cultures, religions, economy flows, wars, named Figures | **Yes** | Always, every tick |
| **Province (meso)** | A region: settlement layouts, roads, farms, local terrain detail | Derived, cached | When viewed or when macro events demand it |
| **Theater (micro)** | Ground level: individual visible people, animals, weather particles | Pure presentation | Only while watched |

Battles follow the same rule: resolved by macro math (strength, terrain, leaders) instantly — but if the player is *watching*, the resolution plays out as a staged battle scene. You never wait for the theater; the theater waits for you.

## Time

- Macro tick = 1 week (52/year). 4,000 years ≈ 208k ticks — trivial for a headless sim that's mostly arithmetic on region structs.
- Player-controlled time dilation: pause ↔ days-per-second (ground watching) ↔ years-per-second (era fast-forward). Fast-forward uses **era compression**: batch ticks, log only chronicle-worthy events.
- **Time scrubbing:** because state = snapshot + event replay (below), we can show the world map at Year 850 on demand. Watching your world's borders and cities morph across a timeline slider is a signature feature for the worldbuilder audience.

## Event sourcing: one architecture, five features

Every sim mutation is an immutable, structured event in an append-only log ("The Chronicle" is literally the database). This single decision gives us:

1. **The lore system** — the chronicle UI is a query + pretty-printer over the log.
2. **Saves** — log + periodic snapshots. Compact, corruption-resistant.
3. **Time scrubbing / replays** — replay to any year.
4. **Multiplayer** — god inputs are sparse commands; a server-authoritative event stream is exactly the netcode a sim like this wants (see doc 05).
5. **Mod & export APIs** — the log is an open, documented format.

Design rule: **the sim emits facts; interpretation is layered on top** (see doc 03 — cultures remember events *wrongly*, and that divergence is stored separately from the facts).

## Autonomy & divergence — two hard guarantees

**Guarantee 1: the world needs no god.** The sim is fully self-driving. A zero-input run from Year 0 produces a complete history — cultures form, wars break out, dynasties rise, religions emerge (to gods mortals *invent*: sun cults, ancestor spirits — not to you; see the Interpretation corollary in doc 03). The god is a perturbation on a living system, never its engine. This isn't aspiration, it's enforced by architecture: the CI integration runs are god-less by definition, so an autonomous world is literally the thing we test every day. It also falls out as a free feature — **Observer Mode**, the pure ant-farm run, which some players (and streams) will treat as the whole game.

**What "alive" includes — the aliveness contract (founder-locked, 2026-07-15).**
"The world runs itself" means far more than settlements growing. The autonomous
baseline must include, with zero divine input: cultures that drift apart and
diverge; **wars and raids between cultures and factions**; **clans, factions and
dynasties** — named groups led by Figures; **trade** — routes and prosperity flows,
with caravans/ships as visible theater; **farming and production** — statistically
simulated per the Theater Principle, visible as fields/pastures/fisheries at closer
zooms; migration and refugee flows; and religions that emerge without the player.
Implementation is phased (see roadmap), but none of this is optional flavor: a
build doesn't count as "the game" until the world does all of this to itself.

**Guarantee 2: runs diverge — by engineering, not by hope.** Emergent sims converge to sameness by default (the "every WorldBox endgame looks alike" problem; naive randomness gives different numbers, not different *stories*). Unrecognizably-different runs are a designed property:

- **Geography is destiny:** map-gen variance propagates through site scoring into everything downstream — where the first river valley is settled reshapes 4,000 years.
- **Early symmetry breaking:** small initial differences are allowed to compound, never damped toward a "balanced" middle.
- **Multiple historical attractors:** the systems must support genuinely different *shapes* of history — a hegemonic thousand-year empire; a permanently fractured world of feuding city-states; a theocratic age; a post-plague dark age; monster-checked wilds where civilization never consolidates. Different attractors, not different parameter values.
- **Fork points:** pivotal Figures and rare high-impact events (a conqueror's early death, a prophet's improbable survival) genuinely branch history.
- **Cultural drift:** similar geography still grows different societies, because values vectors wander.
- **The player** — the biggest chaos agent of all, but explicitly not a required one.
- **Tuning rule:** prune *degenerate* sameness (extinction by Year 300, one-blob-conquers-all, gray eternal stalemate) without sandpapering off the interesting extremes. The goal is a wide distribution of *legible* histories.
- **Measured, not vibed:** CI runs a battery of seeds and asserts *spread* on history metrics (civ counts, war frequency, religion counts, era pacing, largest-empire share). If a tuning change collapses the distribution — runs getting samey — that's a failing test, same as a crash.

## Systems inventory (macro layer)

- **Climate & weather:** latitude bands, rain shadow, ocean currents (coarse), plus divine overrides (cursed/blessed zones). Multi-year droughts create migrations and faith spikes.
- **Hydrology & terrain:** rivers matter — settlement site scoring weights fresh water, arable land, resources, defensibility, harbors. This is what makes "spawn gold in a mountain → mining town" work *systemically* rather than as a scripted response.
- **Ecology:** simplified 3-level food web (vegetation → herbivores → predators). Overhunting/overfarming → collapse → famine → migration. Monsters are apex predators with lairs and behaviors (dragons hoard wealth — see worked example below).
- **Population & settlements:** statistical pops per settlement (size, health, wealth, occupations). Settlements found, grow, specialize, and die by site score + events.
- **Economy:** abstract production/consumption vectors per settlement + trade routes as flows. Visualized as caravans/ships (theater). Inequality stat feeds unrest.
- **Cultures:** a values vector (militarist↔pacifist, pious↔skeptical, open↔insular, …) that drifts with environment, events, and *your* deeds. Separated populations diverge → new cultures, dialect drift via name-generator phoneme shifts (language flavor without simulating language).
- **Tech & eras:** Dawn → Settled → Bronze → Iron → Classical → Medieval (base game caps ~here; see the tech-pace dial in doc 04). Progress speed is influenceable by the god.
- **Conflict:** casus belli emerge from values + scarcity + religious tension. Wars resolved statistically, chronicled richly, watchable as theater.
- **Figures (stand-out people):** individuals get "promoted" from the statistical pool when they cross notability thresholds (deeds, offices, wealth, witnessing a miracle). Figures have relationships, deeds ledgers, generated epithets, bloodlines/dynasties. Warlords and revolutionaries aren't scripted — conditions (inequality + doubt + famine) raise revolt odds, and a revolt promotes a leader Figure.
- **Fame & news propagation:** renown travels along trade routes at realistic speed and *mutates* as it travels — rumor distance is measurable, and it feeds the myth system.
- **Myth-making:** cultures record their *belief* about events, which drifts from the facts over generations. The gold you spawned becomes "the Mountain-Father's gift" three centuries later. Chronicle stores both.
- **Place naming:** procedural names reference chronicle events ("Dragonfall Bay," "Ashenford"). Nearly free to build, disproportionately magical for the target audience.

## Worked example: the gold cascade

Player action, Year 112: *spawn a rich gold vein in the Graypeaks.*

1. Prospector Figure "discovers" it (Year 114 — the sim backfills a plausible discovery story; the chronicle records both the fact *(divine placement)* and the belief *(lucky prospector)*).
2. Site score spikes → mining camp → boomtown ("Goldhollow") within a generation. Trade routes reroute; a lowland port grows rich on the flow.
3. Wealth inequality rises in the region → unrest events → a miners' revolt promotes a revolutionary Figure (Year 156).
4. Concentrated treasure raises dragon-lair attraction. A dragon nests above Goldhollow (Year 203). Terror → prayer volume spikes (crisis is a faith opportunity — see doc 03's moral hazard).
5. Player may intervene (slay it, ignore it, *feed* it). Each choice re-scores their divine Interpretation, spawns myths, renames places, and seeds the next century of consequences: hero Figures who challenge the dragon, a fear-cult of the Wyrm, abandoned Goldhollow becoming a famous ruin and pilgrimage site.

One click, a century of authored-feeling history, zero scripting. This cascade is the demo, the trailer, and the review quote. Everything in the architecture exists to make loops like this fall out naturally.

## Performance envelope (targets, not promises)

- ~200–500 settlements, ~50–200 living Figures, statistical populations in the millions.
- Headless macro sim must run *fast* (whole-history integration tests in CI: "generate 4,000 years, assert no NaNs, no extinct-world degenerate states").
- Theater budget: ~200–2,000 visible agents, all presentation-only.
- Determinism discipline from day 1 (fixed tick order, seeded RNG streams, no float-order sloppiness) — cheap now, impossible to retrofit, and it's what keeps multiplayer and time-scrubbing on the table.
