using UnityEngine;
using UnityEngine.XR;

public sealed class VrPlatformSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject desktopPlayer;
    [SerializeField] private GameObject vrPlayer;
    [SerializeField] private GameObject presentationCamera;

    private bool wasXrActive;

    private void Awake()
    {
        Apply(XRSettings.isDeviceActive);
    }

    private void Update()
    {
        bool xrActive = XRSettings.isDeviceActive;
        if (xrActive != wasXrActive)
        {
            Apply(xrActive);
        }
    }

    private void Apply(bool xrActive)
    {
        wasXrActive = xrActive;
        if (desktopPlayer != null)
        {
            desktopPlayer.SetActive(!xrActive);
        }

        if (vrPlayer != null)
        {
            vrPlayer.SetActive(xrActive);
        }

        if (presentationCamera != null)
        {
            presentationCamera.SetActive(false);
        }
    }
}
