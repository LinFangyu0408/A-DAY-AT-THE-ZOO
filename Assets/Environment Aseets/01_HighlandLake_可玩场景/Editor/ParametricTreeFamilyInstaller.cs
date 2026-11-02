#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-shot installer for the generated samor tree family.</summary>
[InitializeOnLoad]
public static class ParametricTreeFamilyInstaller
{
    private const string ScenePath = "Assets/Scenes/Monekey Mountain.unity";
    private const string MeshFolder = "Assets/Environment Aseets/02_第三方环境资源包/ParametricTrees/Meshes";
    private const string MarkerPath = "Temp/samor-tree-family-installed-v3";
    private const string LogPath = "Logs/samor-tree-family-install.log";

    private static readonly (string Suffix, string ExistingName, string ObjectName, Vector3 Offset)[] Variants =
    {
        ("Spreading", "samor_4077_31 (1)", "samor_Tree_Spreading", Vector3.zero),
        ("WindSwept", "samor_4077_569 (1)", "samor_Tree_WindSwept", Vector3.zero),
        ("Slender", "samor_4077_31 (1)", "samor_Tree_Slender", new Vector3(18f, 0f, -13f)),
        ("TwinFork", "samor_4077_569 (1)", "samor_Tree_TwinFork", new Vector3(-17f, 0f, 14f)),
        ("LowCanopy", "samor_4077_31 (1)", "samor_Tree_LowCanopy", new Vector3(19f, 0f, 15f)),
    };

    static ParametricTreeFamilyInstaller()
    {
        // Kept as a manual recovery template after the one-shot install completed.
    }

    private static void TryInstall()
    {
        if (File.Exists(MarkerPath))
        {
            EditorApplication.update -= TryInstall;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        EditorApplication.update -= TryInstall;
        Install();
    }

    private static void Install()
    {
        bool openedAdditively = false;
        Scene targetScene = SceneManager.GetSceneByPath(ScenePath);
        try
        {
            var meshes = LoadMeshes();
            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                targetScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                openedAdditively = true;
            }

            var trees = FindTrees(targetScene);
            if (trees.Count < 2)
                throw new InvalidOperationException($"Expected two samor tree instances, found {trees.Count}.");

            var log = new StringBuilder();
            log.AppendLine($"Scene: {ScenePath}");
            log.AppendLine($"Tree instances found: {trees.Count}");

            var created = new List<GameObject>();
            foreach (var variant in Variants)
            {
                var basis = trees.FirstOrDefault(pair => pair.Key == variant.ExistingName).Value;
                if (basis == null)
                    throw new InvalidOperationException($"Tree instance not found: {variant.ExistingName}");

                var tree = variant.Offset == Vector3.zero
                    ? basis
                    : CloneAtGroundedOffset(basis, variant.Offset);

                tree.name = variant.ObjectName;
                var filter = tree.GetComponent<MeshFilter>();
                if (filter == null)
                    throw new InvalidOperationException($"MeshFilter missing on {basis.name}.");

                filter.sharedMesh = meshes[variant.Suffix];
                var renderer = tree.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sharedMaterials = basis.GetComponent<MeshRenderer>().sharedMaterials;

                EditorUtility.SetDirty(tree);
                created.Add(tree);
                log.AppendLine(
                    $"{variant.ObjectName}: world={tree.transform.position:F2}, " +
                    $"mesh={meshes[variant.Suffix].name}, height={meshes[variant.Suffix].bounds.size.y:F2}");
            }

            EditorSceneManager.MarkSceneDirty(targetScene);
            if (!EditorSceneManager.SaveScene(targetScene))
                throw new InvalidOperationException("Unity could not save Monekey Mountain.unity.");

            Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? "Logs");
            File.WriteAllText(LogPath, log.ToString());
            Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath) ?? "Temp");
            File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log($"Parametric tree family installed in {ScenePath}. {log}");
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                LogPath,
                $"FAILED {DateTime.UtcNow:O}{Environment.NewLine}{exception}");
            Debug.LogException(exception);
        }
        finally
        {
            if (openedAdditively && targetScene.IsValid() && targetScene.isLoaded)
                EditorSceneManager.CloseScene(targetScene, true);
        }
    }

    private static Dictionary<string, Mesh> LoadMeshes()
    {
        var result = new Dictionary<string, Mesh>(StringComparer.Ordinal);
        foreach (var variant in Variants)
        {
            if (result.ContainsKey(variant.Suffix))
                continue;

            var path = $"{MeshFolder}/samor_4077_133_{variant.Suffix}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
                throw new InvalidOperationException($"No Mesh found in {path}.");
            if (Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z) < 5f)
                throw new InvalidOperationException($"{path} has unexpected bounds: {mesh.bounds.size}.");
            result.Add(variant.Suffix, mesh);
        }
        return result;
    }

    private static Dictionary<string, GameObject> FindTrees(Scene scene)
    {
        var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (Variants.Any(variant =>
                        string.Equals(transform.name, variant.ExistingName, StringComparison.Ordinal)))
                {
                    result[transform.name] = transform.gameObject;
                }
            }
        }
        return result;
    }

    private static GameObject CloneAtGroundedOffset(GameObject basis, Vector3 worldOffset)
    {
        var clone = UnityEngine.Object.Instantiate(basis);
        clone.name = "Temporary Parametric Tree";
        clone.transform.SetParent(basis.transform.parent, true);
        clone.transform.position = basis.transform.position + worldOffset;
        clone.transform.rotation = basis.transform.rotation;
        clone.transform.localScale = basis.transform.localScale;

        var renderer = clone.GetComponent<MeshRenderer>();
        if (renderer == null)
            return clone;

        var rayOrigin = clone.transform.position + Vector3.up * 120f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 260f, ~0, QueryTriggerInteraction.Ignore))
            clone.transform.position += Vector3.up * (hit.point.y - renderer.bounds.min.y);
        return clone;
    }
}
#endif
