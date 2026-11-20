using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    // A small chip in the corner of the HUD that swaps the key-cap glyphs
    // between Xbox letters (A/B) and PlayStation shapes (✕/○). It only changes
    // the letters: layout, positions, sizes, colours of everything else stay
    // exactly as they were. The chip auto-picks the style of whatever pad is
    // connected when it first appears, and can be clicked to flip.
    public sealed class CarePadStyleToggle : MonoBehaviour
    {
        Text label;
        Image background;

        public static CarePadStyleToggle Create(Transform parent, Vector2 pos, Vector2 size, int fontSize)
        {
            var host = new GameObject("Pad style toggle", typeof(RectTransform));
            host.transform.SetParent(parent, false);
            var toggle = host.AddComponent<CarePadStyleToggle>();
            var rect = (RectTransform)host.transform;
            toggle.background = host.AddComponent<Image>();
            toggle.background.sprite = CareUiSkin.KeyCapSprite;
            toggle.background.type = Image.Type.Sliced;
            toggle.background.color = new Color(.13f, .17f, .24f, .82f);
            toggle.background.raycastTarget = true;
            toggle.label = CareRoundView.Label("Pad style label", rect, string.Empty,
                size * .94f, Vector2.zero, fontSize, TextAnchor.MiddleCenter, Color.white);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var button = host.AddComponent<Button>();
            button.targetGraphic = toggle.background;
            button.onClick.AddListener(toggle.Flip);
            toggle.Refresh();
            CareInput.LabelsChanged += toggle.Refresh;
            return toggle;
        }

        void Flip()
        {
            var next = CareInput.PadStyle == CareInput.PadLayout.Xbox
                ? CareInput.PadLayout.PlayStation
                : CareInput.PadLayout.Xbox;
            // Pinning keeps the auto-detector from overwriting the user's
            // choice the next time a device connects or disconnects.
            CareInput.PinStyle(next);
            Refresh();
        }

        void Refresh()
        {
            if (!label) return;
            bool ps = CareInput.PadStyle == CareInput.PadLayout.PlayStation;
            label.text = ps ? "✕ ○  PS" : "A B  XBOX";
        }

        // Clicks come through the raycaster; also flip on the pad's Select
        // button (Share/Create) so it works without a mouse.
        void Update()
        {
            var pad = Gamepad.current;
            if (pad != null && pad.selectButton.wasPressedThisFrame) Flip();
        }
    }
}
