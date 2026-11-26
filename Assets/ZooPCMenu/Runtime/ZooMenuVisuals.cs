using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace ADayAtTheZoo.PC
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ZooGlyphGraphic : MaskableGraphic
    {
        public enum Kind{Lock,Paw,Leaf,Sparkle}
        public Kind kind;
        public ZooGlyphGraphic(){useLegacyMeshGeneration=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;Vector2 center=r.center;float s=Mathf.Min(r.width,r.height);
            if(kind==Kind.Lock)
            {
                for(int i=0;i<24;i++){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;Quad(vh,center+s*(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.28f+Vector2.up*.12f),center+s*(new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.28f+Vector2.up*.12f),center+s*(new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.17f+Vector2.up*.12f),center+s*(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.17f+Vector2.up*.12f),color);}
                Box(vh,center,s,-.28f,-.05f,.11f,.2f,color);Box(vh,center,s,.17f,-.05f,.11f,.2f,color);
                Box(vh,center,s,-.37f,-.38f,.74f,.5f,color);var hole=ZooMenuUI.Cream;hole.a=color.a;Disc(vh,center+Vector2.down*s*.12f,s*.075f,s*.075f,hole);Box(vh,center,s,-.035f,-.26f,.07f,.13f,hole);
            }
            else if(kind==Kind.Paw)
            {
                Disc(vh,center+Vector2.down*s*.12f,s*.27f,s*.22f,color);
                for(int i=0;i<4;i++){float x=(i-1.5f)*.21f,y=(i==1||i==2)?.27f:.13f;Disc(vh,center+new Vector2(x,y)*s,s*.105f,s*.135f,color);}
            }
            else if(kind==Kind.Sparkle)Quad(vh,center+Vector2.up*s*.5f,center+Vector2.right*s*.14f,center+Vector2.down*s*.5f,center+Vector2.left*s*.14f,color);
            else
            {
                int start=vh.currentVertCount;vh.AddVert(center,color,Vector2.zero);
                for(int i=0;i<16;i++){float a=i*Mathf.PI*2/16;var v=new Vector2(Mathf.Cos(a)*.25f,Mathf.Sin(a)*.5f);v=Quaternion.Euler(0,0,-32)*v;vh.AddVert(center+v*s,color,Vector2.zero);}
                for(int i=0;i<16;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%16);
            }
        }
        static void Box(VertexHelper vh,Vector2 c,float s,float x,float y,float w,float h,Color col)=>Quad(vh,c+new Vector2(x,y)*s,c+new Vector2(x+w,y)*s,c+new Vector2(x+w,y+h)*s,c+new Vector2(x,y+h)*s,col);
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color col){int i=vh.currentVertCount;vh.AddVert(a,col,Vector2.zero);vh.AddVert(b,col,Vector2.zero);vh.AddVert(c,col,Vector2.zero);vh.AddVert(d,col,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
        static void Disc(VertexHelper vh,Vector2 c,float rx,float ry,Color col){int n=vh.currentVertCount;vh.AddVert(c,col,Vector2.zero);for(int i=0;i<20;i++){float a=i*Mathf.PI*2/20;vh.AddVert(c+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry),col,Vector2.zero);}for(int i=0;i<20;i++)vh.AddTriangle(n,n+1+i,n+1+(i+1)%20);}
    }
    public sealed class ZooMenuAtmosphere : MonoBehaviour
    {
        RectTransform background,logo;ZooGlyphGraphic[] particles;Vector2[] origins;float[] phases;
        public void Initialize(RectTransform back,RectTransform mark)
        {
            background=back;logo=mark;particles=new ZooGlyphGraphic[24];origins=new Vector2[24];phases=new float[24];var random=new System.Random(5031);
            for(int i=0;i<24;i++){float x=(float)random.NextDouble()*1920,y=(float)random.NextDouble()*1080;origins[i]=new Vector2(x,-y);phases[i]=(float)random.NextDouble()*6.28f;float size=i<7?24:5;particles[i]=ZooMenuUI.Glyph("Sunlit leaf mote",transform,i<7?ZooGlyphGraphic.Kind.Leaf:ZooGlyphGraphic.Kind.Sparkle,x,y,size,size*1.4f,new Color(1,.9f,.55f,.3f));}
        }
        void Update()
        {
            if(particles==null)return;float time=Time.unscaledTime;bool still=ZooPCFlow.ReducedMotion;
            if(background){Vector2 pointer=Mouse.current!=null?Mouse.current.position.ReadValue():new Vector2(Screen.width,Screen.height)*.5f;Vector2 shift=new Vector2(pointer.x/Mathf.Max(1,Screen.width)-.5f,pointer.y/Mathf.Max(1,Screen.height)-.5f);background.localScale=Vector3.one*(still?1:1.025f);background.anchoredPosition=Vector2.Lerp(background.anchoredPosition,still?Vector2.zero:shift*12,Time.unscaledDeltaTime*3);}
            if(logo)logo.localRotation=Quaternion.Euler(0,0,still?0:Mathf.Sin(time*.7f)*.7f);
            for(int i=0;i<particles.Length;i++){float phase=phases[i];var r=particles[i].rectTransform;float speed=i<7?10:4;r.anchoredPosition=origins[i]+(still?Vector2.zero:new Vector2(Mathf.Sin(time*.17f+phase)*45,Mathf.Repeat(time*speed+phase*70,1200)-600));r.localRotation=Quaternion.Euler(0,0,still?0:Mathf.Sin(time*.6f+phase)*35);float alpha=still?.12f:(.12f+.22f*(.5f+.5f*Mathf.Sin(time*.9f+phase)));particles[i].color=i<7?new Color(.74f,.82f,.3f,alpha):new Color(1,.93f,.66f,alpha+.1f);}
        }
    }
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ZooScreenReveal : MonoBehaviour
    {
        CanvasGroup group;float age;
        void OnEnable(){group=GetComponent<CanvasGroup>();age=0;group.alpha=ZooPCFlow.ReducedMotion?1:0;}
        void Update(){age+=Time.unscaledDeltaTime;float t=ZooPCFlow.ReducedMotion?1:Mathf.Clamp01(age/.45f);group.alpha=1-Mathf.Pow(1-t,3);}
    }
}
