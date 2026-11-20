using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    public sealed class CareTutorial : MonoBehaviour
    {
        const int FinalStep = 6;
        // The first ~15 seconds belong to the player: walk around, look at the
        // zoo, get comfortable. Only then does the first monkey get hungry and
        // the bag / feeding instructions appear.
        const float ExplorationSeconds = 15f;
        MonkeyCareSession session;
        RectTransform root;
        CanvasGroup group;
        Text stepLabel, title, body, action, keyLabel;
        Image keyCap;
        int step = -1;
        float shownAt, stepAt, beganAt;
        bool finished, hungerStarted;
        string controllerLabels;

        public void Begin(MonkeyCareSession owner, Camera camera, float visibleAt)
        {
            session = owner;
            shownAt = visibleAt;
            beganAt = Time.unscaledTime;
            stepAt = Time.unscaledTime;
            // The tutorial must never block the player's view. Park it in the
            // upper-left corner as a compact hint panel, well clear of the
            // centred SCORE HUD.
            root = CareUi.Canvas("Care Tutorial", camera.transform,
                new Vector3(-.82f, .24f, 1.34f), .56f, new Vector2(820, 470), 80);
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

            keyCap = CareUiSkin.KeyBadge(root, "GO", new Vector2(132, 132),
                new Vector2(-318, 140), out keyLabel, 46);

            stepLabel = Label("Tutorial step", root, "STEP 1 / 8", new Vector2(170, 38),
                new Vector2(-205, 142), 28, TextAnchor.MiddleCenter, new Color(.95f, .98f, 1f, 1));
            stepLabel.gameObject.SetActive(false);
            title = Label("Tutorial title", root, string.Empty, new Vector2(600, 68),
                new Vector2(95, 118), 58, TextAnchor.MiddleLeft, Color.white);
            body = Label("Tutorial body", root, string.Empty, new Vector2(730, 168),
                new Vector2(0, -14), 54, TextAnchor.MiddleCenter, new Color(.035f, .10f, .19f, 1));
            CareUiSkin.AddSliced(root, "Tutorial action panel", CareUiSkin.Button,
                new Vector2(736, 92), new Vector2(0, -166), new Color(.78f, .94f, 1f, 1f));
            // One key cap only (in the header) keeps the card quiet; the action
            // bar repeats the instruction in words instead of a second badge.
            action = Label("Tutorial action", root, string.Empty, new Vector2(700, 72),
                new Vector2(0, -166), 44, TextAnchor.MiddleCenter, Color.white);

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
                    if (StepElapsed >= 2.5f) ShowStep(1);
                    break;
                case 1:
                    // Move around first; the fallback keeps the tutorial from
                    // stalling if the player only watches.
                    if ((input.LeftStick.sqrMagnitude > .08f && StepElapsed >= 2f) || TotalElapsed >= 8f)
                        ShowStep(2);
                    break;
                case 2:
                    if ((Mathf.Abs(input.RightStick.x) > .18f && StepElapsed >= 2f) || TotalElapsed >= 14.5f)
                        ShowStep(3);
                    break;
                case 3:
                    // The instruction (and its B badge) is already on screen
                    // before the player opens the bag — never after.
                    if (session.GloveInventory != null && session.GloveInventory.IsOpen) ShowStep(4);
                    break;
                case 4:
                    if (session.HeldItem && session.HeldItem.kind == CareNeed.Hunger &&
                        session.GloveInventory != null && !session.GloveInventory.IsOpen) ShowStep(5);
                    break;
                case 5:
                    // Aim and throw are one instruction so the player reads
                    // "release to throw" before releasing, not afterwards.
                    if (session.TutorialHungerSatisfied) ShowStep(FinalStep);
                    break;
                case FinalStep:
                    if (StepElapsed >= 3f) Finish();
                    break;
            }
            stepLabel.text = "STEP " + (step + 1) + " / " + (FinalStep + 1);
        }

        float Elapsed => Time.unscaledTime - stepAt;
        float StepElapsed => Elapsed;
        float TotalElapsed => Time.unscaledTime - beganAt;

        bool HoldingFood => session.HeldItem && session.HeldItem.kind == CareNeed.Hunger;

        void SetKey(string key)
        {
            keyLabel.text = key;
            keyCap.color = CareUiSkin.KeyColor(key);
        }

        void ShowStep(int value)
        {
            step = Mathf.Clamp(value, 0, FinalStep);
            stepAt = Time.unscaledTime;
            stepLabel.text = "STEP " + (step + 1) + " / " + (FinalStep + 1);
            switch (step)
            {
                case 0:
                    title.text = "WELCOME TO THE ZOO";
                    body.text = "Take a look around first.";
                    action.text = "GET READY";
                    SetKey("GO");
                    break;
                case 1:
                    title.text = "WALK";
                    body.text = "Use the left stick to move.";
                    action.text = "WALK TO CONTINUE";
                    SetKey("L");
                    break;
                case 2:
                    title.text = "LOOK AROUND";
                    body.text = "Use the right stick to look.";
                    action.text = "LOOK TO CONTINUE";
                    SetKey("R");
                    break;
                case 3:
                    if (!hungerStarted)
                    {
                        hungerStarted = true;
                        session.BeginTutorialHunger();
                    }
                    title.text = "OPEN THE BAG";
                    body.text = "A monkey is hungry. Press B to open your bag.";
                    action.text = "PRESS B TO OPEN";
                    SetKey(CareInput.BackLabel);
                    break;
                case 4:
                    title.text = "TAKE A BANANA";
                    body.text = "Pick the banana with A.";
                    action.text = "PRESS A TO SELECT";
                    SetKey(CareInput.ConfirmLabel);
                    break;
                case 5:
                    title.text = "AIM AND THROW";
                    body.text = "Hold RT to aim, release to throw.";
                    action.text = "HOLD RT, THEN RELEASE";
                    SetKey("RT");
                    break;
                case FinalStep:
                    title.text = "GREAT JOB";
                    body.text = "Keep feeding the monkeys.";
                    action.text = "TUTORIAL COMPLETE";
                    SetKey("OK");
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
            text.text = value;
            return CareRoundView.Legible(text, fontSize);
        }
    }
}
