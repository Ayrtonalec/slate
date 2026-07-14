/* SLATE Phase 0 — the Chronicle: an append-only log of everything that
   happens, with prose templates. This is the seed of the event-sourcing
   architecture from the design bible: the sim emits facts, the chronicle
   renders them as history. */
(function (S) {
  'use strict';

  // importance: 1 minor, 2 notable, 3 major. tone: 'plain' | 'god' | 'doom'.
  const T = {
    landing: (w, d) => [2, 'plain', `${S.CULTURES[d.culture].demonym} come ashore; ${d.name} is raised where the boats were drawn up.`],
    found: (w, d) => [1, 'plain', `${d.name} founded by settlers out of ${d.parent}.`],
    camp: (w, d) => [2, 'plain', `A mining camp takes root beneath ${d.region}; they call it ${d.name}.`],
    village: (w, d) => [1, 'plain', `${d.name} grows into a proper village.`],
    town: (w, d) => [2, 'plain', `${d.name} raises walls — a town of some ${d.pop} souls.`],
    city: (w, d) => [3, 'plain', `${d.name} is now a city, greatest of ${S.CULTURES[d.culture].demonym}' holdings.`],
    goldSeed: (w, d) => [3, 'god', `A vein of gold blooms in the stone of ${d.region}. The mountain did not hold it yesterday.`],
    goldFound: (w, d) => [2, 'plain', `Prospectors strike gold near ${d.name}. Hungry men arrive within the season.`],
    boom: (w, d) => [2, 'plain', `${d.name} swells with fortune-seekers and grows rich on the diggings.`],
    famine: (w, d) => [2, 'plain', `Lean years strike ${d.name}; the granaries echo.`],
    abandon: (w, d) => [d.tier >= 2 ? 3 : 2, 'plain', `${d.name} stands empty${d.why ? ' — ' + d.why : ''}. The roads grow over.`],
    curse: (w, d) => [3, 'god', `The skies over ${d.place} turn against the living. The storm does not pass. The priests say something is angry.`],
    stormExodus: (w, d) => [2, 'doom', `The people of ${d.name} abandon their homes to the endless storm.`],
    bless: (w, d) => [3, 'god', `A gentleness settles on the land near ${d.place}; seeds take root wherever they fall. Shrines appear by the field-edges.`],
    dragon: (w, d) => [3, 'doom', `A wyrm has nested in ${d.region}. They name it ${d.dragon}. Its shadow falls across ${d.name}.`],
    raid: (w, d) => [2, 'doom', `${d.dragon} descends upon ${d.name}; the granaries burn.`],
    razed: (w, d) => [3, 'doom', `${d.name} is no more. Men will call that place ${d.ruinName}.`],
    slain: (w, d) => [3, 'plain', `${d.hero} of ${d.name} slays ${d.dragon} in ${d.region}. The hoard comes down the mountain in carts.`],
    dragonGone: (w, d) => [2, 'plain', `${d.dragon}, finding nothing left worth coveting, flies beyond the map's edge.`],
    road: (w, d) => [1, 'plain', `A road now runs between ${d.a} and ${d.b}.`],
    migration: (w, d) => [1, 'plain', `Refugees out of ${d.from} settle in ${d.to}.`],
  };

  function create() {
    return { events: [], nextId: 1 };
  }

  function add(world, type, data) {
    const tpl = T[type];
    if (!tpl) throw new Error('unknown chronicle type: ' + type);
    const [imp, tone, text] = tpl(world, data);
    const e = {
      id: world.chronicle.nextId++,
      type, imp, tone, text,
      year: world.year, month: world.month,
      x: data.x, y: data.y,
    };
    world.chronicle.events.push(e);
    if (world.onEvent) world.onEvent(e);
    return e;
  }

  function toMarkdown(world) {
    const lines = [];
    lines.push(`# The Chronicle of ${world.title || 'an Unnamed World'}`);
    lines.push('');
    lines.push(`*World seed \`${world.seed}\` · years 0–${world.year} · set down by no mortal hand.*`);
    lines.push('');
    let century = -1;
    let lastYear = -1;
    for (const e of world.chronicle.events) {
      const c = Math.floor(e.year / 100);
      if (c !== century) {
        century = c;
        lines.push(`## Years ${c * 100}–${c * 100 + 99}`);
        lines.push('');
      }
      const yearLabel = e.year === lastYear ? '     ' : String(e.year).padStart(5, ' ');
      lastYear = e.year;
      const mark = e.tone === 'god' ? ' ✦' : e.tone === 'doom' ? ' †' : '';
      lines.push(`- **Year ${e.year}**${mark} — ${e.text}`);
    }
    lines.push('');
    lines.push('---');
    const alive = world.settlements.filter((s) => !s.ruined);
    lines.push(`*As of Year ${world.year}: ${alive.length} living settlements, ` +
      `${world.ruins.length} ruins, ${world.chronicle.events.length} recorded events.*`);
    return lines.join('\n');
  }

  S.chronicle = { create, add, toMarkdown };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
