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
                    if (_frame < 40) return;
                    Capture("unity-01-atlas.png");
                    var s1 = Biggest(runner.World);
                    if (s1 != null) rig.SnapTo(TerrainSampler.CellToWorld(s1.X, s1.Y), 190f);
                    _stage = 2; _frame = 0;
                    return;

                case 2: // province: the biggest town and its surroundings
                    if (_frame < 40) return;
                    Capture("unity-02-town.png");
                    var s2 = Biggest(runner.World);
                    if (s2 != null) rig.SnapTo(TerrainSampler.CellToWorld(s2.X, s2.Y), 45f, 30f);
                    _stage = 3; _frame = 0;
                    return;

                case 3: // settlement zoom: rooftops and villagers
                    if (_frame < 50) return;
                    Capture("unity-03-street.png");
                    _stage = 4; _frame = 0;
                    return;

                case 4: // let the screenshots flush, then leave
                    if (_frame < 40) return;
                    SessionState.SetBool(Flag, false);
                    EditorApplication.update -= Tick;
                    EditorApplication.Exit(0);
                    return;
            }
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

        private static void Capture(string name)
        {
            string path = Path.Combine(Dir, name);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[Slate] screenshot -> " + path);
        }
    }
}
