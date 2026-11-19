using System;
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
        readonly RectTransform scoreHud, bananaHud;
        RectTransform results;
        readonly Image progressFill;
        readonly Image[] stars = new Image[3];
        Image grade;
        Sprite resultStarSprite, resultStarOutline;
        readonly Text progress, score, clock, bananas;
        Text feedingValue, penaltyValue, finalValue, passValue, starHint;
        CareResultButton[] buttons;

        public static Text Label(string name, Transform parent, string value, Vector2 size,
            Vector2 pos, int fontSize, TextAnchor align = TextAnchor.MiddleCenter,
            Color? color = null)
        {
            var rect = CareUi.Rect(name, parent, size, pos);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = CareFont.LoadReadable();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color ?? new Color(.25f, .16f, .22f);
            text.alignment = align;
            text.raycastTarget = false;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public CareRoundView(Camera camera, CareRoundArtwork artwork)
        {
            art = artwork;
            this.camera = camera;

            scoreHud = CareUi.Canvas("Care SCORE HUD", camera.transform,
                new Vector3(0, .43f, 1.18f), .80f, new Vector2(1000, 320));
            var svgScore = CareUiSkin.SvgScorePanel;
            if (svgScore)
            {
                var scoreArt = CareUi.Image("SVG score panel", scoreHud, svgScore,
                    new Vector2(1000, 323), Vector2.zero);
                scoreArt.preserveAspect = true;
            }
            else if (art.scorePanel)
            {
                var originalScore = CareUi.Image("Original GitHub score panel", scoreHud,
                    art.scorePanel, new Vector2(1000, 323), Vector2.zero);
                originalScore.color = Color.white;
                originalScore.preserveAspect = true;
            }
            else
            {
                CareUiSkin.AddSliced(scoreHud, "Progress shadow", CareUiSkin.Panel,
                    new Vector2(1000, 300), new Vector2(5, -9), new Color(.02f, .08f, .14f, .32f));
                CareUiSkin.AddSliced(scoreHud, "Progress panel", CareUiSkin.LightPanel,
                    new Vector2(990, 290), Vector2.zero, Color.white);
            }
            if (svgScore)
            {
                // The SVG score plate already contains the SCORE heading. Cover
                // only its static gradient so the live value can animate.
                CareUi.Image("Static score bar cover", scoreHud, null,
                    new Vector2(930, 126), new Vector2(0, -31)).color =
                    new Color(.975f, .972f, .968f, 1f);
            }
            else
            {
                // Fallback for projects that have not imported the SVG pack.
                CareUi.Image("Localized progress cover", scoreHud, null,
                    new Vector2(900, 170), new Vector2(0, -24)).color =
                    new Color(.975f, .965f, .945f, 1f);
                CareUiSkin.AddSliced(scoreHud, "Progress title ribbon", CareUiSkin.Header,
                    new Vector2(420, 66), new Vector2(0, 72), Color.white);
                Label("Progress title", scoreHud, "SCORE", new Vector2(400, 58),
                    new Vector2(0, 72), 36, TextAnchor.MiddleCenter, Color.white)
                    .gameObject.AddComponent<Outline>().effectColor =
                    new Color(.04f, .16f, .28f, .9f);
            }

            Vector2 trackSize = svgScore ? new Vector2(850, 88) : new Vector2(700, 50);
            Vector2 trackPosition = svgScore ? new Vector2(-55, -31) : new Vector2(-55, -8);
            var track = CareUi.Rect("Progress track", scoreHud, trackSize, trackPosition);
            var trackBackground = track.gameObject.AddComponent<Image>();
            var scoreFill = CareUiSkin.SvgScoreFill;
            trackBackground.sprite = scoreFill ? scoreFill : CareUiSkin.ProgressTrack;
            trackBackground.type = Image.Type.Sliced;
            trackBackground.type = scoreFill ? Image.Type.Simple : Image.Type.Sliced;
            // The supplied score SVG is a full coloured bar. Tint a second copy
            // down to a soft neutral track so 0 / 10 still reads as an empty
            // progress bar, then draw the live coloured fill above it.
            trackBackground.color = scoreFill
                ? new Color(.46f, .48f, .53f, .82f)
                : new Color(.58f, .62f, .66f, .58f);
            trackBackground.enabled = true;
            trackBackground.raycastTarget = false;
            progressFill = CareUi.Image("Progress fill", track,
                scoreFill ? scoreFill : CareUiSkin.ProgressFill,
                scoreFill ? new Vector2(836, 82) : new Vector2(682, 32), Vector2.zero);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0;
            progressFill.color = scoreFill ? Color.white : new Color(1f, .82f, .20f, 1f);
            progress = Label("Progress value", scoreHud, "0 / 10", new Vector2(170, 52),
                new Vector2(430, trackPosition.y), 50, TextAnchor.MiddleLeft,
                new Color(.18f, .18f, .26f, 1f));

            score = Label("Current score", scoreHud, "TOTAL SCORE: 0", new Vector2(390, 46),
                new Vector2(-225, -112), 44, TextAnchor.MiddleLeft,
                new Color(.27f, .22f, .31f, 1f));
            clock = Label("Round clock", scoreHud, "03:50", new Vector2(180, 46),
                new Vector2(350, -112), 42, TextAnchor.MiddleRight,
                new Color(.38f, .19f, .16f, 1f));

            bananaHud = CareUi.Canvas("Care BANANA HUD",
                art.bananaAnchor ? art.bananaAnchor : camera.transform,
                art.bananaAnchor ? art.bananaOffset : new Vector3(.82f, .46f, 1.28f),
                art.bananaWidth, new Vector2(560, 322));
            var bananaArt = CareUiSkin.SvgBananaPanel ? CareUiSkin.SvgBananaPanel : art.bananaPanel;
            if (bananaArt)
            {
                var originalBanana = CareUi.Image("Original GitHub banana panel", bananaHud,
                    bananaArt, new Vector2(545, 313), Vector2.zero);
                originalBanana.color = Color.white;
                originalBanana.preserveAspect = true;
            }
            else
                CareUiSkin.AddSliced(bananaHud, "Banana HUD panel", CareUiSkin.LightPanel,
                    new Vector2(545, 313), Vector2.zero, Color.white);
            Label("Banana title", bananaHud, "BANANA", new Vector2(230, 70),
                new Vector2(165, 56), 42, TextAnchor.MiddleCenter,
                new Color(.20f, .20f, .25f, 1f));
            bananas = Label("Banana count", bananaHud, "×10", new Vector2(220, 115),
                new Vector2(170, -45), 72, TextAnchor.MiddleCenter,
                new Color(.91f, .34f, .09f, 1f));

            BuildResults();
            Reset();
        }

        void BuildResults()
        {
            results = CareUi.Canvas("Care RESULTS", camera.transform,
                new Vector3(0, -.02f, 1.18f), .98f, new Vector2(1420, 1720));
            var svgResults = CareUiSkin.SvgResultsPanel;
            if (svgResults)
            {
                var resultsArt = CareUi.Image("SVG results panel", results, svgResults,
                    new Vector2(1368, 1779), Vector2.zero);
                resultsArt.preserveAspect = true;
            }
            else if (art.resultsPanel)
            {
                var originalResults = CareUi.Image("Original GitHub results panel", results,
                    art.resultsPanel, new Vector2(1368, 1800), Vector2.zero);
                originalResults.color = new Color(.96f, .97f, .97f, 1f);
                originalResults.preserveAspect = true;
            }
            else
            {
                var shadow = CareUiSkin.AddSliced(results, "Results shadow", CareUiSkin.Panel,
                    new Vector2(1368, 1690), new Vector2(8, -14),
                    new Color(.02f, .06f, .12f, .40f));
                shadow.transform.SetAsFirstSibling();
                CareUiSkin.AddSliced(results, "Results panel", CareUiSkin.LightPanel,
                    new Vector2(1360, 1680), Vector2.zero, Color.white);
            }
            if (!svgResults)
            {
                CareUiSkin.AddSliced(results, "Results content plate", CareUiSkin.LightPanel,
                    new Vector2(1340, 1280), new Vector2(0, -130),
                    new Color(1f, .995f, .97f, 1f));
                CareUiSkin.AddSliced(results, "Results header", CareUiSkin.Header,
                    new Vector2(1090, 164), new Vector2(0, 650), Color.white);
                Label("Results title", results, "LEVEL COMPLETE", new Vector2(880, 105),
                    new Vector2(0, 650), 58, TextAnchor.MiddleCenter,
                    Color.white).gameObject.AddComponent<Outline>().effectColor =
                    new Color(.04f, .18f, .34f, .92f);
                Row("Successful feedings", "Feeding Amount", 315, out feedingValue);
                Row("Patience penalties", "Patience Penalty", 180, out penaltyValue);
                Row("Final score", "Total Score", 45, out finalValue);
                passValue = Label("Pass result", results, "PASSED", new Vector2(1060, 88),
                    new Vector2(0, -72), 54, TextAnchor.MiddleCenter,
                    new Color(.04f, .45f, .25f, 1f));
            }
            else
            {
                const float rowX = -112;
                const float valueX = 150;
                Label("Feeding Amount title", results, "Feeding Amount",
                    new Vector2(560, 92), new Vector2(rowX, 372), 66,
                    TextAnchor.MiddleLeft, new Color(.20f, .20f, .20f, 1f));
                Label("Resolved Status title", results, "Resolved Status",
                    new Vector2(560, 92), new Vector2(rowX, 228), 66,
                    TextAnchor.MiddleLeft, new Color(.20f, .20f, .20f, 1f));
                Label("Patience Penalty title", results, "Patience Penalty",
                    new Vector2(560, 92), new Vector2(rowX, 92), 66,
                    TextAnchor.MiddleLeft, new Color(.20f, .20f, .20f, 1f));
                Label("Total Score title", results, "Total Score",
                    new Vector2(560, 116), new Vector2(rowX, -108), 72,
                    TextAnchor.MiddleLeft, new Color(.18f, .18f, .18f, 1f));
                CareUi.Image("Results divider", results, null,
                    new Vector2(1050, 8), new Vector2(0, -5)).color =
                    new Color(.56f, .58f, .57f, 1f);

                feedingValue = Label("Feeding Amount value", results, "0 / 10",
                    new Vector2(470, 96), new Vector2(valueX, 372), 64,
                    TextAnchor.MiddleRight, new Color(.20f, .20f, .20f, 1f));
                passValue = Label("Pass result", results, "PASSED",
                    new Vector2(470, 96), new Vector2(valueX, 228), 64,
                    TextAnchor.MiddleRight, new Color(.04f, .45f, .25f, 1f));
                penaltyValue = Label("Patience Penalty value", results, "0",
                    new Vector2(470, 96), new Vector2(valueX, 92), 64,
                    TextAnchor.MiddleRight, new Color(.20f, .20f, .20f, 1f));
                finalValue = Label("Total Score value", results, "0 / 10",
                    new Vector2(470, 116), new Vector2(valueX, -108), 72,
                    TextAnchor.MiddleRight, new Color(.18f, .18f, .18f, 1f));
            }

            resultStarSprite = CareUiSkin.SvgStar ? CareUiSkin.SvgStar :
                art.star ? art.star : CareUiSkin.Star;
            resultStarOutline = CareUiSkin.StarOutline;
            grade = CareUi.Image("Grade badge", results, resultStarSprite,
                new Vector2(146, 138), new Vector2(420, -76));
            grade.preserveAspect = true;
            grade.gameObject.SetActive(!svgResults);
            if (!svgResults)
                Label("Star heading", results, "STAR RATING", new Vector2(460, 56),
                    new Vector2(0, -171), 32, TextAnchor.MiddleCenter,
                    new Color(.12f, .29f, .40f, 1f));
            for (int i = 0; i < 3; i++)
            {
                stars[i] = CareUi.Image("Result star " + (i + 1), results,
                    resultStarSprite,
                    new Vector2(178, 174), new Vector2((i - 1) * 220, -318));
                stars[i].preserveAspect = true;
            }
            starHint = Label("Star thresholds", results,
                "NEXT STAR: 1 POINT",
                svgResults ? new Vector2(600, 50) : new Vector2(900, 50),
                svgResults ? new Vector2(100, -605) : new Vector2(0, -395),
                52, svgResults ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                new Color(.24f, .25f, .25f, 1f));

            var continueArt = CareUiSkin.SvgContinue;
            var restartArt = CareUiSkin.SvgRestart;
            bool bakedContinue = continueArt != null;
            bool bakedRestart = restartArt != null;
            buttons = new[]
            {
                ResultButton("Continue exploring", "CONTINUE", results,
                    continueArt ? continueArt : CareUiSkin.BlueButton,
                    bakedContinue ? new Vector2(470, 147) : new Vector2(400, 126),
                    bakedContinue ? new Vector2(-185, -722) : new Vector2(-220, -545),
                    false, Color.white, !bakedContinue),
                ResultButton("Restart round", "RESTART", results,
                    restartArt ? restartArt : CareUiSkin.GreenButton,
                    bakedRestart ? new Vector2(142, 143) : new Vector2(400, 126),
                    bakedRestart ? new Vector2(270, -720) : new Vector2(220, -545),
                    true, Color.white, !bakedRestart)
            };
        }

        void Row(string name, string title, float y, out Text value)
        {
            Label(name + " title", results, title, new Vector2(520, 90),
                new Vector2(-350, y), 44, TextAnchor.MiddleLeft,
                new Color(.11f, .25f, .34f, 1f));
            var divider = CareUi.Image(name + " divider", results, CareUiSkin.Divider,
                new Vector2(1050, 12), new Vector2(0, y - 63));
            divider.type = Image.Type.Sliced;
            divider.color = new Color(.56f, .64f, .70f, .62f);
            value = Label(name + " value", results, "0", new Vector2(470, 90),
                new Vector2(335, y), 47, TextAnchor.MiddleRight,
                new Color(.02f, .35f, .48f, 1f));
        }

        static CareResultButton ResultButton(string name, string caption, Transform parent,
            Sprite sprite, Vector2 size, Vector2 pos, bool restart, Color tint,
            bool showCaption = true)
        {
            var image = CareUi.Image(name, parent, sprite, size, pos);
            image.color = tint;
            var collider = image.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 14);
            collider.isTrigger = true;
            if (showCaption)
                Label(name + " caption", image.transform, caption, new Vector2(size.x - 45, 88),
                    Vector2.zero, 38, TextAnchor.MiddleCenter, Color.white);
            var button = image.gameObject.AddComponent<CareResultButton>();
            button.restart = restart;
            button.image = image;
            return button;
        }

        public void Update(CareRound round, float elapsed)
        {
            if (art.bananaAnchor)
            {
                if (art.bananaAnchor == camera.transform)
                {
                    bananaHud.localPosition = art.bananaOffset;
                    bananaHud.localRotation = Quaternion.identity;
                }
                else
                {
                    bananaHud.position = art.bananaAnchor.position + art.bananaOffset;
                    bananaHud.rotation = camera.transform.rotation;
                }
            }

            float ratio = Mathf.Clamp01((float)round.FeedingTotal / CareRound.PlannedFeedings);
            progressFill.fillAmount = ratio;
            progress.text = round.FeedingTotal + " / " + CareRound.PlannedFeedings;
            score.text = "TOTAL SCORE: " + Mathf.RoundToInt((float)round.Score);
            bananas.text = "×" + round.Bananas;

            int remaining = Mathf.CeilToInt(Mathf.Max(0, CareRound.Cutoff - elapsed));
            clock.text = round.Frozen
                ? "ROUND END"
                : (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00");
            if (!round.Frozen) return;

            feedingValue.text = round.FeedingTotal + " / " + CareRound.PlannedFeedings;
            penaltyValue.text = round.PatiencePenalties > 0
                ? "-" + round.PatiencePenalties
                : "0";
            finalValue.text = Mathf.RoundToInt((float)round.Score) + " / " + CareRound.MaxScore;
            passValue.text = round.IsPassing ? "PASSED" : "FAILED";
            passValue.color = round.IsPassing
                ? new Color(.04f, .45f, .25f, 1f)
                : new Color(.66f, .14f, .10f, 1f);
            for (int i = 0; i < stars.Length; i++)
            {
                bool earned = i < round.Stars;
                stars[i].sprite = CareUiSkin.SvgStar ? resultStarSprite :
                    earned ? resultStarSprite : resultStarOutline;
                stars[i].color = earned ? Color.white : new Color(.84f, .87f, .87f, .58f);
            }
            var level1 = CareUiSkin.SvgLevel1 ? CareUiSkin.SvgLevel1 : art.level1;
            var level2 = CareUiSkin.SvgLevel2 ? CareUiSkin.SvgLevel2 : art.level2;
            var level3 = CareUiSkin.SvgLevel3 ? CareUiSkin.SvgLevel3 : art.level3;
            var gradeArt = round.Stars == 3 ? level3 : round.Stars == 2 ? level2 :
                round.Stars == 1 ? level1 : null;
            grade.sprite = gradeArt ? gradeArt : CareUiSkin.Star;
            grade.color = round.Stars > 0 ? Color.white : new Color(.62f, .66f, .66f, .58f);
            starHint.text = round.Stars == 3
                ? "PERFECT! THREE STARS"
                : "NEXT STAR: " + round.NextStarPoints.ToString("0") +
                  (Math.Abs(round.NextStarPoints - 1) < .001 ? " POINT" : " POINTS");
            foreach (var button in buttons)
            {
                button.available = true;
                button.image.color = Color.white;
            }
        }

        public void ShowResults()
        {
            results.gameObject.SetActive(true);
            scoreHud.gameObject.SetActive(false);
            bananaHud.gameObject.SetActive(false);
        }

        public void ShowHud()
        {
            scoreHud.gameObject.SetActive(true);
            bananaHud.gameObject.SetActive(true);
        }

        public void Continue()
        {
            results.gameObject.SetActive(false);
            ShowHud();
        }

        public void Reset()
        {
            results.gameObject.SetActive(false);
            scoreHud.gameObject.SetActive(false);
            bananaHud.gameObject.SetActive(false);
        }
    }
}
