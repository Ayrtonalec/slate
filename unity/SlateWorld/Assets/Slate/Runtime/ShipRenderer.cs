// SLATE — expedition ships: a dark hull, a mast, and a culture-colored sail
// of actual waving cloth, bobbing on the swell as it makes for the fog.
// Ships inside the fog are not drawn: the world's edge keeps its secrets.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class ShipRenderer : MonoBehaviour
    {
        private WorldRunner _runner;
        private Mesh _hull, _mast, _sail;
        private Material _hullMat, _mastMat;
        private Material[] _sailMats;

        // Sim steps ships monthly; visually they glide (same trick as armies).
        private readonly Dictionary<int, Vector3> _visualPos = new Dictionary<int, Vector3>();
        private readonly List<int> _stale = new List<int>();

        public void Init(WorldRunner runner)
        {
            _runner = runner;
            _hull = ProceduralMeshes.Box();
            _mast = ProceduralMeshes.Box();
            _sail = ProceduralMeshes.FlagCloth(8, 6);
            _hullMat = SettlementRenderer.MakeLit(new Color(0.28f, 0.21f, 0.15f));
            _mastMat = SettlementRenderer.MakeLit(new Color(0.35f, 0.27f, 0.19f));
            _sailMats = new Material[4];
            for (int c = 0; c < 4; c++)
            {
                _sailMats[c] = new Material(Shader.Find("Slate/Flag"));
                // Weathered canvas carrying the culture's color.
                _sailMats[c].SetColor("_BaseColor", Color.Lerp(new Color(0.82f, 0.78f, 0.68f), Palette.CultureColor(c), 0.45f));
                _sailMats[c].SetFloat("_WaveAmp", 0.10f);
                _sailMats[c].SetFloat("_WaveFreq", 5f);
                _sailMats[c].SetFloat("_WaveSpeed", 3.5f);
            }
        }

        private void Update()
        {
            var w = _runner?.World;
            if (w == null) return;
            float t = Time.time;
            float sailSpeed = 1.2f * TerrainSampler.CellSize * _runner.MonthsPerSecond;

            foreach (var ship in w.Ships)
            {
                if (ship.State == Ship.InFog) { _visualPos.Remove(ship.Id); continue; }

                Vector3 simPos = TerrainSampler.CellToWorld(ship.X, ship.Y);
                if (!_visualPos.TryGetValue(ship.Id, out Vector3 pos)) pos = simPos;
                pos = Vector3.MoveTowards(pos, simPos, sailSpeed * Time.deltaTime * 1.25f);
                if ((pos - simPos).sqrMagnitude > 20f * 20f) pos = simPos;
                _visualPos[ship.Id] = pos;

                // Ride the same swell the water shader shows.
                float bobY = Mathf.Sin(pos.x * 0.045f + t * 0.9f) * 0.10f
                           + Mathf.Cos(pos.z * 0.038f + t * 1.15f) * 0.10f;
                float roll = Mathf.Sin(t * 0.8f + ship.Id) * 4f;
                pos.y = 0.35f + bobY;

                Vector3 target = TerrainSampler.CellToWorld(ship.TargetX, ship.TargetY);
                Vector3 dir = target - pos; dir.y = 0;
                var rot = dir.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0, 0, roll)
                    : Quaternion.identity;

                var rp = new RenderParams(_hullMat)
                {
                    worldBounds = new Bounds(pos, Vector3.one * 24),
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                };
                Graphics.RenderMesh(rp, _hull, 0, Matrix4x4.TRS(pos, rot, new Vector3(1.6f, 0.9f, 4.6f)));
                var rpMast = rp; rpMast.material = _mastMat;
                Graphics.RenderMesh(rpMast, _mast, 0, Matrix4x4.TRS(pos + rot * new Vector3(0, 2.4f, 0.2f), rot, new Vector3(0.16f, 4.2f, 0.16f)));
                var rpSail = new RenderParams(_sailMats[Mathf.Clamp(ship.Culture, 0, 3)])
                {
                    worldBounds = new Bounds(pos, Vector3.one * 24),
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                };
                // Sail hangs from the mast, catching wind abeam.
                Graphics.RenderMesh(rpSail, _sail, 0,
                    Matrix4x4.TRS(pos + rot * new Vector3(-1.1f, 2.5f, 0.2f), rot * Quaternion.Euler(0, 90f, 0), new Vector3(2.2f, 2.2f, 1f)));
            }

            _stale.Clear();
            foreach (int id in _visualPos.Keys)
            {
                bool alive = false;
                foreach (var ship in w.Ships) if (ship.Id == id && ship.State != Ship.InFog) { alive = true; break; }
                if (!alive) _stale.Add(id);
            }
            foreach (int id in _stale) _visualPos.Remove(id);
        }
    }
}
