using UnityEngine;

namespace ADayAtTheZoo.WaterStyles
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WaterSurfaceMesh : MonoBehaviour
    {
        [Min(4f)] public float width = 18f;
        [Min(4f)] public float length = 12f;
        [Range(8, 96)] public int radialSegments = 56;
        [Range(3, 48)] public int rings = 20;
        [Range(0f, 0.2f)] public float edgeIrregularity = 0.075f;
        [Range(0f, 0.25f)] public float baseRipple = 0.02f;

        MeshFilter meshFilter;
        Mesh generatedMesh;

        void OnEnable()
        {
            EnsureMesh();
        }

        void OnValidate()
        {
            EnsureMesh();
        }

        public void EnsureMesh()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshFilter == null)
            {
                return;
            }

            if (generatedMesh != null)
            {
                DestroyImmediate(generatedMesh);
            }

            generatedMesh = BuildMesh(width, length, radialSegments, rings, edgeIrregularity, baseRipple);
            meshFilter.sharedMesh = generatedMesh;
        }

        static Mesh BuildMesh(float width, float length, int segments, int ringCount, float irregularity, float ripple)
        {
            segments = Mathf.Max(8, segments);
            ringCount = Mathf.Max(3, ringCount);

            var vertices = new Vector3[1 + segments * ringCount];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * 3 + segments * (ringCount - 1) * 6];

            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);

            for (int ring = 1; ring <= ringCount; ring++)
            {
                float ringT = ring / (float)ringCount;
                float smoothedT = Mathf.SmoothStep(0f, 1f, ringT);
                float edge = 1f
                    + irregularity * Mathf.Sin(ringT * 4.2f)
                    + irregularity * 0.55f * Mathf.Sin(ringT * 9.1f + 1.7f);

                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment / (float)segments * Mathf.PI * 2f;
                    float ringNoise = 1f
                        + irregularity * 0.52f * Mathf.Sin(angle * 5f + ringT * 4.1f)
                        + irregularity * 0.24f * Mathf.Sin(angle * 9f - ringT * 2.8f);
                    float radius = smoothedT * edge * ringNoise;
                    float x = Mathf.Cos(angle) * radius * width * 0.5f;
                    float z = Mathf.Sin(angle) * radius * length * 0.5f;
                    float y = Mathf.Sin(angle * 3f + ringT * 8f) * ripple * ringT;

                    int index = 1 + (ring - 1) * segments + segment;
                    vertices[index] = new Vector3(x, y, z);
                    uv[index] = new Vector2(x / Mathf.Max(width, 0.001f) + 0.5f, z / Mathf.Max(length, 0.001f) + 0.5f);
                }
            }

            int tri = 0;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                int firstRing = 1 + segment;
                int firstRingNext = 1 + next;
                triangles[tri++] = 0;
                triangles[tri++] = firstRingNext;
                triangles[tri++] = firstRing;
            }

            for (int ring = 1; ring < ringCount; ring++)
            {
                int innerStart = 1 + (ring - 1) * segments;
                int outerStart = 1 + ring * segments;

                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    int inner = innerStart + segment;
                    int innerNext = innerStart + next;
                    int outer = outerStart + segment;
                    int outerNext = outerStart + next;

                    triangles[tri++] = inner;
                    triangles[tri++] = innerNext;
                    triangles[tri++] = outerNext;

                    triangles[tri++] = inner;
                    triangles[tri++] = outerNext;
                    triangles[tri++] = outer;
                }
            }

            var mesh = new Mesh
            {
                name = "MM Procedural Low-poly Water Surface",
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
