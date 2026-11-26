#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC.Editor
{
    // Runs the actual runtime menu and book Animator in Play mode. All captures
    // come from Unity cameras; the validation does not synthesize UI frames.
    [DefaultExecutionOrder(10000)]
    public sealed class ZooPCMenuValidation : MonoBehaviour
    {
        static readonly string Output=Path.GetFullPath("../.menu-tools/validation");
        readonly List<string> checks=new();
        Camera uiCamera;
        RenderTexture target;
        Texture2D pixels;
        int videoFrame;
        bool record;
        float oldVolume;
        int oldReduced;
        readonly Dictionary<string,int?> oldProgress=new();
        double deadline;
        public static void Preview(){Launch(false);}
        public static void Full(){Launch(true);}
        static void Launch(bool full)
        {
            Directory.CreateDirectory(Output);SessionState.SetBool("ZooMenu.Validation",true);SessionState.SetBool("ZooMenu.Full",full);
            EditorSceneManager.OpenScene("Assets/ZooPCMenu/Scenes/PC_MainMenu.unity");EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("ZooMenu.Validation",false))
                {
                    SessionState.SetBool("ZooMenu.Validation",false);
                    new GameObject("PC menu validation").AddComponent<ZooPCMenuValidation>();
                }
            };
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);Application.runInBackground=true;
            deadline=EditorApplication.timeSinceStartup+900;
            foreach(var key in new[]{"ZooPC.Day1.Completed","ZooPC.Day1.Stars","ZooPC.Day1.Feedings"})oldProgress[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):(int?)null;
            oldVolume=PlayerPrefs.GetFloat("ZooPC.Volume",.8f);oldReduced=PlayerPrefs.GetInt("ZooPC.ReducedMotion",0);PlayerPrefs.SetInt("ZooPC.ReducedMotion",0);
            Time.captureFramerate=30;
            var go=new GameObject("Offscreen validation camera",typeof(Camera));go.transform.SetParent(transform);uiCamera=go.GetComponent<Camera>();uiCamera.enabled=false;uiCamera.transform.position=new Vector3(0,0,-10);uiCamera.cullingMask=1<<29;uiCamera.clearFlags=CameraClearFlags.SolidColor;uiCamera.backgroundColor=ZooMenuUI.Ink;uiCamera.nearClipPlane=.01f;uiCamera.farClipPlane=100;
            target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32){antiAliasing=2};target.Create();pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            for(int i=0;i<15;i++)yield return null;
            var flow=ZooPCFlow.Instance;Require(flow!=null,"Menu booted");
            Snapshot("01-main-menu");
            if(SessionState.GetBool("ZooMenu.Full",false)){Directory.CreateDirectory(Output+"/frames");record=true;}
            yield return Frames(65);
            Click("Start Game");yield return Frames(60);Require(flow.VisibleScreen=="Journal"&&flow.Journal.Day==0,"Start opens Day 01");Snapshot("02-journal-day1");
            var page=(RenderTexture)flow.Journal.Book.GetPageData(2).material.mainTexture;var previousRt=RenderTexture.active;RenderTexture.active=page;
            var pagePixels=new Texture2D(page.width,page.height,TextureFormat.RGB24,false);pagePixels.ReadPixels(new Rect(0,0,page.width,page.height),0,0);pagePixels.Apply();File.WriteAllBytes(Output+"/printed-day1.png",pagePixels.EncodeToPNG());Destroy(pagePixels);RenderTexture.active=previousRt;
            yield return Frames(65);Click("Next day");yield return Frames(16);Snapshot("03-page-turn");yield return Frames(50);
            Require(flow.Journal.Day==1&&flow.Journal.Book.CurrentRightPageNumber==4,"Forward page turn finished on Day 02");
            string before=SceneManager.GetActiveScene().path;flow.EnterDay();Require(SceneManager.GetActiveScene().path==before&&!flow.IsLoading,"Day 02 cannot load a level");Snapshot("04-journal-day2");
            yield return Frames(65);Click("Previous day");yield return Frames(50);Require(flow.Journal.Day==0,"Backward page turn restores Day 01");
            Click("Animals tab");yield return Frames(55);Snapshot("05-animal-guide");yield return Frames(60);
            Click("Facilities tab");yield return Frames(55);Snapshot("06-facility-guide");yield return Frames(60);
            Click("Back to menu");yield return Frames(2);Click("Continue");yield return Frames(45);Require(flow.Journal.Day==0&&flow.Journal.Section==0,"Continue opens the first log entry");
            Click("Journal settings");yield return Frames(20);Snapshot("07-settings");
            var slider=FindObjectsByType<Slider>(FindObjectsSortMode.None).First(s=>s.name=="Master volume");slider.value=.47f;
            Require(Mathf.Abs(AudioListener.volume-.47f)<.01f,"Volume applies immediately");Click("Done");yield return Frames(2);Click("Journal settings");yield return Frames(10);
            Require(Mathf.Abs(FindObjectsByType<Slider>(FindObjectsSortMode.None).First().value-.47f)<.01f,"Settings survive closing and reopening");Click("Done");
            if(!SessionState.GetBool("ZooMenu.Full",false)){Finish();yield break;}
            yield return Frames(40);Click("Play Day 1");yield return WaitLoad();yield return Frames(100);
            Require(flow.InGameplay&&MonkeyCareSession.Instance!=null,"Playable PC tutorial loaded");Snapshot("08-pc-tutorial");
            flow.ShowPause();yield return Frames(35);Require(Time.timeScale==0&&flow.Paused,"Pause stops the round clock");Snapshot("09-pause");
            float elapsed=MonkeyCareSession.Instance.RoundElapsed;yield return Frames(50);Require(Mathf.Abs(MonkeyCareSession.Instance.RoundElapsed-elapsed)<.001f,"Round remains paused");
            flow.Resume();yield return Frames(10);Require(Time.timeScale==1&&!flow.Paused,"Resume restores the round");
            flow.RetryDay();yield return WaitLoad();yield return Frames(65);Require(MonkeyCareSession.Instance.RoundElapsed<.1f,"Retry loads a fresh Day 01");
            // Drive the real round to its time limit for navigation coverage.
            // This does not claim to be a player-completed feeding playtest.
            var session=MonkeyCareSession.Instance;
            typeof(MonkeyCareSession).GetMethod("AcceptReadyGate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,null);
            yield return Frames(110);
            typeof(MonkeyCareSession).GetField("gameTime",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(session,121f);
            yield return Frames(30);Require(session.Round.Frozen,"Real results screen reached at cutoff");Snapshot("10-results-navigation");
            yield return Frames(75);Click("Continue exploring");yield return WaitLoad();yield return Frames(50);
            Require(flow.VisibleScreen=="Journal"&&flow.Journal.Day==0,"Results continue returns to Day 01 journal");Snapshot("11-returned-journal");yield return Frames(60);
            Finish();
        }
        IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        IEnumerator WaitLoad(){int count=0;do{yield return null;if(++count>18000)throw new Exception("Scene load timeout");}while(ZooPCFlow.Instance.IsLoading);}
        void Require(bool value,string message){if(!value)throw new Exception("VALIDATION FAILED: "+message);checks.Add("PASS: "+message);Debug.Log("[Zoo Menu Validation] PASS: "+message);}
        void Click(string name)
        {
            var button=FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.name==name&&b.isActiveAndEnabled);
            Require(button&&button.interactable,"Button available: "+name);
            Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;var center=rect.TransformPoint(rect.rect.center);
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,center)};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=button)Debug.Log("[Mouse ray] "+name+" at "+pointer.position+" hits "+string.Join(",",hits.Select(h=>h.gameObject.name+"/"+h.gameObject.transform.parent.name)));
            Require(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Mouse ray reaches: "+name);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        void Snapshot(string name){Capture();File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());}
        void LateUpdate(){if(record){Capture();File.WriteAllBytes(Output+"/frames/"+(videoFrame++).ToString("D5")+".jpg",pixels.EncodeToJPG(88));}}
        void Capture()
        {
            var flow=ZooPCFlow.Instance;if(!flow)return;
            if(flow.Journal.BookCamera&&flow.Journal.BookCamera.gameObject.activeInHierarchy)flow.Journal.BookCamera.Render();
            var camera=flow.InGameplay&&MonkeyCareSession.PlayerCamera?MonkeyCareSession.PlayerCamera:uiCamera;
            var oldTarget=camera.targetTexture;camera.targetTexture=target;
            var states=new List<(Canvas c,RenderMode mode,Camera camera,float distance,float scale)>();
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if(!canvas.isRootCanvas||canvas.renderMode!=RenderMode.ScreenSpaceOverlay)continue;
                states.Add((canvas,canvas.renderMode,canvas.worldCamera,canvas.planeDistance,canvas.scaleFactor));
                Layer(canvas.gameObject,29);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.2f;
                var scaler=canvas.GetComponent<CanvasScaler>();
                if(scaler&&scaler.uiScaleMode==CanvasScaler.ScaleMode.ScaleWithScreenSize)canvas.scaleFactor=Mathf.Min(target.width/scaler.referenceResolution.x,target.height/scaler.referenceResolution.y);
            }
            foreach(var label in FindObjectsByType<Text>(FindObjectsSortMode.None))label.SetAllDirty();
            Canvas.ForceUpdateCanvases();camera.Render();
            var old=RenderTexture.active;RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply(false);RenderTexture.active=old;
            foreach(var s in states){s.c.renderMode=s.mode;s.c.worldCamera=s.camera;s.c.planeDistance=s.distance;s.c.scaleFactor=s.scale;}
            camera.targetTexture=oldTarget;
        }
        static void Layer(GameObject go,int layer){go.layer=layer;foreach(Transform child in go.transform)Layer(child.gameObject,layer);}
        void Finish()
        {
            record=false;PlayerPrefs.SetFloat("ZooPC.Volume",oldVolume);PlayerPrefs.SetInt("ZooPC.ReducedMotion",oldReduced);PlayerPrefs.Save();Time.captureFramerate=0;
            foreach(var item in oldProgress){if(item.Value.HasValue)PlayerPrefs.SetInt(item.Key,item.Value.Value);else PlayerPrefs.DeleteKey(item.Key);}PlayerPrefs.Save();
            File.WriteAllLines(Output+"/validation.txt",checks);File.WriteAllText(Output+"/frame-count.txt",videoFrame.ToString());
            Debug.Log("[Zoo Menu Validation] COMPLETE "+checks.Count+" checks, "+videoFrame+" video frames.");
            if(SessionState.GetBool("ZooMenu.BuildAfter",false))EditorApplication.isPlaying=false;else EditorApplication.Exit(0);
        }
        void Update(){if(deadline>0&&EditorApplication.timeSinceStartup>deadline){File.WriteAllLines(Output+"/failed-checks.txt",checks);Debug.LogError("PC menu validation timed out");EditorApplication.Exit(1);}}
    }
}

#endif
