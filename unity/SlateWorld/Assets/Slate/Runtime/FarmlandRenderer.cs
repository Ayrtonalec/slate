// SLATE — visible agriculture: crop rows around every settlement, growing
// with its population, turning with the seasons (green in Seedtime, gold at
// Harvest, bare through Deepwinter). The sim only knows fertility and food
// caps; this performs it.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class FarmlandRenderer : MonoBehaviour
    {
        private WorldRunner _runner;
        private Mesh _row;
        private Material _cropMat;
        private readonly List<Matrix4x4> _rows = new List<Matrix4x4>();
        private readonly List<FarmPlot> _plotBuffer = new List<FarmPlot>();
        private long _signature = -1;

        // Month -> crop color: sow, grow, ripen, harvest stubble, winter bare.
        private static readonly Color[] MonthColors =
        {
            new Color(0.38f, 0.42f, 0.24f), // Thaw — turned earth
            new Color(0.34f, 0.48f, 0.22f), // Seedtime — first green
            new Color(0.30f, 0.50f, 0.21f), // Bloom
            new Color(0.32f, 0.50f, 0.20f), // Highsun
            new Color(0.45f, 0.52f, 0.22f), // Longsun — turning
            new Color(0.72f, 0.60f, 0.25f), // Harvest — gold
            new Color(0.62f, 0.50f, 0.26f), // Gleaning — stubble
            new Color(0.48f, 0.40f, 0.27f), // Mistfall
            new Color(0.42f, 0.35f, 0.26f), // Frostgate — bare
            new Color(0.44f, 0.38f, 0.30f), // Deepwinter
            new Color(0.42f, 0.36f, 0.27f), // Icewane
            new Color(0.40f, 0.39f, 0.25f), // Stirring
        };

        public void Init(WorldRunner runner)
        {
            _runner = runner;
            _row = ProceduralMeshes.Box();
            _cropMat = SettlementRenderer.MakeLit(MonthColors[0]);
        }

        private void Update()
        {
            var w = _runner?.World;
            if (w == null) return;

            long sig = 0;
            foreach (var s in w.Settlements)
            {
                if (s.Ruined) continue;
                sig = sig * 31 + s.Id;
                sig = sig * 31 + (long)(s.Pop / 240);
            }
            if (sig != _signature) { _signature = sig; Rebuild(w); }

            _cropMat.SetColor("_BaseColor", MonthColors[w.Month]);
            SettlementRenderer.DrawGroup(_row, _cropMat, _rows);
        }

        private void Rebuild(World w)
        {
            _rows.Clear();
            foreach (var s in w.Settlements)
            {
                if (s.Ruined) continue;
                FarmPlots.GetPlots(w, s, _plotBuffer);
                foreach (var plot in _plotBuffer)
                {
                    var rot = Quaternion.Euler(0, plot.Rot, 0);
                    int rows = Mathf.Max(4, (int)(plot.Wid / 0.8f));
                    for (int r = 0; r < rows; r++)
                    {
                        float off = (r - (rows - 1) * 0.5f) * 0.8f;
                        Vector3 pos = plot.Center + rot * new Vector3(0, 0, off);
                        pos.y = TerrainSampler.GroundY(pos.x, pos.z) + 0.14f;
                        _rows.Add(Matrix4x4.TRS(pos, rot, new Vector3(plot.Len, 0.26f, 0.5f)));
                    }
                }
            }
        }
    }
}
