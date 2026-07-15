// SLATE — automated eyeball: enters Play mode, ages the world, flies the
// camera through the zoom bands and saves screenshots. Run WITHOUT -batchmode
// (rendering needs a real editor):
//   Unity.exe -projectPath ... -executeMethod Slate.Game.Editor.ScreenshotRunner.Run
// Screenshots land in <repo>/unity/shots/.
//
// Entering Play mode triggers a domain reload that wipes static state and
// event subscriptions, so the "keep going" signal crosses the reload via
// SessionState and [InitializeOnLoadMethod] re-arms the update loop.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Slate.Game;
using Slate.Sim;

namespace Slate.Game.Editor
{
    public static class ScreenshotRunner
    {
        private const string Flag = "slate.shots.active";
        private const string BridgeFlag = "slate.shots.bridge"; // run inside the founder's editor, don't exit
        private static int _frame;
        private static int _stage;

        private static string Dir
        {
            get
            {
                string d = Path.GetFullPath(Path.Combine(Application.dataPath, "../../shots"));
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static void Run()
        {
            SessionState.SetBool(Flag, true);
            SessionState.SetBool(BridgeFlag, false);
            EditorSceneManager.OpenScene("Assets/Scenes/World.unity");
            EditorApplication.EnterPlaymode();
        }

        // Bridge mode: same flythrough, but inside the already-open editor —
        // exits play mode (not the editor) and reports through the bridge.
        public static void RunFromBridge()
        {
            SessionState.SetBool(Flag, true);
            SessionState.SetBool(BridgeFlag, true);
            EditorSceneManager.OpenScene("Assets/Scenes/World.unity");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void ReArmAfterReload()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            _frame = 0; _stage = 0;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            _frame++;
            var runner = Object.FindFirstObjectByType<WorldRunner>();
            var rig = Object.FindFirstObjectByType<CameraRig>();
            if (runner == null || runner.World == null || rig == null) return;

            switch (_stage)
            {
                case 0: // let everything build, then age the world so towns exist
                    if (_frame < 30) return;
                    runner.paused = true;
                    runner.RunYearsImmediate(120);
                    Center(rig, 1000f);
                    _stage = 1; _frame = 0;
                    return;

                case 1: // atlas overview
                    if (_frame < 90) return;
                    Capture("unity-01-atlas.png");
                    var s1 = Biggest(runner.World);
                    if (s1 != null) rig.SnapTo(TerrainSampler.CellToWorld(s1.X, s1.Y), 190f);
                    _stage = 2; _frame = 0;
                    return;

                case 2: // province: the biggest town and its surroundings
                    if (_frame < 90) return;
                    Capture("unity-02-town.png");
                    var s2 = Biggest(runner.World);
                    if (s2 != null) rig.SnapTo(TerrainSampler.CellToWorld(s2.X, s2.Y), 45f, 30f);
                    _stage = 3; _frame = 0;
                    return;

                case 3: // settlement zoom: rooftops and villagers
                    if (_frame < 90) return;
                    Capture("unity-03-street.png");
                    StageDisasters(runner.World, rig);
                    _stage = 4; _frame = 0;
                    return;

                case 4: // wildfire + war banner, particles given time to build
                    if (_frame < 150) return;
                    Capture("unity-04-fire-war.png");
                    StageWinterPlague(runner.World, rig);
                    _stage = 5; _frame = 0;
                    return;

                case 5: // plague town under winter snow
                    if (_frame < 150) return;
                    Capture("unity-05-plague-winter.png");
                    StageFogSea(runner.World, rig);
                    _stage = 6; _frame = 0;
                    return;

                case 6: // the fog at the edge of the world, a ship making for it
                    if (_frame < 150) return;
                    Capture("unity-06-fog-sea.png");
                    _stage = 7; _frame = 0;
                    return;

                case 7: // let the screenshots flush to disk, then wrap up
                    if (_frame < 200) return;
                    SessionState.SetBool(Flag, false);
                    EditorApplication.update -= Tick;
                    if (SessionState.GetBool(BridgeFlag, false))
                    {
                        // Founder's editor: leave it running, just report back.
                        EditorApplication.ExitPlaymode();
                        CommandBridge.Done("shots-done");
                    }
                    else
                    {
                        EditorApplication.Exit(0);
                    }
                    return;
            }
        }

        // Force phenomena into view so the VFX bar can be eyeballed on demand
        // (test tooling: direct state writes are fine here, never in the sim).
        private static void StageDisasters(World w, CameraRig rig)
        {
            w.Month = 5; // Harvest: golden fields, no snow to hide the fire

            // Find a sizable town with forest nearby, and burn a patch of it.
            Settlement town = null;
            int fireX = -1, fireY = -1;
            foreach (var s in w.AliveSettlements())
            {
                if (s.Pop < 400) continue;
                for (int r = 2; r <= 6 && fireX < 0; r++)
                    for (int dy = -r; dy <= r && fireX < 0; dy++)
                        for (int dx = -r; dx <= r && fireX < 0; dx++)
                        {
                            int nx = s.X + dx, ny = s.Y + dy;
                            if (w.InB(nx, ny) && w.Biome[ny * w.W + nx] == B.FOREST) { town = s; fireX = nx; fireY = ny; }
                        }
                if (town != null) break;
            }
            if (town == null) { town = Biggest(w); if (town == null) return; }

            if (fireX >= 0)
                for (int dy = -1; dy <= 2; dy++)
                    for (int dx = -1; dx <= 2; dx++)
                    {
                        int cx = fireX + dx, cy = fireY + dy;
                        if (!w.InB(cx, cy) || w.Biome[cy * w.W + cx] != B.FOREST) continue;
                        int i = cy * w.W + cx;
                        w.Burned[i] = 1; w.BurnCellTick[i] = w.Tick;
                        w.BurningCells.Add(i); w.BurnedVersion++;
                    }

            // A marching army with its banner, just outside the town.
            Settlement enemy = null;
            foreach (var s in w.Settlements)
                if (!s.Ruined && s.Culture != town.Culture && (enemy == null || s.Pop > enemy.Pop)) enemy = s;
            if (enemy != null)
                w.Armies.Add(new Army
                {
                    Id = w.NextArmyId++, Culture = enemy.Culture,
                    FromId = enemy.Id, TargetId = town.Id,
                    FromName = enemy.Name, TargetName = town.Name,
                    X = town.X + 3.5, Y = town.Y + 2.5, Size = 1200,
                });

            // Frame the fire itself (fall back to the town), a clear tilted view.
            float fx = fireX >= 0 ? fireX : town.X;
            float fy = fireY >= 0 ? fireY : town.Y;
            rig.SnapTo(TerrainSampler.CellToWorld(fx, fy), 90f, 35f);
        }

        private static void StageWinterPlague(World w, CameraRig rig)
        {
            // Pick the coldest sizable town so snow AND plague both show.
            Settlement town = null;
            float coldest = 1f;
            foreach (var s in w.AliveSettlements())
            {
                if (s.Pop < 500) continue;
                float temp = w.Temp[s.Y * w.W + s.X];
                if (town == null || temp < coldest) { coldest = temp; town = s; }
            }
            if (town == null) town = Biggest(w);
            if (town == null) return;
            w.PlagueActive = true;
            town.PlagueState = 1;
            town.PlagueSickUntil = w.Tick + 24;
            w.Month = 9; // Deepwinter, visuals only: snow paints + falls, roads close
            rig.SnapTo(TerrainSampler.CellToWorld(town.X, town.Y), 130f, 30f);
        }

        private static void StageFogSea(World w, CameraRig rig)
        {
            w.Month = 5; // summer light again for the sea shot

            // Find a row of guaranteed open ocean at the western edge.
            int seaRow = w.H / 2;
            for (int y = w.H / 4; y < w.H * 3 / 4; y++)
            {
                bool open = true;
                for (int x = 0; x <= 16 && open; x++)
                    if (w.HeightMap[y * w.W + x] > w.Sea) open = false;
                if (open) { seaRow = y; break; }
            }

            // A ship on that open water, making for the western fog.
            Settlement port = null;
            foreach (var s in w.AliveSettlements())
                if (s.Fish && (port == null || s.Pop > port.Pop)) port = s;
            if (port != null)
            {
                w.Ships.Add(new Ship
                {
                    Id = w.NextShipId++, HomeId = port.Id, HomeName = port.Name,
                    Captain = "Captain " + port.Name, Culture = port.Culture,
                    X = 9, Y = seaRow, LaunchX = port.X, LaunchY = port.Y,
                    TargetX = 1, TargetY = seaRow, State = Ship.Outbound,
                });
            }
            // Look WEST down the sea lane: water, the ship, the wall beyond.
            rig.SnapTo(TerrainSampler.CellToWorld(13, seaRow), 45f, 270f);
        }

        private static void Center(CameraRig rig, float height)
        {
            rig.SnapTo(new Vector3(TerrainSampler.WorldWidth * 0.5f, 0, TerrainSampler.WorldDepth * 0.5f), height);
        }

        private static Settlement Biggest(World w)
        {
            Settlement best = null;
            foreach (var s in w.Settlements)
                if (!s.Ruined && (best == null || s.Pop > best.Pop)) best = s;
            return best;
        }

        // Render the main camera to a RenderTexture and read it back — fully
        // synchronous, no file-write race (async CaptureScreenshot dropped
        // files unpredictably in this headless editor). URP's Camera.Render()
        // runs the whole pipeline, transparent particles included.
        private static void Capture(string name)
        {
            const int W = 1920, H = 1080;
            var cam = Camera.main;
            if (cam == null) return;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;

            string path = Path.Combine(Dir, name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Debug.Log("[Slate] screenshot -> " + path);
        }
    }
}
