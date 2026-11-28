using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ADayAtTheZoo.PC.Editor
{
    public static class ZooPCSceneScriptRepair
    {
        // A second MonoBehaviour declared in CareFarmEnvironment.cs was saved
        // as a scene-local MonoScript. The Editor cannot resolve that component;
        // a player resolves its type but receives no serialized field payload.
        // Remove only these invalid records in our dedicated PC scene copies.
        // Day 01's existing CareFarmEnvironment recreates the marker at runtime;
        // Day 02 has its own supply marker. Never edit the team's source scene.
        public static int RemoveEmbeddedPickupMarker(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            if (!path.StartsWith("Assets/ZooPCMenu/", StringComparison.Ordinal) || !path.EndsWith(".unity", StringComparison.Ordinal))
                throw new ArgumentException("Only dedicated ZooPCMenu scene copies may be repaired.", nameof(path));
            string source = File.ReadAllText(path);
            var records = Regex.Split(source, @"(?m)(?=^--- !u!)");
            var components = new HashSet<string>();
            var scripts = new HashSet<string>();
            foreach (string record in records)
            {
                if (!record.Contains("m_EditorClassIdentifier: Assembly-CSharp::ADayAtTheZoo.Monkeys.Care.CarePickupMarker")) continue;
                var script = Regex.Match(record, @"m_Script: \{fileID: (-?\d+)\}\r?\n");
                var header = Regex.Match(record, @"^--- !u!114 &(-?\d+)");
                if (!script.Success || !header.Success) continue;
                components.Add(header.Groups[1].Value); scripts.Add(script.Groups[1].Value);
            }
            if (components.Count == 0) return 0;
            var result = new StringBuilder(source.Length);
            foreach (string record in records)
            {
                var header = Regex.Match(record, @"^--- !u!(\d+) &(-?\d+)");
                if (header.Success && ((header.Groups[1].Value == "114" && components.Contains(header.Groups[2].Value)) ||
                    (header.Groups[1].Value == "115" && scripts.Contains(header.Groups[2].Value)))) continue;
                string cleaned = record;
                foreach (string id in components)
                    cleaned = Regex.Replace(cleaned, @"(?m)^  - component: \{fileID: " + Regex.Escape(id) + @"\}\r?\n", "");
                result.Append(cleaned);
            }
            File.WriteAllText(path, result.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log("[Zoo PC Scene] Removed " + components.Count + " invalid embedded marker record(s) from " + path);
            return components.Count;
        }
    }
}
