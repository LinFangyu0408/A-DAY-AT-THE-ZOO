using UnityEngine;
using UnityEngine.XR;

namespace ADayAtTheZoo.Hands
{
    // Optional input adapter. The existing XR anchor supplies wrist tracking.
    [DisallowMultipleComponent, RequireComponent(typeof(GlovePoseDriver))]
    public sealed class GloveXRInput : MonoBehaviour
    {
        public XRNode hand = XRNode.RightHand;
        [Range(0.05f, 1f)] public float pressThreshold = 0.55f;
        [Range(0f, 0.9f)] public float releaseThreshold = 0.4f;
        private GlovePoseDriver driver;
        private bool gripPressed, triggerPressed;

        private void Awake() => driver = GetComponent<GlovePoseDriver>();
        private void Update()
        {
            var device = InputDevices.GetDeviceAtXRNode(hand);
            if (!device.isValid) { ApplyInputs(0f, 0f); return; }
            device.TryGetFeatureValue(CommonUsages.grip, out float grip);
            device.TryGetFeatureValue(CommonUsages.trigger, out float trigger);
            ApplyInputs(grip, trigger);
        }

        public void ApplyInputs(float grip, float trigger)
        {
            if (driver == null) driver = GetComponent<GlovePoseDriver>();
            float release = Mathf.Min(releaseThreshold, pressThreshold);
            gripPressed = grip >= (gripPressed ? release : pressThreshold);
            triggerPressed = trigger >= (triggerPressed ? release : pressThreshold);
            // Holding an item keeps the grip pose even while selecting with trigger.
            var next = gripPressed ? GlovePose.Grip : triggerPressed ? GlovePose.Point : GlovePose.Idle;
            if (driver.Pose != next) driver.SetPose(next);
        }

        private void OnDisable()
        {
            gripPressed = triggerPressed = false;
            if (driver != null) driver.Idle();
        }
    }
}
