using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ADayAtTheZoo.PC
{
    public static class ZooMenuUI
    {
        public static readonly Color Ink = Hex("35472B"), Cream = Hex("FFF5D9"), Leaf = Hex("526E3C"), Orange = Hex("D17A42"), Brown = Hex("67452D");
        public static Font Heading, Body;
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
            Panel("Shadow",root,0,6,w,h,Hex("62452D"),20);
            var bg=Panel("Face",root,0,0,w,h,fill??Cream,20); bg.raycastTarget=true;
            var b=root.gameObject.AddComponent<Button>(); b.targetGraphic=bg;
            var colors=b.colors; colors.normalColor=Color.white; colors.highlightedColor=Hex("FFE3A6"); colors.selectedColor=Hex("FFE3A6"); colors.pressedColor=Hex("D7B878"); colors.disabledColor=new Color(.65f,.65f,.65f,.75f); colors.fadeDuration=.12f;b.colors=colors;
            Label("Label",root,value,18,0,w-36,h,size,fill.HasValue?Cream:Ink,TextAnchor.MiddleCenter,true);
            b.onClick.AddListener(()=>{ZooPCFlow.Instance?.Click();action?.Invoke();});
            root.gameObject.AddComponent<ZooButtonMotion>(); return b;
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
    public sealed class ZooButtonMotion : MonoBehaviour, IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        bool focus;
        public void OnPointerEnter(PointerEventData e){focus=true;}
        public void OnPointerExit(PointerEventData e){focus=false;}
        public void OnSelect(BaseEventData e){focus=true;}
        public void OnDeselect(BaseEventData e){focus=false;}
        void Update(){float scale=focus&&!ZooPCFlow.ReducedMotion?1.025f:1; transform.localScale=Vector3.Lerp(transform.localScale,Vector3.one*scale,1-Mathf.Exp(-20*Time.unscaledDeltaTime));}
    }
}
