using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    // Every world-space plate used to be parented straight to the camera the
    // session picked during Start(). In VR that camera is often still the
    // desktop stand-in: the headset rig activates a few frames later, the stand
    // -in camera gets disabled, and the whole HUD disappears with it. This
    // anchor always rides the real eye transform, so the UI stays in front of
    // the player whichever camera is currently rendering.
    public sealed class CareHeadAnchor : MonoBehaviour
    {
        Transform source;

        public Transform Source => source;

        public void Follow(Transform value)
        {
            source = value;
            Sync();
        }

        void LateUpdate() => Sync();

        void Sync()
        {
            if (!source) return;
            transform.SetPositionAndRotation(source.position, source.rotation);
        }
    }
}
