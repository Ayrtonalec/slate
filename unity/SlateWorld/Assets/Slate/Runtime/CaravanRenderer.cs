// SLATE — trade you can see: carts trundling back and forth along every road
// between living settlements. Pure theater over the sim's road graph.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class CaravanRenderer : MonoBehaviour
    {
        private WorldRunner _runner;
        private Mesh _box;
        private Material _cartMat, _canvasMat;
        private readonly List<Matrix4x4> _carts = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _canvases = new List<Matrix4x4>();

        public void Init(WorldRunner runner)
        {
            _runner = runner;
            _box = ProceduralMeshes.Box();
            _cartMat = SettlementRenderer.MakeLit(new Color(0.38f, 0.28f, 0.19f));
            _canvasMat = SettlementRenderer.MakeLit(new Color(0.78f, 0.73f, 0.60f));
        }

        private void Update()
        {
            var w = _runner?.World;
            if (w == null) return;
            _carts.Clear(); _canvases.Clear();
            float cell = TerrainSampler.CellSize;

            foreach (var road in w.Roads)
            {
                if (!w.SettlementsById.TryGetValue(road.A, out var a) || a == null || a.Ruined) continue;
                if (!w.SettlementsById.TryGetValue(road.B, out var b) || b == null || b.Ruined) continue;
                int n = road.Pts.Count;
                if (n < 4) continue;

                int cartCount = n > 24 ? 2 : 1;
                for (int k = 0; k < cartCount; k++)
                {
                    // Steady pace, unique phase per cart so traffic looks organic.
                    float phase = ((road.A * 31 + road.B * 17 + k * 7) % 100) / 100f;
                    float tt = Mathf.PingPong(Time.time * 0.045f + phase, 1f);
                    float fi = tt * (n - 1);
                    int i0 = Mathf.Clamp((int)fi, 0, n - 2);
                    float f = fi - i0;
                    var p0 = road.Pts[i0];
                    var p1 = road.Pts[i0 + 1];
                    float wx = Mathf.Lerp((float)((p0.X + 0.5) * cell), (float)((p1.X + 0.5) * cell), f);
                    float wz = Mathf.Lerp((float)((p0.Y + 0.5) * cell), (float)((p1.Y + 0.5) * cell), f);
                    var pos = new Vector3(wx, 0, wz);
                    pos.y = TerrainSampler.GroundY(wx, wz) + 0.3f;
                    if (pos.y < 0.3f) continue;

                    Vector3 dir = new Vector3((float)(p1.X - p0.X), 0, (float)(p1.Y - p0.Y));
                    var rot = dir.sqrMagnitude > 0.0001f
                        ? Quaternion.LookRotation(dir, Vector3.up)
                        : Quaternion.identity;
                    _carts.Add(Matrix4x4.TRS(pos, rot, new Vector3(1.1f, 0.7f, 1.9f)));
                    _canvases.Add(Matrix4x4.TRS(pos + Vector3.up * 0.6f, rot, new Vector3(1.0f, 0.55f, 1.3f)));
                }
            }

            SettlementRenderer.DrawGroup(_box, _cartMat, _carts);
            SettlementRenderer.DrawGroup(_box, _canvasMat, _canvases);
        }
    }
}
