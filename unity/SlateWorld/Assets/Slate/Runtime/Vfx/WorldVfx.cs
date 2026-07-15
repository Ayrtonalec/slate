// SLATE — WorldVfx: reads the sim's state every frame and gives every
// phenomenon its voice. Fire licks and throws embers under a leaning smoke
// plume; sick towns exhale a green miasma under thin pyre smoke; locusts are
// an actual buzzing swarm; storms rain; blessings drift golden motes; winter
// snows; battles and earthquakes burst dust; sacked towns smolder for years.
// Visual layer only: Unity's Random here is legal — the sim never sees it.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class WorldVfx : MonoBehaviour
    {
        public static WorldVfx Instance { get; private set; }

        private WorldRunner _runner;
        private CameraRig _rig;

        private ParticleSystem _flames, _embers, _smoke, _pyre;
        private ParticleSystem _miasma, _locusts, _rain, _motes, _glitter;
        private ParticleSystem _snow, _dust, _sparks, _hearth;

        private class Smolder { public Vector3 Pos; public int UntilYear; }
        private readonly List<Smolder> _smolders = new List<Smolder>();

        // A small pool of flickering point lights: fire casts real light on the
        // world around it. Assigned to burning clusters each frame.
        private Light[] _fireLights;

        public void Init(WorldRunner runner, CameraRig rig)
        {
            Instance = this;
            _runner = runner;
            _rig = rig;
            var t = transform;

            _flames = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "flames", Material = VfxToolkit.FlameMaterial,
                ColorA = new Color(1f, 1f, 1f, 1f), ColorB = new Color(1f, 0.9f, 0.85f, 0.9f),
                SizeMin = 1.8f, SizeMax = 3.4f, LifeMin = 0.45f, LifeMax = 0.9f,
                Gravity = -0.5f, NoiseStrength = 0.55f, NoiseFrequency = 0.7f,
                FadeInOut = true, GrowOverLife = 0.5f, MaxParticles = 1500,
                TallAspect = 1.9f, // upright licks, not blobs
            });
            _embers = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "embers", Material = VfxToolkit.FleckMaterial,
                ColorA = new Color(1f, 0.7f, 0.2f, 1f), ColorB = new Color(1f, 0.45f, 0.1f, 1f),
                SizeMin = 0.15f, SizeMax = 0.35f, LifeMin = 1.2f, LifeMax = 2.6f,
                Gravity = -0.35f, NoiseStrength = 1.6f, NoiseFrequency = 0.8f,
                FadeInOut = true, MaxParticles = 600,
            });
            _smoke = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "smoke", Material = VfxToolkit.BillowMaterial,
                ColorA = new Color(0.32f, 0.30f, 0.28f, 0.35f), ColorB = new Color(0.45f, 0.43f, 0.40f, 0.28f),
                SizeMin = 3f, SizeMax = 6f, LifeMin = 3.5f, LifeMax = 6f,
                Gravity = -0.18f, NoiseStrength = 0.6f, NoiseFrequency = 0.25f,
                FadeInOut = true, GrowOverLife = 2.6f, MaxParticles = 900,
            });
            _pyre = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "pyre-smoke", Material = VfxToolkit.BillowMaterial,
                ColorA = new Color(0.30f, 0.28f, 0.26f, 0.55f), ColorB = new Color(0.20f, 0.19f, 0.18f, 0.45f),
                SizeMin = 1.4f, SizeMax = 2.6f, LifeMin = 3.5f, LifeMax = 5.5f,
                Gravity = -0.28f, NoiseStrength = 0.25f, NoiseFrequency = 0.3f,
                FadeInOut = true, GrowOverLife = 2.4f, MaxParticles = 700,
            });
            _miasma = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "miasma", Material = VfxToolkit.BillowMaterial,
                ColorA = new Color(0.42f, 0.54f, 0.22f, 0.75f), ColorB = new Color(0.30f, 0.40f, 0.18f, 0.85f),
                SizeMin = 4f, SizeMax = 8f, LifeMin = 4f, LifeMax = 7f,
                Gravity = 0f, NoiseStrength = 0.35f, NoiseFrequency = 0.15f,
                FadeInOut = true, GrowOverLife = 1.5f, MaxParticles = 1400,
            });
            _locusts = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "locusts", Material = VfxToolkit.FleckAlphaMaterial,
                ColorA = new Color(0.16f, 0.14f, 0.08f, 1f), ColorB = new Color(0.28f, 0.24f, 0.12f, 1f),
                SizeMin = 0.25f, SizeMax = 0.5f, LifeMin = 1.6f, LifeMax = 2.8f,
                Gravity = 0f, NoiseStrength = 4.5f, NoiseFrequency = 1.6f,
                FadeInOut = true, MaxParticles = 2600,
            });
            _rain = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "rain", Material = VfxToolkit.StreakMaterial,
                ColorA = new Color(0.65f, 0.72f, 0.82f, 0.45f), ColorB = new Color(0.55f, 0.62f, 0.72f, 0.35f),
                SizeMin = 0.35f, SizeMax = 0.55f, LifeMin = 0.8f, LifeMax = 1.2f,
                Gravity = 2.6f, Stretch = true, StretchLength = 9f,
                FadeInOut = true, MaxParticles = 1600,
            });
            _motes = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "bless-motes", Material = VfxToolkit.DotAdditiveMaterial,
                ColorA = new Color(1.0f, 0.85f, 0.4f, 0.8f), ColorB = new Color(0.95f, 0.75f, 0.3f, 0.7f),
                SizeMin = 0.25f, SizeMax = 0.6f, LifeMin = 2.5f, LifeMax = 4.5f,
                Gravity = -0.06f, NoiseStrength = 0.5f, NoiseFrequency = 0.4f,
                FadeInOut = true, MaxParticles = 700,
            });
            _glitter = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "gold-glitter", Material = VfxToolkit.DotAdditiveMaterial,
                ColorA = new Color(1f, 0.9f, 0.5f, 1f), ColorB = new Color(1f, 0.8f, 0.35f, 1f),
                SizeMin = 0.3f, SizeMax = 0.7f, LifeMin = 0.4f, LifeMax = 0.9f,
                Gravity = 0f, FadeInOut = true, MaxParticles = 200,
            });
            _snow = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "snow", Material = VfxToolkit.DotMaterial,
                ColorA = new Color(0.95f, 0.96f, 1f, 0.85f), ColorB = new Color(0.88f, 0.90f, 0.96f, 0.7f),
                SizeMin = 0.25f, SizeMax = 0.5f, LifeMin = 6f, LifeMax = 10f,
                Gravity = 0.16f, NoiseStrength = 1.1f, NoiseFrequency = 0.25f,
                FadeInOut = true, MaxParticles = 2200,
            });
            _dust = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "dust", Material = VfxToolkit.BillowMaterial,
                ColorA = new Color(0.62f, 0.55f, 0.42f, 0.4f), ColorB = new Color(0.55f, 0.48f, 0.38f, 0.3f),
                SizeMin = 1.4f, SizeMax = 3f, LifeMin = 1f, LifeMax = 2.2f,
                Gravity = -0.05f, NoiseStrength = 0.5f, NoiseFrequency = 0.5f,
                FadeInOut = true, GrowOverLife = 2.2f, MaxParticles = 900,
            });
            _sparks = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "sparks", Material = VfxToolkit.FleckMaterial,
                ColorA = new Color(1f, 0.95f, 0.75f, 1f), ColorB = new Color(1f, 0.8f, 0.4f, 1f),
                SizeMin = 0.15f, SizeMax = 0.3f, LifeMin = 0.3f, LifeMax = 0.7f,
                Gravity = 1.2f, FadeInOut = true, MaxParticles = 400,
            });
            _hearth = VfxToolkit.Create(t, new VfxToolkit.SystemSpec
            {
                Name = "hearth-smoke", Material = VfxToolkit.BillowMaterial,
                ColorA = new Color(0.72f, 0.70f, 0.66f, 0.28f), ColorB = new Color(0.60f, 0.58f, 0.55f, 0.22f),
                SizeMin = 0.5f, SizeMax = 0.9f, LifeMin = 2.5f, LifeMax = 4f,
                Gravity = -0.16f, NoiseStrength = 0.2f, NoiseFrequency = 0.4f,
                FadeInOut = true, GrowOverLife = 2.2f, MaxParticles = 600,
            });

            _fireLights = new Light[6];
            for (int i = 0; i < _fireLights.Length; i++)
            {
                var go = new GameObject("fire-light-" + i);
                go.transform.SetParent(t, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.55f, 0.22f);
                l.range = 30f;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                _fireLights[i] = l;
            }

            runner.EventLogged += OnEvent;
            runner.WorldRebuilt += _ => _smolders.Clear();
        }

        private void OnEvent(ChronicleEvent e)
        {
            if (_runner.BurstMode) return;
            Vector3 pos = TerrainSampler.CellToWorld(e.X, e.Y);
            pos.y = Mathf.Max(0.5f, TerrainSampler.GroundY(pos.x, pos.z));

            switch (e.Type)
            {
                case "conquest":
                case "defended":
                    ClashBurst(pos);
                    break;
                case "sacked":
                case "razed":
                    ClashBurst(pos);
                    _smolders.Add(new Smolder { Pos = pos, UntilYear = e.Year + 3 });
                    if (_smolders.Count > 12) _smolders.RemoveAt(0);
                    break;
                case "earthquake":
                    QuakeBurst(pos);
                    break;
            }
        }

        private void ClashBurst(Vector3 pos)
        {
            VfxToolkit.Emit(_dust, pos + Vector3.up * 1.2f, 26, 5f, new Vector3(0, 2.2f, 0));
            for (int i = 0; i < 30; i++)
            {
                var v = new Vector3(Random.Range(-4f, 4f), Random.Range(2f, 7f), Random.Range(-4f, 4f));
                VfxToolkit.Emit(_sparks, pos + Vector3.up * 1.5f, 1, 1.5f, v);
            }
        }

        private void QuakeBurst(Vector3 center)
        {
            for (int i = 0; i < 40; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(0f, 6f) * TerrainSampler.CellSize;
                var p = center + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                p.y = TerrainSampler.GroundY(p.x, p.z) + 0.5f;
                if (p.y < 0.3f) continue;
                VfxToolkit.Emit(_dust, p, 3, 2f, new Vector3(0, Random.Range(1.5f, 4f), 0));
            }
        }

        // Probabilistic per-frame emission so rates stay framerate-independent.
        private static bool Roll(float ratePerSecond)
            => Random.value < ratePerSecond * Time.deltaTime;

        private void Update()
        {
            var w = _runner?.World;
            if (w == null) return;
            float cell = TerrainSampler.CellSize;

            // --- Wildfire: flames lick, embers rise, the plume leans east,
            // and the fire throws real flickering light on its surroundings.
            int fireBudget = 0;
            foreach (int i in w.BurningCells)
            {
                if (++fireBudget > 90) break;
                int cx = i % w.W, cy = i / w.W;
                var p = TerrainSampler.CellToWorld(cx, cy);
                p.y = TerrainSampler.GroundY(p.x, p.z);
                if (Roll(11f)) VfxToolkit.Emit(_flames, p + Vector3.up * 0.6f, 1, cell * 0.42f, new Vector3(0, Random.Range(2f, 4f), 0));
                if (Roll(3f)) VfxToolkit.Emit(_embers, p + Vector3.up * 2f, 1, cell * 0.4f, new Vector3(Random.Range(-1f, 2f), Random.Range(3f, 7f), Random.Range(-1f, 1f)));
                if (Roll(2.2f)) VfxToolkit.Emit(_smoke, p + Vector3.up * 4f, 1, cell * 0.35f, new Vector3(2.2f, 3.5f, 0.4f)); // the plume leans with the wind
            }
            int lightsUsed = 0;
            if (w.BurningCells.Count > 0)
            {
                int stride = Mathf.Max(1, w.BurningCells.Count / _fireLights.Length);
                for (int k = 0; k < w.BurningCells.Count && lightsUsed < _fireLights.Length; k += stride)
                {
                    int i = w.BurningCells[k];
                    var p = TerrainSampler.CellToWorld(i % w.W, i / w.W);
                    p.y = TerrainSampler.GroundY(p.x, p.z) + 5f;
                    var l = _fireLights[lightsUsed++];
                    l.transform.position = p;
                    l.intensity = 3.2f + Mathf.PerlinNoise(Time.time * 7f, i * 0.13f) * 3.5f;
                    l.enabled = true;
                }
            }
            for (int k = lightsUsed; k < _fireLights.Length; k++) _fireLights[k].enabled = false;

            // --- Plague: ground miasma + thin pyre smoke in every sick town.
            if (w.PlagueActive)
            {
                foreach (var s in w.Settlements)
                {
                    if (s.Ruined || s.PlagueState != 1) continue;
                    var c = TerrainSampler.CellToWorld(s.X, s.Y);
                    c.y = TerrainSampler.GroundY(c.x, c.z);
                    float r = cell * (0.9f + 0.4f * s.Tier);
                    if (Roll(14f))
                    {
                        var offset = new Vector3(Random.Range(-r, r), 1.5f, Random.Range(-r, r));
                        VfxToolkit.Emit(_miasma, c + offset, 1, 1f, new Vector3(Random.Range(-0.3f, 0.3f), 0.05f, Random.Range(-0.3f, 0.3f)));
                    }
                    if (Roll(6f))
                    {
                        // A couple of pyres burn at fixed spots around the town.
                        float ang = ((s.Id * 2.399f) + (Roll(0.5f) ? 3.1f : 0f)) % (Mathf.PI * 2);
                        var pyrePos = c + new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r);
                        pyrePos.y = TerrainSampler.GroundY(pyrePos.x, pyrePos.z) + 1f;
                        VfxToolkit.Emit(_pyre, pyrePos, 1, 0.3f, new Vector3(0.3f, 2.4f, 0.1f));
                    }
                }
            }

            // --- Locusts: an actual buzzing swarm above its ground shadow.
            foreach (var cLoc in w.Locusts)
            {
                var p = TerrainSampler.CellToWorld(cLoc.X, cLoc.Y);
                p.y = Mathf.Max(2f, TerrainSampler.GroundY(p.x, p.z)) + 4f;
                int n = Mathf.Min(14, Mathf.CeilToInt(90f * Time.deltaTime));
                VfxToolkit.Emit(_locusts, p, n, 3f * cell,
                    new Vector3((float)cLoc.Dx * 2f, 0, (float)cLoc.Dy * 2f));
            }

            // --- Storms rain; blessings drift gold.
            foreach (var z in w.Storms)
            {
                var c = TerrainSampler.CellToWorld(z.X, z.Y);
                float r = (float)z.R * cell;
                int n = Mathf.Min(10, Mathf.CeilToInt(70f * Time.deltaTime));
                for (int k = 0; k < n; k++)
                {
                    var p = c + new Vector3(Random.insideUnitCircle.x * r, 0, Random.insideUnitCircle.y * r);
                    p.y = TerrainSampler.GroundY(p.x, p.z) + Random.Range(18f, 30f);
                    VfxToolkit.Emit(_rain, p, 1, 0f, new Vector3(0.6f, -14f, 0.2f));
                }
            }
            foreach (var z in w.Blesses)
            {
                if (!Roll(8f)) continue;
                var c = TerrainSampler.CellToWorld(z.X, z.Y);
                float r = (float)z.R * cell * 0.9f;
                var p = c + new Vector3(Random.Range(-r, r), 0, Random.Range(-r, r));
                p.y = TerrainSampler.GroundY(p.x, p.z) + Random.Range(0.5f, 2f);
                if (p.y > 0.3f) VfxToolkit.Emit(_motes, p, 1, 0.5f, new Vector3(0, 0.5f, 0));
            }

            // --- Divine gold glitters through the rock.
            foreach (var v in w.Veins)
            {
                if (!v.Divine || !v.Revealed || !Roll(1.4f)) continue;
                var p = TerrainSampler.CellToWorld(v.X, v.Y);
                p.y = TerrainSampler.GroundY(p.x, p.z) + 1f;
                VfxToolkit.Emit(_glitter, p, 2, 2.5f, new Vector3(0, 0.8f, 0));
            }

            // --- Winter: snow falls around wherever you're looking.
            if ((w.Month >= 8 && w.Month <= 11) && _rig != null)
            {
                var (fx, fy) = TerrainSampler.WorldToCell(_rig.Focus);
                float cold = Mathf.Clamp01((0.52f - w.Temp[fy * w.W + fx]) * 3f);
                float strength = (w.Month == 9 ? 1f : w.Month == 10 ? 0.8f : 0.35f) * cold;
                if (strength > 0.05f)
                {
                    float span = Mathf.Min(420f, _rig.Height * 1.6f + 80f);
                    int n = Mathf.Min(24, Mathf.CeilToInt(160f * strength * Time.deltaTime));
                    for (int k = 0; k < n; k++)
                    {
                        var p = _rig.Focus + new Vector3(Random.Range(-span, span), 0, Random.Range(-span, span));
                        p.y = Mathf.Max(0f, TerrainSampler.GroundY(p.x, p.z)) + Random.Range(25f, 50f);
                        VfxToolkit.Emit(_snow, p, 1, 0f, new Vector3(Random.Range(-0.5f, 0.5f), -2f, Random.Range(-0.5f, 0.5f)));
                    }
                }
            }

            // --- Sacked towns smolder for years.
            for (int i = _smolders.Count - 1; i >= 0; i--)
            {
                if (w.Year > _smolders[i].UntilYear) { _smolders.RemoveAt(i); continue; }
                if (Roll(3f))
                    VfxToolkit.Emit(_smoke, _smolders[i].Pos + Vector3.up * 2f, 1, 4f, new Vector3(1.2f, 2.4f, 0.2f));
            }

            // --- Dragon lairs breathe a thin dark curl.
            foreach (var d in w.Dragons)
            {
                if (!Roll(2f)) continue;
                var p = TerrainSampler.CellToWorld(d.X, d.Y);
                p.y = TerrainSampler.GroundY(p.x, p.z) + 2f;
                VfxToolkit.Emit(_pyre, p, 1, 1f, new Vector3(0.2f, 1.8f, 0.2f));
            }

            // --- Hearth smoke: chimneys breathe in the settlements you're near.
            if (_rig != null && _rig.Height < 240f)
            {
                int budget = 0;
                foreach (var s in w.Settlements)
                {
                    if (s.Ruined || s.PlagueState == 1) continue;
                    var c = TerrainSampler.CellToWorld(s.X, s.Y);
                    if ((c - _rig.Focus).sqrMagnitude > 300f * 300f) continue;
                    if (++budget > 10) break;
                    if (!Roll(3f)) continue;
                    // A few fixed hearths per settlement, hash-placed among the houses.
                    int hearthIdx = Random.Range(0, 3);
                    float ang = (float)(SlateRng.Hash2(s.Id, hearthIdx, 0xCAFE) * Mathf.PI * 2);
                    float dist = TerrainSampler.CellSize * (0.25f + 0.5f * (float)SlateRng.Hash2(hearthIdx, s.Id, 0xBEE));
                    var p = c + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                    p.y = TerrainSampler.GroundY(p.x, p.z) + 2.6f;
                    if (p.y < 2.6f) continue;
                    VfxToolkit.Emit(_hearth, p, 1, 0.2f, new Vector3(0.25f, 1.4f, 0.1f));
                }
            }

            // --- Marching armies raise dust.
            foreach (var a in w.Armies)
            {
                if (!Roll(6f)) continue;
                var p = TerrainSampler.CellToWorld(a.X, a.Y);
                p.y = TerrainSampler.GroundY(p.x, p.z) + 0.5f;
                if (p.y > 0.3f) VfxToolkit.Emit(_dust, p - new Vector3(0, 0, 2f), 1, 3f, new Vector3(0, 1.2f, -0.5f));
            }
        }
    }
}
