using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    // "Where is the banana stand" pointer.
    //
    // The stall sits behind the player's opening view. Instead of a plate with a
    // dark blue backing (which read as a floating slab in VR), the hint is now a
    // bare red arrow plus one line of outlined text. It only appears when the
    // bag is nearly empty, because that is the only moment the stand matters.
    public sealed class CareDirectionHint : MonoBehaviour
    {
        // Half angle of the "looks like you can see it" cone.
        public const float ViewConeDegrees = 55f;
        // Show the pointer only at or below this many bananas.
        public const float LowStockBananas = 3;

        MonkeyCareSession session;
        Transform head;
        Transform target;
        RectTransform root;
        CanvasGroup group;
        Image arrow;
        Text label;
        float fade;
        static Sprite arrowSprite;

        public bool Showing => root && root.gameObject.activeSelf;

        public static CareDirectionHint Create(Transform head, MonkeyCareSession owner, Transform stand)
        {
            if (!head || !owner) return null;
            var host = new GameObject("Care Direction Hint Host");
            host.transform.SetParent(head, false);
            var hint = host.AddComponent<CareDirectionHint>();
            hint.session = owner;
            hint.head = head;
            hint.target = stand;
            hint.Build();
            return hint;
        }

        void Build()
        {
            // Bottom centre: high enough to stay clear of the backpack button,
            // low enough not to sit on top of the score plate. No plate sprite
            // and no shadow: only the arrow and the outlined label remain.
            root = CareUi.Canvas("Care DIRECTION HINT", transform,
                new Vector3(0, -.245f, 1.12f), .52f, new Vector2(780, 160), 25);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
            arrow = CareUiSkin.Add(root, "Hint arrow", ArrowSprite(),
                new Vector2(118, 118), new Vector2(-296, 0), new Color(1f, .18f, .16f, 1f));
            arrow.preserveAspect = true;
            label = CareRoundView.Label("Hint text", root, "BANANA STAND 8m",
                new Vector2(580, 108), new Vector2(52, 0), 52,
                TextAnchor.MiddleLeft, new Color(1f, .98f, .95f, 1f));
            root.gameObject.SetActive(false);
        }

        // A white triangle generated at runtime. The imported arrow artwork is a
        // dark silhouette, so tinting it red only produced black; a generated
        // white shape takes the colour cleanly at any size.
        static Sprite ArrowSprite()
        {
            if (arrowSprite) return arrowSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Care Hint Arrow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y + .5f) / size;
                // Apex at the bottom centre, base across the top.
                float t = Mathf.InverseLerp(.12f, .90f, ny);
                float halfWidth = Mathf.Lerp(0f, .46f, t);
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + .5f) / size;
                    float distance = Mathf.Abs(nx - .5f) - halfWidth;
                    float alpha = ny < .10f ? 0f : Mathf.Clamp01(.5f - distance * size / 1.6f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            arrowSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(.5f, .5f), 100f);
            arrowSprite.name = "Care Hint Arrow";
            return arrowSprite;
        }

        void Update()
        {
            if (!session || !head) return;
            var round = session.Round;
            bool live = session.Begun && round != null && !round.Frozen && MonkeyCareSession.Playing;
            if (!live || !target) { SetVisible(false); return; }

            // Low stock is the only trigger: the pointer is a supply cue, not a
            // permanent compass.
            if (round.Bananas > LowStockBananas) { SetVisible(false); return; }

            Vector3 toStand = target.position - head.position;
            Vector3 flatTo = Vector3.ProjectOnPlane(toStand, Vector3.up);
            Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (flatTo.sqrMagnitude < .0001f || flatForward.sqrMagnitude < .0001f)
            { SetVisible(false); return; }
            float yaw = Vector3.SignedAngle(flatForward, flatTo, Vector3.up);
            float distance = flatTo.magnitude;
            // Facing the stall already: the pointer would only repeat what the
            // player can see.
            bool inView = Mathf.Abs(yaw) <= ViewConeDegrees;
            if (inView) { SetVisible(false); return; }

            SetVisible(true);
            // The arrow art points down; rotating it by the signed yaw aims it at
            // the stall (straight up means it is behind the player).
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, yaw);
            int metres = Mathf.Max(1, Mathf.RoundToInt(distance));
            // One short line only: the pointer must be readable at a glance and
            // must never wrap.
            label.text = Mathf.Abs(yaw) >= 125f
                ? "TURN AROUND · " + metres + "m"
                : "BANANA STAND · " + metres + "m";
        }

        void SetVisible(bool show)
        {
            if (!root) return;
            float goal = show ? 1f : 0f;
            fade = Mathf.MoveTowards(fade, goal, Time.unscaledDeltaTime * 6f);
            group.alpha = fade;
            if (root.gameObject.activeSelf != (fade > .001f))
                root.gameObject.SetActive(fade > .001f);
        }
    }
}
