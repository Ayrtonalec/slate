// SLATE — vegetation with a climate: conifers in the cold north, broadleaf
// in the temperate belt, umbrella pines where it's hot and dry, palms at the
// desert oases, scrub in the marshes. Placement is a pure function of the
// world seed; fire clears it and regrowth brings it back.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class TreeRenderer : MonoBehaviour
    {
        private Mesh _conifer, _broadleaf, _umbrella, _palmCrown, _trunk;
        private Material _coniferMat, _coniferDryMat, _broadleafMat, _umbrellaMat, _palmMat, _trunkMat;

        private readonly List<Matrix4x4> _conifers = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _conifersDry = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _broadleaves = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _umbrellas = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _palms = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _trunks = new List<Matrix4x4>();
        private World _w;
        private int _builtBurnVersion = -1;
        private float _nextRebuildAllowed;

        public void Build(World w)
        {
            _w = w;
            _conifer = ProceduralMeshes.TreeCanopy();
            _broadleaf = ProceduralMeshes.Broadleaf();
            _umbrella = ProceduralMeshes.UmbrellaCanopy();
            _palmCrown = ProceduralMeshes.PalmCrown();
            _trunk = ProceduralMeshes.Trunk();

            _coniferMat = SettlementRenderer.MakeLit(Palette.Canopy);
            _coniferDryMat = SettlementRenderer.MakeLit(Palette.CanopyDry);
            _broadleafMat = SettlementRenderer.MakeLit(new Color(0.32f, 0.45f, 0.20f));
            _umbrellaMat = SettlementRenderer.MakeLit(new Color(0.26f, 0.36f, 0.18f));
            _palmMat = SettlementRenderer.MakeLit(new Color(0.30f, 0.48f, 0.20f));
            _trunkMat = SettlementRenderer.MakeLit(Palette.TrunkBrown);
            Rebuild(w);
        }

        private void Rebuild(World w)
        {
            _builtBurnVersion = w.BurnedVersion;
            _conifers.Clear(); _conifersDry.Clear(); _broadleaves.Clear();
            _umbrellas.Clear(); _palms.Clear(); _trunks.Clear();
            uint tseed = SlateRng.HashStr(w.Seed + "/trees");

            for (int cy = 0; cy < w.H; cy++)
            {
                for (int cx = 0; cx < w.W; cx++)
                {
                    int i = cy * w.W + cx;
                    byte biome = w.Biome[i];
                    bool oasis = biome == B.DESERT && (w.River[i] != 0 || w.SeaDist[i] <= 1);
                    if (biome != B.FOREST && biome != B.MARSH && !oasis) continue;
                    if (w.Burned[i] != 0) continue;

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
                        float rotY = (float)SlateRng.Hash2(cx - t, cy + t, tseed + 4) * 360f;
                        var q = Quaternion.Euler(0, rotY, 0);
                        var pos = new Vector3(wx, y, wz);

                        if (oasis)
                        {
                            // Palms: tall bare trunk, a crown of drooping fronds.
                            float ph = h * 1.15f;
                            _trunks.Add(Matrix4x4.TRS(pos, q, new Vector3(ph * 0.22f, ph, ph * 0.22f)));
                            _palms.Add(Matrix4x4.TRS(pos + Vector3.up * ph * 0.97f, q, new Vector3(ph * 0.55f, ph * 0.5f, ph * 0.55f)));
                        }
                        else if (biome == B.MARSH)
                        {
                            // Low scrub.
                            _trunks.Add(Matrix4x4.TRS(pos, q, new Vector3(h * 0.3f, h * 0.25f, h * 0.3f)));
                            _broadleaves.Add(Matrix4x4.TRS(pos + Vector3.up * h * 0.28f, q, new Vector3(h * 0.5f, h * 0.3f, h * 0.5f)));
                        }
                        else if (temp < 0.42f)
                        {
                            // Cold forest: conifers (sun-scorched variant on dry heat kept for tundra edges).
                            _trunks.Add(Matrix4x4.TRS(pos, q, new Vector3(h * 0.5f, h * 0.42f, h * 0.5f)));
                            var m = Matrix4x4.TRS(pos + Vector3.up * h * 0.30f, q, new Vector3(h * 0.62f, h * 0.80f, h * 0.62f));
                            (moist < 0.5f ? _conifersDry : _conifers).Add(m);
                        }
                        else if (temp > 0.62f && moist < 0.55f)
                        {
                            // Hot and dry: umbrella pines on tall trunks.
                            _trunks.Add(Matrix4x4.TRS(pos, q, new Vector3(h * 0.35f, h * 0.7f, h * 0.35f)));
                            _umbrellas.Add(Matrix4x4.TRS(pos + Vector3.up * h * 0.68f, q, new Vector3(h * 0.85f, h * 0.55f, h * 0.85f)));
                        }
                        else
                        {
                            // Temperate: broadleaf crowns.
                            _trunks.Add(Matrix4x4.TRS(pos, q, new Vector3(h * 0.42f, h * 0.45f, h * 0.42f)));
                            _broadleaves.Add(Matrix4x4.TRS(pos + Vector3.up * h * 0.62f, q, new Vector3(h * 0.85f, h * 0.75f, h * 0.85f)));
                        }
                    }
                }
            }
        }

        private void Update()
        {
            if (_w != null && _w.BurnedVersion != _builtBurnVersion && Time.time > _nextRebuildAllowed)
            {
                _nextRebuildAllowed = Time.time + 1f;
                Rebuild(_w);
            }
            SettlementRenderer.DrawGroup(_trunk, _trunkMat, _trunks);
            SettlementRenderer.DrawGroup(_conifer, _coniferMat, _conifers);
            SettlementRenderer.DrawGroup(_conifer, _coniferDryMat, _conifersDry);
            SettlementRenderer.DrawGroup(_broadleaf, _broadleafMat, _broadleaves);
            SettlementRenderer.DrawGroup(_umbrella, _umbrellaMat, _umbrellas);
            SettlementRenderer.DrawGroup(_palmCrown, _palmMat, _palms);
        }
    }
}
