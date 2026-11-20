#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Launch-shot helper: drop work/monkey-care/shot.request and the editor enters
/// play mode exactly the way a player launches the game (no test driver, no
/// forced platform), then grabs the Game View a few times and writes what the
/// launch actually looked like:
///   work/monkey-care/launch-ready.png  (READY gate, ~1.2s in)
///   work/monkey-care/launch-gate.png   (still waiting for the confirm press)
///   work/monkey-care/launch-state.txt  (device/platform the game picked)
/// ScreenCapture is required: the flat PC HUD is a ScreenSpaceOverlay canvas and
/// a camera render never contains it.
/// </summary>
[InitializeOnLoad]
public static class CareLaunchShot
{
    const string Request = "work/monkey-care/shot.request";
    const string Active = "CareLaunchShotActive";
    const string Folder = "work/monkey-care/";

    static double started;

    static CareLaunchShot()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlaying)
        {
            if (!File.Exists(Request)) return;
            if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().path
                    .EndsWith("HighlandLake_Backup_2026-11-03.unity")) return;
            File.Delete(Request);
            SessionState.SetBool(Active, true);
            started = 0;
            EditorApplication.EnterPlaymode();
            return;
        }
        if (!SessionState.GetBool(Active, false)) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        double age = EditorApplication.timeSinceStartup - started;

        if (age > 1.2 && !File.Exists(Folder + "launch-ready.png"))
        {
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "launch-ready.png"));
            WriteState();
        }
        if (age > 4.0 && !File.Exists(Folder + "launch-gate.png"))
        {
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "launch-gate.png"));
            WriteState();
        }
        if (age > 6.5)
        {
            SessionState.SetBool(Active, false);
            EditorApplication.ExitPlaymode();
        }
    }

    static void WriteState()
    {
        var session = Object.FindFirstObjectByType<ADayAtTheZoo.Monkeys.Care.MonkeyCareSession>();
        string state = "playing=" + EditorApplication.isPlaying +
                       " xr=" + UnityEngine.XR.XRSettings.isDeviceActive +
                       " flatUi=" + ADayAtTheZoo.Monkeys.Care.CarePlatform.FlatUi +
                       " padStyle=" + ADayAtTheZoo.Monkeys.Care.CareInput.PadStyle +
                       " confirm=" + ADayAtTheZoo.Monkeys.Care.CareInput.ConfirmLabel +
                       " back=" + ADayAtTheZoo.Monkeys.Care.CareInput.BackLabel +
                       " device=" + ADayAtTheZoo.Monkeys.Care.CareInput.DescribeDevice() +
                       " screen=" + Screen.width + "x" + Screen.height;
        if (session)
            state += " readyGateActive=" +
                     typeof(ADayAtTheZoo.Monkeys.Care.MonkeyCareSession)
                         .GetField("readyGateActive",
                             System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                         ?.GetValue(session);
        File.WriteAllText(Folder + "launch-state.txt", state + "\n");
    }
}
#endif
