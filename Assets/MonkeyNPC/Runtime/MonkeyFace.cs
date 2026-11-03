using UnityEngine;

namespace ADayAtTheZoo.Monkeys
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonkeyNpcAnimator))]
    public sealed class MonkeyFace : MonoBehaviour
    {
        [SerializeField] private Material neutral, blink, hungry, eatingOpen, eatingClosed, happy, puzzled, greeting;
        private MonkeyNpcAnimator driver;
        private SkinnedMeshRenderer faceRenderer;
        private Material original, applied;
        private int faceSlot = -1;

        private void Awake() => Bind();

        private void LateUpdate()
        {
            ApplyPose(driver.CurrentAction, Time.time);
        }

        private void Bind()
        {
            driver = GetComponent<MonkeyNpcAnimator>();
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int index = 0; index < slots.Length; index++)
                {
                    if (slots[index] == null || !slots[index].name.StartsWith("M_Chibi_Emo_", System.StringComparison.Ordinal))
                        continue;
                    faceRenderer = renderer;
                    faceSlot = index;
                    original = slots[index];
                    return;
                }
            }
        }

        public void ApplyPose(MonkeyAction action, float seconds)
        {
            if (driver == null)
                Bind();
            if (faceSlot < 0)
                return;

            Material face = action switch
            {
                MonkeyAction.Hungry => hungry,
                MonkeyAction.Eating => Mathf.Repeat(seconds, 0.38f) < 0.17f ? eatingOpen : eatingClosed,
                MonkeyAction.Happy => happy,
                MonkeyAction.Scratching => puzzled,
                MonkeyAction.Hi => greeting,
                _ => Mathf.Repeat(seconds, 3.7f) is >= 2.84f and < 2.96f ? blink : neutral
            };
            SetFace(face != null ? face : original);
        }

        private void OnDisable()
        {
            if (faceRenderer != null)
                SetFace(original);
        }

        private void SetFace(Material face)
        {
            if (face == applied)
                return;
            Material[] slots = faceRenderer.sharedMaterials;
            slots[faceSlot] = face;
            faceRenderer.sharedMaterials = slots;
            applied = face;
        }

#if UNITY_EDITOR
        public void Configure(Material neutral, Material blink, Material hungry, Material eatingOpen,
            Material eatingClosed, Material happy, Material puzzled, Material greeting)
        {
            this.neutral = neutral;
            this.blink = blink;
            this.hungry = hungry;
            this.eatingOpen = eatingOpen;
            this.eatingClosed = eatingClosed;
            this.happy = happy;
            this.puzzled = puzzled;
            this.greeting = greeting;
        }
#endif
    }
}
