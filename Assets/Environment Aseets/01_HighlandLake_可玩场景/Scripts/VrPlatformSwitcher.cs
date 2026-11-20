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
        // headset was put on. Retrying is expensive though: a failed attempt
        // tears the loader down, rescans USB/input devices and dumps a stack of
        // OpenXR warnings. In the recorded session five attempts froze the game
        // for ~10 s each — the "severe stutter" at minutes 1-3. The retry budget
        // is now a single attempt, skipped entirely while no VR runtime is alive.
#if UNITY_EDITOR
        if (!editorDesktopPreview || xrActive) return;

        if (Time.unscaledTime >= nextRuntimeProbe)
        {
            nextRuntimeProbe = Time.unscaledTime + 2f;
            bool running = HeadsetRuntimeRunning();
            if (running && !runtimeSeen)
            {
                // Quest Link / SteamVR just came up: retry right away instead of
                // waiting out the backoff that was armed while it was closed.
                runtimeSeen = true;
                runtimeMissingLogged = false;
                xrRetryGaveUp = false;
                xrRetryCount = 0;
                nextXrRetry = 0f;
            }
            else runtimeSeen = running;
        }

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.f2Key.wasPressedThisFrame)
        {
            // Manual recovery for the "headset woke up but the runtime probe
            // still says no" case; costs nothing until the key is pressed.
            xrRetryCount = 0;
            xrRetryGaveUp = false;
            nextXrRetry = 0f;
            Debug.Log("[Zoo Platform] F2：手动重试 OpenXR 初始化。");
        }

        if (xrRetryCount >= MaxXrRetries)
        {
            // One automatic attempt only. A failed attempt blocks the main thread
            // for ~10 s (OpenXR teardown + USB rescan + a stack of warnings), so
            // the old 8-attempt ladder turned the first three minutes into a
            // slideshow: the recording showed five 500 ms-per-frame freezes that
            // lined up exactly with the retries. A headset that is already on the
            // player's head starts OpenXR on the first try; anything later is
            // covered by the runtime probe below and by F2.
            if (!xrRetryGaveUp)
                Debug.Log("[Zoo Platform] OpenXR 未自动启动，已停止重试以保持流畅；戴上头显后按 F2 手动重连（本次保持桌面预览）。");
            xrRetryGaveUp = true;
            return;
        }
        if (!runtimeSeen && !runtimeMissingLogged)
        {
            runtimeMissingLogged = true;
            Debug.Log("[Zoo Platform] 未检测到 Meta/Oculus 或 SteamVR 运行时，跳过 XR 重试以避免卡顿；启动 Quest Link 后会自动重试（或按 F2 手动重试）。");
        }
        if (!runtimeSeen) return;
        if (Time.unscaledTime < nextXrRetry) return;
        float backoff = Mathf.Min(FirstXrRetryDelay * Mathf.Pow(1.5f, xrRetryCount), MaxXrRetryDelay);
        nextXrRetry = Time.unscaledTime + backoff;
        xrRetryCount++;
        TryRecoverXr();
#endif
    }

    // Cheap presence probe (no OpenXR call, no device scan): is a PC VR runtime
    // process alive? Oculus/Quest Link runs OVRServer_x64 + OculusClient, SteamVR
    // runs vrserver. Without one of them an XR init attempt can only fail.
    private static bool HeadsetRuntimeRunning()
    {
        return ProcessAlive("OVRServer_x64") || ProcessAlive("OVRServer") ||
               ProcessAlive("OculusClient") || ProcessAlive("vrserver") ||
               ProcessAlive("vrmonitor");
    }

    private static bool ProcessAlive(string name)
    {
        try
        {
            return System.Diagnostics.Process.GetProcessesByName(name).Length > 0;
        }
        catch { return false; }
    }

    private const float FirstXrRetryDelay = 8f;
    private const float MaxXrRetryDelay = 8f;
    // Exactly one automatic attempt. The counter is reset only when the presence
    // probe notices that a VR runtime came up later, which buys one more try.
    private const int MaxXrRetries = 1;
    private int xrRetryCount;
    private float nextXrRetry;
    private bool xrRetryGaveUp;
    private bool runtimeSeen;
    private bool runtimeMissingLogged;
    private float nextRuntimeProbe;

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
