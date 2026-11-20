using UnityEngine;
using UnityEngine.XR;

namespace ADayAtTheZoo.Monkeys.Care
{
    // Distinguishes the two supported input devices:
    //   - VR Meta 3 (headset active): keep the floating world-space plates.
    //   - PC (desktop preview or gamepad, no headset): a flat 2D HUD.
    public static class CarePlatform
    {
        // Test hook: the smoke suite runs headless (no XR), but exercises the
        // world-space VR layout, so it forces VR mode before building the HUD.
        public static bool ForceVr;

        public static bool IsVr =>
            ForceVr || XRSettings.isDeviceActive ||
            (MonkeyCareSession.Instance && MonkeyCareSession.Instance.UsingVrHands);

        public static bool FlatUi => !IsVr;
    }
}
