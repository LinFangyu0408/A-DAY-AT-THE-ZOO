using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    [Serializable]
    public sealed class CareRoundArtwork
    {
        public Sprite scorePanel, bananaPanel, level1, level2, level3;
        public Sprite resultsPanel, star, continueButton, restartButton;
        [InspectorName("香蕉数量挂点")] public Transform bananaAnchor;
        [InspectorName("香蕉数量位置偏移")] public Vector3 bananaOffset = new Vector3(0, .6f, 0);
        [InspectorName("香蕉数量面板宽度"), Min(.1f)] public float bananaWidth = .55f;
    }
    public sealed class CareRoundView
    {
        readonly CareRoundArtwork art;
        readonly Camera camera;
        readonly RectTransform scoreHud, bananaHud, results;
        readonly Image grade;
        readonly Image[] stars = new Image[3];
        readonly Text score, bananas, zeroGrade, clock, feedings, resolved, tasks, total, next;
        readonly CareResultButton[] buttons;
        static Font font;
        public static Text Label(string name, Transform parent, string value, Vector2 size, Vector2 pos, int fontSize, TextAnchor align = TextAnchor.MiddleCenter)
        {
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var r = CareUi.Rect(name, parent, size, pos); var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.fontStyle = FontStyle.Bold; t.color = new Color(.25f, .16f, .22f);
            t.alignment = align; t.raycastTarget = false; t.text = value; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }
        public CareRoundView(Camera camera, CareRoundArtwork artwork)
        {
            art = artwork;
            this.camera = camera;
            scoreHud = CareUi.Canvas("Care SCORE HUD", camera.transform, new Vector3(0, .64f, 1.5f), .58f, new Vector2(837, 270));
            CareUi.Image("Score panel", scoreHud, art.scorePanel, new Vector2(837, 270), Vector2.zero);
            grade = CareUi.Image("Star level", scoreHud, art.star, new Vector2(105, 110), new Vector2(0, -25));
            zeroGrade = Label("Zero stars", grade.transform, "0", new Vector2(70, 65), Vector2.zero, 50);
            score = Label("Score number", scoreHud, "48.00", new Vector2(200, 60), new Vector2(280, 70), 34);
            clock = Label("Round clock", scoreHud, "03:50", new Vector2(300, 48), new Vector2(0, -165), 35);
            clock.color = Color.white; clock.gameObject.AddComponent<Shadow>();
            bananaHud = CareUi.Canvas("Care BANANA HUD", art.bananaAnchor ? art.bananaAnchor : camera.transform,
                art.bananaAnchor ? art.bananaOffset : new Vector3(.52f, .63f, 1.5f), art.bananaWidth, new Vector2(545, 313));
            CareUi.Image("Banana panel", bananaHud, art.bananaPanel, new Vector2(545, 313), Vector2.zero);
            bananas = Label("Banana count", bananaHud, "×12", new Vector2(260, 140), new Vector2(125, 0), 92);

            results = CareUi.Canvas("Care RESULTS", camera.transform, new Vector3(0, -.02f, 1.5f), .92f, new Vector2(1368, 2000));
            var panel = CareUi.Image("Results panel", results, art.resultsPanel, new Vector2(1368, 1800), new Vector2(0, 100)).rectTransform;
            ReplaceLabel(panel, "Feedings Completed", 400);
            ReplaceLabel(panel, "Resolved Status", 265);
            ReplaceLabel(panel, "Tasks Completed", 130);
            feedings = Label("Feedings value", panel, "0/9", new Vector2(280, 100), new Vector2(475, 400), 76, TextAnchor.MiddleRight);
            resolved = Label("Resolved value", panel, "0.0%", new Vector2(280, 100), new Vector2(475, 265), 76, TextAnchor.MiddleRight);
            tasks = Label("Tasks value", panel, "0/18", new Vector2(280, 100), new Vector2(475, 130), 76, TextAnchor.MiddleRight);
            total = Label("Total score value", panel, "48.00", new Vector2(350, 130), new Vector2(440, -115), 104, TextAnchor.MiddleRight);
            for (int i = 0; i < 3; i++) stars[i] = CareUi.Image("Star " + (i + 1), panel, art.star, new Vector2(300, 300), new Vector2((i - 1) * 350, -430));
            next = Label("Next star value", panel, "2.00", new Vector2(500, 100), new Vector2(300, -675), 68, TextAnchor.MiddleRight);
            buttons = new[] { Button("Continue", results, art.continueButton, new Vector2(850, 240), new Vector2(-180, -850), false),
                Button("Restart", results, art.restartButton, new Vector2(270, 270), new Vector2(490, -850), true) };
            Reset();
        }
        void ReplaceLabel(Transform parent, string text, float y)
        {
            // Sample clean panel artwork as a UI patch; keep the supplied bitmap unchanged.
            var patch = CareUi.Rect("Label patch", parent, new Vector2(850, 125), new Vector2(-125, y));
            var raw = patch.gameObject.AddComponent<RawImage>(); raw.texture = art.resultsPanel.texture;
            raw.uvRect = new Rect(.72f, (900 + y - 62.5f) / 1800f, .06f, 125f / 1800f); raw.raycastTarget = false;
            Label(text, parent, text, new Vector2(850, 115), new Vector2(-125, y), 66, TextAnchor.MiddleLeft);
        }
        static CareResultButton Button(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos, bool restart)
        {
            var image = CareUi.Image(name, parent, sprite, size, pos);
            var collider = image.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(size.x, size.y, 12); collider.isTrigger = true;
            var button = image.gameObject.AddComponent<CareResultButton>(); button.restart = restart; button.image = image; return button;
        }
        public void Update(CareRound round, float elapsed)
        {
            if (art.bananaAnchor)
            {
                bananaHud.position = art.bananaAnchor.position + art.bananaOffset;
                bananaHud.rotation = camera.transform.rotation;
            }
            score.text = round.Score.ToString("F2", CultureInfo.InvariantCulture); bananas.text = "×" + round.Bananas;
            int remaining = Mathf.CeilToInt(Mathf.Max(0, CareRound.Cutoff - elapsed));
            clock.text = round.Frozen ? "ROUND COMPLETE" : (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00");
            grade.sprite = round.Stars == 1 ? art.level1 : round.Stars == 2 ? art.level2 : round.Stars == 3 ? art.level3 : art.star;
            grade.color = round.Stars == 0 ? new Color(.55f, .55f, .55f, 1) : Color.white;
            grade.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-335, 335, (float)round.Score / 100), -25);
            zeroGrade.gameObject.SetActive(round.Stars == 0);
            if (!round.Frozen) return;
            feedings.text = round.FeedingTotal + "/9";
            resolved.text = round.ResolvedPercent.ToString("F1", CultureInfo.InvariantCulture) + "%";
            tasks.text = round.CompletedTotal + "/18"; total.text = round.Score.ToString("F2", CultureInfo.InvariantCulture);
            next.text = round.Stars == 3 ? "MAX" : round.NextStarPoints.ToString("F2", CultureInfo.InvariantCulture);
            for (int i = 0; i < 3; i++) stars[i].color = i < round.Stars ? Color.white : new Color(.38f, .38f, .38f, .65f);
            foreach (var b in buttons) { b.available = elapsed >= CareRound.Duration; b.image.color = b.available ? Color.white : new Color(.65f, .65f, .65f); }
        }
        public void ShowResults() { results.gameObject.SetActive(true); scoreHud.gameObject.SetActive(false); bananaHud.gameObject.SetActive(false); }
        public void ShowHud() { scoreHud.gameObject.SetActive(true); bananaHud.gameObject.SetActive(true); }
        public void Continue() { results.gameObject.SetActive(false); ShowHud(); }
        public void Reset() { results.gameObject.SetActive(false); scoreHud.gameObject.SetActive(false); bananaHud.gameObject.SetActive(false); }
    }
}
