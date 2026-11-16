using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// One-shot setup for Meta Quest 3. Safe to delete after run.
// Uses Meta's OVRProjectSetup.FixAllAsync - the official one-click fix.
public static class Quest3AutoSetup
{
    const string ResultFile = "Quest3SetupResult.txt";
    static System.Text.StringBuilder log;

    [MenuItem("Tools/Quest3 Auto Setup")]
    public static void Run()
    {
        log = new System.Text.StringBuilder();
        try
        {
            log.AppendLine("=== Quest3 Setup via Meta OVRProjectSetup ===");
            log.AppendLine("Unity: " + Application.unityVersion);
            log.AppendLine("Time: " + DateTime.Now.ToString("s"));

            // Load Meta XR SDK editor assemblies
            string dir = Path.Combine(Application.dataPath, "..", "Library", "ScriptAssemblies");
            foreach (var n in Directory.GetFiles(dir, "*.dll"))
            { try { Assembly.LoadFrom(n); } catch { } }

            Type ovrSetup = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            { ovrSetup = asm.GetType("OVRProjectSetup"); if (ovrSetup != null) break; }
            log.AppendLine("OVRProjectSetup found: " + (ovrSetup != null));

            if (ovrSetup == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    foreach (var t in asm.GetTypes())
                        if (t.Name == "OVRProjectSetup") { ovrSetup = t; break; }
                log.AppendLine("OVRProjectSetup(by name): " + (ovrSetup != null));
            }

            var fixAll = ovrSetup.GetMethod("FixAllAsync", BindingFlags.Public | BindingFlags.Static);
            log.AppendLine("FixAllAsync found: " + (fixAll != null));

            foreach (var target in new[] { "Android", "Standalone" })
            {
                object btg = Enum.Parse(typeof(UnityEditor.BuildTargetGroup), target);
                log.AppendLine("FixAll(" + target + ")...");
                var task = (Task)fixAll.Invoke(null, new object[] { btg });
                task.Wait(TimeSpan.FromMinutes(3));
                log.AppendLine("FixAll(" + target + ") done, completed=" + task.IsCompleted);
            }

            AssetDatabase.SaveAssets();
            log.AppendLine("RESULT: SUCCESS");
        }
        catch (Exception e) { log.AppendLine("RESULT: FAILED\n" + e); }
        string path = Path.Combine(Application.dataPath, "..", ResultFile);
        File.WriteAllText(path, log.ToString());
        Debug.Log(log.ToString());
    }
}
