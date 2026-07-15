// SLATE — minimal HUD: year & season, speed, a live chronicle feed and
// settlement labels. IMGUI on purpose (zero setup); a real UI replaces this
// in the vertical slice. Labels are zoom-gated like the atlas.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class HudOverlay : MonoBehaviour
    {
        private WorldRunner _runner;
        private CameraRig _rig;
        private Camera _cam;

        private class FeedItem
        {
            public ChronicleEvent E;
            public float Shown;
        }

        private readonly List<FeedItem> _feed = new List<FeedItem>();
        private GUIStyle _label, _feedStyle, _headStyle, _hintStyle;
        private readonly List<Settlement> _labelBuffer = new List<Settlement>();

        private static readonly Color GodGold = new Color(0.85f, 0.68f, 0.25f);
        private static readonly Color DoomRed = new Color(0.85f, 0.42f, 0.32f);
        private static readonly Color Parchment = new Color(0.93f, 0.90f, 0.80f);

        private bool _subscribed;

        public void Init(WorldRunner runner, CameraRig rig, Camera cam)
        {
            _runner = runner;
            _rig = rig;
            _cam = cam;
            if (_subscribed) return; // Init runs again after New World; subscribe once
            _subscribed = true;
            runner.EventLogged += e =>
            {
                if (e.Imp < 1) return;
                _feed.Add(new FeedItem { E = e, Shown = Time.time });
                if (_feed.Count > 9) _feed.RemoveAt(0);
            };
            runner.WorldRebuilt += _ => _feed.Clear();
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _label.normal.textColor = Color.white;
            _feedStyle = new GUIStyle { fontSize = 13, wordWrap = true };
            _feedStyle.normal.textColor = Parchment;
            _headStyle = new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold };
            _headStyle.normal.textColor = Parchment;
            _hintStyle = new GUIStyle { fontSize = 12 };
            _hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
        }

        private void OnGUI()
        {
            var w = _runner?.World;
            if (w == null || _cam == null) return;
            EnsureStyles();

            // --- Year / season / speed (top-left).
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(12, 12, 250, 58), Texture2D.whiteTexture);
            GUI.color = Color.white;
            Shadowed(new Rect(24, 18, 240, 28), $"Year {w.Year} · {w.MonthName()}", _headStyle);
            string pace = _runner.speedIndex == 0 ? "a month ≈ 12s" : _runner.speedIndex == 1 ? "1 year/s" : "12 years/s";
            Shadowed(new Rect(24, 46, 240, 18),
                _runner.paused ? "❚❚ paused" : $"▶ speed {_runner.SpeedLabel} ({pace})   ·   seed {w.Seed}", _hintStyle);

            // --- Chronicle feed (top-right).
            float y = 14;
            float feedW = Mathf.Min(400, Screen.width * 0.34f);
            foreach (var item in _feed)
            {
                float age = Time.time - item.Shown;
                float alpha = Mathf.Clamp01(1.6f - age / 12f);
                if (alpha <= 0) continue;
                var e = item.E;
                Color c = e.Tone == "god" ? GodGold : e.Tone == "doom" ? DoomRed : Parchment;
                var content = new GUIContent($"Year {e.Year} — {e.Text}");
                float h = _feedStyle.CalcHeight(content, feedW - 16);
                GUI.color = new Color(0, 0, 0, 0.40f * alpha);
                GUI.DrawTexture(new Rect(Screen.width - feedW - 12, y, feedW, h + 8), Texture2D.whiteTexture);
                _feedStyle.normal.textColor = new Color(c.r, c.g, c.b, alpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width - feedW - 4, y + 4, feedW - 16, h), content, _feedStyle);
                y += h + 12;
            }

            // --- Key hints (bottom-left).
            Shadowed(new Rect(16, Screen.height - 26, 900, 20),
                "scroll zoom · WASD pan · drag right mouse · Q/E rotate · space pause · 1/2/3 speed · N new world",
                _hintStyle);

            // --- Settlement labels, zoom-gated like the atlas.
            DrawLabels(w);

            // --- Scar labels: the land's memory, whispered only up close.
            DrawScarLabels(w);
        }

        private void DrawScarLabels(World w)
        {
            float h = _rig != null ? _rig.Height : 500f;
            if (h > 230f) return;
            int drawn = 0;
            for (int i = w.Scars.Count - 1; i >= 0 && drawn < 14; i--)
            {
                var scar = w.Scars[i];
                if (scar.Name == null) continue;
                Vector3 world = TerrainSampler.CellToWorld(scar.X, scar.Y);
                world.y = TerrainSampler.GroundY(world.x, world.z) + 3f;
                Vector3 sp = _cam.WorldToScreenPoint(world);
                if (sp.z < 0) continue;
                float sx = sp.x, sy = Screen.height - sp.y;
                if (sx < -50 || sx > Screen.width + 50 || sy < 0 || sy > Screen.height) continue;

                _label.fontSize = 10;
                _label.fontStyle = FontStyle.Italic;
                var rect = new Rect(sx - 110, sy - 14, 220, 16);
                _label.normal.textColor = new Color(0, 0, 0, 0.6f);
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), scar.Name, _label);
                _label.normal.textColor = new Color(0.82f, 0.80f, 0.72f, 0.85f);
                GUI.Label(rect, scar.Name, _label);
                _label.fontStyle = FontStyle.Bold;
                drawn++;
            }
        }

        private void DrawLabels(World w)
        {
            float h = _rig != null ? _rig.Height : 500f;
            int minTier = h > 650f ? 2 : h > 260f ? 1 : 0;

            _labelBuffer.Clear();
            foreach (var s in w.Settlements)
            {
                if (s.Ruined || s.Tier < minTier) continue;
                _labelBuffer.Add(s);
            }
            // Biggest first; cap the number of labels on screen.
            _labelBuffer.Sort((a, b) => b.Pop.CompareTo(a.Pop));
            int drawn = 0;
            foreach (var s in _labelBuffer)
            {
                if (drawn >= 50) break;
                Vector3 world = TerrainSampler.CellToWorld(s.X, s.Y);
                world.y = TerrainSampler.GroundY(world.x, world.z) + 6f;
                Vector3 sp = _cam.WorldToScreenPoint(world);
                if (sp.z < 0) continue;
                float sx = sp.x, sy = Screen.height - sp.y;
                if (sx < -50 || sx > Screen.width + 50 || sy < -20 || sy > Screen.height + 20) continue;

                _label.fontSize = 11 + s.Tier * 2;
                string text = s.Name;
                var rect = new Rect(sx - 90, sy - 30, 180, 18);
                _label.normal.textColor = new Color(0, 0, 0, 0.75f);
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, _label);
                _label.normal.textColor = Parchment;
                GUI.Label(rect, text, _label);
                drawn++;
            }
        }

        private void Shadowed(Rect r, string text, GUIStyle style)
        {
            var keep = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, style);
            style.normal.textColor = keep;
            GUI.Label(r, text, style);
        }
    }
}
