using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class PolyverseMonkeyMountainSetup
{
    private const string ScenePath = "Assets/Scenes/Monekey Mountain.unity";
    private const string MaterialPath =
        "Assets/Environment Aseets/01_HighlandLake_可玩场景/Materials/Polyverse Clear Blue Stylized Sky.mat";
    private const string CloudCubemapPath =
        "Assets/BOXOPHOBIC/Polyverse Skies/Core/Textures/Clouds/Clouds 04 A.png";
    private const string ShaderName = "BOXOPHOBIC/Polyverse Skies/Standard";

    private const string PreviewPath =
        @"C:\Users\LENOVO\.codex\visualizations\2026\10\30\01a18e25-85be-7071-a82a-e71b73cb54d0\Monkey Mountain Polyverse Preview.png";

    [MenuItem("Tools/A Day at the Zoo/Apply Polyverse Stylized Sky")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            throw new InvalidOperationException("Polyverse Skies Standard shader was not imported.");
        }

        Material skybox = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (skybox == null)
        {
            skybox = new Material(shader) { name = "Polyverse Clear Blue Stylized Sky" };
            AssetDatabase.CreateAsset(skybox, MaterialPath);
        }
        else
        {
            skybox.shader = shader;
        }

        ConfigureSkybox(skybox);
        EditorUtility.SetDirty(skybox);
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.05f;
        RenderSettings.reflectionIntensity = 1f;

        Light sun = FindMainDirectionalLight();
        if (sun != null)
        {
            RenderSettings.sun = sun;
        }

        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        CapturePreview();
        Debug.Log("Polyverse stylized sky applied to Monkey Mountain.");
        Debug.Log("Active color space: " + QualitySettings.activeColorSpace);
    }

    private static void ConfigureSkybox(Material material)
    {
        Cubemap clouds = AssetDatabase.LoadAssetAtPath<Cubemap>(CloudCubemapPath);
        if (clouds == null)
        {
            throw new InvalidOperationException("Polyverse cloud cubemap was not imported.");
        }

        material.SetFloat("_BackgroundMode", 0f);
        material.SetFloat("_BackgroundExposure", 1f);
        material.SetFloat("_EquatorHeight", 0.66f);
        material.SetFloat("_EquatorSmoothness", 0.78f);

        material.SetColor("_SkyColor", Hex("4EACFD"));
        material.SetColor("_EquatorColor", Hex("78C9FD"));
        material.SetColor("_GroundColor", Hex("ACE1FC"));

        material.SetTexture("_CloudsCubemap", clouds);
        material.SetFloat("_CloudsHeight", -0.12f);
        material.SetColor("_CloudsLightColor", Color.white);
        material.SetColor("_CloudsShadowColor", Hex("D8EDFC"));
        material.SetFloat("_CloudsRotation", 28f);
        material.SetVector("_CloudsRotationAxis", new Vector4(0f, 1f, 0f, 0f));
        material.SetFloat("_CloudsRotationSpeed", 0.05f);

        material.SetFloat("_EnableClouds", 1f);
        material.SetFloat("_EnableCloudsRotation", 1f);
        material.SetFloat("_EnablePatternOverlay", 0f);
        material.SetFloat("_EnableStars", 0f);
        material.SetFloat("_EnableStarsTwinkling", 0f);
        material.SetFloat("_EnableStarsRotation", 0f);
        material.SetFloat("_EnableSun", 0f);
        material.SetFloat("_EnableMoon", 0f);
        material.SetFloat("_EnableBuiltinFog", 0f);

        material.DisableKeyword("_BACKGROUNDMODE_CUBEMAP");
        material.DisableKeyword("_BACKGROUNDMODE_COMBINED");
        material.EnableKeyword("_BACKGROUNDMODE_COLORS");

        material.EnableKeyword("_ENABLECLOUDS_ON");
        material.EnableKeyword("_ENABLECLOUDSROTATION_ON");

        material.DisableKeyword("_ENABLEPATTERNOVERLAY_ON");
        material.DisableKeyword("_ENABLESTARS_ON");
        material.DisableKeyword("_ENABLESTARSTWINKLING_ON");
        material.DisableKeyword("_ENABLESTARSROTATION_ON");
        material.DisableKeyword("_ENABLESUN_ON");
        material.DisableKeyword("_ENABLEMOON_ON");
        material.DisableKeyword("_ENABLEBUILTINFOG_ON");
    }

    private static Light FindMainDirectionalLight()
    {
        foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }

        return null;
    }

    private static Color Hex(string rgb)
    {
        ColorUtility.TryParseHtmlString("#" + rgb, out Color color);
        return color;
    }

    private static void CapturePreview()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("No main camera found; preview capture skipped.");
            return;
        }

        const int width = 1600;
        const int height = 900;
        RenderTexture target = RenderTexture.GetTemporary(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);

        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);

        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();

            string directory = Path.GetDirectoryName(PreviewPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

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
}
