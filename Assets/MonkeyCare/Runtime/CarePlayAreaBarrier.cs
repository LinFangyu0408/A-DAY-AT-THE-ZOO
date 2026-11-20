using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    // Invisible fence around the feeding yard.
    //
    // The monkeys and the banana stand used to have nothing behind them: a
    // player could keep walking past the animals and the stall, fall off the
    // flattened play strip and lose the whole scene. This component builds a
    // ring of solid, renderer-less boxes just outside the outermost monkey / the
    // stand, and additionally clamps the player root so VR stick movement (which
    // moves the tracking space and ignores colliders) cannot slip through.
    [DisallowMultipleComponent]
    public sealed class CarePlayAreaBarrier : MonoBehaviour
    {
        public const float MonkeyMargin = 2.4f;
        public const float StandMargin = 3.2f;
        public const float MinRadius = 8.5f;
        public const float MaxRadius = 16f;
        public const float WallHeight = 7f;
        public const float WallThickness = .6f;
        // How far inside the wall the soft clamp keeps the player.
        public const float ClampInset = .55f;
        // The yard is a flattened strip, so the root must stay on the floor it
        // started on. A few centimetres of tolerance absorb uneven ground, and
        // anything above that (a stuck button, a teleport ray, a stick push into
        // a slope) is pulled straight back down: this mode is walk-only.
        public const float MaxRise = .15f;
        public const float MaxDrop = .9f;
        // Artificial offsets written into the tracking space lift the view even
        // when the rig root itself is pinned, so they are clamped as well.
        public const float TrackingTolerance = .06f;

        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }
        public int Segments { get; private set; }
        public float StartY => startY;

        MonkeyCareSession session;
        Transform playerRoot;
        Camera view;
        float startY;
        bool startCaptured;
        bool trackingCaptured;
        float trackingBaseY;

        public static CarePlayAreaBarrier Create(MonkeyCareSession owner, Camera camera, Transform stand)
        {
            if (!camera) return null;
            var host = new GameObject("Care Play Area Barrier");
            var barrier = host.AddComponent<CarePlayAreaBarrier>();
            barrier.session = owner;
            barrier.view = camera;
            barrier.playerRoot = owner ? owner.PlayerRoot : null;
            barrier.startY = barrier.playerRoot ? barrier.playerRoot.position.y : camera.transform.position.y;
            barrier.Build(stand);
            return barrier;
        }

        void Build(Transform stand)
        {
            Center = new Vector3(view.transform.position.x, view.transform.position.y,
                view.transform.position.z);
            float wanted = MinRadius;
            if (session && session.monkeys != null)
            {
                foreach (var npc in session.monkeys)
                {
                    if (!npc) continue;
                    wanted = Mathf.Max(wanted, FlatDistance(npc.transform.position) + MonkeyMargin);
                }
            }
            if (stand)
                wanted = Mathf.Max(wanted, FlatDistance(stand.position) + StandMargin);
            Radius = Mathf.Clamp(wanted, MinRadius, MaxRadius);

            // Enough facets that the player never notices a corner poking in,
            // and a little overlap so no seam can be squeezed through.
            Segments = Mathf.Clamp(Mathf.RoundToInt(Radius * 2.2f), 18, 32);
            float segmentWidth = 2f * Mathf.PI * Radius / Segments * 1.18f;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                Vector3 direction = new(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 point = Center + direction * Radius;
                float ground = GroundAt(point);
                var wall = new GameObject("Barrier Wall " + (i + 1));
                wall.transform.SetParent(transform, false);
                wall.transform.SetPositionAndRotation(
                    new Vector3(point.x, ground + WallHeight * .5f - 1.2f, point.z),
                    Quaternion.LookRotation(direction, Vector3.up));
                var box = wall.AddComponent<BoxCollider>();
                box.size = new Vector3(segmentWidth, WallHeight, WallThickness);
            }
        }

        float FlatDistance(Vector3 world)
        {
            Vector2 delta = new(world.x - Center.x, world.z - Center.z);
            return delta.magnitude;
        }

        void LateUpdate()
        {
            if (!view) view = MonkeyCareSession.PlayerCamera;
            if (!view) return;
            // Stick movement translates the rig (VR) or the character controller
            // (desktop); both ignore collision, so the clamp is what actually
            // stops the player at the fence.
            Transform root = playerRoot;
            bool rigLive = session && session.rig && session.rig.gameObject.activeInHierarchy;
            if (rigLive) root = session.rig.transform;
            else if (root && !view.transform.IsChildOf(root)) root = view.transform.root;
            Transform target = root ? root : view.transform;

            // The floor height must come from the rig root, never from the eye:
            // capturing it off the camera would lock the player 1.7m up.
            if (!startCaptured)
            {
                if (rigLive || playerRoot)
                {
                    startY = (rigLive ? root : playerRoot).position.y;
                    startCaptured = true;
                }
                else return;
            }

            // Vertical guard runs before the horizontal early-out so a floating
            // player is pulled back down even while standing inside the fence.
            GuardVertical(target);

            Vector3 flat = new(view.transform.position.x - Center.x, 0f,
                view.transform.position.z - Center.z);
            float distance = flat.magnitude;
            float limit = Radius - ClampInset;
            if (distance <= limit || distance < .0001f) return;
            target.position += -flat / distance * (distance - limit);
        }

        void GuardVertical(Transform target)
        {
            // In VR the eye height component owns the vertical axis (it places the
            // eye at a fixed standing height over the ground), so the fence only
            // backs it up on the desktop path. Checking the live rig instead of
            // the component's flag keeps this independent of execution order.
            bool eyeDriven = session && session.rig && session.rig.gameObject.activeInHierarchy;
            if (!eyeDriven)
            {
                ClampY(target);
                // Locomotion moves ITS OWN transform (the rig is usually a child of
                // "Player and Locomotion"), so pinning only the rig would let the
                // parent drift upwards every frame while the child compensated.
                if (session && session.locomotion && session.locomotion.transform != target)
                    ClampY(session.locomotion.transform);
            }

            // Locomotion and recentering can offset the tracking space instead
            // of the root; clamping it keeps the eye at standing height.
            if (!session || !session.rig || !session.rig.trackingSpace) return;
            Transform space = session.rig.trackingSpace;
            Vector3 local = space.localPosition;
            if (!trackingCaptured)
            {
                trackingBaseY = local.y;
                trackingCaptured = true;
            }
            float wanted = Mathf.Clamp(local.y, trackingBaseY - TrackingTolerance,
                trackingBaseY + TrackingTolerance);
            if (Mathf.Abs(local.y - wanted) > .0005f)
            {
                local.y = wanted;
                space.localPosition = local;
            }
        }

        void ClampY(Transform target)
        {
            if (!target) return;
            Vector3 guarded = target.position;
            float clamped = Mathf.Clamp(guarded.y, startY - MaxDrop, startY + MaxRise);
            if (Mathf.Abs(guarded.y - clamped) > .0005f)
            {
                guarded.y = clamped;
                target.position = guarded;
            }
        }

        static float GroundAt(Vector3 near)
        {
            var origin = new Vector3(near.x, near.y + 120f, near.z);
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 240f,
                         ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y >= .45f) return hit.point.y;
            }
            var terrain = Terrain.activeTerrain;
            return terrain ? terrain.SampleHeight(near) + terrain.transform.position.y : near.y - 1.6f;
        }
    }
}
