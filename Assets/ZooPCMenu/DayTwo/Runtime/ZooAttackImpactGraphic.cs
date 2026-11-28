using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.PC
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ZooAttackImpactGraphic : MaskableGraphic
    {
        public ZooAttackImpactGraphic() { useLegacyMeshGeneration = false; }
        float flash, intensity, age;
        int hit;
        public void SetImpact(float flash, float intensity, int hit, float age)
        { this.flash = flash; this.intensity = intensity; this.hit = hit; this.age = age; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect; Vector2 center = r.center;
            Quad(vh, new Vector2(r.xMin,r.yMin),new Vector2(r.xMin,r.yMax),new Vector2(r.xMax,r.yMax),new Vector2(r.xMax,r.yMin),new Color(1,.71f,.4f,flash));
            // Inward streaks leave the face unobscured.
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2 / 48;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                float distance = Mathf.Min(r.width * .53f / Mathf.Max(.001f, Mathf.Abs(direction.x)), r.height * .53f / Mathf.Max(.001f, Mathf.Abs(direction.y)));
                Vector2 edge = center + direction * distance;
                float reach = .74f + .13f * Mathf.Sin(i * 13.2f + Mathf.Floor(age * 12));
                Triangle(vh, edge + perpendicular * (4 + i % 4), edge - perpendicular * (4 + i % 4), center + direction * distance * reach, new Color(1,.73f,.36f,intensity * .7f));
            }
            if (flash > .01f)
            {
                Vector2 at = center + new Vector2(hit % 2 == 0 ? -r.width * .18f : r.width * .18f, r.height * .02f);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2 / 12, b = (i + 1) * Mathf.PI * 2 / 12;
                    Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? 70 : 22);
                    Vector2 q = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * (i % 2 == 0 ? 22 : 70);
                    Triangle(vh, at, at+p, at+q, new Color(1,.91f,.62f,Mathf.Min(1,flash*3)));
                }
            }
        }
        static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int n=vh.currentVertCount; vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);
        }
        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        { Triangle(vh,a,b,c,color); Triangle(vh,a,c,d,color); }
    }
}
