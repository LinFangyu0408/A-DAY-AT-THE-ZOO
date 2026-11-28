using UnityEngine;

namespace ADayAtTheZoo.PC
{
    public sealed partial class ZooDayTwoNpc
    {
        public bool roams;
        [Range(.2f, .8f)] public float walkSpeed = .5f;
        public float roamRadius = 1.3f;
        public bool IsWalking { get; private set; }
        public float DistanceWalked { get; private set; }
        public Vector3 Home { get; private set; }
        Vector3 destination;
        int waypoint;
        float idleUntil, heldUntil;
        static readonly Vector2[] Route = { new(.95f, .1f), new(.15f, .75f), new(-.95f, -.1f), new(-.15f, -.75f) };

        void InitializeRoaming()
        {
            Home = transform.position;
            waypoint = index % Route.Length;
            SetDestination();
        }

        public void HoldForCare(float seconds) => heldUntil = Mathf.Max(heldUntil, Time.time + seconds);

        void SetDestination()
        {
            var offset = Route[waypoint % Route.Length] * roamRadius;
            destination = Home + new Vector3(offset.x, 0, offset.y);
        }

        void UpdateRoaming(ZooDayTwoRound.Animal animal)
        {
            IsWalking = false;
            if (!session.IsRunning) return;
            Vector3 toKeeper = session.player.transform.position - transform.position; toKeeper.y = 0;
            bool receivingCare = animal.state == ZooCareState.Eating || animal.state == ZooCareState.Celebrating || Time.time < heldUntil;
            bool nearby = toKeeper.sqrMagnitude < 3.6f * 3.6f;
            if (receivingCare || nearby || !roams)
            {
                if (nearby && animal.state != ZooCareState.Celebrating && toKeeper.sqrMagnitude > .1f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toKeeper), 125 * Time.deltaTime);
                return;
            }
            if (Time.time < idleUntil) return;
            Vector3 delta = destination - transform.position; delta.y = 0;
            if (delta.magnitude < .09f) { waypoint++; SetDestination(); idleUntil = Time.time + 1.2f + (index % 3) * .35f; return; }
            Vector3 direction = delta.normalized;
            // Kinematic stroll on the actual terrain, with a conservative body sweep.
            // Nearby keepers and animals block movement; no navigation changes to Day 01.
            float step = Mathf.Min(walkSpeed * Time.deltaTime, delta.magnitude);
            foreach (var hit in Physics.SphereCastAll(transform.position + Vector3.up * .7f, .33f, direction, step + .12f, ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.normal.y > .65f) continue;
                waypoint++; SetDestination(); idleUntil = Time.time + .6f; return;
            }
            Vector3 next = transform.position + direction * step;
            if (!TryGround(next, out float ground)) { waypoint++; SetDestination(); idleUntil = Time.time + .6f; return; }
            next.y = ground + .025f;
            if (Mathf.Abs(next.y - transform.position.y) > .28f) { waypoint++; SetDestination(); idleUntil = Time.time + .6f; return; }
            DistanceWalked += step; transform.position = next;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 105 * Time.deltaTime);
            IsWalking = true;
        }

        bool TryGround(Vector3 at, out float height)
        {
            height = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 1.1f, Vector3.down, 2.2f, ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < .75f || hit.collider.GetComponentInParent<ZooDayTwoNpc>() || hit.collider.GetComponentInParent<CharacterController>()) continue;
                height = Mathf.Max(height, hit.point.y);
            }
            return !float.IsNegativeInfinity(height);
        }
    }
}
