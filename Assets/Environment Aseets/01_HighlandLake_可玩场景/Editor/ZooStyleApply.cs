using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ZooStyleApply
{
    private const string ScenePath = "Assets/Scenes/Monekey Mountain.unity";
    private const string ShaderName = "Zoo/High Saturation Low Poly";
    private const string MaterialDirectory = "Assets/Environment Aseets/01_HighlandLake_可玩场景/Materials/Zoo Style";
    private const string AlpineMaterialPath = MaterialDirectory + "/Zoo Alpine.mat";
    private const string FoliageMaterialPath = MaterialDirectory + "/Zoo Foliage.mat";
    private const string WaterMaterialPath = MaterialDirectory + "/Zoo Water.mat";
    private const string AlpineTexturePath =
        "Assets/Environment Aseets/02_第三方环境资源包/4077_卡通低面森林场景/FBX_4077/FBX_4077/tex/ALPINE WOODLANDS_MAT_MAIN_MainTex.psd";
    private const string FoliageMaterialSourcePath =
        "Assets/Environment Aseets/01_HighlandLake_可玩场景/Materials/texture_gradient.mat";
    private const string PreviewPath =
        @"C:\Users\LENOVO\.codex\visualizations\2026\10\30\01a18e25-85be-7071-a82a-e71b73cb54d0\zoo_style_audit\ZooStylePreview.png";

    [MenuItem("Tools/A Day at the Zoo/Apply Unified Zoo Style")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            throw new InvalidOperationException($"Shader not found: {ShaderName}");
        }

        EnsureDirectory(MaterialDirectory);
        Material alpine = CreateSharedMaterial(shader, AlpineMaterialPath, 0.98f, 0.86f, 0.64f, "AABBCB");
        Material foliage = CreateSharedMaterial(shader, FoliageMaterialPath, 1.08f, 0.86f, 0.67f, "C7E1D7");
        Material water = CreateSharedMaterial(shader, WaterMaterialPath, 0.9f, 0.98f, 0.75f, "D7F2F6");

        Texture alpineTexture = AssetDatabase.LoadAssetAtPath<Texture>(AlpineTexturePath);
        Material foliageSource = AssetDatabase.LoadAssetAtPath<Material>(FoliageMaterialSourcePath);
        Texture foliageTexture = foliageSource != null ? foliageSource.GetTexture("_MainTex") : null;
        if (alpineTexture == null || foliageTexture == null)
        {
            throw new InvalidOperationException("Failed to load the Alpine or foliage palette texture.");
        }
        alpine.SetTexture("_MainTex", alpineTexture);
        foliage.SetTexture("_MainTex", foliageTexture);
        water.SetTexture("_MainTex", Texture2D.whiteTexture);

        ConfigureMaterial(alpine, false, "AABBCB", "FFEBC8", 0.03f, 1f, 0.65f);
        ConfigureMaterial(foliage, false, "C7E1D7", "F2E0C6", 0.05f, 0.68f, 0.08f);
        ConfigureWater(water);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int changedRenderers = 0;
        foreach (Renderer renderer in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0)
            {
                continue;
            }

            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material original = materials[i];
                if (original == null)
                {
                    continue;
                }

                Material replacement = ClassifyMaterial(original, alpine, foliage, water);
                if (replacement != null && original != replacement)
                {
                    materials[i] = replacement;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
                changedRenderers++;
            }
        }

        ArrangeFarMountains(scene);
        ConfigureCameraAndLighting(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        CapturePreview();
        Debug.Log($"Unified Zoo style applied. Renderers changed={changedRenderers}");
    }

    public static void ReduceFoliageHighlights()
    {
        Material foliage = AssetDatabase.LoadAssetAtPath<Material>(FoliageMaterialPath);
        if (foliage == null)
        {
            throw new InvalidOperationException("Zoo Foliage material was not found.");
        }

        foliage.SetColor("_LightTint", Hex("F2E0C6"));
        EditorUtility.SetDirty(foliage);
        AssetDatabase.SaveAssets();

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CapturePreview();
        Debug.Log("Reduced foreground foliage highlight intensity.");
    }

    private static Material ClassifyMaterial(
        Material original,
        Material alpine,
        Material foliage,
        Material water)
    {
        if (original.name.IndexOf("WATER", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return water;
        }

        Texture texture = null;
        if (original.HasProperty("_BaseMap"))
        {
            texture = original.GetTexture("_BaseMap");
        }
        if (texture == null && original.HasProperty("_MainTex"))
        {
            texture = original.GetTexture("_MainTex");
        }

        if (texture != null && texture.name == "texture_gradient")
        {
            return foliage;
        }
        if (texture != null && texture.name.StartsWith("ALPINE WOODLANDS_MAT_MAIN", StringComparison.Ordinal))
        {
            return alpine;
        }
        return null;
    }

    private static Material CreateSharedMaterial(
        Shader shader,
        string path,
        float saturation,
        float brightness,
        float ambient,
        string shadowTint)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.name = Path.GetFileNameWithoutExtension(path);
        material.SetColor("_Color", Color.white);
        material.SetColor("_Tint", Color.white);
        material.SetFloat("_Saturation", saturation);
        material.SetFloat("_Brightness", brightness);
        material.SetFloat("_AmbientStrength", ambient);
        material.SetFloat("_ShadowLift", 0.08f);
        material.SetFloat("_ToonThreshold", 0.18f);
        material.SetFloat("_ToonSoftness", 0.14f);
        material.SetColor("_ShadowTint", Hex(shadowTint));
        material.SetColor("_LightTint", Hex("FFF4DF"));
        material.SetColor("_RimColor", Hex("CFEFFF"));
        material.SetFloat("_RimStrength", 0.06f);
        material.SetFloat("_UseVertexColor", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureMaterial(
        Material material,
        bool useVertexColor,
        string shadowTint,
        string lightTint,
        float rim,
        float yellowToGreen,
        float rockTintStrength)
    {
        material.SetFloat("_UseVertexColor", useVertexColor ? 1f : 0f);
        material.SetColor("_ShadowTint", Hex(shadowTint));
        material.SetColor("_LightTint", Hex(lightTint));
        material.SetFloat("_RimStrength", rim);
        material.SetFloat("_YellowToGreen", yellowToGreen);
        material.SetColor("_RockTint", Hex("91A8BA"));
        material.SetFloat("_RockTintStrength", rockTintStrength);
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureWater(Material material)
    {
        material.SetColor("_Color", Hex("72CAE1"));
        material.SetColor("_ShadowTint", Hex("D8F2FF"));
        material.SetColor("_LightTint", Hex("FFF8EA"));
        material.SetFloat("_Saturation", 0.9f);
        material.SetFloat("_Brightness", 0.96f);
        material.SetFloat("_AmbientStrength", 0.76f);
        material.SetFloat("_ShadowLift", 0.1f);
        EditorUtility.SetDirty(material);
    }

    private static void ArrangeFarMountains(UnityEngine.SceneManagement.Scene scene)
    {
        GameObject root = scene.GetRootGameObjects()
            .FirstOrDefault(item => item.name == "Mountain Environment");
        if (root == null)
        {
            throw new InvalidOperationException("Mountain Environment was not found.");
        }

        MoveRendererCenter(root, "samor_4077_13", new Vector3(-4635f, 64f, 48020f));
        MoveRendererCenter(root, "samor_4077_10", new Vector3(-3990f, 180f, 48080f));
        SetRendererVisible(root, "samor_4077_07", false);
        SetRendererVisible(root, "samor_4077_09", false);
        SetRendererVisible(root, "samor_4077_12", false);
        SetRendererVisible(root, "samor_4077_11", false);
        SetRendererVisible(root, "samor_4077_18", false);
    }

    private static void MoveRendererCenter(GameObject root, string objectName, Vector3 desiredCenter)
    {
        Renderer renderer = root.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(item => item.gameObject.name == objectName);
        if (renderer == null)
        {
            throw new InvalidOperationException($"Mountain object not found: {objectName}");
        }
        renderer.transform.position += desiredCenter - renderer.bounds.center;
    }

    private static void SetRendererVisible(GameObject root, string objectName, bool visible)
    {
        Renderer renderer = root.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(item => item.gameObject.name == objectName);
        if (renderer == null)
        {
            throw new InvalidOperationException($"Mountain object not found: {objectName}");
        }
        renderer.gameObject.SetActive(visible);
    }

    private static void ConfigureCameraAndLighting(UnityEngine.SceneManagement.Scene scene)
    {
        Camera camera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(item => item.CompareTag("MainCamera"));
        if (camera != null)
        {
            camera.transform.position = new Vector3(-4580f, 8f, 47455f);
            camera.transform.LookAt(new Vector3(-4550f, 42f, 47950f));
            camera.fieldOfView = 55f;
            camera.farClipPlane = 2000f;
            EditorUtility.SetDirty(camera);
        }

        Light sun = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light>(true))
            .FirstOrDefault(item => item.type == LightType.Directional);
        if (sun != null)
        {
            sun.name = "Warm Sun";
            sun.color = Hex("FFEBC4");
            sun.intensity = 0.96f;
            sun.shadowStrength = 0.58f;
            sun.transform.rotation = Quaternion.Euler(44f, -36f, 0f);
            EditorUtility.SetDirty(sun);
        }

        Light fill = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light>(true))
            .FirstOrDefault(item => item.name == "Cool Sky Fill");
        if (fill == null)
        {
            GameObject fillObject = new GameObject("Cool Sky Fill");
            fill = fillObject.AddComponent<Light>();
        }
        fill.type = LightType.Directional;
        fill.color = Hex("8FC4FF");
        fill.intensity = 0.26f;
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(32f, 145f, 0f);
        EditorUtility.SetDirty(fill);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.64f, 0.85f, 0.98f);
        RenderSettings.ambientEquatorColor = new Color(0.47f, 0.7f, 0.58f);
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.38f, 0.24f);
        RenderSettings.ambientIntensity = 1.05f;
        RenderSettings.fog = false;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.64f, 0.84f, 0.9f);
        RenderSettings.fogStartDistance = 54f;
        RenderSettings.fogEndDistance = 145f;
        RenderSettings.sun = sun;
    }

    private static void CapturePreview()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            throw new InvalidOperationException("Main Camera was not found.");
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
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath) ?? string.Empty);
            File.WriteAllBytes(PreviewPath, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static void EnsureDirectory(string path)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(fullPath);
        AssetDatabase.Refresh();
    }

    private static Color Hex(string rgb)
    {
        ColorUtility.TryParseHtmlString("#" + rgb, out Color color);
        return color;
    }
}
