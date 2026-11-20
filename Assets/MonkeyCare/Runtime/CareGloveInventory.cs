using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    public sealed class CareGloveInventory
    {
        readonly MonkeyCareSession session;
        readonly RectTransform root;
        readonly RectTransform bagButton;
        readonly Image selector, bananaIcon;
        readonly Text count, status, confirmButtonLabel, backButtonLabel, actionLabel, bagButtonLabel;
        public bool IsOpen { get; private set; }
        public bool BananaSelected { get; private set; }
        public RectTransform BagButtonRoot => bagButton;

        public CareGloveInventory(MonkeyCareSession owner, Camera camera)
        {
            session = owner;
            root = CareUi.Canvas("Care Backpack", camera.transform,
                new Vector3(0, -.17f, 1.16f), .82f, new Vector2(760, 801), 70);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var background = CareUi.Image("Backpack UI", root, CareUiSkin.BackpackPanel,
                new Vector2(760, 801), Vector2.zero);
            background.preserveAspect = true;
            background.raycastTarget = false;

            // The supplied BAG artwork contains baked lettering. The ribbon keeps
            // the title sharp and consistent with the rest of the English HUD.
            CareUiSkin.AddSliced(root, "Backpack title ribbon", CareUiSkin.Header,
                new Vector2(320, 78), new Vector2(0, 174), Color.white);
            CareRoundView.Label("Backpack title", root, "BAG",
                new Vector2(300, 72), new Vector2(0, 174), 54,
                TextAnchor.MiddleCenter, Color.white);

            selector = CareUiSkin.AddSliced(root, "Selected slot glow", CareUiSkin.YellowButton,
                new Vector2(146, 146), new Vector2(-241, 109),
                new Color(1f, .82f, .24f, .48f));
            selector.gameObject.SetActive(false);

            bananaIcon = CareUi.Image("Banana item", root, CareUiSkin.BackpackBanana,
                new Vector2(112, 112), new Vector2(-241, 109));
            bananaIcon.preserveAspect = true;
            bananaIcon.raycastTarget = false;

            var countBadge = CareUi.Image("Banana count badge", root, CareUiSkin.BackpackCount,
                new Vector2(60, 60), new Vector2(-203, 70));
            countBadge.preserveAspect = true;
            countBadge.raycastTarget = false;
            count = Text("10", countBadge.transform, new Vector2(54, 54), Vector2.zero,
                31, TextAnchor.MiddleCenter, new Color(.30f, .18f, .08f, 1f));

            CareUiSkin.KeyBadge(root, CareInput.ConfirmLabel, new Vector2(78, 78),
                new Vector2(-222, -335), out confirmButtonLabel, 46, "Confirm key");
            actionLabel = Text("SELECT", root, new Vector2(190, 54),
                new Vector2(-103, -335), 42, TextAnchor.MiddleLeft,
                new Color(.34f, .20f, .10f, 1f));

            CareUiSkin.KeyBadge(root, CareInput.BackLabel, new Vector2(78, 78),
                new Vector2(56, -335), out backButtonLabel, 46, "Back key");
            Text("CLOSE BAG", root, new Vector2(230, 50), new Vector2(220, -335),
                42, TextAnchor.MiddleLeft, new Color(.34f, .20f, .10f, 1f));

            status = Text(string.Empty, root, new Vector2(650, 54), new Vector2(0, -388),
                40, TextAnchor.MiddleCenter, new Color(.40f, .25f, .14f, 1f));

            bagButton = CareUi.Canvas("Care Backpack HUD", camera.transform,
                new Vector3(-.77f, -.46f, 1.18f), .48f, new Vector2(360, 210));
            CareUiSkin.Add(bagButton, "Bag icon", CareUiSkin.BackpackIcon,
                new Vector2(128, 128), new Vector2(-70, 20), Color.white);
            CareUiSkin.Add(bagButton, "Bag key thickness", CareUiSkin.RoundedCard,
                new Vector2(60, 60), new Vector2(-57, -60),
                new Color(.20f, .06f, .10f, .75f));
            CareUiSkin.KeyBadge(bagButton, CareInput.BackLabel, new Vector2(52, 52),
                new Vector2(-58, -57), out bagButtonLabel, 29, "Bag key");

            root.gameObject.SetActive(false);
            bagButton.gameObject.SetActive(false);
            Refresh(owner.Round);
        }

        public void Tick(CareInputSnapshot input, CareRound round, bool mayOpen)
        {
            bool showBagButton = mayOpen && !IsOpen;
            if (bagButton.gameObject.activeSelf != showBagButton)
                bagButton.gameObject.SetActive(showBagButton);
            bagButtonLabel.text = CareInput.BackLabel;
            if (input.BWasPressed && (IsOpen || mayOpen)) Toggle(round, mayOpen);
            if (!IsOpen) return;
            Refresh(round);
            selector.gameObject.SetActive(BananaSelected);
            float pulse = .38f + .18f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 4f));
            selector.color = new Color(1f, .78f, .18f, pulse);

            if (!input.AWasPressed) return;
            if (BananaSelected)
            {
                CancelSelection();
                Refresh(round);
                return;
            }
            if (round == null || round.Bananas <= 0) return;

            BananaSelected = true;
            if (session.EquipBananaFromGlove()) Close();
            else
            {
                BananaSelected = false;
                Refresh(round);
                status.text = "Banana is returning to the glove. Try again.";
            }
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

        public void ShowHud()
        {
            bagButtonLabel.text = CareInput.BackLabel;
            bagButton.gameObject.SetActive(true);
        }

        public void HideHud()
        {
            if (!IsOpen) bagButton.gameObject.SetActive(false);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            root.gameObject.SetActive(false);
            session.SetGloveMenuOpen(false);
        }

        public void CancelSelection()
        {
            BananaSelected = false;
            session.CancelSelectedBanana();
        }

        public void ClearSelection() => BananaSelected = false;

        public void Reset(CareRound round)
        {
            ClearSelection();
            Close();
            Refresh(round);
        }

        void Refresh(CareRound round)
        {
            int bananas = round != null ? round.Bananas : 0;
            if (bananas <= 0 && BananaSelected) ClearSelection();
            count.text = bananas.ToString();
            confirmButtonLabel.text = CareInput.ConfirmLabel;
            backButtonLabel.text = CareInput.BackLabel;
            actionLabel.text = BananaSelected ? "CANCEL" : "SELECT";
            selector.gameObject.SetActive(BananaSelected);
            status.text = BananaSelected
                ? "Selected. Press " + CareInput.ConfirmLabel + " to cancel."
                : bananas > 0
                    ? "Press " + CareInput.ConfirmLabel + " to select  |  " +
                      CareInput.BackLabel + " to close"
                    : "No bananas. Refill at the market stand.";
            bananaIcon.color = bananas > 0 ? Color.white : new Color(.55f, .55f, .55f, .65f);
            count.color = bananas > 0
                ? new Color(.30f, .18f, .08f, 1f)
                : new Color(.45f, .45f, .45f, .70f);
        }

        static Text Text(string value, Transform parent, Vector2 size, Vector2 position,
            int fontSize, TextAnchor alignment, Color color)
        {
            var rect = CareUi.Rect("Text", parent, size, position);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = CareFont.LoadReadable();
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.text = value;
            return CareRoundView.Legible(label, fontSize);
        }
    }
}
