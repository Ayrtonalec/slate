// SLATE — bakes the sim's heightfield into one painterly terrain mesh.
// Vertex colors carry the biome wash (fertility-tinted, like the atlas),
// snowcaps, rock on steep slopes, river stains, and — repainted as history
// happens — the dirt of roads.
using System.Collections.Generic;
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class TerrainMeshBuilder : MonoBehaviour
    {
        public const int VertsPerCell = 3; // vertex every ~2.7 world units (relief density)

        private Mesh _mesh;
        private Color[] _baseColors; // pre-road colors, so roads can repaint cleanly
        private Vector3[] _vertices;
        private int _vw, _vh;
        private World _w;
        private int _paintedRoads = -1;

        public void Build(World w)
        {
            _w = w;
            _vw = w.W * VertsPerCell + 1;
            _vh = w.H * VertsPerCell + 1;
            int n = _vw * _vh;
            var verts = new Vector3[n];
            var cols = new Color[n];
            float step = TerrainSampler.CellSize / VertsPerCell;

            for (int vz = 0; vz < _vh; vz++)
            {
                for (int vx = 0; vx < _vw; vx++)
                {
                    float wx = vx * step, wz = vz * step;
                    float y = TerrainSampler.GroundY(wx, wz);
                    int i = vz * _vw + vx;

                    // River channels: carve a shallow groove and stain the banks.
                    var (cx, cz) = ClampCell(wx, wz);
                    bool river = w.River[cz * w.W + cx] != 0;
                    if (river && y > 0.5f) y -= 1.1f;

                    verts[i] = new Vector3(wx, y, wz);
                    cols[i] = VertexColor(wx, wz, y, river);
                }
            }

            var tris = new int[(_vw - 1) * (_vh - 1) * 6];
            int ti = 0;
            for (int vz = 0; vz < _vh - 1; vz++)
            {
                for (int vx = 0; vx < _vw - 1; vx++)
                {
                    int i = vz * _vw + vx;
                    tris[ti++] = i; tris[ti++] = i + _vw; tris[ti++] = i + 1;
                    tris[ti++] = i + 1; tris[ti++] = i + _vw; tris[ti++] = i + _vw + 1;
                }
            }

            _mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            _mesh.vertices = verts;
            _mesh.colors = cols;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            _vertices = verts;
            _baseColors = (Color[])cols.Clone();

            var mf = gameObject.GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = _mesh;
            var mr = gameObject.GetComponent<MeshRenderer>();
            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Slate/Terrain"));
            // Per-pixel ground detail: procedural seamless tiles, two scales in-shader.
            mat.SetTexture("_GrassTex", TerrainTextures.Grass());
            mat.SetTexture("_RockTex", TerrainTextures.Rock());
            mat.SetTexture("_SnowTex", TerrainTextures.Snow());
            mat.SetFloat("_DetailScale", 0.34f);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            _paintedRoads = -1;
            _paintedBurnVersion = -1;
            _paintedSnowBucket = -1;
            _nextRepaintAllowed = 0f;
            RepaintOverlays();
        }

        private (int, int) ClampCell(float wx, float wz)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(wx / TerrainSampler.CellSize), 0, _w.W - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt(wz / TerrainSampler.CellSize), 0, _w.H - 1);
            return (cx, cz);
        }

        // Bilinear biome color between cell centers. RGB carries the macro
        // tint; ALPHA carries the snow mask (permanent caps here, seasonal
        // snow painted in RepaintOverlays). Rock-on-slopes moved to the
        // shader, per-pixel.
        private Color VertexColor(float wx, float wz, float y, bool river)
        {
            float gx = wx / TerrainSampler.CellSize - 0.5f;
            float gz = wz / TerrainSampler.CellSize - 0.5f;
            int x0 = Mathf.FloorToInt(gx), z0 = Mathf.FloorToInt(gz);
            float fx = Mathf.Clamp01(gx - x0), fz = Mathf.Clamp01(gz - z0);

            Color c = Color.Lerp(
                Color.Lerp(CellColor(x0, z0), CellColor(x0 + 1, z0), fx),
                Color.Lerp(CellColor(x0, z0 + 1), CellColor(x0 + 1, z0 + 1), fx), fz);

            float snow = 0f;
            if (y > 0f)
            {
                float rel = TerrainSampler.Height01(wx, wz) - (float)_w.Sea;
                // Permanent caps on the high peaks; frozen latitudes carry a
                // smooth dusting (bilinear temperature: no blocky snowlines).
                snow = Mathf.Clamp01((rel - 0.30f) * 9f);
                float frozen = Mathf.Clamp01((0.22f - TerrainSampler.Temp01(wx, wz)) * 7f);
                snow = Mathf.Max(snow, frozen * 0.7f);

                if (river) c = Color.Lerp(c, Palette.RiverTint, 0.55f);
                // Painterly variation so plains don't read as flat plastic.
                float v = (float)SlateRng.Hash2(Mathf.FloorToInt(wx * 0.7f), Mathf.FloorToInt(wz * 0.7f), 0xBEEF) - 0.5f;
                c = Color.Lerp(c, c * (1f + v * 0.16f), 0.8f);
            }
            c.a = Mathf.Clamp01(snow);
            return c;
        }

        private Color CellColor(int cx, int cz)
        {
            cx = Mathf.Clamp(cx, 0, _w.W - 1);
            cz = Mathf.Clamp(cz, 0, _w.H - 1);
            int i = cz * _w.W + cx;
            byte b = _w.Biome[i];
            Color c = Palette.BiomeColor(b);
            if (b == B.PLAINS || b == B.FOREST || b == B.HILLS || b == B.MARSH)
            {
                // Fertility tints the wash, exactly like the atlas: rich land reads
                // greener, poor land grayer — never washed toward white.
                float fert = _w.Fert[i];
                c = Color.Lerp(c * 0.90f, c * 1.06f, Mathf.Clamp01(fert / 1.1f));
                c = Color.Lerp(c, new Color(0.31f, 0.45f, 0.21f), Mathf.Clamp01(fert - 0.65f) * 0.6f);
            }
            return c;
        }

        private int _paintedBurnVersion = -1;
        private int _paintedSnowBucket = -1;
        private float _nextRepaintAllowed;

        private static readonly Color BurningGlow = new Color(0.70f, 0.28f, 0.09f);
        private static readonly Color CharBlack = new Color(0.16f, 0.14f, 0.12f);
        private static readonly Color SnowTint = new Color(0.85f, 0.88f, 0.93f); // cool, not pure white

        // Season -> snow strength (Frostgate .. Icewane whiten the cold latitudes).
        // A dusting, not a whiteout — the land must still read underneath.
        private static float SnowStrength(int month)
        {
            switch (month)
            {
                case 8: return 0.30f;  // Frostgate
                case 9: return 0.62f;  // Deepwinter
                case 10: return 0.5f;  // Icewane
                case 11: return 0.20f; // Stirring
                default: return 0f;
            }
        }

        // Repaint roads, burn scars and seasonal snow onto the vertex colors.
        // Throttled: this touches ~93k vertices, so it runs only when something
        // actually changed and never more than twice a second.
        public void RepaintOverlays()
        {
            if (_w == null || _mesh == null) return;
            int snowBucket = Mathf.RoundToInt(SnowStrength(_w.Month) * 4);
            bool changed = _w.Roads.Count != _paintedRoads
                || _w.BurnedVersion != _paintedBurnVersion
                || snowBucket != _paintedSnowBucket;
            if (!changed || Time.time < _nextRepaintAllowed) return;
            _nextRepaintAllowed = Time.time + 0.5f;
            _paintedRoads = _w.Roads.Count;
            _paintedBurnVersion = _w.BurnedVersion;
            _paintedSnowBucket = snowBucket;

            var cols = (Color[])_baseColors.Clone();
            float step = TerrainSampler.CellSize / VertsPerCell;

            // Roads: painted dirt.
            foreach (var road in _w.Roads)
            {
                foreach (var (px, py) in road.Pts)
                {
                    float wx = (float)((px + 0.5) * TerrainSampler.CellSize);
                    float wz = (float)((py + 0.5) * TerrainSampler.CellSize);
                    int vx = Mathf.RoundToInt(wx / step), vz = Mathf.RoundToInt(wz / step);
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = vx + dx, nz = vz + dz;
                            if (nx < 0 || nz < 0 || nx >= _vw || nz >= _vh) continue;
                            int i = nz * _vw + nx;
                            if (_vertices[i].y <= 0.2f) continue;
                            float wgt = (dx == 0 && dz == 0) ? 0.75f : 0.35f;
                            float keepA = cols[i].a; // rgb is paint; alpha is the snow mask
                            var rc = Color.Lerp(cols[i], Palette.RoadDirt, wgt);
                            rc.a = keepA * 0.5f;     // trodden roads shed their snow
                            cols[i] = rc;
                        }
                }
            }

            // Fire: burning cells glow, burned cells are charcoal scars that
            // fade as the forest regrows (the land remembers). Fire also melts
            // and blackens the snow mask.
            if (_w.BurnedVersion > 0)
            {
                for (int cz = 0; cz < _w.H; cz++)
                    for (int cx = 0; cx < _w.W; cx++)
                    {
                        byte b = _w.Burned[cz * _w.W + cx];
                        if (b == 0) continue;
                        Color target = b == 1 ? BurningGlow : CharBlack;
                        float wgt = b == 1 ? 0.85f : 0.7f;
                        PaintCell(cols, cx, cz, target, wgt);
                    }
            }

            // Winter: snow settles on the cold latitudes, deepest in Deepwinter.
            // Written into the SNOW MASK (vertex alpha); the shader lays the
            // actual snow texture per-pixel.
            float snow = SnowStrength(_w.Month);
            if (snow > 0.01f)
            {
                for (int vz = 0; vz < _vh; vz++)
                    for (int vx = 0; vx < _vw; vx++)
                    {
                        int i = vz * _vw + vx;
                        if (_vertices[i].y <= 0.2f) continue;
                        int cx = Mathf.Clamp(vx / VertsPerCell, 0, _w.W - 1);
                        int cz = Mathf.Clamp(vz / VertsPerCell, 0, _w.H - 1);
                        float cold = Mathf.Clamp01((0.52f - _w.Temp[cz * _w.W + cx]) * 3.2f);
                        float wgt = cold * snow * 0.85f;
                        if (wgt > 0.02f)
                        {
                            var c = cols[i];
                            c.a = Mathf.Max(c.a, wgt);
                            cols[i] = c;
                        }
                    }
            }

            _mesh.colors = cols;
        }

        private void PaintCell(Color[] cols, int cx, int cz, Color target, float wgt)
        {
            int v0x = cx * VertsPerCell, v0z = cz * VertsPerCell;
            for (int dz = 0; dz <= VertsPerCell; dz++)
                for (int dx = 0; dx <= VertsPerCell; dx++)
                {
                    int nx = v0x + dx, nz = v0z + dz;
                    if (nx >= _vw || nz >= _vh) continue;
                    int i = nz * _vw + nx;
                    var c = Color.Lerp(cols[i], target, wgt);
                    c.a = 0f; // fire melts and blackens the snow mask
                    cols[i] = c;
                }
        }
    }
}
