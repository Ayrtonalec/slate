// SLATE — settlements as clustered procedural architecture, GPU-instanced.
// A hamlet is a handful of huts; a village gains a hall; a town raises walls
// and a tower; a city gets a taller ring and more towers. Roof color is the
// culture's banner color, so the map reads politically at a glance — the
// Theater Principle: the sim only knows "pop 1834, tier 2"; this performs it.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class SettlementRenderer : MonoBehaviour
    {
        private WorldRunner _runner;
        private Mesh _body, _roof, _tower, _wall;
        private Material _plasterMat, _wallMat, _ruinMat;
        private Material[] _roofMats;

        private readonly List<Matrix4x4> _bodies = new List<Matrix4x4>();
        private readonly List<Matrix4x4>[] _roofs = new List<Matrix4x4>[4];
        private readonly List<Matrix4x4> _walls = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _ruins = new List<Matrix4x4>();
        private long _signature = -1;

        public void Init(WorldRunner runner)
        {
            _runner = runner;
            _body = ProceduralMeshes.HouseBody();
            _roof = ProceduralMeshes.GableRoof();
            _tower = ProceduralMeshes.Tower();
            _wall = ProceduralMeshes.Box();

            _plasterMat = MakeLit(Palette.PlasterWarm);
            _wallMat = MakeLit(Palette.WallStone);
            _ruinMat = MakeLit(Palette.RuinChar);
            _roofMats = new Material[4];
            for (int c = 0; c < 4; c++)
            {
                _roofs[c] = new List<Matrix4x4>();
                _roofMats[c] = MakeLit(Palette.CultureColor(c) * 1.05f);
            }
        }

        public static Material MakeLit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.08f);
            m.enableInstancing = true;
            return m;
        }

        private void Update()
        {
            var w = _runner?.World;
            if (w == null) return;

            // Rebuild instance lists only when the built world meaningfully changed.
            long sig = w.Ruins.Count * 1000003L;
            foreach (var s in w.Settlements)
            {
                if (s.Ruined) continue;
                sig = sig * 31 + s.Id;
                sig = sig * 31 + s.Tier;
                sig = sig * 31 + (long)(s.Pop / 130); // house count buckets
            }
            if (sig != _signature) { _signature = sig; Rebuild(w); }

            Draw();
        }

        private void Rebuild(World w)
        {
            _bodies.Clear(); _walls.Clear(); _ruins.Clear();
            for (int c = 0; c < 4; c++) _roofs[c].Clear();

            foreach (var s in w.Settlements)
            {
                if (s.Ruined) continue;
                BuildSettlement(w, s);
            }
            foreach (var r in w.Ruins) BuildRuin(w, r);
        }

        private void BuildSettlement(World w, Settlement s)
        {
            var rnd = new System.Random(unchecked(w.Seed * 486187739 + s.Id * 1000003));
            Vector3 center = TerrainSampler.CellToWorld(s.X, s.Y);
            float radius = TerrainSampler.CellSize * (0.40f + 0.34f * s.Tier)
                + Mathf.Min(6f, (float)s.Pop / 900f);
            int houses = 2 + Mathf.Min(52, (int)(s.Pop / 70));
            var roofList = _roofs[s.Culture];

            for (int i = 0; i < houses; i++)
            {
                Vector3 pos = Vector3.zero;
                bool ok = false;
                for (int attempt = 0; attempt < 4 && !ok; attempt++)
                {
                    float ang = (float)(rnd.NextDouble() * Mathf.PI * 2);
                    float dist = radius * Mathf.Sqrt((float)rnd.NextDouble());
                    pos = center + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                    pos.y = TerrainSampler.GroundY(pos.x, pos.z);
                    ok = pos.y > 0.35f;
                }
                if (!ok) continue;

                float sw = 1.7f + (float)rnd.NextDouble() * 1.2f;
                float sh = 1.5f + (float)rnd.NextDouble() * 0.8f + s.Tier * 0.15f;
                float sd = 1.7f + (float)rnd.NextDouble() * 1.2f;
                var rot = Quaternion.Euler(0, (float)(rnd.NextDouble() * 360), 0);
                _bodies.Add(Matrix4x4.TRS(pos, rot, new Vector3(sw, sh, sd)));
                roofList.Add(Matrix4x4.TRS(pos + Vector3.up * sh, rot, new Vector3(sw, sh * 0.85f, sd)));
            }

            // The hall: one big roof at the center from village up.
            if (s.Tier >= 1)
            {
                Vector3 pos = center; pos.y = TerrainSampler.GroundY(pos.x, pos.z);
                if (pos.y > 0.35f)
                {
                    var rot = Quaternion.Euler(0, (float)(rnd.NextDouble() * 360), 0);
                    float hw = 3.4f + s.Tier * 0.6f, hh = 2.6f + s.Tier * 0.5f;
                    _bodies.Add(Matrix4x4.TRS(pos, rot, new Vector3(hw, hh, hw * 1.25f)));
                    _roofs[s.Culture].Add(Matrix4x4.TRS(pos + Vector3.up * hh, rot, new Vector3(hw, hh, hw * 1.25f)));
                }
            }

            // Walls and towers for towns and cities.
            if (s.Tier >= 2)
            {
                float rw = radius + 3.8f;
                int segs = s.Tier == 3 ? 30 : 22;
                float wallH = s.Tier == 3 ? 3.4f : 2.4f;
                float arc = 2f * Mathf.PI * rw / segs;
                for (int i = 0; i < segs; i++)
                {
                    float ang = i * Mathf.PI * 2 / segs;
                    Vector3 pos = center + new Vector3(Mathf.Cos(ang) * rw, 0, Mathf.Sin(ang) * rw);
                    pos.y = TerrainSampler.GroundY(pos.x, pos.z);
                    if (pos.y < 0.3f) continue; // walls stop at the waterline
                    var rot = Quaternion.Euler(0, -ang * Mathf.Rad2Deg + 90f, 0);
                    _walls.Add(Matrix4x4.TRS(pos + Vector3.up * wallH * 0.5f, rot, new Vector3(arc * 1.06f, wallH, 0.9f)));
                }
                int towers = s.Tier == 3 ? 6 : 4;
                for (int i = 0; i < towers; i++)
                {
                    float ang = i * Mathf.PI * 2 / towers + 0.35f;
                    Vector3 pos = center + new Vector3(Mathf.Cos(ang) * rw, 0, Mathf.Sin(ang) * rw);
                    pos.y = TerrainSampler.GroundY(pos.x, pos.z);
                    if (pos.y < 0.3f) continue;
                    float th = s.Tier == 3 ? 7.5f : 5.5f;
                    _walls.Add(Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(2.6f, th, 2.6f)));
                }
            }
        }

        private void BuildRuin(World w, Ruin r)
        {
            var rnd = new System.Random(unchecked(w.Seed * 92821 + r.X * 53987 + r.Y * 71993));
            Vector3 center = TerrainSampler.CellToWorld(r.X, r.Y);
            int stones = 3 + r.Tier * 2;
            for (int i = 0; i < stones; i++)
            {
                float ang = (float)(rnd.NextDouble() * Mathf.PI * 2);
                float dist = 2.5f + (float)rnd.NextDouble() * 4f;
                Vector3 pos = center + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                pos.y = TerrainSampler.GroundY(pos.x, pos.z);
                if (pos.y < 0.3f) continue;
                var rot = Quaternion.Euler((float)rnd.NextDouble() * 14f, (float)(rnd.NextDouble() * 360), 0);
                float sc = 0.8f + (float)rnd.NextDouble() * 1.4f;
                _ruins.Add(Matrix4x4.TRS(pos + Vector3.up * 0.3f, rot, new Vector3(sc, 0.6f + (float)rnd.NextDouble() * 0.9f, sc)));
            }
        }

        private void Draw()
        {
            DrawGroup(_body, _plasterMat, _bodies);
            for (int c = 0; c < 4; c++) DrawGroup(_roof, _roofMats[c], _roofs[c]);
            DrawGroup(_wall, _wallMat, _walls);
            DrawGroup(_wall, _ruinMat, _ruins);
        }

        public static void DrawGroup(Mesh mesh, Material mat, List<Matrix4x4> matrices)
        {
            if (matrices.Count == 0) return;
            var rp = new RenderParams(mat)
            {
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows = true,
                worldBounds = new Bounds(new Vector3(768, 0, 480), new Vector3(4000, 600, 4000)),
            };
            for (int i = 0; i < matrices.Count; i += 1023)
            {
                int count = Mathf.Min(1023, matrices.Count - i);
                Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, count, i);
            }
        }
    }
}
