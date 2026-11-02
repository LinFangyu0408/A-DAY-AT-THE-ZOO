using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ZooStyleAudit
{
    private const string ScenePath = "Assets/Scenes/Monekey Mountain.unity";
    private const string OutputDirectory =
        @"C:\Users\LENOVO\.codex\visualizations\2026\10\30\01a18e25-85be-7071-a82a-e71b73cb54d0\zoo_style_audit";

    public static void Audit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Renderer[] renderers = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
            .ToArray();

        Directory.CreateDirectory(OutputDirectory);

        var report = new StringBuilder();
        report.AppendLine("Name\tPath\tCenter\tSize\tMaterial\tShader");
        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            string material = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "<none>";
            string shader = renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null
                ? renderer.sharedMaterial.shader.name
                : "<none>";
            report.AppendLine(
                $"{renderer.gameObject.name}\t{GetPath(renderer.transform)}\t" +
                $"{Format(bounds.center)}\t{Format(bounds.size)}\t{material}\t{shader}");
        }

        File.WriteAllText(Path.Combine(OutputDirectory, "renderers.tsv"), report.ToString());
        Bounds allBounds = CalculateBounds(renderers);
        File.WriteAllText(
            Path.Combine(OutputDirectory, "bounds.txt"),
            $"All center={Format(allBounds.center)} size={Format(allBounds.size)}\n" +
            $"Renderers={renderers.Length}\n");

        CaptureFramed(renderers, "CurrentSceneView.png");
        CaptureCamera(Camera.main, "MainCameraView.png");
        Debug.Log(
            $"Zoo style audit complete. Renderers={renderers.Length}, " +
            $"bounds center={Format(allBounds.center)} size={Format(allBounds.size)}");
    }

    public static void ExploreViews()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var views = new[]
        {
            new Vector3(-4580f, 4f, 47230f),
            new Vector3(-4580f, 8f, 47300f),
            new Vector3(-4700f, 3f, 47320f),
            new Vector3(-4500f, 5f, 47400f),
            new Vector3(-4630f, 12f, 47430f),
            new Vector3(-4800f, 8f, 47380f)
        };
        var target = new Vector3(-4550f, 42f, 47900f);

        for (int i = 0; i < views.Length; i++)
        {
            GameObject cameraObject = new GameObject("Zoo Exploration Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2000f;
            camera.transform.position = views[i];
            camera.transform.LookAt(target);
            CaptureCamera(camera, $"Exploration_{i + 1}.png");
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    public static void ExploreNearViews()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var views = new[]
        {
            new Vector3(-4500f, 10f, 47460f),
            new Vector3(-4550f, 14f, 47470f),
            new Vector3(-4580f, 8f, 47455f),
            new Vector3(-4500f, 18f, 47470f)
        };
        var target = new Vector3(-4550f, 42f, 47950f);

        for (int i = 0; i < views.Length; i++)
        {
            GameObject cameraObject = new GameObject("Zoo Near Exploration Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2000f;
            camera.transform.position = views[i];
            camera.transform.LookAt(target);
            CaptureCamera(camera, $"NearView_{i + 1}.png");
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    public static void IsolateMountainCandidates()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = scene.GetRootGameObjects()
            .FirstOrDefault(item => item.name == "Mountain Environment");
        Camera camera = Camera.main;
        if (root == null || camera == null)
        {
            throw new InvalidOperationException("Mountain Environment or Main Camera is missing.");
        }

        CaptureCamera(camera, "Isolation_0_All.png");
        string[] candidates = { "samor_4077_14", "samor_4077_16", "samor_4077_17", "samor_4077_18" };
        for (int i = 0; i < candidates.Length; i++)
        {
            Renderer renderer = root.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(item => item.gameObject.name == candidates[i]);
            if (renderer == null)
            {
                continue;
            }

            bool wasActive = renderer.gameObject.activeSelf;
            renderer.gameObject.SetActive(false);
            CaptureCamera(camera, $"Isolation_{i + 1}_{candidates[i]}.png");
            renderer.gameObject.SetActive(wasActive);
        }
    }

    public static void MaterialAudit()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Renderer[] renderers = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
            .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
            .ToArray();
        Directory.CreateDirectory(OutputDirectory);

        var report = new StringBuilder();
        report.AppendLine("Name\tShader\tRenderQueue\tMainTexture\tColor\tBaseColor\tMode\tKeywords");
        var materials = new HashSet<Material>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || !materials.Add(material))
                {
                    continue;
                }

                Texture texture = null;
                if (material.HasProperty("_BaseMap"))
                {
                    texture = material.GetTexture("_BaseMap");
                }
                if (texture == null && material.HasProperty("_MainTex"))
                {
                    texture = material.GetTexture("_MainTex");
                }

                Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                Color baseColor = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : Color.white;
                float mode = material.HasProperty("_Mode") ? material.GetFloat("_Mode") : 0f;
                string keywords = string.Join("|", material.shaderKeywords);
                report.AppendLine(
                    $"{material.name}\t{material.shader.name}\t{material.renderQueue}\t" +
                    $"{(texture != null ? texture.name : "<none>")}\t{Format(color)}\t" +
                    $"{Format(baseColor)}\t{mode:F0}\t{keywords}");
            }
        }

        File.WriteAllText(Path.Combine(OutputDirectory, "materials.tsv"), report.ToString());
        Debug.Log($"Material audit complete. Unique materials={materials.Count}");
    }

    private static Bounds CalculateBounds(IEnumerable<Renderer> renderers)
    {
        Renderer[] items = renderers.ToArray();
        Bounds bounds = items[0].bounds;
        foreach (Renderer renderer in items.Skip(1))
        {
            bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private static void CaptureFramed(Renderer[] renderers, string fileName)
    {
        Bounds bounds = CalculateBounds(renderers);
        GameObject cameraObject = new GameObject("Zoo Audit Camera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 52f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 5000f;
        camera.transform.position = new Vector3(
            bounds.center.x,
            bounds.min.y + Mathf.Max(4f, bounds.size.y * 0.12f),
            bounds.min.z - Mathf.Max(25f, bounds.size.z * 0.12f));
        Vector3 target = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.22f, bounds.max.z);
        camera.transform.LookAt(target);

        CaptureCamera(camera, fileName);
        UnityEngine.Object.DestroyImmediate(cameraObject);
    }

    private static void CaptureCamera(Camera camera, string fileName)
    {
        if (camera == null)
        {
            return;
        }

        const int width = 1600;
        const int height = 800;
        RenderTexture target = RenderTexture.GetTemporary(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);

        try
        {
            camera.targetTexture = target;
            camera.enabled = true;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, fileName), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static string GetPath(Transform transform)
    {
        var parts = new List<string>();
        while (transform != null)
        {
            parts.Add(transform.name);
            transform = transform.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string Format(Vector3 value)
    {
        return $"{value.x:F2},{value.y:F2},{value.z:F2}";
    }

    private static string Format(Color value)
    {
        return $"{value.r:F3},{value.g:F3},{value.b:F3},{value.a:F3}";
    }
}
