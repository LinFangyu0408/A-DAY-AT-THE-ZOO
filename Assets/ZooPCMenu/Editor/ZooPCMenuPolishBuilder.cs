using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC.Editor
{
    public static class ZooPCMenuPolishBuilder
    {
        const string Root="Assets/ZooPCMenu/";
        public static void PrepareAndPreview(){Prepare();ZooPCMenuValidation.Preview();}
        public static void PrepareAndValidate(){Prepare();ZooPCMenuValidation.Full();}
        [MenuItem("Zoo/PC Menu/Refresh menu art and collection portraits")]
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            var assets=AssetDatabase.LoadAssetAtPath<ZooMenuAssets>(Root+"Resources/ZooPCMenu/MenuAssets.asset");
            var scene=EditorSceneManager.OpenScene(assets.tutorialScene);
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))light.enabled=false;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.75f,.75f,.7f);RenderSettings.fog=false;
            var session=UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>();
            var animator=session.monkeys[0].GetComponentInChildren<Animator>();
            var crate=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="banana box");
            Capture(animator.gameObject,"CollectionMonkey",true);
            Capture(crate.gameObject,"CollectionBananaCrate",false);
            AssetDatabase.Refresh();
            assets=AssetDatabase.LoadAssetAtPath<ZooMenuAssets>(Root+"Resources/ZooPCMenu/MenuAssets.asset");
            assets.titleLogo=Texture("MenuLogoCraft");assets.leafButton=Texture("MenuLeafButton");assets.linenButton=Texture("MenuLinenButton");
            assets.monkeyPortrait=Texture("CollectionMonkey");assets.cratePortrait=Texture("CollectionBananaCrate");
            assets.headingFont=AssetDatabase.LoadAssetAtPath<Font>(Root+"Art/Fonts/LilitaOne-Regular.ttf");
            assets.bodyFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/MonkeyCare/Resources/Fonts/Fredoka-SemiBold.ttf");
            assets.noteFont=AssetDatabase.LoadAssetAtPath<Font>(Root+"Art/Fonts/Caveat-Variable.ttf");
            assets.animals=Collection("meadow-monkey","Meadow monkey","Original form",assets.monkeyPortrait);
            assets.facilities=Collection("banana-crate","Banana crate","Supply equipment",assets.cratePortrait);
            EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            // Discard the temporary lighting and portrait setup. No gameplay scene is saved.
            EditorSceneManager.OpenScene(assets.menuScene);
            Debug.Log("[Zoo PC Polish] Collection portraits and UI art ready; original scenes unchanged.");
        }
        static ZooCollectionEntry[] Collection(string id,string name,string subtitle,Texture2D portrait)
        {
            var entries=new ZooCollectionEntry[6];
            entries[0]=new ZooCollectionEntry{id=id,displayName=name,subtitle=subtitle,portrait=portrait,unlocked=true,discoveredDay=1};
            for(int i=1;i<entries.Length;i++)entries[i]=new ZooCollectionEntry{id="undiscovered-"+i,displayName="",subtitle="",portrait=null,unlocked=false};
            return entries;
        }
        static Texture2D Texture(string name)
        {
            string path=Root+"Art/"+name+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer){importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void Capture(GameObject source,string name,bool animated)
        {
            var stage=new GameObject("Collection portrait studio");stage.transform.position=new Vector3(2000,0,0);
            var model=UnityEngine.Object.Instantiate(source,stage.transform);model.SetActive(true);model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.enabled=true;
            Layer(model,28);
            if(animated)
            {
                var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/MonkeyCare/Generated/Care_StandingIdle.anim");
                AnimationMode.StartAnimationMode();AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(model,idle,.65f);AnimationMode.EndSampling();
                foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=new Mesh();skin.BakeMesh(mesh);
                    var baked=new GameObject("Portrait pose",typeof(MeshFilter),typeof(MeshRenderer));baked.layer=28;baked.transform.SetParent(skin.transform,false);
                    baked.GetComponent<MeshFilter>().sharedMesh=mesh;baked.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
                }
            }
            var renderers=model.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            if(renderers.Length==0)throw new InvalidOperationException("No renderers for "+name);
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            var key=new GameObject("Portrait key",typeof(Light));key.transform.SetParent(stage.transform);key.transform.rotation=Quaternion.Euler(45,-35,0);var light=key.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;light.color=new Color(1,.95f,.82f);light.cullingMask=1<<28;
            var cameraObject=new GameObject("Portrait camera",typeof(Camera));cameraObject.transform.SetParent(stage.transform);var camera=cameraObject.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<28;camera.orthographic=true;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.allowHDR=false;
            var direction=animated?new Vector3(.55f,.25f,3):new Vector3(2.2f,1.5f,-3);
            camera.transform.position=bounds.center+direction.normalized*Mathf.Max(bounds.size.magnitude*2,3);camera.transform.LookAt(bounds.center);
            float height=0,width=0;foreach(var r in renderers){var b=r.bounds;for(int i=0;i<8;i++){var point=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var v=camera.transform.InverseTransformPoint(point);height=Mathf.Max(height,Mathf.Abs(v.y));width=Mathf.Max(width,Mathf.Abs(v.x));}}
            camera.orthographicSize=Mathf.Max(height,width)*1.08f;
            var rt=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32){antiAliasing=4};camera.targetTexture=rt;camera.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1024,1024,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1024,1024),0,0);image.Apply();File.WriteAllBytes(Root+"Art/"+name+".png",image.EncodeToPNG());RenderTexture.active=old;
            if(animated)AnimationMode.StopAnimationMode();
            UnityEngine.Object.DestroyImmediate(stage);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
        }
        static void Layer(GameObject go,int layer){go.layer=layer;foreach(Transform child in go.transform)Layer(child.gameObject,layer);}
    }
}
