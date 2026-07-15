// SLATE — wires the whole visible world together at Play. The scene only
// contains a camera, a sun and this object; everything else (terrain, sea,
// forests, settlements, crowds, HUD) is built from the sim at runtime.
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    [RequireComponent(typeof(WorldRunner))]
    public class SlateBootstrap : MonoBehaviour
    {
        private WorldRunner _runner;
        private GameObject _generated;
        private TerrainMeshBuilder _terrain;
        private float _roadCheckAt;

        private void Start()
        {
            _runner = GetComponent<WorldRunner>();
            BuildAll(_runner.World);
            _runner.WorldRebuilt += BuildAll;
        }

        private void BuildAll(World w)
        {
            if (_generated != null) Destroy(_generated);
            _generated = new GameObject("Generated");

            var terrainGO = new GameObject("Terrain");
            terrainGO.transform.SetParent(_generated.transform, false);
            _terrain = terrainGO.AddComponent<TerrainMeshBuilder>();
            _terrain.Build(w);

            var waterGO = new GameObject("Sea");
            waterGO.transform.SetParent(_generated.transform, false);
            waterGO.AddComponent<WaterPlane>().Build(w);

            var treesGO = new GameObject("Forests");
            treesGO.transform.SetParent(_generated.transform, false);
            treesGO.AddComponent<TreeRenderer>().Build(w);

            var settlementsGO = new GameObject("Settlements");
            settlementsGO.transform.SetParent(_generated.transform, false);
            settlementsGO.AddComponent<SettlementRenderer>().Init(_runner);

            // Camera: start over the first homeland, high enough to read the world.
            var cam = Camera.main;
            var rig = cam != null ? cam.GetComponent<CameraRig>() : null;
            if (rig != null)
            {
                Vector3 start = w.Homelands.Count > 0
                    ? TerrainSampler.CellToWorld(w.Homelands[0].X, w.Homelands[0].Y)
                    : new Vector3(TerrainSampler.WorldWidth * 0.5f, 0, TerrainSampler.WorldDepth * 0.5f);
                rig.Init(start, TerrainSampler.WorldWidth, TerrainSampler.WorldDepth);
            }

            var crowdGO = new GameObject("Crowds");
            crowdGO.transform.SetParent(_generated.transform, false);
            crowdGO.AddComponent<VillagerCrowd>().Init(_runner, rig);

            var zonesGO = new GameObject("Zones");
            zonesGO.transform.SetParent(_generated.transform, false);
            zonesGO.AddComponent<ZoneEffects>().Init(_runner);

            var hud = GetComponent<HudOverlay>();
            if (hud == null) hud = gameObject.AddComponent<HudOverlay>();
            hud.Init(_runner, rig, cam);
        }

        private void Update()
        {
            // New roads appear as painted dirt; check once a second.
            if (Time.time > _roadCheckAt && _terrain != null)
            {
                _roadCheckAt = Time.time + 1f;
                _terrain.RepaintRoads();
            }
        }
    }
}
