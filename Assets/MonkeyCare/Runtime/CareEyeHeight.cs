using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    // The view is locked at a fixed standing height above the ground, exactly as
    // requested: the eye is held 1.6m over the walkable floor, every frame, no
    // matter what moves the rig (a button, a teleport ray, a stick push, a
    // recenter). The correction is applied by shifting the rig root so it works
    // for both device-level and floor-level tracking origins and for any rig
    // hierarchy, because the head pose is always relative to the rig root.
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public sealed class CareEyeHeight : MonoBehaviour
    {
        public const float StandingEyeHeight = 1.6f;
        // The ground itself may rise/fall gently while walking; it is followed at
        // walking speed rather than snapped so slopes never feel like teleports.
        public const float FloorFollowSpeed = 6f;
        public const float FloorStepLimit = 1.2f;

        public static bool Active { get; private set; }

        OVRCameraRig rig;
        float floorY;
        bool floorKnown;

        public float FloorY => floorY;
        public float EyeAboveFloor => rig && rig.centerEyeAnchor
            ? rig.centerEyeAnchor.position.y - floorY : 0f;

        public void Begin(OVRCameraRig value)
        {
            rig = value;
            floorKnown = false;
        }

        void OnDisable()
        {
            Active = false;
            floorKnown = false;
        }

        void LateUpdate()
        {
            Active = false;
            if (!rig || !rig.centerEyeAnchor || !rig.gameObject.activeInHierarchy) return;
            Active = true;

            Vector3 head = rig.centerEyeAnchor.position;
            if (!TryGround(head, out float ground))
            {
                if (!floorKnown) return;
                ground = floorY;
            }
            if (!floorKnown)
            {
                floorY = ground;
                floorKnown = true;
            }
            else
            {
                float step = Mathf.Clamp(ground - floorY, -FloorStepLimit, FloorStepLimit);
                floorY = Mathf.MoveTowards(floorY, floorY + step, FloorFollowSpeed * Time.deltaTime);
            }

            // Move the whole rig so the eye lands exactly on floor + eye height.
            // No origin-mode branching: this is correct for every rig layout.
            float delta = (floorY + StandingEyeHeight) - head.y;
            if (Mathf.Abs(delta) > .0005f)
            {
                Vector3 position = rig.transform.position;
                position.y += delta;
                rig.transform.position = position;
            }

            // Artificial vertical offsets written into the tracking space lift the
            // view even when the rig root is pinned; keep it at its base height.
            if (rig.trackingSpace)
            {
                Vector3 local = rig.trackingSpace.localPosition;
                if (Mathf.Abs(local.y) > .001f)
                {
                    local.y = 0f;
                    rig.trackingSpace.localPosition = local;
                }
            }
        }

        bool TryGround(Vector3 head, out float ground)
        {
            ground = floorY;
            float ceiling = floorKnown ? floorY + FloorStepLimit : head.y + 1.5f;
            Vector3 origin = new(head.x, Mathf.Max(head.y, floorKnown ? floorY : head.y) + 60f, head.z);
            bool found = false;
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 300f,
                         ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < .5f) continue;
                if (hit.transform.IsChildOf(rig.transform)) continue;
                if (hit.collider.GetComponentInParent<MonkeyCareNpc>()) continue;
                if (hit.collider.GetComponentInParent<CareItem>()) continue;
                if (hit.point.y > ceiling) continue;
                if (hit.point.y <= best) continue;
                best = hit.point.y;
                found = true;
            }
            if (!found && Terrain.activeTerrain)
            {
                best = Terrain.activeTerrain.SampleHeight(new Vector3(head.x, 0f, head.z)) +
                       Terrain.activeTerrain.transform.position.y;
                found = best <= ceiling + .5f;
            }
            if (found) ground = best;
            return found;
        }
    }
}
