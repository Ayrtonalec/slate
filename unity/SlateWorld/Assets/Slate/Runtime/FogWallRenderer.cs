// SLATE — the fog at the edge of the world. Beyond the last known water
// there is nothing yet: the sea fades into a dark gradient and a wall of
// slow-churning gloom. When the Archipelago phase lands, discovery will push
// this boundary outward; today it is the world's mythic frame.
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class FogWallRenderer : MonoBehaviour
    {
        private ParticleSystem _gloom;
        private float _perimeter;
        private float _w, _d;
        private const float Margin = 30f;   // fog line sits just past the map edge

        public void Build(World world)
        {
            _w = TerrainSampler.WorldWidth;
            _d = TerrainSampler.WorldDepth;
            _perimeter = 2f * (_w + _d);

            // The sea beyond the edge fades to near-black: a rectangular ring
            // mesh, transparent on the inside, dark at the outer rim.
            BuildDarkRing();

            // The wall itself: slow, huge, dark billows churning along the rim.
            _gloom = VfxToolkit.Create(transform, new VfxToolkit.SystemSpec
            {
                Name = "fog-wall", Material = VfxToolkit.BillowMaterial,
                // Darker than the darkened sea beneath it, so stacked billows
                // read as gathering gloom, not pale mist.
                ColorA = new Color(0.012f, 0.020f, 0.038f, 0.60f),
                ColorB = new Color(0.028f, 0.038f, 0.062f, 0.50f),
                SizeMin = 34f, SizeMax = 64f, LifeMin = 7f, LifeMax = 12f,
                Gravity = -0.012f, NoiseStrength = 1.4f, NoiseFrequency = 0.05f,
                FadeInOut = true, GrowOverLife = 1.35f, MaxParticles = 900,
            });
        }

        private void BuildDarkRing()
        {
            const float outer = 720f;
            float x0 = -Margin, x1 = _w + Margin, z0 = -Margin, z1 = _d + Margin;
            float ox0 = x0 - outer, ox1 = x1 + outer, oz0 = z0 - outer, oz1 = z1 + outer;
            Color inner = new Color(0.03f, 0.045f, 0.07f, 0f);
            Color outerC = new Color(0.015f, 0.025f, 0.045f, 0.96f);

            var verts = new Vector3[]
            {
                new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1),
                new Vector3(ox0, 0, oz0), new Vector3(ox1, 0, oz0), new Vector3(ox1, 0, oz1), new Vector3(ox0, 0, oz1),
            };
            var cols = new Color[] { inner, inner, inner, inner, outerC, outerC, outerC, outerC };
            var uvs = new Vector2[8];
            for (int i = 0; i < 8; i++) uvs[i] = new Vector2(0.5f, 0.5f); // sample the soft dot's solid center
            var tris = new int[]
            {
                0, 4, 5, 0, 5, 1,   // south band
                1, 5, 6, 1, 6, 2,   // east band
                2, 6, 7, 2, 7, 3,   // north band
                3, 7, 4, 3, 4, 0,   // west band
            };

            var mesh = new Mesh { vertices = verts, colors = cols, uv = uvs, triangles = tris };
            mesh.RecalculateBounds();

            var go = new GameObject("fog-dark-sea");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, 0.22f, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = VfxToolkit.DotMaterial; // transparent, vertex-color driven
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private void Update()
        {
            if (_gloom == null) return;
            // Feed the wall: billows appear at random points along the rim.
            int n = Mathf.Min(6, Mathf.CeilToInt(46f * Time.deltaTime));
            for (int i = 0; i < n; i++)
            {
                float t = Random.value * _perimeter;
                Vector3 p;
                if (t < _w) p = new Vector3(t, 0, -Margin);
                else if (t < _w + _d) p = new Vector3(_w + Margin, 0, t - _w);
                else if (t < 2f * _w + _d) p = new Vector3(t - _w - _d, 0, _d + Margin);
                else p = new Vector3(-Margin, 0, t - 2f * _w - _d);

                p += new Vector3(Random.Range(-14f, 34f) * Mathf.Sign(p.x - _w * 0.5f) * 0.5f, Random.Range(2f, 16f), 0);
                VfxToolkit.Emit(_gloom, p, 1, 12f, new Vector3(Random.Range(-0.4f, 0.4f), 0.25f, Random.Range(-0.4f, 0.4f)));
            }
        }
    }
}
