using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    public sealed class CareTutorial : MonoBehaviour
    {
        const int FinalStep = 8;
        MonkeyCareSession session;
        RectTransform root;
        CanvasGroup group;
        Text stepLabel, title, body, action, keyBadge;
        int step = -1;
        float shownAt, stepAt, hungerStartedAt;
        bool finished, hungerStarted;
        string controllerLabels;

        public void Begin(MonkeyCareSession owner, Camera camera, float visibleAt)
        {
            session = owner;
            shownAt = visibleAt;
            stepAt = Time.unscaledTime;
            root = CareUi.Canvas("Care Tutorial", camera.transform,
                new Vector3(-.48f, .32f, 1.50f), .94f, new Vector2(820, 470));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;

            var shadow = CareUiSkin.AddSliced(root, "Tutorial shadow", CareUiSkin.Panel,
                new Vector2(800, 450), new Vector2(0, -12), new Color(.03f, .09f, .16f, .30f));
            shadow.transform.SetAsFirstSibling();
            CareUiSkin.AddSliced(root, "Tutorial panel", CareUiSkin.Panel,
                new Vector2(800, 440), Vector2.zero, new Color(1f, .97f, .88f, .99f));
            CareUiSkin.AddSliced(root, "Tutorial header", CareUiSkin.Header,
                new Vector2(758, 110), new Vector2(0, 140), new Color(.90f, .98f, 1f, 1f));
            var accent = CareUi.Image("Tutorial accent", root, null, new Vector2(758, 7), new Vector2(0, 85));
            accent.color = new Color(1f, .78f, .20f, 1f);
            var star = CareUiSkin.Add(root, "Tutorial star", CareUiSkin.Star,
                new Vector2(82, 76), new Vector2(334, 202), new Color(1f, .82f, .20f, 1f));
            star.preserveAspect = true;

            var badgePanel = CareUiSkin.Add(root, "Key badge", CareUiSkin.RoundButton,
                new Vector2(132, 132), new Vector2(-318, 140), Color.white);
            keyBadge = Label("Key", badgePanel.transform, string.Empty, new Vector2(128, 128), Vector2.zero,
                66, TextAnchor.MiddleCenter, new Color(.08f, .18f, .31f, 1));

            stepLabel = Label("Tutorial step", root, "STEP 1 / 9", new Vector2(170, 38),
                new Vector2(-205, 142), 28, TextAnchor.MiddleCenter, new Color(.95f, .98f, 1f, 1));
            stepLabel.gameObject.SetActive(false);
            title = Label("Tutorial title", root, string.Empty, new Vector2(610, 70),
                new Vector2(95, 76), 64, TextAnchor.MiddleLeft, Color.white);
            AddOutline(title, new Color(.08f, .20f, .36f, .95f), new Vector2(2, -2));
            body = Label("Tutorial body", root, string.Empty, new Vector2(724, 154),
                new Vector2(0, -8), 56, TextAnchor.MiddleLeft, new Color(.035f, .10f, .19f, 1));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            CareUiSkin.AddSliced(root, "Tutorial action panel", CareUiSkin.Button,
                new Vector2(736, 84), new Vector2(0, -158), new Color(.78f, .94f, 1f, 1f));
            action = Label("Tutorial action", root, string.Empty, new Vector2(686, 62),
                new Vector2(0, -158), 50, TextAnchor.MiddleCenter, Color.white);
            AddOutline(action, new Color(.06f, .16f, .29f, .95f), new Vector2(2, -2));
            action.horizontalOverflow = HorizontalWrapMode.Wrap;

            root.gameObject.SetActive(false);
            ShowStep(0);
        }

        void Update()
        {
            if (!root || finished || !session || !session.Begun) return;
            if (!MonkeyCareSession.Playing) { group.alpha = 0; return; }
            if (Time.unscaledTime < shownAt) { root.gameObject.SetActive(false); return; }

            root.gameObject.SetActive(true);
            group.alpha = Mathf.MoveTowards(group.alpha, 1, Time.unscaledDeltaTime * 5f);
            CareInputSnapshot input = session.Input;
            string labels = CareInput.ConfirmLabel + "/" + CareInput.BackLabel;
            if (labels != controllerLabels)
            {
                controllerLabels = labels;
                ShowStep(step);
            }
            switch (step)
            {
                case 0:
                    if (Elapsed >= 1.5f) ShowStep(1);
                    break;
                case 1:
                    if (input.LeftStick.sqrMagnitude > .08f) ShowStep(2);
                    break;
                case 2:
                    if (Mathf.Abs(input.RightStick.x) > .25f) ShowStep(3);
                    break;
                case 3:
                    if (Elapsed >= 4f) ShowStep(4);
                    break;
                case 4:
                    if (session.GloveInventory != null && session.GloveInventory.IsOpen) ShowStep(5);
                    break;
                case 5:
                    if (session.HeldItem && session.HeldItem.kind == CareNeed.Hunger &&
                        session.GloveInventory != null && !session.GloveInventory.IsOpen) ShowStep(6);
                    break;
                case 6:
                    if (input.TriggerHeld && session.HeldItem) ShowStep(7);
                    break;
                case 7:
                    if (session.TutorialHungerSatisfied) ShowStep(8);
                    else if (HoldingFood && (session.GloveInventory == null || !session.GloveInventory.IsOpen))
                        ShowStep(6);
                    else if (Elapsed >= 1.5f && !HoldingFood &&
                             (session.GloveInventory == null || !session.GloveInventory.IsOpen))
                    {
                        action.text = "RELEASE RT TO TRY AGAIN";
                        keyBadge.text = "RT";
                    }
                    break;
                case FinalStep:
                    if (Elapsed >= 3f) Finish();
                    break;
            }
            if (step == 4 && session.GloveInventory != null && session.GloveInventory.IsOpen)
                keyBadge.text = CareInput.BackLabel;
            stepLabel.text = "STEP " + (step + 1) + " / " + (FinalStep + 1);
        }

        float Elapsed => Time.unscaledTime - stepAt;

        bool HoldingFood => session.HeldItem && session.HeldItem.kind == CareNeed.Hunger;

        void ShowStep(int value)
        {
            step = Mathf.Clamp(value, 0, FinalStep);
            stepAt = Time.unscaledTime;
            stepLabel.text = "STEP " + (step + 1) + " / " + (FinalStep + 1);
            switch (step)
            {
                case 0:
                    title.text = "FEED MONKEYS";
                    body.text = "Learn the controls in under a minute.";
                    action.text = "GET READY";
                    keyBadge.text = "GO";
                    break;
                case 1:
                    title.text = "WALK";
                    body.text = "Use the left stick.";
                    action.text = "MOVE TO CONTINUE";
                    keyBadge.text = "L";
                    break;
                case 2:
                    title.text = "LOOK AROUND";
                    body.text = "Use the right stick.";
                    action.text = "LOOK TO CONTINUE";
                    keyBadge.text = "R";
                    break;
                case 3:
                    if (!hungerStarted)
                    {
                        hungerStarted = true;
                        hungerStartedAt = Time.unscaledTime;
                        session.BeginTutorialHunger();
                    }
                    title.text = "A MONKEY IS HUNGRY";
                    body.text = "Find it and open your bag.";
                    action.text = "FIND THE MONKEY";
                    keyBadge.text = "!";
                    break;
                case 4:
                    title.text = "OPEN THE BAG";
                    body.text = "Press " + CareInput.BackLabel + ".";
                    action.text = "PRESS " + CareInput.BackLabel + " TO OPEN";
                    keyBadge.text = CareInput.BackLabel;
                    break;
                case 5:
                    title.text = "TAKE A BANANA";
                    body.text = "Press " + CareInput.ConfirmLabel + ".";
                    action.text = "PRESS " + CareInput.ConfirmLabel + " TO SELECT";
                    keyBadge.text = CareInput.ConfirmLabel;
                    break;
                case 6:
                    title.text = "AIM";
                    body.text = "Hold RT on the monkey.";
                    action.text = HoldingFood ? "HOLD RT TO AIM" : "OPEN THE BAG AND TAKE A BANANA";
                    keyBadge.text = "RT";
                    break;
                case 7:
                    title.text = "THROW";
                    body.text = "Release RT to throw.";
                    action.text = session.TutorialHungerSatisfied ? "GOOD THROW!" : "RELEASE RT TO THROW";
                    keyBadge.text = "RT";
                    break;
                default:
                    title.text = "GREAT JOB";
                    body.text = "Keep feeding the monkeys.";
                    action.text = "TUTORIAL COMPLETE";
                    keyBadge.text = "OK";
                    break;
            }
        }

        void Finish()
        {
            finished = true;
            session.FinishTutorial();
            if (root) Destroy(root.gameObject);
        }

        void OnDestroy()
        {
            if (root) Destroy(root.gameObject);
        }

        static Text Label(string name, Transform parent, string value, Vector2 size, Vector2 position,
            int fontSize, TextAnchor alignment, Color color)
        {
            var rect = CareUi.Rect(name, parent, size, position);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = CareFont.LoadReadable();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = value;
            return text;
        }

        static void AddOutline(Graphic graphic, Color color, Vector2 distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }
    }
}
