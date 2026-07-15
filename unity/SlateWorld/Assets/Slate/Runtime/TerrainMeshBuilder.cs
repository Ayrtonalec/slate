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
        public const int VertsPerCell = 2; // vertex every 4 world units

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
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            _paintedRoads = -1;
            RepaintRoads();
        }

        private (int, int) ClampCell(float wx, float wz)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(wx / TerrainSampler.CellSize), 0, _w.W - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt(wz / TerrainSampler.CellSize), 0, _w.H - 1);
            return (cx, cz);
        }

        // Bilinear biome color between cell centers, with land dressing.
        private Color VertexColor(float wx, float wz, float y, bool river)
        {
            float gx = wx / TerrainSampler.CellSize - 0.5f;
            float gz = wz / TerrainSampler.CellSize - 0.5f;
            int x0 = Mathf.FloorToInt(gx), z0 = Mathf.FloorToInt(gz);
            float fx = Mathf.Clamp01(gx - x0), fz = Mathf.Clamp01(gz - z0);

            Color c = Color.Lerp(
                Color.Lerp(CellColor(x0, z0), CellColor(x0 + 1, z0), fx),
                Color.Lerp(CellColor(x0, z0 + 1), CellColor(x0 + 1, z0 + 1), fx), fz);

            if (y > 0f)
            {
                // Rock tint climbs the relief, snow tops it, rivers stain their banks.
                float rel = TerrainSampler.Height01(wx, wz) - (float)_w.Sea;
                if (rel > 0.24f) c = Color.Lerp(c, Palette.RockSlope, Mathf.Clamp01((rel - 0.24f) * 5f));
                if (rel > 0.31f) c = Color.Lerp(c, Palette.SnowCap, Mathf.Clamp01((rel - 0.31f) * 9f));
                if (river) c = Color.Lerp(c, Palette.RiverTint, 0.55f);
                // Painterly variation so plains don't read as flat plastic.
                float v = (float)SlateRng.Hash2(Mathf.FloorToInt(wx * 0.7f), Mathf.FloorToInt(wz * 0.7f), 0xBEEF) - 0.5f;
                c = Color.Lerp(c, c * (1f + v * 0.16f), 0.8f);
            }
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

        // Paint road dirt onto the vertex colors whenever new roads appear.
        public void RepaintRoads()
        {
            if (_w == null || _mesh == null) return;
            if (_w.Roads.Count == _paintedRoads) return;
            _paintedRoads = _w.Roads.Count;

            var cols = (Color[])_baseColors.Clone();
            float step = TerrainSampler.CellSize / VertsPerCell;
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
                            cols[i] = Color.Lerp(cols[i], Palette.RoadDirt, wgt);
                        }
                }
            }
            _mesh.colors = cols;
        }
    }
}
