using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ADayAtTheZoo.Monkeys.Care;

[InitializeOnLoad]
public static class HighlandLayoutApply
{
    const string Work="work/monkey-care/";
    static HighlandLayoutApply(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Work+"layout-apply.request"))return;
        File.Delete(Work+"layout-apply.request");
        try{Apply();}catch(Exception e){File.WriteAllText(Work+"layout-result.txt",e.ToString());}
    }
    [Serializable] sealed class Move{public string path;public Vector3 before,after;}
    [Serializable] sealed class Report{public string snapshot;public Move[] moves;public string[] preserved;}
    [MenuItem("Tools/猴子照顾/应用已确认的局部森林布局")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();if(scene.path!=MonkeyCareInstaller.ScenePath)throw new Exception("Wrong scene");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        var byPath=all.GroupBy(PathOf).ToDictionary(g=>g.Key,g=>g.First());
        var changes=new Dictionary<string,Vector2>{
            {"Highland Lake/03 Vegetation/Bush_group_average 132",new Vector2(-12.8f,-13.1f)},
            {"Highland Lake/03 Vegetation/Bush_average_flowers 159",new Vector2(-5.5f,-10.7f)},
            {"Highland Lake/03 Vegetation/Bush_group_big 160",new Vector2(5.5f,-10.8f)},
            {"Highland Lake/03 Vegetation/Bush_average_flowers 192",new Vector2(3.2f,-8.7f)},
            {"Highland Lake/03 Vegetation/Bush_group_big 174",new Vector2(-12f,-22.3f)},
            {"Highland Lake/03 Vegetation/Bush_group_average 221",new Vector2(-14.5f,-10.3f)},
            {"Highland Lake/03 Vegetation/Tree_Fir_average 23",new Vector2(17f,-13.8f)},
            {"Highland Lake (1)/03 Vegetation/Tree_low_lush 87",new Vector2(3f,-25f)},
            {"Highland Lake/03 Vegetation/Tree_Fir_average 1",new Vector2(8.6f,-25.8f)},
            {"Highland Lake/03 Vegetation/Flower_high_purple 371",new Vector2(12.6f,-20.4f)}
        };
        foreach(var key in changes.Keys)if(!byPath.ContainsKey(key))throw new Exception("Missing layout target: "+key);
        var ground=all.Single(t=>t.name=="Island_1").GetComponent<Collider>();
        var planned=new List<Move>();
        foreach(var pair in changes)
        {
            var t=byPath[pair.Key];var before=t.position;
            var after=new Vector3(pair.Value.x,before.y,pair.Value.y);
            if(!ground.Raycast(new Ray(before+Vector3.up*20,Vector3.down),out var oldFloor,50)||
               !ground.Raycast(new Ray(after+Vector3.up*20,Vector3.down),out var newFloor,50)||newFloor.normal.y<.6f)throw new Exception("Invalid ground: "+pair.Key);
            after.y=newFloor.point.y+(before.y-oldFloor.point.y);
            planned.Add(new Move{path=pair.Key,before=before,after=after});
        }
        string backup=File.ReadAllText(Work+"layout-backup-path.txt").Trim();
        string snapshot=backup+"/HighlandLake_EditorBeforeLayout_"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
        if(!EditorSceneManager.SaveScene(scene,snapshot,true))throw new Exception("Cannot save editor snapshot");
        Undo.SetCurrentGroupName("森林核心区局部布局");int undo=Undo.GetCurrentGroup();
        foreach(var m in planned)
        {
            var t=byPath[m.path];Undo.RecordObject(t,"局部森林布局");t.position=m.after;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);EditorUtility.SetDirty(t);
        }
        Physics.SyncTransforms();
        var session=UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>();
        foreach(var npc in session.monkeys)if(!session.locomotion.IsLandingClear(npc.teleportPoint.Landing))
        {Undo.RevertAllDownToGroup(undo);throw new Exception("Layout obstructs teleport: "+npc.name);}
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new Exception("Cannot save layout");
        var report=new Report{snapshot=snapshot,moves=planned.ToArray(),preserved=new[]{"All NPC transforms","All rock transforms","Supply props and their references","Far forest","Original models and materials"}};
        File.WriteAllText(Work+"layout-changes.json",JsonUtility.ToJson(report,true));
        File.WriteAllText(Work+"layout-result.txt","SUCCESS "+planned.Count+" models repositioned; all three teleport landings clear; "+snapshot);
        HighlandLayoutPreview.Capture("after");
    }
}
