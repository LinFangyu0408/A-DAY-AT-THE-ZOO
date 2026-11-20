using System.Collections.Generic;
using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    [DefaultExecutionOrder(-30), DisallowMultipleComponent]
    public sealed class CareItem : MonoBehaviour
    {
        [InspectorName("物品用途")] public CareNeed kind;
        [InspectorName("补给间隔（秒）"), Min(.1f)] public float respawnSeconds = 2;
        [InspectorName("抓握位置偏移")] public Vector3 gripPosition = new Vector3(0, -.02f, .15f);
        [InspectorName("抓握旋转偏移")] public Vector3 gripRotation;
        [InspectorName("抓握缩放倍率"), Min(.01f)] public float heldScale = 1;
        [InspectorName("虚影材质")] public Material ghostMaterial;
        [InspectorName("虚影高度（米）")] public float ghostHeight = .23f;
        [InspectorName("虚影晃动幅度（米）")] public float ghostAmplitude = .04f;
        [InspectorName("虚影晃动速度")] public float ghostSpeed = 3;
        [InspectorName("手套模式")] public bool gloveMode;
        [InspectorName("未命中返回（秒）"), Min(.1f)] public float missedThrowSeconds = 2.5f;
        [InspectorName("默认投掷速度（米/秒）"), Min(.1f)] public float throwSpeed = 9;
        public bool Held { get; private set; }
        public bool Available { get; private set; } = true;
        public bool Caring { get; private set; }
        public bool Thrown { get; private set; }
        public MonkeyCareNpc Target { get; private set; }
        Vector3 homePosition, homeScale;
        Quaternion homeRotation;
        Transform homeParent;
        Rigidbody body;
        Renderer[] renderers;
        Collider[] colliders;
        GameObject ghost;
        float returnAt = float.PositiveInfinity;
        readonly HashSet<Collider> touching = new();
        readonly ScratchStroke stroke = new();
        MonkeyCareNpc previousScratchTarget;
        MonkeyCareNpc caughtTarget;
        MonkeyCareNpc assist;
        Vector3 flightFrom;
        float caughtAt;

        void Awake()
        {
            homeParent = transform.parent; homePosition = transform.position; homeRotation = transform.rotation; homeScale = transform.localScale;
            body = GetComponent<Rigidbody>(); renderers = GetComponentsInChildren<Renderer>(); colliders = GetComponentsInChildren<Collider>();
        }
        public bool Grab(Transform hand)
        {
            if (gloveMode || !Available || Held || !hand) return false;
            return Equip(hand);
        }
        public bool EquipFromGlove(Transform hand)
        {
            if (!gloveMode || !Available || Held || Thrown || !hand) return false;
            return Equip(hand);
        }
        bool Equip(Transform hand)
        {
            returnAt = float.PositiveInfinity;
            Held = true;
            Thrown = false;
            caughtTarget = null;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
            SetVisible(true, true);
            transform.SetParent(hand, true); transform.localPosition = gripPosition; transform.localEulerAngles = gripRotation;
            // Keep authored world scale when attaching to differently scaled rigs.
            Vector3 handScale = hand.lossyScale;
            transform.localScale = new Vector3(homeScale.x / handScale.x, homeScale.y / handScale.y, homeScale.z / handScale.z) * heldScale;
            touching.Clear(); stroke.Reset();
            if (!gloveMode) BuildGhost();
            MonkeyCareSession.Instance?.audioFeedback.Grab(hand, kind);
            return true;
        }
        public void Release()
        {
            if (!Held) return;
            Held = false; Caring = false; Target = null;
            caughtTarget = null;
            if (gloveMode) { RestoreSupply(); return; }
            transform.SetParent(homeParent, true); transform.localScale = homeScale;
            body.isKinematic = false; stroke.Reset(); previousScratchTarget = null;
            if (kind == CareNeed.Itch) returnAt = Time.time + respawnSeconds;
            if (ghost) Destroy(ghost);
        }
        public void ThrowAt(MonkeyCareNpc target)
        {
            if (!Held) return;
            Vector3 destination = target ? TargetPoint(target) : transform.position + transform.forward * 5;
            Vector3 origin = transform.position;
            Throw(BallisticVelocity(origin, destination, throwSpeed));
        }
        public static Vector3 BallisticVelocity(Vector3 origin, Vector3 destination, float speed)
        {
            // A longer allowed flight keeps the launch speed human: the old .7s
            // cap turned a ten-metre throw into a 20 m/s bullet that tunneled
            // straight through the monkey's small contact trigger.
            float flight = Mathf.Clamp(Vector3.Distance(origin, destination) / Mathf.Max(4f, speed),
                .25f, 1.05f);
            return (destination - origin) / flight - Physics.gravity * (.5f * flight);
        }
        public void Throw(Vector3 velocity) => Throw(velocity, null);
        public void Throw(Vector3 velocity, MonkeyCareNpc assistTarget)
        {
            if (!Held) return;
            Held = false; Caring = false; Target = null;
            caughtTarget = null;
            transform.SetParent(homeParent, true); transform.localScale = homeScale;
            transform.position += velocity.normalized * .08f;
            body.isKinematic = false;
            body.useGravity = true;
            // A hand-thrown banana covers ~25cm per physics step; without
            // continuous detection it passes through the small "Care Contact"
            // trigger and ends up hovering behind the monkey.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            body.angularVelocity = UnityEngine.Random.onUnitSphere * 7f;
            Thrown = true;
            flightFrom = transform.position;
            assist = assistTarget && assistTarget.Accepts(CareNeed.Hunger) ? assistTarget : null;
            caughtAt = 0f;
            returnAt = Time.time + missedThrowSeconds;
            if (ghost) Destroy(ghost);
            if (kind == CareNeed.Hunger) MonkeyCareSession.Instance?.ConsumeThrownBanana();
            MonkeyCareSession.Instance?.StopHaptics();
        }
        public void Consume()
        {
            if (!Available) return;
            Available = false; Held = false; Caring = false; Target = null;
            Thrown = false;
            caughtTarget = null;
            caughtAt = 0f;
            assist = null;
            transform.SetParent(homeParent, true); body.isKinematic = true; touching.Clear(); stroke.Reset();
            if (ghost) Destroy(ghost);
            SetVisible(false, false);
            returnAt = Time.time + respawnSeconds;
            MonkeyCareSession.Instance?.StopHaptics();
        }
        public void RestoreSupply()
        {
            if (Held) { Held = false; Caring = false; Target = null; }
            caughtTarget = null;
            caughtAt = 0f;
            assist = null;
            Thrown = false;
            returnAt = float.PositiveInfinity;
            Available = true; Caring = false; Target = null;
            Thrown = false;
            transform.SetParent(homeParent, true); transform.SetPositionAndRotation(homePosition, homeRotation); transform.localScale = homeScale;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            SetVisible(!gloveMode, !gloveMode);
            touching.Clear(); stroke.Reset();
        }
        public void EnterGloveSupply(Transform supplyRoot, Vector3 hiddenPosition)
        {
            gloveMode = true;
            homeParent = supplyRoot;
            homePosition = hiddenPosition;
            homeRotation = supplyRoot ? supplyRoot.rotation : Quaternion.identity;
            RestoreSupply();
        }
        void Update()
        {
            if (Thrown && Time.time >= returnAt)
            {
                RestoreSupply();
                return;
            }
            // A caught banana that the monkey never actually starts eating (its
            // task was already closed, the round froze, or the completion was
            // rejected) used to stay frozen in mid-air forever. Give it a moment
            // and hand it back to the player instead.
            if (caughtTarget && body.isKinematic && !caughtTarget.EatingNow &&
                caughtAt > 0f && Time.time - caughtAt > 1.2f)
            {
                RestoreSupply();
                return;
            }
            AssistFlight();
            if (!Held && (Time.time >= returnAt ||
                (!gloveMode && Available && (transform.position.y < -.4f || Vector3.Distance(transform.position, homePosition) > 45))))
            {
                RestoreSupply();
                return;
            }
            if (!Available) return;
            if (!MonkeyCareSession.Playing) { Caring = false; if (ghost) ghost.SetActive(false); return; }
            Target = ContactTarget() ?? caughtTarget;
            var session = MonkeyCareSession.Instance;
            if (!Target && Held && kind == CareNeed.Hunger && session && session.RayNpc && session.RayNpc.Accepts(kind)) Target = session.RayNpc;
            Caring = Target && Target.Accepts(kind) && (kind == CareNeed.Hunger || Held);
            if (Caring && kind == CareNeed.Hunger)
            {
                if (!Held && Thrown)
                {
                    if (!caughtTarget)
                    {
                        caughtTarget = Target;
                        CatchAtTarget();
                    }
                    Target.OfferFood(this);
                }
            }
            else if (Caring && kind == CareNeed.Itch)
            {
                if (previousScratchTarget != Target) stroke.Reset();
                previousScratchTarget = Target;
                if (session && session.trackingSpace && session.RightHand)
                {
                    float y = session.trackingSpace.InverseTransformPoint(session.RightHand.position).y;
                    if (stroke.Sample(y, true, Target.strokeDistance, Target.resetDistance)) Target.AddStroke(this);
                }
            }
            else if (Held && kind == CareNeed.Itch && session && session.trackingSpace && session.RightHand)
            {
                float y = session.trackingSpace.InverseTransformPoint(session.RightHand.position).y;
                stroke.Sample(y, false, previousScratchTarget ? previousScratchTarget.strokeDistance : .08f,
                    previousScratchTarget ? previousScratchTarget.resetDistance : .06f);
            }
            else { stroke.Reset(); previousScratchTarget = null; }
            if (ghost)
            {
                ghost.SetActive(Held && !Caring);
                ghost.transform.position = transform.position + Vector3.up * (ghostHeight + Mathf.Sin(Time.time * ghostSpeed) * ghostAmplitude);
                ghost.transform.rotation = transform.rotation;
            }
        }
        void CatchAtTarget()
        {
            // Keep the thrown state active while the monkey eats so each frame can
            // continue advancing satisfaction until Consume() clears the banana.
            returnAt = float.PositiveInfinity;
            caughtAt = Time.time;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            transform.position = TargetPoint(Target);
            transform.rotation = Quaternion.LookRotation(Target.transform.forward, Vector3.up);
        }
        // Fast throws can pass the small contact trigger between two physics
        // steps, and a throw aimed at a hungry monkey should never be lost to a
        // rail or a leaf on the way. This swept test plus the short-range snap
        // make the banana land in the monkey's hands whenever the reticle was on
        // it at release.
        void AssistFlight()
        {
            if (!Thrown || Held || caughtTarget) return;
            Vector3 now = transform.position;
            float step = Vector3.Distance(flightFrom, now);
            if (step > .0005f)
            {
                Vector3 direction = (now - flightFrom) / step;
                foreach (var hit in Physics.SphereCastAll(flightFrom, .12f, direction,
                             step + .12f, ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Collide))
                {
                    var npc = hit.collider.GetComponentInParent<MonkeyCareNpc>();
                    if (npc && npc != caughtTarget && npc.Accepts(kind))
                    {
                        touching.Add(hit.collider);
                        break;
                    }
                }
                flightFrom = now;
            }

            if (!assist || !assist.Accepts(kind)) return;
            if (Vector3.Distance(now, TargetPoint(assist)) > 1.5f) return;
            Target = assist;
            caughtTarget = assist;
            CatchAtTarget();
            assist.OfferFood(this);
        }
        MonkeyCareNpc ContactTarget()
        {
            touching.RemoveWhere(c => !c || !c.enabled || !c.gameObject.activeInHierarchy || !AcceptContact(c));
            MonkeyCareNpc nearest = null; float distance = float.PositiveInfinity;
            foreach (var c in touching)
            {
                var npc = c.GetComponentInParent<MonkeyCareNpc>();
                if (!npc || !npc.Accepts(kind)) continue;
                float d = (npc.transform.position - transform.position).sqrMagnitude;
                if (d < distance) { nearest = npc; distance = d; }
            }
            return nearest;
        }
        bool AcceptContact(Collider other) => other.GetComponentInParent<MonkeyCareNpc>() && (kind != CareNeed.Itch || (other is BoxCollider && other.isTrigger && other.name == "Care Contact"));
        void OnTriggerEnter(Collider other) { if (AcceptContact(other)) touching.Add(other); }
        void OnTriggerStay(Collider other) { if (AcceptContact(other)) touching.Add(other); }
        void OnTriggerExit(Collider other) { touching.Remove(other); }
        void OnCollisionEnter(Collision collision)
        {
            if (collision.collider && AcceptContact(collision.collider)) touching.Add(collision.collider);
        }
        static Vector3 TargetPoint(MonkeyCareNpc npc)
        {
            var contact = npc.transform.Find("Care Contact");
            var collider = contact ? contact.GetComponent<Collider>() : null;
            if (collider && collider.enabled)
            {
                Vector3 center = collider.bounds.center;
                // The contact box is only trusted when it actually hugs the
                // monkey: a zero-sized or stray trigger reports bounds at the
                // world origin, which froze the banana in mid-air.
                if ((center - npc.transform.position).sqrMagnitude < 2.25f &&
                    center.y > npc.transform.position.y - .5f &&
                    center.y < npc.transform.position.y + 3f)
                    return center;
            }
            return npc.transform.position + Vector3.up * .8f;
        }
        void SetVisible(bool visible, bool collide)
        {
            foreach (var r in renderers) if (r) r.enabled = visible;
            foreach (var c in colliders) if (c) c.enabled = collide;
        }
        void BuildGhost()
        {
            if (ghost) Destroy(ghost);
            ghost = new GameObject("Care Item Ghost"); ghost.layer = 2;
            ghost.transform.SetPositionAndRotation(transform.position, transform.rotation);
            ghost.transform.localScale = transform.lossyScale;
            foreach (var originalRenderer in renderers)
            {
                var source = originalRenderer.GetComponent<MeshFilter>();
                if (!source) continue;
                if (!source.sharedMesh || source.GetComponentInParent<ParticleSystem>()) continue;
                var part = new GameObject("Ghost mesh", typeof(MeshFilter), typeof(MeshRenderer)); part.layer = 2;
                part.transform.SetParent(ghost.transform, false);
                part.transform.localPosition = transform.InverseTransformPoint(source.transform.position);
                part.transform.localRotation = Quaternion.Inverse(transform.rotation) * source.transform.rotation;
                var sourceScale = source.transform.lossyScale; var rootScale = transform.lossyScale;
                part.transform.localScale = new Vector3(sourceScale.x / rootScale.x, sourceScale.y / rootScale.y, sourceScale.z / rootScale.z);
                part.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var r = part.GetComponent<MeshRenderer>(); var sourceRenderer = source.GetComponent<Renderer>();
                var mats = new Material[sourceRenderer ? sourceRenderer.sharedMaterials.Length : 1];
                for (int i = 0; i < mats.Length; i++) mats[i] = ghostMaterial;
                r.sharedMaterials = mats; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        public void ResetStroke() { stroke.Reset(); previousScratchTarget = null; }
        void OnDisable()
        {
            if (ghost) Destroy(ghost);
            touching.Clear(); Caring = false; MonkeyCareSession.Instance?.StopHaptics();
        }
    }
}


