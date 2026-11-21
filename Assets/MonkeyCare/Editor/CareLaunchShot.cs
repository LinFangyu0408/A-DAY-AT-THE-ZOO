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
    const string ConfirmKey = "CareLaunchShotConfirm";
    const string ResultsKey = "CareLaunchShotResults";
    const string Folder = "work/monkey-care/";

    static double started;
    static double audioWrittenAt;
    static double confirmedAt;
    static bool autoConfirm, confirmed, autoResults, resultsShown, longRun;

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
            // A request whose text contains "confirm" also presses A once, so the
            // run covers the READY -> round music hand-off from the player side.
            string request = "";
            try { request = File.ReadAllText(Request); } catch { }
            autoConfirm = request.Contains("confirm");
            autoResults = request.Contains("results");
            autoConfirm |= autoResults;
            // EnterPlaymode reloads the domain, which wipes static fields; the
            // session state is the only thing that survives into play mode.
            SessionState.SetBool(ConfirmKey, autoConfirm);
            SessionState.SetBool(ResultsKey, autoResults);
            SessionState.SetBool("CareLaunchTrace", request.Contains("trace"));
            // "long" keeps the run alive long enough to see the guided lesson
            // reach its feeding step (the timed walk/look steps take ~15 s).
            SessionState.SetBool("CareLaunchLong", request.Contains("long"));
            tracedAt = 0;
            // A second copy of the intent that survives a domain reload without
            // relying on SessionState: entering play mode reloads the domain and
            // the statics were being lost, which left the run parked on READY.
            try { File.WriteAllText(Folder + "shot.active", request); } catch { }
            File.Delete(Request);
            SessionState.SetBool(Active, true);
            started = 0;
            audioWrittenAt = 0;
            confirmedAt = 0;
            confirmed = false;
            resultsShown = false;
            EditorApplication.EnterPlaymode();
            return;
        }
        if (!SessionState.GetBool(Active, false)) return;
        autoConfirm = SessionState.GetBool(ConfirmKey, false);
        autoResults = SessionState.GetBool(ResultsKey, false);
        longRun = SessionState.GetBool("CareLaunchLong", false) || Pending("long");
        // Domain reload can wipe the mirror above, so fall back to the on-disk
        // intent once and fold it back into the session state.
        if (!autoConfirm && !autoResults)
        {
            autoConfirm = Pending("confirm");
            autoResults = Pending("results");
            autoConfirm |= autoResults;
            if (autoConfirm)
            {
                SessionState.SetBool(ConfirmKey, autoConfirm);
                SessionState.SetBool(ResultsKey, autoResults);
                try { File.Delete(Folder + "shot.active"); } catch { }
            }
        }
        if (started == 0) started = EditorApplication.timeSinceStartup;
        double age = EditorApplication.timeSinceStartup - started;
        Trace(age);

        // The press waits for the soundtrack to be cued. A cold Play spends a
        // few seconds loading the wav; releasing A before that used to be
        // swallowed because there was no session to talk to yet, and the run
        // was left sitting on the READY gate.
        if (autoConfirm && age > 1.5 && !confirmed && MusicCued())
        {
            confirmed = ConfirmThroughSession();
            if (confirmed) confirmedAt = age;
        }
        // "results" request: skip the lesson, run the clock down to the last
        // frame and let the session's own freeze branch open the results card,
        // so the run proves the real path (card + victory score) not a mock.
        if (autoResults && confirmed && age > confirmedAt + 1.6 && !resultsShown)
        {
            resultsShown = true;
            JumpToResults();
        }
        if (age > 1.2 && !File.Exists(Folder + "launch-ready.png"))
        {
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "launch-ready.png"));
            WriteState();
        }
        if (age > 4.0 && !autoResults && MusicCued() && !File.Exists(Folder + "launch-gate.png"))
        {
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "launch-gate.png"));
            WriteState();
        }
        // One beat after the card opens: the lesson overlay is destroyed on the
        // next frame, and grabbing the same frame catches it still on screen.
        if (autoResults && resultsShown && age > confirmedAt + 2.2 &&
            !File.Exists(Folder + "launch-results.png"))
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "launch-results.png"));
        // Sample after the round is actually running, not just after the press:
        // the cross-fade needs a moment before the volumes mean anything.
        if (age > 1.5 && MusicCued() && audioWrittenAt == 0 &&
            (confirmed ? age > confirmedAt + 1.2 : age > 3.0))
        {
            audioWrittenAt = age;
            WriteAudioState();
        }
        // Second reading after the sting has had time to ring out: proves the
        // results screen goes quiet instead of looping the fanfare.
        if (autoResults && audioWrittenAt > 0 && age > audioWrittenAt + 6.0 &&
            !File.Exists(Folder + "launch-audio-late.txt"))
            WriteAudioState("launch-audio-late.txt");
        // Long run: the lesson's timed walk/look steps take ~15 s, so this frame
        // catches it sitting on the feeding step with a hungry monkey.
        if (longRun && confirmed && age > confirmedAt + 18 &&
            !File.Exists(Folder + "launch-lesson.png"))
        {
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "launch-lesson.png"));
            WriteState();
            WriteAudioState("launch-audio-lesson.txt");
        }
        double finishAt = audioWrittenAt > 0
            ? audioWrittenAt + (autoResults ? 7.4 : longRun ? 30.0 : 2.5)
            : 60.0;
        if (age > finishAt)
        {
            SessionState.SetBool(Active, false);
            // Never let one run leak its intent into the next Play session.
            SessionState.SetBool(ConfirmKey, false);
            SessionState.SetBool(ResultsKey, false);
            try { File.Delete(Folder + "shot.active"); } catch { }
            EditorApplication.ExitPlaymode();
        }
    }

    // Optional trace: a request containing "trace" writes one line every
    // 0.4 s with the state that decides whether the press lands. Silent
    // otherwise, so ordinary launch runs leave nothing behind.
    static double tracedAt;
    static void Trace(double age)
    {
        if (!SessionState.GetBool("CareLaunchTrace", false) || age < tracedAt + .4) return;
        tracedAt = age;
        var session = Object.FindFirstObjectByType<ADayAtTheZoo.Monkeys.Care.MonkeyCareSession>();
        string line = "age=" + age.ToString("F2") +
                      " auto=" + autoConfirm + " confirmed=" + confirmed +
                      " cued=" + MusicCued() +
                      " session=" + (session ? "yes" : "no") +
                      " gate=" + (session ? ReadyGateActive(session).ToString() : "-") +
                      " tutorial=" + (session ? session.TutorialActive.ToString() : "-") +
                      " round=" + (session ? session.RoundStarted.ToString() : "-") +
                      " elapsed=" + (session ? session.RoundElapsed.ToString("F2") : "-") + "\n";
        try { File.AppendAllText(Folder + "launch-trace.txt", line); } catch { }
    }

    static bool ReadyGateActive(ADayAtTheZoo.Monkeys.Care.MonkeyCareSession session) =>
        typeof(ADayAtTheZoo.Monkeys.Care.MonkeyCareSession)
            .GetField("readyGateActive", System.Reflection.BindingFlags.Instance |
                                         System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(session) is bool value && value;

    static bool MusicCued()    {
        var session = Object.FindFirstObjectByType<ADayAtTheZoo.Monkeys.Care.MonkeyCareSession>();
        return session && session.audioFeedback != null &&
               !string.IsNullOrEmpty(session.audioFeedback.MusicName) &&
               session.audioFeedback.MusicName != "none";
    }

    static bool Pending(string token)
    {
        try
        {
            string path = Folder + "shot.active";
            return File.Exists(path) && File.ReadAllText(path).Contains(token);
        }
        catch { return false; }
    }

    // Confirm through the session instead of injecting a fake gamepad: the
    // input layer has its own coverage, this run is about what the player hears
    // after the press.
    static bool ConfirmThroughSession()
    {
        var session = Object.FindFirstObjectByType<ADayAtTheZoo.Monkeys.Care.MonkeyCareSession>();
        if (!session) return false;
        typeof(ADayAtTheZoo.Monkeys.Care.MonkeyCareSession)
            .GetMethod("AcceptReadyGate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(session, null);
        // Prove the press is audible: the click is a one-shot, so it is only
        // observable on the very frame the gate is confirmed.
        string line = "";
        foreach (var source in session.GetComponentsInChildren<AudioSource>(false))
        {
            if (!source.name.Contains("Ui Click")) continue;
            line += source.name + " playing=" + source.isPlaying +
                    " clip=" + (source.clip ? source.clip.name : "none") +
                    " volume=" + source.volume.ToString("F2") + "\n";
        }
        File.WriteAllText(Folder + "launch-click.txt", line);
        return true;
    }

    static void JumpToResults()
    {
        var session = Object.FindFirstObjectByType<ADayAtTheZoo.Monkeys.Care.MonkeyCareSession>();
        if (!session) return;
        // Push the round clock onto its final frame. LateUpdate sees the cutoff
        // and runs the real freeze: HUD off, results card, victory score.
        // Destroy the lesson component first: it owns the overlay card, and the
        // session method alone would leave that card floating over the results
        // (a real player closes the lesson through the component, which does
        // the same cleanup).
        var tutorial = session.GetComponent<ADayAtTheZoo.Monkeys.Care.CareTutorial>();
        if (tutorial) Object.Destroy(tutorial);
        session.FinishTutorial();
        typeof(ADayAtTheZoo.Monkeys.Care.MonkeyCareSession)
            .GetField("gameTime",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(session, ADayAtTheZoo.Monkeys.Care.CareRound.Cutoff - .01f);
    }

    static void WriteAudioState(string file = "launch-audio.txt")
    {
        var session = Object.FindFirstObjectByType<ADayAtTheZoo.Monkeys.Care.MonkeyCareSession>();
        if (!session) return;
        var audio = session.audioFeedback;
        string line = "round=" + audio.RoundMusicActive +
                      " track=" + audio.MusicName +
                      " ambient=" + audio.AmbientName +
                      " time=" + audio.MusicTime.ToString("F2") +
                      " pitch=" + audio.MusicPitch.ToString("F3") +
                      " victory=" + audio.VictoryPlaying +
                      " victoryClip=" + audio.VictoryClipName +
                      " victoryLen=" + audio.VictoryClipLength.ToString("F2") +
                      " victoryLoop=" + audio.VictoryLoops;
        foreach (var source in session.GetComponentsInChildren<AudioSource>(false))
        {
            if (source.name != "Care Music" && source.name != "Care Ambient Music" &&
                source.name != "Care Victory Sting") continue;
            line += "\n" + source.name + " volume=" + source.volume.ToString("F3") +
                    " time=" + source.time.ToString("F2") +
                    " playing=" + source.isPlaying +
                    " loop=" + source.loop;
        }
        File.WriteAllText(Folder + file, line + "\n");
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
