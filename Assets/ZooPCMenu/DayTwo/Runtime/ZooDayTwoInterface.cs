using UnityEngine;
using UnityEngine.UI;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC
{
    public sealed partial class ZooDayTwoSession
    {
        RectTransform hud, briefing, scoreFill, supplyLabel;
        Transform interfaceRoot;
        Text clockText, scoreText, paceText, targetText, feedbackText, stockText, bananaText, toolText;
        Text reticle;
        ZooRoundedPanel bananaSelection, scratchSelection;

        void BuildInterface()
        {
            var canvas = ZooMenuUI.Canvas("Day 02 keeper interface", 110);
            canvas.transform.SetParent(transform, false);
            var composition = ZooMenuUI.Rect("Day 02 safe composition", canvas.transform, 0, 0, 1920, 1080);
            composition.anchorMin = composition.anchorMax = composition.pivot = Vector2.one * .5f;
            composition.anchoredPosition = Vector2.zero; interfaceRoot = composition;
            hud = ZooMenuUI.Group("Day 02 HUD", composition);
            ZooMenuUI.Panel("Day patch", hud, 38, 30, 405, 122, ZooMenuUI.Cream, 25);
            ZooMenuUI.Label("Day name", hud, "DAY 02 / LAKESIDE CARE", 60, 46, 365, 43, 27, ZooMenuUI.Ink, TextAnchor.MiddleLeft, true);
            paceText = ZooMenuUI.Label("Shift pace", hud, "ONE REQUEST AT A TIME", 60, 96, 365, 32, 20, ZooMenuUI.Brown);
            ZooMenuUI.Panel("Score paper", hud, 550, 30, 820, 122, ZooMenuUI.Cream, 25);
            scoreText = ZooMenuUI.Label("Care total", hud, "CARE 0 / 12", 578, 44, 755, 42, 30, ZooMenuUI.Ink, TextAnchor.MiddleCenter, true);
            ZooMenuUI.Panel("Score track", hud, 588, 108, 744, 18, ZooMenuUI.Hex("D7C9A7"), 9);
            scoreFill = ZooMenuUI.Panel("Care progress", hud, 588, 108, 0, 18, ZooMenuUI.Leaf, 9).rectTransform;
            for (int i = 1; i <= 3; i++) ZooMenuUI.Glyph("Star goal marker", hud, ZooGlyphGraphic.Kind.Sparkle, 578 + 248 * i, 96, 22, 40, ZooMenuUI.Orange);
            ZooMenuUI.Panel("Clock paper", hud, 1520, 30, 360, 122, ZooMenuUI.Cream, 25);
            clockText = ZooMenuUI.Label("Shift clock", hud, "02:00", 1540, 42, 320, 80, 63, ZooMenuUI.Ink, TextAnchor.MiddleCenter, true);
            reticle = ZooMenuUI.Label("Aim reticle", hud, "+", 936, 516, 48, 48, 39, Color.white, TextAnchor.MiddleCenter, true);
            reticle.gameObject.AddComponent<Shadow>().effectColor = ZooMenuUI.Ink;
            var hint = ZooMenuUI.Panel("Target hint paper", hud, 570, 818, 780, 95, new Color(.16f, .24f, .12f, .84f), 25);
            targetText = ZooMenuUI.Label("Target instruction", hint.transform, "", 25, 8, 730, 80, 26, ZooMenuUI.Cream, TextAnchor.MiddleCenter);
            feedbackText = ZooMenuUI.Label("Care feedback", hud, "", 420, 172, 1080, 70, 31, ZooMenuUI.Cream, TextAnchor.MiddleCenter, true);
            feedbackText.gameObject.AddComponent<Shadow>().effectColor = ZooMenuUI.Ink;
            bananaSelection = ZooMenuUI.Panel("Banana selected", hud, 38, 882, 252, 106, ZooMenuUI.Orange, 24);
            scratchSelection = ZooMenuUI.Panel("Scratcher selected", hud, 312, 882, 252, 106, ZooMenuUI.Orange, 24);
            var bananaButton = ZooMenuUI.Button("Choose banana", hud, "", 44, 888, 240, 94, () => SelectTool(0), null, 25);
            var scratchButton = ZooMenuUI.Button("Choose scratcher", hud, "", 318, 888, 240, 94, () => SelectTool(1), null, 25);
            ZooMenuUI.Picture("Banana icon", bananaButton.transform, bananaIcon, 7, 3, 80, 80);
            bananaText = ZooMenuUI.Label("Banana stock", bananaButton.transform, "1 / BANANA", 91, 8, 140, 78, 21, ZooMenuUI.Ink, TextAnchor.MiddleCenter, true);
            ZooMenuUI.Picture("Scratcher icon", scratchButton.transform, scratcherIcon, 7, 3, 80, 80);
            ZooMenuUI.Label("Scratcher name", scratchButton.transform, "2 / SCRATCH\nREUSABLE", 85, 8, 147, 78, 20, ZooMenuUI.Ink, TextAnchor.MiddleCenter, true);
            ZooMenuUI.Panel("Stand hint", hud, 1400, 900, 480, 80, new Color(.16f, .24f, .12f, .84f), 22);
            stockText = ZooMenuUI.Label("Restock direction", hud, "", 1410, 911, 460, 55, 24, ZooMenuUI.Cream, TextAnchor.MiddleCenter, true);
            toolText = ZooMenuUI.Label("Controls", hud, "WASD MOVE  •  HOLD RMB LOOK  •  LMB USE  •  1 / 2 TOOLS  •  E RESTOCK  •  ESC PAUSE", 50, 1020, 1820, 36, 23, ZooMenuUI.Cream, TextAnchor.MiddleCenter);
            toolText.gameObject.AddComponent<Shadow>().effectColor = ZooMenuUI.Ink;
            hud.gameObject.SetActive(false);
            BuildBriefing(composition);
            BuildSupplyMarker();
        }

        void BuildBriefing(Transform composition)
        {
            briefing = ZooMenuUI.Group("Day 02 briefing", composition);
            var shade = ZooMenuUI.Panel("Briefing shade", briefing, 0, 0, 1920, 1080, new Color(.08f, .13f, .05f, .57f), 0); shade.raycastTarget = true;
            ZooMenuUI.Panel("Briefing binding", briefing, 408, 155, 1104, 765, ZooMenuUI.Brown, 36);
            ZooMenuUI.Picture("Briefing paper", briefing, ZooPCFlow.Instance.assets.paper, 420, 167, 1080, 741);
            ZooMenuUI.Label("Briefing heading", briefing, "DAY 02  /  LAKESIDE CARE", 490, 200, 940, 85, 53, ZooMenuUI.Ink, TextAnchor.MiddleCenter, true);
            ZooMenuUI.Handwriting("Briefing invitation", briefing, "More little needs. More ways to help.", 500, 294, 920, 70, 44);
            BriefingTip(briefing, bananaIcon, "01  FEED THE HUNGRY ONES", "Choose banana [1], aim, then click to throw.\nCarry six bananas; restock at the stand with [E].", 400);
            BriefingTip(briefing, scratcherIcon, "02  A BRAND-NEW CARE TOOL", "Choose scratcher [2] and move close.\nClick three separate strokes to soothe an itchy monkey.", 560);
            ZooMenuUI.Label("Briefing goal", briefing, "2 MINUTES  •  12 CARE TASKS  •  STAR GOALS 4 / 8 / 12", 485, 722, 950, 50, 29, ZooMenuUI.Leaf, TextAnchor.MiddleCenter, true);
            ZooMenuUI.Button("Begin Day 02 shift", briefing, "LET'S HELP!", 770, 808, 380, 76, StartShift, ZooMenuUI.Leaf, 32);
            ZooMenuUI.Label("Start shortcut", briefing, "SPACE / GAMEPAD A", 810, 932, 300, 40, 21, ZooMenuUI.Cream, TextAnchor.MiddleCenter);
        }
        void BriefingTip(Transform p, Texture texture, string title, string description, float y)
        {
            ZooMenuUI.Picture("Care tool", p, texture, 490, y - 10, 125, 125);
            ZooMenuUI.Label("Tip title", p, title, 645, y, 740, 47, 31, ZooMenuUI.Ink, TextAnchor.MiddleLeft, true);
            ZooMenuUI.Label("Tip body", p, description, 645, y + 52, 740, 86, 27, ZooMenuUI.Brown);
        }
        void BuildSupplyMarker()
        {
            var go = new GameObject("Day 02 banana stand label", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(supply, false); var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            supplyLabel = go.GetComponent<RectTransform>(); supplyLabel.sizeDelta = new Vector2(800, 200);
            supplyLabel.localScale = Vector3.one * .0019f;
            ZooMenuUI.Panel("Supply sign", supplyLabel, 0, 0, 800, 200, ZooMenuUI.Cream, 40);
            ZooMenuUI.Label("Supply caption", supplyLabel, "BANANA SUPPLIES\nE / GAMEPAD A  •  RESTOCK", 28, 20, 744, 155, 40, ZooMenuUI.Ink, TextAnchor.MiddleCenter, true);
        }
        void UpdateInterface()
        {
            int remaining = Mathf.CeilToInt(Mathf.Max(0, rules.duration - Round.Elapsed));
            clockText.text = (remaining / 60).ToString("00") + ":" + (remaining % 60).ToString("00");
            clockText.color = remaining <= 20 ? ZooMenuUI.Orange : ZooMenuUI.Ink;
            paceText.text = Round.Elapsed < rules.secondRequestAt ? "ONE REQUEST AT A TIME" : "BUSY SHIFT  •  TWO REQUESTS";
            scoreText.text = "CARE " + Round.Completed + " / " + Round.Total + "     •     " + Round.Stars + " / 3 STARS";
            scoreFill.sizeDelta = new Vector2(744 * Mathf.Clamp01((float)Round.Completed / rules.threeStars), 18);
            bananaSelection.gameObject.SetActive(SelectedTool == 0); scratchSelection.gameObject.SetActive(SelectedTool == 1);
            bananaText.text = "1 / BANANA\n" + Round.Bananas + " / " + rules.bananaCapacity;
            feedbackText.text = Time.time < feedbackUntil ? feedback : "";
            float distance = HorizontalDistance(player.transform.position, supply.position);
            Vector3 towardStand = playerCamera.transform.InverseTransformDirection(supply.position-playerCamera.transform.position);
            string bearing = towardStand.z < 0 ? "BEHIND" : Mathf.Abs(towardStand.x) > Mathf.Abs(towardStand.z)*.4f ? (towardStand.x < 0 ? "LEFT" : "RIGHT") : "AHEAD";
            stockText.text = distance <= 3.4f ? "E / A  •  REFILL BANANAS" : "STAND " + bearing + "  •  " + Mathf.CeilToInt(distance) + " m";
            stockText.color = Round.Bananas <= 2 ? new Color(1, .76f, .36f) : ZooMenuUI.Cream;
            string hint = SelectedTool == 0 ? "AIM AT A HUNGRY MONKEY\nLEFT CLICK / RT TO THROW" : "MOVE CLOSE TO AN ITCHY MONKEY\nLEFT CLICK / RT FOR EACH STROKE";
            reticle.color = Color.white;
            if (Target)
            {
                var a = Round.animals[Target.index];
                if (a.state == ZooCareState.Eating) hint = "MUNCHING...\nLOOK FOR THE NEXT REQUEST";
                else if (a.state == ZooCareState.Waiting)
                {
                    bool correct = (SelectedTool == 0) == (a.need == CareNeed.Hunger);
                    if (!correct) hint = a.need == CareNeed.Hunger ? "THIS MONKEY IS HUNGRY\nCHOOSE BANANA [1 / GAMEPAD X]" : "THIS MONKEY IS ITCHY\nCHOOSE SCRATCHER [2 / GAMEPAD Y]";
                    else if (SelectedTool == 1)
                    {
                        float d = Vector3.Distance(playerCamera.transform.position, Target.AimPoint);
                        hint = d > rules.scratchReach ? "MOVE CLOSER  •  " + d.ToString("0.0") + " m" : "CLICK TO SCRATCH  •  " + a.strokes + " / " + Round.StrokeGoal(a);
                        if (d <= rules.scratchReach) reticle.color = new Color(.52f, .9f, 1);
                    }
                    else { hint = "HUNGRY MONKEY IN SIGHT\nCLICK TO THROW A BANANA"; reticle.color = new Color(1, .78f, .23f); }
                }
            }
            targetText.text = hint;
        }
        void LateUpdate()
        {
            if (supplyLabel && playerCamera)
            {
                supplyLabel.position = supply.position + Vector3.up * 1.8f;
                supplyLabel.rotation = Quaternion.LookRotation(supplyLabel.position - playerCamera.transform.position);
            }
        }
        void ShowResults()
        {
            hud.gameObject.SetActive(false);
            var p = ZooMenuUI.Group("Day 02 results", interfaceRoot);
            var shade = ZooMenuUI.Panel("Results shade", p, 0, 0, 1920, 1080, new Color(.08f, .13f, .05f, .73f), 0); shade.raycastTarget = true;
            ZooMenuUI.Panel("Results binding", p, 480, 125, 960, 830, ZooMenuUI.Brown, 32);
            ZooMenuUI.Picture("Results paper", p, ZooPCFlow.Instance.assets.paper, 492, 137, 936, 806);
            ZooMenuUI.Label("Shift result", p, "DAY 02  /  SHIFT RECORDED", 530, 180, 860, 75, 45, ZooMenuUI.Ink, TextAnchor.MiddleCenter, true);
            ZooMenuUI.Handwriting("Result note", p, Round.Passing ? "Happy monkeys. A day well spent." : "A little practice makes a caring keeper.", 530, 270, 860, 60, 38);
            for (int i = 0; i < 3; i++)
            {
                var star = ZooMenuUI.Rect("Result star " + (i + 1), p, 720 + i * 170, 350, 140, 140).gameObject.AddComponent<Image>();
                star.sprite = CareUiSkin.Art("star_gold"); star.color = i < Round.Stars ? Color.white : new Color(.55f, .52f, .44f, .45f); star.raycastTarget = false;
            }
            ZooMenuUI.Label("Task breakdown", p, "FEEDINGS             SCRATCHING             TOTAL CARE\n" + Round.Feedings + " / 6                     " + Round.Scratches + " / 6                     " + Round.Completed + " / " + Round.Total, 560, 520, 800, 115, 29, ZooMenuUI.Ink, TextAnchor.MiddleCenter, true);
            ZooMenuUI.Label("Best stars", p, "BEST RECORD  " + ZooPCFlow.BestStarsFor(2) + " / 3 STARS", 560, 654, 800, 60, 29, ZooMenuUI.Leaf, TextAnchor.MiddleCenter, true);
            ZooMenuUI.Button("Day 02 retry", p, "RETRY DAY 02", 570, 775, 350, 82, () => ZooPCFlow.Instance.RetryDay(), null, 28);
            ZooMenuUI.Button("Day 02 work log", p, "WORK LOG  >", 1000, 775, 350, 82, () => ZooPCFlow.Instance.ReturnToJournal(), ZooMenuUI.Leaf, 28);
        }
    }
}
