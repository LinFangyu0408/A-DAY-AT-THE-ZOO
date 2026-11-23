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
        Sprite resultStarSprite, resultEmptyStarSprite;
        readonly Text score, clock, bananas;
        Text sprint;
        Text feedingValue, penaltyValue, finalValue, passValue, starHint;
        CareResultButton[] buttons;
        CareProgressStarFollower starFollower;

        public static Text Label(string name, Transform parent, string value, Vector2 size,
            Vector2 pos, int fontSize, TextAnchor align = TextAnchor.MiddleCenter,
            Color? color = null)
        {
            var rect = CareUi.Rect(name, parent, size, pos);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = CareFont.LoadReadable();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
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
                    ? new Color(1f, 1f, 1f, .32f)      // dark text -> light edge
                    : new Color(.04f, .10f, .18f, .38f); // light text -> dark edge
                outline.effectDistance = new Vector2(.65f, -.65f);
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
                // The style chip is a UI Button; without an EventSystem a mouse
                // click never reaches it (only the pad's Select worked).
                CareUi.EnsureEventSystem();
                // Reference-video layout, slightly smaller per user request so
                // the plates never crowd the play area. The score plate is
                // top-anchored and pushed up 15px past the canvas top: only the
                // transparent margin of the plate reaches that far, while the
                // printed artwork and the live bar land exactly level with the
                // banana counter plate on the top-right (both plates read as
                // one top row).
                scoreHud = CareUi.Plate("Care SCORE HUD", flatHud,
                    new Vector2(0, -40), new Vector2(1000, 480),
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
            var scoreArt = CareUi.Image("SVG score panel", scoreHud, CareUiSkin.Art("panel_score"),
                new Vector2(1000, 347), Vector2.zero);
            scoreArt.preserveAspect = true;
            Label("Progress title", scoreHud, "SCORE", new Vector2(270, 70), new Vector2(0, 91),
                58, TextAnchor.MiddleCenter, new Color(.47f, .25f, .12f));

            // The baked rainbow bar occupies canvas rect x[-441,444] y[-77,14]
            // (measured from UI SCORE.png). Align the live track exactly to it
            // so the fill never overflows the printed artwork.
            Vector2 trackSize = new Vector2(886, 92);
            Vector2 trackPosition = new Vector2(2, -31);
            var track = CareUi.Rect("Progress track", scoreHud, trackSize, trackPosition);
            var trackBackground = track.gameObject.AddComponent<Image>();
            // Rounded cream empty track: matches the reference layout where the
            // unfilled bar reads as a soft rounded slot on the white plate.
            trackBackground.sprite = CareUiSkin.Art("progress_track");
            trackBackground.type = Image.Type.Simple;
            trackBackground.color = Color.white;
            trackBackground.raycastTarget = false;
            var scoreFill = CareUiSkin.Art("progress_rainbow");
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
            var scoreBadge = CareUiSkin.Add(scoreHud, "Score star badge", CareUiSkin.Art("star_score"),
                new Vector2(126, 123), new Vector2(passX, trackPosition.y), Color.white);
            scoreBadge.preserveAspect = true;
            scoreBadge.transform.SetAsLastSibling();
            starFollower = scoreBadge.gameObject.AddComponent<CareProgressStarFollower>();
            starFollower.progressFill = progressFill;
            MoveStar();

            score = Label("Current score", scoreHud, "0%", new Vector2(300, 60),
                new Vector2(320, 84), 48, TextAnchor.MiddleRight,
                new Color(.33f, .20f, .30f, 1f));
            CareUi.Image("Round clock plate", scoreHud, CareUiSkin.Art("panel_counter"),
                new Vector2(240, 76), new Vector2(0, -198));
            clock = Label("Round clock", scoreHud, "03:50", new Vector2(220, 50),
                new Vector2(0, -198), 40, TextAnchor.MiddleCenter, new Color(.34f, .20f, .10f));
            // Final-sprint banner: names the exact goal instead of leaving the
            // player standing around for the last seconds.
            sprint = Label("Final sprint", scoreHud, string.Empty, new Vector2(720, 50),
                new Vector2(0, -248), 30, TextAnchor.MiddleCenter,
                new Color(1f, .80f, .24f, 1f));

            bananaHud = CarePlatform.FlatUi
                ? CareUi.Plate("Care BANANA HUD", flatHud,
                    new Vector2(-230, -50), new Vector2(560, 322),
                    new Vector2(1f, 1f), new Vector2(1f, 1f))
                : CareUi.Canvas("Care BANANA HUD",
                    art.bananaAnchor ? art.bananaAnchor : head,
                    art.bananaAnchor ? art.bananaOffset : new Vector3(.43f, .26f, 1.12f),
                    art.bananaWidth, new Vector2(560, 322), 10);
            if (CarePlatform.FlatUi) bananaHud.localScale = Vector3.one * .72f;
            CareUi.Image("Banana HUD panel", bananaHud, CareUiSkin.Art("panel_counter"),
                new Vector2(545, 284), Vector2.zero).preserveAspect = true;
            CareUi.Image("Banana HUD icon", bananaHud, CareUiSkin.Art("icon_banana"),
                new Vector2(255, 255), new Vector2(-123, 24)).preserveAspect = true;
            bananas = Label("Banana count", bananaHud, "×10", new Vector2(250, 110),
                new Vector2(125, 0), 78, TextAnchor.MiddleCenter,
                new Color(.14f, .14f, .18f, 1f));

            // STAMINA lives above the hungry monkey's head (see MonkeyCareNpc /
            // CarePanel). A second plate floating in front of the player only
            // duplicated the value and blocked the view, so it is gone.

            BuildResults();
            Reset();
        }

        void BuildResults()
        {
            // Rebuild the reference from blank layers so all results stay live.
            results = CarePlatform.FlatUi
                ? CareUi.Plate("Care RESULTS", flatHud, new Vector2(0, -10), new Vector2(960, 1100))
                : CareUi.Canvas("Care RESULTS", head,
                    new Vector3(0, -.02f, 1.30f), .50f, new Vector2(960, 1100), 100);
            if (CarePlatform.FlatUi) results.localScale = Vector3.one * 1.10f;
            var shadow = CareUiSkin.AddSliced(results, "Results shadow", CareUiSkin.ResultsReferencePanel,
                new Vector2(900, 900), new Vector2(6, -10), new Color(.08f, .12f, .18f, .20f));
            shadow.pixelsPerUnitMultiplier = 2f;
            shadow.transform.SetAsFirstSibling();
            var card = CareUiSkin.AddSliced(results, "Results card", CareUiSkin.ResultsReferencePanel,
                new Vector2(900, 900), Vector2.zero, Color.white);
            card.pixelsPerUnitMultiplier = 2f;
            CareUiSkin.Add(results, "Results ribbon", CareUiSkin.ResultsReferenceRibbon,
                new Vector2(780, 160), new Vector2(0, 440), Color.white);
            ResultsLabel("Results title", results, "LEVEL COMPLETE", new Vector2(650, 90),
                new Vector2(-20, 440), 56, TextAnchor.MiddleCenter, Color.white, true);

            CareUiSkin.Add(results, "Results title star", CareUiSkin.ResultsReferenceStar,
                new Vector2(125, 125), new Vector2(360, 430), Color.white);
            CareUiSkin.Add(results, "Results statistics inset", CareUiSkin.ResultsReferenceStats,
                new Vector2(740, 250), new Vector2(0, 230), Color.white);
            CareUiSkin.Add(results, "Results score inset", CareUiSkin.ResultsReferenceScore,
                new Vector2(740, 100), new Vector2(0, 10), Color.white);
            feedingValue = StatRow("Feeding Amount", 295);
            passValue = StatRow("Resolved Status", 225);
            penaltyValue = StatRow("Facilities Added", 155);
            ResultsLabel("Total Score title", results, "Total Score:", new Vector2(470, 95),
                new Vector2(-105, 10), 70, TextAnchor.MiddleLeft, new Color(.43f, .24f, .12f), true);
            finalValue = ResultsLabel("Total Score value", results, "0", new Vector2(210, 95),
                new Vector2(255, 10), 70, TextAnchor.MiddleRight, new Color(.43f, .24f, .12f), true);
            resultStarSprite = CareUiSkin.ResultsReferenceStar;
            resultEmptyStarSprite = CareUiSkin.ResultsReferenceEmptyStar;
            for (int i = 0; i < 3; i++)
            {
                stars[i] = CareUi.Image("Result star " + (i + 1), results, resultStarSprite,
                    new Vector2(220, 220), new Vector2((i - 1) * 240, -155));
                stars[i].preserveAspect = true;
            }
            starHint = ResultsLabel("Star thresholds", results, "Next Star: 1",
                new Vector2(750, 60), new Vector2(0, -290), 40, TextAnchor.MiddleLeft,
                new Color(.43f, .24f, .12f));

            // Keep both existing actions, including mouse and VR ray activation.
            buttons = new[]
            {
                ResultButton("Continue exploring", "CONTINUE", CareInput.ConfirmLabel, results,
                    CareUiSkin.ResultsReferenceContinue, new Vector2(420, 112),
                    new Vector2(0, -380), false, Color.white),
                ResultButton("Restart round", "RESTART", CareInput.BackLabel, results,
                    CareUiSkin.ResultsReferenceRestart, new Vector2(210, 76),
                    new Vector2(0, -510), true, Color.white)
            };
            buttons[1].gameObject.SetActive(false);
            CareUiCustomization.Bind("Score", scoreHud);
            CareUiCustomization.Bind("Banana counter", bananaHud);
            CareUiCustomization.Bind("Results", results);
        }

        // UI text uses scalable font glyphs, with no lettering baked into textures.
        static Text ResultsLabel(string name, Transform parent, string value, Vector2 size,
            Vector2 pos, int fontSize, TextAnchor align, Color color, bool bold = false)
        {
            var label = Label(name, parent, value, size, pos, fontSize, align, color);
            label.font = CareFont.LoadReadable();
            label.fontStyle = FontStyle.Normal;
            var outline = label.GetComponent<Outline>();
            if (outline) outline.enabled = false;
            return label;
        }

        Text StatRow(string title, float y)
        {
            ResultsLabel(title + " title", results, title, new Vector2(480, 70),
                new Vector2(-80, y), 48, TextAnchor.MiddleLeft, new Color(.43f, .24f, .12f));
            return ResultsLabel(title + " value", results, "0", new Vector2(200, 70),
                new Vector2(260, y), 52, TextAnchor.MiddleRight, new Color(.43f, .24f, .12f), true);
        }

        static CareResultButton ResultButton(string name, string caption, string key,
            Transform parent, Sprite sprite, Vector2 size, Vector2 pos, bool restart, Color tint)
        {
            var image = CareUi.Image(name, parent, sprite, size, pos);
            image.color = tint;
            // A pill sprite has to be sliced or its rounded caps stretch into a
            // blurry lozenge at this size.
            if (sprite && sprite.border != Vector4.zero)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 2f;
            }
            var collider = image.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, 14);
            collider.isTrigger = true;
            // Both actions show the button that triggers them (A/✕ = continue,
            // B/○ = restart) so the pad player never has to aim at the card.
            var badge = CareUiSkin.KeyBadge(image.transform, key, new Vector2(restart ? 42 : 62, restart ? 42 : 62),
                new Vector2(-size.x * .5f + (restart ? 32 : 54), 0), out var badgeLabel, restart ? 29 : 40, name + " key");
            badge.transform.SetAsLastSibling();
            if (badgeLabel) badgeLabel.color = Color.white;
            ResultsLabel(name + " caption", image.transform, caption,
                new Vector2(size.x - (restart ? 72 : 118), size.y - 22), new Vector2(restart ? 20 : 24, 0), restart ? 31 : 46,
                TextAnchor.MiddleCenter, Color.white, true);
            var button = image.gameObject.AddComponent<CareResultButton>();
            button.restart = restart;
            button.image = image;
            // The PC card is a screen-space overlay, so a plain mouse click must
            // work as well as aiming the look ray at it.
            if (CarePlatform.FlatUi)
            {
                CareUi.EnsureEventSystem();
                image.raycastTarget = true;
                var click = image.gameObject.AddComponent<Button>();
                click.targetGraphic = image;
                click.transition = Selectable.Transition.None;
                click.onClick.AddListener(button.Activate);
            }
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
            MoveStar();
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
            // Amber only for the final sprint; the rest of the round is white
            // like the digits themselves.
            clock.color = sprinting ? new Color(.88f, .25f, .08f, 1f) : new Color(.34f, .20f, .10f);
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
                stars[i].sprite = earned ? resultStarSprite : resultEmptyStarSprite;
                stars[i].color = Color.white;
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

        void MoveStar()
        {
            if (starFollower) starFollower.Follow();
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




