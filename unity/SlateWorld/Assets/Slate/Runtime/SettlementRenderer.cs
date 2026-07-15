// SLATE — settlements built from Architecture Kit 2.0: every culture builds
// like itself (thatch, longhouses, adobe domes, terracotta villas), towns
// keep their earned walls, and cities raise crenellated keeps. Politics now
// reads from culture-colored banners on halls and keeps (roofs are honest
// materials). Everything GPU-instanced; the Theater Principle throughout.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class SettlementRenderer : MonoBehaviour
    {
        private WorldRunner _runner;
        private Mesh[][] _houses;         // [culture][variant], 2 submeshes each
        private Mesh[] _halls, _keeps;    // [culture]
        private Mesh _wall, _flag;
        private Material[] _wallsMats, _accentMats, _flagMats;
        private Material _stoneMat, _palisadeMat, _ruinMat, _poleMat;

        private readonly List<Matrix4x4>[][] _houseGroups = new List<Matrix4x4>[4][];
        private readonly List<Matrix4x4>[] _hallGroups = new List<Matrix4x4>[4];
        private readonly List<Matrix4x4>[] _keepGroups = new List<Matrix4x4>[4];
        private readonly List<Matrix4x4> _walls = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _palisades = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _ruins = new List<Matrix4x4>();
        private readonly List<(Vector3 pos, int culture)> _banners = new List<(Vector3, int)>();
        private long _signature = -1;

        private static readonly string[] CultureFolder = { "aldish", "vasker", "serai", "tessian" };
        private Material _trimMat;

        // Blender-built model if present (tools/blender/gen_houses.py exports
        // into Resources/Models); the in-code ArchitectureKit is the fallback,
        // so the project always runs even without generated assets.
        private static Mesh LoadModel(string path)
        {
            var go = Resources.Load<GameObject>(path);
            if (go == null) return null;
            var mf = go.GetComponentInChildren<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        public void Init(WorldRunner runner)
        {
            _runner = runner;
            _wall = ProceduralMeshes.Box();
            _flag = ProceduralMeshes.FlagCloth();
            _houses = new Mesh[4][];
            _halls = new Mesh[4];
            _keeps = new Mesh[4];
            _wallsMats = new Material[4];
            _accentMats = new Material[4];
            _flagMats = new Material[4];
            _trimMat = MakeLit(new Color(0.20f, 0.16f, 0.12f)); // dark timber & shadowed openings
            for (int c = 0; c < 4; c++)
            {
                _houses[c] = ArchitectureKit.Houses(c);
                for (int vIdx = 0; vIdx < 3; vIdx++)
                {
                    var m = LoadModel($"Models/houses/{CultureFolder[c]}_v{vIdx}");
                    if (m != null) _houses[c][vIdx] = m;
                }
                _halls[c] = ArchitectureKit.Hall(c);
                _keeps[c] = ArchitectureKit.Keep(c);
                _wallsMats[c] = MakeLit(ArchitectureKit.WallsColor(c));
                _accentMats[c] = MakeLit(ArchitectureKit.AccentColor(c));
                _flagMats[c] = new Material(Shader.Find("Slate/Flag"));
                _flagMats[c].SetColor("_BaseColor", Palette.CultureColor(c) * 1.35f);
                _houseGroups[c] = new List<Matrix4x4>[3];
                for (int vIdx = 0; vIdx < 3; vIdx++) _houseGroups[c][vIdx] = new List<Matrix4x4>();
                _hallGroups[c] = new List<Matrix4x4>();
                _keepGroups[c] = new List<Matrix4x4>();
            }
            _stoneMat = MakeLit(Palette.WallStone);
            _palisadeMat = MakeLit(new Color(0.34f, 0.26f, 0.18f));
            _ruinMat = MakeLit(Palette.RuinChar);
            _poleMat = MakeLit(new Color(0.30f, 0.24f, 0.18f));
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

            long sig = w.Ruins.Count * 1000003L;
            foreach (var s in w.Settlements)
            {
                if (s.Ruined) continue;
                sig = sig * 31 + s.Id;
                sig = sig * 31 + s.Tier;
                sig = sig * 31 + s.Culture;
                sig = sig * 31 + s.Walls;
                sig = sig * 31 + (long)(s.Pop / 130);
            }
            if (sig != _signature) { _signature = sig; Rebuild(w); }

            Draw();
        }

        private void Rebuild(World w)
        {
            for (int c = 0; c < 4; c++)
            {
                for (int vIdx = 0; vIdx < 3; vIdx++) _houseGroups[c][vIdx].Clear();
                _hallGroups[c].Clear();
                _keepGroups[c].Clear();
            }
            _walls.Clear(); _palisades.Clear(); _ruins.Clear(); _banners.Clear();

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
            int culture = Mathf.Clamp(s.Culture, 0, 3);

            float elong = 1.05f + (float)rnd.NextDouble() * 0.45f;
            float density = 0.75f + (float)rnd.NextDouble() * 0.5f;
            float axisDeg = (float)(rnd.NextDouble() * 180);
            var shoreDir = ShoreDirection(w, s);
            if (shoreDir.HasValue) axisDeg = shoreDir.Value + 90f;
            var axis = Quaternion.Euler(0, axisDeg, 0);

            float radius = TerrainSampler.CellSize * (0.40f + 0.34f * s.Tier)
                + Mathf.Min(6f, (float)s.Pop / 900f);
            int houses = Mathf.Max(2, (int)((2 + Mathf.Min(52, (int)(s.Pop / 70))) * density));

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

                // Variant mix: mostly the common house, some larger kinds.
                double roll = rnd.NextDouble();
                int variant = roll < 0.6 ? 0 : roll < 0.85 ? 1 : 2;
                float sc = 2.1f + (float)rnd.NextDouble() * 0.9f + s.Tier * 0.15f;
                var rot = Quaternion.Euler(0, (float)(rnd.NextDouble() * 360), 0);
                _houseGroups[culture][variant].Add(Matrix4x4.TRS(pos, rot, new Vector3(sc, sc * (0.92f + (float)rnd.NextDouble() * 0.16f), sc)));
            }

            // The center: a hall from village up, a crenellated keep for cities.
            Vector3 c0 = center; c0.y = TerrainSampler.GroundY(c0.x, c0.z);
            if (c0.y > 0.35f)
            {
                var rot = Quaternion.Euler(0, axisDeg, 0);
                if (s.Tier >= 3)
                {
                    float k = 3.4f + Mathf.Min(1.2f, (float)s.Wealth / 80f);
                    _keepGroups[culture].Add(Matrix4x4.TRS(c0, rot, new Vector3(k, k, k)));
                    _banners.Add((c0 + Vector3.up * (k * 2.9f), culture));
                }
                else if (s.Tier >= 1)
                {
                    float h = 2.6f + s.Tier * 0.5f + Mathf.Min(1.0f, (float)s.Wealth / 60f);
                    _hallGroups[culture].Add(Matrix4x4.TRS(c0, rot, new Vector3(h, h, h)));
                    _banners.Add((c0 + Vector3.up * (h * 1.6f), culture));
                }
            }

            // Defenses: what the sim says was built and paid for.
            if (s.Walls >= 1)
            {
                float rx = radius * elong + 3.6f;
                float rz = radius * 0.85f + 3.6f;
                if (s.Walls == 1)
                {
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
            for (int c = 0; c < 4; c++)
            {
                for (int vIdx = 0; vIdx < 3; vIdx++)
                {
                    DrawGroupSub(_houses[c][vIdx], 0, _wallsMats[c], _houseGroups[c][vIdx]);
                    DrawGroupSub(_houses[c][vIdx], 1, _accentMats[c], _houseGroups[c][vIdx]);
                    if (_houses[c][vIdx].subMeshCount > 2)
                        DrawGroupSub(_houses[c][vIdx], 2, _trimMat, _houseGroups[c][vIdx]);
                }
                DrawGroupSub(_halls[c], 0, _wallsMats[c], _hallGroups[c]);
                DrawGroupSub(_halls[c], 1, _accentMats[c], _hallGroups[c]);
                DrawGroupSub(_keeps[c], 0, _stoneMat, _keepGroups[c]);
                DrawGroupSub(_keeps[c], 1, _accentMats[c], _keepGroups[c]);
            }
            DrawGroup(_wall, _stoneMat, _walls);
            DrawGroup(_wall, _palisadeMat, _palisades);
            DrawGroup(_wall, _ruinMat, _ruins);

            // Banners: politics at a glance, waving over halls and keeps.
            foreach (var (pos, culture) in _banners)
            {
                var rp = new RenderParams(_poleMat)
                {
                    worldBounds = new Bounds(pos, Vector3.one * 16),
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                };
                Graphics.RenderMesh(rp, _wall, 0, Matrix4x4.TRS(pos + Vector3.up * 1.2f, Quaternion.identity, new Vector3(0.12f, 2.4f, 0.12f)));
                var rpFlag = new RenderParams(_flagMats[culture])
                {
                    worldBounds = new Bounds(pos, Vector3.one * 16),
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                };
                Graphics.RenderMesh(rpFlag, _flag, 0, Matrix4x4.TRS(pos + Vector3.up * 2.1f, Quaternion.identity, new Vector3(1.5f, 0.8f, 1f)));
            }
        }

        private static void DrawGroupSub(Mesh mesh, int submesh, Material mat, List<Matrix4x4> matrices)
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
                Graphics.RenderMeshInstanced(rp, mesh, submesh, matrices, count, i);
            }
        }

        public static void DrawGroup(Mesh mesh, Material mat, List<Matrix4x4> matrices)
            => DrawGroupSub(mesh, 0, mat, matrices);
    }
}
