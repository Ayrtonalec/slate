// SLATE — the crowd layer: villagers walking their settlements when you zoom
// in. Pure theater (the sim only knows population numbers); walkers spawn for
// settlements near the camera and despawn when you fly away. Instanced,
// culture-tinted, with a little walk bob so the world reads as alive.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class VillagerCrowd : MonoBehaviour
    {
        private const float ActiveCameraHeight = 300f; // crowds appear below this zoom
        private const float ActiveRadius = 380f;       // around the camera focus
        private const int MaxActiveSettlements = 14;

        private WorldRunner _runner;
        private CameraRig _rig;
        private Mesh _mesh;
        private Material[] _mats;

        private enum Role { Wander, Farmer, Hunter }

        private class Walker
        {
            public Vector3 Pos, Target;
            public float Speed, Phase;
            public Role Job;
        }

        private class Crowd
        {
            public List<Walker> Walkers = new List<Walker>();
            public System.Random Rnd;
            public Settlement S;
            public List<Vector3> FarmPoints = new List<Vector3>();   // field centers to work
            public List<Vector3> ForestPoints = new List<Vector3>(); // hunting grounds
        }

        private readonly List<FarmPlot> _plotBuffer = new List<FarmPlot>();

        private readonly Dictionary<int, Crowd> _crowds = new Dictionary<int, Crowd>();
        private readonly List<int> _toRemove = new List<int>();
        private readonly List<Matrix4x4>[] _matrices = new List<Matrix4x4>[4];

        public void Init(WorldRunner runner, CameraRig rig)
        {
            _runner = runner;
            _rig = rig;
            _mesh = ProceduralMeshes.Villager();
            _mats = new Material[4];
            for (int c = 0; c < 4; c++)
            {
                _matrices[c] = new List<Matrix4x4>();
                // Clothes: culture color, brightened so figures read against the ground.
                _mats[c] = SettlementRenderer.MakeLit(Color.Lerp(Palette.CultureColor(c) * 1.25f, new Color(0.55f, 0.48f, 0.40f), 0.15f));
            }
            _runner.WorldRebuilt += _ => _crowds.Clear();
        }

        private void Update()
        {
            var w = _runner?.World;
            if (w == null || _rig == null) return;

            Vector3 focus = _rig.Focus;
            bool active = _rig.Height < ActiveCameraHeight;

            // Spawn crowds for near, living settlements.
            if (active)
            {
                int spawned = _crowds.Count;
                foreach (var s in w.Settlements)
                {
                    if (s.Ruined || _crowds.ContainsKey(s.Id)) continue;
                    if (spawned >= MaxActiveSettlements) break;
                    Vector3 c = TerrainSampler.CellToWorld(s.X, s.Y);
                    if ((c - focus).sqrMagnitude > ActiveRadius * ActiveRadius) continue;
                    _crowds[s.Id] = MakeCrowd(w, s);
                    spawned++;
                }
            }

            // Despawn far, dead or over-quota crowds.
            _toRemove.Clear();
            foreach (var kv in _crowds)
            {
                var s = kv.Value.S;
                Vector3 c = TerrainSampler.CellToWorld(s.X, s.Y);
                bool tooFar = (c - focus).sqrMagnitude > ActiveRadius * ActiveRadius * 1.4f;
                if (!active || tooFar || s.Ruined) _toRemove.Add(kv.Key);
            }
            foreach (int id in _toRemove) _crowds.Remove(id);

            // Walk.
            for (int c = 0; c < 4; c++) _matrices[c].Clear();
            float dt = Time.deltaTime;
            float t = Time.time;
            foreach (var kv in _crowds)
            {
                var crowd = kv.Value;
                var s = crowd.S;
                SyncCount(crowd);
                foreach (var wk in crowd.Walkers)
                {
                    Vector3 delta = wk.Target - wk.Pos;
                    delta.y = 0;
                    if (delta.sqrMagnitude < 0.36f)
                    {
                        wk.Target = PickTarget(crowd, wk);
                        continue;
                    }
                    Vector3 dir = delta.normalized;
                    wk.Pos += dir * (wk.Speed * dt);
                    wk.Pos.y = TerrainSampler.GroundY(wk.Pos.x, wk.Pos.z);

                    float bob = Mathf.Abs(Mathf.Sin(t * 7f + wk.Phase)) * 0.12f;
                    var rot = Quaternion.LookRotation(dir, Vector3.up);
                    var m = Matrix4x4.TRS(
                        wk.Pos + Vector3.up * bob,
                        rot,
                        new Vector3(1.9f, 1.9f + bob, 1.9f));
                    _matrices[s.Culture].Add(m);
                }
            }

            for (int c = 0; c < 4; c++)
                SettlementRenderer.DrawGroup(_mesh, _mats[c], _matrices[c]);
        }

        private Crowd MakeCrowd(World w, Settlement s)
        {
            var crowd = new Crowd
            {
                S = s,
                Rnd = new System.Random(unchecked(w.Seed * 65599 + s.Id * 2654435761u.GetHashCode())),
            };

            // Where this settlement's people work: its fields and nearby woods.
            FarmPlots.GetPlots(w, s, _plotBuffer);
            foreach (var p in _plotBuffer) crowd.FarmPoints.Add(p.Center);
            float cell = TerrainSampler.CellSize;
            for (int dy = -4; dy <= 4 && crowd.ForestPoints.Count < 6; dy++)
                for (int dx = -4; dx <= 4 && crowd.ForestPoints.Count < 6; dx++)
                {
                    int nx = s.X + dx, ny = s.Y + dy;
                    if (!w.InB(nx, ny) || w.Biome[ny * w.W + nx] != B.FOREST) continue;
                    var fp = TerrainSampler.CellToWorld(nx, ny);
                    fp.y = TerrainSampler.GroundY(fp.x, fp.z);
                    if (fp.y > 0.35f) crowd.ForestPoints.Add(fp);
                }

            SyncCount(crowd);
            return crowd;
        }

        private void SyncCount(Crowd crowd)
        {
            int want = 2 + Mathf.Min(30, (int)(crowd.S.Pop / 110));
            var w = _runner?.World;
            if (w != null)
            {
                // Winter keeps people by the hearth; the pest keeps them behind doors.
                if (w.Month == 9 || w.Month == 10) want = Mathf.Max(1, want / 2);
                if (crowd.S.PlagueState == 1) want = Mathf.Max(1, want / 3);
            }
            while (crowd.Walkers.Count < want)
            {
                double roll = crowd.Rnd.NextDouble();
                Role job = crowd.FarmPoints.Count > 0 && roll < 0.45 ? Role.Farmer
                    : crowd.ForestPoints.Count > 0 && roll < 0.58 ? Role.Hunter
                    : Role.Wander;
                var wk = new Walker
                {
                    Job = job,
                    Speed = 1.4f + (float)crowd.Rnd.NextDouble() * 1.0f,
                    Phase = (float)crowd.Rnd.NextDouble() * 20f,
                };
                wk.Pos = PickTarget(crowd, wk);
                wk.Target = PickTarget(crowd, wk);
                crowd.Walkers.Add(wk);
            }
            if (crowd.Walkers.Count > want)
                crowd.Walkers.RemoveRange(want, crowd.Walkers.Count - want);
        }

        private Vector3 PickTarget(Crowd crowd, Walker wk)
        {
            // Farmers walk their fields (and linger, working the rows); hunters
            // head for the treeline; the rest drift between the houses. Everyone
            // goes home now and then, so paths cross in the streets.
            if (wk.Job == Role.Farmer && crowd.FarmPoints.Count > 0 && crowd.Rnd.NextDouble() < 0.75)
            {
                var f = crowd.FarmPoints[crowd.Rnd.Next(crowd.FarmPoints.Count)];
                var p = f + new Vector3(
                    (float)(crowd.Rnd.NextDouble() - 0.5) * 6f, 0,
                    (float)(crowd.Rnd.NextDouble() - 0.5) * 4f);
                p.y = TerrainSampler.GroundY(p.x, p.z);
                if (p.y > 0.35f) return p;
            }
            if (wk.Job == Role.Hunter && crowd.ForestPoints.Count > 0 && crowd.Rnd.NextDouble() < 0.6)
            {
                var f = crowd.ForestPoints[crowd.Rnd.Next(crowd.ForestPoints.Count)];
                var p = f + new Vector3(
                    (float)(crowd.Rnd.NextDouble() - 0.5) * 5f, 0,
                    (float)(crowd.Rnd.NextDouble() - 0.5) * 5f);
                p.y = TerrainSampler.GroundY(p.x, p.z);
                if (p.y > 0.35f) return p;
            }

            var s = crowd.S;
            Vector3 center = TerrainSampler.CellToWorld(s.X, s.Y);
            float radius = TerrainSampler.CellSize * (0.40f + 0.34f * s.Tier)
                + Mathf.Min(6f, (float)s.Pop / 900f); // same footprint as the buildings
            for (int i = 0; i < 6; i++)
            {
                float ang = (float)(crowd.Rnd.NextDouble() * Mathf.PI * 2);
                float dist = radius * (0.15f + 0.95f * Mathf.Sqrt((float)crowd.Rnd.NextDouble()));
                var p = center + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                p.y = TerrainSampler.GroundY(p.x, p.z);
                if (p.y > 0.35f) return p;
            }
            center.y = TerrainSampler.GroundY(center.x, center.z);
            return center;
        }
    }
}
