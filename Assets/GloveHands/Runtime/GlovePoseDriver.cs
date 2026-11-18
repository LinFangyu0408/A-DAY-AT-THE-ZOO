using UnityEngine;

namespace ADayAtTheZoo.Hands
{
    public enum GlovePose { Idle = 0, Grip = 1, Point = 2 }

    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class GlovePoseDriver : MonoBehaviour
    {
        [SerializeField] private GlovePose pose;
        private Animator animator;
        private static readonly int PoseParameter = Animator.StringToHash("Pose");
        public GlovePose Pose => pose;
        public Animator Animator => animator != null ? animator : animator = GetComponent<Animator>();

        private void OnEnable() => SetPose(pose);
        public void SetPose(GlovePose value)
        {
            pose = value;
            if (Animator.runtimeAnimatorController != null) Animator.SetInteger(PoseParameter, (int)value);
        }
        public void Idle() => SetPose(GlovePose.Idle);
        public void Grip() => SetPose(GlovePose.Grip);
        public void Point() => SetPose(GlovePose.Point);
    }
}
