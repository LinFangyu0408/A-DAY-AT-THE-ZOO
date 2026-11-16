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

        public void Begin(MonkeyCareSession owner, Camera camera, float visibleAt)
        {
            session = owner;
            shownAt = visibleAt;
            stepAt = Time.unscaledTime;
            root = CareUi.Canvas("Care Chinese Tutorial", camera.transform,
                new Vector3(-.44f, .34f, 1.4f), .72f, new Vector2(720, 400));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;

            var panel = CareUi.Image("Tutorial panel", root, null, new Vector2(720, 400), Vector2.zero);
            panel.color = new Color(.025f, .085f, .11f, .82f);
            var edge = CareUi.Image("Tutorial accent", root, null, new Vector2(10, 400), new Vector2(-355, 0));
            edge.color = new Color(1f, .74f, .24f, 1);
            var badgePanel = CareUi.Image("Key badge", root, null, new Vector2(102, 86), new Vector2(282, 101));
            badgePanel.color = new Color(1f, .74f, .24f, .20f);
            keyBadge = Label("Key", badgePanel.transform, string.Empty, new Vector2(102, 86), Vector2.zero,
                34, TextAnchor.MiddleCenter, new Color(1f, .88f, .53f, 1));

            stepLabel = Label("Tutorial step", root, "1 / 9", new Vector2(280, 42),
                new Vector2(-235, 158), 26, TextAnchor.MiddleLeft, new Color(1f, .82f, .42f, 1));
            title = Label("Tutorial title", root, string.Empty, new Vector2(520, 58),
                new Vector2(-18, 102), 42, TextAnchor.MiddleLeft, Color.white);
            body = Label("Tutorial body", root, string.Empty, new Vector2(630, 150),
                new Vector2(-10, -6), 28, TextAnchor.MiddleLeft, new Color(.92f, .96f, .94f, 1));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            var actionPanel = CareUi.Image("Tutorial action panel", root, null, new Vector2(630, 70),
                new Vector2(-10, -145));
            actionPanel.color = new Color(.06f, .20f, .21f, .92f);
            action = Label("Tutorial action", root, string.Empty, new Vector2(600, 60),
                new Vector2(-10, -145), 26, TextAnchor.MiddleLeft, new Color(1f, .88f, .58f, 1));
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
                        action.text = "没喂到也没关系：再按 B，按 A 重新拿香蕉";
                        keyBadge.text = "B/A";
                    }
                    break;
                case FinalStep:
                    if (Elapsed >= 3f) Finish();
                    break;
            }
            if (step == 4 && session.GloveInventory != null && session.GloveInventory.IsOpen)
                keyBadge.text = "B";
            if (step >= 3 && step <= 7)
            {
                int seconds = Mathf.FloorToInt(Time.unscaledTime - hungerStartedAt);
                stepLabel.text = (step + 1) + " / " + (FinalStep + 1) + " · A/B " + seconds + "s";
            }
        }

        float Elapsed => Time.unscaledTime - stepAt;

        bool HoldingFood => session.HeldItem && session.HeldItem.kind == CareNeed.Hunger;

        void ShowStep(int value)
        {
            step = Mathf.Clamp(value, 0, FinalStep);
            stepAt = Time.unscaledTime;
            stepLabel.text = (step + 1) + " / " + (FinalStep + 1);
            switch (step)
            {
                case 0:
                    title.text = "欢迎来到动物园";
                    body.text = "这一关只练习喂香蕉。先熟悉走路和手套项目栏，之后再照顾三只猴子。";
                    action.text = "准备开始";
                    keyBadge.text = "香蕉";
                    break;
                case 1:
                    title.text = "先走两步";
                    body.text = "推动左摇杆向前、向后或左右移动。项目栏打开时移动会暂停。";
                    action.text = "推动左摇杆，完成后再继续";
                    keyBadge.text = "L";
                    break;
                case 2:
                    title.text = "转身看猴子";
                    body.text = "左右推动右摇杆转动视角。习惯用视线和身体一起寻找目标。";
                    action.text = "左右推动右摇杆";
                    keyBadge.text = "R";
                    break;
                case 3:
                    if (!hungerStarted)
                    {
                        hungerStarted = true;
                        hungerStartedAt = Time.unscaledTime;
                        session.BeginTutorialHunger();
                    }
                    title.text = "一只猴子饿了";
                    body.text = "正前方的猴子已经出现饥饿提示。接下来用约 1 分钟练习 A/B，它会一直等你完成。";
                    action.text = "看清猴子的耐心条，准备打开手套";
                    keyBadge.text = "饥饿";
                    break;
                case 4:
                    title.text = "打开手套项目栏";
                    body.text = "B 是手套菜单键。按一下 B 打开项目栏，再按一次 B 可以退出；打开时人物会暂时停止移动。";
                    action.text = "按 B 打开手套项目栏";
                    keyBadge.text = "B";
                    break;
                case 5:
                    title.text = "选择香蕉";
                    body.text = "A 是确认键。项目栏打开时按 A，选择香蕉；菜单会收起，香蕉会出现在右手。";
                    action.text = "按 A 选择香蕉";
                    keyBadge.text = "A";
                    break;
                case 6:
                    title.text = "瞄准猴子";
                    body.text = "现在香蕉已经在手上。转身面对饥饿猴子，把屏幕中心或右手对准它，然后按住 RT。";
                    action.text = HoldingFood ? "按住 RT / Trigger 瞄准" : "请重新打开项目栏选择香蕉";
                    keyBadge.text = "RT";
                    break;
                case 7:
                    title.text = "松开投掷，等它吃完";
                    body.text = "松开 RT 把香蕉投出去。命中后保持片刻，猴子会吃完并出现开心表情。";
                    action.text = session.TutorialHungerSatisfied ? "喂食成功" : "松开 RT；没命中就重新按 B、A";
                    keyBadge.text = "松";
                    break;
                default:
                    title.text = "A/B 操作练习完成";
                    body.text = "你已经完成取香蕉和喂食。正式关卡开始后，打开手套取香蕉，按住 RT 瞄准，松开投掷。";
                    action.text = "每只猴子需要 3 次喂食，共 9 次";
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
            text.font = CareFont.Load();
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
    }
}
