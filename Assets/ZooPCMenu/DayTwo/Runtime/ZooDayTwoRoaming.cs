using UnityEngine;
using UnityEngine.AI;

namespace ADayAtTheZoo.PC
{
    public sealed partial class ZooDayTwoNpc
    {
        public bool roams;
        [Range(.2f, .8f)] public float walkSpeed = .5f;
        public float roamRadius = 6;
        public bool IsWalking { get; private set; }
        public float DistanceWalked { get; private set; }
        public float FarthestFromHome { get; private set; }
        public Vector3 Home { get; private set; }
        public bool OnWalkableGround => agent && agent.isOnNavMesh;
        NavMeshAgent agent;
        Vector3 previousPosition;
        int destinationNumber;
        float idleUntil, heldUntil, blockedTime;

        void InitializeRoaming()
        {
            Home = transform.position; previousPosition = Home;
            if (!session.navigation || !session.navigation.Ready) return;
            if (!NavMesh.SamplePosition(Home, out var spawn, .8f, NavMesh.AllAreas)) return;
            transform.position = spawn.position + Vector3.up * .025f;
            Home = previousPosition = transform.position;
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.agentTypeID = session.navigation.agentTypeID;
            agent.radius = .43f; agent.height = 1.5f; agent.baseOffset = .025f;
            agent.speed = walkSpeed; agent.acceleration = 2; agent.angularSpeed = 105;
            agent.updateRotation = false; agent.stoppingDistance = .12f;
            agent.avoidancePriority = 30 + index * 8;
            agent.isStopped = true;
        }

        public void HoldForCare(float seconds) => heldUntil = Mathf.Max(heldUntil, Time.time + seconds);

        bool PickDestination()
        {
            var path = new NavMeshPath();
            // Sample destinations across the clearing, not a tiny orbit at spawn.
            for (int attempt = 0; attempt < 32; attempt++)
            {
                float angle = (index * 103 + destinationNumber * 137.5f + attempt * 47) * Mathf.Deg2Rad;
                float distance = roamRadius * (.75f + .25f * ((attempt + destinationNumber) % 3) / 2);
                Vector3 candidate = Home + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                if (!session.navigation.Contains(candidate)) continue;
                if (!NavMesh.SamplePosition(candidate, out var hit, .65f, agent.areaMask)) continue;
                if (Mathf.Abs(hit.position.y - Home.y) > .7f || !session.navigation.Contains(hit.position)) continue;
                if (Vector3.Distance(hit.position, transform.position) < 3.5f || Vector3.Distance(hit.position, Home) > roamRadius + .1f) continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                agent.SetPath(path); destinationNumber++; blockedTime = 0; return true;
            }
            destinationNumber++; return false;
        }

        void UpdateRoaming(ZooDayTwoRound.Animal animal)
        {
            float moved = Vector3.Distance(previousPosition, transform.position);
            if (session.IsRunning) { DistanceWalked += moved; FarthestFromHome = Mathf.Max(FarthestFromHome, Vector3.Distance(Home, transform.position)); }
            previousPosition = transform.position;
            IsWalking = false;
            if (!agent || !agent.isOnNavMesh) return;
            Vector3 toKeeper = session.player.transform.position - transform.position; toKeeper.y = 0;
            bool receivingCare = animal.state == ZooCareState.Eating || animal.state == ZooCareState.Celebrating || Time.time < heldUntil;
            // Only requesting animals wait for an approaching keeper. Merely
            // passing a resting animal does not freeze the surrounding troop.
            bool nearby = animal.state == ZooCareState.Waiting && toKeeper.sqrMagnitude < 3.6f * 3.6f;
            bool stop = !session.IsRunning || !roams || receivingCare || nearby || Time.time < idleUntil;
            agent.isStopped = stop;
            if (stop)
            {
                if (session.IsRunning && (nearby || receivingCare) && animal.state != ZooCareState.Celebrating && toKeeper.sqrMagnitude > .1f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toKeeper), 125 * Time.deltaTime);
                return;
            }
            if (!agent.hasPath || (!agent.pathPending && agent.remainingDistance <= .2f))
            {
                bool arrived = agent.hasPath;
                agent.ResetPath();
                if (arrived) idleUntil = Time.time + 1.3f + index * .15f;
                if (!PickDestination()) idleUntil = Time.time + 1;
                return;
            }
            IsWalking = moved > .001f;
            blockedTime = IsWalking ? 0 : blockedTime + Time.deltaTime;
            if (blockedTime > 2) { agent.ResetPath(); PickDestination(); }
            Vector3 direction = agent.velocity; direction.y = 0;
            if (direction.sqrMagnitude > .001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 105 * Time.deltaTime);
        }
    }
}
