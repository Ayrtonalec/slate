// SLATE — districts: settlements stop being generic blobs and grow the
// quarters their stats already imply (Batch C, "the face of things").
//   fishing town  -> a pier on the shore side, moored boats, drying racks
//   market town   -> stalls around the hall, granaries by the edge
//   walled town   -> a poor sprawl of hovels outside the gate
// All derived from existing sim fields (Fish, Hinterland, Walls, Pop);
// pure theater, GPU-instanced, deterministic per settlement.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class DistrictRenderer : MonoBehaviour
    {
        private WorldRunner _runner;
        private Mesh _box, _gable, _boatHull, _hovelBody, _hovelRoof;
        private Material _woodMat, _postMat, _canvasMat, _strawMat, _hovelMat, _boatMat;
        private Material[] _stallCanopyMats;

        private readonly List<Matrix4x4> _planks = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _posts = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _racks = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _boats = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _stallCounters = new List<Matrix4x4>();
        private readonly List<Matrix4x4>[] _stallCanopies = new List<Matrix4x4>[4];
        private readonly List<Matrix4x4> _granaries = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _granaryRoofs = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _hovelBodies = new List<Matrix4x4>();
        private readonly List<Matrix4x4> _hovelRoofs = new List<Matrix4x4>();
        private long _signature = -1;

        public void Init(WorldRunner runner)
        {
            _runner = runner;
            _box = ProceduralMeshes.Box();
            _gable = ProceduralMeshes.GableRoof();
            _hovelBody = ProceduralMeshes.HouseBody();
            _hovelRoof = ProceduralMeshes.GableRoof();
            var go = Resources.Load<GameObject>("Models/nature/ship_hull");
            var mf = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
            _boatHull = mf != null ? mf.sharedMesh : ProceduralMeshes.Box();

            _woodMat = SettlementRenderer.MakeLit(new Color(0.42f, 0.33f, 0.24f));
            _postMat = SettlementRenderer.MakeLit(new Color(0.32f, 0.25f, 0.18f));
            _canvasMat = SettlementRenderer.MakeLit(new Color(0.80f, 0.75f, 0.62f));
            _strawMat = SettlementRenderer.MakeLit(new Color(0.62f, 0.52f, 0.32f));
            _hovelMat = SettlementRenderer.MakeLit(new Color(0.52f, 0.46f, 0.38f));
            _boatMat = SettlementRenderer.MakeLit(new Color(0.30f, 0.23f, 0.16f));
            _stallCanopyMats = new Material[4];
            for (int c = 0; c < 4; c++)
            {
                _stallCanopies[c] = new List<Matrix4x4>();
                _stallCanopyMats[c] = SettlementRenderer.MakeLit(
                    Color.Lerp(new Color(0.85f, 0.80f, 0.68f), Palette.CultureColor(c), 0.5f));
            }
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
                sig = sig * 31 + s.Tier;
                sig = sig * 31 + s.Walls;
                sig = sig * 31 + s.Hinterland;
                sig = sig * 31 + (long)(s.Pop / 200);
            }
            if (sig != _signature) { _signature = sig; Rebuild(w); }

            Draw();
        }

        private void Rebuild(World w)
        {
            _planks.Clear(); _posts.Clear(); _racks.Clear(); _boats.Clear();
            _stallCounters.Clear(); _granaries.Clear(); _granaryRoofs.Clear();
            _hovelBodies.Clear(); _hovelRoofs.Clear();
            for (int c = 0; c < 4; c++) _stallCanopies[c].Clear();

            foreach (var s in w.Settlements)
            {
                if (s.Ruined) continue;
                var rnd = new System.Random(unchecked(w.Seed * 15485863 + s.Id * 32452843));
                if (s.Fish && s.Pop > 250) BuildDocks(w, s, rnd);
                if (s.Tier >= 2 || s.Hinterland >= 2) BuildMarket(w, s, rnd);
                if (s.Walls >= 1 && s.Pop > 900) BuildSprawl(w, s, rnd);
            }
        }

        // --- The docks: a pier reaching into the water, boats at its side.
        private void BuildDocks(World w, Settlement s, System.Random rnd)
        {
            Vector3 center = TerrainSampler.CellToWorld(s.X, s.Y);
            // March toward the water to find the shoreline.
            Vector3 dir = Vector3.zero;
            for (int r = 1; r <= 3 && dir == Vector3.zero; r++)
                for (int dy = -r; dy <= r && dir == Vector3.zero; dy++)
                    for (int dx = -r; dx <= r && dir == Vector3.zero; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int nx = s.X + dx, ny = s.Y + dy;
                        if (w.InB(nx, ny) && w.HeightMap[ny * w.W + nx] <= w.Sea)
                            dir = new Vector3(dx, 0, dy).normalized;
                    }
            if (dir == Vector3.zero) return;

            Vector3 p = center;
            for (int step = 0; step < 40; step++)
            {
                if (TerrainSampler.GroundY(p.x, p.z) < 0.3f) break;
                p += dir * 1.2f;
            }
            Vector3 start = p - dir * 1.8f;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            float pierLen = 9f + (float)rnd.NextDouble() * 5f;

            for (float d = 0; d < pierLen; d += 1.15f)
            {
                Vector3 plank = start + dir * d;
                _planks.Add(Matrix4x4.TRS(plank + Vector3.up * 0.75f, rot, new Vector3(2.2f, 0.14f, 1.05f)));
                if (((int)(d / 1.15f)) % 3 == 0)
                {
                    _posts.Add(Matrix4x4.TRS(plank + rot * new Vector3(-1.0f, 0.1f, 0), rot, new Vector3(0.18f, 1.6f, 0.18f)));
                    _posts.Add(Matrix4x4.TRS(plank + rot * new Vector3(1.0f, 0.1f, 0), rot, new Vector3(0.18f, 1.6f, 0.18f)));
                }
            }

            // Moored boats: little sisters of the expedition hull.
            int boats = 1 + rnd.Next(3);
            for (int i = 0; i < boats; i++)
            {
                float along = pierLen * (0.35f + 0.55f * (float)rnd.NextDouble());
                float side = rnd.Next(2) == 0 ? -2.2f : 2.2f;
                Vector3 bp = start + dir * along + rot * new Vector3(side, 0, 0);
                bp.y = 0.18f;
                var brot = rot * Quaternion.Euler(0, (float)rnd.NextDouble() * 24f - 12f + 90f, 0);
                _boats.Add(Matrix4x4.TRS(bp - Vector3.up * 0.18f, brot, Vector3.one * 1.05f));
            }

            // Drying racks on the beach.
            for (int i = 0; i < 2; i++)
            {
                Vector3 rp = start - dir * (2.5f + i * 2.2f) + rot * new Vector3((i % 2 == 0 ? 2.4f : -2.4f), 0, 0);
                rp.y = TerrainSampler.GroundY(rp.x, rp.z);
                if (rp.y < 0.3f) continue;
                _racks.Add(Matrix4x4.TRS(rp + Vector3.up * 0.9f, rot, new Vector3(2.6f, 0.1f, 0.12f)));
                _racks.Add(Matrix4x4.TRS(rp + rot * new Vector3(-1.2f, 0, 0) + Vector3.up * 0.45f, rot, new Vector3(0.12f, 0.9f, 0.12f)));
                _racks.Add(Matrix4x4.TRS(rp + rot * new Vector3(1.2f, 0, 0) + Vector3.up * 0.45f, rot, new Vector3(0.12f, 0.9f, 0.12f)));
            }
        }

        // --- The market: stalls around the hall, granaries at the edge.
        private void BuildMarket(World w, Settlement s, System.Random rnd)
        {
            Vector3 center = TerrainSampler.CellToWorld(s.X, s.Y);
            int culture = Mathf.Clamp(s.Culture, 0, 3);
            int stalls = 3 + Mathf.Min(4, s.Hinterland);
            float plazaR = 5.5f + s.Tier;

            for (int i = 0; i < stalls; i++)
            {
                float ang = i * Mathf.PI * 2 / stalls + (float)rnd.NextDouble() * 0.4f;
                Vector3 p = center + new Vector3(Mathf.Cos(ang) * plazaR, 0, Mathf.Sin(ang) * plazaR);
                p.y = TerrainSampler.GroundY(p.x, p.z);
                if (p.y < 0.35f) continue;
                var rot = Quaternion.Euler(0, -ang * Mathf.Rad2Deg + 90f, 0);
                _stallCounters.Add(Matrix4x4.TRS(p + Vector3.up * 0.45f, rot, new Vector3(1.9f, 0.9f, 1.1f)));
                _stallCanopies[culture].Add(Matrix4x4.TRS(p + Vector3.up * 1.5f, rot, new Vector3(2.2f, 0.5f, 1.5f)));
            }

            // Granaries: fat little stores under straw caps, one per few hundred souls.
            int granaries = Mathf.Clamp((int)(s.Pop / 700), 1, 3);
            for (int i = 0; i < granaries; i++)
            {
                float ang = (float)(rnd.NextDouble() * Mathf.PI * 2);
                float dist = plazaR + 3f + (float)rnd.NextDouble() * 2f;
                Vector3 p = center + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                p.y = TerrainSampler.GroundY(p.x, p.z);
                if (p.y < 0.35f) continue;
                var rot = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                _granaries.Add(Matrix4x4.TRS(p + Vector3.up * 0.8f, rot, new Vector3(1.7f, 1.6f, 1.7f)));
                _granaryRoofs.Add(Matrix4x4.TRS(p + Vector3.up * 1.6f, rot, new Vector3(2.0f, 1.2f, 2.0f)));
            }
        }

        // --- The sprawl: those who can't afford the walls live against them.
        private void BuildSprawl(World w, Settlement s, System.Random rnd)
        {
            Vector3 center = TerrainSampler.CellToWorld(s.X, s.Y);
            float wallR = TerrainSampler.CellSize * (0.40f + 0.34f * s.Tier)
                + Mathf.Min(6f, (float)s.Pop / 900f) + 3.6f;
            int hovels = Mathf.Clamp((int)(s.Pop / 400), 3, 10);
            for (int i = 0; i < hovels; i++)
            {
                float ang = (float)(rnd.NextDouble() * Mathf.PI * 2);
                float dist = wallR + 2.2f + (float)rnd.NextDouble() * 4f;
                Vector3 p = center + new Vector3(Mathf.Cos(ang) * dist, 0, Mathf.Sin(ang) * dist);
                p.y = TerrainSampler.GroundY(p.x, p.z);
                if (p.y < 0.35f) continue;
                var rot = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                float sc = 1.1f + (float)rnd.NextDouble() * 0.5f;
                _hovelBodies.Add(Matrix4x4.TRS(p, rot, new Vector3(sc, sc * 0.6f, sc)));
                _hovelRoofs.Add(Matrix4x4.TRS(p + Vector3.up * sc * 0.6f, rot, new Vector3(sc, sc * 0.5f, sc)));
            }
        }

        private void Draw()
        {
            SettlementRenderer.DrawGroup(_box, _woodMat, _planks);
            SettlementRenderer.DrawGroup(_box, _postMat, _posts);
            SettlementRenderer.DrawGroup(_box, _postMat, _racks);
            SettlementRenderer.DrawGroup(_boatHull, _boatMat, _boats);
            SettlementRenderer.DrawGroup(_box, _woodMat, _stallCounters);
            for (int c = 0; c < 4; c++)
                SettlementRenderer.DrawGroup(_gable, _stallCanopyMats[c], _stallCanopies[c]);
            SettlementRenderer.DrawGroup(_box, _hovelMat, _granaries);
            SettlementRenderer.DrawGroup(_gable, _strawMat, _granaryRoofs);
            SettlementRenderer.DrawGroup(_hovelBody, _hovelMat, _hovelBodies);
            SettlementRenderer.DrawGroup(_hovelRoof, _strawMat, _hovelRoofs);
        }
    }
}
