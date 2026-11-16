using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    public sealed class CareGloveInventory
    {
        readonly MonkeyCareSession session;
        readonly RectTransform root;
        readonly Image selector;
        readonly Text count, status;
        public bool IsOpen { get; private set; }

        public CareGloveInventory(MonkeyCareSession owner, Camera camera)
        {
            session = owner;
            root = CareUi.Canvas("Care Glove Inventory", camera.transform,
                new Vector3(0, -.23f, 1.15f), .88f, new Vector2(1000, 440));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var panel = CareUi.Image("Glove panel", root, null, new Vector2(1000, 440), Vector2.zero);
            panel.color = new Color(.035f, .10f, .12f, .96f);
            var header = CareUi.Image("Glove header", root, null, new Vector2(1000, 84), new Vector2(0, 178));
            header.color = new Color(.10f, .27f, .27f, 1);
            var accent = CareUi.Image("Glove accent", root, null, new Vector2(12, 440), new Vector2(-494, 0));
            accent.color = new Color(1f, .74f, .24f, 1);

            Text("手套项目栏", root, new Vector2(760, 62), new Vector2(-80, 178), 48, TextAnchor.MiddleLeft, Color.white);
            Text("当前关卡：喂香蕉", root, new Vector2(760, 42), new Vector2(-80, 92), 30,
                TextAnchor.MiddleLeft, new Color(1f, .86f, .53f, 1));

            var slot = CareUi.Image("Banana slot", root, null, new Vector2(790, 166), new Vector2(-55, 2));
            slot.color = new Color(.12f, .22f, .21f, 1);
            selector = CareUi.Image("Selected slot", root, null, new Vector2(810, 184), new Vector2(-55, 2));
            selector.color = new Color(1f, .73f, .22f, .34f);
            selector.transform.SetSiblingIndex(slot.transform.GetSiblingIndex());

            var icon = CareUi.Image("Banana icon", root, null, new Vector2(126, 126), new Vector2(-386, 2));
            icon.color = new Color(1f, .78f, .20f, 1);
            Text("香蕉", root, new Vector2(250, 66), new Vector2(-205, 34), 43, TextAnchor.MiddleLeft, Color.white);
            Text("喂给饥饿的猴子", root, new Vector2(330, 42), new Vector2(-165, -24), 28,
                TextAnchor.MiddleLeft, new Color(.76f, .88f, .85f, 1));
            count = Text("×12", root, new Vector2(220, 90), new Vector2(338, 8), 62,
                TextAnchor.MiddleRight, new Color(1f, .88f, .53f, 1));
            status = Text(string.Empty, root, new Vector2(790, 44), new Vector2(-55, -118), 28,
                TextAnchor.MiddleLeft, new Color(.76f, .88f, .85f, 1));

            var a = CareUi.Image("A button", root, null, new Vector2(52, 52), new Vector2(-340, -190));
            a.color = new Color(.22f, .62f, .30f, 1);
            Text("A", a.transform, new Vector2(52, 52), Vector2.zero, 34, TextAnchor.MiddleCenter, Color.white);
            Text("选择", root, new Vector2(90, 46), new Vector2(-270, -190), 28,
                TextAnchor.MiddleLeft, Color.white);
            var b = CareUi.Image("B button", root, null, new Vector2(52, 52), new Vector2(235, -190));
            b.color = new Color(.76f, .22f, .23f, 1);
            Text("B", b.transform, new Vector2(52, 52), Vector2.zero, 34, TextAnchor.MiddleCenter, Color.white);
            Text("打开 / 退出", root, new Vector2(240, 46), new Vector2(355, -190), 28,
                TextAnchor.MiddleLeft, Color.white);
            Text("项目栏打开时无法移动", root, new Vector2(360, 38), new Vector2(300, -240), 22,
                TextAnchor.MiddleRight, new Color(.58f, .72f, .70f, 1));

            root.gameObject.SetActive(false);
            Refresh(owner.Round);
        }

        public void Tick(CareInputSnapshot input, CareRound round, bool mayOpen)
        {
            if (input.BWasPressed && (IsOpen || mayOpen)) Toggle(round, mayOpen);
            if (!IsOpen) return;
            Refresh(round);
            float pulse = .28f + .14f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 4f));
            selector.color = new Color(1f, .73f, .22f, pulse);
            if (input.AWasPressed && session.EquipBananaFromGlove()) Close();
        }

        void Toggle(CareRound round, bool mayOpen)
        {
            if (IsOpen) Close();
            else if (mayOpen) Open(round);
        }

        public void Open(CareRound round)
        {
            if (IsOpen) return;
            IsOpen = true;
            Refresh(round);
            root.gameObject.SetActive(true);
            session.SetGloveMenuOpen(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            root.gameObject.SetActive(false);
            session.SetGloveMenuOpen(false);
        }

        public void Reset(CareRound round)
        {
            Close();
            Refresh(round);
        }

        void Refresh(CareRound round)
        {
            int bananas = round != null ? round.Bananas : 0;
            count.text = "×" + bananas;
            status.text = session.HeldItem && session.HeldItem.kind == CareNeed.Hunger
                ? "香蕉已装备"
                : bananas > 0 ? "摇杆选择 · A 确认" : "香蕉已经用完";
            selector.color = bananas > 0
                ? new Color(1f, .73f, .22f, .34f)
                : new Color(.40f, .40f, .40f, .25f);
        }

        static Text Text(string value, Transform parent, Vector2 size, Vector2 position,
            int fontSize, TextAnchor alignment, Color color)
        {
            var rect = CareUi.Rect("Text", parent, size, position);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = CareFont.Load();
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = value;
            return label;
        }
    }
}
