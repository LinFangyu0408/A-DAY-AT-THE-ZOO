using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    // The care rig runs on a device-level XR origin: the head pose sits on the
    // rig root (the eye is only ~2cm above it), which means the rig's Y *is* the
    // player's eye height. Nothing kept that in sync with the ground, so the eye
    // ended up half a metre above the grass and the yard was viewed as if the
    // player were sitting on the floor.
    //
    // This component owns the vertical axis while the care scene is running: the
    // eye is held a fixed standing height above the walkable ground, so no input
    // path can lift, jump or climb. Props (monkeys, the stall, the fence) are
    // ignored by the ground probe, which is what used to ratchet the player up.
    [DefaultExecutionOrder(200), DisallowMultipleComponent]
    public sealed class CareEyeHeight : MonoBehaviour
    {
        public const float StandingEyeHeight = 1.58f;
        // Ground is followed at walking speed, never teleported.
        public const float FloorFollowSpeed = 6f;
        // How far the floor may jump between two frames (a step or a slope).
        public const float FloorStepLimit = 1.1f;
        // True while this component is the one placing the eye; the fence skips
        // its own vertical clamp then so the two never fight.
        public static bool Active { get; private set; }

        OVRCameraRig rig;
        float floorY;
        bool floorKnown;

        public float FloorY => floorY;
        public bool Driving => Active;
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

            // A floor-level origin already reports the head ~1.6m above the rig;
            // only the device-level rig needs the correction.
            float eyeOffset = rig.centerEyeAnchor.position.y - rig.transform.position.y;
            if (eyeOffset >= .8f) return;

            Active = true;
            Vector3 head = rig.centerEyeAnchor.position;
            if (!TryGround(head, out float ground))
            {
                if (!floorKnown) return;
                ground = floorY;
            }

            Vector3 position = rig.transform.position;
            if (!floorKnown)
            {
                floorY = ground;
                floorKnown = true;
                position.y = floorY + StandingEyeHeight;
                rig.transform.position = position;
                return;
            }

            floorY = Mathf.MoveTowards(floorY, ground, FloorFollowSpeed * Time.deltaTime);
            float wanted = floorY + StandingEyeHeight;
            if (Mathf.Abs(position.y - wanted) > .001f)
            {
                position.y = Mathf.MoveTowards(position.y, wanted, 12f * Time.deltaTime);
                rig.transform.position = position;
            }
        }

        bool TryGround(Vector3 head, out float ground)
        {
            ground = floorY;
            float ceiling = floorKnown ? floorY + FloorStepLimit : head.y + .6f;
            Vector3 origin = new(head.x, Mathf.Max(head.y, floorKnown ? floorY : head.y) + 40f, head.z);
            bool found = false;
            float best = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 200f,
                         ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < .6f) continue;
                if (hit.transform.IsChildOf(rig.transform)) continue;
                if (hit.collider.GetComponentInParent<MonkeyCareNpc>()) continue;
                if (hit.collider.GetComponentInParent<CareItem>()) continue;
                if (hit.point.y > ceiling) continue;
                if (hit.point.y <= best) continue;
                best = hit.point.y;
                found = true;
            }
            if (found) ground = best;
            return found;
        }
    }
}
