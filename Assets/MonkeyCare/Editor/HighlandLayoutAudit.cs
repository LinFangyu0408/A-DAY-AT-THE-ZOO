using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class HighlandLayoutAudit
{
    const string Folder = "work/monkey-care/";
    static double nextStatus;
    static HighlandLayoutAudit()
    {
        File.WriteAllText(Folder + "layout-editor-state.txt", "Audit loaded " + DateTime.Now.ToString("s"));
        EditorApplication.update += Tick;
        if (File.Exists(Folder + "layout-audit.request"))
        {
            try { Run(); } catch (Exception e) { File.WriteAllText(Folder + "layout-audit-error.txt", e.ToString()); }
        }
    }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > nextStatus)
        {
            nextStatus = EditorApplication.timeSinceStartup + 5;
            File.WriteAllText(Folder + "layout-editor-state.txt", DateTime.Now.ToString("s") + " playing=" + EditorApplication.isPlayingOrWillChangePlaymode + " compiling=" + EditorApplication.isCompiling + " updating=" + EditorApplication.isUpdating + " scene=" + SceneManager.GetActiveScene().path);
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Folder + "layout-audit.request")) return;
        File.Delete(Folder + "layout-audit.request");
        try { Run(); } catch(Exception e) { File.WriteAllText(Folder + "layout-audit-error.txt", e.ToString()); }
    }
    [Serializable] sealed class Entry { public string name,path; public Vector3 position,size,center; }
    [Serializable] sealed class Report { public Entry[] objects; }
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;
    static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if(scene.path != MonkeyCareInstaller.ScenePath) throw new Exception("Wrong active scene");
        var all = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
        var entries = all.Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t.name,@"^(Tree_|BananaTree|Bush_|Grass_|Flower_|Rock_|Monkey_Wave|02奶咖|Wooden crate|Player and Locomotion|Island_1)") ||
            (t.GetComponent<Renderer>() && t.position.x > -18 && t.position.x < 20 && t.position.z > -28 && t.position.z < -5))
            .Select(t=> {
                var rs=t.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)).ToArray();
                var b=new Bounds(t.position,Vector3.zero); if(rs.Length>0){ b=rs[0].bounds; foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds); }
                return new Entry{name=t.name,path=PathOf(t),position=t.position,size=b.size,center=b.center};
            }).ToArray();
        File.WriteAllText(Folder + "layout-inventory.json",JsonUtility.ToJson(new Report{objects=entries},true));
    }
}
