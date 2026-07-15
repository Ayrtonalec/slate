// SLATE — forests as instanced low-poly conifers. Placement is a pure
// function of the world seed (deterministic, but visual-only). Dry climates
// get sun-scorched canopies; everything is two instanced draw calls.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class TreeRenderer : MonoBehaviour
    {
        private Mesh _canopy, _trunk;
        private Material _canopyMat, _canopyDryMat, _trunkMat;
        private readonly List<Matrix4x4> _canopies = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _canopiesDry = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _trunks = new List<Matrix4x4>();

        public void Build(World w)
        {
            _canopy = ProceduralMeshes.TreeCanopy();
            _trunk = ProceduralMeshes.Trunk();
            _canopyMat = SettlementRenderer.MakeLit(Palette.Canopy);
            _canopyDryMat = SettlementRenderer.MakeLit(Palette.CanopyDry);
            _trunkMat = SettlementRenderer.MakeLit(Palette.TrunkBrown);

            _canopies.Clear(); _canopiesDry.Clear(); _trunks.Clear();
            uint tseed = SlateRng.HashStr(w.Seed + "/trees");

            for (int cy = 0; cy < w.H; cy++)
            {
                for (int cx = 0; cx < w.W; cx++)
                {
                    int i = cy * w.W + cx;
                    if (w.Biome[i] != B.FOREST && w.Biome[i] != B.MARSH) continue;
                    bool marsh = w.Biome[i] == B.MARSH;
                    int count = marsh ? 1 : 3 + (int)(SlateRng.Hash2(cx, cy, tseed) * 3);

                    for (int t = 0; t < count; t++)
                    {
                        float jx = (float)SlateRng.Hash2(cx * 7 + t, cy * 3, tseed + 1) - 0.5f;
                        float jz = (float)SlateRng.Hash2(cx * 3, cy * 7 + t, tseed + 2) - 0.5f;
                        float wx = (cx + 0.5f + jx * 0.92f) * TerrainSampler.CellSize;
                        float wz = (cy + 0.5f + jz * 0.92f) * TerrainSampler.CellSize;
                        float y = TerrainSampler.GroundY(wx, wz);
                        if (y < 0.4f) continue;

                        float h = (marsh ? 2.0f : 3.4f) + (float)SlateRng.Hash2(cx + t, cy - t, tseed + 3) * 2.4f;
                        float rot = (float)SlateRng.Hash2(cx - t, cy + t, tseed + 4) * 360f;
                        var q = Quaternion.Euler(0, rot, 0);
                        var pos = new Vector3(wx, y, wz);
                        _trunks.Add(Matrix4x4.TRS(pos, q, new Vector3(h * 0.5f, h * 0.42f, h * 0.5f)));
                        var canopyM = Matrix4x4.TRS(pos + Vector3.up * h * 0.30f, q, new Vector3(h * 0.62f, h * 0.80f, h * 0.62f));
                        // Hot, dry forest reads sun-scorched.
                        bool dry = w.Temp[i] > 0.62f && w.Moist[i] < 0.58f;
                        (dry ? _canopiesDry : _canopies).Add(canopyM);
                    }
                }
            }
        }

        private void Update()
        {
            SettlementRenderer.DrawGroup(_trunk, _trunkMat, _trunks);
            SettlementRenderer.DrawGroup(_canopy, _canopyMat, _canopies);
            SettlementRenderer.DrawGroup(_canopy, _canopyDryMat, _canopiesDry);
        }
    }
}
