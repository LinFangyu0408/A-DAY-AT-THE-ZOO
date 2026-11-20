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
        // Below three bananas the counter starts its red flash alarm.
        const int LowStockAlarm = 3;
        static readonly Color AlarmLit = new(.88f, .13f, .13f, 1f);
        static readonly Color AlarmDim = new(.88f, .13f, .13f, .22f);
        static readonly Color BananaInk = new(.14f, .14f, .18f, 1f);
        readonly CareRoundArtwork art;
        readonly Transform head;
        readonly RectTransform scoreHud, bananaHud;
        RectTransform results;
        RectTransform flatHud;
        // Which layout the plates were built for, so the session can rebuild
        // them when the headset connects (or is removed) mid-game.
        public bool BuiltForVr { get; private set; }
        // The HUD writes the same string most frames; legacy Text rebuilds its
        // mesh on every .text assignment, which was repainting the whole canvas
        // 60+ times a second (the main stutter source). Cache and only assign
        // on change.
        string scoreCache = "", bananaCache = "", clockCache = "", sprintCache = null;
        // Numeric shadows of the cached strings; they let Update skip building a
        // new string entirely when nothing changed.
        int percentCache = -1, bananaCountCache = -1, clockSecondCache = int.MinValue;
        int sprintSecondCache = int.MinValue;
        bool clockFrozenShown;
        readonly Image progressFill;
        readonly Image[] stars = new Image[3];
        Sprite resultStarSprite;
        readonly Text score, clock, bananas;
        Text sprint;
        Text feedingValue, penaltyValue, finalValue, passValue, starHint;
        CareResultButton[] buttons;
        RectTransform starBadge;
        float trackLeft, trackRight, trackCenterY;

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
            return Legible(text, fontSize);
        }

        // Shared readability pass. LoadReadable() now returns a genuine bold OS
        // face, so glyphs are already heavy; a single contrast outline keeps
        // them crisp on any panel. Best-fit scaling keeps long strings inside
        // their rect so text never spills out of frame.
        public static Text Legible(Text text, int maxFontSize)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(13, Mathf.RoundToInt(maxFontSize * .62f));
            text.resizeTextMaxSize = maxFontSize;
            if (!text.gameObject.GetComponent<Outline>())
            {
                Color c = text.color;
                float lum = c.r * .299f + c.g * .587f + c.b * .114f;
                var outline = text.gameObject.AddComponent<Outline>();
                outline.effectColor = lum < .55f
                    ? new Color(1f, 1f, 1f, .95f)      // dark text -> light edge
                    : new Color(.04f, .10f, .18f, .95f); // light text -> dark edge
                outline.effectDistance = new Vector2(2.5f, -2.5f);
                outline.useGraphicAlpha = true;
            }
            return text;
        }

        public CareRoundView(Transform head, CareRoundArtwork artwork)
        {
            art = artwork;
            this.head = head;
            BuiltForVr = !CarePlatform.FlatUi;

            // Kept well inside the headset's field of view: the old plate sat 21°
            // above the gaze line and the banana counter 33° to the right, so in
            // the headset both were parked on (or past) the edge of vision. On a
            // PC (no headset) the same art lives in a scaled 2D HUD root.
            if (CarePlatform.FlatUi)
            {
                flatHud = CareUi.OverlayRoot("Care PC HUD", 50);
                // Reference-video layout, slightly smaller per user request so
                // the plates never crowd the play area. The score plate is
                // top-anchored and pushed up 15px past the canvas top: only the
                // transparent margin of the plate reaches that far, while the
                // printed artwork and the live bar land exactly level with the
                // banana counter plate on the top-right (both plates read as
                // one top row).
                scoreHud = CareUi.Plate("Care SCORE HUD", flatHud,
                    new Vector2(60, 15), new Vector2(1000, 480),
                    new Vector2(.5f, 1f), new Vector2(.5f, 1f));
                scoreHud.localScale = Vector3.one * .72f;
                // One pad-glyph toggle for the whole flat HUD (created once per
                // plate set; DestroyPlates tears it down with the root).
                CarePadStyleToggle.Create(flatHud, new Vector2(-30, 28),
                    new Vector2(170, 56), 26);
            }
            else
                scoreHud = CareUi.Canvas("Care SCORE HUD", head,
                    new Vector3(0, .34f, 1.12f), .70f, new Vector2(1000, 480), 10);
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
                // The game-UI SCORE plate bakes the rainbow gradient. Cover it
                // with the panel's own cream tone so the live value can animate.
                CareUi.Image("Static score bar cover", scoreHud, null,
                    new Vector2(900, 102), new Vector2(2, -31)).color =
                    new Color(.922f, .925f, .902f, 1f);
            }
            else
            {
                // Fallback for projects that have not imported the game-UI pack.
                CareUi.Image("Localized progress cover", scoreHud, null,
                    new Vector2(900, 170), new Vector2(0, -24)).color =
                    new Color(.975f, .965f, .945f, 1f);
                CareUiSkin.AddSliced(scoreHud, "Progress title ribbon", CareUiSkin.Header,
                    new Vector2(420, 66), new Vector2(0, 72), Color.white);
                Label("Progress title", scoreHud, "SCORE", new Vector2(400, 58),
                    new Vector2(0, 72), 36, TextAnchor.MiddleCenter, Color.white);
            }

            // The baked rainbow bar occupies canvas rect x[-441,444] y[-77,14]
            // (measured from UI SCORE.png). Align the live track exactly to it
            // so the fill never overflows the printed artwork.
            Vector2 trackSize = new Vector2(886, 92);
            Vector2 trackPosition = new Vector2(2, -31);
            var track = CareUi.Rect("Progress track", scoreHud, trackSize, trackPosition);
            var trackBackground = track.gameObject.AddComponent<Image>();
            // Rounded cream empty track: matches the reference layout where the
            // unfilled bar reads as a soft rounded slot on the white plate.
            trackBackground.sprite = CareUiSkin.RoundedCard;
            trackBackground.type = Image.Type.Sliced;
            trackBackground.color = new Color(.84f, .82f, .78f, 1f);
            trackBackground.raycastTarget = false;
            var scoreFill = CareUiSkin.SvgScoreFill;
            progressFill = CareUi.Image("Progress fill", track,
                scoreFill ? scoreFill : CareUiSkin.ProgressFill,
                scoreFill ? new Vector2(872, 80) : new Vector2(682, 32), Vector2.zero);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0;
            progressFill.color = scoreFill ? Color.white : new Color(1f, .82f, .20f, 1f);
            // 60% passing line: a slim marker on the track so the player can
            // see exactly where the round starts to count as passed.
            var passMarker = CareUi.Rect("Progress pass marker", track,
                new Vector2(10, trackSize.y), Vector2.zero);
            var passMarkerImage = passMarker.gameObject.AddComponent<Image>();
            passMarkerImage.color = new Color(1f, .72f, .10f, .95f);
            passMarkerImage.raycastTarget = false;
            passMarker.anchoredPosition = new Vector2(
                -trackSize.x * .5f + trackSize.x * CareRound.PassingRatio, 0f);
            passMarker.SetAsLastSibling();
            // The star is the live progress marker: it slides along the track
            // with the fill, so it passes over (and momentarily covers) the
            // passing line at exactly six bananas. It starts pinned to the left
            // end of the track instead of floating in the middle.
            float passX = trackPosition.x - trackSize.x * .5f + trackSize.x * CareRound.PassingRatio;
            var scoreBadge = CareUiSkin.Add(scoreHud, "Score star badge", CareUiSkin.SvgStar,
                new Vector2(126, 123), new Vector2(passX, trackPosition.y), Color.white);
            scoreBadge.preserveAspect = true;
            scoreBadge.transform.SetAsLastSibling();
            starBadge = scoreBadge.rectTransform;
            trackLeft = trackPosition.x - trackSize.x * .5f;
            trackRight = trackPosition.x + trackSize.x * .5f;
            trackCenterY = trackPosition.y;
            MoveStar(0f);

            score = Label("Current score", scoreHud, "0%", new Vector2(300, 60),
                new Vector2(330, 60), 52, TextAnchor.MiddleRight,
                new Color(.33f, .20f, .30f, 1f));
            clock = Label("Round clock", scoreHud, "03:50", new Vector2(220, 50),
                new Vector2(0, -198), 44, TextAnchor.MiddleCenter, Color.white);
            // Final-sprint banner: names the exact goal instead of leaving the
            // player standing around for the last seconds.
            sprint = Label("Final sprint", scoreHud, string.Empty, new Vector2(720, 50),
                new Vector2(0, -248), 30, TextAnchor.MiddleCenter,
                new Color(1f, .80f, .24f, 1f));

            bananaHud = CarePlatform.FlatUi
                ? CareUi.Plate("Care BANANA HUD", flatHud,
                    new Vector2(-70, -50), new Vector2(560, 322),
                    new Vector2(1f, 1f), new Vector2(1f, 1f))
                : CareUi.Canvas("Care BANANA HUD",
                    art.bananaAnchor ? art.bananaAnchor : head,
                    art.bananaAnchor ? art.bananaOffset : new Vector3(.50f, .26f, 1.12f),
                    art.bananaWidth, new Vector2(560, 322), 10);
            if (CarePlatform.FlatUi) bananaHud.localScale = Vector3.one * .72f;
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
            // The banana slice bakes the fruit on the left; only the counter is live.
            bananas = Label("Banana count", bananaHud, "×10", new Vector2(250, 110),
                new Vector2(150, -20), 78, TextAnchor.MiddleCenter,
                new Color(.14f, .14f, .18f, 1f));

            // STAMINA lives above the hungry monkey's head (see MonkeyCareNpc /
            // CarePanel). A second plate floating in front of the player only
            // duplicated the value and blocked the view, so it is gone.

            BuildResults();
            Reset();
        }

        void BuildResults()
        {
            // Reference card: big-corner white rounded plate, blue LEVEL
            // COMPLETE ribbon, three airy stat rows, big total score, three
            // stars, next-star hint, CONTINUE pill plus a round green button.
            results = CarePlatform.FlatUi
                ? CareUi.Plate("Care RESULTS", flatHud, new Vector2(0, -10), new Vector2(900, 1300))
                : CareUi.Canvas("Care RESULTS", head,
                    new Vector3(0, -.02f, 1.30f), .50f, new Vector2(900, 1300), 100);
            if (CarePlatform.FlatUi) results.localScale = Vector3.one * .78f;
            // The shadow must share the card's rounded silhouette, otherwise a
            // square dark rim shows around the rounded card corners.
            var shadow = CareUiSkin.AddSliced(results, "Results shadow", CareUiSkin.RoundedCard,
                new Vector2(896, 1296), new Vector2(8, -14),
                new Color(.02f, .06f, .12f, .35f));
            shadow.transform.SetAsFirstSibling();
            var card = CareUiSkin.AddSliced(results, "Results card", CareUiSkin.RoundedCard,
                new Vector2(880, 1290), Vector2.zero, new Color(.985f, .99f, .99f, 1f));
            CareUiSkin.AddSliced(results, "Results ribbon", CareUiSkin.Header,
                new Vector2(780, 150), new Vector2(0, 505), Color.white);
            Label("Results title", results, "LEVEL COMPLETE", new Vector2(720, 100),
                new Vector2(0, 505), 56, TextAnchor.MiddleCenter, Color.white);

            feedingValue = StatRow("Feeding Amount", 345);
            passValue = StatRow("Resolved Status", 225);
            penaltyValue = StatRow("Facilities Added", 105);

            CareUi.Image("Results divider", results, null,
                new Vector2(780, 6), new Vector2(0, -25)).color =
                new Color(.72f, .74f, .75f, 1f);
            Label("Total Score title", results, "Total Score:", new Vector2(500, 110),
                new Vector2(-160, -130), 80, TextAnchor.MiddleLeft,
                new Color(.12f, .12f, .14f, 1f));
            finalValue = Label("Total Score value", results, "0", new Vector2(260, 110),
                new Vector2(255, -130), 80, TextAnchor.MiddleRight,
                new Color(.12f, .12f, .14f, 1f));

            resultStarSprite = CareUiSkin.SvgStar ? CareUiSkin.SvgStar :
                art.star ? art.star : CareUiSkin.Star;
            for (int i = 0; i < 3; i++)
            {
                stars[i] = CareUi.Image("Result star " + (i + 1), results,
                    resultStarSprite,
                    new Vector2(200, 196), new Vector2((i - 1) * 235, -310));
                stars[i].preserveAspect = true;
            }
            starHint = Label("Star thresholds", results, "Next Star: 1",
                new Vector2(500, 60), new Vector2(-130, -450), 48, TextAnchor.MiddleLeft,
                new Color(.24f, .25f, .25f, 1f));

            var continueArt = CareUiSkin.SvgContinue;
            var restartArt = CareUiSkin.SvgRestart;
            bool bakedContinue = continueArt != null;
            bool bakedRestart = restartArt != null;
            buttons = new[]
            {
                ResultButton("Continue exploring", "CONTINUE", results,
                    continueArt ? continueArt : CareUiSkin.BlueButton,
                    bakedContinue ? new Vector2(430, 134) : new Vector2(400, 126),
                    bakedContinue ? new Vector2(-155, -575) : new Vector2(-180, -575),
                    false, Color.white, !bakedContinue),
                ResultButton("Restart round", "RESTART", results,
                    restartArt ? restartArt : CareUiSkin.GreenButton,
                    bakedRestart ? new Vector2(140, 140) : new Vector2(320, 110),
                    bakedRestart ? new Vector2(215, -575) : new Vector2(230, -575),
                    true, Color.white, !bakedRestart)
            };
        }

        Text StatRow(string title, float y)
        {
            Label(title + " title", results, title, new Vector2(480, 88),
                new Vector2(-190, y), 64, TextAnchor.MiddleLeft,
                new Color(.16f, .16f, .18f, 1f));
            return Label(title + " value", results, "0", new Vector2(340, 88),
                new Vector2(215, y), 64, TextAnchor.MiddleRight,
                new Color(.16f, .16f, .18f, 1f));
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

        // The headset can connect (or be removed) after the round started: tear
        // the old plates down so the session can rebuild them for the other
        // layout instead of leaving a giant flat panel over the VR view.
        public void DestroyPlates()
        {
            foreach (var plate in new RectTransform[] { flatHud, scoreHud, bananaHud, results })
                if (plate) UnityEngine.Object.Destroy(plate.gameObject);
            flatHud = null; results = null;
        }

        public void Update(CareRound round, float elapsed)
        {
            // The banana plate only tracks the head/anchor in VR; the flat PC
            // overlay stays put at its screen corner.
            if (!CarePlatform.FlatUi && art.bananaAnchor)
            {
                if (art.bananaAnchor == head)
                {
                    bananaHud.localPosition = art.bananaOffset;
                    bananaHud.localRotation = Quaternion.identity;
                }
                else
                {
                    bananaHud.position = art.bananaAnchor.position + art.bananaOffset;
                    bananaHud.rotation = head.rotation;
                }
            }

            // Read the fill from the star goal instead of the task count so the
            // bar and the three-star rating always agree.
            float ratio = Mathf.Clamp01((float)round.FeedingRatio);
            progressFill.fillAmount = ratio;
            // Build the label strings only when the integer behind them moves:
            // "×" + n and n + "%" allocated two strings every frame, which fed
            // the GC a steady trickle of garbage for a value that changes a few
            // times per round.
            int percent = Mathf.RoundToInt(ratio * 100f);
            if (percent != percentCache)
            {
                percentCache = percent;
                scoreCache = percent + "%";
                score.text = scoreCache;
            }
            MoveStar(ratio);
            if (round.Bananas != bananaCountCache)
            {
                bananaCountCache = round.Bananas;
                bananaCache = "×" + round.Bananas;
                bananas.text = bananaCache;
            }
            // Banana stock alarm: with fewer than three left (0/1/2) the
            // counter turns red and blinks until the player restocks at the
            // market stand, which restores the count to ten and stops it.
            if (round.Bananas < LowStockAlarm)
            {
                // Visible flash: alternate between solid red and a dimmed red
                // phase at ~2.3 blinks per second (unscaled clock, so the
                // alarm keeps flashing even when the round clock matters).
                bool lit = Mathf.Repeat(Time.unscaledTime * 2.3f, 1f) < .62f;
                bananas.color = lit ? AlarmLit : AlarmDim;
            }
            else
                bananas.color = BananaInk;

            int remaining = Mathf.CeilToInt(Mathf.Max(0, CareRound.Cutoff - elapsed));
            bool sprinting = !round.Frozen && remaining > 0 &&
                             remaining <= MonkeyCareSession.FinalSprintSeconds;
            if (round.Frozen)
            {
                if (!clockFrozenShown)
                {
                    clockFrozenShown = true;
                    Set(clock, ref clockCache, "ROUND END");
                }
            }
            else
            {
                clockFrozenShown = false;
                if (remaining != clockSecondCache)
                {
                    clockSecondCache = remaining;
                    Set(clock, ref clockCache, (remaining / 60).ToString("00") + ":" +
                                                (remaining % 60).ToString("00"));
                }
            }
            clock.color = sprinting ? new Color(1f, .70f, .20f, 1f) : Color.white;
            if (!round.Frozen)
            {
                // The sprint banner is rebuilt only when its second tick changes.
                if (sprinting)
                {
                    if (remaining != sprintSecondCache)
                    {
                        sprintSecondCache = remaining;
                        Set(sprint, ref sprintCache, SprintCopy(round, remaining));
                    }
                }
                else if (sprintSecondCache != -1)
                {
                    sprintSecondCache = -1;
                    Set(sprint, ref sprintCache, string.Empty);
                }
                return;
            }
            sprintSecondCache = -1;
            Set(sprint, ref sprintCache, string.Empty);

            // Report against the three-star goal so "9 / 9" always reads as a
            // completed round; banana twelve is bonus, not a missing task.
            feedingValue.text = Mathf.Min(round.FeedingTotal, CareRound.ScoreGoal) + " / " +
                                CareRound.ScoreGoal;
            int resolved = Mathf.RoundToInt(100f * (float)round.FeedingRatio);
            passValue.text = resolved.ToString("0") + "%";
            penaltyValue.text = "0";
            finalValue.text = Mathf.RoundToInt((float)round.Score).ToString("N0");
            for (int i = 0; i < stars.Length; i++)
            {
                bool earned = i < round.Stars;
                stars[i].color = earned ? Color.white : new Color(.84f, .87f, .87f, .55f);
            }
            starHint.text = round.Stars >= CareRound.MaxStars
                ? "PERFECT! ALL STARS EARNED"
                : "Next Star: " + round.NextStarFeedings + " MORE";
            foreach (var button in buttons)
            {
                button.available = true;
                button.image.color = Color.white;
            }
        }

        // Slide the star badge along the track with the fill. Small margin at
        // both ends keeps the whole glyph inside the printed plate.
        // Only touch legacy Text when the string actually changed: assigning
        // .text unconditionally dirtied the whole canvas every frame.
        static void Set(Text label, ref string cache, string value)
        {
            if (!label) return;
            if (cache == value) return;
            cache = value;
            label.text = value;
        }

        void MoveStar(float ratio)
        {
            if (!starBadge) return;
            const float margin = 60f;
            float x = Mathf.Lerp(trackLeft, trackRight, Mathf.Clamp01(ratio));
            x = Mathf.Clamp(x, trackLeft + margin, trackRight - margin);
            starBadge.anchoredPosition = new Vector2(x, trackCenterY);
        }

        // Last-twenty-seconds copy: state the exact remaining goal so the player
        // always knows what is still worth doing.
        static string SprintCopy(CareRound round, int remaining)
        {
            if (round.Stars >= CareRound.MaxStars) return "FINAL " + remaining + "s · ALL STARS LOCKED IN";
            int need = Mathf.Max(1, round.NextStarFeedings);
            int target = Mathf.Min(CareRound.MaxStars, round.Stars + 1);
            return "FINAL " + remaining + "s · " + need + " MORE FOR " + target +
                   (target == 1 ? " STAR" : " STARS");
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
