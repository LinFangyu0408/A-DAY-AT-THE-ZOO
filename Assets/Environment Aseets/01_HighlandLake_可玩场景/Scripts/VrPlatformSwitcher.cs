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
            return;
        }

        // OpenXR gives up once ("XR_ERROR_FORM_FACTOR_UNAVAILABLE") when Play
        // starts before the headset is worn, and Unity never retries on its own,
        // so the session stayed on the desktop preview forever even after the
        // headset was put on. Retry the loader a bounded number of times: each
        // attempt re-scans USB/input devices, so an unbounded loop was churning
        // the gamepad and could knock it offline.
#if UNITY_EDITOR
        if (!editorDesktopPreview || xrActive) return;
        if (xrRetryCount >= MaxXrRetries) return;
        if (Time.unscaledTime < nextXrRetry) return;
        nextXrRetry = Time.unscaledTime + XrRetryInterval;
        xrRetryCount++;
        TryRecoverXr();
#endif
    }

    private const float XrRetryInterval = 15f;
    private const int MaxXrRetries = 8;
    private int xrRetryCount;
    private float nextXrRetry;

    private void TryRecoverXr()
    {
        try
        {
            var settings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (settings == null || settings.Manager == null) return;
            var manager = settings.Manager;
            if (manager.isInitializationComplete) return;
            manager.InitializeLoaderSync();
            if (manager.activeLoader == null) return;
            manager.StartSubsystems();
            xrRetryCount = MaxXrRetries; // success: never touch the loader again
            Debug.Log("[Zoo Platform] OpenXR 延迟初始化成功，切换到 VR");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[Zoo Platform] XR retry failed: " + exception.Message);
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
