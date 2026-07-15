// SLATE — tiny procedural mesh kit. Placeholder architecture and figures,
// designed to read well at distance (silhouette + roof color carry the look).
// Asset-pack replacements slot in later without touching the renderers.
using System.Collections.Generic;
using UnityEngine;

namespace Slate.Game
{
    public static class ProceduralMeshes
    {
        private static Mesh Build(List<Vector3> v, List<int> t)
        {
            EnsureOutward(v, t);
            var m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // All kit shapes are convex around their centroid, so any triangle whose
        // winding faces inward is flipped. Hand-authoring windings per shape is
        // how the first build ended up with inside-out cones.
        private static void EnsureOutward(List<Vector3> v, List<int> t)
        {
            Vector3 centroid = Vector3.zero;
            foreach (var p in v) centroid += p;
            centroid /= Mathf.Max(1, v.Count);
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                Vector3 outward = (a + b + c) / 3f - centroid;
                if (Vector3.Dot(n, outward) < 0f)
                {
                    (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                }
            }
        }

        private static void AddBox(List<Vector3> v, List<int> t, Vector3 c, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] p =
            {
                c + new Vector3(-h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, -h.z),
                c + new Vector3(h.x, -h.y, h.z), c + new Vector3(-h.x, -h.y, h.z),
                c + new Vector3(-h.x, h.y, -h.z), c + new Vector3(h.x, h.y, -h.z),
                c + new Vector3(h.x, h.y, h.z), c + new Vector3(-h.x, h.y, h.z),
            };
            int[][] faces =
            {
                new[] { 0, 4, 5, 1 }, new[] { 1, 5, 6, 2 }, new[] { 2, 6, 7, 3 },
                new[] { 3, 7, 4, 0 }, new[] { 4, 7, 6, 5 }, new[] { 3, 0, 1, 2 },
            };
            foreach (var f in faces)
            {
                int b = v.Count;
                v.Add(p[f[0]]); v.Add(p[f[1]]); v.Add(p[f[2]]); v.Add(p[f[3]]);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }
        }

        // Unit cube centered at origin, 1x1x1.
        public static Mesh Box()
        {
            var v = new List<Vector3>(); var t = new List<int>();
            AddBox(v, t, Vector3.zero, Vector3.one);
            return Build(v, t);
        }

        // House body: 1x1x1 with the base at y=0.
        public static Mesh HouseBody()
        {
            var v = new List<Vector3>(); var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), Vector3.one);
            return Build(v, t);
        }

        // Gabled roof over a 1x1 footprint, slight overhang; base at y=0.
        public static Mesh GableRoof()
        {
            var v = new List<Vector3>(); var t = new List<int>();
            float o = 0.62f, ridge = 0.55f;
            Vector3 a = new Vector3(-o, 0, -o), b = new Vector3(o, 0, -o);
            Vector3 c = new Vector3(o, 0, o), d = new Vector3(-o, 0, o);
            Vector3 r1 = new Vector3(0, ridge, -o), r2 = new Vector3(0, ridge, o);
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                int i = v.Count; v.Add(p0); v.Add(p1); v.Add(p2);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                Tri(p0, p1, p2); Tri(p0, p2, p3);
            }
            Quad(a, r1, r2, d);       // west slope
            Quad(b, c, r2, r1);       // east slope
            Tri(a, b, r1);            // south gable
            Tri(c, d, r2);            // north gable
            return Build(v, t);
        }

        // Round tower with a cone cap; base at y=0, height 1, radius 0.5.
        public static Mesh Tower(int seg = 8)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            float bodyH = 0.72f, capH = 0.28f, r = 0.5f;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
                Vector3 t0 = b0 + Vector3.up * bodyH, t1 = b1 + Vector3.up * bodyH;
                Vector3 apex = new Vector3(0, bodyH + capH, 0);
                int k = v.Count;
                v.Add(b0); v.Add(b1); v.Add(t1); v.Add(t0);
                t.Add(k); t.Add(k + 2); t.Add(k + 1);
                t.Add(k); t.Add(k + 3); t.Add(k + 2);
                int k2 = v.Count;
                v.Add(t0); v.Add(t1); v.Add(apex);
                t.Add(k2); t.Add(k2 + 2); t.Add(k2 + 1);
            }
            return Build(v, t);
        }

        // Tree canopy: two stacked cones; base at y=0, height 1.
        public static Mesh TreeCanopy(int seg = 7)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            void Cone(float y0, float h, float r)
            {
                Vector3 apex = new Vector3(0, y0 + h, 0);
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                    Vector3 b0 = new Vector3(Mathf.Cos(a0) * r, y0, Mathf.Sin(a0) * r);
                    Vector3 b1 = new Vector3(Mathf.Cos(a1) * r, y0, Mathf.Sin(a1) * r);
                    int k = v.Count;
                    v.Add(b0); v.Add(b1); v.Add(apex);
                    t.Add(k); t.Add(k + 2); t.Add(k + 1);
                }
            }
            Cone(0.0f, 0.62f, 0.42f);
            Cone(0.40f, 0.60f, 0.30f);
            return Build(v, t);
        }

        // Tree trunk: thin box, base at y=0, height 1.
        public static Mesh Trunk()
        {
            var v = new List<Vector3>(); var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(0.16f, 1f, 0.16f));
            return Build(v, t);
        }

        // Villager: body + head in one mesh; base at y=0, height 1.
        public static Mesh Villager()
        {
            var v = new List<Vector3>(); var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.34f, 0), new Vector3(0.30f, 0.68f, 0.20f)); // body
            AddBox(v, t, new Vector3(0, 0.82f, 0), new Vector3(0.20f, 0.22f, 0.20f)); // head
            return Build(v, t);
        }

        // Banner cloth: subdivided quad in the XZ... no — XY plane, pole at x=0,
        // 1 unit long and 1 unit tall, uv.x = 0 at the pole so the flag shader
        // can pin it there and wave the free edge.
        public static Mesh FlagCloth(int segX = 9, int segY = 4)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int y = 0; y <= segY; y++)
                for (int x = 0; x <= segX; x++)
                {
                    float fx = (float)x / segX, fy = (float)y / segY;
                    v.Add(new Vector3(fx, fy - 0.5f, 0));
                    uv.Add(new Vector2(fx, fy));
                }
            for (int y = 0; y < segY; y++)
                for (int x = 0; x < segX; x++)
                {
                    int i = y * (segX + 1) + x;
                    t.Add(i); t.Add(i + segX + 1); t.Add(i + 1);
                    t.Add(i + 1); t.Add(i + segX + 1); t.Add(i + segX + 2);
                }
            var m = new Mesh();
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // Flat disc of radius 1 at y=0 with UVs (r encoded in uv.x for the zone shader).
        public static Mesh Disc(int seg = 40)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            var uv = new List<Vector2>();
            v.Add(Vector3.zero); uv.Add(new Vector2(0, 0));
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg;
                v.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                uv.Add(new Vector2(1, 0));
                if (i > 0) { t.Add(0); t.Add(v.Count - 1); t.Add(v.Count - 2); }
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
