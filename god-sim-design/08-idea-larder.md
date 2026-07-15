# The Idea Larder — proposals, not canon

*Written 2026-07-15 at the founder's request ("ga los en kom met je beste ideeën").
Nothing here is scheduled. Ideas graduate into `TODO.md` only when the founder
greenlights them. Everything below was filtered through four gates:*

1. *Serves a pillar: the alive world, the Chronicle, faith, or the D&D export.*
2. *Obeys the Theater Principle: statistical at macro, performed at zoom.*
3. *Deterministic: own labeled rng stream, fixed place in the tick order.*
4. *Sagas, not noise: rare and consequential beats frequent and forgettable.*

**The design rule of thumb this list keeps rediscovering:** the aliveness of the
map comes from things that **move across it** (armies, caravans, plagues, treks,
fires) and things that **scar it** (ruins, battlefields, burned forests). Every
strong idea below does one of the two. Static +2% modifiers do neither.

Cost codes: **S** = days, **M** = week-ish, **L** = weeks, **XL** = a phase.

---

## 1. What the other god games have (and what our answer is)

| Game | Its signature thing | Our status / answer |
|---|---|---|
| WorldBox | Toybox chaos: instant lava, lightning, borders shifting live | Powers exist; need more *instant-feedback* toys (§4) and visible border pulse |
| Dwarf Fortress | Legends mode: every person/item/place has traceable history | The Chronicle IS this pillar; add **artifacts** (§3.4) and **Figures** (T3) to deepen it |
| Black & White | Being physically present (creature, gestures, worshipped) | Avatar verb parked (doc 04); faith = T4. The *presence* fantasy is our Phase 2 soul |
| Crusader Kings | Personal drama: dynasties, feuds, ambition | Figures v1 (T3) is the door; §3.1 dynasties hang off it |
| Civilization | Wonders, eras, great people | Tech-pace dial parked (doc 04); mortal-built wonders in §3.6 |
| Manor Lords | Burgage plots visually upgrading; one gorgeous village | Our Settlement zoom; districts/specialization (§2.4) is the atlas-scale version |
| From Dust | Physical terrain consequence (water finds its way) | Bounded version: river rerouting + floods (§2.2), never full fluid sim |
| RimWorld/DF | The storyteller: adversity with rhythm | A deterministic "history director" that *spaces* regional drama (§5.3) — carefully |
| Songs of Syx | Massive-scale economy strata | Production vectors (T3) — we stay lighter on purpose |

Half of the founder's wish list (religion visible, figures, districts) is already
the designed soul of the game (docs 03/04, T3/T4). The larder adds what *isn't*
yet anywhere.

---

## 2. Realism: the medieval world we're still missing

### 2.1 Plague along the trade routes ★ my top pick
**What:** Rare (once per 1–3 centuries) a pestilence enters through a port or
caravan town and spreads **along the road/sea network we already simulate** —
market towns and their hinterlands get hit hardest (density), isolated hamlets
are spared. 20–40% mortality in connected places over 2–5 years, then it burns
out. Aftermath: survivor prosperity bump (labor scarcity — historically real),
abandoned villages, a generation-long shadow in the chronicle.
**Why real:** The Black Death is *the* shaper of late-medieval history, and its
vector was exactly our graph: trade.
**Why fun:** It's a visible traveling phenomenon (sick towns tint, roads empty,
caravans stop), it inverts the market-town logic (your biggest city is your
weakest point), and it writes devastating chronicle prose. Interacts with
everything: wars pause or turn opportunistic, walls don't help, faith (later)
spikes — plague was the great faith event.
**Fit/cost:** M. Own rng stream `plague`; contagion = deterministic walk over
the road graph + proximity. Theater: plague banners on towns, thinning crowds.
**Hook for later:** armies carry it (sieges spread disease — historically true),
refugees carry it, and the Interpretation Engine will someday ask: *whose god
sent this?*

### 2.2 Natural disasters (the "acts of no one" package)
The world currently only suffers what god or dragons do. History's other author
was nature. All rare, all scarring, all on their own rng stream:
- **Wildfire** (S/M): dry summer + forest → a fire front that visibly eats
  woodland for weeks, threatens palisaded (wooden!) towns, leaves a black scar
  that regrows over ~40 years. Uses the forest data we have; regrowth makes the
  land itself historical.
- **Flood** (S): river settlements get fertility years and flood years; a great
  flood damages riverside towns but re-silts fields (+fertility after). Delta
  land = rich but risky, exactly as it was.
- **Earthquake** (S): fault lines derived from worldgen ridges; topples **stone
  walls** (walls system interaction — a wall-cracked city invites a war of
  opportunity), rare, regional.
- **Locust cloud** (S): a visible cloud crossing farmland eating the crop rows
  from the map as it passes. Small system, unforgettable theater.
- **Harsh winter** (S): we have lean years; make winter severity visible —
  snowline descends, rivers freeze (caravans halt: winter isolation, and a
  little "roads open" spring moment each year).
- **Volcano** (M, only if worldgen gets one): generational — ash years then the
  most fertile fields in the world. A gift wrapped in a catastrophe.

### 2.3 Migration you can watch ("de verhuizing")
**What:** Three visible movers on top of the stats we already shift around:
1. **Family drift** — poor-village families with handcarts walking the roads to
   the booming market town (the market-pull mechanic, made visible).
2. **The Trek / Exodus** ★ — after a sack, plague, or famine streak, a whole
   community packs up: a wagon train crosses the map for weeks to found a new
   settlement somewhere far (reusing colonization site-scoring at range). The
   single most watchable emergent story a map can have; the god can bless the
   road ahead or scatter them.
3. **Transhumance** (ambient, S) — herders walking flocks to summer pastures
   and back. Pure seasonal breathing of the countryside.
**Fit/cost:** family drift S; the Trek M (a caravan entity like an Army, plus
foundation-on-arrival); transhumance S.

### 2.4 Districts & specialization ("de wijken") ★
**What:** Settlements stop being generic house-blobs and grow **functional
quarters derived from stats we already track**: fish → docks, drying racks,
boats out at dawn; gold → smithies with smoke; high hinterland → big market
square with stalls and a granary count (famine becomes *visible*: empty
granaries); walls → cramped inner city + **poor sprawl outside the walls**
(historically real, and it burns first in a siege — theater cruelty); wealth →
a stone hall, later a temple quarter (T4). Houses themselves get 2–3 visual
tiers (hovel → timber → stone) by settlement wealth.
**Why:** This is the deep answer to "identical houses": differentiated
*function*, not just size. It also pre-builds the stage that faith (temples)
and Figures (the lord's hall) will walk onto.
**Fit/cost:** M–L, entirely theater-layer (renderer reads existing stats; no
sim change). The best visual-return-per-effort item on this list.

### 2.5 Bandits & sea-raiders (the wilds should bite)
**What:** Wilderness is currently scenery. Give it teeth: **bandit camps**
seed in rough hills near rich roads, skim caravans (visible ambush moment,
prosperity dip on that route) until a town musters wardens or a hero (Figures)
clears them — a little pacification arc. **Sea-raiders** (very Vasker) beach
at rich coastal towns, burn, and leave; coasts respond with watchtowers (an
extension of the walls-as-choice system).
**Why real:** Roads were dangerous; the viking age happened.
**Why fun:** Danger gradients make geography matter (the safe interior vs the
rich risky coast), and raids create local sagas without full wars.
**Fit/cost:** M. Camp = mini-settlement entity; raid = mini-army.

### 2.6 Rivers as arteries
Bridges appear where roads cross rivers (named — "the bridge at X" is exactly
the kind of place D&D campaigns are made of), river barges as a caravan
variant, water mills on river towns (+prosperity, visible wheel). Ports and
**sea lanes** between coastal towns are the L-sized big brother: ships,
cross-sea contact, port cities becoming the richest places in the world —
historically true and it finally makes the ocean alive. Cost: bridges/mills S,
barges S, sea trade L.

### 2.7 The living forest
Towns visibly eat their forests (deforestation rings around big cities — the
medieval reality), regrowth after abandonment, deer & wolves for hunters to
meet, one white stag every few centuries for the chronicle to whisper about.
Cost: S–M, mostly renderer + a slow forest-state layer.

---

## 3. Story-engine additions (the chronicle's best friends)

### 3.1 Figures v1+ — dynasties, temperament, feuds (already T3, prioritize)
Everything the founder keeps asking for personally ("a chaotic warlord who
burns everything") lands here. Minimal strong version: every town of rank has
a ruling family; leaders have 2–3 trait words (cruel/pious/builder/greedy);
war outcomes read temperament (torch vs throne — the hook is already in the
war code); succession occasionally splits a family into a feud (a war casus
belli that is *personal*); heroes and prophets get deed ledgers.
**This is the single highest-leverage system on the list** because five other
ideas (dynasty drama, artifacts, bandit-clearing heroes, wonder-builders,
interpretation voices) plug into it. Cost: L (but it's already planned scope).

### 3.2 "The land remembers" — scars & named places ★
Battlefields get cairns and names ("Redford Field, where the Vasker broke");
sacked cities' ruins age visibly (fresh smoke → blackened shells → grassed-over
mounds after a century); abandoned roads fade; old walls half-bury. The map
becomes a readable history document — no other god game does this properly, and
for the D&D-export audience it auto-generates *adventure sites*. Cost: S–M
(generalize the ruins list into a `Scar` entity with age-staged rendering).

### 3.3 Omens (the Interpretation Engine's opening act)
Deterministic celestial schedule: eclipses, comets, red moons. Mortals react
per culture (panic, prophecy, a war blamed on the sky). Later, faith makes
omens contested readings of *you*. Cheap (S) and it seeds the game's soul early.

### 3.4 Named artifacts & relics ★
Objects born from events — the crown of a conquered city, the wyrmslayer's
sword, the first sheaf of a blessed harvest — that **travel**: looted in sacks,
carried home by armies, enshrined (T4 temples), lost in ruins for later ages to
find. Each artifact is a thread stitching decades of chronicle together, and
the D&D export turns them into instant plot hooks. Cost: S–M (an entity with a
location + event log; the sim already generates all the birth-moments).

### 3.5 Saga view — the chronicle curates itself
Auto-compiled threads: "The Rise and Fall of Grimbridge", "The War of the Two
Rivers", "The Years of the Pest" — grouping related events by place/actors/time
into readable story arcs, in-game and in the markdown export. The chronicle is
the product; curation multiplies it. Cost: M (pure data analysis, no sim).

### 3.6 Mortal-built wonders
A proud, rich city starts a project across decades (a colossus, a great
temple, a sea wall): visible construction stages, drained treasury, chronicle
milestones — and the god can bless, ignore, or topple it. Half-built wonders
after a sack are the best ruins imaginable. Cost: M.

---

## 4. Toybox & god-feel (the WorldBox lesson: instant feedback)

- **More instant-consequence powers** with visible first-minute effects:
  split a river, raise a hill (Shape verb, S–M each within our banded terrain);
  a plague of frogs (harmless, mortals interpret furiously — S).
- **Border pulse**: culture borders visibly breathe on conquest/growth (S).
- **Beacon chains**: when war marches, smoke columns relay along hilltops
  toward the threatened town (S, pure theater, LotR energy).
- **Heraldry generator** ★ (S–M): procedural coats of arms per culture → town →
  dynasty; on banners, halls, army flags, the atlas, and the D&D export. Pure
  code, disproportionate identity payoff.
- **Documentary mode** (S): slow auto-panning camera that follows interest
  (recent events), for the Bob Ross / Twitch-idle experience.

---

## 4b. THE ENDLESS SEA — founder's infinite-world proposal (2026-07-15) ★★

**The founder's pitch:** an animated dark fog wall over the ocean at the edge
of the known world. Mortals must launch ships to explore; expeditions discover
new procedurally generated lands (big, small, everything between) — and it can
go on forever, Minecraft-ish. Is it possible: assets, technology, multiplayer,
performance?

**Verdict: yes — with one crucial reframe, and it's a better fit for THIS game
than for almost any other.** Literal Minecraft streaming (only what's loaded
exists/simulates) contradicts our soul: we simulate a *whole world's history*.
The reframe: **not an infinite world — an unbounded *discovered* world.** The
world is the set of regions mortals have found. The fog isn't hiding terrain;
beyond the fog there IS nothing yet — the world comes into being when it is
witnessed. (For a god game, that's not a hack, that's theology.)

### How it works
- **Region = worldgen tile** (~a current map, 192×120 cells), generated
  deterministically from `f(seed, regionX, regionY)` — discovery order can't
  change what a region contains (determinism law holds).
- **Expeditions**: port towns launch ships (own rng stream `exploration`;
  prosperity + population pressure + a restless culture spark them). A visible
  ship sails into the fog (theater, like armies). Fates: returns with a
  discovery / returns with nothing / lost with all hands (chronicle drama
  either way). God powers apply: bless the voyage, or storm it.
- **On discovery** the region materializes, in one of two flavors, mixed:
  - *Virgin land*: empty wilderness; colonization spreads (Polynesian /
    Age-of-Sail energy). Cheap.
  - *Peopled land* ★: generate the region AND fast-forward its own isolated
    history to the current year — our deterministic core simulates 500 years
    in under a second, so "the explorers found an empire with a real
    600-year chronicle" is *actually computable on the spot*. No other
    engine trick we own is this uniquely ours. First contact between an old
    culture and a fresh one is doc-05's drama engine, single-player.
- **The fog**: animated dark wall (particles + shader) ringing the discovered
  world; retreats when a region joins. Optional tease: faint far peaks on
  clear days ("sailors swear they saw mountains"). Rumors before discovery.
- **The chronicle** gains an Age of Discovery: expedition sagas, first
  contact, colonial wars, plague jumping continents by ship (all existing
  systems compose!). The D&D export becomes a world atlas with terra
  incognita — campaign gold.

### Feasibility honestly
- **Technology (sim)**: the real cost. Today every array/loop assumes one
  fixed W×H grid. Refactor: `World` holds regions (each its own grid +
  offset); global queries route through region lookup; settlements/armies/
  roads already live in lists (fine); grid systems (fire, regrowth, claims,
  fert) become per-region. Big but mechanical; the executable-spec tests
  guard the port. Size: **XL — its own phase** ("the Archipelago update").
- **Performance**: compute grows with *discovered* regions, not with infinity.
  ~0.15ms/tick per region today → 10 regions ≈ 1.5ms/tick: nothing at Manor
  Lords pace, manageable at speed III (cap concurrent "hot" regions or
  slow-tick distant ones — statistical LOD: far regions tick yearly, near
  regions monthly — the Theater Principle applied to TIME). Memory: ~2MB per
  region. Verdict: dozens of regions easy = practically infinite.
- **Multiplayer**: *better* than fine — event-sourced world = save/netcode is
  just (seed + event log); discovered-region set derives from events.
  Pantheon co-op discovery races are an obvious delight.
- **Assets**: nearly zero new — ships (procedural or one pack model), the fog
  (our VFX toolkit), region terrain reuses everything.
- **Risks**: attention dilution (a god can't watch 10 continents — mitigate:
  discovery is rare/expensive, regions coalesce into "the known world" at
  atlas zoom); sim refactor churn (do it as a phase, not a patch); ocean
  seams between regions (worldgen must blend edges — make outer ring of every
  region deep ocean, so regions are islands/continents in a shared sea: no
  seam problem at all ★ easiest correct answer).

### Suggested placement
After the current batches (C, B, D) — it multiplies everything they add
(figures captain the ships, districts get harbors, faith gets missionaries).
Prototype the *fog wall + one scripted expedition* early (S, pure theater)
to feel it; schedule the sim refactor as its own phase with founder sign-off.

## 5. Structural notes

### 5.1 Population realism boundary
Age pyramids and per-person sim stay OUT at atlas scale (Theater Principle).
Famine dropping births and boom towns pulling the young is as deep as macro
needs; Figures carry the personal layer.

### 5.2 Disasters × systems = the actual content
The gold is in interactions, all nearly free once both systems exist:
earthquake cracks walls → war of opportunity; army sacks city → plague follows
the army home; plague empties a region → the Trek founds new towns → new
border frictions. Design disasters as *inputs to existing systems*, never as
isolated animations.

### 5.3 A history director — handle with care
A deterministic pacing weight that *spaces* major dramas per region (no three
plagues in a decade, no century of nothing) is compatible with autonomy (it
only modulates event *timing* weights, seeded per run). It must never inject
events that have no cause. Prototype in the JS playground first (T6).

---

## 6. Suggested batches (when the founder greenlights)

- **Batch A — "The world bites back"** (realism + drama, ~M total each):
  Plague along trade routes · wildfire + flood + earthquake · scars & named
  battlefields. Uses: roads, forests, walls — all already built.
- **Batch B — "People, not tokens"**: Figures v1 (T3, the keystone) →
  dynasties & temperament → artifacts → bandits/raiders with heroes.
- **Batch C — "The face of things"**: districts & specialization · house
  tiers · heraldry · beacon chains · deforestation rings.
- **Batch D — "The soul"** (already the roadmap): faith v0.1 (T4) + omens +
  visible shrines/temples + interpretation voices.

My honest pick for maximum founder-joy-per-week: **A, then C, then B, then D**
— but B's Figures unlock the most downstream content, so if the chronicle
feels impersonal by then, swap B forward.
