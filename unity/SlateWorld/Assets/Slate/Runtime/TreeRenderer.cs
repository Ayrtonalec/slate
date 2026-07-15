// SLATE — nature with real shapes: Blender-built branching broadleaves,
// tiered conifers, curved palms and umbrella pines (tools/blender/
// gen_nature.py), plus boulders on the high ground. Species follow climate;
// fire clears and regrowth replants. Models load from Resources with a
// graceful fallback to the old procedural pair, so the project always runs.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class TreeRenderer : MonoBehaviour
    {
        private class Species
        {
            public Mesh Model;                 // 2 submeshes: 0 bark, 1 foliage (null -> fallback)
            public Material Foliage;
            public readonly List<Matrix4x4> Instances = new List<Matrix4x4>();
            // Fallback (procedural pair) when no model asset is available.
            public Mesh FallTrunk, FallCanopy;
            public readonly List<Matrix4x4> FallTrunks = new List<Matrix4x4>();
            public readonly List<Matrix4x4> FallCanopies = new List<Matrix4x4>();
        }

        private Species _broad0, _broad1, _conifer0, _conifer1, _umbrella, _palm, _scrub;
        private Species[] _all;
        private readonly Mesh[] _rockMeshes = new Mesh[3];
        private readonly List<Matrix4x4>[] _rocks = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
        private Material _barkMat, _rockMat;
        private World _w;
        private int _builtBurnVersion = -1;
        private long _builtClearSig = -1;
        private float _nextRebuildAllowed;

        // Civilization eats its forests: no trees within a town's clearing ring.
        private static long ClearingSignature(World w)
        {
            long sig = 0;
            foreach (var s in w.Settlements)
            {
                if (s.Ruined) continue;
                sig = sig * 31 + s.Id;
                sig = sig * 31 + (long)(s.Pop / 400);
            }
            return sig;
        }

        private static bool Cleared(World w, int cx, int cy)
        {
            foreach (var s in w.Settlements)
            {
                if (s.Ruined) continue;
                double r = 1.1 + s.Pop / 900.0;          // cells; big towns clear wide
                if (r > 4.0) r = 4.0;
                double dx = s.X - cx, dy = s.Y - cy;
                if (dx * dx + dy * dy <= r * r) return true;
            }
            return false;
        }

        private static Mesh LoadModel(string path)
        {
            var go = Resources.Load<GameObject>(path);
            if (go == null) return null;
            var mf = go.GetComponentInChildren<MeshFilter>();
            return mf != null ? mf.sharedMesh : null;
        }

        private Species Make(string model, Color foliage)
        {
            return new Species
            {
                Model = LoadModel("Models/nature/" + model),
                Foliage = SettlementRenderer.MakeLit(foliage),
                FallTrunk = ProceduralMeshes.Trunk(),
                FallCanopy = ProceduralMeshes.Broadleaf(),
            };
        }

        public void Build(World w)
        {
            _w = w;
            _barkMat = SettlementRenderer.MakeLit(Palette.TrunkBrown);
            _rockMat = SettlementRenderer.MakeLit(new Color(0.47f, 0.44f, 0.41f));
            _broad0 = Make("broadleaf_v0", new Color(0.32f, 0.45f, 0.20f));
            _broad1 = Make("broadleaf_v1", new Color(0.36f, 0.47f, 0.22f));
            _conifer0 = Make("conifer_v0", Palette.Canopy);
            _conifer1 = Make("conifer_v1", Palette.CanopyDry);
            _umbrella = Make("umbrella_v0", new Color(0.26f, 0.36f, 0.18f));
            _palm = Make("palm_v0", new Color(0.30f, 0.48f, 0.20f));
            _scrub = Make("broadleaf_v1", new Color(0.34f, 0.42f, 0.26f)); // squashed reuse
            _all = new[] { _broad0, _broad1, _conifer0, _conifer1, _umbrella, _palm, _scrub };
            for (int r = 0; r < 3; r++) _rockMeshes[r] = LoadModel("Models/nature/rock_v" + r);
            Rebuild(w);
        }

        private void Add(Species sp, Vector3 pos, float yaw, float scale, float squash = 1f)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            if (sp.Model != null)
            {
                sp.Instances.Add(Matrix4x4.TRS(pos, q, new Vector3(scale, scale * squash, scale)));
            }
            else
            {
                sp.FallTrunks.Add(Matrix4x4.TRS(pos, q, new Vector3(scale * 0.4f, scale * 0.45f, scale * 0.4f)));
                sp.FallCanopies.Add(Matrix4x4.TRS(pos + Vector3.up * scale * 0.6f * squash, q,
                    new Vector3(scale * 0.8f, scale * 0.7f * squash, scale * 0.8f)));
            }
        }

        private void Rebuild(World w)
        {
            _builtBurnVersion = w.BurnedVersion;
            _builtClearSig = ClearingSignature(w);
            foreach (var sp in _all)
            {
                sp.Instances.Clear(); sp.FallTrunks.Clear(); sp.FallCanopies.Clear();
            }
            foreach (var list in _rocks) list.Clear();
            uint tseed = SlateRng.HashStr(w.Seed + "/trees");

            for (int cy = 0; cy < w.H; cy++)
            {
                for (int cx = 0; cx < w.W; cx++)
                {
                    int i = cy * w.W + cx;
                    byte biome = w.Biome[i];

                    // Boulders on the high, bare ground.
                    if (biome == B.MOUNTAIN || (biome == B.HILLS && SlateRng.Hash2(cx, cy, tseed + 9) < 0.3))
                    {
                        int n = biome == B.MOUNTAIN ? 2 : 1;
                        for (int k = 0; k < n; k++)
                        {
                            float rjx = (float)SlateRng.Hash2(cx * 5 + k, cy * 11, tseed + 10) - 0.5f;
                            float rjz = (float)SlateRng.Hash2(cx * 11, cy * 5 + k, tseed + 11) - 0.5f;
                            float rx = (cx + 0.5f + rjx * 0.9f) * TerrainSampler.CellSize;
                            float rz = (cy + 0.5f + rjz * 0.9f) * TerrainSampler.CellSize;
                            float ry = TerrainSampler.GroundY(rx, rz);
                            if (ry < 0.4f) continue;
                            int rockIdx = (int)(SlateRng.Hash2(cx + k, cy, tseed + 12) * 3) % 3;
                            float rs = 1.6f + (float)SlateRng.Hash2(cx + k, cy + k, tseed + 13) * 2.8f;
                            _rocks[rockIdx].Add(Matrix4x4.TRS(new Vector3(rx, ry, rz),
                                Quaternion.Euler(0, (float)SlateRng.Hash2(cx, cy + k, tseed + 14) * 360f, 0),
                                new Vector3(rs, rs * 0.8f, rs)));
                        }
                    }

                    bool oasis = biome == B.DESERT && (w.River[i] != 0 || w.SeaDist[i] <= 1);
                    if (biome != B.FOREST && biome != B.MARSH && !oasis) continue;
                    if (w.Burned[i] != 0) continue;
                    if (Cleared(w, cx, cy)) continue; // felled for timber and firewood

                    float temp = w.Temp[i], moist = w.Moist[i];
                    int count = biome == B.MARSH ? 1 : oasis ? 2 : 3 + (int)(SlateRng.Hash2(cx, cy, tseed) * 3);

                    for (int t = 0; t < count; t++)
                    {
                        float jx = (float)SlateRng.Hash2(cx * 7 + t, cy * 3, tseed + 1) - 0.5f;
                        float jz = (float)SlateRng.Hash2(cx * 3, cy * 7 + t, tseed + 2) - 0.5f;
                        float wx = (cx + 0.5f + jx * 0.92f) * TerrainSampler.CellSize;
                        float wz = (cy + 0.5f + jz * 0.92f) * TerrainSampler.CellSize;
                        float y = TerrainSampler.GroundY(wx, wz);
                        if (y < 0.4f) continue;

                        float h = 3.2f + (float)SlateRng.Hash2(cx + t, cy - t, tseed + 3) * 2.4f;
                        float yaw = (float)SlateRng.Hash2(cx - t, cy + t, tseed + 4) * 360f;
                        var pos = new Vector3(wx, y, wz);
                        bool alt = SlateRng.Hash2(cx * 13 + t, cy, tseed + 5) < 0.5;

                        if (oasis) Add(_palm, pos, yaw, h * 1.05f);
                        else if (biome == B.MARSH) Add(_scrub, pos, yaw, h * 0.5f, 0.55f);
                        else if (temp < 0.42f) Add(moist < 0.5f ? _conifer1 : _conifer0, pos, yaw, h * 0.85f);
                        else if (temp > 0.62f && moist < 0.55f) Add(_umbrella, pos, yaw, h * 0.9f);
                        else Add(alt ? _broad1 : _broad0, pos, yaw, h * 0.8f);
                    }
                }
            }
        }

        private void Update()
        {
            if (_w != null && Time.time > _nextRebuildAllowed
                && (_w.BurnedVersion != _builtBurnVersion || ClearingSignature(_w) != _builtClearSig))
            {
                _nextRebuildAllowed = Time.time + 8f; // clearings creep, they don't flicker
                Rebuild(_w);
            }
            foreach (var sp in _all)
            {
                if (sp.Model != null)
                {
                    DrawSub(sp.Model, 0, _barkMat, sp.Instances);
                    if (sp.Model.subMeshCount > 1) DrawSub(sp.Model, 1, sp.Foliage, sp.Instances);
                }
                else
                {
                    SettlementRenderer.DrawGroup(sp.FallTrunk, _barkMat, sp.FallTrunks);
                    SettlementRenderer.DrawGroup(sp.FallCanopy, sp.Foliage, sp.FallCanopies);
                }
            }
            for (int r = 0; r < 3; r++)
                if (_rockMeshes[r] != null)
                    SettlementRenderer.DrawGroup(_rockMeshes[r], _rockMat, _rocks[r]);
        }

        private static void DrawSub(Mesh mesh, int submesh, Material mat, List<Matrix4x4> matrices)
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
    }
}
