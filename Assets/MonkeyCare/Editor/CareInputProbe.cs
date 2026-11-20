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

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextWrite) return;
        nextWrite = EditorApplication.timeSinceStartup + 1.0;
        try { Write(); } catch (Exception e) { Debug.LogWarning("CareInputProbe: " + e.Message); }
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
        try
        {
            sb.AppendLine("ovrConnected=" + OVRInput.GetConnectedControllers());
            sb.AppendLine("ovrRTouch=" + OVRInput.IsControllerConnected(OVRInput.Controller.RTouch));
            sb.AppendLine("ovrLTouch=" + OVRInput.IsControllerConnected(OVRInput.Controller.LTouch));
        }
        catch (Exception e) { sb.AppendLine("ovrError=" + e.Message); }

        AppendPlayerHeight(sb);

        File.WriteAllText(Folder + "input-probe.txt", sb.ToString());
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
                sb.AppendLine("rig y=" + session.rig.transform.position.y.ToString("F2") +
                    " eyeY=" + (session.rig.centerEyeAnchor ? session.rig.centerEyeAnchor.position.y.ToString("F2") : "?") +
                    " trackingLocalY=" + (session.rig.trackingSpace ? session.rig.trackingSpace.localPosition.y.ToString("F3") : "?"));
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
