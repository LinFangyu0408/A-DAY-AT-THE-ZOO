using UnityEngine;

namespace ADayAtTheZoo.Monkeys
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class MonkeyNpcAnimator : MonoBehaviour
    {
        [SerializeField] private MonkeyAnimationLibrary library;
        [SerializeField, Min(0f)] private float blendSeconds = 0.15f;
        private Animator animator;

        public MonkeyAnimationLibrary Library => library;
        public Animator Animator => animator != null ? animator : GetComponent<Animator>();

        public MonkeyAction CurrentAction
        {
            get
            {
                Animator target = Animator;
                AnimatorStateInfo state = target.IsInTransition(0)
                    ? target.GetNextAnimatorStateInfo(0) : target.GetCurrentAnimatorStateInfo(0);
                foreach (MonkeyAction action in System.Enum.GetValues(typeof(MonkeyAction)))
                    if (state.shortNameHash == UnityEngine.Animator.StringToHash(action.ToString()))
                        return action;
                return MonkeyAction.StandingIdle;
            }
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            // Navigation and gameplay code own world movement; clips play in place.
            animator.applyRootMotion = false;
        }

        public bool TryPlay(MonkeyAction action)
        {
            Animator target = Animator;
            int state = UnityEngine.Animator.StringToHash("Base Layer." + action);
            if (library == null || !library.HasAction(action) ||
                target.runtimeAnimatorController == null || !target.HasState(0, state))
            {
                Debug.LogWarning($"[Monkey NPC] {action} is not available. Build the animation library first.", this);
                return false;
            }
            target.CrossFadeInFixedTime(state, blendSeconds, 0, 0f);
            return true;
        }

        // Public hooks for the feeding, navigation and interaction systems.
        public void Stand() => TryPlay(MonkeyAction.StandingIdle);
        public void Sit() => TryPlay(MonkeyAction.SittingIdle);
        public void ShowHunger() => TryPlay(MonkeyAction.Hungry);
        public void Walk() => TryPlay(MonkeyAction.Walking);
        public void Run() => TryPlay(MonkeyAction.Running);
        public void Scratch() => TryPlay(MonkeyAction.Scratching);
        public void Wave() => TryPlay(MonkeyAction.Hi);

        public bool Feed()
        {
            if (library == null || !library.HasAction(MonkeyAction.Eating) ||
                !library.HasAction(MonkeyAction.Happy))
            {
                Debug.LogWarning("[Monkey NPC] Feeding needs both Eating and Happy clips.", this);
                return false;
            }
            // Controller transitions handle Eating -> Happy -> StandingIdle.
            return TryPlay(MonkeyAction.Eating);
        }

#if UNITY_EDITOR
        public void Configure(MonkeyAnimationLibrary value)
        {
            library = value;
        }
#endif
    }
}
