using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Kept as a manual diagnostic helper; the one-off inspection already completed.
public static class HighlandLakeVrInspect
{
    static HighlandLakeVrInspect()
    {
        EditorApplication.delayCall += Inspect;
    }

    private static void Inspect()
    {
        string reportPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Logs",
            "HighlandLakeVrInspection.txt");
        StringBuilder report = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        report.AppendLine("Scene: " + scene.path);
        report.AppendLine("Dirty: " + scene.isDirty);

        string[] tokens = { "rig", "camera", "tracking", "anchor", "hand", "locomotion" };
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                bool relevant = false;
                string lower = transform.name.ToLowerInvariant();
                foreach (string token in tokens)
                {
                    if (lower.Contains(token))
                    {
                        relevant = true;
                        break;
                    }
                }

                if (!relevant && transform.GetComponents<Component>().Length > 1)
                {
                    relevant = true;
                }

                if (!relevant)
                {
                    continue;
                }

                report.AppendLine(
                    "SCENE " + HierarchyPath(transform) +
                    " | active=" + transform.gameObject.activeSelf +
                    " | layer=" + transform.gameObject.layer +
                    " | pos=" + transform.position.ToString("F2"));
                foreach (Component component in transform.GetComponents<Component>())
                {
                    report.AppendLine("  " + (component == null ? "MissingScript" : component.GetType().FullName));
                }
            }
        }

        string[] prefabTokens =
        {
            "InteractionRigOVR",
            "OVRComprehensiveInteractionRig",
            "LocomotionControllerInteractorGroup",
            "LocomotionSettingsWiring",
            "TeleportControllerInteractor",
            "Locomotor"
        };
        foreach (string token in prefabTokens)
        {
            string[] guids = AssetDatabase.FindAssets(token + " t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != token)
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                report.AppendLine("PREFAB " + path);
                foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    report.AppendLine("  NODE " + HierarchyPath(transform));
                    foreach (Component component in transform.GetComponents<Component>())
                    {
                        report.AppendLine("    " + (component == null ? "MissingScript" : component.GetType().FullName));
                    }
                }
            }
        }

        File.WriteAllText(reportPath, report.ToString());
        Debug.Log("Highland Lake VR inspection written: " + reportPath);
    }

    private static string HierarchyPath(Transform transform)
    {
        List<string> parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}
