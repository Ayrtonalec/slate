// SLATE — deterministic farmland layout around a settlement. Shared between
// the farmland renderer (draws the crop rows) and the crowd (farmers walk to
// the same fields). Pure function of world seed + settlement, visual-only.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public struct FarmPlot
    {
        public Vector3 Center;
        public float Rot;     // degrees
        public float Len, Wid;
    }

    public static class FarmPlots
    {
        public static void GetPlots(World w, Settlement s, List<FarmPlot> result)
        {
            result.Clear();
            if (s.Ruined) return;
            int count = 1 + Mathf.Min(9, (int)(s.Pop / 240));
            var rnd = new System.Random(unchecked(w.Seed * 743 + s.Id * 786433));
            Vector3 center = TerrainSampler.CellToWorld(s.X, s.Y);
            float cell = TerrainSampler.CellSize;

            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    float ang = (float)(rnd.NextDouble() * Mathf.PI * 2);
                    float dist = cell * (1.1f + 1.7f * (float)rnd.NextDouble());
                    var p = center + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                    int cx = Mathf.Clamp(Mathf.FloorToInt(p.x / cell), 0, w.W - 1);
                    int cz = Mathf.Clamp(Mathf.FloorToInt(p.z / cell), 0, w.H - 1);
                    int ci = cz * w.W + cx;
                    if (w.HeightMap[ci] <= w.Sea || w.FertBase[ci] < 0.4f) continue;
                    p.y = TerrainSampler.GroundY(p.x, p.z);
                    if (p.y < 0.4f) continue;
                    result.Add(new FarmPlot
                    {
                        Center = p,
                        Rot = (float)(rnd.NextDouble() * 180),
                        Len = 6.5f + (float)rnd.NextDouble() * 3f,
                        Wid = 4f + (float)rnd.NextDouble() * 1.6f,
                    });
                    break;
                }
            }
        }
    }
}
