using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ADayAtTheZoo.PC
{
    [DefaultExecutionOrder(-500)]
    public sealed class ZooPCFlow : MonoBehaviour
    {
        public static ZooPCFlow Instance {get;private set;}
        public static bool ReducedMotion => PlayerPrefs.GetInt("ZooPC.ReducedMotion",0)!=0;
        public static int BestStars => PlayerPrefs.GetInt("ZooPC.Day1.Stars",0);
        public static bool DayCompleted => PlayerPrefs.GetInt("ZooPC.Day1.Completed",0)!=0;
        public ZooMenuAssets assets;
        public bool IsLoading {get;private set;}
        public bool Paused {get;private set;}
        public bool InGameplay => SceneManager.GetActiveScene().path==assets.tutorialScene;
        public string VisibleScreen {get;private set;}
        public ZooJournal Journal {get;private set;}
        public RectTransform UiRoot {get;private set;}
        RectTransform home,journal,modal,loading;
        GameObject eventSystem;
        Text loadingText, dayLabel;
        Button next,previous,enter;
        AudioSource audioSource;
        AudioClip clickSound;
        string pendingMenu="Home";
        float previousTimeScale=1;
        Action closeModal;
        void Awake()
        {
            if(Instance&&Instance!=this){Destroy(gameObject);return;}
            Instance=this; DontDestroyOnLoad(gameObject);
            if(!assets)assets=Resources.Load<ZooMenuAssets>("ZooPCMenu/MenuAssets");
            ZooMenuUI.Heading=assets.headingFont;ZooMenuUI.Body=assets.bodyFont;
            AudioListener.volume=PlayerPrefs.GetFloat("ZooPC.Volume",.8f);
            if(PlayerPrefs.HasKey("ZooPC.Fullscreen"))Screen.fullScreen=PlayerPrefs.GetInt("ZooPC.Fullscreen")==1;
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.ignoreListenerPause=true;
            clickSound=AudioClip.Create("Soft wooden menu tap",2205,1,44100,false);
            var samples=new float[2205];for(int i=0;i<samples.Length;i++)samples[i]=.13f*Mathf.Sin(i*.108f)*Mathf.Exp(-i/350f);clickSound.SetData(samples,0);
            var canvas=ZooMenuUI.Canvas("Zoo PC interface",300);canvas.transform.SetParent(transform,false);
            UiRoot=ZooMenuUI.Rect("Safe menu composition",canvas.transform,0,0,1920,1080);
            UiRoot.anchorMin=UiRoot.anchorMax=UiRoot.pivot=Vector2.one*.5f;UiRoot.anchoredPosition=Vector2.zero;
            eventSystem=new GameObject("Zoo PC menu input",typeof(EventSystem),typeof(InputSystemUIInputModule));eventSystem.transform.SetParent(transform,false);
            BuildHome();BuildJournal();BuildLoading();
            SceneManager.sceneLoaded+=SceneLoaded;
        }
        void Start(){if(Instance==this){if(InGameplay)EnterGameplay();else ShowHome();}}
        void OnDestroy(){if(Instance==this){SceneManager.sceneLoaded-=SceneLoaded;Instance=null;if(clickSound)Destroy(clickSound);}}
        void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.path==assets.menuScene)
            {
                if(pendingMenu=="Journal")ShowJournal();else ShowHome();
            }
            else if(scene.path==assets.tutorialScene)
            {
                EnterGameplay();
            }
        }
        public void EnterGameplay()
        {
            home.gameObject.SetActive(false);journal.gameObject.SetActive(false);Journal.SetVisible(false);eventSystem.SetActive(true);
            foreach(var other in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))if(other.gameObject!=eventSystem)other.gameObject.SetActive(false);
            VisibleScreen="Gameplay";Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
        }
        void BuildHome()
        {
            home=ZooMenuUI.Group("Main Menu",UiRoot);
            Background(home,1);
            ZooMenuUI.Panel("Edition patch",home,100,98,520,47,ZooMenuUI.Cream,14);
            ZooMenuUI.Label("Edition",home,"A LITTLE CARE. A BIG ADVENTURE.",117,102,486,38,22,ZooMenuUI.Ink);
            var title=ZooMenuUI.Label("Game title",home,"A DAY AT\nTHE ZOO",92,154,580,245,94,ZooMenuUI.Cream,TextAnchor.MiddleLeft,true);
            var outline=title.gameObject.AddComponent<Outline>();outline.effectColor=ZooMenuUI.Ink;outline.effectDistance=new Vector2(3,-3);
            var shadow=title.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.1f,.18f,.08f,.7f);shadow.effectDistance=new Vector2(5,-6);
            title.lineSpacing=.88f;
            ZooMenuUI.Panel("Title underline",home,100,414,110,7,ZooMenuUI.Orange,3);
            var invitation=ZooMenuUI.Label("Invitation",home,"Your first day as a zookeeper awaits.",102,443,540,64,27,ZooMenuUI.Cream);
            invitation.gameObject.AddComponent<Shadow>().effectColor=ZooMenuUI.Brown;
            ZooMenuUI.Button("Start Game",home,"START GAME",104,548,500,94,ShowJournal,ZooMenuUI.Leaf,36);
            ZooMenuUI.Button("Continue",home,"CONTINUE",104,666,500,94,ShowJournal,null,36);
            ZooMenuUI.Button("Settings",home,"SETTINGS",104,789,238,76,()=>ShowSettings("Home"),null,26);
            ZooMenuUI.Button("Quit",home,"QUIT",366,789,238,76,ShowQuit,ZooMenuUI.Orange,26);
            ZooMenuUI.Label("Menu note",home,"ONE LITTLE ZOO  /  ONE NEW ADVENTURE",102,972,690,40,22,ZooMenuUI.Cream);
            ZooMenuUI.Label("PC edition",home,"PC EDITION  •  DAY 01",1510,985,330,40,22,ZooMenuUI.Cream,TextAnchor.MiddleRight);
        }
        void Background(Transform parent,float brightness)
        {
            var pic=ZooMenuUI.Picture("Zoo morning",parent,assets.cover,0,0,1920,1080,Color.white*brightness);
            ZooMenuUI.Stretch(pic.rectTransform);
            var fitter=pic.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fitter.aspectRatio=(float)assets.cover.width/assets.cover.height;
        }
        void BuildJournal()
        {
            journal=ZooMenuUI.Group("Keeper journal",UiRoot);
            Background(journal,.36f);
            var backdrop=ZooMenuUI.Panel("Desk shadow",journal,0,0,1920,1080,new Color(.15f,.09f,.04f,.58f),0);ZooMenuUI.Stretch(backdrop.rectTransform);
            ZooMenuUI.Picture("Keeper title",journal,assets.journalTitle,745,0,520,149);
            ZooMenuUI.Button("Back to menu",journal,"<  MENU",50,42,238,70,ShowHome,null,25);
            ZooMenuUI.Button("Journal settings",journal,"SETTINGS",1635,42,235,70,()=>ShowSettings("Journal"),null,25);
            Journal=new GameObject("Animated keeper book").AddComponent<ZooJournal>();Journal.transform.SetParent(transform,false);
            Journal.Initialize(assets,journal);
            ZooMenuUI.Button("Logs tab",journal,"LOGS",64,328,226,98,()=>SelectSection(0),ZooMenuUI.Orange,29);
            ZooMenuUI.Button("Animals tab",journal,"ANIMALS",64,450,226,98,()=>SelectSection(1),ZooMenuUI.Hex("B38A3C"),29);
            ZooMenuUI.Button("Facilities tab",journal,"FACILITIES",64,572,226,98,()=>SelectSection(2),ZooMenuUI.Leaf,25);
            ZooMenuUI.Label("Tabs caption",journal,"KEEPER'S\nFIELD GUIDE",73,709,210,100,23,ZooMenuUI.Cream,TextAnchor.MiddleCenter);
            previous=ZooMenuUI.Button("Previous day",journal,"<  PREVIOUS",380,960,265,74,()=>TurnDay(-1),null,26);
            enter=ZooMenuUI.Button("Play Day 1",journal,"BEGIN DAY 01  >",816,945,430,92,EnterDay,ZooMenuUI.Leaf,32);
            next=ZooMenuUI.Button("Next day",journal,"NEXT  >",1454,960,250,74,()=>TurnDay(1),null,26);
            dayLabel=ZooMenuUI.Label("Current entry",journal,"",790,1043,480,32,19,ZooMenuUI.Cream,TextAnchor.MiddleCenter);
            Journal.Changed=RefreshJournal;
            journal.gameObject.SetActive(false);
        }
        public void ShowHome()
        {
            CloseModal();Paused=false;Time.timeScale=1;AudioListener.pause=false;
            home.gameObject.SetActive(true);journal.gameObject.SetActive(false);Journal.SetVisible(false);eventSystem.SetActive(true);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;VisibleScreen="Home";
            Select(home.Find("Start Game").gameObject);
        }
        public void ShowJournal()
        {
            CloseModal();home.gameObject.SetActive(false);journal.gameObject.SetActive(true);eventSystem.SetActive(true);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;VisibleScreen="Journal";
            Journal.SetVisible(true);Journal.OpenFirstDay();RefreshJournal();Select(enter.gameObject);
        }
        public void SelectSection(int section){if(!IsLoading)Journal.SelectSection(section);}
        public void TurnDay(int delta){if(!IsLoading)Journal.TurnDay(delta);}
        void RefreshJournal()
        {
            bool logs=Journal.Section==0;
            previous.interactable=logs&&Journal.Day==1&&!Journal.Busy;
            next.interactable=logs&&Journal.Day==0&&!Journal.Busy;
            enter.gameObject.SetActive(logs);enter.interactable=Journal.Day==0&&!Journal.Busy;
            enter.GetComponentInChildren<Text>().text=Journal.Day==0?"BEGIN DAY 01  >":"DAY 02  •  COMING SOON";
            dayLabel.text=logs?(Journal.Day==0?"DAY 01 / 02  •  MONKEY MEADOW":"DAY 02 / 02  •  NOT YET AVAILABLE"):(Journal.Section==1?"ANIMALS  /  MEET YOUR TROOP":"FACILITIES  /  AROUND THE MEADOW");
        }
        public void EnterDay()
        {
            // Guard the route as well as the visual button. An unfinished day never loads a scene.
            if(Journal.Section!=0||Journal.Day!=0||Journal.Busy||IsLoading)return;
            Load(assets.tutorialScene,"Preparing Monkey Meadow...");
        }
        public void ReturnToJournal(){pendingMenu="Journal";Load(assets.menuScene,"Opening your work log...");}
        public void ReturnToMenu(){pendingMenu="Home";Load(assets.menuScene,"Returning to the zoo gate...");}
        public void RetryDay(){Load(assets.tutorialScene,"A fresh start for Day 01...");}
        public void Load(string scene,string caption)
        {
            if(IsLoading)return;StartCoroutine(LoadRoutine(scene,caption));
        }
        IEnumerator LoadRoutine(string scene,string caption)
        {
            IsLoading=true;CloseModal();Paused=false;Time.timeScale=1;AudioListener.pause=false;
            eventSystem.SetActive(true);loading.gameObject.SetActive(true);loading.SetAsLastSibling();loadingText.text=caption;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            yield return null;
            AsyncOperation op;
#if UNITY_EDITOR
            op=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scene,new LoadSceneParameters(LoadSceneMode.Single));
#else
            op=SceneManager.LoadSceneAsync(Path.GetFileNameWithoutExtension(scene));
#endif
            while(!op.isDone){loadingText.text=caption+"\n"+Mathf.RoundToInt(Mathf.Clamp01(op.progress/.9f)*100)+"%";yield return null;}
            yield return null;loading.gameObject.SetActive(false);IsLoading=false;
        }
        void BuildLoading()
        {
            loading=ZooMenuUI.Group("Loading",UiRoot);
            var bg=ZooMenuUI.Panel("Loading shade",loading,0,0,1920,1080,ZooMenuUI.Ink,0);ZooMenuUI.Stretch(bg.rectTransform);bg.raycastTarget=true;
            ZooMenuUI.Label("Loading title",loading,"A DAY AT THE ZOO",420,370,1080,100,64,ZooMenuUI.Cream,TextAnchor.MiddleCenter,true);
            loadingText=ZooMenuUI.Label("Loading progress",loading,"",420,505,1080,150,30,ZooMenuUI.Cream,TextAnchor.MiddleCenter);
            loading.gameObject.SetActive(false);
        }
        RectTransform Modal(string title,string description)
        {
            CloseModal();modal=ZooMenuUI.Group("Dialog",UiRoot);modal.SetAsLastSibling();
            var bg=ZooMenuUI.Panel("Dim",modal,0,0,1920,1080,new Color(.08f,.12f,.06f,.8f),0);ZooMenuUI.Stretch(bg.rectTransform);bg.raycastTarget=true;
            ZooMenuUI.Panel("Paper frame",modal,510,190,900,720,ZooMenuUI.Brown,35);
            ZooMenuUI.Panel("Paper",modal,520,200,880,695,ZooMenuUI.Cream,30);
            ZooMenuUI.Label("Dialog title",modal,title,590,243,740,85,49,ZooMenuUI.Ink,TextAnchor.MiddleCenter,true);
            ZooMenuUI.Label("Dialog description",modal,description,595,337,730,90,25,ZooMenuUI.Brown,TextAnchor.MiddleCenter);
            return modal;
        }
        public void ShowSettings(string from)
        {
            var p=Modal("MAKE YOURSELF AT HOME","A few small comforts for your day at the zoo.");
            VisibleScreen="Settings";
            ZooMenuUI.Label("Master volume label",p,"MASTER VOLUME",600,456,480,42,25);
            var volumeText=ZooMenuUI.Label("Volume value",p,Mathf.RoundToInt(AudioListener.volume*100)+"%",1130,456,190,42,25,null,TextAnchor.MiddleRight);
            var sliderRoot=ZooMenuUI.Rect("Master volume",p,600,515,720,38);var slider=sliderRoot.gameObject.AddComponent<Slider>();
            var track=ZooMenuUI.Panel("Track",sliderRoot,0,12,720,12,ZooMenuUI.Hex("D3C19B"),6);track.raycastTarget=true;
            var fillArea=ZooMenuUI.Rect("Fill area",sliderRoot,0,10,720,16);var fill=ZooMenuUI.Panel("Fill",fillArea,0,0,720,16,ZooMenuUI.Leaf,7);ZooMenuUI.Stretch(fill.rectTransform);slider.fillRect=fill.rectTransform;
            var handleArea=ZooMenuUI.Rect("Handle area",sliderRoot,17,0,686,38);var handle=ZooMenuUI.Panel("Handle",handleArea,0,0,34,38,ZooMenuUI.Orange,17);
            handle.rectTransform.pivot=Vector2.one*.5f;handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=Vector2.one*.5f;handle.rectTransform.anchoredPosition=Vector2.zero;
            handle.raycastTarget=true;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
            slider.minValue=0;slider.maxValue=1;slider.value=AudioListener.volume;
            slider.onValueChanged.AddListener(v=>{AudioListener.volume=v;volumeText.text=Mathf.RoundToInt(v*100)+"%";PlayerPrefs.SetFloat("ZooPC.Volume",v);});
            Button full=null,motion=null;
            full=ZooMenuUI.Button("Fullscreen",p,"FULLSCREEN: "+(Screen.fullScreen?"ON":"OFF"),600,585,720,65,()=>{bool on=!Screen.fullScreen;Screen.fullScreen=on;PlayerPrefs.SetInt("ZooPC.Fullscreen",on?1:0);full.GetComponentInChildren<Text>().text="FULLSCREEN: "+(on?"ON":"OFF");},ZooMenuUI.Hex("7B8858"),25);
            motion=ZooMenuUI.Button("Reduced motion",p,"REDUCED MOTION: "+(ReducedMotion?"ON":"OFF"),600,677,720,65,()=>{PlayerPrefs.SetInt("ZooPC.ReducedMotion",ReducedMotion?0:1);motion.GetComponentInChildren<Text>().text="REDUCED MOTION: "+(ReducedMotion?"ON":"OFF");},ZooMenuUI.Hex("7B8858"),25);
            closeModal=()=>{PlayerPrefs.Save();if(from=="Pause")ShowPause();else{CloseModal();VisibleScreen=from;Select(from=="Home"?home.Find("Settings").gameObject:enter.gameObject);}};
            ZooMenuUI.Button("Done",p,"DONE",805,786,310,72,()=>closeModal?.Invoke(),ZooMenuUI.Leaf,29);Select(slider.gameObject);
        }
        public void ShowPause()
        {
            if(!InGameplay||IsLoading)return;
            if(!Paused){previousTimeScale=Time.timeScale;Paused=true;Time.timeScale=0;AudioListener.pause=true;}
            var p=Modal("TAKE A BREATHER","Your shift is paused. The monkeys can wait a moment.");
            VisibleScreen="Pause";eventSystem.SetActive(true);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            var resume=ZooMenuUI.Button("Resume",p,"BACK TO WORK",635,460,650,70,Resume,ZooMenuUI.Leaf,29);
            ZooMenuUI.Button("Retry",p,"RETRY DAY 01",635,553,650,65,()=>ConfirmLeave(true),null,26);
            ZooMenuUI.Button("Pause settings",p,"SETTINGS",635,640,650,65,()=>ShowSettings("Pause"),null,26);
            ZooMenuUI.Button("Pause journal",p,"RETURN TO WORK LOG",635,727,650,65,()=>ConfirmLeave(false),null,26);
            closeModal=Resume;Select(resume.gameObject);
        }
        void ConfirmLeave(bool retry)
        {
            var p=Modal(retry?"A FRESH START?":"CLOSE THIS SHIFT?","This round's unfinished progress will not be saved.");
            ZooMenuUI.Button("Confirm leave",p,retry?"RETRY DAY 01":"RETURN TO WORK LOG",620,520,680,85,retry?(Action)RetryDay:ReturnToJournal,ZooMenuUI.Orange,29);
            var cancel=ZooMenuUI.Button("Keep playing",p,"GO BACK",750,660,420,80,ShowPause,null,29);closeModal=ShowPause;Select(cancel.gameObject);
        }
        public void Resume(){CloseModal();Paused=false;Time.timeScale=previousTimeScale;AudioListener.pause=false;eventSystem.SetActive(true);VisibleScreen="Gameplay";}
        void ShowQuit()
        {
            var p=Modal("LEAVING SO SOON?","Your completed day records and settings are saved automatically.");
            ZooMenuUI.Button("Confirm quit",p,"QUIT GAME",620,520,680,85,()=>{
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
#else
                Application.Quit();
#endif
            },ZooMenuUI.Orange,30);
            var back=ZooMenuUI.Button("Stay",p,"STAY A LITTLE LONGER",670,660,580,80,ShowHome,null,27);closeModal=ShowHome;VisibleScreen="Quit";Select(back.gameObject);
        }
        void CloseModal(){closeModal=null;if(modal){modal.gameObject.SetActive(false);Destroy(modal.gameObject);modal=null;}}
        public void Click(){if(audioSource&&clickSound)audioSource.PlayOneShot(clickSound);}
        public void PaperSound(){if(assets.pageTurn)audioSource.PlayOneShot(assets.pageTurn,.45f);else Click();}
        public static void SaveDay(int stars,int feedings)
        {
            PlayerPrefs.SetInt("ZooPC.Day1.Completed",1);PlayerPrefs.SetInt("ZooPC.Day1.Stars",Mathf.Max(BestStars,stars));
            PlayerPrefs.SetInt("ZooPC.Day1.Feedings",Mathf.Max(PlayerPrefs.GetInt("ZooPC.Day1.Feedings",0),feedings));PlayerPrefs.Save();
        }
        void Select(GameObject target){StartCoroutine(SelectLater(target));}
        IEnumerator SelectLater(GameObject target){yield return null;if(EventSystem.current&&target&&target.activeInHierarchy)EventSystem.current.SetSelectedGameObject(target);}
        void Update()
        {
            if(IsLoading)return;
            var k=Keyboard.current;var pad=Gamepad.current;
            bool back=(k!=null&&k.escapeKey.wasPressedThisFrame)||(pad!=null&&pad.buttonEast.wasPressedThisFrame&&(!InGameplay||Paused));
            if(InGameplay&&Paused&&pad!=null&&pad.startButton.wasPressedThisFrame){Resume();return;}
            if(InGameplay&&!Paused&&((k!=null&&k.escapeKey.wasPressedThisFrame)||(pad!=null&&pad.startButton.wasPressedThisFrame))){ShowPause();return;}
            if(back){if(modal){closeModal?.Invoke();}else if(VisibleScreen=="Journal")ShowHome();}
            if(VisibleScreen=="Journal"&&!modal)
            {
                if((k!=null&&k.rightArrowKey.wasPressedThisFrame)||(pad!=null&&pad.rightShoulder.wasPressedThisFrame))TurnDay(1);
                if((k!=null&&k.leftArrowKey.wasPressedThisFrame)||(pad!=null&&pad.leftShoulder.wasPressedThisFrame))TurnDay(-1);
            }
        }
    }
}
