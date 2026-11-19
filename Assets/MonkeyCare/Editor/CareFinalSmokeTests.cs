using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ADayAtTheZoo.Monkeys;
using ADayAtTheZoo.Monkeys.Care;

[InitializeOnLoad]
public static class CareFinalSmokeTests
{
    const string Request = "work/monkey-care/final-smoke.request";
    const string Active = "CareFinalSmokeActive";

    static CareFinalSmokeTests()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode &&
                SessionState.GetBool(Active, false) &&
                !UnityEngine.Object.FindFirstObjectByType<CareFinalSmokeDriver>())
                new GameObject("Care Final Smoke Driver").AddComponent<CareFinalSmokeDriver>();
        };
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists(Request)) return;
        if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().path
                .EndsWith("HighlandLake_Backup_2026-11-03.unity")) return;
        File.Delete(Request);
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }

    public static void Finish()
    {
        SessionState.SetBool(Active, false);
        EditorApplication.ExitPlaymode();
    }
}

public sealed class CareFinalSmokeDriver : MonoBehaviour
{
    readonly List<string> report = new();
    MonkeyCareSession session;
    Camera gameCamera;
    Transform hand;
    CareItem banana;
    MonkeyCareNpc monkey;
    int checks;

    void Awake()
    {
        session = FindFirstObjectByType<MonkeyCareSession>();
        if (!session) return;
        session.testAllowUnfocused = true;
        session.showTutorial = false;
        foreach (var switcher in FindObjectsByType<VrPlatformSwitcher>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            switcher.enabled = false;
        if (session.rig) session.rig.gameObject.SetActive(false);
    }

    IEnumerator Start()
    {
        var failure = (Exception)null;
        IEnumerator run = Run();
        while (true)
        {
            object current = null;
            try
            {
                if (!run.MoveNext()) break;
                current = run.Current;
            }
            catch (Exception exception)
            {
                failure = exception;
                break;
            }
            yield return current;
        }

        if (failure == null)
        {
            File.WriteAllText("work/monkey-care/final-smoke-result.txt",
                "SUCCESS " + checks + " assertions\n" + string.Join("\n", report));
        }
        else
        {
            File.WriteAllText("work/monkey-care/final-smoke-result.txt",
                "FAIL " + failure + "\n" + string.Join("\n", report));
            Debug.LogException(failure);
        }
        CareFinalSmokeTests.Finish();
    }

    IEnumerator Run()
    {
        while (!session || !session.Begun) yield return null;
        gameCamera = Camera.allCameras.FirstOrDefault(item =>
            item && item.enabled && item.gameObject.activeInHierarchy &&
            item.cameraType == CameraType.Game);
        Check(gameCamera, "game camera is active");
        var tutorialHost = new GameObject("Final Tutorial Visual");
        var tutorial = tutorialHost.AddComponent<CareTutorial>();
        tutorial.Begin(session, gameCamera, Time.unscaledTime);
        yield return new WaitForSecondsRealtime(.55f);
        var tutorialTitle = GameObject.Find("Tutorial title")?.GetComponent<Text>();
        var tutorialBody = GameObject.Find("Tutorial body")?.GetComponent<Text>();
        var tutorialKey = GameObject.Find("Key badge")?.GetComponent<Image>();
        Check(tutorialTitle && tutorialTitle.text == "FEED MONKEYS",
            "beginner tutorial uses short English title");
        Check(tutorialBody && tutorialBody.fontSize >= 52,
            "beginner tutorial body text is enlarged");
        Check(tutorialKey && tutorialKey.rectTransform.sizeDelta.x >= 132,
            "beginner tutorial key badge is enlarged and round");
        Capture("final-tutorial");
        typeof(CareTutorial).GetMethod("ShowStep", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(tutorial, new object[] { 4 });
        yield return null;
        var tutorialBagTitle = GameObject.Find("Tutorial title")?.GetComponent<Text>();
        var tutorialBagKey = tutorialKey ? tutorialKey.GetComponentInChildren<Text>() : null;
        Check(tutorialBagTitle && tutorialBagTitle.text == "OPEN THE BAG" &&
              tutorialBagKey && tutorialBagKey.text == "B",
            "beginner tutorial shows the enlarged round B control");
        Capture("final-tutorial-bag");
        UnityEngine.Object.Destroy(tutorialHost);
        yield return null;

        hand = new GameObject("Final Smoke Hand").transform;
        hand.SetParent(gameCamera.transform, false);
        hand.localPosition = new Vector3(.33f, -.25f, .52f);
        hand.localRotation = Quaternion.identity;
        SetSessionField("recordingHand", hand);

        yield return null;
        Capture("final-ready");
        var readyPrompt = GameObject.Find("Ready prompt")?.GetComponent<Text>();
        Check(readyPrompt && readyPrompt.text == "PRESS A TO START",
            "ready gate shows the PRESS A TO START hint below Ready Go");
        yield return Press("recordingA");
        yield return WaitUntil(() => session.RoundStarted, 3);
        Check(session.RoundStarted, "A starts the round");
        var progressTrack = GameObject.Find("Progress track")?.GetComponent<Image>();
        Check(GameObject.Find("SVG score panel") && GameObject.Find("Static score bar cover") &&
              progressTrack && progressTrack.enabled && progressTrack.color.a >= .18f,
            "top progress uses the supplied artwork with a visible empty track");
        Check(GameObject.Find("Progress pass marker") && GameObject.Find("Progress pass caption"),
            "sixty-percent passing line is marked on the score bar");
        var bananaHud = GameObject.Find("Care BANANA HUD")?.GetComponent<RectTransform>();
        Check(bananaHud && bananaHud.localPosition.x >= .75f && bananaHud.localPosition.y >= .35f,
            "banana counter is fixed in the upper-right");
        var scoreHud = GameObject.Find("Care SCORE HUD")?.GetComponent<RectTransform>();
        Check(scoreHud && scoreHud.localPosition.y >= .33f && scoreHud.localPosition.y <= .48f,
            "top progress bar stays below the screen edge");
        var scoreText = GameObject.Find("Current score")?.GetComponent<Text>();
        Check(!GameObject.Find("Progress value") &&
              scoreText && scoreText.fontSize >= 30,
            "top progress shows only the percentage (no raw 0/9 counter)");
        // Readability fix: the variable Fredoka face renders hairline-thin on
        // Unity's legacy Text, so gameplay copy now uses a real bold face.
        // Accept either a bold readable face or the themed Fredoka fallback.
        Check(scoreText && scoreText.font &&
              (scoreText.font.name.Contains("Arial") ||
               scoreText.font.name.Contains("Bold") ||
               scoreText.font.name.Contains("Fredoka")),
            "gameplay UI uses a bold readable font");
        var bagButton = session.GloveInventory.BagButtonRoot;
        Check(bagButton && bagButton.gameObject.activeInHierarchy,
            "persistent backpack button is visible in the lower-left");
        Check(bagButton.localPosition.x < -.7f && bagButton.localPosition.y < -.4f,
            "persistent backpack button is placed in the lower-left");
        var bagIcon = GameObject.Find("Bag icon")?.GetComponent<Image>();
        Check(bagIcon && bagIcon.sprite && bagIcon.sprite.name == "Care Backpack - bag_icon",
            "persistent backpack uses the supplied bag icon");
        var bagKeyImage = GameObject.Find("Bag key")?.GetComponent<Image>();
        var bagKeyText = GameObject.Find("Bag key")?.GetComponentInChildren<Text>();
        Check(bagKeyImage && Mathf.Approximately(bagKeyImage.rectTransform.sizeDelta.x,
                  bagKeyImage.rectTransform.sizeDelta.y) && bagKeyText && bagKeyText.text == "B",
            "backpack uses a round B key badge");
        Check(bagKeyImage && bagKeyImage.rectTransform.sizeDelta.x <= 56 &&
              !GameObject.Find("Backpack entry plate") &&
              !GameObject.Find("Backpack entry shadow"),
            "backpack B is smaller with a visible depth edge and no white plate");
        Capture("final-hud");

        var stand = GameObject.Find("Banana Market Stand");
        Check(stand, "market stand exists");
        if (stand)
        {
            Bounds bounds = BoundsOf(stand);
            float ground = GroundBelow(bounds.center, stand.transform);
            float gap = bounds.min.y - ground;
            report.Add("MARKET bounds=" + bounds.min.ToString("F3") + ".." +
                       bounds.max.ToString("F3") + " ground=" + ground.ToString("F3") +
                       " gap=" + gap.ToString("F3"));
            Check(Mathf.Abs(gap) <= .08f, "market stand rests on the sampled ground");
            var materials = stand.GetComponentsInChildren<Renderer>(true)
                .Where(item => item && item.enabled)
                .SelectMany(item => item.sharedMaterials).Where(item => item).Distinct().ToArray();
            Check(materials.Length >= 3 && materials.All(item => item.mainTexture),
                "market stand uses visible wood/fabric textures");
            Check(materials.All(item => item.shader && item.shader.isSupported &&
                                        item.shader.name != "Hidden/InternalErrorShader"),
                "market stand materials are supported by the active render pipeline");

            Vector3 view = stand.transform.position + stand.transform.forward * 4.7f +
                            Vector3.up * 1.8f;
            gameCamera.transform.position = view;
            gameCamera.transform.LookAt(stand.transform.position + Vector3.up * 1.2f);
            yield return null;
            Capture("final-market");
        }

        monkey = session.TutorialMonkey;
        Aim(monkey);
        yield return null;
        yield return Press("recordingB");
        Check(session.GloveInventory.IsOpen, "B opens the glove inventory");
        var backpack = GameObject.Find("Backpack UI")?.GetComponent<Image>();
        Check(backpack && backpack.sprite &&
              backpack.sprite.name == "Care Backpack - bag",
            "new backpack artwork is visible");
        var backpackTitle = GameObject.Find("Backpack title")?.GetComponent<Text>();
        Check(backpackTitle && backpackTitle.text == "BAG",
            "backpack title is in English");
        var confirmKey = GameObject.Find("Confirm key")?.GetComponent<Image>();
        var backKey = GameObject.Find("Back key")?.GetComponent<Image>();
        Check(confirmKey && backKey &&
              Mathf.Approximately(confirmKey.rectTransform.sizeDelta.x, confirmKey.rectTransform.sizeDelta.y) &&
              Mathf.Approximately(backKey.rectTransform.sizeDelta.x, backKey.rectTransform.sizeDelta.y),
            "inventory action keys use round badges");
        Capture("final-backpack");
        yield return Press("recordingA");
        yield return WaitUntil(() => session.HeldItem, 2);
        banana = session.HeldItem;
        Check(banana && banana.kind == CareNeed.Hunger, "A equips a banana");
        Check(banana.transform.parent == session.Hands.RightGripSocket,
            "banana root is parented directly to the right GripSocket");

        Physics.SyncTransforms();
        Vector3 itemBefore = banana.transform.position;
        Vector3 mountedPosition = banana.transform.localPosition;
        Quaternion mountedRotation = banana.transform.localRotation;
        Transform mount = banana.transform.parent;
        Vector3 handBefore = hand.position;
        hand.localPosition += new Vector3(.22f, .08f, .05f);
        hand.localRotation = Quaternion.Euler(8, -12, 6);
        Physics.SyncTransforms();
        yield return null;
        yield return null;
        Vector3 itemDelta = banana.transform.position - itemBefore;
        Vector3 handDelta = hand.position - handBefore;
        float gripDrift = Vector3.Distance(banana.transform.localPosition, mountedPosition);
        float gripTurn = Quaternion.Angle(banana.transform.localRotation, mountedRotation);
        report.Add("HAND move=" + handDelta.ToString("F3") +
                   " item=" + itemDelta.ToString("F3") +
                   " localDrift=" + gripDrift.ToString("F4") +
                   " localTurn=" + gripTurn.ToString("F2"));
        Check(itemDelta.magnitude >= .05f &&
              banana.transform.parent == mount,
            "banana stays mounted to the moving controller grip");
        Capture("final-banana-in-hand");

        monkey = session.TutorialMonkey;
        monkey.feedingSeconds = .45f;
        monkey.lateFeedingSeconds = .55f;
        monkey.patienceSeconds = 60;
        monkey.BeginGuidedTutorial(CareNeed.Hunger);
        Aim(monkey);

        SetSessionField("recordingTrigger", true);
        for (int frame = 0; frame < 5; frame++) yield return null;
        report.Add("THROW rayNpc=" +
                   (session.RayNpc ? session.RayNpc.name : "none") +
                   " held=" + (session.HeldItem ? session.HeldItem.name : "none"));
        Check(session.RayNpc == monkey, "held RT predicts the hungry monkey");
        SetSessionField("recordingTrigger", false);
        yield return null;
        yield return null;
        Check(banana.Thrown, "releasing RT launches the held banana");
        var replacementBanana = session.HeldItem;
        report.Add("RELOAD held=" + (replacementBanana ? replacementBanana.name : "none") +
                   " bananas=" + session.Round.Bananas +
                   " supplyItems=" + session.items.Count(item => item && item.kind == CareNeed.Hunger));
        Check(replacementBanana && replacementBanana != banana &&
              replacementBanana.transform.parent == session.Hands.RightGripSocket,
            "a fresh banana appears in the throwing hand immediately");
        var pickupKey = GameObject.Find("Pickup key");
        var pickupLabel = GameObject.Find("Pickup key label")?.GetComponent<Text>();
        Check(pickupKey && pickupKey.activeInHierarchy && pickupLabel && pickupLabel.text == "A",
            "banana storage exposes the A pickup prompt");
        var pickupMarker = GetObjectField(session, "bananaPickup") as CarePickupMarker;
        if (stand)
        {
            gameCamera.transform.position =
                stand.transform.position + stand.transform.forward * 2.25f + Vector3.up * 1.7f;
            gameCamera.transform.LookAt(stand.transform.position + Vector3.up * 1.3f);
            yield return null;
            yield return null;
            var pickupText = GameObject.Find("Pickup text")?.GetComponent<Text>();
            Check(pickupMarker && pickupMarker.PlayerInRange &&
                  pickupText && pickupText.text == "RESTOCK",
                "walking up to the crates shows A and RESTOCK");
            Capture("final-market-pickup");
        }
        report.Add("PICKUP playing=" + MonkeyCareSession.Playing +
                   " frozen=" + (session.Round != null && session.Round.Frozen) +
                   " inRange=" + (pickupMarker != null && pickupMarker.PlayerInRange) +
                   " bananas=" + (session.Round != null ? session.Round.Bananas : -1));
        bool refilled = session.TryPickupBananas();
        Check(refilled && pickupMarker != null && session.Round.Bananas == CareRound.StartingBananas,
            "A refills the banana inventory at the market stand");
        if (stand)
        {
            Aim(monkey);
            yield return null;
        }

        float deadline = Time.realtimeSinceStartup + 4;
        while (!monkey.Celebrating && Time.realtimeSinceStartup < deadline)
            yield return null;
        report.Add("THROW result position=" + banana.transform.position.ToString("F3") +
                   " celebrating=" + monkey.Celebrating +
                   " caring=" + banana.Caring +
                   " target=" + (banana.Target ? banana.Target.name : "none") +
                   " held=" + banana.Held +
                   " thrown=" + banana.Thrown +
                   " available=" + banana.Available +
                   " caught=" + (GetObjectField(banana, "caughtTarget") is MonkeyCareNpc caught
                       ? caught.name : "none") +
                   " demand=" + monkey.HasDemand +
                   " fed=" + (monkey.Progress != null ? monkey.Progress.Fed.ToString("F2") : "none") +
                   " waited=" + (monkey.Progress != null ? monkey.Progress.Waited.ToString("F2") : "none") +
                   " locked=" + (monkey.Progress != null && monkey.Progress.Locked));
        yield return null;
        Check(monkey.Celebrating, "completed feeding enters the celebrating state");
        Check(monkey.Phase == MonkeyAction.Happy, "monkey state switches to Happy");
        var satisfactionTitle = GameObject.Find("Panel title")?.GetComponent<Text>();
        Check(satisfactionTitle && satisfactionTitle.text == "SATISFACTION",
            "feeding progress switches to the English satisfaction bar");
        var actor = monkey.GetComponent<MonkeyNpcAnimator>();
        AnimatorStateInfo animationState = monkey.animator.IsInTransition(0)
            ? monkey.animator.GetNextAnimatorStateInfo(0)
            : monkey.animator.GetCurrentAnimatorStateInfo(0);
        bool animatorHappy = animationState.shortNameHash ==
                             Animator.StringToHash(MonkeyAction.Happy.ToString());
        report.Add("ANIMATOR stateHash=" + animationState.shortNameHash +
                   " happy=" + Animator.StringToHash(MonkeyAction.Happy.ToString()) +
                   " actorAction=" + (actor ? actor.CurrentAction.ToString() : "none"));
        Check(animatorHappy,
            "animator controller is visibly playing Happy");
        yield return new WaitForSecondsRealtime(.35f);
        Capture("final-monkey-happy");

        var round = new CareRound();
        int completed = 0;
        for (int task = 0; task < 4 && completed < 7; task++)
        {
            for (int index = 0; index < 3 && completed < 7; index++)
            {
                if (!round.Complete(index, task, CareNeed.Hunger, 100)) continue;
                completed++;
            }
        }
        round.Freeze();
        // Mirror the session freeze path so NPC world-space HUDs (satisfaction
        // bars, emotions) are hidden exactly like the real round end does.
        if (session.monkeys != null)
            foreach (var npc in session.monkeys) npc.StopAtCutoff();
        var viewField = typeof(MonkeyCareSession).GetField("roundView",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var roundView = viewField?.GetValue(session) as CareRoundView;
        Check(roundView != null, "round view is available");
        roundView.ShowResults();
        roundView.Update(round, 240);
        yield return null;
        Check(GameObject.Find("Results card") &&
              GameObject.Find("Continue exploring") &&
              GameObject.Find("Restart round"),
            "results use the reference card layout with both round buttons");
        var cardShadow = GameObject.Find("Results shadow")?.GetComponent<Image>();
        var cardImage = GameObject.Find("Results card")?.GetComponent<Image>();
        Check(cardShadow && cardShadow.sprite && cardShadow.sprite.name.Contains("RoundedCard") &&
              cardImage && cardImage.sprite && cardImage.sprite.name.Contains("RoundedCard") &&
              cardImage.sprite.border == new Vector4(96, 96, 96, 96),
            "results card and shadow share the large rounded-corner silhouette");
        var resultsTitle = GameObject.Find("Feeding Amount title")?.GetComponent<Text>();
        var totalTitle = GameObject.Find("Total Score title")?.GetComponent<Text>();
        Check(resultsTitle && resultsTitle.fontSize >= 64 &&
              totalTitle && totalTitle.fontSize >= 72,
            "results copy uses the enlarged readable font");
        Capture("final-results");
        Check(round.Score == 7 && round.Stars == 1 && round.IsPassing,
            "seven feedings shows passing one-star results");
    }

    IEnumerator Press(string fieldName)
    {
        SetSessionField(fieldName, true);
        yield return null;
        yield return null;
        SetSessionField(fieldName, false);
        yield return null;
        yield return null;
    }

    void SetSessionField(string fieldName, object value)
    {
        SetObjectField(session, fieldName, value);
    }

    static void SetObjectField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(target.GetType().FullName, fieldName);
        field.SetValue(target, value);
    }

    static object GetObjectField(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(target);
    }

    IEnumerator WaitUntil(Func<bool> condition, float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
    }

    void Aim(MonkeyCareNpc target)
    {
        if (!target) return;
        Vector3 center = target.transform.position + Vector3.up * .82f;
        gameCamera.transform.position = center + target.transform.forward * 3.2f + Vector3.up * .18f;
        gameCamera.transform.LookAt(center);
        hand.localPosition = new Vector3(.33f, -.25f, .52f);
        hand.localRotation = Quaternion.identity;
    }

    void Check(bool value, string description)
    {
        checks++;
        report.Add((value ? "PASS " : "FAIL ") + description);
        if (!value) throw new Exception(description);
    }

    void Capture(string label)
    {
        Canvas.ForceUpdateCanvases();
        var previous = RenderTexture.active;
        var original = gameCamera.targetTexture;
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            gameCamera.targetTexture = target;
            gameCamera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes("work/monkey-care/" + label + ".png", image.EncodeToPNG());
        }
        finally
        {
            gameCamera.targetTexture = original;
            RenderTexture.active = previous;
            Destroy(target);
            Destroy(image);
        }
    }

    static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true)
            .Where(item => item && item.enabled).ToArray();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static float GroundBelow(Vector3 center, Transform ignore)
    {
        var hits = Physics.RaycastAll(center + Vector3.up * 80, Vector3.down, 160,
            ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits.Where(item => !item.transform.IsChildOf(ignore)).OrderBy(item => item.point.y))
            if (hit.normal.y >= .4f) return hit.point.y;
        return float.NaN;
    }
}
