using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    // Follow after editable layout overrides so they cannot pin the progress marker.
    [DefaultExecutionOrder(1100)]
    public sealed class CareProgressStarFollower : MonoBehaviour
    {
        public Image progressFill;
        [Tooltip("Offset from the moving fill endpoint, in progress-bar pixels.")]
        public Vector2 offset;
        [HideInInspector] public CareProgressStarFollower settings;

        void LateUpdate() => Follow();

        public void Follow()
        {
            if (!progressFill) return;
            var rect = progressFill.rectTransform;
            var bounds = rect.rect;
            var adjustment = settings ? settings.offset : offset;
            var point = new Vector3(Mathf.Lerp(bounds.xMin, bounds.xMax,
                Mathf.Clamp01(progressFill.fillAmount)), bounds.center.y, 0);
            point += (Vector3)adjustment;
            transform.position = rect.TransformPoint(point);
        }
    }
}
