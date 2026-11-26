using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ADayAtTheZoo.PC
{
    public static class ZooMenuUI
    {
        public static readonly Color Ink = Hex("35472B"), Cream = Hex("FFF5D9"), Leaf = Hex("526E3C"), Orange = Hex("D17A42"), Brown = Hex("67452D");
        public static Font Heading, Body, Notes;
        public static Texture LeafTexture, LinenTexture;
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString("#"+value,out var c); return c; }
        public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
        }
        public static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; }
        public static RectTransform Group(string name, Transform parent) { var r=Rect(name,parent,0,0,1920,1080); Stretch(r); return r; }
        public static ZooRoundedPanel Panel(string name, Transform parent,float x,float y,float w,float h,Color color,float radius=24)
        {
            var g=Rect(name,parent,x,y,w,h).gameObject.AddComponent<ZooRoundedPanel>(); g.color=color; g.radius=radius; g.raycastTarget=false; return g;
        }
        public static RawImage Picture(string name,Transform parent,Texture texture,float x,float y,float w,float h,Color? color=null)
        {
            var g=Rect(name,parent,x,y,w,h).gameObject.AddComponent<RawImage>(); g.texture=texture; g.color=color??Color.white; g.raycastTarget=false; return g;
        }
        public static Text Label(string name,Transform parent,string value,float x,float y,float w,float h,int size,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft,bool heading=false)
        {
            var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Text>(); t.font=heading?Heading:Body;
            t.text=value; t.fontSize=size; t.color=color??Ink; t.alignment=align; t.raycastTarget=false;
            t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Truncate;
            return t;
        }
        public static Button Button(string name,Transform parent,string value,float x,float y,float w,float h,Action action,Color? fill=null,int size=32)
        {
            var root=Rect(name,parent,x,y,w,h);
            root.pivot=Vector2.one*.5f;root.anchoredPosition=new Vector2(x+w*.5f,-y-h*.5f);
            Graphic bg;
            if(LeafTexture&&LinenTexture)
            {
                bool green=fill.HasValue&&fill.Value.g>=fill.Value.r;
                var face=Picture("Fabric face",root,green?LeafTexture:LinenTexture,-w*.05f,-h*.25f,w*1.1f,h*1.5f);
                if(fill.HasValue&&!green)face.color=Color.Lerp(Color.white,fill.Value,.48f);
                face.raycastTarget=true;bg=face;
            }
            else
            {
                Panel("Shadow",root,0,6,w,h,Hex("62452D"),20);
                bg=Panel("Face",root,0,0,w,h,fill??Cream,20);bg.raycastTarget=true;
            }
            var b=root.gameObject.AddComponent<Button>(); b.targetGraphic=bg;
            var colors=b.colors; colors.normalColor=Color.white; colors.highlightedColor=Hex("FFF3CF"); colors.selectedColor=Color.white; colors.pressedColor=Hex("D7B878"); colors.disabledColor=new Color(.65f,.65f,.65f,.7f); colors.fadeDuration=.12f;b.colors=colors;
            bool dark=fill.HasValue&&fill.Value.g>=fill.Value.r;
            var label=Label("Label",root,value,35,0,w-70,h,size,dark?Cream:Ink,TextAnchor.MiddleCenter,true);
            label.gameObject.AddComponent<Shadow>().effectColor=dark?new Color(.13f,.22f,.09f,.5f):new Color(1,1,.85f,.5f);
            label.GetComponent<Shadow>().effectDistance=new Vector2(0,-1.5f);
            b.onClick.AddListener(()=>{ZooPCFlow.Instance?.Click();action?.Invoke();});
            root.gameObject.AddComponent<ZooButtonMotion>(); return b;
        }
        public static Text Handwriting(string name,Transform parent,string value,float x,float y,float w,float h,int size,Color? color=null,TextAnchor align=TextAnchor.MiddleCenter)
        {
            var label=Label(name,parent,value,x,y,w,h,size,color??Brown,align);if(Notes)label.font=Notes;return label;
        }
        public static ZooGlyphGraphic Glyph(string name,Transform parent,ZooGlyphGraphic.Kind kind,float x,float y,float w,float h,Color color)
        {
            var icon=Rect(name,parent,x,y,w,h).gameObject.AddComponent<ZooGlyphGraphic>();icon.kind=kind;icon.color=color;icon.raycastTarget=false;return icon;
        }
        public static void Stitches(Transform parent,float x,float y,float w,float h,Color color)
        {
            for(float px=x+18;px<x+w-18;px+=17){Panel("Stitch",parent,px,y,8,2,color,1);Panel("Stitch",parent,px,y+h,8,2,color,1);}
            for(float py=y+15;py<y+h-10;py+=17){Panel("Stitch",parent,x,py,2,8,color,1);Panel("Stitch",parent,x+w,py,2,8,color,1);}
        }
        public static Canvas Canvas(string name,int order=100)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var c=go.GetComponent<Canvas>(); c.renderMode=RenderMode.ScreenSpaceOverlay; c.sortingOrder=order;
            var s=go.GetComponent<CanvasScaler>(); s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution=new Vector2(1920,1080);s.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            return c;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ZooRoundedPanel : MaskableGraphic
    {
        public float radius=24;
        public ZooRoundedPanel(){useLegacyMeshGeneration=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect; float rad=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
            vh.AddVert(r.center,color,Vector2.one*.5f);
            int count=0;
            for(int corner=0;corner<4;corner++)
            {
                var center=new Vector2(corner<2?r.xMax-rad:r.xMin+rad,(corner==0||corner==3)?r.yMax-rad:r.yMin+rad);
                for(int i=0;i<=8;i++)
                {
                    float a=(90-corner*90-i*90f/8)*Mathf.Deg2Rad;
                    vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*rad,color,Vector2.zero);count++;
                }
            }
            for(int i=1;i<=count;i++)vh.AddTriangle(0,i,i==count?1:i+1);
        }
    }
    public sealed class ZooButtonMotion : MonoBehaviour, IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler,IPointerDownHandler,IPointerUpHandler
    {
        bool hover,selected,pressed;float pulse;Button button;RectTransform rect;ZooGlyphGraphic[] glints;
        void Awake(){button=GetComponent<Button>();rect=(RectTransform)transform;glints=new ZooGlyphGraphic[3];for(int i=0;i<3;i++)glints[i]=ZooMenuUI.Glyph("Focus glint",transform,ZooGlyphGraphic.Kind.Sparkle,rect.rect.width*(.13f+i*.34f),i==1?-12:rect.rect.height-10,16,22,new Color(1,.85f,.34f,0));}
        public void OnPointerEnter(PointerEventData e){hover=true;pulse=1;}
        public void OnPointerExit(PointerEventData e){hover=false;pressed=false;}
        public void OnSelect(BaseEventData e){selected=true;pulse=1;}
        public void OnDeselect(BaseEventData e){selected=false;}
        public void OnPointerDown(PointerEventData e){pressed=true;}
        public void OnPointerUp(PointerEventData e){pressed=false;pulse=1;}
        void OnDisable(){transform.localScale=Vector3.one;transform.localRotation=Quaternion.identity;hover=pressed=selected=false;}
        void Update()
        {
            bool focus=button&&button.interactable&&(hover||selected);float t=Time.unscaledTime;
            float scale=ZooPCFlow.ReducedMotion?1:pressed?.96f:focus?1.045f:1;
            transform.localScale=Vector3.Lerp(transform.localScale,Vector3.one*scale,1-Mathf.Exp(-16*Time.unscaledDeltaTime));
            float tilt=focus&&!ZooPCFlow.ReducedMotion?Mathf.Sin(t*3)*.6f:0;transform.localRotation=Quaternion.Euler(0,0,tilt);
            pulse=Mathf.Max(0,pulse-Time.unscaledDeltaTime*.8f);
            for(int i=0;i<glints.Length;i++){float a=focus?(ZooPCFlow.ReducedMotion?.6f:(.25f+.6f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*3+i*1.7f)),3))):pulse*.35f;glints[i].color=new Color(1,.85f,.34f,a);}
        }
    }
}
