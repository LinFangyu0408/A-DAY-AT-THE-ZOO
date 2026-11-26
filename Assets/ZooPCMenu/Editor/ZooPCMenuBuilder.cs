using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC.Editor
{
    public static class ZooPCMenuBuilder
    {
        const string Root="Assets/ZooPCMenu/";
        const string SourceScene="Assets/Environment Aseets/01_HighlandLake_可玩场景/Scenes/HighlandLake_Backup_2026-11-23.unity";
        [MenuItem("Zoo/PC Menu/Rebuild dedicated PC scenes")]
        public static void Setup()
        {
            var assets=AssetDatabase.LoadAssetAtPath<ZooMenuAssets>(Root+"Resources/ZooPCMenu/MenuAssets.asset");
            if(!assets){assets=ScriptableObject.CreateInstance<ZooMenuAssets>();AssetDatabase.CreateAsset(assets,Root+"Resources/ZooPCMenu/MenuAssets.asset");}
            assets.cover=Texture("MenuCover");assets.paper=Texture("JournalPaper");assets.journalTitle=Texture("JournalTitle");assets.journalCard=Texture("JournalCard");
            assets.wood=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MonkeyCare/Resources/Farm/MarketWood.png");
            assets.bookPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EndlessBook/Book/Prefabs/Book.prefab");
            assets.headingFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/MonkeyCare/Resources/Fonts/Fredoka-SemiBold.ttf");
            assets.bodyFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/MonkeyCare/Resources/Fonts/Arial-Bold.ttf");
            assets.pageTurn=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Art/PageTurn.wav");
            foreach(var name in new[]{"MenuCover","JournalPaper","JournalTitle","JournalCard"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"Art/"+name+".png");
                importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.SaveAndReimport();
            }
            EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(SourceScene,OpenSceneMode.Single);
            assets=AssetDatabase.LoadAssetAtPath<ZooMenuAssets>(Root+"Resources/ZooPCMenu/MenuAssets.asset");
            new GameObject("PC menu navigation").AddComponent<ZooPCSessionBridge>();
            var session=UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>(FindObjectsInactive.Include);
            if(session)session.desktopPreview=true;
            EditorSceneManager.SaveScene(scene,assets.tutorialScene);
            // A real view from the tutorial, used as the journal's location photograph.
            Vector3 photoCenter=Vector3.zero;int monkeyCount=0;
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/MonkeyCare/Generated/Care_StandingIdle.anim");
            AnimationMode.StartAnimationMode();
            if(session)foreach(var npc in session.monkeys)
            {
                if(!npc)continue;photoCenter+=npc.transform.position;monkeyCount++;
                var animator=npc.GetComponentInChildren<Animator>();
                if(animator&&idle){AnimationMode.BeginSampling();AnimationMode.SampleAnimationClip(animator.gameObject,idle,.6f);AnimationMode.EndSampling();}
                foreach(var skin in npc.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=new Mesh();skin.BakeMesh(mesh);
                    var baked=new GameObject("Photo pose",typeof(MeshFilter),typeof(MeshRenderer));baked.transform.SetParent(skin.transform,false);
                    baked.GetComponent<MeshFilter>().sharedMesh=mesh;baked.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
                }
            }
            if(monkeyCount>0)photoCenter/=monkeyCount;
            var cameraGo=new GameObject("Journal location photograph",typeof(Camera));var camera=cameraGo.GetComponent<Camera>();
            camera.transform.position=photoCenter+new Vector3(1.5f,3,-8.5f);camera.transform.LookAt(photoCenter+Vector3.up*.8f);camera.fieldOfView=55;camera.nearClipPlane=.1f;camera.farClipPlane=500;
            camera.clearFlags=CameraClearFlags.Skybox;var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;var photo=new Texture2D(1280,720,TextureFormat.RGB24,false);photo.ReadPixels(new Rect(0,0,1280,720),0,0);photo.Apply();File.WriteAllBytes(Root+"Art/MonkeyMeadow.png",photo.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(cameraGo);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(photo);
            AnimationMode.StopAnimationMode();
            AssetDatabase.ImportAsset(Root+"Art/MonkeyMeadow.png");assets=AssetDatabase.LoadAssetAtPath<ZooMenuAssets>(Root+"Resources/ZooPCMenu/MenuAssets.asset");assets.levelPhoto=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Art/MonkeyMeadow.png");
            EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            var menu=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            assets=AssetDatabase.LoadAssetAtPath<ZooMenuAssets>(Root+"Resources/ZooPCMenu/MenuAssets.asset");
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.78f,.77f,.7f);RenderSettings.fog=false;
            new GameObject("PC Main Menu").AddComponent<ZooPCFlow>().assets=assets;
            var menuCamera=new GameObject("Menu presentation camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            menuCamera.cullingMask=0;menuCamera.clearFlags=CameraClearFlags.SolidColor;menuCamera.backgroundColor=ZooMenuUI.Ink;menuCamera.depth=-100;
            EditorSceneManager.SaveScene(menu,assets.menuScene);
            AssetDatabase.SaveAssets();Debug.Log("[Zoo PC Menu] Setup complete. Original tutorial and VR assets were not modified.");
        }
        static Texture2D Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Art/"+name+".png");
        public static void SetupBatch(){Setup();EditorApplication.Exit(0);}
        [MenuItem("Zoo/PC Menu/Open main menu")]
        public static void Open(){EditorSceneManager.OpenScene(Root+"Scenes/PC_MainMenu.unity");}
        [MenuItem("Zoo/PC Menu/Build Windows preview")]
        public static void Build()
        {
            var assets=Resources.Load<ZooMenuAssets>("ZooPCMenu/MenuAssets");
            Directory.CreateDirectory("Builds/PCMenu");
            var xr=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            bool xrAuto=xr&&xr.InitManagerOnStart;bool native=PlayerSettings.defaultIsNativeResolution;
            var mode=PlayerSettings.fullScreenMode;int width=PlayerSettings.defaultScreenWidth,height=PlayerSettings.defaultScreenHeight;
            try
            {
                // Only this PC player skips headset startup. Restore shared editor settings afterwards.
                if(xr)xr.InitManagerOnStart=false;
                PlayerSettings.defaultIsNativeResolution=false;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
                using(var shaders=new ZooPCShaderBuildScope())
                {
                    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{assets.menuScene,assets.tutorialScene},locationPathName="Builds/PCMenu/A-Day-At-The-Zoo.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                    if(report.summary.result!=BuildResult.Succeeded)throw new Exception("PC build failed: "+report.summary.result);
                }
            }
            finally
            {
                if(xr)xr.InitManagerOnStart=xrAuto;
                PlayerSettings.defaultIsNativeResolution=native;PlayerSettings.fullScreenMode=mode;PlayerSettings.defaultScreenWidth=width;PlayerSettings.defaultScreenHeight=height;
            }
            Debug.Log("[Zoo PC Menu] Windows preview build succeeded.");
        }
        public static void BuildBatch(){Build();EditorApplication.Exit(0);}
        public static void ValidateAndBuild(){SessionState.SetBool("ZooMenu.BuildAfter",true);ZooPCMenuValidation.Full();}
        [InitializeOnLoadMethod]
        static void ContinueValidatedBuild()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("ZooMenu.BuildAfter",false))
                {
                    SessionState.SetBool("ZooMenu.BuildAfter",false);EditorApplication.delayCall+=BuildBatch;
                }
            };
        }
    }
}
