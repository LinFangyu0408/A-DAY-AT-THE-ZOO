using ADayAtTheZoo.Hands;
using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    // Keeps the authored gloves on the active VR/desktop hand anchors and
    // exposes the palm sockets used by the care items.
    public sealed class CareHandVisuals : MonoBehaviour
    {
        const float GloveScale = .92f;
        const float BananaLength = .2f;

        [SerializeField] GameObject rightGlovePrefab;
        [SerializeField] GameObject leftGlovePrefab;

        MonkeyCareSession session;
        Transform rightAnchor, leftAnchor;
        GameObject rightGlove, leftGlove;
        GlovePoseDriver rightPose, leftPose;
        Transform rightGrip, leftGrip;
        CareItem mountedBanana;
        Vector3 mountedLocalPosition;
        Vector3 mountedLocalScale;
        Quaternion mountedLocalRotation;
        bool missingGloveLogged;

        public Transform RightGripSocket => rightGrip ? rightGrip : rightGlove ? rightGlove.transform : null;
        public Transform LeftGripSocket => leftGrip ? leftGrip : leftGlove ? leftGlove.transform : null;
        public bool HeldItemMounted =>
            mountedBanana && session && session.HeldItem == mountedBanana &&
            mountedBanana.transform.parent == RightGripSocket;

        public void Begin(MonkeyCareSession owner)
        {
            session = owner;
            EnsureObjects();
        }

        public void AttachHeldItem(CareItem item)
        {
            if (!item) return;
            EnsureObjects();
            Transform mount = RightGripSocket;
            if (!mount) return;

            item.transform.SetParent(mount, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.Euler(item.gripRotation);
            FitAndCenter(item.gameObject, BananaLength);
            item.transform.localPosition += item.gripPosition;
            mountedLocalPosition = item.transform.localPosition;
            mountedLocalRotation = item.transform.localRotation;
            mountedLocalScale = item.transform.localScale;
            mountedBanana = item;
            rightPose?.Grip();
        }

        public void ReleaseHeldItem()
        {
            mountedBanana = null;
            rightPose?.Idle();
        }

        void LateUpdate()
        {
            if (!session) return;
            EnsureObjects();
            FollowHandAnchors();

            if (mountedBanana && session.HeldItem != mountedBanana)
            {
                mountedBanana = null;
            }
            if (!mountedBanana && session.HeldItem && session.HeldItem.kind == CareNeed.Hunger)
            {
                AttachHeldItem(session.HeldItem);
            }
            else if (mountedBanana && session.HeldItem == mountedBanana)
            {
                // Keep the banana as a literal child of the animated GripSocket.
                // Do not move it in world space here: the authored glove bone is
                // the single source of truth, so head/controller motion carries
                // the banana without lag or drift.
                Transform mount = RightGripSocket;
                if (mount && mountedBanana.transform.parent != mount)
                    mountedBanana.transform.SetParent(mount, false);
                if (mount)
                {
                    mountedBanana.transform.localPosition = mountedLocalPosition;
                    mountedBanana.transform.localRotation = mountedLocalRotation;
                    mountedBanana.transform.localScale = mountedLocalScale;
                }
            }

            bool holding = mountedBanana && mountedBanana.Held && mountedBanana == session.HeldItem;
            SetPose(rightPose, holding ? GlovePose.Grip : GlovePose.Idle);
            SetPose(leftPose, GlovePose.Idle);
        }

        void EnsureObjects()
        {
            if (!rightGlove)
            {
                rightGlovePrefab ??= Resources.Load<GameObject>("Gloves/Glove_Right");
                rightGlove = CreateGlove(rightGlovePrefab, "Care Right Glove", false);
                if (rightGlove)
                {
                    rightPose = rightGlove.GetComponent<GlovePoseDriver>();
                    rightGrip = FindDeep(rightGlove.transform, "GripSocket");
                }
            }

            if (!leftGlove)
            {
                leftGlovePrefab ??= Resources.Load<GameObject>("Gloves/Glove_Left");
                leftGlove = CreateGlove(leftGlovePrefab, "Care Left Glove", true);
                if (leftGlove)
                {
                    leftPose = leftGlove.GetComponent<GlovePoseDriver>();
                    leftGrip = FindDeep(leftGlove.transform, "GripSocket");
                }
            }

            if (!missingGloveLogged && (!rightGlovePrefab || !leftGlovePrefab))
            {
                missingGloveLogged = true;
                Debug.LogWarning("[Monkey Care] Glove prefabs are missing from Resources/Gloves.");
            }
        }

        GameObject CreateGlove(GameObject prefab, string objectName, bool left)
        {
            if (!prefab) return null;
            var instance = Instantiate(prefab, transform);
            instance.name = objectName;
            PrepareVisual(instance, 2);
            instance.transform.localScale = Vector3.one * GloveScale;
            var input = instance.GetComponentInChildren<GloveXRInput>(true);
            if (input) input.enabled = false;
            return instance;
        }

        void FollowHandAnchors()
        {
            Transform nextRight = session.RightHand;
            if (rightGlove && nextRight != rightAnchor)
            {
                rightAnchor = nextRight;
                rightGlove.transform.SetParent(rightAnchor, false);
                rightGlove.transform.localPosition = Vector3.zero;
                rightGlove.transform.localRotation = Quaternion.Euler(-6f, -4f, 0);
                rightGlove.transform.localScale = Vector3.one * GloveScale;
            }

            Transform nextLeft = session.LeftHand;
            if (leftGlove && nextLeft != leftAnchor)
            {
                leftAnchor = nextLeft;
                leftGlove.transform.SetParent(leftAnchor, false);
                leftGlove.transform.localPosition = Vector3.zero;
                leftGlove.transform.localRotation = Quaternion.Euler(-6f, 4f, 0);
                leftGlove.transform.localScale = Vector3.one * GloveScale;
            }

        }

        void PrepareVisual(GameObject root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        }

        static void FitAndCenter(GameObject root, float maxSize)
        {
            if (!Bounds(root, out var bounds)) return;
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest > .0001f) root.transform.localScale *= maxSize / largest;
            if (!Bounds(root, out bounds)) return;
            Vector3 center = root.transform.parent.InverseTransformPoint(bounds.center);
            root.transform.localPosition -= center;
        }

        static bool Bounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = new Bounds(root.transform.position, Vector3.zero);
            bool found = false;
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        static void SetPose(GlovePoseDriver driver, GlovePose pose)
        {
            if (driver && driver.Pose != pose) driver.SetPose(pose);
        }
    }
}
