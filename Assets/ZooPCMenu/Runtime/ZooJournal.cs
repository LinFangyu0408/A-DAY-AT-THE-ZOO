using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using echo17.EndlessBook;

namespace ADayAtTheZoo.PC
{
    public sealed class ZooJournal : MonoBehaviour
    {
        public int Day {get;private set;}
        public int Section {get;private set;}
        public bool Busy => book && book.IsTurningPages;
        public Action Changed;
        public EndlessBook Book => book;
        public Camera BookCamera => bookCamera;
        ZooMenuAssets assets;
        EndlessBook book;
        Camera bookCamera;
        RenderTexture bookImage;
        GameObject stage;
        readonly List<RenderTexture> paperImages=new();
        readonly List<Material> materials=new();
        bool initialized;
        public void Initialize(ZooMenuAssets config,Transform ui)
        {
            assets=config;
            stage=new GameObject("Book photography stage");stage.transform.SetParent(transform,false);stage.transform.position=new Vector3(10000,0,0);
            book=Instantiate(assets.bookPrefab,stage.transform).GetComponent<EndlessBook>();book.name="Keeper's work log";book.transform.localPosition=Vector3.zero;book.transform.localScale=Vector3.one*10;
            Layer(book.gameObject,30);
            book.SetState(EndlessBook.StateEnum.OpenMiddle,0);
            for(int i=0;i<8;i++)
            {
                var rt=DrawPage(i);paperImages.Add(rt);
                var mat=new Material(Resources.Load<Shader>("ZooPCMenu/JournalPaper")){name="Keeper page "+(i+1),mainTexture=rt};
                materials.Add(mat);
                if(i<book.LastPageNumber)book.SetPageData(i+1,new PageData{material=mat});else book.AddPageData(mat);
            }
            var cover=new Material(Shader.Find("Standard")){name="Forest cloth journal",color=ZooMenuUI.Hex("425438")};
            cover.SetFloat("_Glossiness",.12f);materials.Add(cover);book.SetMaterial(EndlessBook.MaterialEnum.BookCover,cover);
            var edges=new Material(Shader.Find("Standard")){name="Warm paper edges",color=ZooMenuUI.Cream};edges.SetFloat("_Glossiness",0);materials.Add(edges);
            book.SetMaterial(EndlessBook.MaterialEnum.BookPageBack,edges);book.SetMaterial(EndlessBook.MaterialEnum.BookPageFront,edges);
            book.SetPageNumber(2);
            var cameraGo=new GameObject("Journal camera",typeof(Camera));cameraGo.transform.SetParent(stage.transform,false);bookCamera=cameraGo.GetComponent<Camera>();
            bookCamera.orthographic=true;bookCamera.nearClipPlane=.01f;bookCamera.farClipPlane=40;bookCamera.cullingMask=1<<30;
            bookCamera.clearFlags=CameraClearFlags.SolidColor;bookCamera.backgroundColor=Color.clear;bookCamera.allowHDR=false;bookCamera.allowMSAA=true;
            var bounds=new Bounds(book.transform.position,Vector3.zero);bool first=true;
            foreach(var renderer in book.GetComponentsInChildren<Renderer>()) {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
            Debug.Log("[Zoo PC Journal] Open book bounds: "+bounds);
            bookCamera.transform.position=bounds.center+new Vector3(0,8,-1.1f);bookCamera.transform.LookAt(bounds.center,Vector3.forward);
            bookCamera.orthographicSize=Mathf.Max(bounds.extents.z,bounds.extents.x/1.8f)*1.05f;
            bookImage=new RenderTexture(1800,1000,24,RenderTextureFormat.ARGB32){name="Live animated journal",antiAliasing=4};bookImage.Create();bookCamera.targetTexture=bookImage;
            var picture=ZooMenuUI.Picture("3D book",ui,bookImage,307,155,1460,790);picture.uvRect=new Rect(0,0,1,1);
            var lightGo=new GameObject("Warm window light",typeof(Light));lightGo.transform.SetParent(stage.transform,false);lightGo.transform.rotation=Quaternion.Euler(65,-25,0);
            var light=lightGo.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.color=new Color(1,.94f,.82f);light.cullingMask=1<<30;light.shadows=LightShadows.Soft;
            initialized=true;SetVisible(false);
        }
        public void SetVisible(bool value){if(stage)stage.SetActive(value);}
        public void OpenFirstDay()
        {
            Day=0;Section=0;
            if(book.IsTurningPages)book.StopTurningPages();
            book.SetState(EndlessBook.StateEnum.OpenMiddle,0);book.SetPageNumber(2);
            // Refresh the day record after returning from an actual completed round.
            ReplacePage(1);Changed?.Invoke();
        }
        void ReplacePage(int index)
        {
            if(!initialized)return;
            var old=paperImages[index];var rt=DrawPage(index);paperImages[index]=rt;materials[index].mainTexture=rt;
            old.Release();Destroy(old);
        }
        public void TurnDay(int direction)
        {
            if(Section!=0||Busy)return;int next=Mathf.Clamp(Day+direction,0,1);if(next==Day)return;
            Day=next;TurnTo(Day==0?2:4);
        }
        public void SelectSection(int section)
        {
            if(Busy)return;section=Mathf.Clamp(section,0,2);if(Section==section)return;Section=section;
            TurnTo(section==0?(Day==0?2:4):section==1?6:8);
        }
        void TurnTo(int page)
        {
            ZooPCFlow.Instance.PaperSound();
            if(ZooPCFlow.ReducedMotion){book.SetPageNumber(page);Changed?.Invoke();return;}
            book.TurnToPage(page,EndlessBook.PageTurnTimeTypeEnum.TotalTurnTime,1.15f,onCompleted:(a,b,p)=>Changed?.Invoke());Changed?.Invoke();
        }
        RenderTexture DrawPage(int index)
        {
            const int width=1024,height=1280;
            var root=new GameObject("Printed keeper page "+index,typeof(RectTransform),typeof(Canvas));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            var rect=root.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.sizeDelta=new Vector2(width,height);rect.position=Vector3.zero;rect.localScale=Vector3.one*.001f;
            ZooMenuUI.Picture("Leaf paper",root.transform,assets.paper,0,0,width,height);
            if(index==0)WelcomePage(rect);
            if(index==1)DayOne(rect);
            if(index==2)FieldNotes(rect);
            if(index==3)DayTwo(rect);
            if(index==4)AnimalPortrait(rect);
            if(index==5)AnimalGuide(rect);
            if(index==6)FacilityPortrait(rect);
            if(index==7)FacilityGuide(rect);
            ZooMenuUI.Label("Page number",rect,(index+1).ToString("00"),420,1187,184,46,28,ZooMenuUI.Brown,TextAnchor.MiddleCenter);
            Layer(root,31);
            var cameraGo=new GameObject("Print camera",typeof(Camera));var camera=cameraGo.GetComponent<Camera>();camera.transform.position=new Vector3(0,0,-5);camera.orthographic=true;camera.orthographicSize=.64f;camera.aspect=.8f;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ZooMenuUI.Cream;camera.cullingMask=1<<31;camera.allowHDR=false;
            var texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="Printed page "+(index+1),antiAliasing=2};texture.Create();camera.targetTexture=texture;
            Canvas.ForceUpdateCanvases();camera.Render();camera.targetTexture=null;root.SetActive(false);cameraGo.SetActive(false);Destroy(root);Destroy(cameraGo);
            return texture;
        }
        void Eyebrow(Transform p,string text){ZooMenuUI.Label("Section",p,text,96,105,830,48,28,ZooMenuUI.Orange,TextAnchor.MiddleLeft,true);}
        void Heading(Transform p,string text){ZooMenuUI.Label("Heading",p,text,92,170,840,172,64,ZooMenuUI.Ink,TextAnchor.UpperLeft,true);}
        void Paragraph(Transform p,string text,float y,int size=30){ZooMenuUI.Label("Note",p,text,102,y,812,220,size,ZooMenuUI.Brown,TextAnchor.UpperLeft);}
        void Rule(Transform p,float y){ZooMenuUI.Panel("Ink rule",p,100,y,812,3,ZooMenuUI.Hex("C0AC7D"),0);}
        void Stamp(Transform p,string text,float y,Color color){ZooMenuUI.Panel("Stamp",p,105,y,810,68,color,12);ZooMenuUI.Label("Stamp text",p,text,120,y,780,68,28,ZooMenuUI.Cream,TextAnchor.MiddleCenter,true);}
        void Photo(Transform p,float y,string caption)
        {
            ZooMenuUI.Panel("Photo shadow",p,118,y+9,796,425,new Color(.3f,.2f,.1f,.2f),4);
            ZooMenuUI.Panel("Photo frame",p,108,y,796,425,Color.white,4);
            var photo=ZooMenuUI.Picture("Meadow photograph",p,assets.levelPhoto?assets.levelPhoto:assets.cover,128,y+20,756,330);
            if(!assets.levelPhoto)photo.uvRect=new Rect(.38f,.04f,.62f,.8f);
            ZooMenuUI.Label("Photo caption",p,caption,130,y+350,750,60,26,ZooMenuUI.Brown,TextAnchor.MiddleCenter,true);
            ZooMenuUI.Panel("Tape",p,405,y-12,210,35,new Color(.72f,.69f,.39f,.78f),2);
        }
        void WelcomePage(Transform p)
        {
            Eyebrow(p,"KEEPER'S WORK LOG");Heading(p,"Small acts.\nHappy animals.");
            Photo(p,365,"A morning at Monkey Meadow");
            Paragraph(p,"A new day, a fresh page.\nGet to know your troop and make sure\nevery hungry monkey gets a little care.",845,31);
            Rule(p,1080);ZooMenuUI.Label("Signature",p,"YOUR ADVENTURE STARTS HERE",104,1100,810,52,27,ZooMenuUI.Leaf,TextAnchor.MiddleCenter,true);
        }
        void DayOne(Transform p)
        {
            Eyebrow(p,"DAY 01  /  TUTORIAL");Heading(p,"Welcome to\nMonkey Meadow");
            Stamp(p,ZooPCFlow.DayCompleted?"SHIFT RECORDED  •  READY TO REPLAY":"YOUR FIRST SHIFT",375,ZooMenuUI.Leaf);
            ZooMenuUI.Label("Round facts",p,"3 MONKEYS     •     2 MINUTES",105,475,815,70,33,ZooMenuUI.Brown,TextAnchor.MiddleCenter,true);
            Rule(p,568);
            Entry(p,"01","Meet your troop", "Follow the opening lesson and learn\nhow to use your keeper's bag.",606);
            Entry(p,"02","A banana for everyone", "Watch for hunger, collect supplies\nand feed the monkeys.",780);
            Rule(p,976);
            string record=ZooPCFlow.DayCompleted?"BEST RECORD   "+ZooPCFlow.BestStars+" / 3 STARS":"STAR GOALS   3 / 6 / 9 FEEDINGS";
            ZooMenuUI.Label("Best record",p,record,102,1012,815,64,31,ZooMenuUI.Leaf,TextAnchor.MiddleCenter,true);
            ZooMenuUI.Label("Replay note",p,"You can revisit this day any time.",102,1095,815,50,26,ZooMenuUI.Brown,TextAnchor.MiddleCenter);
        }
        void Entry(Transform p,string number,string title,string detail,float y)
        {
            ZooMenuUI.Panel("Number badge",p,102,y,70,70,ZooMenuUI.Hex("E0BC7A"),35);
            ZooMenuUI.Label("Number",p,number,102,y,70,70,29,ZooMenuUI.Brown,TextAnchor.MiddleCenter,true);
            ZooMenuUI.Label("Entry title",p,title,198,y-3,700,65,36,ZooMenuUI.Ink,TextAnchor.MiddleLeft,true);
            ZooMenuUI.Label("Entry detail",p,detail,198,y+72,700,100,32,ZooMenuUI.Brown,TextAnchor.UpperLeft);
        }
        void FieldNotes(Transform p)
        {
            Eyebrow(p,"FIELD NOTES");Heading(p,"A little better,\nevery day.");
            ZooMenuUI.Picture("Keeper note card",p,assets.journalCard,90,381,840,306);
            ZooMenuUI.Label("Card message",p,"GOOD CARE\nTAKES PRACTICE",145,440,435,145,37,ZooMenuUI.Ink,TextAnchor.MiddleLeft,true);
            Paragraph(p,"Missed a hungry monkey?\nTurn back to Day 01 and try again.\nYour best star record stays in this book.",760,32);
            Rule(p,1030);ZooMenuUI.Label("Return hint",p,"<  EARLIER DAYS ARE ALWAYS HERE",102,1070,815,70,28,ZooMenuUI.Leaf,TextAnchor.MiddleCenter,true);
        }
        void DayTwo(Transform p)
        {
            Eyebrow(p,"DAY 02  /  NEXT CHAPTER");Heading(p,"Tomorrow's\nadventure");
            Stamp(p,"COMING SOON",399,ZooMenuUI.Hex("A29474"));
            ZooMenuUI.Label("Large day",p,"02",210,530,600,250,180,ZooMenuUI.Hex("BEB18F"),TextAnchor.MiddleCenter,true);
            Paragraph(p,"The next shift is still being prepared.\n\nFor now, return to Day 01 to meet\nthe troop and improve your record.",850,32);
            ZooMenuUI.Label("Locked note",p,"THIS DAY CANNOT BE STARTED YET",102,1110,815,48,27,ZooMenuUI.Brown,TextAnchor.MiddleCenter,true);
        }
        void AnimalPortrait(Transform p)
        {
            Eyebrow(p,"ANIMALS  /  FIELD GUIDE");Heading(p,"Meet your\nmonkey troop");Photo(p,370,"Curious faces. Very hungry tummies.");
            Paragraph(p,"Three monkeys share the meadow.\nLook for their hunger cues and give\neach of them your attention.",850,33);
        }
        void AnimalGuide(Transform p)
        {
            Eyebrow(p,"CARE NOTES");Heading(p,"Happy monkeys,\nhappy keeper.");
            Entry(p,"01","Read the signs","A hungry monkey needs a banana.\nKeep an eye on the care prompts.",395);
            Entry(p,"02","Take your time","Let a feeding finish before moving\non to the next hungry friend.",614);
            Entry(p,"03","Share the care","Walk around the meadow and\ncheck in with the whole troop.",833);
            Rule(p,1060);ZooMenuUI.Label("Guide scope",p,"YOUR DAY 01 FIELD GUIDE",100,1090,820,60,27,ZooMenuUI.Leaf,TextAnchor.MiddleCenter,true);
        }
        void FacilityPortrait(Transform p)
        {
            Eyebrow(p,"FACILITIES  /  FIELD GUIDE");Heading(p,"Everything\na keeper needs");Photo(p,370,"Find your way around the meadow");
            Paragraph(p,"The meadow is small enough to explore\nand full of places to discover.\nYour supplies are never far away.",850,32);
        }
        void FacilityGuide(Transform p)
        {
            Eyebrow(p,"AROUND THE MEADOW");Heading(p,"A place for\neverything.");
            Entry(p,"01","Banana supplies","Return to the supply point when\nyour keeper's bag runs low.",395);
            Entry(p,"02","Keeper's bag","Open your bag, choose a banana\nand get ready to feed the troop.",614);
            Entry(p,"03","Monkey habitat","Follow the paths around the island\nto reach each hungry monkey.",833);
            Rule(p,1060);ZooMenuUI.Label("Guide scope",p,"EXPLORE IT ALL IN DAY 01",100,1090,820,60,27,ZooMenuUI.Leaf,TextAnchor.MiddleCenter,true);
        }
        static void Layer(GameObject go,int layer){go.layer=layer;foreach(Transform child in go.transform)Layer(child.gameObject,layer);}
        void OnDestroy(){foreach(var rt in paperImages){rt.Release();Destroy(rt);}foreach(var mat in materials)Destroy(mat);if(bookImage){bookImage.Release();Destroy(bookImage);}}
    }
}
