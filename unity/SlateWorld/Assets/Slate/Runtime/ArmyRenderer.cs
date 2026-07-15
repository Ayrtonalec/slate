// SLATE — warbands on the march: a block of soldiers under a culture banner,
// walking the campaign line the sim decided. War you can watch coming.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class ArmyRenderer : MonoBehaviour
    {
        private WorldRunner _runner;
        private Mesh _soldier, _pole, _flag;
        private Material[] _soldierMats;
        private Material _poleMat;
        private Material[] _flagMats;
        private readonly List<Matrix4x4>[] _soldiers = new List<Matrix4x4>[4];

        public void Init(WorldRunner runner)
        {
            _runner = runner;
            _soldier = ProceduralMeshes.Villager();
            _pole = ProceduralMeshes.Box();
            _flag = ProceduralMeshes.Box();
            _soldierMats = new Material[4];
            _flagMats = new Material[4];
            _poleMat = SettlementRenderer.MakeLit(new Color(0.30f, 0.24f, 0.18f));
            for (int c = 0; c < 4; c++)
            {
                _soldiers[c] = new List<Matrix4x4>();
                // Soldiers read darker and harder than villagers: iron over cloth.
                _soldierMats[c] = SettlementRenderer.MakeLit(Color.Lerp(Palette.CultureColor(c), new Color(0.30f, 0.30f, 0.32f), 0.45f));
                _flagMats[c] = SettlementRenderer.MakeLit(Palette.CultureColor(c) * 1.35f);
            }
        }

        private void Update()
        {
            var w = _runner?.World;
            if (w == null) return;
            for (int c = 0; c < 4; c++) _soldiers[c].Clear();
            float t = Time.time;

            foreach (var a in w.Armies)
            {
                Vector3 pos = TerrainSampler.CellToWorld(a.X, a.Y);
                pos.y = TerrainSampler.GroundY(pos.x, pos.z);
                if (pos.y < 0.2f) pos.y = 0.2f;

                // Face the target.
                float yaw = 0f;
                if (w.SettlementsById.TryGetValue(a.TargetId, out var target) && target != null)
                {
                    Vector3 tp = TerrainSampler.CellToWorld(target.X, target.Y);
                    Vector3 d = tp - pos; d.y = 0;
                    if (d.sqrMagnitude > 0.01f) yaw = Quaternion.LookRotation(d, Vector3.up).eulerAngles.y;
                }
                var rot = Quaternion.Euler(0, yaw, 0);

                // A marching block, sized by strength (12..24 figures).
                int figures = 12 + Mathf.Min(12, (int)(a.Size / 250));
                int cols = 4;
                var list = _soldiers[Mathf.Clamp(a.Culture, 0, 3)];
                for (int i = 0; i < figures; i++)
                {
                    int row = i / cols, col = i % cols;
                    Vector3 off = rot * new Vector3((col - (cols - 1) * 0.5f) * 1.15f, 0, -row * 1.05f);
                    Vector3 p = pos + off;
                    p.y = TerrainSampler.GroundY(p.x, p.z);
                    if (p.y < 0.2f) p.y = 0.2f;
                    float bob = Mathf.Abs(Mathf.Sin(t * 6.5f + i * 1.7f)) * 0.10f;
                    list.Add(Matrix4x4.TRS(p + Vector3.up * bob, rot, new Vector3(2.0f, 2.0f + bob, 2.0f)));
                }

                // The banner at the head of the column.
                Vector3 bannerPos = pos + rot * new Vector3(0, 0, 1.4f);
                bannerPos.y = TerrainSampler.GroundY(bannerPos.x, bannerPos.z);
                var rp = new RenderParams(_poleMat)
                {
                    worldBounds = new Bounds(bannerPos, Vector3.one * 20),
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                };
                Graphics.RenderMesh(rp, _pole, 0,
                    Matrix4x4.TRS(bannerPos + Vector3.up * 2.2f, rot, new Vector3(0.16f, 4.4f, 0.16f)));
                var rpFlag = new RenderParams(_flagMats[Mathf.Clamp(a.Culture, 0, 3)])
                {
                    worldBounds = new Bounds(bannerPos, Vector3.one * 20),
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                };
                float wave = Mathf.Sin(t * 3f + a.Id) * 8f;
                Graphics.RenderMesh(rpFlag, _flag, 0,
                    Matrix4x4.TRS(bannerPos + Vector3.up * 4.0f + rot * new Vector3(0.8f, 0, 0),
                        rot * Quaternion.Euler(0, wave, 0), new Vector3(1.6f, 1.0f, 0.1f)));
            }

            for (int c = 0; c < 4; c++)
                SettlementRenderer.DrawGroup(_soldier, _soldierMats[c], _soldiers[c]);
        }
    }
}
