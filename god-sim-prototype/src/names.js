/* SLATE Phase 0 — procedural naming: cultures, places, people, regions.
   Four founding cultures, each with its own phoneme flavor so the map
   reads as a world with distinct peoples, and separated colonies drift
   apart in sound. */
(function (S) {
  'use strict';

  S.CULTURES = [
    {
      id: 0, name: 'Aldish', color: '#55663B', demonym: 'the Aldish',
      starts: ['Ash', 'Thorn', 'Wex', 'Bram', 'Hart', 'Elm', 'Grim', 'Wether', 'Ryd', 'Col', 'Marl', 'Oat', 'Fenn', 'Bar'],
      ends: ['ford', 'stead', 'hollow', 'wick', 'bury', 'combe', 'field', 'mere', 'don', 'leigh', 'thorpe', 'bridge'],
      goldEnds: ['delve', 'hollow', 'wick', 'ford'],
      people: ['Osric', 'Aldwyn', 'Berta', 'Cedd', 'Godgifu', 'Hild', 'Leof', 'Wulfa', 'Eda', 'Sigred', 'Oswin', 'Merewen'],
    },
    {
      id: 1, name: 'Vasker', color: '#4A6274', demonym: 'the Vasker',
      starts: ['Skjal', 'Hrafn', 'Ulf', 'Gren', 'Kald', 'Bjor', 'Stein', 'Varg', 'Eld', 'Snor', 'Hval', 'Jarn'],
      ends: ['vik', 'heim', 'fjall', 'strand', 'nes', 'dal', 'holm', 'gard', 'foss', 'havn'],
      goldEnds: ['gruva', 'delv', 'heim', 'gard'],
      people: ['Ragna', 'Torvald', 'Sigrun', 'Eirik', 'Halla', 'Bjarke', 'Yrsa', 'Knut', 'Solveig', 'Orm', 'Astrid', 'Geir'],
    },
    {
      id: 2, name: 'Serai', color: '#A2652F', demonym: 'the Serai',
      starts: ['Al-Qas', 'Zafir', 'Mira', 'Sahl', 'Dar', 'Kal', 'Azar', 'Nur', 'Rasha', 'Tal', 'Zeyd', 'Har'],
      ends: ['ir', 'aba', 'oun', 'esh', 'ara', 'im', 'at', 'ez', 'ula', 'an'],
      goldEnds: ['-dhahab', 'ara', 'ir', 'esh'],
      people: ['Zahra', 'Idris', 'Layl', 'Basim', 'Naima', 'Tariq', 'Suheir', 'Omar', 'Yasmin', 'Khalid', 'Farah', 'Nadim'],
    },
    {
      id: 3, name: 'Tessian', color: '#6E4A5E', demonym: 'the Tessians',
      starts: ['Val', 'Cor', 'Aur', 'Sept', 'Mar', 'Luc', 'Tarr', 'Vin', 'Cael', 'Ost', 'Pell', 'Riv'],
      ends: ['ium', 'ora', 'essa', 'anum', 'ola', 'is', 'urnum', 'atia', 'ento', 'aris'],
      goldEnds: ['aurum', 'ora', 'ium'],
      people: ['Livia', 'Cassian', 'Aurel', 'Petra', 'Marcus', 'Octavia', 'Loran', 'Vitus', 'Sabina', 'Tullo', 'Camilla', 'Renz'],
    },
  ];

  const REGION_ADJ = ['Gray', 'Amber', 'Ashen', 'Whispering', 'Sundered', 'Elder', 'Silent', 'Golden', 'Thorn',
    'Mist', 'Winter', 'Red', 'Hollow', 'Storm', 'Far', 'Sleeping', 'Broken', 'Pale', 'Iron', 'Weeping'];
  const RANGE_END = ['Peaks', 'Spine', 'Crowns', 'Reach', 'Teeth', 'Fells', 'Wall'];
  const FOREST_END = ['Weald', 'Wood', 'Deepwood', 'Tangle', 'Wilds'];
  const SEA_END = ['Deep', 'Main', 'Expanse', 'Mirror', 'Gulf'];
  const DESERT_END = ['Waste', 'Sands', 'Reach'];
  const DRAGON_NAMES = ['Vharax', 'Skorn', 'Ymmeth', 'Cauldrax', 'Nyr', 'Vellumbra', 'Oroth', 'Kazmyre', 'Sarquel', 'Draumr'];

  function makeNamer(seed) {
    const rng = S.rng.makeRng(seed, 'names');
    const used = new Set();

    function unique(make) {
      for (let i = 0; i < 40; i++) {
        const n = make();
        if (!used.has(n)) { used.add(n); return n; }
      }
      // Give up on uniqueness gracefully rather than looping forever.
      const n = make() + ' ' + rng.int(2, 9);
      used.add(n);
      return n;
    }

    function joinName(a, b) {
      if (b.startsWith('-')) return a + b.slice(1);
      // Avoid awkward duplicate letters at the seam ("Thornnes" → "Thornes").
      if (a[a.length - 1] === b[0]) return a + b.slice(1);
      return a + b;
    }

    return {
      place(cultureId) {
        const c = S.CULTURES[cultureId];
        return unique(() => joinName(rng.pick(c.starts), rng.pick(c.ends)));
      },
      goldPlace(cultureId) {
        const c = S.CULTURES[cultureId];
        return unique(() => {
          if (rng.chance(0.5)) return joinName('Gold', rng.pick(c.goldEnds));
          return joinName(rng.pick(c.starts), rng.pick(c.goldEnds));
        });
      },
      person(cultureId) {
        return rng.pick(S.CULTURES[cultureId].people);
      },
      region(kind) {
        const adj = () => rng.pick(REGION_ADJ);
        return unique(() => {
          switch (kind) {
            case 'range': return 'The ' + adj() + ' ' + rng.pick(RANGE_END);
            case 'forest': return rng.chance(0.5) ? 'The ' + adj() + ' ' + rng.pick(FOREST_END) : adj() + rng.pick(['wood', 'weald']).toLowerCase();
            case 'sea': return 'The ' + adj() + ' ' + rng.pick(SEA_END);
            case 'ocean': return 'The ' + rng.pick(['Endless', 'Outer', 'Sunless', 'Wide']) + ' ' + rng.pick(['Ocean', 'Main']);
            case 'desert': return 'The ' + adj() + ' ' + rng.pick(DESERT_END);
            default: return 'The ' + adj() + ' Land';
          }
        });
      },
      dragon() {
        return rng.pick(DRAGON_NAMES);
      },
    };
  }

  S.names = { makeNamer };
})(typeof SLATE !== 'undefined' ? SLATE : (globalThis.SLATE = {}));
