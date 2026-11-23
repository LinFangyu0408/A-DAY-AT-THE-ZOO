using System;
using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    public enum CareFinish { [InspectorName("跟随交互状态")] State, [InspectorName("固定时长")] Seconds, [InspectorName("重复次数")] Repeats }
    public enum CareTint { [InspectorName("保留原色")] Original, [InspectorName("颜色覆盖")] Color, [InspectorName("渐变覆盖")] Gradient }

    [Serializable]
    public sealed class CareAnimation
    {
        [InspectorName("动画类型")] public MonkeyAction action;
        [InspectorName("自定义 Humanoid 动画")] public AnimationClip clip;
        [InspectorName("播放速度"), Min(.01f)] public float speed = 1;
        [InspectorName("结束方式")] public CareFinish finish;
        [InspectorName("持续秒数"), Min(.01f)] public float seconds = 2;
        [InspectorName("重复次数"), Min(1)] public int repeats = 1;
        public float Length(AnimationClip resolved) => finish == CareFinish.Seconds ? Mathf.Max(.01f, seconds) :
            finish == CareFinish.Repeats ? Mathf.Max(1, repeats) * resolved.length / Mathf.Max(.01f, speed) : float.PositiveInfinity;
    }

    [Serializable]
    public sealed class CareEffect
    {
        [InspectorName("特效 Prefab")] public GameObject prefab;
        [InspectorName("挂点（空为所属对象）")] public Transform anchor;
        [InspectorName("相对位置")] public Vector3 position;
        [InspectorName("相对旋转")] public Vector3 rotation;
        [InspectorName("相对缩放")] public Vector3 scale = Vector3.one;
        [InspectorName("颜色设置")] public CareTint tint;
        [InspectorName("颜色")] public Color color = Color.white;
        [InspectorName("渐变")] public Gradient gradient = new Gradient();
        [InspectorName("持续秒数（0跟随状态）"), Min(0)] public float duration;

        public GameObject Spawn(Transform owner, float lifetime = -1)
        {
            if (!prefab) return null;
            var go = UnityEngine.Object.Instantiate(prefab, anchor ? anchor : owner);
            go.name = "Care FX - " + prefab.name;
            go.transform.localPosition = position; go.transform.localEulerAngles = rotation; go.transform.localScale = scale;
            foreach (var collider in go.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (tint != CareTint.Original)
                {
                    var original = main.startColor;
                    Gradient g = new Gradient();
                    g.SetKeys(tint == CareTint.Color ? new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) } : gradient.colorKeys,
                        original.mode == ParticleSystemGradientMode.Gradient ? original.gradient.alphaKeys :
                        new[] { new GradientAlphaKey(original.color.a, 0), new GradientAlphaKey(original.color.a, 1) });
                    main.startColor = new ParticleSystem.MinMaxGradient(g);
                }
                ps.Play();
            }
            if (tint != CareTint.Original)
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer) continue;
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                    var c = tint == CareTint.Color ? color : gradient.Evaluate(.5f);
                    foreach (var property in new[] { "_BaseColor", "_Color", "_TintColor" })
                        if (renderer.sharedMaterial && renderer.sharedMaterial.HasProperty(property)) block.SetColor(property, c);
                    renderer.SetPropertyBlock(block);
                }
            float seconds = lifetime >= 0 ? lifetime : duration;
            if (seconds > 0) UnityEngine.Object.Destroy(go, seconds);
            return go;
        }
    }

    [Serializable]
    public sealed class CareUiStyle
    {
        [InspectorName("耐心面板")] public Sprite stamina;
        [InspectorName("满意度面板")] public Sprite satisfaction;
        [InspectorName("进度槽")] public Sprite track;
        [InspectorName("绿蓝填充")] public Sprite staminaFill;
        [InspectorName("黄橙填充")] public Sprite satisfactionFill;
        [InspectorName("耐心图标")] public Sprite staminaLogo;
        [InspectorName("满意图标")] public Sprite satisfactionLogo;
        [InspectorName("超时表情")] public Sprite sad;
        [InspectorName("成功表情")] public Sprite happy;
        [InspectorName("头顶面板偏移")] public Vector3 barOffset = new Vector3(0, 1.85f, 0);
        [InspectorName("面板宽度（米）"), Min(.1f)] public float width = 1.1f;
        [InspectorName("悲伤表情偏移")] public Vector3 sadOffset = new Vector3(.7f, 1.5f, 0);
        [InspectorName("开心表情偏移")] public Vector3 happyOffset = new Vector3(-.7f, 1.5f, 0);
        [InspectorName("表情宽度（米）"), Min(.1f)] public float emotionSize = .4f;
        [InspectorName("分段间距（像素）"), Range(0, 16)] public float segmentGap = 6;
    }

    public static class CareUi
    {
        public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.sizeDelta = size; r.anchoredPosition = pos; return r;
        }
        public static Image Image(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos)
        {
            var r = Rect(name, parent, size, pos); var image = r.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false; return image;
        }
        public static RectTransform Canvas(string name, Transform parent, Vector3 position, float width, Vector2 size, int sortingOrder = 0)
        {
            var r = Rect(name, parent, size, Vector2.zero); r.localPosition = position;
            Vector3 parentScale = parent ? parent.lossyScale : Vector3.one;
            float unit = width / size.x;
            r.localScale = new Vector3(unit / parentScale.x, unit / parentScale.y, unit / parentScale.z);
            var canvas = r.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            // World-space canvases sort by distance unless an explicit order is
            // given; the monkey's head plate must win over the fixed HUD.
            canvas.sortingOrder = sortingOrder;
            return r;
        }
        // Flat 2D HUD for the PC (no-headset) layout. One root canvas carries a
        // CanvasScaler so every plate scales with the screen: fixed-pixel plates
        // were rendered raw and a Game view zoom of 2x blew them up to cover the
        // whole screen.
        public static RectTransform OverlayRoot(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // A larger reference keeps the plates at HUD-like proportions
            // (score ≈ 42% of the width) instead of half the screen.
            scaler.referenceResolution = new Vector2(2400, 1350);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            var r = go.transform as RectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return r;
        }

        // Screen-space buttons (pad-style chip, results actions) are real UI
        // Buttons; without an EventSystem nothing routes a mouse click to them.
        // The scene ships without one because the VR layout never needed it.
        public static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current) return;
            var go = new GameObject("Care Event System",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var module = go.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (module) module.AssignDefaultActions();
        }
        // A plate inside the flat HUD root. Positions are in the reference space;
        // pass an anchor/pivot to pin a plate to a screen corner.
        public static RectTransform Plate(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
            Vector2? anchor = null, Vector2? pivot = null)
        {
            var r = Rect(name, parent, size, anchoredPos);
            r.anchorMin = r.anchorMax = anchor ?? new Vector2(.5f, .5f);
            r.pivot = pivot ?? new Vector2(.5f, .5f);
            r.anchoredPosition = anchoredPos;
            r.sizeDelta = size;
            return r;
        }
    }

    public sealed class CarePanel
    {
        public readonly RectTransform Root;
        readonly Image panel, logo;
        readonly Text caption;
        readonly RectTransform track;
        readonly CareUiStyle style;
        readonly System.Collections.Generic.List<RectTransform> masks = new();
        readonly System.Collections.Generic.List<GameObject> dividers = new();
        int count = -1;
        bool satisfaction;
        public CarePanel(Transform parent, CareUiStyle value)
        {
            style = value;
            Root = CareUi.Canvas("Care Progress", parent, value.barOffset, value.width, new Vector2(640, 290), 60);
            // Bottom-centre pivot: barOffset now names the plate's LOWER edge, so
            // the card always grows upward from the anchor. With a centre pivot
            // half of this 0.5 m-tall card hung below the anchor and sank into the
            // monkey's head whenever the anchor hugged the skull.
            Root.pivot = new Vector2(.5f, 0f);
            panel = CareUi.Image("Panel", Root, value.stamina, new Vector2(640, 290), Vector2.zero);
            // The panel artwork already contains its title; keep the live label hidden.
            caption = CareRoundView.Label("Panel title", Root, "STAMINA",
                new Vector2(470, 68), new Vector2(0, 77), 40,
                TextAnchor.MiddleCenter, new Color(.36f, .20f, .24f, 1f));
            caption.resizeTextForBestFit = false;
            caption.horizontalOverflow = HorizontalWrapMode.Overflow;
            caption.verticalOverflow = VerticalWrapMode.Overflow;
            caption.enabled = false;
            track = CareUi.Image("Track", Root, value.track, new Vector2(490, 85), new Vector2(30, -43)).rectTransform;
            logo = CareUi.Image("Logo", Root, value.staminaLogo, new Vector2(122, 122), new Vector2(-210, -43));
            Show(false, 1, 0);
        }
        public void Show(bool isSatisfaction, float amount, int segments)
        {
            if (count != Mathf.Max(1, segments) || satisfaction != isSatisfaction)
            {
                foreach (var m in masks) UnityEngine.Object.Destroy(m.gameObject);
                foreach (var divider in dividers) UnityEngine.Object.Destroy(divider);
                dividers.Clear();
                masks.Clear(); count = Mathf.Max(1, segments); satisfaction = isSatisfaction;
                panel.sprite = isSatisfaction ? style.satisfaction : style.stamina;
                logo.sprite = isSatisfaction ? style.satisfactionLogo : style.staminaLogo;
                caption.text = isSatisfaction ? "SATISFACTION" : "STAMINA";
                float slot = 490f / count;
                for (int i = 0; i < count; i++)
                {
                    var mask = CareUi.Rect("Segment " + (i + 1), track, new Vector2(slot, 85), new Vector2(i * slot, 0));
                    mask.anchorMin = mask.anchorMax = mask.pivot = new Vector2(0, .5f);
                    mask.gameObject.AddComponent<RectMask2D>(); masks.Add(mask);
                    var fill = CareUi.Image("Fill", mask, isSatisfaction ? style.satisfactionFill : style.staminaFill,
                        new Vector2(490, 85), new Vector2(-i * slot, 0)).rectTransform;
                    fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0, .5f);
                }
                for (int i = 1; i < count; i++)
                {
                    var divider = CareUi.Image("Segment divider", track, null, new Vector2(Mathf.Max(2, style.segmentGap), 77),
                        new Vector2(-245 + i * slot - style.segmentGap * .5f, 0));
                    divider.color = new Color(.95f, .91f, .82f, .8f); dividers.Add(divider.gameObject);
                }
            }
            // Resizing a RectMask2D dirties the canvas; quantize so the mask
            // geometry is only touched when the bar visibly moved.
            int steps = Mathf.RoundToInt(amount * count * 50f);
            if (steps == lastMaskSteps) return;
            lastMaskSteps = steps;
            float quantized = steps / (50f * count);
            for (int i = 0; i < count; i++)
                masks[i].sizeDelta = new Vector2((490f / count - (i < count - 1 ? style.segmentGap : 0)) * Mathf.Clamp01(quantized * count - i), 85);
        }
        int lastMaskSteps = -1;
    }
}
