using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Writes a small heartbeat with the live XR input situation so the headset can
/// be diagnosed from outside the editor:
///   work/monkey-care/input-probe.txt
/// It answers the only questions that matter when "the controllers do nothing":
/// is the XR display running, is a head / left / right device actually tracked,
/// and what does OVRInput believe is connected.
/// </summary>
[InitializeOnLoad]
public static class CareInputProbe
{
    const string Folder = "work/monkey-care/";
    static double nextWrite;

    static CareInputProbe()
    {
        Directory.CreateDirectory(Folder);
        EditorApplication.update += Tick;
    }

    // Frame-time watchdog: the editor ticks once per rendered frame, so the gap
    // between two ticks is the frame time. Keeping the worst gap of the last
    // second makes a freeze visible from outside ("frame.worstMs=5300" was the
    // XR-retry stall) instead of having to watch a 4 minute recording again.
    static double lastFrameTick;
    static double worstFrameMs;
    static int framesInWindow;

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (lastFrameTick > 0)
        {
            double ms = (now - lastFrameTick) * 1000.0;
            if (ms > worstFrameMs) worstFrameMs = ms;
            framesInWindow++;
        }
        lastFrameTick = now;
        if (now < nextWrite) return;
        nextWrite = now + 1.0;
        try { Write(); } catch (Exception e) { Debug.LogWarning("CareInputProbe: " + e.Message); }
        worstFrameMs = 0;
        framesInWindow = 0;
    }

    static void Write()
    {
        var sb = new StringBuilder();
        sb.AppendLine("time=" + DateTime.Now.ToString("s"));
        sb.AppendLine("playing=" + EditorApplication.isPlaying);
        sb.AppendLine("compiling=" + EditorApplication.isCompiling);
        sb.AppendLine("xrActive=" + XRSettings.isDeviceActive);
        sb.AppendLine("xrDevice=" + XRSettings.loadedDeviceName);

        AppendNode(sb, "head", XRNode.Head);
        AppendNode(sb, "leftHand", XRNode.LeftHand);
        AppendNode(sb, "rightHand", XRNode.RightHand);

        var pads = UnityEngine.InputSystem.Gamepad.all;
        sb.AppendLine("gamepads=" + pads.Count + " [" +
            string.Join(", ", pads.Select(p => p.layout + "/" + p.displayName)) + "]");
        // Which content variant the game would launch: VR world plates, or the
        // flat PC HUD with Xbox/PS5 key glyphs. Read from the same detector the
        // game uses, so the probe proves the launch decision, not a guess.
        try
        {
            sb.AppendLine("device.mode=" + (XRSettings.isDeviceActive ? "VR" : "PC"));
            sb.AppendLine("device.input=" + ADayAtTheZoo.Monkeys.Care.CareInput.DescribeDevice());
            sb.AppendLine("device.padStyle=" + ADayAtTheZoo.Monkeys.Care.CareInput.PadStyle +
                " autoStyle=" + ADayAtTheZoo.Monkeys.Care.CareInput.AutoDetectStyle() +
                " confirm=" + ADayAtTheZoo.Monkeys.Care.CareInput.ConfirmLabel +
                " back=" + ADayAtTheZoo.Monkeys.Care.CareInput.BackLabel +
                " pinned=" + ADayAtTheZoo.Monkeys.Care.CareInput.StylePinned);
        }
        catch (Exception e) { sb.AppendLine("deviceError=" + e.Message); }
        try
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null)
            {
                sb.AppendLine("pad.buttons=" + (pad.buttonSouth.isPressed ? "S" : ".") +
                    (pad.buttonEast.isPressed ? "E" : ".") +
                    (pad.buttonWest.isPressed ? "W" : ".") +
                    (pad.buttonNorth.isPressed ? "N" : ".") +
                    " stickL=(" + pad.leftStick.x.ReadValue().ToString("F2") + "," +
                    pad.leftStick.y.ReadValue().ToString("F2") + ")" +
                    " triggerR=" + pad.rightTrigger.ReadValue().ToString("F2"));
            }
        }
        catch (Exception e) { sb.AppendLine("padError=" + e.Message); }
        try
        {
            sb.AppendLine("ovrConnected=" + OVRInput.GetConnectedControllers());
            sb.AppendLine("ovrRTouch=" + OVRInput.IsControllerConnected(OVRInput.Controller.RTouch));
            sb.AppendLine("ovrLTouch=" + OVRInput.IsControllerConnected(OVRInput.Controller.LTouch));
        }
        catch (Exception e) { sb.AppendLine("ovrError=" + e.Message); }

        AppendPlayerHeight(sb);
        AppendAudio(sb);
        sb.AppendLine("frame.worstMs=" + worstFrameMs.ToString("F0") +
            " frames=" + framesInWindow);

        File.WriteAllText(Folder + "input-probe.txt", sb.ToString());
    }

    // Audio heartbeat: "all game sounds vanished" can only be judged from
    // numbers — master volume/pause, which listener is live, and whether the
    // music source is actually playing a loaded clip.
    static void AppendAudio(StringBuilder sb)
    {
        try
        {
            sb.AppendLine("audio.masterVolume=" + AudioListener.volume.ToString("F2") +
                " masterPause=" + AudioListener.pause);
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var listener in listeners)
            {
                var camera = listener.GetComponent<Camera>();
                sb.AppendLine("audio.listener=" + listener.name +
                    " enabled=" + listener.enabled +
                    " cameraRendering=" + (camera && camera.enabled && camera.gameObject.activeInHierarchy));
            }
            if (!EditorApplication.isPlaying) return;
            var session = UnityEngine.Object.FindFirstObjectByType<ADayAtTheZoo.Monkeys.Care.MonkeyCareSession>();
            if (!session) { sb.AppendLine("audio.session=none"); return; }
            var sources = session.GetComponentsInChildren<AudioSource>(false);
            foreach (var source in sources)
            {
                if (source.name != "Care Music") continue;
                sb.AppendLine("audio.musicSource=" + (source.clip ? source.clip.name : "noClip") +
                    " playing=" + source.isPlaying +
                    " volume=" + source.volume.ToString("F2") +
                    " time=" + source.time.ToString("F1") +
                    " loadState=" + (source.clip ? source.clip.loadState.ToString() : "none") +
                    " spatial=" + source.spatialBlend.ToString("F2"));
            }
            sb.AppendLine("audio.effectSources=" + sources.Count(item => item.name == "Care Spatial Audio"));
            var liveListeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            sb.AppendLine("audio.activeListeners=" + liveListeners.Count(item => item.enabled));
        }
        catch (Exception e) { sb.AppendLine("audioError=" + e.Message); }
    }

    // Height telemetry: the "I float in the air" report can only be judged from
    // numbers, so the rig root, the eye and the tracking-space offset are all
    // written out every second.
    static void AppendPlayerHeight(StringBuilder sb)
    {
        if (!EditorApplication.isPlaying) return;
        try
        {
            var session = UnityEngine.Object.FindFirstObjectByType<ADayAtTheZoo.Monkeys.Care.MonkeyCareSession>();
            if (!session) { sb.AppendLine("session=none"); return; }
            Transform root = session.PlayerRoot;
            sb.AppendLine("playerRoot=" + (root ? root.name + " y=" + root.position.y.ToString("F2") +
                " xz=(" + root.position.x.ToString("F2") + "," + root.position.z.ToString("F2") + ")" : "null"));
            var loco = UnityEngine.Object.FindFirstObjectByType<Locomotion>();
            if (loco)
            {
                sb.AppendLine("locomotion groundOnly=" + loco.groundOnly + " allowTeleport=" + loco.allowTeleport +
                    " islandFloor=" + loco.IslandFloorY.ToString("F2"));
            }
            if (session.rig)
            {
                float eyeY = session.rig.centerEyeAnchor ? session.rig.centerEyeAnchor.position.y : 0f;
                sb.AppendLine("rig y=" + session.rig.transform.position.y.ToString("F2") +
                    " eyeY=" + (session.rig.centerEyeAnchor ? eyeY.ToString("F2") : "?") +
                    " trackingLocalY=" + (session.rig.trackingSpace ? session.rig.trackingSpace.localPosition.y.ToString("F3") : "?"));
                // Standing height above the actual floor: <1.0 means the player is
                // looking at the yard as if sitting on the grass.
                if (session.rig.centerEyeAnchor &&
                    Physics.Raycast(new Vector3(session.rig.centerEyeAnchor.position.x,
                        eyeY + 30f, session.rig.centerEyeAnchor.position.z),
                        Vector3.down, out RaycastHit ground, 80f, ~((1 << 2) | (1 << 5)),
                        QueryTriggerInteraction.Ignore))
                {
                    sb.AppendLine("ground y=" + ground.point.y.ToString("F2") +
                        " eyeAboveGround=" + (eyeY - ground.point.y).ToString("F2"));
                }
            }
            var barrier = UnityEngine.Object.FindFirstObjectByType<ADayAtTheZoo.Monkeys.Care.CarePlayAreaBarrier>();
            if (barrier) sb.AppendLine("barrier startY=" + barrier.StartY.ToString("F2"));
        }
        catch (Exception e) { sb.AppendLine("heightError=" + e.Message); }
    }

    static void AppendNode(StringBuilder sb, string name, XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        sb.Append(name).Append(": valid=").Append(device.isValid);
        if (device.isValid)
        {
            sb.Append(" name='").Append(device.name).Append("'")
              .Append(" maker='").Append(device.manufacturer).Append("'");
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked))
                sb.Append(" tracked=").Append(tracked);
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
                sb.Append(" pos=").Append(pos.ToString("F2"));
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float trigger))
                sb.Append(" trigger=").Append(trigger.ToString("F2"));
            if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool a))
                sb.Append(" A=").Append(a);
        }
        sb.AppendLine();
    }
}
