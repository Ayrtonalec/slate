# Multiplayer & streamability

## Why this game is unusually multiplayer-friendly (technically)

God inputs are sparse RTS-style commands, not 60Hz movement. With the deterministic, event-sourced sim core (doc 02), multiplayer is "replicate a low-bandwidth command stream against a server-authoritative sim" — the *cheapest* honest netcode shape that exists. The hard part isn't bandwidth; it's sim determinism discipline, and we're paying that cost anyway for saves/scrubbing. This is why event sourcing is a day-1 rule.

Still: multiplayer ships **after** the solo game is fun (see roadmap). Below is the ladder, cheapest first — each rung is valuable alone.

## The multiplayer ladder

1. **Share codes & exports (ship with v1.0):** world seeds, map paintings, and full chronicle exports are shareable artifacts. Zero netcode; seeds the community.
2. **Archaeology mode (async, cheap, extremely on-brand):** load a *copy* of a friend's 4,000-year world. Explore their ruins, read their chronicle, leave one artifact/inscription they'll discover. Async "multiplayer" for the worldbuilder audience with near-zero infrastructure.
3. **Pantheon co-op (2–4 gods, one world — the headline mode):**
   - Spawn cultures on opposite sides of the world; each god tends their own garden under a different divine style.
   - **First contact is the drama engine:** a Dread-shaped culture meeting a Devotion-shaped culture is an automatic story — trade, holy war, syncretism, missionary races. The chronicle credits both gods; the world's history becomes *your friendship's* history.
   - Shared-pantheon option: both gods worshipped by the same civilization with different domains (storm-god + harvest-god), competing gently for altar-share.
   - **Co-authored miracles:** combine domains for wonders neither god can afford alone (your storm + my sea = the Great Flood, chronicled forever with both names). Great co-op ritual; great stream moment.
4. **Rival pantheons (soft competition):** faith-share is the scoreboard; gods never directly attack each other — but their *followers* can war. Competitive drama without PvP toxicity; fits the chill covenant.
5. **Stretch — "DM mode" (asymmetric):** one head god (the DM) referees while friends incarnate as mortal Heroes doing quests inside the living world. This is the D&D fantasy closed into a loop. Explicitly a stretch goal; noted because it's the mode this audience will beg for.

## Streamability (design it in, don't bolt it on)

- **Chat as citizens:** viewers' names become villagers/Figures; the chronicle tracks their fates. "RIP xXDragonLord99Xx, taken by the plague of Year 341" is a clip, a subscription, and a reason the stream's world matters to its chat.
- **Chat as congregation:** channel-point votes on miracles ("mercy or meteor?"), prayer raids, chat-voted omens. The god game is the only genre where the audience being a *mob petitioning the streamer* is diegetic.
- **Session chronicle:** auto-generated "what happened this stream" summary + shareable lore cards (a Figure's deed-sheet, a battle, a map-region). Organic marketing that the game produces as a side effect of existing.
- **Legibility rules:** big miracles telegraph clearly at Province zoom; the Interpretation Engine surfaces "what they think of you" as readable UI moments. Spectacle must parse in a 720p stream window.

## The chill covenant (anti-scope, anti-toxicity rules)

- No fail states, ever. Worlds wind down into myth (doc 03); they don't game-over.
- Pause is sacred; nothing punishes absence. A pantheon world persists and gods drop in like a Minecraft server, not a raid schedule.
- No direct god-vs-god damage verbs. Conflict flows through mortals and faith-share only.
- Small player counts (≤4) — this is a shared terrarium, not an MMO. Keeps netcode, hosting, and social dynamics tractable.
