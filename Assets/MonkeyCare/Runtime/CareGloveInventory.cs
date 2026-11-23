using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    public sealed class CareGloveInventory
    {
        readonly MonkeyCareSession session;
        readonly RectTransform root;
        readonly RectTransform bagButton;
        readonly Image selector, bananaIcon, confirmKey, backKey, bagKeyBackdrop;
        readonly Text count, status, confirmButtonLabel, backButtonLabel, actionLabel, bagButtonLabel;
        // Once the player has picked a banana the first time, the glove keeps
        // feeding itself: every throw is followed by a fresh banana and a
        // restock at the stand re-arms the hand. The corner entry button stays
        // visible (and B keeps reopening the bag) for the whole round, so the
        // player can always check or cancel the supply.
        bool autoEquip;
        public bool IsOpen { get; private set; }
        public bool BananaSelected => autoEquip;
        public bool AutoEquip => autoEquip;
        public RectTransform BagButtonRoot => bagButton;

        public CareGloveInventory(MonkeyCareSession owner, Transform head)
        {
            session = owner;
            root = CareUi.Canvas("Care Backpack", head,
                new Vector3(0, -.17f, 1.16f), .82f, new Vector2(760, 801), 70);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var background = CareUi.Image("Backpack UI", root, CareUiSkin.Art("panel_bag"),
                new Vector2(760, 760), new Vector2(0, -20));
            background.preserveAspect = true;
            CareUiSkin.Add(root, "Backpack hero icon", CareUiSkin.Art("icon_backpack"),
                new Vector2(180, 180), new Vector2(0, 310), Color.white).preserveAspect = true;
            CareRoundView.Label("Backpack title", root, "BAG", new Vector2(225, 72),
                new Vector2(0, 205), 66, TextAnchor.MiddleCenter, new Color(.47f, .25f, .12f));
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 5; column++)
                    CareUiSkin.Add(root, "Backpack slot " + (row * 5 + column), CareUiSkin.Art("slot_empty"),
                        new Vector2(114, 114), new Vector2(-248 + column * 124, 75 - row * 124), Color.white);
            selector = CareUiSkin.Add(root, "Selected slot glow", CareUiSkin.Art("slot_selected"),
                new Vector2(114, 114), new Vector2(-248, 75), Color.white);
            selector.gameObject.SetActive(false);
            bananaIcon = CareUi.Image("Banana item", root, CareUiSkin.Art("icon_banana"),
                new Vector2(99, 99), new Vector2(-248, 75));
            bananaIcon.preserveAspect = true;
            var countBadge = CareUi.Image("Banana count badge", root, CareUiSkin.Art("star_gold"),
                new Vector2(48, 48), new Vector2(-215, 42));
            count = Text("10", countBadge.transform, new Vector2(42, 42), Vector2.zero,
                27, TextAnchor.MiddleCenter, new Color(.30f, .18f, .08f, 1f));
            confirmKey = CareUiSkin.KeyBadge(root, CareInput.ConfirmLabel, new Vector2(72, 72),
                new Vector2(-254, -290), out confirmButtonLabel, 43, "Confirm key");
            actionLabel = CareRoundView.Label("Backpack action", root, "SELECT", new Vector2(165, 50),
                new Vector2(-133, -290), 38, TextAnchor.MiddleCenter, new Color(.47f, .25f, .12f));
            backKey = CareUiSkin.KeyBadge(root, CareInput.BackLabel, new Vector2(72, 72),
                new Vector2(13, -290), out backButtonLabel, 43, "Back key");
            CareRoundView.Label("Backpack close caption", root, "CLOSE BAG", new Vector2(234, 50),
                new Vector2(173, -290), 38, TextAnchor.MiddleCenter, new Color(.47f, .25f, .12f));
            status = Text(string.Empty, root, new Vector2(650, 42), new Vector2(0, -350),
                25, TextAnchor.MiddleCenter, new Color(.40f, .25f, .14f, 1f));

            // The bag entry button never disappears once the round allows it:
            // the old "gone for good after the first selection" behaviour read
            // as a bug (close the bag or grab a banana and the button vanished).
            // Keep the corner entry alive for the whole round instead.
            if (CarePlatform.FlatUi)
            {
                // PC flat layout: a screen-space plate pinned to the lower-left
                // corner, tucked further left than the old world-space copy so
                // it reads as part of the flat HUD chrome.
                var flatRoot = CareUi.OverlayRoot("Care PC Bag Root", 55);
                bagButton = CareUi.Plate("Care Backpack HUD", flatRoot,
                    new Vector2(-35, -45), new Vector2(190, 220),
                    new Vector2(1f, 1f), new Vector2(1f, 1f));
                // The corner entry used to be scaled to .72 of an already small
                // plate, which left the bag icon at roughly 3.5% of the screen
                // width. .9 with the larger plate keeps it a comfortable,
                // readable HUD target without crowding the play area.
                bagButton.localScale = Vector3.one * .9f;
            }
            else
            {
                bagButton = CareUi.Canvas("Care Backpack HUD", head,
                    new Vector3(.73f, .26f, 1.18f), .20f, new Vector2(400, 240));
            }
            CareUiSkin.Add(bagButton, "Bag icon", CareUiSkin.Art("icon_backpack"),
                new Vector2(180, 180), new Vector2(0, 10), Color.white);
            // A thin white ring under the key cap separates it from the brown
            // bag artwork and the grass behind it (the badge used to sit on a
            // dark translucent plate that read as a muddy halo).
            CareUiSkin.Add(bagButton, "Bag key ring", CareUiSkin.KeyCapSprite,
                new Vector2(88, 88), new Vector2(0, -82), Color.clear);
            var bagBadge = bagKeyBackdrop = CareUiSkin.KeyBadge(bagButton, CareInput.BackLabel, new Vector2(74, 74),
                new Vector2(0, -82), out bagButtonLabel, 52, "Bag key");
            // Draw the cap above the icon so the badge never looks clipped.
            bagBadge.sprite = CareUiSkin.Art("bag_key_red");
            bagButtonLabel.font = Resources.Load<Font>("Fonts/Arial-Bold");
            bagButtonLabel.fontStyle = FontStyle.Normal;
            bagBadge.transform.SetAsLastSibling();

            count.name = "Backpack quantity";
            status.name = "Backpack status";
            CareUiCustomization.Bind("Backpack", root);
            CareUiCustomization.Bind("Bag entry", bagButton);
            root.gameObject.SetActive(false);
            bagButton.gameObject.SetActive(false);
            Refresh(owner.Round);
        }

        public void Tick(CareInputSnapshot input, CareRound round, bool mayOpen)
        {
            // The corner entry stays for the whole round: closing the bag or
            // grabbing the first banana must not destroy or hide it.
            bool showBagButton = mayOpen && !IsOpen;
            if (bagButton.gameObject.activeSelf != showBagButton)
                bagButton.gameObject.SetActive(showBagButton);
            bagButtonLabel.text = CareInput.BackLabel;
            bagKeyBackdrop.sprite = CareUiSkin.Art("bag_key_red");
            bool canOpen = mayOpen;
            if (input.BWasPressed && (IsOpen || canOpen)) Toggle(round, canOpen);
            if (!IsOpen) return;
            Refresh(round);
            selector.gameObject.SetActive(BananaSelected);
            float pulse = .38f + .18f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 4f));
            selector.color = new Color(1f, 1f, 1f, .75f + pulse * .25f);

            if (!input.AWasPressed) return;
            if (BananaSelected)
            {
                CancelSelection();
                Refresh(round);
                return;
            }
            if (round == null || round.Bananas <= 0) return;

            autoEquip = true;
            if (session.EquipBananaFromGlove()) Close();
            else
            {
                autoEquip = false;
                Refresh(round);
                status.text = "Banana is returning to the glove. Try again.";
            }
        }

        void Toggle(CareRound round, bool mayOpen)
        {
            // Opening and closing the bag is a button press too: one click for
            // each, so the corner chip never feels dead.
            session.audioFeedback?.Click();
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
            bagKeyBackdrop.sprite = CareUiSkin.Art("bag_key_red");
            bagButton.gameObject.SetActive(true);
        }

        public void HideHud()
        {
            if (!IsOpen) bagButton.gameObject.SetActive(false);
        }

        // The headset can be put on (or taken off) after Play started: the
        // session rebuilds the whole HUD family for the other layout, so the
        // bag plates (screen-space on PC, world-space in VR) must follow.
        public void DestroyPlates()
        {
            if (root) UnityEngine.Object.Destroy(root.gameObject);
            if (bagButton)
            {
                var flatRoot = bagButton.parent;
                UnityEngine.Object.Destroy(bagButton.gameObject);
                // The flat variant owns a whole overlay canvas; remove it too
                // or an empty ScreenSpaceOverlay root lingers over VR.
                if (flatRoot && flatRoot.name == "Care PC Bag Root")
                    UnityEngine.Object.Destroy(flatRoot.gameObject);
            }
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
            autoEquip = false;
            session.CancelSelectedBanana();
        }

        public void ClearSelection() => autoEquip = false;

        public void Reset(CareRound round)
        {
            // The auto-equip unlock survives a restart: the bag is a one-time
            // lesson, not a per-round chore.
            Close();
            Refresh(round);
        }

        void Refresh(CareRound round)
        {
            int bananas = round != null ? round.Bananas : 0;
            count.text = bananas.ToString();
            confirmButtonLabel.text = CareInput.ConfirmLabel;
            backButtonLabel.text = CareInput.BackLabel;
            confirmKey.gameObject.SetActive(true);
            backKey.gameObject.SetActive(true);
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
            label.fontStyle = FontStyle.Normal;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.text = value;
            return CareRoundView.Legible(label, fontSize);
        }
    }
}


