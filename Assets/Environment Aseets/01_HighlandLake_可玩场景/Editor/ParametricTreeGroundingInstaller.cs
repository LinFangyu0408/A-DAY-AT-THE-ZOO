#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ParametricTreeGroundingInstaller
{
    private const string ScenePath = "Assets/Scenes/Monekey Mountain.unity";
    private const string MarkerPath = "Temp/samor-tree-family-grounded-v4";
    private const string LogPath = "Logs/samor-tree-family-grounding.log";

    private static readonly (string Name, Vector3 Position)[] GroundedTrees =
    {
        ("samor_Tree_Slender", new Vector3(-4357.355f, 8.120f, 47545.88f)),
        ("samor_Tree_TwinFork", new Vector3(-4401.075f, -7.706f, 47572.24f)),
        ("samor_Tree_LowCanopy", new Vector3(-4356.355f, 15.152f, 47573.88f)),
    };

    static ParametricTreeGroundingInstaller()
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
        var scene = SceneManager.GetSceneByPath(ScenePath);
        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                openedAdditively = true;
            }

            var transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .GroupBy(item => item.name)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var tree in GroundedTrees)
            {
                if (!transforms.TryGetValue(tree.Name, out var transform))
                    throw new InvalidOperationException($"Tree instance not found: {tree.Name}");
                transform.position = tree.Position;
                EditorUtility.SetDirty(transform.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save Monekey Mountain.unity.");

            Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? "Logs");
            File.WriteAllText(LogPath, "Grounded three generated tree variants.");
            Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath) ?? "Temp");
            File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log("Grounded the three added parametric tree variants.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(LogPath, $"FAILED {DateTime.UtcNow:O}{Environment.NewLine}{exception}");
            Debug.LogException(exception);
        }
        finally
        {
            if (openedAdditively && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }
}
#endif
