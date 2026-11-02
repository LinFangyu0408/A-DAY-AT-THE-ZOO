using UnityEngine;

namespace ADayAtTheZoo.Habitat
{
    [DisallowMultipleComponent]
    public sealed class MonkeyWindSway : MonoBehaviour
    {
        private Quaternion baseRotation;
        private Vector3 windDirection;
        private float amplitude;
        private float speed;
        private float phase;

        public void Configure(Vector3 direction, float swayDegrees, float swaySpeed, float phaseOffset)
        {
            baseRotation = transform.rotation;
            windDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            amplitude = Mathf.Clamp(swayDegrees, 0f, 4f);
            speed = Mathf.Max(0.1f, swaySpeed);
            phase = phaseOffset;
        }

        private void LateUpdate()
        {
            float gust = Mathf.PerlinNoise(Time.time * speed + phase, phase * 0.37f) * 2f - 1f;
            float flutter = Mathf.Sin(Time.time * speed * 2.1f + phase) * 0.18f;
            float angle = (gust + flutter) * amplitude;
            Vector3 sideAxis = Vector3.Cross(Vector3.up, windDirection).normalized;
            if (sideAxis.sqrMagnitude < 0.001f)
                sideAxis = Vector3.right;
            transform.rotation = Quaternion.AngleAxis(angle, sideAxis) * baseRotation;
        }
    }
}
