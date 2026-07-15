// SLATE — the sea. A gently tessellated plane at y = 0 with the depth-tinted
// water shader; the swell is done in the shader's vertex stage.
using UnityEngine;
using Slate.Sim;

namespace Slate.Game
{
    public class WaterPlane : MonoBehaviour
    {
        public void Build(World w)
        {
            const int gx = 96, gz = 60;
            float margin = TerrainSampler.CellSize * 6f;
            float width = TerrainSampler.WorldWidth + margin * 2f;
            float depth = TerrainSampler.WorldDepth + margin * 2f;

            var verts = new Vector3[(gx + 1) * (gz + 1)];
            for (int z = 0; z <= gz; z++)
                for (int x = 0; x <= gx; x++)
                    verts[z * (gx + 1) + x] = new Vector3(
                        -margin + width * x / gx, 0f, -margin + depth * z / gz);

            var tris = new int[gx * gz * 6];
            int ti = 0;
            for (int z = 0; z < gz; z++)
                for (int x = 0; x < gx; x++)
                {
                    int i = z * (gx + 1) + x;
                    tris[ti++] = i; tris[ti++] = i + gx + 1; tris[ti++] = i + 1;
                    tris[ti++] = i + 1; tris[ti++] = i + gx + 1; tris[ti++] = i + gx + 2;
                }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Slate/Water"));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
    }
}
