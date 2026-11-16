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
        public bool Held { get; private set; }
        public bool Available { get; private set; } = true;
        public bool Caring { get; private set; }
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

        void Awake()
        {
            homeParent = transform.parent; homePosition = transform.position; homeRotation = transform.rotation; homeScale = transform.localScale;
            body = GetComponent<Rigidbody>(); renderers = GetComponentsInChildren<Renderer>(); colliders = GetComponentsInChildren<Collider>();
        }
        public bool Grab(Transform hand)
        {
            if (!Available || Held || !hand) return false;
            returnAt = float.PositiveInfinity;
            Held = true;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
            transform.SetParent(hand, true); transform.localPosition = gripPosition; transform.localEulerAngles = gripRotation;
            // Keep authored world scale when attaching to differently scaled rigs.
            Vector3 handScale = hand.lossyScale;
            transform.localScale = new Vector3(homeScale.x / handScale.x, homeScale.y / handScale.y, homeScale.z / handScale.z) * heldScale;
            touching.Clear(); stroke.Reset(); BuildGhost();
            MonkeyCareSession.Instance?.audioFeedback.Grab(hand, kind);
            return true;
        }
        public void Release()
        {
            if (!Held) return;
            Held = false; Caring = false; Target = null;
            transform.SetParent(homeParent, true); transform.localScale = homeScale;
            body.isKinematic = false; stroke.Reset(); previousScratchTarget = null;
            if (kind == CareNeed.Itch) returnAt = Time.time + respawnSeconds;
            if (ghost) Destroy(ghost);
        }
        public void Consume()
        {
            if (!Available) return;
            Available = false; Held = false; Caring = false; Target = null;
            transform.SetParent(homeParent, true); body.isKinematic = true; touching.Clear(); stroke.Reset();
            if (ghost) Destroy(ghost);
            foreach (var r in renderers) r.enabled = false;
            foreach (var c in colliders) c.enabled = false;
            returnAt = Time.time + respawnSeconds;
            MonkeyCareSession.Instance?.StopHaptics();
        }
        public void RestoreSupply()
        {
            if (Held) Release();
            returnAt = float.PositiveInfinity;
            Available = true; Caring = false; Target = null;
            transform.SetParent(homeParent, true); transform.SetPositionAndRotation(homePosition, homeRotation); transform.localScale = homeScale;
            body.isKinematic = true;
            foreach (var r in renderers) r.enabled = true;
            foreach (var c in colliders) c.enabled = true;
            touching.Clear(); stroke.Reset();
        }
        void Update()
        {
            if (!Held && (Time.time >= returnAt ||
                (Available && (transform.position.y < -.4f || Vector3.Distance(transform.position, homePosition) > 45))))
            {
                RestoreSupply();
                return;
            }
            if (!Available) return;
            if (!MonkeyCareSession.Playing) { Caring = false; if (ghost) ghost.SetActive(false); return; }
            Target = ContactTarget();
            var session = MonkeyCareSession.Instance;
            if (!Target && Held && kind == CareNeed.Hunger && session && session.RayNpc && session.RayNpc.Accepts(kind)) Target = session.RayNpc;
            Caring = Target && Target.Accepts(kind) && (kind == CareNeed.Hunger || Held);
            if (Caring && kind == CareNeed.Hunger) Target.OfferFood(this);
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


