using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    [DefaultExecutionOrder(900)]
    public sealed class CareUiCustomization : MonoBehaviour
    {
        [Tooltip("Edit the Text, Image and RectTransform components under this object outside Play mode, then save the scene.")]
        public Transform editableUi;
        [Serializable] public sealed class TutorialCopy
        {
            public string title;
            [TextArea] public string body;
            public string action;
        }
        [Tooltip("Optional per-step copy. Empty fields retain the game's instruction and current controller prompts.")]
        public TutorialCopy[] tutorialSteps = new TutorialCopy[7];

        [Serializable] public sealed class State
        {
            public Vector2 position, size, anchorMin, anchorMax, pivot;
            public Vector3 scale;
            public Color color;
            public string text;
            public Font font;
            public int fontSize;
            public FontStyle fontStyle;
            public TextAnchor alignment;
            public bool bestFit;
            public Sprite sprite;
            public static State Read(RectTransform r)
            {
                var s = new State { position = r.anchoredPosition, size = r.sizeDelta,
                    anchorMin = r.anchorMin, anchorMax = r.anchorMax, pivot = r.pivot, scale = r.localScale };
                var g = r.GetComponent<Graphic>(); if (g) s.color = g.color;
                var t = r.GetComponent<Text>();
                if (t) { s.text = t.text; s.font = t.font; s.fontSize = t.fontSize;
                    s.fontStyle = t.fontStyle; s.alignment = t.alignment; s.bestFit = t.resizeTextForBestFit; }
                var i = r.GetComponent<Image>(); if (i) s.sprite = i.sprite;
                return s;
            }
        }
        [Serializable] public sealed class Element
        {
            public string group, key;
            public RectTransform source;
            public State baseline;
            public bool liveValue;
        }
        [HideInInspector] public List<Element> elements = new();
        sealed class Binding
        {
            public Element element;
            public RectTransform target;
            public State initial;
            public string lastText;
            public int edited;
        }
        readonly List<Binding> bindings = new();

        void Awake() { if (editableUi) editableUi.gameObject.SetActive(false); }
        void LateUpdate()
        {
            for (int n = bindings.Count - 1; n >= 0; n--)
            {
                var b = bindings[n];
                if (!b.target || !b.element.source) { bindings.RemoveAt(n); continue; }
                Apply(b);
            }
        }
        public static string Key(Transform node, Transform root)
        {
            if (node == root) return ".";
            return Key(node.parent, root) + "/" + node.name + "#" + node.GetSiblingIndex();
        }
        public void Capture(string group, RectTransform root)
        {
            foreach (var r in root.GetComponentsInChildren<RectTransform>(true))
            {
                string name = r.name;
                elements.Add(new Element { group = group, key = Key(r, root), source = r, baseline = State.Read(r),
                    liveValue = name == "Current score" || name == "Round clock" || name == "Banana count" ||
                        name == "Backpack quantity" || name == "Backpack status" || name == "Final sprint" ||
                        name.Contains(" key") || name.StartsWith("Key ") || name == "Tutorial step" });
            }
        }
        public static void Bind(string group, RectTransform root)
        {
            if (!Application.isPlaying) return;
            var config = FindFirstObjectByType<CareUiCustomization>();
            if (!config) return;
            var lookup = new Dictionary<string, Element>();
            foreach (var e in config.elements) if (e.group == group && e.source) lookup[e.key] = e;
            foreach (var r in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (!lookup.TryGetValue(Key(r, root), out var e)) continue;
                var b = new Binding { element = e, target = r, initial = State.Read(r) };
                var follower = r.GetComponent<CareProgressStarFollower>();
                if (follower) follower.settings = e.source.GetComponent<CareProgressStarFollower>();
                config.bindings.Add(b); Apply(b);
            }
        }
        static bool Edited(Binding binding, int bit, bool changed)
        {
            bool apply = changed || (binding.edited & bit) != 0;
            if (changed) binding.edited |= bit;
            else binding.edited &= ~bit;
            return apply;
        }
        static void Apply(Binding b)
        {
            var e = b.element; var s = e.source; var t = b.target; var baseline = e.baseline;
            var canvas = t.GetComponent<Canvas>();
            float positionUnit = canvas && canvas.renderMode == RenderMode.WorldSpace ? b.initial.scale.x : 1f;
            if (Edited(b, 1, s.anchoredPosition != baseline.position))
                t.anchoredPosition = b.initial.position + (s.anchoredPosition - baseline.position) * positionUnit;
            if (Edited(b, 2, s.sizeDelta != baseline.size)) t.sizeDelta = b.initial.size + s.sizeDelta - baseline.size;
            if (Edited(b, 4, s.anchorMin != baseline.anchorMin)) t.anchorMin = s.anchorMin;
            if (Edited(b, 8, s.anchorMax != baseline.anchorMax)) t.anchorMax = s.anchorMax;
            if (Edited(b, 16, s.pivot != baseline.pivot)) t.pivot = s.pivot;
            if (Edited(b, 32, s.localScale != baseline.scale))
                t.localScale = Vector3.Scale(b.initial.scale, new Vector3(
                    s.localScale.x / baseline.scale.x, s.localScale.y / baseline.scale.y, s.localScale.z / baseline.scale.z));
            var sg = s.GetComponent<Graphic>(); var tg = t.GetComponent<Graphic>();
            if (sg && tg && Edited(b, 64, sg.color != baseline.color)) tg.color = sg.color;
            var st = s.GetComponent<Text>(); var tt = t.GetComponent<Text>();
            if (st && tt)
            {
                if (!e.liveValue && (st.text != baseline.text || b.lastText != null) && (tt.text == baseline.text || tt.text == b.lastText))
                    { tt.text = st.text; b.lastText = st.text; }
                if (Edited(b, 128, st.font != baseline.font)) tt.font = st.font;
                if (Edited(b, 256, st.fontSize != baseline.fontSize)) { tt.fontSize = st.fontSize; tt.resizeTextMaxSize = st.fontSize; }
                if (Edited(b, 512, st.fontStyle != baseline.fontStyle)) tt.fontStyle = st.fontStyle;
                if (Edited(b, 1024, st.alignment != baseline.alignment)) tt.alignment = st.alignment;
                if (Edited(b, 2048, st.resizeTextForBestFit != baseline.bestFit)) tt.resizeTextForBestFit = st.resizeTextForBestFit;
            }
            var si = s.GetComponent<Image>(); var ti = t.GetComponent<Image>();
            if (si && ti && Edited(b, 4096, si.sprite != baseline.sprite)) ti.sprite = si.sprite;
        }
        public static void TutorialText(int step, Text title, Text body, Text action)
        {
            if (!Application.isPlaying) return;
            var config = FindFirstObjectByType<CareUiCustomization>();
            if (!config || config.tutorialSteps == null || step >= config.tutorialSteps.Length) return;
            var copy = config.tutorialSteps[step]; if (copy == null) return;
            string Expand(string s) => s.Replace("{A}", CareInput.ConfirmLabel).Replace("{B}", CareInput.BackLabel);
            if (!string.IsNullOrEmpty(copy.title)) title.text = Expand(copy.title);
            if (!string.IsNullOrEmpty(copy.body)) body.text = Expand(copy.body);
            if (!string.IsNullOrEmpty(copy.action)) action.text = Expand(copy.action);
        }
    }
}

