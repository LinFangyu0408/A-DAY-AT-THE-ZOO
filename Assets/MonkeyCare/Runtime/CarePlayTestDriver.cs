#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ADayAtTheZoo.Monkeys;
using ADayAtTheZoo.Monkeys.Care;
[DefaultExecutionOrder(-40)]
public sealed partial class CarePlayTestDriver : MonoBehaviour
{
    MonkeyCareSession session;
    new Camera camera;
    MonkeyCareNpc feed, itch, third, feeding;
    CareItem banana, scratcher;
    Transform previewHand;
    UnityEngine.InputSystem.Mouse testMouse;
    UnityEngine.InputSystem.InputSettings originalInputSettings, testInputSettings;
    readonly StringBuilder report = new();
    int assertions;
    void Awake()
    {
        originalInputSettings = UnityEngine.InputSystem.InputSystem.settings;
        testInputSettings = Instantiate(originalInputSettings);
        testInputSettings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        testInputSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        UnityEngine.InputSystem.InputSystem.settings = testInputSettings;
        foreach (var platform in FindObjectsByType<VrPlatformSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None)) platform.enabled = false;
        session = FindFirstObjectByType<MonkeyCareSession>(); session.testAllowUnfocused = true;
        session.rig.gameObject.SetActive(false);
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) cam.enabled = false;
        var go = new GameObject("Care Temporary Preview Camera"); camera = go.AddComponent<Camera>(); camera.tag = "MainCamera";
        if (File.Exists("work/monkey-care/record-demo.request")) { File.Delete("work/monkey-care/record-demo.request"); CareDemoRecorder.Section="core"; camera.gameObject.AddComponent<CareDemoRecorder>(); }
        camera.fieldOfView = 55; camera.nearClipPlane = .05f;
        previewHand = new GameObject("Care Preview Hand").transform; previewHand.SetParent(camera.transform, false);
        previewHand.localPosition = new Vector3(.35f, -.3f, .5f);
        feed = session.monkeys[0]; itch = session.monkeys[1]; third = session.monkeys[2];
        banana = session.items[0]; scratcher = session.items[1];
        Aim(feed); Time.timeScale = camera.GetComponent<CareDemoRecorder>() ? 1.2f : 4; Application.runInBackground = true;
        Application.logMessageReceived += Log;
        File.WriteAllText("work/monkey-care/play-tests.txt", "RUNNING scoring v1.0\n");
    }
    void Log(string condition, string stack, LogType type)
    {
        if ((type == LogType.Error || type == LogType.Exception) && (condition.Contains("Care") || stack.Contains("MonkeyCare") || stack.Contains("CareItem")))
            report.AppendLine("RUNTIME ERROR: " + condition + "\n" + stack);
    }
    void Update()
    {
        if (feeding && feeding.HasDemand) { feeding.OfferFood(banana); feeding.OfferFood(banana); }
    }
    void Check(bool ok, string description)
    {
        assertions++; report.AppendLine((ok ? "PASS " : "FAIL ") + description);
        if (!ok) throw new Exception("Care play assertion: " + description);
    }
    IEnumerator Start()
    {
        File.AppendAllText("work/monkey-care/play-tests.txt", "Coroutine started\n");
        var steps = new Stack<IEnumerator>();
        steps.Push(camera.GetComponent<CareDemoRecorder>() ? RecordRound() :
            SessionState.GetBool("CareAudioSupplyOnly", false) ? CheckAudioAndSupply() : Run());
        while (steps.Count > 0)
        {
            object next;
            try
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                next = step.Current;
            }
            catch (Exception ex) { report.AppendLine("FAIL " + ex); break; }
            if (next is IEnumerator nested) { steps.Push(nested); continue; }
            yield return next;
        }
        File.WriteAllText("work/monkey-care/play-tests.txt", "Assertions: " + assertions + "\n" + report);
        Time.timeScale = 1; SessionState.SetBool("MonkeyCarePlayTestActive", false); EditorApplication.ExitPlaymode();
    }
    IEnumerator CheckAudioAndSupply()
    {
        while (!session.Begun) yield return null;
        yield return new WaitForSecondsRealtime(.35f);
        var music = session.transform.Find("Care Music").GetComponent<AudioSource>();
        Check(music.clip && music.loop && music.isPlaying && music.volume > 0,
            "background music is playing and fading in");
        Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled) == 1,
            "one active audio listener");

        var supply = scratcher.transform.position;
        Check(scratcher.Grab(previewHand), "new scratch tool can be grabbed");
        scratcher.Release();
        yield return new WaitForSeconds(scratcher.respawnSeconds + .2f);
        Check(Vector3.Distance(scratcher.transform.position, supply) < .01f && scratcher.Available,
            "dropped scratch tool returns to its original supply position");

        Check(scratcher.Grab(previewHand), "returned tool can be grabbed again");
        scratcher.Consume();
        Check(!scratcher.Available, "consumed tool waits before replenishing");
        session.testAllowUnfocused = false;
        session.SendMessage("OnApplicationFocus", false);
        yield return new WaitForSeconds(scratcher.respawnSeconds + .2f);
        Check(scratcher.Available && Vector3.Distance(scratcher.transform.position, supply) < .01f,
            "consumed scratch tool replenishes even if game input is unfocused");
    }

    IEnumerator Run()
    {
        float until = 0, saved = 0;
        float watchdog = Time.realtimeSinceStartup;
        while (!session.Begun)
        {
            if (Time.realtimeSinceStartup - watchdog > 15) throw new Exception("Session camera start timeout");
            yield return null;
        }
        if (SessionState.GetBool("CareInputOnly", false))
        {
            yield return null; Capture("ready");
            while (MonkeyCareSession.GameTime < session.readySeconds + .2f) yield return null;
            var supply = session.roundArtwork.bananaAnchor;
            camera.transform.position = supply.position + new Vector3(0, 1.2f, -2);
            camera.transform.LookAt(supply.position + Vector3.up * .5f);
            yield return null; Capture("banana-at-crate");
            goto InputChecks;
        }
        yield return null; Capture("ready");
        Check(!camera.transform.Find("Care SCORE HUD").gameObject.activeSelf && !session.roundArtwork.bananaAnchor.Find("Care BANANA HUD").gameObject.activeSelf,
            "score and banana HUD stay hidden during READY GO");
        Check(session.Round.Bananas == 12 && session.Round.Score == 48 && session.Round.Stars == 0, "initial 12 bananas / 48 points / zero stars");
        while (MonkeyCareSession.GameTime < 14) yield return null;
        Check(camera.transform.Find("Care SCORE HUD").gameObject.activeSelf && session.roundArtwork.bananaAnchor.Find("Care BANANA HUD").gameObject.activeSelf && !camera.transform.Find("Care READY GO"),
            "HUD appears only after READY GO disappears");
        Check(!feed.HasDemand && !itch.HasDemand && !third.HasDemand, "familiarization has no tasks or resource spend");
        while (MonkeyCareSession.GameTime < 15.3f) yield return null;
        Check(feed.Accepts(CareNeed.Hunger) && !itch.HasDemand, "first hunger at 15s");
        Capture("hunger"); feeding = feed;
        while (feed.Progress.Fed < 2) yield return null;
        feeding = null; yield return null; saved = feed.Progress.Fed;
        until = MonkeyCareSession.GameTime + 2;
        while (MonkeyCareSession.GameTime < until) yield return null;
        Check(Mathf.Approximately(saved, feed.Progress.Fed) && feed.Phase == MonkeyAction.Hungry, "feeding interruption retains progress");
        Capture("feeding-three-segments"); feeding = feed;
        while (feed.HasDemand) yield return null;
        feeding = null;
        Check(feed.Progress.Fed == 8 && !feed.Progress.Late && session.Round.CompletedTotal == 1 && session.Round.Bananas == 11,
            "normal eight seconds, duplicate offers do not accelerate or double consume");
        Check(feed.Celebrating && feed.Phase == MonkeyAction.Happy && !banana.Available && feed.victory.repeats == 3, "Victory x3 and banana consumed");
        Check(itch.Accepts(CareNeed.Itch), "second monkey itch at 25s");
        float celebrationStart = MonkeyCareSession.GameTime;
        while (feed.Celebrating) yield return null;
        Check(MonkeyCareSession.GameTime - celebrationStart >= feed.library.GetClip(MonkeyAction.Happy).length * 3 - .4f, "Victory three loops before Wave");
        Check(feed.Phase == MonkeyAction.Hi && banana.Available, "banana replenished after two seconds");
        while (MonkeyCareSession.GameTime < 35.3f) yield return null;
        Check(third.Accepts(CareNeed.Hunger), "third monkey hunger at 35s");
        scratcher.Grab(previewHand); Aim(itch); itch.AddStroke(scratcher);
        Check(itch.Progress.Strokes == 1 && itch.HasDemand, "first stroke fills one segment");
        yield return new WaitForSeconds(.3f); Capture("scratch-three-segments");
        itch.AddStroke(scratcher); itch.AddStroke(scratcher);
        Check(itch.Celebrating && itch.Phase == MonkeyAction.Cute1 && !scratcher.Available, "three strokes consume tool and trigger Cute1");
        celebrationStart = MonkeyCareSession.GameTime;
        while (itch.Celebrating) yield return null;
        Check(MonkeyCareSession.GameTime - celebrationStart >= itch.library.GetClip(MonkeyAction.Cute1).length * 2 - .4f, "Cute1 two loops before Wave");
        Check(itch.Phase == MonkeyAction.Hi, "scratch feedback ends in Wave");
        while (MonkeyCareSession.GameTime < 58) yield return null;
        Check(feed.Accepts(CareNeed.Itch), "next task alternates and waits from actual completion");
        Aim(third); feeding = third;
        while (third.Progress.Fed < 6) yield return null;
        feeding = null; yield return null; saved = third.Progress.Fed;
        until = MonkeyCareSession.GameTime + 3;
        while (MonkeyCareSession.GameTime < until) yield return null;
        Check(third.Progress.Late && third.Progress.FeedTarget == 10 && Mathf.Approximately(saved, third.Progress.Fed), "timeout upgrades total to ten and preserves six seconds");
        Capture("feeding-four-segments"); feeding = third;
        while (third.HasDemand) yield return null;
        feeding = null;
        Check(third.Progress.Fed == 10 && third.NextIn > 21.5f && third.NextIn <= 22.1f, "rescued feed waits twenty-two seconds from success");
        while (MonkeyCareSession.GameTime < 76) yield return null;
        Aim(feed); scratcher.Grab(previewHand);
        for (int i = 0; i < 3; i++) feed.AddStroke(scratcher);
        Check(feed.HasDemand && feed.Progress.Late && feed.Progress.StrokeTarget == 4, "rescued itch still needs fourth stroke");
        yield return new WaitForSeconds(.3f); Capture("scratch-four-segments"); feed.AddStroke(scratcher);
        Check(feed.Celebrating && !scratcher.Available, "fourth stroke finishes rescue");
        while (!third.HasDemand) yield return null;
        Check(third.Need == CareNeed.Itch && third.Progress.StrokeTarget == 3 && !third.Progress.Late, "task after rescue restores normal requirement");
        scratcher.Grab(previewHand); third.AddStroke(scratcher); third.AddStroke(scratcher); third.AddStroke(scratcher);
        while (!scratcher.Available) yield return null;
        scratcher.Grab(previewHand); scratcher.Release();
        Check(!scratcher.Held && !scratcher.Caring, "release clears carry feedback");
        scratcher.transform.position = new Vector3(0, -5, 0); yield return null; yield return null;
        Check(scratcher.transform.position.y > -.4f && scratcher.Available, "lost tool returns to supply");
        Check(session.Round.CompletedTotal == 5 && session.Round.FeedingTotal == 2 && session.Round.ScratchTotal == 3 && session.Round.Bananas == 10, "round aggregates task types and only consumes successful feeding");
        while (!session.Round.Frozen) yield return null;
        yield return null; Capture("results-live");
        Check(session.monkeys.All(n => !n.HasDemand && !n.Celebrating), "230s clears all task and feedback state");
        Check(FindObjectsByType<CareResultButton>(FindObjectsSortMode.None).All(b => !b.available), "results display interval locks buttons until 240s");
        int count = session.Round.CompletedTotal; yield return new WaitForSeconds(3);
        Check(session.Round.CompletedTotal == count && session.Round.Frozen, "results statistics stay frozen");
        while (MonkeyCareSession.GameTime < 240) yield return null;
        yield return null;
        Check(FindObjectsByType<CareResultButton>(FindObjectsSortMode.None).All(b => b.available), "continue and restart available at 240s");
        Check(!report.ToString().Contains("RUNTIME ERROR"), "no care runtime errors");
        CareDemoRecorder.Section="pause";
        var restart = FindObjectsByType<CareResultButton>(FindObjectsSortMode.None).First(b => b.restart); restart.Activate();
        yield return null;
        Check(!session.Round.Frozen && session.Round.CompletedTotal == 0 && session.Round.Bananas == 12 && session.monkeys.All(n => n.CurrentTaskIndex == 0), "restart resets round, inventory and all NPC task sequences");
        Check(!camera.transform.Find("Care SCORE HUD").gameObject.activeSelf && !session.roundArtwork.bananaAnchor.Find("Care BANANA HUD").gameObject.activeSelf,
            "restart hides HUD for READY GO again");
        InputChecks:
        CareDemoRecorder.Section="input";
        testMouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>("Care Test Mouse");
        MouseButtons(false, false); yield return null; yield return null;
        var bananaBounds = banana.GetComponent<Collider>().bounds;
        camera.transform.position = bananaBounds.center + new Vector3(0, .8f, -.05f); camera.transform.LookAt(bananaBounds.center);
        MouseButtons(true, true); yield return null; yield return null;
        report.AppendLine("INPUT: mouse=" + (UnityEngine.InputSystem.Mouse.current == testMouse) + " left=" + testMouse.leftButton.isPressed + " right=" + testMouse.rightButton.isPressed +
            " XR=" + UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand).isValid + " OVR=" + OVRInput.IsControllerConnected(OVRInput.Controller.RTouch));
        foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, 2).OrderBy(h => h.distance))
            report.AppendLine("RAY " + string.Join("/", hit.collider.GetComponentsInParent<Transform>().Reverse().Select(t => t.name)) + " distance=" + hit.distance + " layer=" + hit.collider.gameObject.layer + " item=" + hit.collider.GetComponentInParent<CareItem>());
        Check(session.HeldItem == banana && banana.Held, "Trigger plus Grip actually grabs the original banana");
        MouseButtons(false, true); yield return null; yield return null;
        Check(session.HeldItem == banana && banana.Held, "releasing Trigger while holding Grip retains the item");
        MouseButtons(false, false); yield return null; yield return null;
        Check(!banana.Held && session.Round.Bananas == 12, "releasing Grip drops banana without spending stock");
        banana.RestoreSupply(); Physics.SyncTransforms();
        while (MonkeyCareSession.GameTime < 15.4f) yield return null;
        bananaBounds = banana.GetComponent<Collider>().bounds;
        camera.transform.position = bananaBounds.center + new Vector3(0, .8f, -.05f); camera.transform.LookAt(bananaBounds.center);
        MouseButtons(true, true); yield return null; yield return null;
        MouseButtons(false, true); Aim(feed);
        until = MonkeyCareSession.GameTime + 1.2f;
        while (MonkeyCareSession.GameTime < until) yield return null;
        Check(session.RayNpc == feed && feed.Progress.Fed > .5f, "held banana feeds through actual straight-ray detection");
        camera.transform.Rotate(0, 100, 0); yield return null; yield return null;
        saved = feed.Progress.Fed; until = MonkeyCareSession.GameTime + .5f;
        while (MonkeyCareSession.GameTime < until) yield return null;
        Check(Mathf.Approximately(saved, feed.Progress.Fed), "looking away pauses actual ray feeding");
        MouseButtons(false, false); yield return null; yield return null;
        banana.GetComponent<Rigidbody>().isKinematic = true;
        banana.transform.position = feed.transform.Find("Care Contact").GetComponent<Collider>().bounds.center;
        Physics.SyncTransforms(); saved = feed.Progress.Fed;
        until = MonkeyCareSession.GameTime + 1.2f;
        while (MonkeyCareSession.GameTime < until) yield return null;
        Check(feed.Progress.Fed > saved + .5f && feed.Progress.Fed < saved + 1.6f, "physical contact feeds once per frame after switching from ray");
        banana.RestoreSupply(); Physics.SyncTransforms();
        foreach (var npc in session.monkeys)
            Check(session.locomotion.IsLandingClear(npc.teleportPoint.Landing) && session.locomotion.TryTeleport(npc.teleportPoint.Landing), "safe teleport landing for " + npc.name);
        Check(!session.locomotion.TryTeleport(new Vector3(0, -5, 0)), "underwater teleport is rejected");
        UnityEngine.InputSystem.InputSystem.RemoveDevice(testMouse); testMouse = null; Aim(feed);
        var crate = session.roundArtwork.bananaAnchor;
        camera.transform.position = crate.position + new Vector3(0, 1.2f, -2); camera.transform.LookAt(crate.position + Vector3.up * .5f);
        yield return null; Capture("banana-at-crate");
        Check(Vector3.Distance(crate.Find("Care BANANA HUD").position, crate.position + session.roundArtwork.bananaOffset) < .01f,
            "banana count stays anchored above the named crate");
        Aim(feed);
        foreach (int target in new[] { 0, 13, 18 })
        {
            foreach (Transform child in camera.transform)
                if (child.name.StartsWith("Care ")) child.gameObject.SetActive(false);
            var round = new CareRound(); int completed = 0;
            for (int task = 0; task < 6; task++) for (int monkey = 0; monkey < 3 && completed < target; monkey++)
            { round.Complete(monkey, task, (task + (monkey == 1 ? 1 : 0)) % 2 == 0 ? CareNeed.Hunger : CareNeed.Itch, 200); completed++; }
            round.Freeze(); var view = new CareRoundView(camera.transform, session.roundArtwork); view.ShowResults(); view.Update(round, 240);
            yield return null; Capture("results-" + target);
        }
    }
    void Aim(MonkeyCareNpc npc)
    {
        Vector3 center = npc.transform.position + Vector3.up * .8f;
        camera.transform.position = center + npc.transform.forward * 3.4f + Vector3.up * .25f;
        camera.transform.LookAt(center + Vector3.up * .15f);
    }
    void MouseButtons(bool trigger, bool grip)
    {
        var state = new UnityEngine.InputSystem.LowLevel.MouseState();
        state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, trigger).WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right, grip);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(testMouse, state);
        testMouse.MakeCurrent(); UnityEngine.InputSystem.InputSystem.Update();
    }
    void Capture(string label)
    {
        Canvas.ForceUpdateCanvases();
        var previous = RenderTexture.active; var originalTarget = camera.targetTexture;
        var rt = new RenderTexture(1280, 1000, 24); var image = new Texture2D(1280, 1000, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 1280, 1000), 0, 0); image.Apply();
            File.WriteAllBytes("work/monkey-care/" + label + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = originalTarget; RenderTexture.active = previous; Destroy(rt); Destroy(image); }
    }
    void OnDestroy()
    {
        Application.logMessageReceived -= Log;
        if (testMouse != null && testMouse.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(testMouse);
        if (originalInputSettings) UnityEngine.InputSystem.InputSystem.settings = originalInputSettings;
        if (testInputSettings) Destroy(testInputSettings);
    }
}

#endif





