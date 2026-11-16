using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    // Geometry and existing star artwork form the opening decoration; no scene backdrop is added.
    public sealed class CareReadyView : MonoBehaviour
    {
        Vector3 originalScale;
        float began, duration;
        RectTransform rays;
        CanvasGroup alpha;
        public static void Decorate(RectTransform root, Sprite star, float seconds)
        {
            var glow = CareUi.Rect("Ready gold glow", root, new Vector2(1100, 850), Vector2.zero);
            glow.gameObject.AddComponent<CareReadyGraphic>().kind = 0; glow.SetAsFirstSibling();
            var rays = CareUi.Rect("Ready light rays", root, new Vector2(1040, 800), Vector2.zero);
            rays.gameObject.AddComponent<CareReadyGraphic>().kind = 1; rays.SetSiblingIndex(1);
            var ribbon = CareUi.Rect("Ready gold ribbon", root, new Vector2(1000, 210), new Vector2(0, -80));
            ribbon.gameObject.AddComponent<CareReadyGraphic>().kind = 2; ribbon.SetSiblingIndex(2);
            foreach (var point in new[] { new Vector2(-410, 35), new Vector2(410, 35), new Vector2(-275, -230), new Vector2(275, -230) })
            {
                var image = CareUi.Image("Ready star", root, star, new Vector2(90, 90), point);
                image.rectTransform.localEulerAngles = new Vector3(0, 0, point.x < 0 ? -12 : 12);
            }
            var view = root.gameObject.AddComponent<CareReadyView>(); view.duration = seconds;
            view.originalScale = root.localScale; view.began = MonkeyCareSession.GameTime; view.rays = rays;
            view.alpha = root.gameObject.AddComponent<CanvasGroup>();
        }
        void Update()
        {
            float t = MonkeyCareSession.GameTime - began;
            float pop = t < .3f ? Mathf.Lerp(.82f, 1, Mathf.SmoothStep(0, 1, t / .3f)) : 1 + Mathf.Sin((t - .3f) * 3) * .012f;
            transform.localScale = originalScale * pop;
            alpha.alpha = Mathf.Clamp01(t / .1f) * Mathf.Clamp01((duration - t) / .18f);
            rays.localEulerAngles = new Vector3(0, 0, t * 5);
        }
    }
}

