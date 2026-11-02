using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HighlandLakeMeshColliderInstaller
{
    private const string MarkerDirectory = "Library";
    private const string MarkerPrefix = "HighlandLakeMeshColliders_";

    static HighlandLakeMeshColliderInstaller()
    {
        EditorApplication.delayCall += AutoInstall;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Environment/Add Mesh Colliders to Highland Lake Objects")]
    public static void InstallFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!IsHighlandLakeScene(scene))
        {
            Debug.LogWarning("Open a Highland Lake scene before adding mesh colliders.");
            return;
        }

        Install(scene, true);
    }

    private static void AutoInstall()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!IsHighlandLakeScene(scene) || File.Exists(MarkerPath(scene)))
        {
            return;
        }

        Install(scene, false);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += AutoInstall;
        }
    }

    private static bool IsHighlandLakeScene(Scene scene)
    {
        return scene.IsValid() &&
               scene.isLoaded &&
               scene.path.IndexOf(
                   "01_HighlandLake_可玩场景/Scenes/HighlandLake",
                   System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void Install(Scene scene, bool saveScene)
    {
        int added = 0;
        int alreadyCollidable = 0;
        int skipped = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("Scene: " + scene.path);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Undo.RegisterFullObjectHierarchyUndo(root, "Add Mesh Colliders");
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null ||
                    IsUnderPlayerOrInteractionRig(filter.transform))
                {
                    skipped++;
                    continue;
                }

                Collider existing = filter.GetComponent<Collider>();
                if (existing != null)
                {
                    alreadyCollidable++;
                    continue;
                }

                MeshCollider meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = filter.sharedMesh;
                Rigidbody body = filter.GetComponent<Rigidbody>();
                meshCollider.convex = body != null && !body.isKinematic;
                added++;
                report.AppendLine("Added: " + HierarchyPath(filter.transform));
            }
        }

        report.Insert(
            0,
            $"Added={added}, AlreadyCollidable={alreadyCollidable}, Skipped={skipped}\n");
        File.WriteAllText(MarkerPath(scene), report.ToString());

        if (added > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
            }
            else
            {
                // Save the requested collider changes automatically once,
                // including after the user exits Play mode.
                EditorSceneManager.SaveScene(scene);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Highland Lake mesh colliders complete: added {added}, " +
            $"already collidable {alreadyCollidable}, skipped {skipped}.");
    }

    private static bool IsUnderPlayerOrInteractionRig(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (current.name == "Player and Locomotion" ||
                current.name == "Desktop Walking Player" ||
                current.name == "[BuildingBlock] Camera Rig" ||
                current.name == "Meta XR Interaction")
            {
                return true;
            }
        }
        return false;
    }

    private static string MarkerPath(Scene scene)
    {
        string safeName = System.Text.RegularExpressions.Regex.Replace(
            scene.path,
            "[^A-Za-z0-9]+",
            "_");
        return Path.Combine(MarkerDirectory, MarkerPrefix + safeName + ".txt");
    }

    private static string HierarchyPath(Transform transform)
    {
        StringBuilder path = new StringBuilder(transform.name);
        for (Transform current = transform.parent;
             current != null;
             current = current.parent)
        {
            path.Insert(0, current.name + "/");
        }
        return path.ToString();
    }
}
