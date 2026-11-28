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
        public bool HasDayTwo => !string.IsNullOrEmpty(assets.dayTwoScene);
        public int ActiveDay => HasDayTwo && SceneManager.GetActiveScene().path==assets.dayTwoScene ? 2 : 1;
        public bool InGameplay => SceneManager.GetActiveScene().path==assets.tutorialScene || (HasDayTwo && SceneManager.GetActiveScene().path==assets.dayTwoScene);
        public string VisibleScreen {get;private set;}
        public ZooJournal Journal {get;private set;}
        public RectTransform UiRoot {get;private set;}
        RectTransform home,journal,modal,loading;
        GameObject eventSystem;
        Text loadingText, dayLabel;
        Button next,previous,enter;
        RectTransform tabMarker;
        AudioSource audioSource;
        AudioClip clickSound;
        string pendingMenu="Home";
        int returnDay;
        float previousTimeScale=1;
        Action closeModal;
        void Awake()
        {
            if(Instance&&Instance!=this){Destroy(gameObject);return;}
            Instance=this; DontDestroyOnLoad(gameObject);
            if(!assets)assets=Resources.Load<ZooMenuAssets>("ZooPCMenu/MenuAssets");
            ZooMenuUI.Heading=assets.headingFont;ZooMenuUI.Body=assets.bodyFont;
            ZooMenuUI.Notes=assets.noteFont;ZooMenuUI.LeafTexture=assets.leafButton;ZooMenuUI.LinenTexture=assets.linenButton;
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
                if(pendingMenu=="Journal")OpenJournal(returnDay);else ShowHome();
            }
            else if(scene.path==assets.tutorialScene || (HasDayTwo && scene.path==assets.dayTwoScene))
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
            var background=Background(home,1);
            var logo=ZooMenuUI.Picture("Handcrafted game title",home,assets.titleLogo,43,43,635,401);
            ZooMenuUI.Handwriting("Invitation",home,"A little care. A big adventure.",87,430,550,64,43,ZooMenuUI.Cream);
            for(int side=0;side<2;side++)
            {
                float x=125+side*477;ZooMenuUI.Panel("Rope shadow",home,x+4,490,9,353,ZooMenuUI.Brown,4);ZooMenuUI.Panel("Hanging rope",home,x,488,9,353,ZooMenuUI.Hex("DEB77C"),4);
                for(int y=490;y<839;y+=11){var knot=ZooMenuUI.Panel("Rope twist",home,x-1,y,11,3,ZooMenuUI.Hex("AD8153"),1);knot.rectTransform.localRotation=Quaternion.Euler(0,0,-22);}
            }
            var start=ZooMenuUI.Button("Start Game",home,"Start Game",95,523,550,132,ShowJournal,ZooMenuUI.Leaf,44);
            ZooMenuUI.Glyph("Start paw",start.transform,ZooGlyphGraphic.Kind.Paw,47,48,32,32,ZooMenuUI.Cream);
            ZooMenuUI.Button("Continue",home,"Continue",98,687,550,126,ShowJournal,null,44);
            ZooMenuUI.Button("Settings",home,"Settings",100,867,252,89,()=>ShowSettings("Home"),null,31);
            ZooMenuUI.Button("Quit",home,"Quit Game",377,867,252,89,ShowQuit,ZooMenuUI.Orange,30);
            ZooMenuUI.Handwriting("Menu note",home,"Every keeper starts with one little day.",92,1000,650,50,33,ZooMenuUI.Cream,TextAnchor.MiddleLeft);
            ZooMenuUI.Panel("Edition patch",home,1570,980,280,52,new Color(.2f,.29f,.12f,.82f),20);ZooMenuUI.Stitches(home,1580,989,260,31,new Color(1,.92f,.67f,.55f));
            ZooMenuUI.Label("PC edition",home,HasDayTwo?"PC EDITION  /  DAYS 01–02":"PC EDITION  /  DAY 01",1590,986,240,40,20,ZooMenuUI.Cream,TextAnchor.MiddleCenter,true);
            home.gameObject.AddComponent<ZooMenuAtmosphere>().Initialize(background.rectTransform,logo.rectTransform);
            home.gameObject.AddComponent<ZooScreenReveal>();
        }
        RawImage Background(Transform parent,float brightness)
        {
            var pic=ZooMenuUI.Picture("Zoo morning",parent,assets.cover,0,0,1920,1080,Color.white*brightness);
            ZooMenuUI.Stretch(pic.rectTransform);
            var fitter=pic.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fitter.aspectRatio=(float)assets.cover.width/assets.cover.height;
            return pic;
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
            ZooMenuUI.Button("Logs tab",journal,"Logs",64,328,226,98,()=>SelectSection(0),ZooMenuUI.Orange,31);
            ZooMenuUI.Button("Animals tab",journal,"Animals",64,450,226,98,()=>SelectSection(1),ZooMenuUI.Hex("B38A3C"),31);
            ZooMenuUI.Button("Facilities tab",journal,"Facilities",64,572,226,98,()=>SelectSection(2),ZooMenuUI.Leaf,27);
            tabMarker=ZooMenuUI.Glyph("Current collection bookmark",journal,ZooGlyphGraphic.Kind.Paw,15,360,35,35,ZooMenuUI.Hex("E5BF64")).rectTransform;
            ZooMenuUI.Handwriting("Tabs caption",journal,"Little discoveries,\nbig memories.",48,713,250,108,35,ZooMenuUI.Cream);
            ZooMenuUI.Glyph("Journal paw seal",journal,ZooGlyphGraphic.Kind.Paw,140,851,58,58,new Color(.93f,.79f,.43f,.75f));
            previous=ZooMenuUI.Button("Previous day",journal,"<  PREVIOUS",380,960,265,74,()=>TurnDay(-1),null,26);
            enter=ZooMenuUI.Button("Play Day 1",journal,"BEGIN DAY 01  >",816,945,430,92,EnterDay,ZooMenuUI.Leaf,32);
            next=ZooMenuUI.Button("Next day",journal,"NEXT  >",1454,960,250,74,()=>TurnDay(1),null,26);
            dayLabel=ZooMenuUI.Label("Current entry",journal,"",790,1043,480,32,19,ZooMenuUI.Cream,TextAnchor.MiddleCenter);
            Journal.Changed=RefreshJournal;
            journal.gameObject.AddComponent<ZooScreenReveal>();
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
            OpenJournal(0);
        }
        void OpenJournal(int day)
        {
            CloseModal();home.gameObject.SetActive(false);journal.gameObject.SetActive(true);eventSystem.SetActive(true);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;VisibleScreen="Journal";
            Journal.SetVisible(true);Journal.OpenDay(day);RefreshJournal();Select(enter.gameObject);
        }
        public void SelectSection(int section){if(!IsLoading)Journal.SelectSection(section);}
        public void TurnDay(int delta){if(!IsLoading)Journal.TurnDay(delta);}
        void RefreshJournal()
        {
            bool logs=Journal.Section==0;
            if(tabMarker)tabMarker.anchoredPosition=new Vector2(15,-360-Journal.Section*122);
            previous.interactable=logs&&Journal.Day==1&&!Journal.Busy;
            next.interactable=logs&&Journal.Day==0&&!Journal.Busy;
            enter.gameObject.SetActive(logs);enter.interactable=(Journal.Day==0||HasDayTwo)&&!Journal.Busy;
            enter.GetComponentInChildren<Text>().text=Journal.Day==0?"BEGIN DAY 01  >":HasDayTwo?"BEGIN DAY 02  >":"DAY 02  •  COMING SOON";
            dayLabel.text=logs?(Journal.Day==0?"DAY 01 / 02  •  MONKEY MEADOW":HasDayTwo?"DAY 02 / 02  •  LAKESIDE CARE":"DAY 02 / 02  •  NOT YET AVAILABLE"):(Journal.Section==1?"ANIMALS  /  YOUR COLLECTION":"FACILITIES  /  YOUR COLLECTION");
        }
        public void EnterDay()
        {
            if(Journal.Section!=0||Journal.Busy||IsLoading||(Journal.Day==1&&!HasDayTwo))return;
            Load(Journal.Day==0?assets.tutorialScene:assets.dayTwoScene,Journal.Day==0?"Preparing Monkey Meadow...":"Preparing Lakeside Care...");
        }
        public void ReturnToJournal(){returnDay=ActiveDay-1;pendingMenu="Journal";Load(assets.menuScene,"Opening your work log...");}
        public void ReturnToMenu(){pendingMenu="Home";Load(assets.menuScene,"Returning to the zoo gate...");}
        public void RetryDay(){int day=ActiveDay;Load(day==2?assets.dayTwoScene:assets.tutorialScene,"A fresh start for Day "+day.ToString("00")+"...");}
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
            ZooMenuUI.Picture("Textured dialog paper",modal,assets.paper,535,215,850,665);
            ZooMenuUI.Label("Dialog title",modal,title,590,243,740,85,49,ZooMenuUI.Ink,TextAnchor.MiddleCenter,true);
            ZooMenuUI.Label("Dialog description",modal,description,595,337,730,90,25,ZooMenuUI.Brown,TextAnchor.MiddleCenter);
            modal.gameObject.AddComponent<ZooScreenReveal>();
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
            ZooMenuUI.Button("Retry",p,"RETRY DAY "+ActiveDay.ToString("00"),635,553,650,65,()=>ConfirmLeave(true),null,26);
            ZooMenuUI.Button("Pause settings",p,"SETTINGS",635,640,650,65,()=>ShowSettings("Pause"),null,26);
            ZooMenuUI.Button("Pause journal",p,"RETURN TO WORK LOG",635,727,650,65,()=>ConfirmLeave(false),null,26);
            closeModal=Resume;Select(resume.gameObject);
        }
        void ConfirmLeave(bool retry)
        {
            var p=Modal(retry?"A FRESH START?":"CLOSE THIS SHIFT?","This round's unfinished progress will not be saved.");
            ZooMenuUI.Button("Confirm leave",p,retry?"RETRY DAY "+ActiveDay.ToString("00"):"RETURN TO WORK LOG",620,520,680,85,retry?(Action)RetryDay:ReturnToJournal,ZooMenuUI.Orange,29);
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
            SaveDay(1,stars,feedings,0);
        }
        public static int BestStarsFor(int day)=>PlayerPrefs.GetInt("ZooPC.Day"+day+".Stars",0);
        public static bool DayIsCompleted(int day)=>PlayerPrefs.GetInt("ZooPC.Day"+day+".Completed",0)!=0;
        public static bool CollectionUnlocked(string id)=>!string.IsNullOrEmpty(id)&&PlayerPrefs.GetInt("ZooPC.Collection."+id,0)!=0;
        public static void UnlockCollection(string id){if(string.IsNullOrEmpty(id))return;PlayerPrefs.SetInt("ZooPC.Collection."+id,1);PlayerPrefs.Save();}
        public static void SaveDay(int day,int stars,int feedings,int scratches)
        {
            string key="ZooPC.Day"+Mathf.Clamp(day,1,2)+".";
            PlayerPrefs.SetInt(key+"Completed",1);PlayerPrefs.SetInt(key+"Stars",Mathf.Max(PlayerPrefs.GetInt(key+"Stars",0),Mathf.Clamp(stars,0,3)));
            PlayerPrefs.SetInt(key+"Feedings",Mathf.Max(PlayerPrefs.GetInt(key+"Feedings",0),feedings));
            PlayerPrefs.SetInt(key+"Scratches",Mathf.Max(PlayerPrefs.GetInt(key+"Scratches",0),scratches));PlayerPrefs.Save();
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
