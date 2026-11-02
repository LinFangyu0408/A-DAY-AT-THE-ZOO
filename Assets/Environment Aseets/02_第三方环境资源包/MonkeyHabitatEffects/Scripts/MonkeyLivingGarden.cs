using System.Collections.Generic;
using UnityEngine;

namespace ADayAtTheZoo.Habitat
{
    /// <summary>
    /// Terrain-following flower garden driven by the four-state automaton.
    /// Blooms spread to neighbours, and periodic wind gusts sow dormant cells.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonkeyLivingGarden : MonoBehaviour
    {
        [Header("Planting area")]
        public int columns = 12;
        public int rows = 8;
        public float spacing = 1.35f;
        public bool showRuleTiles = true;

        [Header("Automaton")]
        [Range(1, 4)] public int neighbourThreshold = 2;
        public float budSeconds = 3.2f;
        public float bloomSeconds = 5.8f;
        public float recoverySeconds = 4.2f;
        public float evolutionSpeed = 1f;

        [Header("Wind seeding")]
        public bool windSeeding = true;
        public float windSeedInterval = 4.5f;
        public Vector3 windDirection = new Vector3(0.83f, 0f, 0.56f);

        public int WindSown { get; private set; }
        public int Sown { get; private set; }
        public MonkeyGardenAutomaton Simulation { get; private set; }

        private Vector3 anchor;
        private Vector3[] treeBases;
        private Vector3[] groundPositions;
        private Vector3[] groundNormals;
        private bool[] soil;
        private Mesh flowerMesh;
        private Mesh ruleMesh;
        private Material gardenMaterial;
        private GameObject flowerObject;
        private GameObject ruleObject;
        private float accumulator;
        private float windClock;
        private bool initialized;

        public void Configure(Vector3 plantingCenter, Vector3[] existingTrees, Vector3 wind)
        {
            anchor = plantingCenter;
            treeBases = existingTrees;
            if (wind.sqrMagnitude > 0.001f)
                windDirection = wind.normalized;
        }

        private void Start()
        {
            if (!initialized)
                InitializeGarden();
        }

        private void InitializeGarden()
        {
            initialized = true;
            columns = Mathf.Clamp(columns, 4, 20);
            rows = Mathf.Clamp(rows, 3, 16);
            EnsureTerrainColliders();

            int count = columns * rows;
            groundPositions = new Vector3[count];
            groundNormals = new Vector3[count];
            soil = new bool[count];
            float halfWidth = (columns - 1) * spacing * 0.5f;
            float halfDepth = (rows - 1) * spacing * 0.5f;

            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int index = z * columns + x;
                    float worldX = anchor.x + x * spacing - halfWidth;
                    float worldZ = anchor.z + z * spacing - halfDepth;
                    Vector3 sample = new Vector3(worldX, anchor.y + 100f, worldZ);
                    Vector3 normal = Vector3.up;
                    if (Physics.Raycast(sample, Vector3.down, out RaycastHit hit, 220f,
                                        ~0, QueryTriggerInteraction.Ignore))
                    {
                        groundPositions[index] = hit.point + hit.normal * 0.035f;
                        normal = hit.normal;
                        soil[index] = true;
                    }
                    else
                    {
                        float fallback = anchor.y +
                                         (worldX - anchor.x) * 0.16f +
                                         (worldZ - anchor.z) * 0.08f;
                        groundPositions[index] = new Vector3(worldX, fallback, worldZ);
                        soil[index] = true;
                    }

                    if (treeBases != null)
                    {
                        for (int t = 0; t < treeBases.Length; t++)
                        {
                            Vector2 delta = new Vector2(
                                worldX - treeBases[t].x,
                                worldZ - treeBases[t].z);
                            if (delta.sqrMagnitude < 2.2f * 2.2f)
                            {
                                soil[index] = false;
                                break;
                            }
                        }
                    }
                    groundNormals[index] = normal;
                }
            }

            Simulation = new MonkeyGardenAutomaton(columns, rows, soil);
            Simulation.Sow(columns / 2, rows / 2, 1);
            Simulation.Sow(Mathf.Clamp(columns / 3, 0, columns - 1),
                           Mathf.Clamp(rows / 3, 0, rows - 1), 0);

            gardenMaterial = BuildMaterial();
            flowerMesh = new Mesh { name = "Monkey Habitat Living Flowers" };
            flowerMesh.MarkDynamic();
            ruleMesh = new Mesh { name = "Monkey Habitat Garden Rule Colors" };
            ruleMesh.MarkDynamic();

            flowerObject = new GameObject("Living Flower Automaton");
            flowerObject.transform.SetParent(transform, false);
            flowerObject.AddComponent<MeshFilter>().sharedMesh = flowerMesh;
            var flowerRenderer = flowerObject.AddComponent<MeshRenderer>();
            flowerRenderer.sharedMaterial = gardenMaterial;
            flowerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ruleObject = new GameObject("Garden Rule Coloring");
            ruleObject.transform.SetParent(transform, false);
            ruleObject.AddComponent<MeshFilter>().sharedMesh = ruleMesh;
            var ruleRenderer = ruleObject.AddComponent<MeshRenderer>();
            ruleRenderer.sharedMaterial = gardenMaterial;
            ruleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ruleRenderer.receiveShadows = false;

            RebuildMeshes();
        }

        private void Update()
        {
            if (Simulation == null || !initialized)
                return;

            accumulator += Mathf.Min(Time.deltaTime, 0.1f) * Mathf.Max(0.1f, evolutionSpeed);
            if (accumulator < 0.16f)
                return;

            float simStep = accumulator;
            accumulator = 0f;
            Simulation.Tick(simStep, neighbourThreshold, budSeconds, bloomSeconds, recoverySeconds);

            if (windSeeding)
            {
                windClock += simStep;
                if (windClock >= windSeedInterval)
                {
                    windClock = 0f;
                    TryWindSow();
                }
            }
            RebuildMeshes();
        }

        private void TryWindSow()
        {
            int blooms = Simulation.BloomCount;
            if (blooms <= 0)
            {
                // Keep the installation alive when a whole wave has recovered.
                int[] candidates = new int[Simulation.Cells.Length];
                int candidateCount = 0;
                for (int i = 0; i < Simulation.Cells.Length; i++)
                {
                    if (Simulation.Soil[i] &&
                        Simulation.Cells[i] == MonkeyGardenAutomaton.Phase.Rest)
                        candidates[candidateCount++] = i;
                }
                if (candidateCount > 0)
                {
                    int seed = candidates[Random.Range(0, candidateCount)];
                    Simulation.Sow(seed % columns, seed / columns, 0);
                    WindSown++;
                    Sown++;
                }
                return;
            }

            int pick = Random.Range(0, blooms);
            int source = -1;
            for (int i = 0; i < Simulation.Cells.Length; i++)
            {
                if (Simulation.Cells[i] != MonkeyGardenAutomaton.Phase.Bloom)
                    continue;
                if (pick-- == 0)
                {
                    source = i;
                    break;
                }
            }
            if (source < 0)
                return;

            int sourceX = source % columns;
            int sourceY = source / columns;
            int stepX = Mathf.RoundToInt(windDirection.x * 2.2f);
            int stepY = Mathf.RoundToInt(windDirection.z * 2.2f);
            stepX += stepX == 0 ? 1 : 0;
            stepY += stepY == 0 ? 1 : 0;

            int targetX = Mathf.Clamp(sourceX + stepX, 0, columns - 1);
            int targetY = Mathf.Clamp(sourceY + stepY, 0, rows - 1);
            int target = targetY * columns + targetX;
            if (Simulation.Soil[target] && Simulation.Cells[target] == MonkeyGardenAutomaton.Phase.Rest)
            {
                Simulation.Sow(targetX, targetY, 0);
                WindSown++;
                Sown++;
            }
        }

        private void RebuildMeshes()
        {
            var vertices = new List<Vector3>(4096);
            var colors = new List<Color>(4096);
            var triangles = new List<int>(6144);

            for (int i = 0; i < groundPositions.Length; i++)
            {
                if (!soil[i])
                    continue;
                MonkeyGardenAutomaton.Phase phase = Simulation.Cells[i];
                float age = Simulation.Ages[i];
                float growth = GetGrowth(phase, age);
                float bloom = GetBloom(phase, age);
                AppendFlower(vertices, colors, triangles, i, phase, growth, bloom, age);
            }

            SetMesh(flowerMesh, vertices, colors, triangles);
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
            for (int i = 0; i < groundPositions.Length; i++)
            {
                if (soil[i] && Simulation.Cells[i] != MonkeyGardenAutomaton.Phase.Rest)
                    AppendRuleTile(vertices, colors, triangles, i,
                        Simulation.Cells[i], 1f, GetBloom(Simulation.Cells[i], Simulation.Ages[i]));
            }
            SetMesh(ruleMesh, vertices, colors, triangles);
            if (ruleObject != null)
                ruleObject.SetActive(showRuleTiles);
        }

        private void SetMesh(Mesh mesh, List<Vector3> vertices, List<Color> colors, List<int> triangles)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private float GetGrowth(MonkeyGardenAutomaton.Phase phase, float age)
        {
            switch (phase)
            {
                case MonkeyGardenAutomaton.Phase.Bud:
                    return Mathf.Lerp(0.16f, 0.86f, Mathf.Clamp01(age / Mathf.Max(0.2f, budSeconds)));
                case MonkeyGardenAutomaton.Phase.Bloom:
                    return 1f;
                case MonkeyGardenAutomaton.Phase.Recover:
                    return Mathf.Lerp(1f, 0.22f,
                        Mathf.Clamp01(age / Mathf.Max(0.2f, recoverySeconds)));
                default:
                    return 0f;
            }
        }

        private float GetBloom(MonkeyGardenAutomaton.Phase phase, float age)
        {
            switch (phase)
            {
                case MonkeyGardenAutomaton.Phase.Bud:
                    return Mathf.Clamp01(age / Mathf.Max(0.2f, budSeconds) * 1.35f - 0.35f);
                case MonkeyGardenAutomaton.Phase.Bloom:
                    return 1f;
                case MonkeyGardenAutomaton.Phase.Recover:
                    return 1f - Mathf.Clamp01(age / Mathf.Max(0.2f, recoverySeconds));
                default:
                    return 0f;
            }
        }

        private void AppendRuleTile(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            int index,
            MonkeyGardenAutomaton.Phase phase,
            float growth,
            float bloom)
        {
            BuildBasis(groundNormals[index], out Vector3 tangent, out Vector3 bitangent);
            Vector3 center = groundPositions[index] + groundNormals[index] * 0.015f;
            float half = spacing * 0.34f;
            Color color = RuleColor(phase);
            color *= Mathf.Lerp(0.72f, 1.12f, Mathf.Max(growth, bloom * 0.8f));
            color.a = 0f;

            int start = vertices.Count;
            vertices.Add(center - tangent * half - bitangent * half);
            vertices.Add(center - tangent * half + bitangent * half);
            vertices.Add(center + tangent * half + bitangent * half);
            vertices.Add(center + tangent * half - bitangent * half);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private void AppendFlower(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            int index,
            MonkeyGardenAutomaton.Phase phase,
            float growth,
            float bloom,
            float age)
        {
            if (phase == MonkeyGardenAutomaton.Phase.Rest || growth < 0.03f)
                return;

            Vector3 root = groundPositions[index];
            float baseHeight = 1.05f + Hash01(index * 19 + 7) * 0.38f;
            float height = baseHeight * growth;
            Vector3 top = root + Vector3.up * height;
            Vector3 bend = new Vector3(
                Mathf.Sin(age * 0.7f + index) * 0.035f,
                0f,
                Mathf.Cos(age * 0.6f + index * 0.7f) * 0.035f) * growth;
            top += bend;
            Vector3 tangent = Vector3.right;
            Vector3 bitangent = Vector3.forward;
            float stemWidth = 0.032f + bloom * 0.014f;
            Color stemBottom = new Color(0.10f, 0.28f, 0.08f, 0.1f);
            Color stemTop = new Color(0.22f, 0.52f, 0.12f, 0.9f);
            AppendQuad(vertices, colors, triangles,
                root - tangent * stemWidth, root + tangent * stemWidth,
                top + tangent * stemWidth, top - tangent * stemWidth,
                stemBottom, stemBottom, stemTop, stemTop);
            AppendQuad(vertices, colors, triangles,
                root - bitangent * stemWidth, root + bitangent * stemWidth,
                top + bitangent * stemWidth, top - bitangent * stemWidth,
                stemBottom, stemBottom, stemTop, stemTop);

            Color leafColor = new Color(0.22f, 0.58f, 0.13f, 0.55f);
            Vector3 leafRoot = Vector3.Lerp(root, top, 0.48f);
            Vector3 leafTip = leafRoot + tangent * (0.28f * growth) + Vector3.up * 0.055f;
            AppendTriangle(vertices, colors, triangles,
                leafRoot, leafRoot + bitangent * 0.06f, leafTip, leafColor);
            AppendTriangle(vertices, colors, triangles,
                leafRoot, leafRoot - bitangent * 0.06f,
                leafRoot - tangent * (0.24f * growth) + Vector3.up * 0.035f, leafColor);

            if (bloom <= 0.03f)
            {
                Color budColor = new Color(0.32f, 0.70f, 0.20f, 1f);
                AppendOctahedron(vertices, colors, triangles, top,
                    0.085f + 0.06f * bloom, budColor);
                return;
            }

            Color flowerColor = FlowerColor(index, phase);
            flowerColor *= Mathf.Lerp(0.86f, 1.08f, bloom);
            float petalLength = (0.21f + Hash01(index * 13 + 3) * 0.08f) * bloom;
            float petalWidth = 0.115f * bloom;
            for (int petal = 0; petal < 5; petal++)
            {
                float angle = petal * Mathf.PI * 2f / 5f + Hash01(index * 37) * 0.8f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                AppendTriangle(vertices, colors, triangles,
                    top,
                    top + direction * petalLength + side * petalWidth + Vector3.up * 0.025f,
                    top + direction * petalLength - side * petalWidth + Vector3.up * 0.025f,
                    flowerColor);
            }
            AppendOctahedron(vertices, colors, triangles, top + Vector3.up * 0.025f,
                0.065f, new Color(1f, 0.74f, 0.16f, 1f));
        }

        private static void AppendQuad(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Color ca,
            Color cb,
            Color cc,
            Color cd)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            colors.Add(ca);
            colors.Add(cb);
            colors.Add(cc);
            colors.Add(cd);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static void AppendTriangle(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Color color)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private static void AppendOctahedron(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector3 center,
            float radius,
            Color color)
        {
            Vector3 up = Vector3.up * radius;
            Vector3 down = Vector3.down * radius;
            Vector3 right = Vector3.right * radius;
            Vector3 left = Vector3.left * radius;
            Vector3 forward = Vector3.forward * radius;
            Vector3 back = Vector3.back * radius;
            Vector3[] around = { right, forward, left, back };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = around[i];
                Vector3 b = around[(i + 1) % 4];
                AppendTriangle(vertices, colors, triangles, center + up, center + a, center + b, color);
                AppendTriangle(vertices, colors, triangles, center + down, center + b, center + a, color);
            }
        }

        private static void BuildBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            normal = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
            tangent = Vector3.Cross(normal, Vector3.forward);
            if (tangent.sqrMagnitude < 0.01f)
                tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            bitangent = Vector3.Cross(normal, tangent).normalized;
        }

        private static Color RuleColor(MonkeyGardenAutomaton.Phase phase)
        {
            switch (phase)
            {
                case MonkeyGardenAutomaton.Phase.Bud:
                    return new Color(0.34f, 0.62f, 0.21f);
                case MonkeyGardenAutomaton.Phase.Bloom:
                    return new Color(0.68f, 0.82f, 0.30f);
                case MonkeyGardenAutomaton.Phase.Recover:
                    return new Color(0.58f, 0.40f, 0.22f);
                default:
                    return new Color(0.34f, 0.37f, 0.20f);
            }
        }

        private static Color FlowerColor(int index, MonkeyGardenAutomaton.Phase phase)
        {
            if (phase == MonkeyGardenAutomaton.Phase.Recover)
                return new Color(0.48f, 0.35f, 0.20f);
            switch (index % 4)
            {
                case 0:
                    return new Color(0.90f, 0.38f, 0.50f);
                case 1:
                    return new Color(0.93f, 0.72f, 0.22f);
                case 2:
                    return new Color(0.55f, 0.60f, 0.92f);
                default:
                    return new Color(0.82f, 0.52f, 0.86f);
            }
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                uint hash = (uint)value;
                hash ^= hash >> 16;
                hash *= 0x7feb352d;
                hash ^= hash >> 15;
                hash *= 0x846ca68b;
                hash ^= hash >> 16;
                return (hash & 0x00ffffff) / 16777215f;
            }
        }

        private Material BuildMaterial()
        {
            Material template = Resources.Load<Material>("MonkeyWindColor");
            Shader shader = template != null ? template.shader : Shader.Find("Zoo/Monkey Wind Color");
            if (shader == null)
                shader = Shader.Find("Zoo/High Saturation Low Poly");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            var material = template != null
                ? new Material(template)
                : new Material(shader);
            material.name = "Monkey Habitat Living Garden";
            material.SetVector("_WindDirection", new Vector4(windDirection.x, 0f, windDirection.z, 0f));
            material.SetFloat("_WindStrength", 0.11f);
            material.SetFloat("_WindSpeed", 1.05f);
            material.SetFloat("_WindFrequency", 0.32f);
            material.SetFloat("_Flutter", 0.24f);
            material.SetFloat("_Saturation", 1.08f);
            material.SetFloat("_Brightness", 1.05f);
            material.enableInstancing = false;
            return material;
        }

        private void EnsureTerrainColliders()
        {
            MeshFilter[] filters = FindObjectsByType<MeshFilter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int added = 0;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter.name != "samor_4077_599" &&
                    filter.name != "samor_4077_600")
                    continue;
                if (filter.sharedMesh == null)
                    continue;
                if (filter.GetComponent<MeshCollider>() == null)
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    added++;
                }
            }

            // Fallback for a rebuilt scene where the source mesh names changed.
            for (int i = 0; i < filters.Length && added == 0; i++)
            {
                MeshFilter filter = filters[i];
                if (filter.name == "Living Flower Automaton" ||
                    filter.name == "Garden Rule Coloring" ||
                    filter.name.StartsWith("Boid ") ||
                    filter.name.StartsWith("Cloud ") ||
                    filter.name.StartsWith("samor_Tree_") ||
                    filter.sharedMesh == null)
                    continue;
                Bounds bounds = filter.sharedMesh.bounds;
                if (bounds.size.x < 8f || bounds.size.z < 8f)
                    continue;
                if (filter.GetComponent<MeshCollider>() == null)
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    added++;
                }
            }
            if (added > 0)
                Physics.SyncTransforms();
        }

        private void OnDestroy()
        {
            if (flowerMesh != null)
                Destroy(flowerMesh);
            if (ruleMesh != null)
                Destroy(ruleMesh);
            if (gardenMaterial != null)
                Destroy(gardenMaterial);
        }
    }
}
