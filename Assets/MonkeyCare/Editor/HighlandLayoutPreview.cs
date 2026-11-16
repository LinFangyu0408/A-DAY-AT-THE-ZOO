using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ADayAtTheZoo.Monkeys.Care;

[InitializeOnLoad]
public static class HighlandLayoutPreview
{
    const string Work="work/monkey-care/";
    static HighlandLayoutPreview(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Work+"layout-preview.request"))return;
        string label=File.ReadAllText(Work+"layout-preview.request").Trim(); File.Delete(Work+"layout-preview.request");
        try{Capture(label);}catch(Exception e){File.WriteAllText(Work+"layout-preview-error.txt",e.ToString());}
    }
    public static void Capture(string label)
    {
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=MonkeyCareInstaller.ScenePath)throw new Exception("Wrong scene");
        var session=UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>();
        string folder=Work+"layout-"+label;Directory.CreateDirectory(folder);
        var go=new GameObject("Temporary Layout Preview");go.hideFlags=HideFlags.HideAndDontSave;
        var camera=go.AddComponent<Camera>();camera.fieldOfView=65;camera.nearClipPlane=.05f;camera.farClipPlane=200;
        try
        {
            for(int i=0;i<session.monkeys.Length;i++)
            {
                var t=session.monkeys[i].transform; var center=t.position+Vector3.up*.95f;
                camera.transform.position=center+t.forward*4.8f+Vector3.up*.25f;camera.transform.LookAt(center);
                Save(camera,folder+"/npc-"+(i+1)+"-front.png");
                camera.transform.position=center+Quaternion.AngleAxis(60,Vector3.up)*t.forward*4.8f+Vector3.up*.25f;camera.transform.LookAt(center);
                Save(camera,folder+"/npc-"+(i+1)+"-side.png");
            }
            camera.transform.position=new Vector3(0,16,-27);camera.transform.LookAt(new Vector3(0,0,-16));Save(camera,folder+"/overview.png");
            var npc=session.monkeys[0].transform;var look=npc.position+Vector3.up*.95f;
            camera.transform.position=look+npc.forward*3.4f+Vector3.up*.25f;camera.transform.LookAt(look);
            var canvas=CareUi.Canvas("Ready preview",camera.transform,new Vector3(0,0,1.8f),1,new Vector2(800,470));
            CareUi.Image("READY GO",canvas,session.readyGo,new Vector2(800,470),Vector2.zero);
            CareReadyView.Decorate(canvas,session.roundArtwork.star,3);
            Canvas.ForceUpdateCanvases();Save(camera,folder+"/ready.png");
            var details=canvas.GetComponentsInChildren<Graphic>().Select(g=>{
                var mesh=g.canvasRenderer.GetMesh();
                string line=g.name+" verts="+mesh.vertexCount+" material="+g.material.name+" color="+g.color;
                return line;
            });File.WriteAllLines(folder+"/ready-render-state.txt",details);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
        File.WriteAllText(Work+"layout-preview-result.txt","SUCCESS "+folder);
    }
    static void Save(Camera camera,string path)
    {
        var rt=new RenderTexture(1280,900,24);var tex=new Texture2D(1280,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,900),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
    }
}

