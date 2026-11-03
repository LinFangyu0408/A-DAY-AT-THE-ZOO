using UnityEngine;

namespace ADayAtTheZoo.Monkeys
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonkeyNpcAnimator))]
    public sealed class MonkeyHandContact : MonoBehaviour
    {
        private MonkeyNpcAnimator driver;
        private Transform hips, chest, head, jaw, leftEye, rightEye;
        private Arm left, right;

        private void Awake() => Bind();

        private void LateUpdate() => ApplyPose();

        private void Bind()
        {
            driver = GetComponent<MonkeyNpcAnimator>();
            var animator = driver.Animator;
            if (animator.avatar == null || !animator.isHuman)
                return;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            jaw = animator.GetBoneTransform(HumanBodyBones.Jaw);
            leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            {
                if (jaw == null && bone.name == "Jaw_M") jaw = bone;
                if (leftEye == null && bone.name == "Eye_L") leftEye = bone;
                if (rightEye == null && bone.name == "Eye_R") rightEye = bone;
            }
            left = new Arm(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            right = new Arm(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
        }

        public void ApplyPose()
        {
            if (driver == null)
                Bind();
            MonkeyAction action = driver.CurrentAction;
            if ((action != MonkeyAction.Hungry && action != MonkeyAction.Eating) ||
                hips == null || chest == null || head == null || !left.Valid || !right.Valid)
                return;

            Vector3 side = (right.Upper.position - left.Upper.position).normalized;
            Vector3 forward = Vector3.Cross(side, transform.up).normalized;
            float shoulderWidth = Vector3.Distance(right.Upper.position, left.Upper.position);
            if (action == MonkeyAction.Hungry)
            {
                Vector3 belly = Vector3.Lerp(hips.position, chest.position, 0.30f) + forward * shoulderWidth * 0.44f;
                left.Reach(belly - side * shoulderWidth * 0.17f, 0.86f);
                right.Reach(belly + side * shoulderWidth * 0.17f, 0.86f);
                return;
            }

            Animator animator = driver.Animator;
            float time = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0).normalizedTime
                : animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            float contact = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.27f, time)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.64f, 0.86f, time)));
            Vector3 mouth = jaw != null ? jaw.position : head.position;
            if (leftEye != null && rightEye != null)
            {
                Vector3 eyes = (leftEye.position + rightEye.position) * 0.5f;
                mouth = Vector3.Lerp(mouth, eyes, 0.32f);
                Vector3 faceForward = Vector3.ProjectOnPlane(eyes - head.position, transform.up);
                if (faceForward.sqrMagnitude > 0.0001f)
                    forward = faceForward.normalized;
            }
            // The target is the wrist, so leave room for the hand mesh in front of it.
            // Approach from below and from the right to keep the forearm clear of the chin.
            Vector3 wristTarget = mouth + shoulderWidth *
                (forward * 0.72f + side * 0.24f - transform.up * 0.24f);
            right.Reach(wristTarget, contact);
        }

        private readonly struct Arm
        {
            public readonly Transform Upper;
            private readonly Transform lower, hand;
            public bool Valid => Upper != null && lower != null && hand != null;

            public Arm(Animator animator, HumanBodyBones upper, HumanBodyBones elbow, HumanBodyBones wrist)
            {
                Upper = animator.GetBoneTransform(upper);
                lower = animator.GetBoneTransform(elbow);
                hand = animator.GetBoneTransform(wrist);
            }

            public void Reach(Vector3 target, float weight)
            {
                if (weight <= 0f)
                    return;
                Vector3 shoulder = Upper.position;
                Vector3 desired = Vector3.Lerp(hand.position, target, weight) - shoulder;
                float upperLength = Vector3.Distance(shoulder, lower.position);
                float lowerLength = Vector3.Distance(lower.position, hand.position);
                if (desired.sqrMagnitude < 0.000001f || upperLength < 0.001f || lowerLength < 0.001f)
                    return;
                Vector3 direction = desired.normalized;
                float distance = Mathf.Clamp(desired.magnitude,
                    Mathf.Abs(upperLength - lowerLength) + 0.0001f, (upperLength + lowerLength) * 0.985f);
                Vector3 bend = Vector3.ProjectOnPlane(lower.position - shoulder, direction);
                if (bend.sqrMagnitude < 0.000001f)
                    bend = Vector3.ProjectOnPlane(-Upper.up, direction);
                if (bend.sqrMagnitude < 0.000001f)
                    return;
                float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
                float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
                Vector3 elbow = shoulder + direction * along + bend.normalized * height;
                Upper.rotation = Quaternion.FromToRotation(lower.position - shoulder, elbow - shoulder) * Upper.rotation;
                Vector3 wrist = shoulder + direction * distance;
                lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, wrist - lower.position) * lower.rotation;
            }
        }
    }
}
