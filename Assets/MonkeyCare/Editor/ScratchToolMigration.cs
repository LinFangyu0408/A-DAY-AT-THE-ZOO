using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ADayAtTheZoo.Monkeys.Care;
[InitializeOnLoad] public static class ScratchToolMigration {
 static ScratchToolMigration(){EditorApplication.update+=Tick;}
 static void Tick(){
 if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists("work/monkey-care/scratch-migrate.request"))return;
 File.Delete("work/monkey-care/scratch-migrate.request");
 try{Run();}catch(Exception e){File.WriteAllText("work/monkey-care/scratch-migrate-result.txt",e.ToString());}
 }
 static void Run(){
 var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!=MonkeyCareInstaller.ScenePath)throw new Exception("Wrong scene");
 var session=UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>();
 var old=session.items.Single(i=>i.kind==CareNeed.Itch);var banana=session.items.Single(i=>i.kind==CareNeed.Hunger);
 var target=old.transform.parent.Cast<Transform>().Single(t=>t.name=="base_basic_shaded (9)").gameObject;
 if(target==banana.gameObject||target==old.gameObject)throw new Exception("Invalid target");
 var folder=File.ReadAllText("work/monkey-care/scratch-backup.txt").Trim();
 if(!EditorSceneManager.SaveScene(scene,folder+"/EditorBeforeMigration-"+DateTime.Now.ToString("HHmmss")+".unity",true))throw new Exception("Backup failed");
 var care=target.GetComponent<CareItem>();if(!care)care=Undo.AddComponent<CareItem>(target);EditorUtility.CopySerialized(old,care);
 var body=target.GetComponent<Rigidbody>();if(!body)body=Undo.AddComponent<Rigidbody>(target);EditorUtility.CopySerialized(old.GetComponent<Rigidbody>(),body);
 var box=target.GetComponent<BoxCollider>();if(!box)box=Undo.AddComponent<BoxCollider>(target);EditorUtility.CopySerialized(old.GetComponent<BoxCollider>(),box);
 var filters=target.GetComponentsInChildren<MeshFilter>();bool first=true;Bounds bounds=default;
 foreach(var f in filters){var b=f.sharedMesh.bounds;for(int k=0;k<8;k++){var p=target.transform.InverseTransformPoint(f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1))));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}}
 if(first)throw new Exception("No target mesh");box.center=bounds.center;box.size=bounds.size;
 var map=new System.Collections.Generic.Dictionary<UnityEngine.Object,UnityEngine.Object>{{old,care},{old.gameObject,target},{old.transform,target.transform},{old.GetComponent<Rigidbody>(),body},{old.GetComponent<BoxCollider>(),box}};
 foreach(var root in scene.GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Component>(true)){
 if(!c || c is Transform)continue;var so=new SerializedObject(c);var p=so.GetIterator();bool changed=false;
 while(p.Next(true)){if(p.propertyType==SerializedPropertyType.ObjectReference&&p.name!="m_GameObject"&&p.name!="m_Father"&&p.objectReferenceValue&&map.TryGetValue(p.objectReferenceValue,out var replacement)){p.objectReferenceValue=replacement;changed=true;}}
 if(changed)so.ApplyModifiedProperties();
 }
 Undo.RecordObject(old.gameObject,"Retire old scratch tool");old.gameObject.SetActive(false);
 foreach(var c in new Component[]{care,body,box,session}){EditorUtility.SetDirty(c);PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
 if(session.items.Single(i=>i.kind==CareNeed.Itch)!=care||session.items.Single(i=>i.kind==CareNeed.Hunger)!=banana)throw new Exception("Reference validation failed");
 EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
 File.WriteAllText("work/monkey-care/scratch-migrate-result.txt","SUCCESS new="+target.transform.parent.name+"/"+target.name+"; old disabled; banana unchanged; collider fitted; backup="+folder);
 }
}
