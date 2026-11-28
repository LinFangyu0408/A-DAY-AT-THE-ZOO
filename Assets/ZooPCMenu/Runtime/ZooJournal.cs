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
            OpenDay(0);
        }
        public void OpenDay(int day)
        {
            Day=Mathf.Clamp(day,0,1);Section=0;
            if(book.IsTurningPages)book.StopTurningPages();
            book.SetState(EndlessBook.StateEnum.OpenMiddle,0);book.SetPageNumber(Day==0?2:4);
            // Refresh the day record after returning from an actual completed round.
            ReplacePage(1);ReplacePage(3);for(int i=4;i<8;i++)ReplacePage(i);Changed?.Invoke();
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
            if(index==4)CollectionPortrait(rect,true);
            if(index==5)CollectionGrid(rect,true);
            if(index==6)CollectionPortrait(rect,false);
            if(index==7)CollectionGrid(rect,false);
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
            if(!string.IsNullOrEmpty(assets.dayTwoScene))
            {
                Eyebrow(p,"KEEPER'S FIELD NOTES");Heading(p,"Two ways to\nmake their day.");
                var photo=ZooMenuUI.Picture("Day two photograph",p,assets.dayTwoPhoto?assets.dayTwoPhoto:assets.levelPhoto,105,390,815,459);
                ZooMenuUI.Handwriting("Day two photo note",p,"An afternoon beside the lake.",105,858,815,65,39);
                Paragraph(p,"A banana for a hungry friend.\nA gentle scratch for an itchy one.\nWatch both requests as the shift gets busier.",958,29);
                return;
            }
            Eyebrow(p,"FIELD NOTES");Heading(p,"A little better,\nevery day.");
            ZooMenuUI.Picture("Keeper note card",p,assets.journalCard,90,381,840,306);
            ZooMenuUI.Label("Card message",p,"GOOD CARE\nTAKES PRACTICE",145,440,435,145,37,ZooMenuUI.Ink,TextAnchor.MiddleLeft,true);
            Paragraph(p,"Missed a hungry monkey?\nTurn back to Day 01 and try again.\nYour best star record stays in this book.",760,32);
            Rule(p,1030);ZooMenuUI.Label("Return hint",p,"<  EARLIER DAYS ARE ALWAYS HERE",102,1070,815,70,28,ZooMenuUI.Leaf,TextAnchor.MiddleCenter,true);
        }
        void DayTwo(Transform p)
        {
            if(!string.IsNullOrEmpty(assets.dayTwoScene))
            {
                var rules = assets.dayTwoRules;
                Eyebrow(p,"DAY 02  /  LAKESIDE CARE");Heading(p,"A little extra\ncare by the lake");
                Stamp(p,ZooPCFlow.DayIsCompleted(2)?"SHIFT RECORDED  •  READY TO REPLAY":"YOUR NEXT SHIFT",375,ZooMenuUI.Leaf);
                ZooMenuUI.Label("Round facts",p,rules.monkeyCount + " MONKEYS     •     " + (rules.duration / 60).ToString("0.#") + " MINUTES",105,475,815,70,33,ZooMenuUI.Brown,TextAnchor.MiddleCenter,true);
                Rule(p,568);
                Entry(p,"01","Feed and soothe", "Bring bananas and a back scratcher.\nMatch the tool to each monkey's request.",606);
                Entry(p,"02","A busier afternoon", "Some monkeys stroll around the clearing.\nMove close and they will stop for care.",780);
                Rule(p,976);
                ZooMenuUI.Label("Day two record",p,ZooPCFlow.DayIsCompleted(2)?"BEST RECORD   "+ZooPCFlow.BestStarsFor(2)+" / 3 STARS":"STAR GOALS   " + rules.oneStar + " / " + rules.twoStars + " / " + rules.threeStars + " CARE TASKS",102,1012,815,64,31,ZooMenuUI.Leaf,TextAnchor.MiddleCenter,true);
                ZooMenuUI.Label("Day two replay",p,"Five little friends. Take care of them all.",102,1095,815,50,26,ZooMenuUI.Brown,TextAnchor.MiddleCenter);
                return;
            }
            Eyebrow(p,"DAY 02  /  NEXT CHAPTER");Heading(p,"Tomorrow's\nadventure");
            Stamp(p,"COMING SOON",399,ZooMenuUI.Hex("A29474"));
            ZooMenuUI.Label("Large day",p,"02",210,530,600,250,180,ZooMenuUI.Hex("BEB18F"),TextAnchor.MiddleCenter,true);
            Paragraph(p,"The next shift is still being prepared.\n\nFor now, return to Day 01 to meet\nthe troop and improve your record.",850,32);
            ZooMenuUI.Label("Locked note",p,"THIS DAY CANNOT BE STARTED YET",102,1110,815,48,27,ZooMenuUI.Brown,TextAnchor.MiddleCenter,true);
        }
        public ZooCollectionEntry[] Collection(bool animals)=>animals?assets.animals:assets.facilities;
        public int DiscoveredCount(bool animals){int count=0;foreach(var entry in Collection(animals))if(entry!=null&&entry.IsUnlocked)count++;return count;}
        void CollectionPortrait(Transform p,bool animals)
        {
            var entries=Collection(animals);ZooCollectionEntry featured=null;
            foreach(var entry in entries)if(entry!=null&&entry.IsUnlocked)featured=entry;
            int entryNumber=featured==null?1:System.Array.IndexOf(entries,featured)+1;
            Eyebrow(p,(animals?"ANIMALS":"FACILITIES")+"  /  NO. "+entryNumber.ToString("000"));
            if(featured==null){Heading(p,"Still a\nmystery...");ZooMenuUI.Glyph("Undiscovered",p,ZooGlyphGraphic.Kind.Lock,350,500,320,320,ZooMenuUI.Brown);return;}
            Heading(p,featured.displayName);
            ZooMenuUI.Handwriting("Collection subtitle",p,"A little discovery, kept forever.",95,285,830,65,39,ZooMenuUI.Brown,TextAnchor.MiddleLeft);
            ZooMenuUI.Panel("Portrait shadow",p,123,376,780,622,new Color(.29f,.22f,.12f,.18f),15);
            ZooMenuUI.Panel("Linen portrait mount",p,112,365,780,622,ZooMenuUI.Hex("FFF9E9"),15);
            ZooMenuUI.Stitches(p,130,384,744,583,ZooMenuUI.Hex("D5C69F"));
            ZooMenuUI.Panel("Sage portrait field",p,185,407,632,512,ZooMenuUI.Hex("E4E7C7"),220);
            ZooMenuUI.Glyph("Pressed leaf",p,ZooGlyphGraphic.Kind.Leaf,735,805,90,120,new Color(.37f,.48f,.24f,.2f));
            ZooMenuUI.Picture("Discovered model",p,featured.portrait,220,380,565,565);
            ZooMenuUI.Panel("Tape",p,400,348,210,41,new Color(.74f,.6f,.29f,.75f),3);
            ZooMenuUI.Handwriting("Specimen name",p,featured.subtitle,155,921,690,68,42,ZooMenuUI.Brown);
            ZooMenuUI.Glyph("Discovery paw",p,ZooGlyphGraphic.Kind.Paw,144,1044,67,67,ZooMenuUI.Leaf);
            ZooMenuUI.Label("Unlock date",p,"DISCOVERED  /  DAY "+featured.discoveredDay.ToString("00"),238,1044,645,67,32,ZooMenuUI.Leaf,TextAnchor.MiddleLeft,true);
            ZooMenuUI.Handwriting("Collection signature",p,"Keeper's collection",120,1120,775,55,36,ZooMenuUI.Brown);
        }
        void CollectionGrid(Transform p,bool animals)
        {
            Eyebrow(p,"YOUR COLLECTION");Heading(p,animals?"Monkey types":"Equipment");
            ZooMenuUI.Handwriting("Collection count",p,DiscoveredCount(animals)+" discovered • more stories to come",98,282,824,62,37,ZooMenuUI.Brown,TextAnchor.MiddleLeft);
            var entries=Collection(animals);
            for(int i=0;i<entries.Length&&i<6;i++)
            {
                var entry=entries[i];bool unlocked=entry!=null&&entry.IsUnlocked;float x=102+(i%2)*418,y=380+(i/2)*246;
                ZooMenuUI.Panel("Card shadow",p,x+4,y+7,386,220,new Color(.32f,.25f,.14f,.13f),18);
                ZooMenuUI.Panel("Card border",p,x,y,386,220,unlocked?ZooMenuUI.Leaf:ZooMenuUI.Hex("B9AD8D"),18);
                ZooMenuUI.Panel("Collection card",p,x+4,y+4,378,212,unlocked?ZooMenuUI.Hex("F7F5DC"):ZooMenuUI.Hex("DDD3B7"),15);
                if(unlocked)
                {
                    ZooMenuUI.Picture("Unlocked entry portrait",p,entry.portrait,x+112,y+8,162,162);
                    ZooMenuUI.Glyph("Registered paw",p,ZooGlyphGraphic.Kind.Paw,x+20,y+19,34,34,ZooMenuUI.Leaf);
                    ZooMenuUI.Label("Known type",p,entry.displayName,x+15,y+169,356,40,29,ZooMenuUI.Ink,TextAnchor.MiddleCenter,true);
                }
                else
                {
                    // Unknown slots deliberately expose neither a future name nor its silhouette.
                    ZooMenuUI.Glyph("Locked unknown entry",p,ZooGlyphGraphic.Kind.Lock,x+154,y+31,79,89,ZooMenuUI.Hex("9D8B65"));
                    ZooMenuUI.Label("Unknown type",p,"???",x+15,y+128,356,44,34,ZooMenuUI.Hex("9D8B65"),TextAnchor.MiddleCenter,true);
                    ZooMenuUI.Label("Locked caption",p,"UNDISCOVERED",x+15,y+178,356,30,20,ZooMenuUI.Hex("9D8B65"),TextAnchor.MiddleCenter,true);
                }
            }
            ZooMenuUI.Handwriting("Collection footer",p,"Some pages are still waiting for their story.",95,1123,830,60,31,ZooMenuUI.Brown);
        }
        static void Layer(GameObject go,int layer){go.layer=layer;foreach(Transform child in go.transform)Layer(child.gameObject,layer);}
        void OnDestroy(){foreach(var rt in paperImages){rt.Release();Destroy(rt);}foreach(var mat in materials)Destroy(mat);if(bookImage){bookImage.Release();Destroy(bookImage);}}
    }
}
