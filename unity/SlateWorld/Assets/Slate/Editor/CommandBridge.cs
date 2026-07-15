// SLATE — command bridge: lets an external process (Claude's CLI) drive THIS
// already-running editor instead of booting new ones. It watches
// unity/bridge/command.txt; supported commands (first line of the file):
//   ping     -> replies "pong" (bridge alive check)
//   refresh  -> AssetDatabase.Refresh (recompile freshly edited code)
//   shots    -> run the screenshot flythrough in-place (no editor exit)
//   tests    -> run the EditMode battery via TestRunnerApi
//   stop     -> exit play mode
// Results land in unity/bridge/result.txt. Zero impact when no command file
// exists. The bridge dir is gitignored.
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Slate.Game.Editor
{
    [InitializeOnLoad]
    public static class CommandBridge
    {
        private static double _nextPoll;

        public static string BridgeDir
        {
            get
            {
                string d = Path.GetFullPath(Path.Combine(Application.dataPath, "../../bridge"));
                Directory.CreateDirectory(d);
                return d;
            }
        }

        static CommandBridge()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 0.5;

            string cmdFile = Path.Combine(BridgeDir, "command.txt");
            if (!File.Exists(cmdFile)) return;
            string cmd;
            try { cmd = File.ReadAllText(cmdFile).Trim(); File.Delete(cmdFile); }
            catch { return; } // writer may still hold it; retry next poll
            Debug.Log("[SlateBridge] command: " + cmd);

            switch (cmd)
            {
                case "ping":
                    Done("pong");
                    break;
                case "refresh":
                    AssetDatabase.Refresh();
                    Done("refreshed");
                    break;
                case "stop":
                    EditorApplication.ExitPlaymode();
                    Done("stopped");
                    break;
                case "shots":
                    if (EditorApplication.isPlaying) { Done("error: stop play mode first"); break; }
                    ScreenshotRunner.RunFromBridge();
                    // ScreenshotRunner writes the result when the last shot lands.
                    break;
                case "tests":
                    RunTests();
                    break;
                default:
                    Done("error: unknown command '" + cmd + "'");
                    break;
            }
        }

        public static void Done(string msg)
        {
            File.WriteAllText(Path.Combine(BridgeDir, "result.txt"), msg);
            Debug.Log("[SlateBridge] result: " + msg);
        }

        // --- EditMode battery through the runner API, results to result.txt.

        private class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                string summary = $"tests: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped";
                if (result.FailCount > 0)
                    summary += "\n" + CollectFailures(result);
                Done(summary);
            }

            private static string CollectFailures(ITestResultAdaptor node)
            {
                if (!node.HasChildren)
                    return node.TestStatus == TestStatus.Failed
                        ? $"FAIL {node.FullName}: {node.Message}\n"
                        : "";
                string s = "";
                foreach (var child in node.Children) s += CollectFailures(child);
                return s;
            }
        }

        private static void RunTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new TestCallbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }
    }
}
