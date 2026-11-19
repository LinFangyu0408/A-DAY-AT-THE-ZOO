using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CareCasualUiInstaller
{
    const string Work = "work/monkey-care/";
    const string Request = Work + "casual-ui.request";
    const string Result = Work + "casual-ui-result.txt";
    const string Target = "Assets/MonkeyCare/Resources/UI/CasualHD";
    const string StoreUrl = "https://assetstore.unity.com/packages/2d/gui/icons/2d-casual-ui-hd-82080";
    const string ChinaStoreUrl = "https://assetstore.u3d.cn/";
    const string ChinaGuiPartsUrl = "https://assetstore.u3d.cn/packages/2d/gui/assetstore-package-20006628";
    const string ChinaButtonPackUrl = "https://assetstore.u3d.cn/packages/2d/gui/assetstore-package-20004118";

    static CareCasualUiInstaller()
    {
        Directory.CreateDirectory(Work);
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists(Request)) return;
        File.Delete(Request);
        try
        {
            int copied = Import();
            File.WriteAllText(Result, copied > 0
                ? "SUCCESS " + copied + " textures copied to " + Target
                : "MISSING Import 2D Casual UI HD from the Asset Store first.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(Result, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/猴子照顾/整理 2D Casual UI HD 素材")]
    public static void MenuImport() => Import();

    [MenuItem("Tools/猴子照顾/打开 2D Casual UI HD 商店页面")]
    public static void OpenStore() => Application.OpenURL(StoreUrl);

    [MenuItem("Tools/猴子照顾/打开 Unity 中国资源商店")]
    public static void OpenChinaStore() => Application.OpenURL(ChinaStoreUrl);

    [MenuItem("Tools/猴子照顾/打开中国商店的免费 UI 替代素材")]
    public static void OpenChinaFallback()
    {
        Application.OpenURL(ChinaGuiPartsUrl);
        Application.OpenURL(ChinaButtonPackUrl);
    }

    public static int Import()
    {
        Directory.CreateDirectory(Target);
        var sourcePaths = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(IsCasualTexture)
            .Distinct()
            .OrderBy(path => path)
            .ToList();
        if (sourcePaths.Count == 0)
        {
            Debug.LogWarning("[Monkey Care] 未找到 2D Casual UI HD。请先在 Package Manager > My Assets 导入。");
            return 0;
        }

        var existingTargets = new Dictionary<string, string>();
        foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { Target })
                     .Select(AssetDatabase.GUIDToAssetPath))
            existingTargets[Path.GetFileName(path)] = path;

        var report = new StringBuilder();
        int copied = 0;
        foreach (string source in sourcePaths)
        {
            string fileName = Path.GetFileName(source);
            string targetName = Category(Path.GetFileNameWithoutExtension(fileName), source) +
                                "_" + Sanitize(Path.GetFileNameWithoutExtension(fileName)) +
                                Path.GetExtension(fileName).ToLowerInvariant();
            string destination = Target + "/" + targetName;
            if (!existingTargets.ContainsKey(targetName))
            {
                if (!AssetDatabase.CopyAsset(source, destination))
                {
                    report.AppendLine("COPY FAILED " + source);
                    continue;
                }
            }
            Configure(destination);
            report.AppendLine(source + " -> " + destination);
            copied++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Monkey Care] 2D Casual UI HD 已整理 " + copied + " 张纹理。\n" + report);
        return copied;
    }

    static bool IsCasualTexture(string path)
    {
        if (string.IsNullOrEmpty(path) || path.StartsWith(Target, StringComparison.OrdinalIgnoreCase))
            return false;
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") return false;
        string lower = path.ToLowerInvariant();
        return lower.Contains("2d casual ui hd") ||
               lower.Contains("2d-casual-ui") ||
               lower.Contains("casual ui hd") ||
               lower.Contains("casual_ui") ||
               lower.Contains("gui元件包") ||
               lower.Contains("gui parts") ||
               lower.Contains("gui - basic button pack") ||
               lower.Contains("2d icons - casual icon pack");
    }

    static string Category(string name, string path)
    {
        string value = (name + " " + path).ToLowerInvariant();
        if (Contains(value, "panel", "window", "frame", "background", "board")) return "casual_panel";
        if (Contains(value, "header", "title", "ribbon", "tab")) return "casual_header";
        if (Contains(value, "button", "btn")) return "casual_button";
        if (Contains(value, "star")) return "casual_star";
        if (Contains(value, "arrow")) return "casual_arrow";
        if (Contains(value, "banana", "fruit")) return "casual_banana";
        if (Contains(value, "slot", "square")) return "casual_slot";
        if (Contains(value, "green")) return "casual_green";
        if (Contains(value, "red")) return "casual_red";
        return "casual";
    }

    static bool Contains(string value, params string[] tokens) =>
        tokens.Any(token => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);

    static string Sanitize(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray();
        string result = new string(chars);
        while (result.Contains("__")) result = result.Replace("__", "_");
        return result.Trim('_');
    }

    static void Configure(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }
}
