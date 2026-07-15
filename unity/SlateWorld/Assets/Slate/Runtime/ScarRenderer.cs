// SLATE — the land remembers: cairns on named battlefields, broken ground
// where the earth once heaved. Silent map memory; the D&D exporter will one
// day turn every one of these into an adventure site.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class ScarRenderer : MonoBehaviour
    {
        private WorldRunner _runner;
        private Mesh _stone;
        private Material _stoneMat;
        private readonly List<Matrix4x4> _stones = new List<Matrix4x4>();
        private int _builtCount = -1;

        public void Init(WorldRunner runner)
        {
            _runner = runner;
            _stone = ProceduralMeshes.Box();
            _stoneMat = SettlementRenderer.MakeLit(new Color(0.44f, 0.42f, 0.40f));
        }

        private void Update()
        {
            var w = _runner?.World;
            if (w == null) return;
            if (w.Scars.Count != _builtCount) Rebuild(w);
            SettlementRenderer.DrawGroup(_stone, _stoneMat, _stones);
        }

        private void Rebuild(World w)
        {
            _builtCount = w.Scars.Count;
            _stones.Clear();
            foreach (var scar in w.Scars)
            {
                var rnd = new System.Random(unchecked(w.Seed * 31337 + scar.X * 92821 + scar.Y * 486187739));
                Vector3 center = TerrainSampler.CellToWorld(scar.X, scar.Y);
                center.y = TerrainSampler.GroundY(center.x, center.z);
                if (center.y < 0.3f) continue;

                if (scar.Kind == "battle")
                {
                    // A cairn: stacked stones, and a scatter of smaller ones around it.
                    for (int layer = 0; layer < 3; layer++)
                    {
                        float s = 1.6f - layer * 0.45f;
                        _stones.Add(Matrix4x4.TRS(
                            center + Vector3.up * (0.3f + layer * 0.55f),
                            Quaternion.Euler(0, (float)(rnd.NextDouble() * 90), 0),
                            new Vector3(s, 0.6f, s)));
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = (float)(rnd.NextDouble() * Mathf.PI * 2);
                        float dist = 1.5f + (float)rnd.NextDouble() * 2.5f;
                        var p = center + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                        p.y = TerrainSampler.GroundY(p.x, p.z) + 0.15f;
                        if (p.y < 0.3f) continue;
                        float s = 0.35f + (float)rnd.NextDouble() * 0.4f;
                        _stones.Add(Matrix4x4.TRS(p, Quaternion.Euler(
                            (float)rnd.NextDouble() * 20, (float)(rnd.NextDouble() * 360), 0),
                            new Vector3(s, s * 0.7f, s)));
                    }
                }
                else // quake: a line of tumbled rock along the broken ground
                {
                    float ang = (float)(rnd.NextDouble() * Mathf.PI);
                    for (int i = 0; i < 6; i++)
                    {
                        float t = (i - 2.5f) * 2.2f;
                        var p = center + new Vector3(Mathf.Cos(ang) * t, 0, Mathf.Sin(ang) * t);
                        p.y = TerrainSampler.GroundY(p.x, p.z) + 0.1f;
                        if (p.y < 0.3f) continue;
                        float s = 0.5f + (float)rnd.NextDouble() * 0.7f;
                        _stones.Add(Matrix4x4.TRS(p, Quaternion.Euler(
                            (float)rnd.NextDouble() * 30, (float)(rnd.NextDouble() * 360), 0),
                            new Vector3(s, s * 0.5f, s * 1.4f)));
                    }
                }
            }
        }
    }
}
