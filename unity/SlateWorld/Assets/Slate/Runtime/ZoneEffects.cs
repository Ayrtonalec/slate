// SLATE — divine weather, blessings, dragon menace and event pings, drawn as
// soft discs and rings hovering just above the terrain. The visible trace of
// the invisible: where a god or a wyrm is at work, the land shows it.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class ZoneEffects : MonoBehaviour
    {
        private WorldRunner _runner;
        private Mesh _disc;
        private Material _storm, _bless, _lair, _menace, _pingGod, _pingDoom, _pingPlain;

        private class Ping
        {
            public Vector3 Pos;
            public float Born;
            public Material Mat;
        }

        private readonly List<Ping> _pings = new List<Ping>();

        public void Init(WorldRunner runner)
        {
            _runner = runner;
            _disc = ProceduralMeshes.Disc();
            _storm = MakeZone(new Color(Palette.StormGray.r, Palette.StormGray.g, Palette.StormGray.b, 0.45f), ring: 0f);
            _bless = MakeZone(new Color(Palette.DivineGold.r, Palette.DivineGold.g, Palette.DivineGold.b, 0.28f), ring: 0f);
            _lair = MakeZone(new Color(0.16f, 0.10f, 0.09f, 0.55f), ring: 0f);
            _menace = MakeZone(new Color(Palette.DoomBlood.r, Palette.DoomBlood.g, Palette.DoomBlood.b, 0.40f), ring: 1f);
            _pingGod = MakeZone(new Color(Palette.DivineGold.r, Palette.DivineGold.g, Palette.DivineGold.b, 0.9f), ring: 1f);
            _pingDoom = MakeZone(new Color(Palette.DoomBlood.r, Palette.DoomBlood.g, Palette.DoomBlood.b, 0.9f), ring: 1f);
            _pingPlain = MakeZone(new Color(0.95f, 0.92f, 0.80f, 0.8f), ring: 1f);

            runner.EventLogged += OnEvent;
            runner.WorldRebuilt += _ => _pings.Clear();
        }

        private static Material MakeZone(Color c, float ring)
        {
            var m = new Material(Shader.Find("Slate/Zone"));
            m.SetColor("_Color", c);
            m.SetFloat("_Ring", ring);
            m.SetFloat("_RingWidth", 0.10f);
            return m;
        }

        private void OnEvent(ChronicleEvent e)
        {
            if (_runner != null && _runner.BurstMode) return; // no ping storm on fast-forward
            // Rings only for real drama: every god/doom act, plus major plain events.
            if (e.Imp < 3 && e.Tone == "plain") return;
            Vector3 pos = TerrainSampler.CellToWorld(e.X, e.Y);
            pos.y = Mathf.Max(1.2f, TerrainSampler.GroundY(pos.x, pos.z) + 1.0f);
            var mat = e.Tone == "god" ? _pingGod : e.Tone == "doom" ? _pingDoom : _pingPlain;
            _pings.Add(new Ping { Pos = pos, Born = Time.time, Mat = mat });
            if (_pings.Count > 24) _pings.RemoveAt(0);
        }

        private void Update()
        {
            var w = _runner?.World;
            if (w == null) return;
            float cell = TerrainSampler.CellSize;
            float pulse = 0.9f + Mathf.Sin(Time.time * 2.2f) * 0.1f;

            foreach (var z in w.Storms) DrawZone(_storm, z.X, z.Y, (float)z.R * cell * 1.05f);
            foreach (var z in w.Blesses) DrawZone(_bless, z.X, z.Y, (float)z.R * cell * 1.05f);
            foreach (var d in w.Dragons)
            {
                DrawZone(_lair, d.X, d.Y, cell * 1.4f);
                DrawZone(_menace, d.X, d.Y, 8f * cell * pulse); // the terror radius, breathing
            }

            // Expanding, fading rings for notable chronicle events.
            float now = Time.time;
            for (int i = _pings.Count - 1; i >= 0; i--)
            {
                var p = _pings[i];
                float age = now - p.Born;
                if (age > 2.2f) { _pings.RemoveAt(i); continue; }
                float r = 6f + age * 42f;
                var rp = new RenderParams(p.Mat)
                {
                    worldBounds = new Bounds(p.Pos, Vector3.one * (r * 2 + 10)),
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                Graphics.RenderMesh(rp, _disc, 0, Matrix4x4.TRS(p.Pos, Quaternion.identity, new Vector3(r, 1, r)));
            }
        }

        private void DrawZone(Material mat, double cx, double cy, float radius)
        {
            Vector3 pos = TerrainSampler.CellToWorld(cx, cy);
            pos.y = Mathf.Max(1.0f, TerrainSampler.GroundY(pos.x, pos.z) + 0.8f);
            var rp = new RenderParams(mat)
            {
                worldBounds = new Bounds(pos, Vector3.one * (radius * 2 + 10)),
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMesh(rp, _disc, 0, Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(radius, 1, radius)));
        }
    }
}
