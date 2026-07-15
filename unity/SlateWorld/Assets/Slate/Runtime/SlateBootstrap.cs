// SLATE — wires the whole visible world together at Play. The scene only
// contains a camera, a sun and this object; everything else (terrain, sea,
// forests, settlements, crowds, HUD, post-processing) is built from the sim
// at runtime.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

        private bool _postFxDone;

        private void Start()
        {
            _runner = GetComponent<WorldRunner>();
            BuildAll(_runner.World);
            _runner.WorldRebuilt += BuildAll;
        }

        // Cinematic grade, built in code so no scene rebuild is ever needed:
        // bloom makes fire/embers/motes actually glow, gentle grading warms the
        // image, SMAA kills the jaggies ("pixelated" edges).
        private void SetupPostFx(Camera cam)
        {
            if (_postFxDone || cam == null) return;
            _postFxDone = true;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.intensity.Override(0.75f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.6f);
            var grade = profile.Add<ColorAdjustments>();
            grade.active = true;
            grade.saturation.Override(8f);
            grade.contrast.Override(8f);
            grade.postExposure.Override(0.05f);
            var vig = profile.Add<Vignette>();
            vig.active = true;
            vig.intensity.Override(0.16f);
            vig.smoothness.Override(0.55f);

            var volGO = new GameObject("PostFx");
            volGO.transform.SetParent(transform, false);
            var vol = volGO.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            vol.profile = profile;

            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
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

            var districtsGO = new GameObject("Districts");
            districtsGO.transform.SetParent(_generated.transform, false);
            districtsGO.AddComponent<DistrictRenderer>().Init(_runner);

            var farmsGO = new GameObject("Farmland");
            farmsGO.transform.SetParent(_generated.transform, false);
            farmsGO.AddComponent<FarmlandRenderer>().Init(_runner);

            var armiesGO = new GameObject("Armies");
            armiesGO.transform.SetParent(_generated.transform, false);
            armiesGO.AddComponent<ArmyRenderer>().Init(_runner);

            var caravansGO = new GameObject("Caravans");
            caravansGO.transform.SetParent(_generated.transform, false);
            caravansGO.AddComponent<CaravanRenderer>().Init(_runner);

            var scarsGO = new GameObject("Scars");
            scarsGO.transform.SetParent(_generated.transform, false);
            scarsGO.AddComponent<ScarRenderer>().Init(_runner);

            var fogGO = new GameObject("FogWall");
            fogGO.transform.SetParent(_generated.transform, false);
            fogGO.AddComponent<FogWallRenderer>().Build(w);

            var shipsGO = new GameObject("Ships");
            shipsGO.transform.SetParent(_generated.transform, false);
            shipsGO.AddComponent<ShipRenderer>().Init(_runner);

            // Camera: start over the first homeland, high enough to read the world.
            var cam = Camera.main;
            SetupPostFx(cam);
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

            var vfxGO = new GameObject("WorldVfx");
            vfxGO.transform.SetParent(_generated.transform, false);
            vfxGO.AddComponent<WorldVfx>().Init(_runner, rig);

            var hud = GetComponent<HudOverlay>();
            if (hud == null) hud = gameObject.AddComponent<HudOverlay>();
            hud.Init(_runner, rig, cam);
        }

        private void Update()
        {
            // Roads, burn scars and seasonal snow paint themselves onto the
            // terrain as history happens; check twice a second (repaint itself
            // is further throttled and change-gated).
            if (Time.time > _roadCheckAt && _terrain != null)
            {
                _roadCheckAt = Time.time + 0.5f;
                _terrain.RepaintOverlays();
            }
        }
    }
}
