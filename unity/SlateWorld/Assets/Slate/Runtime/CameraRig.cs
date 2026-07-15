// SLATE — the zoom camera: one smooth flight from Atlas (top-down, the whole
// world) to Settlement (tilted, rooftops and villagers). The pitch curve is
// the design's "banded zoom" in a single gesture: high = map, low = place.
using UnityEngine;

namespace Slate.Game
{
    public class CameraRig : MonoBehaviour
    {
        public const float MinHeight = 18f;
        public const float MaxHeight = 1150f;

        private Camera _cam;
        private Vector3 _focus;          // point on the ground the camera orbits
        private float _height = 900f;    // current smoothed height above focus
        private float _heightTarget = 900f;
        private float _yaw = 0f, _yawTarget = 0f;
        private Vector3 _focusTarget;
        private float _worldW = 1536f, _worldD = 960f;
        private Vector3 _dragAnchor;
        private bool _dragging;

        public Vector3 Focus => _focus;
        public float Height => _height;

        // Jump instantly (no smoothing) — used by screenshot tooling and tests.
        public void SnapTo(Vector3 focus, float height, float yaw = 0f)
        {
            _focus = _focusTarget = focus;
            _height = _heightTarget = Mathf.Clamp(height, MinHeight, MaxHeight);
            _yaw = _yawTarget = yaw;
            Apply();
        }

        public void Init(Vector3 startFocus, float worldW, float worldD)
        {
            _worldW = worldW; _worldD = worldD;
            _focus = _focusTarget = startFocus;
            _cam = GetComponent<Camera>();
            _cam.nearClipPlane = 0.5f;
            _cam.farClipPlane = 6000f;
            _cam.fieldOfView = 42f;
            Apply();
        }

        private void Update()
        {
            if (_cam == null) return;

            // --- Zoom (scroll), toward the cursor.
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                bool hasBefore = GroundPointUnderCursor(out Vector3 before);
                float factor = Mathf.Pow(0.87f, scroll);
                float newTarget = Mathf.Clamp(_heightTarget * factor, MinHeight, MaxHeight);
                // Pull the focus toward the point under the cursor as we dive.
                if (newTarget < _heightTarget && hasBefore)
                {
                    float pull = 1f - newTarget / _heightTarget;
                    _focusTarget += (before - _focusTarget) * pull * 0.9f;
                }
                _heightTarget = newTarget;
            }

            // --- Pan (WASD / arrows), speed scales with zoom.
            float panSpeed = _height * 0.9f + 12f;
            Vector3 fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move += fwd;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move -= fwd;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += right;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move -= right;
            _focusTarget += move.normalized * (panSpeed * Time.deltaTime);

            // --- Drag pan (middle or right mouse).
            if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1))
            {
                _dragging = GroundPointUnderCursor(out _dragAnchor);
            }
            if (_dragging && (Input.GetMouseButton(2) || Input.GetMouseButton(1)))
            {
                if (GroundPointUnderCursor(out Vector3 now))
                {
                    Vector3 delta = _dragAnchor - now;
                    delta.y = 0;
                    _focusTarget += delta;
                    _focus += delta; // keep the grab point glued to the cursor
                    Apply();
                }
            }
            else _dragging = false;

            // --- Rotate (Q/E).
            if (Input.GetKey(KeyCode.Q)) _yawTarget -= 80f * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) _yawTarget += 80f * Time.deltaTime;

            // --- Clamp focus to the map.
            _focusTarget.x = Mathf.Clamp(_focusTarget.x, 0, _worldW);
            _focusTarget.z = Mathf.Clamp(_focusTarget.z, 0, _worldD);
            _focusTarget.y = Mathf.Max(0f, TerrainSampler.BoundWorld != null
                ? TerrainSampler.GroundY(_focusTarget.x, _focusTarget.z) : 0f);

            // --- Smooth and apply.
            float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
            _focus = Vector3.Lerp(_focus, _focusTarget, k);
            _height = Mathf.Lerp(_height, _heightTarget, k);
            _yaw = Mathf.Lerp(_yaw, _yawTarget, k);
            Apply();
        }

        private void Apply()
        {
            // Pitch flattens to top-down as you rise: Settlement 52° -> Atlas 89°.
            float t = Mathf.InverseLerp(MinHeight, 700f, _height);
            float pitch = Mathf.Lerp(52f, 89f, Mathf.Sqrt(Mathf.Clamp01(t)));
            float rad = pitch * Mathf.Deg2Rad;
            float dist = _height / Mathf.Sin(rad);

            var rot = Quaternion.Euler(pitch, _yaw, 0);
            Vector3 back = rot * Vector3.back;
            transform.position = _focus + back * dist;
            transform.rotation = rot;
        }

        private bool GroundPointUnderCursor(out Vector3 point)
        {
            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            // Intersect the y = focus.y plane (good enough for grabbing the map).
            var plane = new Plane(Vector3.up, new Vector3(0, _focus.y, 0));
            if (plane.Raycast(ray, out float enter)) { point = ray.GetPoint(enter); return true; }
            point = Vector3.zero;
            return false;
        }
    }
}
