using System.Collections.Generic;
using UnityEngine;

namespace ADayAtTheZoo.PC
{
    public sealed partial class ZooDayTwoSession
    {
        public bool ScratchInProgress => scratchTarget;
        public int ConfirmedScratchContacts { get; private set; }
        public float LastScratchContactDistance { get; private set; } = float.PositiveInfinity;
        public Vector3 LastScratchSurfacePoint { get; private set; }
        public Transform ScratchTip => scratchTip;
        Transform scratchTip;
        Vector3 scratchRestScale;
        ZooDayTwoNpc scratchTarget;
        float scratchAge;
        bool scratchAwarded;

        bool BeginScratch(ZooDayTwoNpc target)
        {
            if (!scratchTip || !target.TryScratchSurface(playerCamera.transform.position, playerCamera.transform.right, 0, out _, out _)) return false;
            scratchTarget = target; scratchAge = 0; scratchAwarded = false;
            target.HoldForCare(1.1f);
            return true;
        }

        void CancelScratch()
        {
            scratchTarget = null;
            if (!scratcherView) return;
            scratcherView.transform.localPosition = Vector3.zero;
            scratcherView.transform.localRotation = Quaternion.Euler(-15, 0, -30);
            scratcherView.transform.localScale = scratchRestScale;
        }

        // LateUpdate runs after the skeleton has animated: the claw tracks the
        // current skin surface, rather than the NPC's loose gameplay capsule.
        void UpdateScratchMotion()
        {
            if (!scratchTarget || !IsRunning) return;
            if (Vector3.Distance(playerCamera.transform.position, scratchTarget.AimPoint) > rules.scratchReach + .1f ||
                !ClearPath(playerCamera.transform.position, scratchTarget.AimPoint, scratchTarget)) { CancelScratch(); return; }
            scratchAge += Time.deltaTime;
            float scrub = Mathf.Sin(Mathf.Clamp01((scratchAge - .2f) / .28f) * Mathf.PI * 2) * .055f;
            if (!scratchTarget.TryScratchSurface(playerCamera.transform.position, playerCamera.transform.right, scrub, out var surface, out var normal)) { CancelScratch(); return; }
            float reach = scratchAge < .2f ? Mathf.SmoothStep(0, 1, scratchAge / .2f) : scratchAge < .48f ? 1 : 1 - Mathf.SmoothStep(0, 1, (scratchAge - .48f) / .18f);
            Transform tool = scratcherView.transform;
            Vector3 toolUp = (Vector3.up * .8f - normal * .6f).normalized;
            Quaternion contactRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(normal, toolUp), toolUp);
            tool.rotation = Quaternion.Slerp(heldRoot.rotation * Quaternion.Euler(-15, 0, -30), contactRotation, reach);
            tool.localScale = scratchRestScale * Mathf.Lerp(1, 1.65f, reach);
            tool.position = heldRoot.position;
            // Place the actual central claw tip on the baked, animated mesh.
            Vector3 contactRoot = tool.position + surface + normal * .004f - scratchTip.position;
            tool.position = Vector3.Lerp(heldRoot.position, contactRoot, reach);
            if (!scratchAwarded && scratchAge >= .32f && scratchAge <= .48f)
            {
                float separation = Vector3.Distance(scratchTip.position, surface);
                if (separation < .015f && Round.Stroke(scratchTarget.index))
                {
                    scratchAwarded = true; ConfirmedScratchContacts++;
                    LastScratchContactDistance = separation; LastScratchSurfacePoint = surface;
                    PlayCareSound("stroke", surface);
                }
            }
            if (scratchAge >= .66f) CancelScratch();
        }
    }

    public sealed partial class ZooDayTwoNpc
    {
        SkinnedMeshRenderer contactSkin;
        Mesh contactMesh;
        Transform torso;
        readonly List<Vector3> contactVertices = new();
        int[] contactTriangles;

        public bool TryScratchSurface(Vector3 from, Vector3 screenRight, float scrub, out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            if (!contactSkin)
            {
                foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (skin.sharedMesh && (!contactSkin || skin.sharedMesh.vertexCount > contactSkin.sharedMesh.vertexCount)) contactSkin = skin;
                foreach (var bone in GetComponentsInChildren<Transform>()) if (bone.name == "spine_02") torso = bone;
                if (!contactSkin) return false;
                contactMesh = new Mesh { name = "Day 02 animated contact surface" };
                contactMesh.MarkDynamic();
            }
            contactSkin.BakeMesh(contactMesh);
            contactMesh.GetVertices(contactVertices);
            if (contactTriangles == null) contactTriangles = contactMesh.triangles;
            Vector3 target = (torso ? torso.position : transform.position + Vector3.up * .55f) + screenRight * .1f + Vector3.up * scrub;
            Vector3 direction = (target - from).normalized;
            // Work in world space so non-uniform character scale does not affect distance.
            for (int i = 0; i < contactVertices.Count; i++) contactVertices[i] = contactSkin.transform.TransformPoint(contactVertices[i]);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < contactTriangles.Length; i += 3)
            {
                Vector3 a = contactVertices[contactTriangles[i]], b = contactVertices[contactTriangles[i + 1]], c = contactVertices[contactTriangles[i + 2]];
                Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(direction, e2);
                float det = Vector3.Dot(e1, p); if (Mathf.Abs(det) < .0000001f) continue;
                float inv = 1 / det; Vector3 s = from - a;
                float u = Vector3.Dot(s, p) * inv; if (u < 0 || u > 1) continue;
                Vector3 q = Vector3.Cross(s, e1); float v = Vector3.Dot(direction, q) * inv; if (v < 0 || u + v > 1) continue;
                float distance = Vector3.Dot(e2, q) * inv;
                if (distance <= 0 || distance >= nearest) continue;
                nearest = distance; normal = Vector3.Cross(e1, e2).normalized;
            }
            if (float.IsPositiveInfinity(nearest)) return false;
            point = from + direction * nearest;
            if (Vector3.Dot(normal, direction) > 0) normal = -normal;
            return true;
        }

        void OnDestroy() { if (contactMesh) Destroy(contactMesh); }
    }
}
