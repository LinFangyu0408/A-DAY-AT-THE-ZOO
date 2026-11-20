using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    // Proactive "where is the banana stand" hint.
    //
    // The stall sits behind the player's opening view, so anyone who turns
    // around has no idea where to walk. This plate appears at the bottom of the
    // view as soon as the player swings the camera and the stall is outside the
    // forward cone, points at it with a rotating arrow and shows the distance.
    // It also comes back on its own when the bag runs low.
    public sealed class CareDirectionHint : MonoBehaviour
    {
        // Half angle of the "looks like you can see it" cone.
        public const float ViewConeDegrees = 55f;
        // Turning this many degrees (accumulated, with cooling) counts as
        // "the player is looking around and may be lost".
        public const float TurnTriggerDegrees = 35f;
        public const float HoldSeconds = 4.5f;
        public const float IntroSeconds = 6f;
        public const float LowStockBananas = 2;

        MonkeyCareSession session;
        Camera view;
        Transform target;
        RectTransform root;
        CanvasGroup group;
        Image arrow;
        Text label;
        float lastYaw, turnHeat, showUntil, fade;
        bool seeded;

        public bool Showing => root && root.gameObject.activeSelf;

        public static CareDirectionHint Create(Camera camera, MonkeyCareSession owner, Transform stand)
        {
            if (!camera || !owner) return null;
            var host = new GameObject("Care Direction Hint Host");
            host.transform.SetParent(camera.transform, false);
            var hint = host.AddComponent<CareDirectionHint>();
            hint.session = owner;
            hint.view = camera;
            hint.target = stand;
            hint.Build();
            return hint;
        }

        void Build()
        {
            // Bottom centre: high enough to stay clear of the backpack button,
            // low enough not to sit on top of the score plate.
            root = CareUi.Canvas("Care DIRECTION HINT", transform,
                new Vector3(0, -.235f, 1.15f), .34f, new Vector2(640, 150), 25);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
            CareUiSkin.AddSliced(root, "Hint plate shadow", CareUiSkin.Slot,
                new Vector2(614, 132), new Vector2(4, -10), new Color(.02f, .07f, .14f, .35f));
            CareUiSkin.AddSliced(root, "Hint plate", CareUiSkin.Slot,
                new Vector2(600, 126), Vector2.zero, new Color(.07f, .22f, .38f, .95f));
            arrow = CareUiSkin.Add(root, "Hint arrow", CareUiSkin.DownArrow,
                new Vector2(92, 92), new Vector2(-238, 0), new Color(1f, .72f, .12f, 1f));
            arrow.preserveAspect = true;
            label = CareRoundView.Label("Hint text", root, "BANANA STAND 8m",
                new Vector2(470, 88), new Vector2(44, 0), 36,
                TextAnchor.MiddleLeft, Color.white);
            root.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!session || !view) return;
            var round = session.Round;
            bool live = session.Begun && round != null && !round.Frozen && MonkeyCareSession.Playing;
            if (!live || !target) { SetVisible(false); return; }
            // One short introduction as soon as play starts: the stall sits
            // behind the player, so say where it is before they wander off.
            if (!seeded)
            {
                seeded = true;
                showUntil = Time.unscaledTime + IntroSeconds;
            }

            Vector3 toStand = target.position - view.transform.position;
            Vector3 flatTo = Vector3.ProjectOnPlane(toStand, Vector3.up);
            Vector3 flatForward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up);
            if (flatTo.sqrMagnitude < .0001f || flatForward.sqrMagnitude < .0001f)
            { SetVisible(false); return; }
            float yaw = Vector3.SignedAngle(flatForward, flatTo, Vector3.up);
            float distance = flatTo.magnitude;
            bool inView = Mathf.Abs(yaw) <= ViewConeDegrees && distance <= 26f;

            // Accumulate how much the player has been turning. A deliberate swing
            // fires the hint; drifting the sticks slowly does not spam it.
            float delta = Mathf.DeltaAngle(lastYaw, view.transform.eulerAngles.y);
            lastYaw = view.transform.eulerAngles.y;
            turnHeat = Mathf.Max(0f, turnHeat + Mathf.Abs(delta) - Time.deltaTime * 25f);
            if (turnHeat >= TurnTriggerDegrees)
            {
                showUntil = Time.unscaledTime + HoldSeconds;
                turnHeat = 0f;
            }

            bool lowStock = round.Bananas <= LowStockBananas;
            bool show = !inView && (Time.unscaledTime < showUntil || lowStock);
            SetVisible(show);
            if (!show) return;

            // The arrow art points down; rotating it by the signed yaw aims it at
            // the stall (straight up means it is behind the player).
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, yaw);
            // One short line only: the plate is narrow and two lines clipped.
            int metres = Mathf.Max(1, Mathf.RoundToInt(distance));
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
