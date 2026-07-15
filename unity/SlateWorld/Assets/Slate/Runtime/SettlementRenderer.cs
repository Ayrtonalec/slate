// SLATE — settlements as clustered procedural architecture, GPU-instanced.
// No two look alike: each has its own footprint (stretched along its own
// axis), density, and roof shade within its culture's color. Defenses are
// drawn from the sim's Walls field — a timber palisade if the town paid for
// one, dressed stone if it paid for more, nothing if it never had to. The
// Theater Principle: the sim knows "pop 1834, walls 1"; this performs it.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class SettlementRenderer : MonoBehaviour
    {
        private WorldRunner _runner;
        private Mesh _body, _roof, _tower, _wall;
        private Material _plasterMat, _wallMat, _palisadeMat, _ruinMat;
        private Material[] _roofMats; // 4 cultures x 3 shades

        private readonly List<Matrix4x4> _bodies = new List<Matrix4x4>();
        private readonly List<Matrix4x4>[] _roofs = new List<Matrix4x4>[12];
        private readonly List<Matrix4x4> _walls = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _palisades = new List<Matrix4x4>();
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
            _palisadeMat = MakeLit(new Color(0.34f, 0.26f, 0.18f)); // weathered timber
            _ruinMat = MakeLit(Palette.RuinChar);
            _roofMats = new Material[12];
            float[] shades = { 0.82f, 1.0f, 1.18f };
            for (int c = 0; c < 4; c++)
                for (int k = 0; k < 3; k++)
                {
                    _roofs[c * 3 + k] = new List<Matrix4x4>();
                    _roofMats[c * 3 + k] = MakeLit(Palette.CultureColor(c) * shades[k] * 1.05f);
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
                sig = sig * 31 + s.Culture;         // conquest re-tints the roofs
                sig = sig * 31 + s.Walls;           // defenses appear when paid for
                sig = sig * 31 + (long)(s.Pop / 130); // house count buckets
            }
            if (sig != _signature) { _signature = sig; Rebuild(w); }

            Draw();
        }

        private void Rebuild(World w)
        {
            _bodies.Clear(); _walls.Clear(); _palisades.Clear(); _ruins.Clear();
            for (int i = 0; i < 12; i++) _roofs[i].Clear();

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

            // --- Personality: every place has its own shape.
            float elong = 1.05f + (float)rnd.NextDouble() * 0.45f;   // stretched footprint
            float density = 0.75f + (float)rnd.NextDouble() * 0.5f;  // tight or sprawling
            int shade = rnd.Next(3);                                  // roof shade in the culture color
            float axisDeg = (float)(rnd.NextDouble() * 180);
            // Towns on the water stretch along their shore.
            var shoreDir = ShoreDirection(w, s);
            if (shoreDir.HasValue) axisDeg = shoreDir.Value + 90f;
            var axis = Quaternion.Euler(0, axisDeg, 0);

            float radius = TerrainSampler.CellSize * (0.40f + 0.34f * s.Tier)
                + Mathf.Min(6f, (float)s.Pop / 900f);
            int houses = Mathf.Max(2, (int)((2 + Mathf.Min(52, (int)(s.Pop / 70))) * density));
            var roofList = _roofs[s.Culture * 3 + shade];

            for (int i = 0; i < houses; i++)
            {
                Vector3 pos = Vector3.zero;
                bool ok = false;
                for (int attempt = 0; attempt < 4 && !ok; attempt++)
                {
                    float ang = (float)(rnd.NextDouble() * Mathf.PI * 2);
                    float dist = radius * Mathf.Sqrt((float)rnd.NextDouble());
                    pos = center + axis * new Vector3(Mathf.Cos(ang) * dist * elong, 0, Mathf.Sin(ang) * dist * 0.85f);
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

            // The hall: one big roof at the center from village up. Rich towns build bigger.
            if (s.Tier >= 1)
            {
                Vector3 pos = center; pos.y = TerrainSampler.GroundY(pos.x, pos.z);
                if (pos.y > 0.35f)
                {
                    var rot = Quaternion.Euler(0, axisDeg, 0);
                    float wealthBonus = Mathf.Min(1.2f, (float)s.Wealth / 60f);
                    float hw = 3.2f + s.Tier * 0.6f + wealthBonus, hh = 2.5f + s.Tier * 0.5f + wealthBonus * 0.5f;
                    _bodies.Add(Matrix4x4.TRS(pos, rot, new Vector3(hw, hh, hw * 1.25f)));
                    _roofs[s.Culture * 3 + shade].Add(Matrix4x4.TRS(pos + Vector3.up * hh, rot, new Vector3(hw, hh, hw * 1.25f)));
                }
            }

            // --- Defenses: only what the sim says was actually built and paid for.
            if (s.Walls >= 1)
            {
                float rx = radius * elong + 3.6f;
                float rz = radius * 0.85f + 3.6f;
                if (s.Walls == 1)
                {
                    // Timber palisade: a ring of posts.
                    float circ = Mathf.PI * (rx + rz);
                    int posts = Mathf.Max(20, (int)(circ / 1.15f));
                    for (int i = 0; i < posts; i++)
                    {
                        float ang = i * Mathf.PI * 2 / posts;
                        Vector3 pos = center + axis * new Vector3(Mathf.Cos(ang) * rx, 0, Mathf.Sin(ang) * rz);
                        pos.y = TerrainSampler.GroundY(pos.x, pos.z);
                        if (pos.y < 0.3f) continue;
                        float h = 2.0f + (float)SlateRng.Hash2(s.Id, i, 0xFAB) * 0.5f;
                        _palisades.Add(Matrix4x4.TRS(pos + Vector3.up * h * 0.5f, Quaternion.identity, new Vector3(0.38f, h, 0.38f)));
                    }
                }
                else
                {
                    // Dressed stone: slabs and towers.
                    float circ = Mathf.PI * (rx + rz);
                    int segs = Mathf.Max(18, (int)(circ / 5.2f));
                    float wallH = s.Tier >= 3 ? 3.6f : 2.6f;
                    for (int i = 0; i < segs; i++)
                    {
                        float ang = i * Mathf.PI * 2 / segs;
                        Vector3 pos = center + axis * new Vector3(Mathf.Cos(ang) * rx, 0, Mathf.Sin(ang) * rz);
                        pos.y = TerrainSampler.GroundY(pos.x, pos.z);
                        if (pos.y < 0.3f) continue;
                        Vector3 tangent = axis * new Vector3(-Mathf.Sin(ang) * rx, 0, Mathf.Cos(ang) * rz);
                        var rot = Quaternion.LookRotation(tangent.normalized, Vector3.up) * Quaternion.Euler(0, 90, 0);
                        _walls.Add(Matrix4x4.TRS(pos + Vector3.up * wallH * 0.5f, rot, new Vector3(circ / segs * 1.08f, wallH, 0.9f)));
                    }
                    int towers = s.Tier >= 3 ? 6 : 4;
                    for (int i = 0; i < towers; i++)
                    {
                        float ang = i * Mathf.PI * 2 / towers + 0.35f;
                        Vector3 pos = center + axis * new Vector3(Mathf.Cos(ang) * rx, 0, Mathf.Sin(ang) * rz);
                        pos.y = TerrainSampler.GroundY(pos.x, pos.z);
                        if (pos.y < 0.3f) continue;
                        float th = s.Tier >= 3 ? 7.5f : 5.5f;
                        _walls.Add(Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(2.6f, th, 2.6f)));
                    }
                }
            }
        }

        // Direction (yaw degrees) from the settlement toward nearby open water, if any.
        private float? ShoreDirection(World w, Settlement s)
        {
            for (int r = 1; r <= 3; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int nx = s.X + dx, ny = s.Y + dy;
                        if (!w.InB(nx, ny) || w.HeightMap[ny * w.W + nx] > w.Sea) continue;
                        return Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                    }
            return null;
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
            for (int i = 0; i < 12; i++) DrawGroup(_roof, _roofMats[i], _roofs[i]);
            DrawGroup(_wall, _wallMat, _walls);
            DrawGroup(_wall, _palisadeMat, _palisades);
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
