using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC.Editor
{
    public static partial class ZooDayTwoBuilder
    {
        const string SourceScene = "Assets/Environment Aseets/01_HighlandLake_可玩场景/Scenes/HighlandLake_Backup_2026-11-23.unity";
        const string Root = "Assets/ZooPCMenu/DayTwo/";
        public static void AuditSerialization()
        {
            EditorSceneManager.OpenScene(Root + "Scenes/PC_DayTwoLakeside.unity");
            var lines = new System.Collections.Generic.List<string>();
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!behaviour) continue;
                var script = MonoScript.FromMonoBehaviour(behaviour);
                lines.Add(behaviour.GetType().FullName + " | " + behaviour.name + " | " + AssetDatabase.GetAssetPath(script) + " | resolves=" + script?.GetClass()?.FullName);
            }
            File.WriteAllLines(Path.GetFullPath("../.menu-tools/day2-serialization.txt"), lines);
            EditorApplication.Exit(0);
        }
        public static void Probe()
        {
            EditorSceneManager.OpenScene(SourceScene);
            var session = UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>(FindObjectsInactive.Include);
            var report = new System.Text.StringBuilder();
            report.AppendLine("SESSION " + session.name);
            foreach (var npc in session.monkeys)
                report.AppendLine("MONKEY " + npc.name + " position=" + npc.transform.position + " scale=" + npc.transform.lossyScale + " bounds=" + BoundsOf(npc.gameObject));
            foreach (var item in session.items)
                report.AppendLine("ITEM " + item.name + " kind=" + item.kind + " position=" + item.transform.position + " scale=" + item.transform.lossyScale + " bounds=" + BoundsOf(item.gameObject));
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                report.AppendLine("CAMERA " + camera.name + " parent=" + camera.transform.parent?.name + " position=" + camera.transform.position + " rotation=" + camera.transform.eulerAngles);
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name.Contains("Stand") || t.name == "banana box" || t.name.Contains("Farm") || t.name.Contains("Sractch")))
                report.AppendLine("PROP " + t.name + " position=" + t.position + " bounds=" + BoundsOf(t.gameObject));
            File.WriteAllText(Path.GetFullPath("../.menu-tools/day2-probe.txt"), report.ToString());
            Debug.Log("[Zoo Day 2] Probe complete.");
            EditorApplication.Exit(0);
        }
        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            if (renderers.Length > 0) { bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds); }
            return bounds;
        }
    }
}
