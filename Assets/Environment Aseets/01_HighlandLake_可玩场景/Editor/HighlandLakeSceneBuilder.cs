using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = System.Random;

public static class HighlandLakeSceneBuilder
{
    private const string Root = "Assets/Environment Aseets/01_HighlandLake_可玩场景";
    private const string ScenePath = Root + "/Scenes/HighlandLake.unity";
    private const string MaterialPath = Root + "/Materials";
    private const string LowPolyShaderPath = Root + "/Shaders/HighSaturationLowPoly.shader";
    private const string WaterfallShaderPath = Root + "/Shaders/StylizedWaterfall.shader";
    private const string DeepLakeShaderPath = Root + "/Shaders/DeepLakeWater.shader";
    private const string WindGrassShaderPath = Root + "/Shaders/LowPolyWindGrass.shader";
    private const string ForestRoot = Root + "/Meshes";
    private const string IslandRoot = "Assets/RG-PolyIslandPack/Models";
    private const float LakeFloorHeight = -1.25f;
    private const float LakeWaterHeight = -0.18f;

    private static Shader lowPolyShader;
    private static Shader waterfallShader;
    private static Shader deepLakeShader;
    private static Shader windGrassShader;
    private static Material terrainMaterial;
    private static Material cliffMaterial;
    private static Material pathMaterial;
    private static Material mountainMaterial;
    private static Material cloudMaterial;
    private static Material windGrassMaterial;
    private static readonly Dictionary<string, Material> styledMaterials =
        new Dictionary<string, Material>();
    private static int objectCounter;
    private static StringBuilder report;
    private static bool automatedBuild;

    [MenuItem("Tools/Environment/Create Highland Lake Scene")]
    public static void BuildScene()
    {
        EnsureFolders();
        lowPolyShader = LoadRequired<Shader>(LowPolyShaderPath);
        waterfallShader = LoadRequired<Shader>(WaterfallShaderPath);
        deepLakeShader = LoadRequired<Shader>(DeepLakeShaderPath);
        windGrassShader = LoadRequired<Shader>(WindGrassShaderPath);
        styledMaterials.Clear();
        objectCounter = 0;
        report = new StringBuilder();
        report.AppendLine("Highland Lake scene build report");
        report.AppendLine("Unity " + Application.unityVersion);

        bool batchMode = Application.isBatchMode;
        if (!batchMode && !automatedBuild &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            batchMode ? NewSceneMode.Single : NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);

        CreateMaterials();
        CreateEnvironmentSettings();

        GameObject world = new GameObject("Highland Lake");
        SceneManager.MoveGameObjectToScene(world, scene);

        Transform terrainRoot = CreateGroup("01 Terrain and Cliffs", world.transform);
        Transform waterRoot = CreateGroup("02 Water", world.transform);
        Transform vegetationRoot = CreateGroup("03 Vegetation", world.transform);
        Transform rocksRoot = CreateGroup("04 Rocks and Bridge", world.transform);
        Transform atmosphereRoot = CreateGroup("05 Atmosphere", world.transform);

        CreateTerrain(terrainRoot);
        CreatePath(terrainRoot);
        CreateCliffWall(terrainRoot);
        CreateLakeBed(waterRoot);
        CreateLake(waterRoot);
        CreateWaterfall(waterRoot);
        SpawnVegetation(vegetationRoot);
        CreateWindGrass(vegetationRoot);
        SpawnRocks(rocksRoot);
        CreateBridge(rocksRoot);
        CreateCameraAndSun(atmosphereRoot);
        CreateClouds(atmosphereRoot);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        CapturePreview(scene);
        CaptureTopDownPreview(scene);
        WriteReport();

        if (!batchMode && previousScene.IsValid())
        {
            EditorSceneManager.SetActiveScene(previousScene);
        }

        Debug.Log("Highland Lake scene created: " + ScenePath);
    }

    public static void BuildSceneAutomated()
    {
        automatedBuild = true;
        try
        {
            BuildScene();
        }
        finally
        {
            automatedBuild = false;
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder(Root);
        EnsureFolder(Root + "/Scenes");
        EnsureFolder(MaterialPath);
        EnsureFolder(Root + "/Shaders");
        EnsureFolder(Root + "/Editor");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static T LoadRequired<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new InvalidOperationException("Required asset missing: " + path);
        }

        return asset;
    }

    private static Transform CreateGroup(string name, Transform parent)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static void CreateMaterials()
    {
        terrainMaterial = CreateLowPolyMaterial(
            "Ground Meadow",
            Color.white,
            1.3f,
            0.94f,
            0.38f,
            1);

        pathMaterial = CreateLowPolyMaterial(
            "Warm Earth Path",
            new Color(0.86f, 0.72f, 0.42f),
            1.3f,
            1.02f,
            0.38f,
            1);

        mountainMaterial = CreateLowPolyMaterial(
            "Distant Mountain Haze",
            Color.white,
            0.9f,
            1.02f,
            0.52f,
            1);

        cliffMaterial = CreateLowPolyMaterial(
            "Cliff Rock",
            new Color(0.42f, 0.5f, 0.52f),
            0.82f,
            0.86f,
            0.34f,
            1);

        cloudMaterial = CreateLowPolyMaterial(
            "Soft Cloud",
            new Color(1f, 1f, 1f),
            0f,
            1.02f,
            0.88f,
            0);
        cloudMaterial.SetFloat("_ToonThreshold", -0.5f);

        CreateWaterMaterial();
        CreateWaterfallMaterial();
        CreateSkyMaterial();
        CreateWindGrassMaterial();
    }

    private static Material CreateLowPolyMaterial(
        string name,
        Color color,
        float saturation,
        float brightness,
        float ambient,
        float useVertexColor)
    {
        string path = MaterialPath + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(lowPolyShader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = lowPolyShader;
        material.name = name;
        material.SetColor("_Color", color);
        material.SetColor("_Tint", Color.white);
        material.SetFloat("_Saturation", saturation);
        material.SetFloat("_Brightness", brightness);
        material.SetFloat("_AmbientStrength", ambient);
        material.SetFloat("_UseVertexColor", useVertexColor);
        material.SetFloat("_ToonThreshold", 0.12f);
        material.SetFloat("_ToonSoftness", 0.09f);
        material.SetFloat("_RimStrength", useVertexColor > 0.5f ? 0f : 0.08f);
        material.SetColor("_RimColor", new Color(0.75f, 1f, 0.64f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateWaterMaterial()
    {
        string destination = MaterialPath + "/Lake Water.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(destination);
        if (material == null)
        {
            material = new Material(deepLakeShader);
            AssetDatabase.CreateAsset(material, destination);
        }

        material.shader = deepLakeShader;
        material.SetColor("_DeepColor", new Color(0.025f, 0.22f, 0.5f, 1f));
        material.SetColor("_MidColor", new Color(0.045f, 0.46f, 0.72f, 1f));
        material.SetColor("_ShallowColor", new Color(0.18f, 0.74f, 0.86f, 1f));
        material.SetColor("_SkyColor", new Color(0.5f, 0.84f, 0.98f, 1f));
        material.SetColor("_FoamColor", new Color(0.72f, 0.96f, 0.95f, 1f));
        material.SetFloat("_WaveHeight", 0.018f);
        material.SetFloat("_WaveScale", 2.8f);
        material.SetFloat("_WaveSpeed", 0.68f);
        material.SetFloat("_FoamWidth", 0.105f);
        material.SetFloat("_FoamStrength", 0.52f);
        EditorUtility.SetDirty(material);
    }

    private static void CreateWindGrassMaterial()
    {
        string path = MaterialPath + "/Low Poly Wind Grass.mat";
        windGrassMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (windGrassMaterial == null)
        {
            windGrassMaterial = new Material(windGrassShader);
            AssetDatabase.CreateAsset(windGrassMaterial, path);
        }

        windGrassMaterial.shader = windGrassShader;
        windGrassMaterial.SetColor("_BaseColor", new Color(0.09f, 0.36f, 0.055f, 1f));
        windGrassMaterial.SetColor("_TipColor", new Color(0.47f, 0.86f, 0.18f, 1f));
        windGrassMaterial.SetVector("_WindDirection", new Vector4(0.83f, 0f, 0.56f, 0f));
        windGrassMaterial.SetFloat("_WindStrength", 0.072f);
        windGrassMaterial.SetFloat("_WindSpeed", 1.08f);
        windGrassMaterial.SetFloat("_WindFrequency", 0.39f);
        windGrassMaterial.SetFloat("_Flutter", 0.24f);
        windGrassMaterial.SetFloat("_Ambient", 0.6f);
        windGrassMaterial.SetFloat("_SunStrength", 0.55f);
        EditorUtility.SetDirty(windGrassMaterial);
    }

    private static void CreateWaterfallMaterial()
    {
        string path = MaterialPath + "/Waterfall.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(waterfallShader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = waterfallShader;
        material.SetColor("_TopColor", new Color(0.58f, 0.98f, 1f, 1));
        material.SetColor("_BottomColor", new Color(0.04f, 0.55f, 0.78f, 1));
        material.SetColor("_FoamColor", new Color(0.92f, 1f, 1f, 1));
        material.SetFloat("_Speed", 1.15f);
        material.SetFloat("_StripeDensity", 19f);
        material.SetFloat("_Opacity", 0.88f);
        EditorUtility.SetDirty(material);
    }

    private static void CreateSkyMaterial()
    {
        string path = MaterialPath + "/Highland Sky.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (material == null)
        {
            material = new Material(skyShader);
            AssetDatabase.CreateAsset(material, path);
        }

        if (skyShader != null)
        {
            material.shader = skyShader;
            material.SetColor("_SkyTint", new Color(0.44f, 0.79f, 0.98f));
            material.SetColor("_GroundColor", new Color(0.62f, 0.8f, 0.78f));
            material.SetFloat("_AtmosphereThickness", 0.58f);
            material.SetFloat("_Exposure", 1.22f);
            material.SetFloat("_SunSize", 0.015f);
            EditorUtility.SetDirty(material);
        }

        RenderSettings.skybox = material;
    }

    private static void CreateEnvironmentSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.64f, 0.85f, 0.98f);
        RenderSettings.ambientEquatorColor = new Color(0.47f, 0.7f, 0.58f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.38f, 0.24f);
        RenderSettings.ambientIntensity = 1.05f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.64f, 0.84f, 0.9f);
        RenderSettings.fogStartDistance = 54f;
        RenderSettings.fogEndDistance = 145f;
    }

    private static void CreateTerrain(Transform parent)
    {
        GameObject ground = CreateTerrainObject(
            "Meadow and Outer Slopes",
            -48f, 48f, -42f, 52f, 56, 54, GroundHeight, GroundColor, parent);
        Bounds groundBounds = RendererBounds(ground);
        report.AppendLine(
            "Terrain bounds min=" + groundBounds.min.ToString("F2") +
            " max=" + groundBounds.max.ToString("F2"));
        MeshCollider collider = ground.AddComponent<MeshCollider>();
        collider.sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;

        Vector3[] centers =
        {
            new Vector3(-68, -1, 76), new Vector3(-47, -1, 82),
            new Vector3(-24, -1, 88), new Vector3(0, -1, 94),
            new Vector3(24, -1, 88), new Vector3(47, -1, 82),
            new Vector3(68, -1, 76), new Vector3(-72, -1, 40),
            new Vector3(72, -1, 40), new Vector3(-60, -1, 7),
            new Vector3(60, -1, 7)
        };
        float[] heights = { 25, 31, 28, 34, 29, 32, 26, 22, 24, 17, 20 };
        float[] radii = { 21, 24, 21, 26, 22, 24, 21, 18, 18, 16, 17 };

        for (int i = 0; i < centers.Length; i++)
        {
            CreateMountain(
                "Distant Mountain " + (i + 1),
                centers[i],
                radii[i],
                heights[i],
                i * 1.73f,
                parent);
        }
    }

    private static GameObject CreateTerrainObject(
        string name,
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        int cellsX,
        int cellsZ,
        Func<float, float, float> height,
        Func<float, float, float, Color> color,
        Transform parent)
    {
        List<Vector3> vertices = new List<Vector3>(cellsX * cellsZ * 6);
        List<int> triangles = new List<int>(cellsX * cellsZ * 6);
        List<Color> colors = new List<Color>(cellsX * cellsZ * 6);

        for (int z = 0; z < cellsZ; z++)
        {
            float z0 = Mathf.Lerp(minZ, maxZ, z / (float)cellsZ);
            float z1 = Mathf.Lerp(minZ, maxZ, (z + 1) / (float)cellsZ);
            for (int x = 0; x < cellsX; x++)
            {
                float x0 = Mathf.Lerp(minX, maxX, x / (float)cellsX);
                float x1 = Mathf.Lerp(minX, maxX, (x + 1) / (float)cellsX);
                Vector3 a = new Vector3(x0, height(x0, z0), z0);
                Vector3 b = new Vector3(x0, height(x0, z1), z1);
                Vector3 c = new Vector3(x1, height(x1, z1), z1);
                Vector3 d = new Vector3(x1, height(x1, z0), z0);
                Color ab = color(x0, z0, a.y);
                Color bc = color(x0, z1, b.y);
                Color cd = color(x1, z1, c.y);
                Color da = color(x1, z0, d.y);

                if ((x + z) % 2 == 0)
                {
                    AddTriangle(vertices, triangles, colors, a, b, c, ab, bc, cd);
                    AddTriangle(vertices, triangles, colors, a, c, d, ab, cd, da);
                }
                else
                {
                    AddTriangle(vertices, triangles, colors, a, b, d, ab, bc, da);
                    AddTriangle(vertices, triangles, colors, b, c, d, bc, cd, da);
                }
            }
        }

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Mesh mesh = new Mesh { name = name + " Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = terrainMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return go;
    }

    private static void AddTriangle(
        List<Vector3> vertices,
        List<int> triangles,
        List<Color> colors,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Color ca,
        Color cb,
        Color cc)
    {
        int index = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        colors.Add(ca);
        colors.Add(cb);
        colors.Add(cc);
        triangles.Add(index);
        triangles.Add(index + 1);
        triangles.Add(index + 2);
    }

    private static float GroundHeight(float x, float z)
    {
        float lake = Mathf.Sqrt((x / 23f) * (x / 23f) + ((z - 7f) / 19f) * ((z - 7f) / 19f));
        float bank = SmoothRange(0.8f, 1.02f, lake);
        return Mathf.Lerp(LakeFloorHeight, 0f, bank);
    }

    private static float SmoothRange(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static Color GroundColor(float x, float z, float y)
    {
        float lake = Mathf.Sqrt((x / 23f) * (x / 23f) + ((z - 7f) / 19f) * ((z - 7f) / 19f));
        if (lake < 0.9f)
        {
            return Color.Lerp(
                new Color(0.075f, 0.32f, 0.31f),
                new Color(0.14f, 0.5f, 0.36f),
                Mathf.Clamp01((lake - 0.42f) * 1.75f));
        }

        if (lake < 1.12f)
        {
            return Color.Lerp(
                new Color(0.61f, 0.66f, 0.34f),
                new Color(0.48f, 0.88f, 0.27f),
                Mathf.InverseLerp(0.9f, 1.12f, lake));
        }

        float patch = Mathf.PerlinNoise(x * 0.055f + 3f, z * 0.055f + 9f);
        return Color.Lerp(
            new Color(0.31f, 0.73f, 0.19f),
            new Color(0.48f, 0.89f, 0.27f),
            0.45f + patch * 0.55f);
    }

    private static void CreateMountain(
        string name,
        Vector3 center,
        float radius,
        float height,
        float seed,
        Transform parent)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();
        int segments = 9;
        Color low = new Color(0.26f, 0.5f, 0.48f);
        Color high = new Color(0.42f, 0.7f, 0.68f);

        Vector3[] baseRing = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float wobble = 0.82f + Mathf.PerlinNoise(seed + i * 0.17f, seed * 0.7f) * 0.35f;
            baseRing[i] = center + new Vector3(
                Mathf.Cos(angle) * radius * wobble,
                0,
                Mathf.Sin(angle) * radius * wobble);
        }

        Vector3 peak = center + new Vector3(
            (Mathf.PerlinNoise(seed, 2f) - 0.5f) * radius * 0.32f,
            height,
            (Mathf.PerlinNoise(3f, seed) - 0.5f) * radius * 0.2f);

        for (int i = 0; i < segments; i++)
        {
            Vector3 a = baseRing[i];
            Vector3 b = baseRing[(i + 1) % segments];
            Vector3 mid = Vector3.Lerp(Vector3.Lerp(a, b, 0.5f), peak, 0.42f);
            mid.y += (Mathf.PerlinNoise(i + seed, seed * 0.2f) - 0.5f) * height * 0.16f;
            AddTriangle(vertices, triangles, colors, a, b, mid, low, low, high);
            AddTriangle(vertices, triangles, colors, b, peak, mid, high, high, high);
            AddTriangle(vertices, triangles, colors, mid, peak, a, high, high, low);
        }

        Mesh mesh = new Mesh { name = name + " Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mountainMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void CreatePath(Transform parent)
    {
        const int steps = 180;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int i = 0; i < steps; i++)
        {
            float a0 = (i / (float)steps) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)steps) * Mathf.PI * 2f;
            Vector3 p0 = PathPoint(a0);
            Vector3 p1 = PathPoint(a1);
            Vector3 t0 = (PathPoint(a0 + 0.01f) - PathPoint(a0 - 0.01f)).normalized;
            Vector3 t1 = (PathPoint(a1 + 0.01f) - PathPoint(a1 - 0.01f)).normalized;
            Vector3 n0 = new Vector3(-t0.z, 0, t0.x) * 1.65f;
            Vector3 n1 = new Vector3(-t1.z, 0, t1.x) * 1.65f;
            p0.y = GroundHeight(p0.x, p0.z) + 0.045f;
            p1.y = GroundHeight(p1.x, p1.z) + 0.045f;
            Vector3 a = p0 - n0;
            Vector3 b = p0 + n0;
            Vector3 c = p1 + n1;
            Vector3 d = p1 - n1;
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            uvs.Add(new Vector2(0, i / 4f));
            uvs.Add(new Vector2(1, i / 4f));
            uvs.Add(new Vector2(1, (i + 1) / 4f));
            uvs.Add(new Vector2(0, (i + 1) / 4f));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        Mesh mesh = new Mesh { name = "Ring Path Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GameObject go = new GameObject("Winding Meadow Path");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = pathMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Vector3 PathPoint(float angle)
    {
        float wobble = 1f + Mathf.Sin(angle * 3f + 0.8f) * 0.045f;
        return new Vector3(
            Mathf.Cos(angle) * 31.5f * wobble,
            0,
            6f + Mathf.Sin(angle) * 27.5f * wobble);
    }

    private static void CreateCliffWall(Transform parent)
    {
        const int segments = 22;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();
        Color lower = new Color(0.29f, 0.42f, 0.49f);
        Color upper = new Color(0.52f, 0.56f, 0.58f);

        for (int i = 0; i < segments; i++)
        {
            float x0 = Mathf.Lerp(-27f, 27f, i / (float)segments);
            float x1 = Mathf.Lerp(-27f, 27f, (i + 1) / (float)segments);
            float z0 = 27f + Mathf.Sin(i * 1.2f) * 0.45f;
            float z1 = 27f + Mathf.Sin((i + 1) * 1.2f) * 0.45f;
            float top0 = 7.2f + Mathf.PerlinNoise(i * 0.23f, 4f) * 2.4f;
            float top1 = 7.2f + Mathf.PerlinNoise((i + 1) * 0.23f, 4f) * 2.4f;
            if (Mathf.Abs(x0) < 4.5f)
            {
                top0 += 0.4f;
            }

            AddTriangle(
                vertices, triangles, colors,
                new Vector3(x0, -0.35f, z0),
                new Vector3(x0, top0, z0),
                new Vector3(x1, top1, z1),
                lower, upper, upper);
            AddTriangle(
                vertices, triangles, colors,
                new Vector3(x0, -0.35f, z0),
                new Vector3(x1, top1, z1),
                new Vector3(x1, -0.35f, z1),
                lower, upper, lower);
        }

        Mesh mesh = new Mesh { name = "Layered Cliff Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GameObject go = new GameObject("Waterfall Shelf Cliff");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = cliffMaterial;
        renderer.receiveShadows = true;
    }

    private static void CreateLakeBed(Transform parent)
    {
        GameObject bed = CreateDisc("Deep Lake Bed", 23.2f, 19.2f, 56);
        bed.transform.SetParent(parent, false);
        bed.transform.position = new Vector3(0, LakeFloorHeight - 0.14f, 7f);
        bed.AddComponent<MeshCollider>().sharedMesh = bed.GetComponent<MeshFilter>().sharedMesh;
        MeshRenderer renderer = bed.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateLowPolyMaterial(
            "Lake Bed Teal",
            new Color(0.12f, 0.52f, 0.52f),
            1.2f,
            0.95f,
            0.66f,
            1);
    }

    private static GameObject CreateDisc(string name, float radiusX, float radiusZ, int segments)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();
        Color centerColor = new Color(0.12f, 0.45f, 0.48f);
        Color edgeColor = new Color(0.2f, 0.62f, 0.55f);
        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            AddTriangle(
                vertices, triangles, colors,
                new Vector3(0, -0.3f, 0),
                new Vector3(Mathf.Cos(a0) * radiusX, 0, Mathf.Sin(a0) * radiusZ),
                new Vector3(Mathf.Cos(a1) * radiusX, 0, Mathf.Sin(a1) * radiusZ),
                centerColor, edgeColor, edgeColor);
        }

        Mesh mesh = new Mesh { name = name + " Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GameObject go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = terrainMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    private static void CreateLake(Transform parent)
    {
        GameObject water = CreateOvalWaterMesh("Transparent Stylized Lake", 22.8f, 18.2f, 24, 72);
        water.transform.SetParent(parent, false);
        water.name = "Opaque Deep Blue Lake";
        water.transform.position = new Vector3(0, LakeWaterHeight, 7f);
        Material lakeMaterial = LoadRequired<Material>(MaterialPath + "/Lake Water.mat");
        foreach (MeshRenderer renderer in water.GetComponentsInChildren<MeshRenderer>())
        {
            renderer.sharedMaterial = lakeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static GameObject CreateOvalWaterMesh(
        string name,
        float radiusX,
        float radiusZ,
        int rings,
        int segments)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        vertices.Add(Vector3.zero);
        uvs.Add(new Vector2(0.5f, 0.5f));

        for (int ring = 1; ring <= rings; ring++)
        {
            float radial = ring / (float)rings;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radiusX * radial;
                float z = Mathf.Sin(angle) * radiusZ * radial;
                vertices.Add(new Vector3(x, 0, z));
                uvs.Add(new Vector2(
                    Mathf.Cos(angle) * radial * 0.5f + 0.5f,
                    Mathf.Sin(angle) * radial * 0.5f + 0.5f));
            }
        }

        for (int segment = 0; segment < segments; segment++)
        {
            int next = (segment + 1) % segments;
            triangles.Add(0);
            triangles.Add(1 + segment);
            triangles.Add(1 + next);
        }

        for (int ring = 1; ring < rings; ring++)
        {
            int innerStart = 1 + (ring - 1) * segments;
            int outerStart = 1 + ring * segments;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                int a = innerStart + segment;
                int b = outerStart + segment;
                int c = outerStart + next;
                int d = innerStart + next;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        Mesh mesh = new Mesh { name = name + " Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GameObject go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go;
    }

    private static void CreateWaterfall(Transform parent)
    {
        const int segments = 18;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float y = Mathf.Lerp(8.7f, 0.34f, t);
            float halfWidth = Mathf.Lerp(2.2f, 3.3f, t) + Mathf.Sin(t * 13f) * 0.28f;
            float z = 25.25f + Mathf.Sin(t * 17f) * 0.09f;
            vertices.Add(new Vector3(-halfWidth, y, z));
            vertices.Add(new Vector3(halfWidth, y, z));
            uvs.Add(new Vector2(0, 1f - t));
            uvs.Add(new Vector2(1, 1f - t));
            if (i < segments)
            {
                int start = i * 2;
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
        }

        Mesh mesh = new Mesh { name = "Animated Waterfall Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GameObject waterfall = new GameObject("Tall Cascading Waterfall");
        waterfall.transform.SetParent(parent, false);
        waterfall.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = waterfall.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = LoadRequired<Material>(MaterialPath + "/Waterfall.mat");
        renderer.shadowCastingMode = ShadowCastingMode.Off;

        GameObject foam = CreateDisc("Waterfall Foam Pool", 5.5f, 3.4f, 34);
        foam.transform.SetParent(parent, false);
        foam.transform.position = new Vector3(0, 0.31f, 24f);
        Material foamMaterial = new Material(Shader.Find("Unlit/Transparent"));
        foamMaterial.color = new Color(0.78f, 1f, 1f, 0.58f);
        foam.GetComponent<MeshRenderer>().sharedMaterial = foamMaterial;
    }

    private static GameObject InstantiateModel(
        string path,
        Vector3 position,
        Quaternion rotation,
        Transform parent,
        Vector3 scale)
    {
        GameObject source = LoadRequired<GameObject>(path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent.gameObject.scene);
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(source);
        }

        instance.name = Path.GetFileNameWithoutExtension(path) + " " + (++objectCounter);
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = position;
        instance.transform.localRotation = rotation;
        instance.transform.localScale = scale;
        return instance;
    }

    private static void PlaceAtHeight(
        GameObject instance,
        float x,
        float z,
        float targetHeight,
        float baseY,
        float offset)
    {
        instance.transform.position = new Vector3(x, 0, z);
        instance.transform.localScale = Vector3.one;
        Bounds original = RendererBounds(instance);
        float scale = targetHeight / Mathf.Max(0.01f, original.size.y);
        instance.transform.localScale = Vector3.one * scale;
        Bounds bounds = RendererBounds(instance);
        float y = baseY + offset - bounds.min.y;
        instance.transform.position = new Vector3(x, y, z);
    }

    private static void PlaceOnGround(
        GameObject instance,
        float x,
        float z,
        float targetHeight,
        float offset)
    {
        PlaceAtHeight(instance, x, z, targetHeight, GroundHeight(x, z), offset);
    }

    private static void StyleModel(GameObject instance, Color fallbackColor, Color instanceTint)
    {
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            Material[] originals = renderer.sharedMaterials;
            Material[] styled = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                styled[i] = GetStyledMaterial(originals[i], fallbackColor);
            }

            renderer.sharedMaterials = styled;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_Tint", instanceTint);
            renderer.SetPropertyBlock(block);
        }
    }

    private static Material GetStyledMaterial(Material original, Color fallbackColor)
    {
        string originalName = original == null ? "Flat Color" : original.name;
        Texture texture = original == null ? null : original.GetTexture("_MainTex");
        Color color = original != null && original.HasProperty("_Color")
            ? original.GetColor("_Color")
            : fallbackColor;
        string textureKey = texture == null ? "NoTexture" : texture.name;
        Color sourceColor = color;
        bool sourceIsColored =
            Mathf.Abs(sourceColor.r - sourceColor.g) > 0.08f ||
            Mathf.Abs(sourceColor.g - sourceColor.b) > 0.08f ||
            Mathf.Abs(sourceColor.r - sourceColor.b) > 0.08f;
        bool useVertexColor = originalName.IndexOf("AllColors", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!sourceIsColored && !useVertexColor)
        {
            color = fallbackColor;
        }

        if (useVertexColor)
        {
            color = Color.white;
        }

        string key = originalName + "|" + textureKey + "|" +
                     ColorUtility.ToHtmlStringRGBA(color) + "|" +
                     ColorUtility.ToHtmlStringRGBA(fallbackColor) + "|" + useVertexColor;
        if (styledMaterials.ContainsKey(key))
        {
            return styledMaterials[key];
        }

        string safeName = Regex.Replace(originalName, "[^A-Za-z0-9 _-]", "");
        string path = MaterialPath + "/Styled " + safeName + " " + ShortHash(key) + ".mat";
        Material styled = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (styled == null)
        {
            styled = new Material(lowPolyShader);
            AssetDatabase.CreateAsset(styled, path);
        }

        styled.name = Path.GetFileNameWithoutExtension(path);
        styled.shader = lowPolyShader;
        if (texture != null)
        {
            styled.SetTexture("_MainTex", texture);
        }

        styled.SetColor("_Color", color);
        styled.SetColor("_Tint", Color.white);
        styled.SetFloat("_Saturation", 1.16f);
        styled.SetFloat("_Brightness", 0.96f);
        styled.SetFloat("_AmbientStrength", 0.48f);
        styled.SetFloat("_UseVertexColor", useVertexColor ? 1f : 0f);
        styled.SetFloat("_ToonThreshold", 0.12f);
        styled.SetFloat("_ToonSoftness", 0.075f);
        styled.SetFloat("_RimStrength", 0.1f);
        styled.SetColor("_RimColor", new Color(0.7f, 1f, 0.56f));
        EditorUtility.SetDirty(styled);
        styledMaterials[key] = styled;
        report.AppendLine(
            "Styled material: " + originalName +
            ", texture=" + textureKey +
            ", sourceColor=" + ColorUtility.ToHtmlStringRGBA(sourceColor) +
            ", fallback=" + ColorUtility.ToHtmlStringRGBA(fallbackColor) +
            ", vertexColor=" + useVertexColor);
        return styled;
    }

    private static string ShortHash(string value)
    {
        uint hash = 2166136261;
        for (int i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= 16777619;
        }

        return hash.ToString("X8");
    }

    private static void SpawnVegetation(Transform parent)
    {
        string[] deciduous =
        {
            "Tree_Oak_regular", "Tree_average_lush", "Tree_giant_lush",
            "Tree_double_lush", "Tree_Birch_regular", "Tree_high_lush",
            "Tree_low_lush", "Tree_small_regular"
        };
        string[] coniferous =
        {
            "Tree_Fir_average", "Tree_Pine_average", "Tree_Spruce_average_01",
            "Tree_Spruce_average_02"
        };
        string[] islandTrees = { "BananaTree_1", "BananaTree_2", "Palm_1", "Palm_3", "Palm_6" };
        string[] bushes =
        {
            "Bush_group_big", "Bush_group_average", "Bush_fern_big",
            "Bush_average_flowers", "Bush_raspberries_berry", "Bush_plain_big"
        };
        string[] grasses =
        {
            "Grass_bush_low_01", "Grass_bush_low_02", "Grass_plain_high_01",
            "Grass_plain_high_02", "Grass_plain_low_01", "Grass_plain_low_02"
        };
        string[] flowers =
        {
            "Flower_bush_white", "Flower_bush_yellow", "Flower_bush_red",
            "Flower_high_blue", "Flower_high_purple", "Flower_high_red"
        };

        Random random = new Random(8317);
        int count = 0;

        for (int i = 0; i < 92; i++)
        {
            float angle = NextFloat(random, 0, Mathf.PI * 2f);
            float radius = NextFloat(random, 32f, 49f);
            float x = Mathf.Cos(angle) * radius;
            float z = 6f + Mathf.Sin(angle) * radius * 0.92f;
            if (x > -9f && x < 9f && z < -23f)
            {
                x += 13f;
            }

            string path = IslandRoot + "/" + Pick(random, islandTrees) + ".fbx";
            float roll = NextFloat(random, 0, 1);
            if (roll > 0.13f)
            {
                string model = Pick(random, roll < 0.72f ? deciduous : coniferous);
                path = ForestRoot + "/" +
                       (roll < 0.72f ? "Deciduous_Trees/" : "Coniferous_Trees/") +
                       model + ".fbx";
            }

            GameObject tree = InstantiateModel(
                path, Vector3.zero, Quaternion.Euler(0, NextFloat(random, 0, 360), 0), parent, Vector3.one);
            PlaceOnGround(tree, x, z, NextFloat(random, 5.8f, 10.2f), -0.02f);
            StyleModel(tree, new Color(0.22f, 0.55f, 0.18f), TintFromRandom(random, 0.9f, 1.05f));
            count++;
        }

        SpawnHeroTree(parent, ForestRoot + "/Deciduous_Trees/Tree_giant_lush.fbx", -31f, -21f, 13.5f);
        SpawnHeroTree(parent, ForestRoot + "/Deciduous_Trees/Tree_Oak_regular.fbx", 32f, -22f, 14.2f);
        SpawnHeroTree(parent, ForestRoot + "/Coniferous_Trees/Tree_Spruce_average_01.fbx", -25f, -28f, 11.5f);
        SpawnHeroTree(parent, ForestRoot + "/Deciduous_Trees/Tree_double_lush.fbx", -28f, -8f, 9.5f);
        SpawnHeroTree(parent, ForestRoot + "/Deciduous_Trees/Tree_Oak_regular.fbx", 29f, -6f, 10.5f);

        for (int i = 0; i < 25; i++)
        {
            float x = NextFloat(random, -26f, 26f);
            float z = NextFloat(random, 29f, 37f);
            GameObject tree = InstantiateModel(
                ForestRoot + "/Coniferous_Trees/" + Pick(random, coniferous) + ".fbx",
                Vector3.zero,
                Quaternion.Euler(0, NextFloat(random, 0, 360), 0),
                parent,
                Vector3.one);
            PlaceOnGround(tree, x, z, NextFloat(random, 3.8f, 7.2f), 0f);
            StyleModel(tree, new Color(0.18f, 0.48f, 0.17f), TintFromRandom(random, 0.9f, 1.02f));
            count++;
        }

        for (int i = 0; i < 105; i++)
        {
            Vector3 point = RandomAroundLake(random, 0.98f, 1.62f);
            string model = Pick(random, bushes);
            GameObject bush = InstantiateModel(
                ForestRoot + "/Bushes/" + model + ".fbx",
                Vector3.zero,
                Quaternion.Euler(0, NextFloat(random, 0, 360), 0),
                parent,
                Vector3.one);
            PlaceOnGround(bush, point.x, point.z, NextFloat(random, 0.9f, 1.9f), 0.02f);
            StyleModel(bush, new Color(0.2f, 0.52f, 0.18f), TintFromRandom(random, 0.9f, 1.04f));
            count++;
        }

        for (int i = 0; i < 135; i++)
        {
            Vector3 point = RandomAroundLake(random, 0.94f, 1.85f);
            string model = Pick(random, grasses);
            GameObject grass = InstantiateModel(
                ForestRoot + "/Grass/" + model + ".fbx",
                Vector3.zero,
                Quaternion.Euler(0, NextFloat(random, 0, 360), 0),
                parent,
                Vector3.one);
            PlaceOnGround(grass, point.x, point.z, NextFloat(random, 0.38f, 1.05f), 0f);
            StyleModel(grass, new Color(0.25f, 0.59f, 0.15f), TintFromRandom(random, 0.94f, 1.05f));
            count++;
        }

        for (int i = 0; i < 52; i++)
        {
            Vector3 point = RandomAroundLake(random, 1.0f, 1.45f);
            GameObject flower = InstantiateModel(
                ForestRoot + "/Flowers/" + Pick(random, flowers) + ".fbx",
                Vector3.zero,
                Quaternion.Euler(0, NextFloat(random, 0, 360), 0),
                parent,
                Vector3.one);
            PlaceOnGround(flower, point.x, point.z, NextFloat(random, 0.3f, 0.72f), 0.02f);
            StyleModel(flower, new Color(0.9f, 0.9f, 0.68f), TintFromRandom(random, 0.96f, 1.06f));
            count++;
        }

        report.AppendLine("Vegetation objects: " + count);
    }

    private static void CreateWindGrass(Transform parent)
    {
        const int targetTufts = 5600;
        const int spawnTufts = 2400;
        Random random = new Random(42917);
        List<Vector3> vertices = new List<Vector3>((targetTufts + spawnTufts) * 15);
        List<Color> colors = new List<Color>((targetTufts + spawnTufts) * 15);
        List<int> triangles = new List<int>((targetTufts + spawnTufts) * 15);
        int tuftCount = 0;
        int bladeCount = 0;

        for (int attempt = 0; attempt < targetTufts * 3 && tuftCount < targetTufts; attempt++)
        {
            float x = NextFloat(random, -44.5f, 44.5f);
            float z = NextFloat(random, -36f, 49f);
            float lake = Mathf.Sqrt((x / 23f) * (x / 23f) + ((z - 7f) / 19f) * ((z - 7f) / 19f));
            if (lake < 1.13f)
            {
                continue;
            }

            float pathRing = Mathf.Sqrt(
                (x / 31.5f) * (x / 31.5f) +
                ((z - 6f) / 27.5f) * ((z - 6f) / 27.5f));
            if (Mathf.Abs(pathRing - 1f) < 0.065f)
            {
                continue;
            }

            bladeCount += AppendWindGrassTuft(
                random, x, z, vertices, colors, triangles);
            tuftCount++;
        }

        // The desktop/VR spawn is at (0, -20). A denser local meadow makes the
        // ground readable at eye level while keeping the lake and path clear.
        for (int attempt = 0;
             attempt < spawnTufts * 3 && tuftCount < targetTufts + spawnTufts;
             attempt++)
        {
            float angle = NextFloat(random, 0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(NextFloat(random, 0f, 1f)) * 16.5f;
            float x = Mathf.Cos(angle) * radius * 1.18f;
            float z = -20f + Mathf.Sin(angle) * radius;
            float lake = Mathf.Sqrt((x / 23f) * (x / 23f) + ((z - 7f) / 19f) * ((z - 7f) / 19f));
            float pathRing = Mathf.Sqrt(
                (x / 31.5f) * (x / 31.5f) +
                ((z - 6f) / 27.5f) * ((z - 6f) / 27.5f));
            if (lake < 1.13f || Mathf.Abs(pathRing - 1f) < 0.065f)
            {
                continue;
            }

            bladeCount += AppendWindGrassTuft(
                random, x, z, vertices, colors, triangles);
            tuftCount++;
        }

        Mesh mesh = new Mesh { name = "Low Poly Wind Grass Mesh" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject grass = new GameObject("Low Poly Wind Grass");
        grass.transform.SetParent(parent, false);
        grass.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = grass.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = windGrassMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        report.AppendLine("Animated low-poly grass: " + tuftCount + " tufts, " + bladeCount + " blades");
    }

    private static int AppendWindGrassTuft(
        Random random,
        float x,
        float z,
        List<Vector3> vertices,
        List<Color> colors,
        List<int> triangles)
    {
        float baseY = GroundHeight(x, z) + 0.012f;
        int bladesThisTuft = random.Next(3, 7);
        int blades = 0;
        Color variation = Color.Lerp(
            new Color(0.78f, 0.92f, 0.8f),
            new Color(1.05f, 1.02f, 0.78f),
            NextFloat(random, 0f, 1f));
        for (int blade = 0; blade < bladesThisTuft; blade++)
        {
            float angle = NextFloat(random, 0f, Mathf.PI * 2f);
            Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 leanDirection = new Vector3(-side.z, 0f, side.x);
            float width = NextFloat(random, 0.028f, 0.075f);
            float height = NextFloat(random, 0.25f, 0.62f);
            float lean = NextFloat(random, 0.02f, 0.13f);
            Vector3 offset = new Vector3(
                NextFloat(random, -0.18f, 0.18f),
                0f,
                NextFloat(random, -0.18f, 0.18f));
            Vector3 center = new Vector3(x, baseY, z) + offset;
            Vector3 left = center - side * (width * 0.5f);
            Vector3 right = center + side * (width * 0.5f);
            Vector3 tip = center + Vector3.up * height + leanDirection * lean;

            int start = vertices.Count;
            vertices.Add(left);
            vertices.Add(right);
            vertices.Add(tip);
            colors.Add(new Color(variation.r, variation.g, variation.b, 0f));
            colors.Add(new Color(variation.r, variation.g, variation.b, 0f));
            colors.Add(new Color(variation.r, variation.g, variation.b, 1f));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            blades++;
        }
        return blades;
    }

    public static void RefreshWindGrass()
    {
        lowPolyShader = LoadRequired<Shader>(LowPolyShaderPath);
        windGrassShader = LoadRequired<Shader>(WindGrassShaderPath);
        CreateWindGrassMaterial();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        GameObject vegetation = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "03 Vegetation")
                {
                    vegetation = child.gameObject;
                    break;
                }
            }
            if (vegetation != null)
            {
                break;
            }
        }

        if (vegetation == null)
        {
            throw new InvalidOperationException("The vegetation group was not found.");
        }

        Transform oldGrass = vegetation.transform.Find("Low Poly Wind Grass");
        if (oldGrass != null)
        {
            UnityEngine.Object.DestroyImmediate(oldGrass.gameObject);
        }

        report = new StringBuilder();
        CreateWindGrass(vegetation.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        CapturePreview(scene);
        CaptureTopDownPreview(scene);
        WriteReport();
    }

    private static void SpawnHeroTree(
        Transform parent,
        string path,
        float x,
        float z,
        float targetHeight)
    {
        GameObject tree = InstantiateModel(path, Vector3.zero, Quaternion.Euler(0, 17f, 0), parent, Vector3.one);
        PlaceOnGround(tree, x, z, targetHeight, 0f);
        StyleModel(tree, new Color(0.2f, 0.5f, 0.15f), new Color(1.01f, 1.02f, 0.96f));
    }

    private static Vector3 RandomAroundLake(Random random, float minRadius, float maxRadius)
    {
        for (int i = 0; i < 30; i++)
        {
            float angle = NextFloat(random, 0, Mathf.PI * 2f);
            float radius = NextFloat(random, minRadius, maxRadius);
            float x = Mathf.Cos(angle) * 23f * radius;
            float z = 7f + Mathf.Sin(angle) * 19f * radius;
            float lake = Mathf.Sqrt((x / 23f) * (x / 23f) + ((z - 7f) / 19f) * ((z - 7f) / 19f));
            if (lake >= minRadius * 0.98f)
            {
                return new Vector3(x, 0, z);
            }
        }

        return new Vector3(0, 0, -14);
    }

    private static Color TintFromRandom(Random random, float min, float max)
    {
        float value = NextFloat(random, min, max);
        return new Color(
            value * NextFloat(random, 0.95f, 1.04f),
            value,
            value * NextFloat(random, 0.92f, 1.03f));
    }

    private static float NextFloat(Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    private static string Pick(Random random, string[] values)
    {
        return values[random.Next(0, values.Length)];
    }

    private static void SpawnRocks(Transform parent)
    {
        Random random = new Random(1977);
        string[] islandRocks = { "Rock_3", "Rock_6", "Rock_7", "Rock_9", "Rock_10", "Rock_Formation_1" };
        string[] forestStones =
        {
            "Stone_big_01_mossy", "Stone_big_02_mossy", "Stone_big_03_mossy",
            "Stone_average_01_mossy", "Stone_average_02_mossy",
            "Stone_group_big_mossy", "Stone_group_average_mossy"
        };

        for (int i = 0; i < 22; i++)
        {
            float x = NextFloat(random, -25f, 25f);
            float y = NextFloat(random, 0.1f, 1.2f);
            if (Mathf.Abs(x) < 5f)
            {
                x += Mathf.Sign(x == 0 ? 1 : x) * 5.6f;
            }

            float z = NextFloat(random, 25.5f, 28.4f);
            GameObject rock = InstantiateModel(
                IslandRoot + "/" + Pick(random, islandRocks) + ".fbx",
                Vector3.zero,
                Quaternion.Euler(0, NextFloat(random, 0, 360), 0),
                parent,
                Vector3.one);
            PlaceAtHeight(rock, x, z, NextFloat(random, 3.2f, 6.8f), y, 0f);
            StyleModel(rock, new Color(0.36f, 0.5f, 0.58f), TintFromRandom(random, 0.95f, 1.08f));
        }

        for (int i = 0; i < 19; i++)
        {
            float x = NextFloat(random, -25f, 25f);
            float z = NextFloat(random, 28.5f, 34f);
            float top = 8.3f + Mathf.PerlinNoise(x * 0.09f, z * 0.06f) * 1.4f;
            GameObject rock = InstantiateModel(
                IslandRoot + "/" + Pick(random, islandRocks) + ".fbx",
                Vector3.zero,
                Quaternion.Euler(0, NextFloat(random, 0, 360), 0),
                parent,
                Vector3.one);
            PlaceAtHeight(rock, x, z, NextFloat(random, 2.8f, 6.2f), top, 0f);
            StyleModel(rock, new Color(0.33f, 0.48f, 0.55f), TintFromRandom(random, 0.9f, 1.05f));
        }

        for (int i = 0; i < 9; i++)
        {
            float angle = NextFloat(random, 0, Mathf.PI * 2f);
            float radius = NextFloat(random, 0.25f, 0.72f);
            float x = Mathf.Cos(angle) * 21f * radius;
            float z = 7f + Mathf.Sin(angle) * 16.5f * radius;
            GameObject rock = InstantiateModel(
                IslandRoot + "/" + Pick(random, islandRocks) + ".fbx",
                Vector3.zero,
                Quaternion.Euler(0, NextFloat(random, 0, 360), 0),
                parent,
                Vector3.one);
            PlaceAtHeight(rock, x, z, NextFloat(random, 0.9f, 2.5f), -0.55f, 0f);
            StyleModel(rock, new Color(0.24f, 0.42f, 0.5f), TintFromRandom(random, 0.9f, 1.05f));
        }

        Vector3[] boulderPositions =
        {
            new Vector3(-25f, 0, -19f), new Vector3(-18f, 0, -27f),
            new Vector3(24f, 0, -18f), new Vector3(18f, 0, -27f),
            new Vector3(-14f, 0, -13f), new Vector3(13f, 0, -14f),
            new Vector3(-8f, 0, 25f), new Vector3(8f, 0, 25f)
        };
        float[] boulderScales = { 3.6f, 2.6f, 3.8f, 2.5f, 1.7f, 1.8f, 2.2f, 2.3f };
        for (int i = 0; i < boulderPositions.Length; i++)
        {
            GameObject stone = InstantiateModel(
                ForestRoot + "/Stones/" + forestStones[i % forestStones.Length] + ".fbx",
                Vector3.zero,
                Quaternion.Euler(0, i * 39f, 0),
                parent,
                Vector3.one);
            PlaceOnGround(
                stone,
                boulderPositions[i].x,
                boulderPositions[i].z,
                boulderScales[i] + 1.6f,
                -0.1f);
            StyleModel(stone, new Color(0.35f, 0.48f, 0.56f), TintFromRandom(random, 0.94f, 1.06f));
        }
    }

    private static void CreateBridge(Transform parent)
    {
        GameObject bridge = InstantiateModel(
            IslandRoot + "/Bridge.fbx",
            Vector3.zero,
            Quaternion.identity,
            parent,
            Vector3.one);
        bridge.name = "Rustic Bridge over the Lake";
        Bounds initial = RendererBounds(bridge);
        if (initial.size.z > initial.size.x)
        {
            bridge.transform.localRotation = Quaternion.Euler(0, 90f, 0);
        }

        Bounds rotated = RendererBounds(bridge);
        float scale = 16.5f / Mathf.Max(0.01f, rotated.size.x);
        bridge.transform.localScale = Vector3.one * scale;
        Bounds scaled = RendererBounds(bridge);
        bridge.transform.position = new Vector3(0, 0.24f - scaled.min.y, 6f);
        StyleModel(bridge, new Color(0.48f, 0.25f, 0.11f), new Color(1f, 0.96f, 0.9f));
        report.AppendLine("Bridge fitted length: " + (RendererBounds(bridge).size.x).ToString("F2") + " m");
    }

    private static void CreateCameraAndSun(Transform parent)
    {
        GameObject cameraObject = new GameObject("Presentation Camera");
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.position = new Vector3(0, 23f, -47f);
        cameraObject.transform.LookAt(new Vector3(0, 5.6f, 10f));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 45f;
        camera.nearClipPlane = 0.25f;
        camera.farClipPlane = 250f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.5f, 0.82f, 0.99f);
        camera.allowHDR = true;
        camera.depthTextureMode = DepthTextureMode.Depth;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.tag = "MainCamera";

        GameObject sunObject = new GameObject("Warm Sun");
        sunObject.transform.SetParent(parent, false);
        sunObject.transform.rotation = Quaternion.Euler(44f, -36f, 0);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.92f, 0.77f);
        sun.intensity = 0.96f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.58f;
        sun.shadowBias = 0.045f;
        RenderSettings.sun = sun;

        GameObject fillObject = new GameObject("Cool Sky Fill");
        fillObject.transform.SetParent(parent, false);
        fillObject.transform.rotation = Quaternion.Euler(32f, 145f, 0);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.56f, 0.77f, 1f);
        fill.intensity = 0.26f;
        fill.shadows = LightShadows.None;
    }

    private static void CreateClouds(Transform parent)
    {
        Vector3[] centers =
        {
            new Vector3(-30, 31, 64), new Vector3(14, 35, 72),
            new Vector3(42, 27, 58), new Vector3(-9, 26, 48)
        };
        Vector3[] scales =
        {
            new Vector3(7.5f, 2.2f, 3.6f), new Vector3(8.4f, 2.6f, 3.9f),
            new Vector3(5.8f, 1.8f, 2.7f), new Vector3(4.2f, 1.4f, 2.1f)
        };

        for (int c = 0; c < centers.Length; c++)
        {
            GameObject cloudGroup = new GameObject("Cloud Cluster " + (c + 1));
            cloudGroup.transform.SetParent(parent, false);
            cloudGroup.transform.position = centers[c];
            for (int i = 0; i < 5; i++)
            {
                GameObject puff = CreateLowPolySphere("Cloud Puff", 1f);
                puff.transform.SetParent(cloudGroup.transform, false);
                puff.transform.localPosition = new Vector3((i - 2) * 1.55f, Mathf.Sin(i * 2.1f) * 0.45f, 0);
                puff.transform.localScale = scales[c] * (i == 2 ? 1f : 0.72f);
                MeshRenderer renderer = puff.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = cloudMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }

    private static GameObject CreateLowPolySphere(string name, float radius)
    {
        const float golden = 1.61803398875f;
        Vector3[] points =
        {
            new Vector3(-1, golden, 0), new Vector3(1, golden, 0),
            new Vector3(-1, -golden, 0), new Vector3(1, -golden, 0),
            new Vector3(0, -1, golden), new Vector3(0, 1, golden),
            new Vector3(0, -1, -golden), new Vector3(0, 1, -golden),
            new Vector3(golden, 0, -1), new Vector3(golden, 0, 1),
            new Vector3(-golden, 0, -1), new Vector3(-golden, 0, 1)
        };
        int[] faces =
        {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        for (int i = 0; i < faces.Length; i++)
        {
            vertices.Add(points[faces[i]].normalized * radius);
            triangles.Add(i);
        }

        Mesh mesh = new Mesh { name = "Low Poly Cloud Sphere" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GameObject go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>();
        return go;
    }

    private static Bounds RendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void CapturePreview(Scene scene)
    {
        EditorSceneManager.SetActiveScene(scene);
        Camera camera = FindSceneCamera(scene);
        if (camera == null)
        {
            return;
        }

        const int width = 1600;
        const int height = 920;
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        camera.targetTexture = renderTexture;
        camera.Render();
        RenderTexture.active = renderTexture;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        string imagePath = Root + "/Preview_HighlandLake.png";
        File.WriteAllBytes(imagePath, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTexture);
        UnityEngine.Object.DestroyImmediate(image);
        AssetDatabase.ImportAsset(imagePath);
        Debug.Log("Preview written: " + imagePath);
    }

    private static void CaptureTopDownPreview(Scene scene)
    {
        EditorSceneManager.SetActiveScene(scene);
        Camera camera = FindSceneCamera(scene);
        if (camera == null)
        {
            return;
        }

        Transform originalTransform = camera.transform;
        Vector3 position = originalTransform.position;
        Quaternion rotation = originalTransform.rotation;
        bool orthographic = camera.orthographic;
        float orthographicSize = camera.orthographicSize;
        originalTransform.position = new Vector3(0, 95, 7);
        originalTransform.rotation = Quaternion.Euler(90, 0, 0);
        camera.orthographic = true;
        camera.orthographicSize = 52;

        const int width = 1000;
        const int height = 1000;
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = renderTexture;
        camera.Render();
        RenderTexture.active = renderTexture;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Root + "/Preview_HighlandLake_TopDown.png", image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTexture);
        UnityEngine.Object.DestroyImmediate(image);

        originalTransform.position = position;
        originalTransform.rotation = rotation;
        camera.orthographic = orthographic;
        camera.orthographicSize = orthographicSize;
    }

    private static Camera FindSceneCamera(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (camera.gameObject.name == "Presentation Camera")
                {
                    return camera;
                }
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera = root.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                return camera;
            }
        }
        return null;
    }

    private static void WriteReport()
    {
        int renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
        report.AppendLine("Styled materials: " + styledMaterials.Count);
        report.AppendLine("Renderers in scene: " + renderers);
        report.AppendLine("Camera: 0, 23.5, -42");
        report.AppendLine("Style: flat meadow, recessed opaque deep-blue lake, low-poly wind grass");
        File.WriteAllText(Root + "/BuildReport.txt", report.ToString());
        AssetDatabase.ImportAsset(Root + "/BuildReport.txt");
    }
}
