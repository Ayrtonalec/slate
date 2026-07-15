// SLATE — Architecture Kit 2.0: every culture builds like itself.
// Aldish: plaster + steep thatch + stone chimneys. Vasker: dark timber
// longhouses under steep shingle. Serai: adobe cubes, flat roofs, parapets
// and domes. Tessian: pale stone, low terracotta gables, columned porticos.
// Cities raise real keeps with crenellations. Each mesh has two submeshes:
// 0 = walls, 1 = accent (roof/dome/trim), so the renderer draws any variant
// with two instanced calls. Design doc 06 called kit-bashed architecture "a
// scripting problem" — this is that script.
using System.Collections.Generic;
using UnityEngine;

namespace Slate.Game
{
    public static class ArchitectureKit
    {
        public static Color WallsColor(int culture)
        {
            switch (culture)
            {
                case 0: return new Color(0.80f, 0.74f, 0.62f); // Aldish warm plaster
                case 1: return new Color(0.36f, 0.29f, 0.22f); // Vasker dark timber
                case 2: return new Color(0.78f, 0.68f, 0.50f); // Serai adobe
                default: return new Color(0.72f, 0.69f, 0.62f); // Tessian pale stone
            }
        }

        public static Color AccentColor(int culture)
        {
            switch (culture)
            {
                case 0: return new Color(0.62f, 0.52f, 0.32f); // thatch straw
                case 1: return new Color(0.24f, 0.21f, 0.18f); // dark shingle
                case 2: return new Color(0.62f, 0.50f, 0.36f); // sun-baked clay
                default: return new Color(0.63f, 0.35f, 0.24f); // terracotta tile
            }
        }

        // ---------- tiny two-submesh mesh builder ----------

        private sealed class B
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<int> _t0 = new List<int>(); // walls
            private readonly List<int> _t1 = new List<int>(); // accent

            private List<int> T(bool accent) => accent ? _t1 : _t0;

            public void Box(Vector3 c, Vector3 s, bool accent, float yaw = 0f)
            {
                var q = Quaternion.Euler(0, yaw, 0);
                Vector3 h = s * 0.5f;
                var p = new Vector3[8];
                int k = 0;
                for (int yy = -1; yy <= 1; yy += 2)
                    for (int zz = -1; zz <= 1; zz += 2)
                        for (int xx = -1; xx <= 1; xx += 2)
                            p[k++] = c + q * new Vector3(h.x * xx, h.y * yy, h.z * zz);
                // p order: (-,-,-)(+,-,-)(-,-,+)(+,-,+)(-,+,-)(+,+,-)(-,+,+)(+,+,+)
                int[][] faces =
                {
                    new[] { 0, 1, 5, 4 }, new[] { 1, 3, 7, 5 }, new[] { 3, 2, 6, 7 },
                    new[] { 2, 0, 4, 6 }, new[] { 4, 5, 7, 6 }, new[] { 2, 3, 1, 0 },
                };
                var t = T(accent);
                foreach (var f in faces)
                {
                    int b = _v.Count;
                    _v.Add(p[f[0]]); _v.Add(p[f[1]]); _v.Add(p[f[2]]); _v.Add(p[f[3]]);
                    t.Add(b); t.Add(b + 1); t.Add(b + 2);
                    t.Add(b); t.Add(b + 2); t.Add(b + 3);
                }
            }

            // Gabled prism: rectangular eaves at baseY, ridge along Z.
            public void Gable(Vector3 baseC, float w, float h, float d, bool accent, float overhang = 0.12f)
            {
                float hw = w * 0.5f + overhang, hd = d * 0.5f + overhang;
                Vector3 a = baseC + new Vector3(-hw, 0, -hd);
                Vector3 b = baseC + new Vector3(hw, 0, -hd);
                Vector3 c = baseC + new Vector3(hw, 0, hd);
                Vector3 dd = baseC + new Vector3(-hw, 0, hd);
                Vector3 r1 = baseC + new Vector3(0, h, -hd);
                Vector3 r2 = baseC + new Vector3(0, h, hd);
                var t = T(accent);
                void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
                {
                    int i = _v.Count; _v.Add(p0); _v.Add(p1); _v.Add(p2);
                    t.Add(i); t.Add(i + 1); t.Add(i + 2);
                }
                Tri(a, r1, r2); Tri(a, r2, dd);   // west slope
                Tri(b, c, r2); Tri(b, r2, r1);    // east slope
                Tri(a, b, r1);                     // south gable
                Tri(c, dd, r2);                    // north gable
            }

            // Low-poly hemisphere dome.
            public void Dome(Vector3 c, float r, bool accent, int seg = 8, int rings = 3)
            {
                var t = T(accent);
                for (int ring = 0; ring < rings; ring++)
                {
                    float a0 = Mathf.PI * 0.5f * ring / rings, a1 = Mathf.PI * 0.5f * (ring + 1) / rings;
                    float y0 = Mathf.Sin(a0) * r, y1 = Mathf.Sin(a1) * r;
                    float r0 = Mathf.Cos(a0) * r, r1c = Mathf.Cos(a1) * r;
                    for (int s = 0; s < seg; s++)
                    {
                        float b0 = s * Mathf.PI * 2 / seg, b1 = (s + 1) * Mathf.PI * 2 / seg;
                        Vector3 p00 = c + new Vector3(Mathf.Cos(b0) * r0, y0, Mathf.Sin(b0) * r0);
                        Vector3 p10 = c + new Vector3(Mathf.Cos(b1) * r0, y0, Mathf.Sin(b1) * r0);
                        Vector3 p01 = c + new Vector3(Mathf.Cos(b0) * r1c, y1, Mathf.Sin(b0) * r1c);
                        Vector3 p11 = c + new Vector3(Mathf.Cos(b1) * r1c, y1, Mathf.Sin(b1) * r1c);
                        int i = _v.Count;
                        _v.Add(p00); _v.Add(p10); _v.Add(p11); _v.Add(p01);
                        t.Add(i); t.Add(i + 2); t.Add(i + 1);
                        t.Add(i); t.Add(i + 3); t.Add(i + 2);
                    }
                }
            }

            // Crenellation teeth around a rectangular rim.
            public void Teeth(Vector3 rimCenter, float w, float d, bool accent, int perSide = 4)
            {
                float s = 0.14f;
                for (int i = 0; i < perSide; i++)
                {
                    float f = (i + 0.5f) / perSide - 0.5f;
                    Box(rimCenter + new Vector3(f * w, 0, -d * 0.5f), new Vector3(s, s * 1.6f, s), accent);
                    Box(rimCenter + new Vector3(f * w, 0, d * 0.5f), new Vector3(s, s * 1.6f, s), accent);
                    Box(rimCenter + new Vector3(-w * 0.5f, 0, f * d), new Vector3(s, s * 1.6f, s), accent);
                    Box(rimCenter + new Vector3(w * 0.5f, 0, f * d), new Vector3(s, s * 1.6f, s), accent);
                }
            }

            public Mesh Build()
            {
                FixWinding(_t0); FixWinding(_t1);
                var m = new Mesh();
                m.SetVertices(_v);
                m.subMeshCount = 2;
                m.SetTriangles(_t0, 0);
                m.SetTriangles(_t1, 1);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }

            private void FixWinding(List<int> t)
            {
                Vector3 centroid = Vector3.zero;
                foreach (var p in _v) centroid += p;
                centroid /= Mathf.Max(1, _v.Count);
                for (int i = 0; i < t.Count; i += 3)
                {
                    Vector3 a = _v[t[i]], b = _v[t[i + 1]], c = _v[t[i + 2]];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    if (Vector3.Dot(n, (a + b + c) / 3f - centroid) < 0f)
                        (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
                }
            }
        }

        // ---------- houses: three variants per culture ----------

        public static Mesh[] Houses(int culture)
        {
            switch (culture)
            {
                case 0: return AldishHouses();
                case 1: return VaskerHouses();
                case 2: return SeraiHouses();
                default: return TessianHouses();
            }
        }

        private static Mesh[] AldishHouses()
        {
            var v0 = new B(); // cottage under steep thatch
            v0.Box(new Vector3(0, 0.5f, 0), new Vector3(1, 1, 1), false);
            v0.Gable(new Vector3(0, 1f, 0), 1, 0.62f, 1, true);
            v0.Box(new Vector3(0.3f, 1.28f, 0.18f), new Vector3(0.13f, 0.6f, 0.13f), false); // stone chimney

            var v1 = new B(); // long cottage
            v1.Box(new Vector3(0, 0.45f, 0), new Vector3(0.95f, 0.9f, 1.5f), false);
            v1.Gable(new Vector3(0, 0.9f, 0), 0.95f, 0.55f, 1.5f, true);
            v1.Box(new Vector3(0.25f, 1.15f, -0.4f), new Vector3(0.12f, 0.55f, 0.12f), false);

            var v2 = new B(); // two-story with jetty
            v2.Box(new Vector3(0, 0.55f, 0), new Vector3(0.92f, 1.1f, 0.92f), false);
            v2.Box(new Vector3(0, 1.32f, 0), new Vector3(1.04f, 0.45f, 1.04f), false); // jettied upper floor
            v2.Gable(new Vector3(0, 1.55f, 0), 1.04f, 0.6f, 1.04f, true);
            v2.Box(new Vector3(-0.3f, 1.85f, 0.2f), new Vector3(0.13f, 0.6f, 0.13f), false);

            return new[] { v0.Build(), v1.Build(), v2.Build() };
        }

        private static Mesh[] VaskerHouses()
        {
            var v0 = new B(); // longhouse
            v0.Box(new Vector3(0, 0.38f, 0), new Vector3(0.9f, 0.76f, 1.7f), false);
            v0.Gable(new Vector3(0, 0.76f, 0), 0.9f, 0.85f, 1.7f, true);

            var v1 = new B(); // great longhouse
            v1.Box(new Vector3(0, 0.42f, 0), new Vector3(1f, 0.84f, 2.2f), false);
            v1.Gable(new Vector3(0, 0.84f, 0), 1f, 0.95f, 2.2f, true);

            var v2 = new B(); // steep-roofed cabin with a store shed
            v2.Box(new Vector3(0, 0.45f, 0.2f), new Vector3(0.9f, 0.9f, 1f), false);
            v2.Gable(new Vector3(0, 0.9f, 0.2f), 0.9f, 0.9f, 1f, true);
            v2.Box(new Vector3(0, 0.28f, -0.72f), new Vector3(0.6f, 0.56f, 0.5f), false);
            v2.Gable(new Vector3(0, 0.56f, -0.72f), 0.6f, 0.4f, 0.5f, true);

            return new[] { v0.Build(), v1.Build(), v2.Build() };
        }

        private static Mesh[] SeraiHouses()
        {
            var v0 = new B(); // flat-roofed cube with parapet
            v0.Box(new Vector3(0, 0.5f, 0), new Vector3(1, 1, 1), false);
            v0.Box(new Vector3(0, 1.04f, 0), new Vector3(1.08f, 0.08f, 1.08f), true); // roof slab
            v0.Teeth(new Vector3(0, 1.14f, 0), 0.95f, 0.95f, false, 3);

            var v1 = new B(); // L-shaped courtyard house
            v1.Box(new Vector3(-0.2f, 0.45f, 0), new Vector3(0.7f, 0.9f, 1.3f), false);
            v1.Box(new Vector3(0.35f, 0.4f, -0.35f), new Vector3(0.55f, 0.8f, 0.6f), false);
            v1.Box(new Vector3(-0.2f, 0.94f, 0), new Vector3(0.78f, 0.08f, 1.38f), true);
            v1.Box(new Vector3(0.35f, 0.84f, -0.35f), new Vector3(0.62f, 0.08f, 0.68f), true);

            var v2 = new B(); // tower house with a small dome
            v2.Box(new Vector3(0, 0.75f, 0), new Vector3(0.85f, 1.5f, 0.85f), false);
            v2.Box(new Vector3(0, 1.54f, 0), new Vector3(0.93f, 0.08f, 0.93f), true);
            v2.Dome(new Vector3(0, 1.58f, 0), 0.3f, true, 8, 2);

            return new[] { v0.Build(), v1.Build(), v2.Build() };
        }

        private static Mesh[] TessianHouses()
        {
            var v0 = new B(); // villa under low tile
            v0.Box(new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.9f, 1f), false);
            v0.Gable(new Vector3(0, 0.9f, 0), 1.2f, 0.32f, 1f, true);

            var v1 = new B(); // two-story townhouse
            v1.Box(new Vector3(0, 0.7f, 0), new Vector3(0.95f, 1.4f, 0.95f), false);
            v1.Gable(new Vector3(0, 1.4f, 0), 0.95f, 0.3f, 0.95f, true);

            var v2 = new B(); // porticoed house
            v2.Box(new Vector3(0, 0.5f, -0.1f), new Vector3(1.1f, 1f, 0.9f), false);
            v2.Gable(new Vector3(0, 1f, -0.1f), 1.1f, 0.34f, 0.9f, true);
            for (int i = -1; i <= 1; i++)
                v2.Box(new Vector3(i * 0.4f, 0.4f, 0.45f), new Vector3(0.09f, 0.8f, 0.09f), false); // columns
            v2.Box(new Vector3(0, 0.84f, 0.45f), new Vector3(1.1f, 0.08f, 0.3f), true); // porch roof

            return new[] { v0.Build(), v1.Build(), v2.Build() };
        }

        // ---------- halls and keeps ----------

        public static Mesh Hall(int culture)
        {
            var b = new B();
            switch (culture)
            {
                case 0: // Aldish great hall
                    b.Box(new Vector3(0, 0.6f, 0), new Vector3(1.5f, 1.2f, 2.4f), false);
                    b.Gable(new Vector3(0, 1.2f, 0), 1.5f, 0.9f, 2.4f, true);
                    b.Box(new Vector3(0.45f, 1.7f, 0.6f), new Vector3(0.16f, 0.8f, 0.16f), false);
                    break;
                case 1: // Vasker mead-longhouse
                    b.Box(new Vector3(0, 0.55f, 0), new Vector3(1.3f, 1.1f, 3f), false);
                    b.Gable(new Vector3(0, 1.1f, 0), 1.3f, 1.2f, 3f, true);
                    break;
                case 2: // Serai domed hall
                    b.Box(new Vector3(0, 0.7f, 0), new Vector3(1.8f, 1.4f, 1.8f), false);
                    b.Box(new Vector3(0, 1.44f, 0), new Vector3(1.9f, 0.08f, 1.9f), true);
                    b.Dome(new Vector3(0, 1.48f, 0), 0.62f, true);
                    b.Teeth(new Vector3(0, 1.56f, 0), 1.7f, 1.7f, false, 4);
                    break;
                default: // Tessian basilica
                    b.Box(new Vector3(0, 0.65f, -0.15f), new Vector3(1.6f, 1.3f, 2.2f), false);
                    b.Gable(new Vector3(0, 1.3f, -0.15f), 1.6f, 0.5f, 2.2f, true);
                    for (int i = -2; i <= 2; i++)
                        b.Box(new Vector3(i * 0.32f, 0.5f, 1.05f), new Vector3(0.1f, 1f, 0.1f), false);
                    b.Box(new Vector3(0, 1.06f, 1.05f), new Vector3(1.6f, 0.09f, 0.4f), true);
                    break;
            }
            return b.Build();
        }

        // The city keep: a real castle heart, crenellated, culture-flavored.
        public static Mesh Keep(int culture)
        {
            var b = new B();
            b.Box(new Vector3(0, 1.3f, 0), new Vector3(2f, 2.6f, 2f), false);
            b.Teeth(new Vector3(0, 2.72f, 0), 1.9f, 1.9f, false, 5);
            // Corner turrets.
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    b.Box(new Vector3(x * 0.95f, 1.7f, z * 0.95f), new Vector3(0.55f, 3.4f, 0.55f), false);
                    if (culture == 2) b.Dome(new Vector3(x * 0.95f, 3.4f, z * 0.95f), 0.3f, true, 6, 2);
                    else b.Gable(new Vector3(x * 0.95f, 3.4f, z * 0.95f), 0.55f, 0.45f, 0.55f, true);
                }
            if (culture == 2) b.Dome(new Vector3(0, 2.6f, 0), 0.8f, true);
            else b.Gable(new Vector3(0, 2.6f, 0), 1.2f, 0.7f, 1.2f, true);
            return b.Build();
        }
    }
}
