using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ADayAtTheZoo.Monkeys;
using ADayAtTheZoo.Monkeys.Care;

[InitializeOnLoad]
public static class CarePlayTests
{
    const string Request = "work/monkey-care/play-test.request";
    const string Active = "MonkeyCarePlayTestActive";
    static double started, statusAt;
    static CarePlayTests()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) Boot(); };
    }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying)
        {
            if (File.Exists("work/monkey-care/stop-play.request"))
            { File.Delete("work/monkey-care/stop-play.request"); Finish(); return; }
            Boot(); return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request) || File.Exists("work/monkey-care/install.request") || File.Exists("work/monkey-care/refresh.request")) return;
        if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().path.EndsWith("HighlandLake_Backup_2026-11-03.unity")) return;
        if (!UnityEngine.Object.FindFirstObjectByType<MonkeyCareSession>()) return;
        if (File.Exists("work/monkey-care/install-result.txt") && !File.ReadAllText("work/monkey-care/install-result.txt").StartsWith("SUCCESS")) return;
        string mode = File.ReadAllText(Request).Trim();
        SessionState.SetBool("CareInputOnly", mode == "input");
        SessionState.SetBool("CareAudioSupplyOnly", mode == "audio-supply");
        File.Delete(Request); SessionState.SetBool(Active, true); started = 0; EditorApplication.EnterPlaymode();
    }
    static void Boot()
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (EditorApplication.timeSinceStartup >= statusAt)
        {
            statusAt = EditorApplication.timeSinceStartup + 5;
            File.WriteAllText("work/monkey-care/play-state.txt", "playing=" + EditorApplication.isPlaying + " paused=" + EditorApplication.isPaused + " time=" + MonkeyCareSession.GameTime);
        }
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (started == 0) started = EditorApplication.timeSinceStartup;
        var driver = UnityEngine.Object.FindFirstObjectByType<CarePlayTestDriver>();
        var timeout = UnityEngine.Object.FindFirstObjectByType<CareDemoRecorder>() ? 420 : 160;
        if (EditorApplication.timeSinceStartup - started > timeout)
        { File.AppendAllText("work/monkey-care/play-tests.txt", "\nFAIL: editor watchdog timeout"); Finish(); return; }
        if (!driver) new GameObject("Care Temporary Play Test").AddComponent<CarePlayTestDriver>();
    }
    public static void Finish() { SessionState.SetBool(Active, false); EditorApplication.ExitPlaymode(); }
}


