using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[DefaultExecutionOrder(-1000)]
public sealed class VrPlatformSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject desktopPlayer;
    [SerializeField] private GameObject vrPlayer;
    [SerializeField] private GameObject presentationCamera;
    [SerializeField] private bool requireTrackedHeadset = true;
    [Tooltip("Unity Editor 没有可用头显时使用桌面/Xbox 预览；头显出图后自动切换为 VR 控制器。")]
    [SerializeField] private bool editorDesktopPreview = true;

    private bool wasXrActive;
    private static readonly List<XRDisplaySubsystem> displays = new();

    private void Awake()
    {
        bool xrActive = ShouldUseXr();
        Apply(xrActive);
        Debug.Log("[Zoo Platform] " + (xrActive ? "XR 已启动" : "桌面/Xbox 预览已启动"));
    }

    private void Update()
    {
        bool xrActive = ShouldUseXr();
        if (xrActive != wasXrActive)
        {
            Apply(xrActive);
        }
    }

    private bool ShouldUseXr()
    {
#if UNITY_EDITOR
        // Keep the lightweight desktop preview until an XR display is
        // actually running. Once the headset is active, the VR rig wins so
        // gloves can follow the Meta controller anchors.
        if (editorDesktopPreview && !XRSettings.isDeviceActive) return false;
#endif
        if (!XRSettings.isDeviceActive) return false;
        displays.Clear();
        SubsystemManager.GetSubsystems(displays);
        bool displayRunning = false;
        foreach (var display in displays)
        {
            if (!display.running) continue;
            displayRunning = true;
            break;
        }
        if (!displayRunning) return false;
        if (!requireTrackedHeadset) return true;
        return InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid;
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

        if (!xrActive && desktopPlayer != null)
        {
            foreach (var camera in desktopPlayer.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = true;
            }
            foreach (var listener in desktopPlayer.GetComponentsInChildren<AudioListener>(true))
            {
                listener.enabled = true;
            }
        }
    }
}
