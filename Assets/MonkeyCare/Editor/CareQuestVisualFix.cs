using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ADayAtTheZoo.Monkeys.Care;
[InitializeOnLoad] public static class CareQuestVisualFix {
 static CareQuestVisualFix(){EditorApplication.update+=Tick;}
 static void Tick(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("work/monkey-care/quest-fix.request"))return;File.Delete("work/monkey-care/quest-fix.request");try{Run();}catch(Exception e){File.WriteAllText("work/monkey-care/quest-fix-result.txt",e.ToString());}}
 static void Run(){var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!=MonkeyCareInstaller.ScenePath)throw new Exception("Wrong scene");var s=UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>();var folder=File.ReadAllText("work/monkey-care/quest-backup.txt").Trim();if(!EditorSceneManager.SaveScene(scene,folder+"/BeforeQuestFix.unity",true))throw new Exception("Backup failed");int count=0;
 foreach(var b in s.rig.GetComponentsInChildren<MonoBehaviour>(true)){if(!b)continue;string type=b.GetType().Name;if(type!="RayInteractorRayVisual"&&type!="ControllerRayVisual")continue;var so=new SerializedObject(b);var p=so.FindProperty("_renderer");if(p!=null&&p.objectReferenceValue is Renderer r){r.enabled=false;EditorUtility.SetDirty(r);PrefabUtility.RecordPrefabInstancePropertyModifications(r);}b.enabled=false;EditorUtility.SetDirty(b);PrefabUtility.RecordPrefabInstancePropertyModifications(b);count++;}
 foreach(var n in s.monkeys){var c=n.transform.Find("Care Contact").GetComponent<BoxCollider>();if(!c||!c.isTrigger)throw new Exception("Missing box trigger "+n.name);}
 EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");File.WriteAllText("work/monkey-care/quest-fix-result.txt","SUCCESS disabled SDK ray visuals="+count+"; all NPC Box Triggers verified; "+folder);}
}
