using UnityEngine;
using UnityEngine.UI;
namespace ADayAtTheZoo.Monkeys.Care
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CareReadyGraphic : MaskableGraphic
    {
        public int kind;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); raycastTarget = false;
            if (kind == 2)
            {
                // Each notched tail uses two convex pieces so the notch stays open.
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 P(float x,float y) => new Vector2(side*x,y);
                    Quad(mesh,P(320,65),P(505,35),P(455,-20),P(320,-30),new Color(1,.69f,.10f));
                    Quad(mesh,P(320,-30),P(455,-20),P(505,-100),P(320,-75),new Color(1,.55f,.04f));
                    Polygon(mesh,new[]{P(320,-75),P(385,-45),P(385,80)},new Color(.78f,.34f,.025f));
                }
                Quad(mesh,new Vector2(-385,-45),new Vector2(385,-45),new Vector2(385,105),new Vector2(-385,105),new Color(1,.72f,.12f));
                Quad(mesh,new Vector2(-385,30),new Vector2(385,30),new Vector2(385,105),new Vector2(-385,105),new Color(1,.83f,.30f));
                return;
            }
            const int steps = 96;
            float rx = rectTransform.rect.width * .5f, ry = rectTransform.rect.height * .5f;
            for (int i=0;i<steps;i++)
            {
                float a = i * Mathf.PI * 2 / steps, b = (i+1)*Mathf.PI*2/steps;
                Color center = new Color(1,.86f,.05f,kind == 0 ? .7f : .22f);
                float radius = kind == 0 ? 1 : (i % 4 < 2 ? 1 : .47f);
                Triangle(mesh, Vector2.zero, new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry)*radius,
                    new Vector2(Mathf.Cos(b)*rx,Mathf.Sin(b)*ry)*radius, center, new Color(1,.82f,.03f,0));
            }
        }
        static void Triangle(VertexHelper m, Vector2 a, Vector2 b, Vector2 c, Color center, Color edge)
        {
            int n=m.currentVertCount; m.AddVert(a,center,Vector2.zero); m.AddVert(b,edge,Vector2.zero); m.AddVert(c,edge,Vector2.zero); m.AddTriangle(n,n+2,n+1);
        }
        static void Quad(VertexHelper m,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        { Polygon(m,new[]{a,b,c,d},color); }
        static void Polygon(VertexHelper m,Vector2[] points,Color color)
        {
            int start=m.currentVertCount;
            foreach(var p in points)m.AddVert(p,color,Vector2.zero);
            for(int i=1;i<points.Length-1;i++)m.AddTriangle(start,start+i+1,start+i);
        }
    }
}



