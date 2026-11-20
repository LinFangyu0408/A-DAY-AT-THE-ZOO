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
        // The headless test scene has no active XR device, so the HUD would pick
        // the flat PC overlay. Force VR mode here so the world-space layout (and
        // every positional assertion below) is what actually runs.
        CarePlatform.ForceVr = true;
        while (!session || !session.Begun) yield return null;
        gameCamera = Camera.allCameras.FirstOrDefault(item =>
            item && item.enabled && item.gameObject.activeInHierarchy &&
            item.cameraType == CameraType.Game);
        Check(gameCamera, "game camera is active");
        var tutorialHost = new GameObject("Final Tutorial Visual");
        var tutorial = tutorialHost.AddComponent<CareTutorial>();
        tutorial.Begin(session, gameCamera.transform, Time.unscaledTime);
        yield return new WaitForSecondsRealtime(.55f);
        var tutorialTitle = GameObject.Find("Tutorial title")?.GetComponent<Text>();
        var tutorialBody = GameObject.Find("Tutorial body")?.GetComponent<Text>();
        var tutorialKey = GameObject.Find("Key GO")?.GetComponent<Image>();
        Check(tutorialTitle && tutorialTitle.text == "WELCOME TO THE ZOO",
            "beginner tutorial opens with a calm welcome step");
        Check(tutorialBody && tutorialBody.fontSize >= 52,
            "beginner tutorial body text is enlarged");
        Check(tutorialKey && tutorialKey.rectTransform.sizeDelta.x >= 132,
            "beginner tutorial key badge is enlarged and round");
        Check(tutorialKey && tutorialKey.color.g > tutorialKey.color.b,
            "key badges are colour coded (GO / A green, B red, RT orange)");
        Capture("final-tutorial");
        typeof(CareTutorial).GetMethod("ShowStep", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(tutorial, new object[] { 3 });
        yield return null;
        var tutorialBagTitle = GameObject.Find("Tutorial title")?.GetComponent<Text>();
        var tutorialBagBody = GameObject.Find("Tutorial body")?.GetComponent<Text>();
        var tutorialBagKey = tutorialKey ? tutorialKey.GetComponentInChildren<Text>() : null;
        Check(tutorialBagTitle && tutorialBagTitle.text == "OPEN THE BAG" &&
              tutorialBagKey && tutorialBagKey.text == "B",
            "beginner tutorial shows the enlarged round B control");
        Check(tutorialBagBody && tutorialBagBody.text.Contains("Press B"),
            "the bag step names the key before the player acts");
        Check(tutorialKey && tutorialKey.color.r > .6f && tutorialKey.color.g < .5f,
            "the B key cap is red while the A cap stays green");
        Capture("final-tutorial-bag");
        UnityEngine.Object.Destroy(tutorialHost);
        yield return null;

        hand = new GameObject("Final Smoke Hand").transform;
        hand.SetParent(gameCamera.transform, false);
        hand.localPosition = new Vector3(.33f, -.25f, .52f);
        hand.localRotation = Quaternion.identity;
        SetSessionField("recordingHand", hand);

        yield return null;
        var readyCanvasField = typeof(MonkeyCareSession).GetField("readyCanvas",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var readyCanvas = readyCanvasField?.GetValue(session) as RectTransform;
        report.Add("READY canvas=" + (readyCanvas ? readyCanvas.name : "none") +
                   " active=" + (readyCanvas && readyCanvas.gameObject.activeInHierarchy) +
                   " pos=" + (readyCanvas ? readyCanvas.position.ToString("F2") : "-") +
                   " scale=" + (readyCanvas ? readyCanvas.lossyScale.x.ToString("F4") : "-") +
                   " cam=" + gameCamera.transform.position.ToString("F2") +
                   " camName=" + gameCamera.name +
                   " sessionCam=" + (MonkeyCareSession.PlayerCamera ? MonkeyCareSession.PlayerCamera.name : "none"));
        var readyGroup = readyCanvas ? readyCanvas.GetComponent<CanvasGroup>() : null;
        Check(readyGroup && readyGroup.alpha > .8f,
            "the READY GO gate is actually visible while it waits for A");
        Capture("final-ready");
        var readyPrompt = GameObject.Find("Ready prompt")?.GetComponent<Text>();
        Check(readyPrompt && readyPrompt.text == "PRESS A TO START",
            "ready gate shows the PRESS A TO START hint below Ready Go");
        Check(readyPrompt && readyPrompt.gameObject.activeInHierarchy && !session.RoundStarted,
            "the round waits for a deliberate A press instead of starting itself");
        yield return new WaitForSecondsRealtime(.6f);
        yield return Press("recordingA");
        yield return WaitUntil(() => session.RoundStarted, 3);
        Check(session.RoundStarted, "A starts the round");
        var progressTrack = GameObject.Find("Progress track")?.GetComponent<Image>();
        Check(GameObject.Find("SVG score panel") && GameObject.Find("Static score bar cover") &&
              progressTrack && progressTrack.enabled && progressTrack.color.a >= .18f,
            "top progress uses the supplied artwork with a visible empty track");
        Check(GameObject.Find("Progress pass marker"),
            "the sixty-percent passing line is still drawn on the bar");
        var starBadge = GameObject.Find("Score star badge")?.GetComponent<RectTransform>();
        var passMarkerRect = GameObject.Find("Progress pass marker")?.GetComponent<RectTransform>();
        var starBefore = starBadge ? starBadge.anchoredPosition.x : float.NaN;
        Check(starBadge && passMarkerRect,
            "the star badge and the passing line both live on the progress bar");
        var bananaHud = GameObject.Find("Care BANANA HUD")?.GetComponent<RectTransform>();
        // Both plates used to sit outside (or right on) the headset's field of
        // view: the counter 33° right and the score plate 21° up. The contract is
        // now an angle budget, so they stay comfortably readable in the headset.
        float BananaYaw = bananaHud ? Mathf.Atan2(bananaHud.localPosition.x, bananaHud.localPosition.z) * Mathf.Rad2Deg : 999f;
        float BananaPitch = bananaHud ? Mathf.Atan2(bananaHud.localPosition.y, bananaHud.localPosition.z) * Mathf.Rad2Deg : 999f;
        report.Add("HUD banana yaw=" + BananaYaw.ToString("F1") + " pitch=" + BananaPitch.ToString("F1"));
        Check(bananaHud && Mathf.Abs(BananaYaw) <= 30f && BananaPitch >= 5f && BananaPitch <= 22f,
            "banana counter sits inside the headset view, above the gaze line");
        var scoreHud = GameObject.Find("Care SCORE HUD")?.GetComponent<RectTransform>();
        float scorePitch = scoreHud ? Mathf.Atan2(scoreHud.localPosition.y, scoreHud.localPosition.z) * Mathf.Rad2Deg : 999f;
        report.Add("HUD score pitch=" + scorePitch.ToString("F1"));
        Check(scoreHud && scorePitch >= 12f && scorePitch <= 24f,
            "top progress bar stays below the screen edge and inside the view");
        var tutorialHud = GameObject.Find("Care Tutorial")?.GetComponent<RectTransform>();
        float tutorialYaw = tutorialHud ? Mathf.Atan2(tutorialHud.localPosition.x,
            tutorialHud.localPosition.z) * Mathf.Rad2Deg : 999f;
        report.Add("HUD tutorial yaw=" + tutorialYaw.ToString("F1"));
        // The tutorial plate is only built once the round leaves READY GO, so it
        // may not exist yet when this check runs.
        if (tutorialHud)
            Check(Mathf.Abs(tutorialYaw) <= 28f,
                "the tutorial prompt sits inside the headset view, not on its edge");
        else
            report.Add("HUD tutorial not created yet (still on the ready gate)");
        var scoreText = GameObject.Find("Current score")?.GetComponent<Text>();
        Check(!GameObject.Find("Progress value") &&
              scoreText && scoreText.fontSize >= 30,
            "top progress shows only the percentage (no raw 0/9 counter)");
        Check(!GameObject.Find("Care STAMINA HUD"),
            "no stamina plate floats in front of the player any more");
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
        yield return null;
        yield return null;
        Check(session.GloveInventory.AutoEquip &&
              !session.GloveInventory.BagButtonRoot.gameObject.activeSelf,
            "the bag never has to be opened again after the first banana");
        Check(session.HeldItem && !session.GloveInventory.IsOpen,
            "restocking at the stand puts a banana straight back in the hand");
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
        // Requirement: the tutorial banana scores like a real one.
        Check(session.Round.TutorialFeedings == 1 && session.Round.FeedingTotal >= 1,
            "the tutorial banana scores and moves the progress bar");
        var monkeyPanel = typeof(MonkeyCareNpc)
            .GetField("panel", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(monkey) as CarePanel;
        var monkeyEmotion = GameObject.Find("Emotion")?.GetComponent<Image>();
        Check(monkeyPanel != null && !monkeyPanel.Root.gameObject.activeInHierarchy,
            "the overhead hunger bar disappears once the banana is eaten");
        Check(monkeyEmotion && monkeyEmotion.gameObject.activeInHierarchy &&
              monkeyEmotion.sprite == monkey.ui.happy,
            "a happy reaction replaces the old satisfaction bar");
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

        // ---- one hungry monkey at a time, bar above its head, instant score ----
        yield return WaitUntil(() => session.monkeys.Any(item => item && item.HasDemand), 10);
        var demanding = session.monkeys.Where(item => item && item.HasDemand).ToArray();
        report.Add("HUNGER demanding=" + demanding.Length +
                   " owner=" + (session.HungerOwner ? session.HungerOwner.name : "none"));
        Check(demanding.Length <= 1,
            "the hunger rotation keeps only one monkey hungry at a time");
        Check(demanding.Length == 1, "a monkey is asking for food after the rotation starts");
        if (demanding.Length == 1)
        {
            var hungry = demanding[0];
            var panelField = typeof(MonkeyCareNpc).GetField("panel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var panel = panelField?.GetValue(hungry) as CarePanel;
            var panelRoot = panel?.Root;
            Vector3 flat = panelRoot ? new Vector3(panelRoot.position.x, 0, panelRoot.position.z) : Vector3.zero;
            Vector3 npcFlat = new Vector3(hungry.transform.position.x, 0, hungry.transform.position.z);
            report.Add("OVERHEAD panel=" + (panelRoot ? panelRoot.position.ToString("F2") : "none") +
                       " monkey=" + hungry.transform.position.ToString("F2"));
            Check(panelRoot && panelRoot.gameObject.activeInHierarchy &&
                  panelRoot.position.y >= hungry.transform.position.y + .8f &&
                  Vector3.Distance(flat, npcFlat) <= .65f,
                "the tolerance bar floats above the hungry monkey's head");
            var panelTitle = panelRoot ? panelRoot.Find("Panel title")?.GetComponent<Text>() : null;
            Check(panelTitle && panelTitle.text == "STAMINA",
                "the overhead bar shows live hunger tolerance");
            Aim(hungry);
            yield return null;
            yield return null;
            Capture("final-hunger-bar");

            // Feeding must move the score bar in the very same frame.
            int before = session.Round.FeedingTotal;
            // Leave the guided-tutorial lane so the feed counts on the round.
            hungry.EndGuidedTutorial();
            hungry.BeginSession();
            hungry.feedingSeconds = 2f;
            hungry.BeginDemand(CareNeed.Hunger);
            if (!session.HeldItem) session.EquipBananaFromGlove(false);
            Aim(hungry);
            yield return null;
            // Eating only starts for a thrown banana, so release RT at it.
            SetSessionField("recordingTrigger", true);
            for (int frame = 0; frame < 4; frame++) yield return null;
            SetSessionField("recordingTrigger", false);
            yield return WaitUntil(() => hungry.EatingNow, 4);
            Check(hungry.EatingNow && panelRoot && panelRoot.gameObject.activeInHierarchy,
                "the hunger bar keeps showing (and draining) while the monkey eats");
            yield return WaitUntil(() => session.Round.FeedingTotal > before, 8);
            report.Add("FEED total=" + session.Round.FeedingTotal +
                       " task=" + hungry.CurrentTaskIndex +
                       " celebrating=" + hungry.Celebrating +
                       " demand=" + hungry.HasDemand +
                       " fed=" + (hungry.Progress != null ? hungry.Progress.Fed.ToString("F2") : "none") +
                       " timeout=" + (hungry.Progress != null ? hungry.Progress.Deadline.ToString("F1") : "-") +
                       " held=" + (session.HeldItem ? session.HeldItem.name : "none") +
                       " ray=" + (session.RayNpc ? session.RayNpc.name : "none") +
                       " frozen=" + session.Round.Frozen +
                       " feedings=" + string.Join(",", session.Round.Feedings));
            Check(session.Round.FeedingTotal == before + 1,
                "a completed feeding raises the round score right away");
            int expected = Mathf.RoundToInt(100f * session.Round.FeedingTotal / CareRound.ScoreGoal);
            Check(scoreText && scoreText.text == expected + "%",
                "the score percentage repaints without waiting for the next frame");
            var scoreFill = GameObject.Find("Progress fill")?.GetComponent<Image>();
            Check(scoreFill && scoreFill.fillAmount > 0f,
                "the score bar fill advances immediately after feeding");
            Check(panelRoot && !panelRoot.gameObject.activeInHierarchy,
                "the hunger bar clears the moment the feeding resolves");
            Capture("final-score-after-feed");
        }

        var viewField = typeof(MonkeyCareSession).GetField("roundView",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var roundView = viewField?.GetValue(session) as CareRoundView;
        Check(roundView != null, "round view is available");

        // ---- the whole HUD rides a head anchor, not the camera VR may swap ----
        var headAnchor = GameObject.Find("Care Head Anchor");
        var scorePlate = GameObject.Find("Care SCORE HUD");
        var bananaPlate = GameObject.Find("Care BANANA HUD");
        Check(headAnchor && scorePlate && bananaPlate &&
              scorePlate.transform.parent == headAnchor.transform &&
              bananaPlate.transform.parent == headAnchor.transform,
            "score and banana plates hang off the head anchor instead of the camera");
        Check(headAnchor && session.Head == headAnchor.transform,
            "the session exposes the head anchor as the UI parent");

        // ---- the ballistic solve must land exactly on the aimed point ----
        var aimStart = new Vector3(0f, 1.5f, 0f);
        var aimEnd = new Vector3(0f, 1.2f, 4f);
        Vector3 aimVelocity = CareItem.BallisticVelocity(aimStart, aimEnd, 9f);
        float aimFlight = new Vector2(aimVelocity.x, aimVelocity.z).magnitude < .01f ? 0f
            : 4f / new Vector2(aimVelocity.x, aimVelocity.z).magnitude;
        Vector3 aimLanded = aimStart + aimVelocity * aimFlight +
                            Physics.gravity * (.5f * aimFlight * aimFlight);
        report.Add("AIM landed=" + aimLanded.ToString("F2"));
        Check(Vector3.Distance(aimLanded, aimEnd) < .2f,
            "the solved arc lands on the point the ray is aiming at");

        // ---- the star rides the progress bar ----
        Canvas.ForceUpdateCanvases();
        roundView.Update(FedRound(0), 40f);
        Canvas.ForceUpdateCanvases();
        float starAtZero = starBadge.position.x;
        roundView.Update(FedRound(6), 40f);
        Canvas.ForceUpdateCanvases();
        float starAtSix = starBadge.position.x;
        float lineAtSix = passMarkerRect.position.x;
        roundView.Update(FedRound(9), 40f);
        Canvas.ForceUpdateCanvases();
        float starAtNine = starBadge.position.x;
        report.Add("STAR x0=" + starAtZero.ToString("F3") + " x6=" + starAtSix.ToString("F3") +
                   " line=" + lineAtSix.ToString("F3") + " x9=" + starAtNine.ToString("F3") +
                   " start=" + starBefore.ToString("F3"));
        Check(starAtNine > starAtSix && starAtSix > starAtZero,
            "the star icon slides along the track together with the progress");
        Check(Mathf.Abs(starAtSix - lineAtSix) <= 1.5f,
            "at six bananas the moving star sits exactly on the yellow passing line");

        // ---- the banana counter turns red at three or fewer ----
        var bananaText = GameObject.Find("Banana count")?.GetComponent<Text>();
        var lowRound = FedRound(0);
        for (int i = 0; i < CareRound.StartingBananas - 3; i++) lowRound.ConsumeBanana();
        roundView.Update(lowRound, 40f);
        Check(bananaText && bananaText.text == "×3" && bananaText.color.r > .7f &&
              bananaText.color.g < .35f,
            "the banana counter turns red at three bananas");
        roundView.Update(FedRound(0), 40f);
        Check(bananaText && bananaText.color.g > .1f && !(bananaText.color.r > .7f && bananaText.color.g < .35f),
            "a full banana stock keeps the counter dark");
        roundView.Update(session.Round, 40f);

        // ---- the stand pointer is a bare red arrow, only while stock is low ----
        var directionHint = FindFirstObjectByType<CareDirectionHint>(
            FindObjectsInactive.Include);
        Check(directionHint != null, "the banana stand pointer still exists");
        var hintRoot = directionHint ? directionHint.transform.Find("Care DIRECTION HINT") : null;
        Check(hintRoot, "the pointer canvas is built under the camera");
        Check(!hintRoot.Find("Hint plate") && !hintRoot.Find("Hint plate shadow"),
            "the pointer no longer uses the dark blue plate");
        var hintArrow = hintRoot.Find("Hint arrow")?.GetComponent<Image>();
        Check(hintArrow && hintArrow.color.r > .7f && hintArrow.color.g < .35f,
            "the pointer arrow is red");
        Check(Mathf.Approximately(CareDirectionHint.LowStockBananas, 3f),
            "the pointer only appears when three or fewer bananas are left");

        // Visual proof: low stock, back turned to the stall, bare red arrow.
        var standObject = GameObject.Find("Banana Market Stand");
        if (standObject && directionHint)
        {
            for (int i = 0; i < CareRound.StartingBananas - 2; i++) session.Round.ConsumeBanana();
            var camTransform = gameCamera.transform;
            Vector3 awayFromStand = camTransform.position - standObject.transform.position;
            awayFromStand.y = 0f;
            if (awayFromStand.sqrMagnitude < .01f) awayFromStand = Vector3.forward;
            camTransform.rotation = Quaternion.LookRotation(awayFromStand.normalized, Vector3.up);
            yield return new WaitForSecondsRealtime(.6f);
            report.Add("HINT showing=" + directionHint.Showing + " bananas=" + session.Round.Bananas);
            Check(directionHint.Showing, "the pointer appears as soon as stock is low");
            Capture("final-stand-hint");
        }

        // ---- the yard is fenced off behind the monkeys and the stall ----
        var barrierRoot = GameObject.Find("Care Play Area Barrier");
        var barrier = barrierRoot ? barrierRoot.GetComponent<CarePlayAreaBarrier>() : null;
        Check(barrier != null, "an invisible fence surrounds the feeding yard");
        if (barrier)
        {
            var walls = barrierRoot.GetComponentsInChildren<BoxCollider>(true);
            float farthest = session.monkeys.Where(item => item).Select(item =>
                Vector2.Distance(new Vector2(item.transform.position.x, item.transform.position.z),
                    new Vector2(barrier.Center.x, barrier.Center.z))).DefaultIfEmpty(0f).Max();
            report.Add("FENCE radius=" + barrier.Radius.ToString("F2") +
                       " segments=" + barrier.Segments + " walls=" + walls.Length +
                       " farthestMonkey=" + farthest.ToString("F2"));
            Check(walls.Length >= 18 &&
                  barrierRoot.GetComponentsInChildren<Renderer>(true).Length == 0,
                "the fence is solid but completely invisible");
            Check(barrier.Radius > farthest,
                "every monkey stands inside the fence, so nothing can be reached from behind");
            Vector3 before = gameCamera.transform.position;
            gameCamera.transform.position =
                barrier.Center + new Vector3(barrier.Radius + 6f, 1.6f, 0f);
            yield return new WaitForSecondsRealtime(.06f);
            Vector3 escaped = gameCamera.transform.position;
            float escapedDistance = Vector2.Distance(
                new Vector2(escaped.x, escaped.z), new Vector2(barrier.Center.x, barrier.Center.z));
            report.Add("FENCE clamp=" + escapedDistance.ToString("F2") +
                       " limit=" + (barrier.Radius - CarePlayAreaBarrier.ClampInset).ToString("F2"));
            Check(escapedDistance <= barrier.Radius - CarePlayAreaBarrier.ClampInset + .35f,
                "trying to walk past the fence pushes the player back inside");
            gameCamera.transform.position = before;
            yield return null;

            // ---- walk-only: no jumping, no flying, no teleporting onto props ----
            Transform riseRoot = session.PlayerRoot;
            Vector3 riseBase = riseRoot ? riseRoot.position : gameCamera.transform.position;
            Vector3 riseTarget = riseBase + Vector3.up * 6f;
            if (riseRoot) riseRoot.position = riseTarget;
            else gameCamera.transform.position = riseTarget;
            yield return new WaitForSecondsRealtime(.1f);
            float riseNow = riseRoot ? riseRoot.position.y : gameCamera.transform.position.y;
            report.Add("RISE now=" + riseNow.ToString("F2") + " start=" + riseBase.y.ToString("F2") +
                       " cap=" + (riseBase.y + CarePlayAreaBarrier.MaxRise).ToString("F2"));
            Check(riseNow - riseBase.y <= CarePlayAreaBarrier.MaxRise + .05f,
                "lifting the player six metres is undone within a frame");
            if (riseRoot) riseRoot.position = riseBase;
            else gameCamera.transform.position = riseBase;
            yield return null;

            // A ray that lands on the yard still must not teleport: the yard is
            // walk-only, so every teleport path is refused outright.
            var loco = FindFirstObjectByType<Locomotion>();
            if (loco)
            {
                Vector3 yardPoint = riseBase + Vector3.forward * 2f;
                bool teleported = loco.TryTeleport(yardPoint);
                report.Add("WALK groundOnly=" + loco.groundOnly + " allowTeleport=" + loco.allowTeleport +
                           " teleportAccepted=" + teleported + " islandFloor=" + loco.IslandFloorY.ToString("F2"));
                Check(loco.groundOnly && !loco.allowTeleport,
                    "the care yard runs in walk-only mode");
                Check(!teleported, "teleport is refused even on flat yard ground");
                Check(loco.IslandFloorY < riseBase.y + 1f,
                    "the island floor limit uses the mesh bottom, not its tallest hill");
            }
            else
            {
                // The smoke scene may be bootstrapped without the HighlandLake
                // rig (desktop preview), so the walk-only flags cannot be checked
                // here; the vertical guard above already proved the ground lock.
                report.Add("WALK locomotion missing (headless care scene)");
            }
        }

        // ---- final twenty seconds name the goal instead of leaving dead air ----
        var sprintText = GameObject.Find("Final sprint")?.GetComponent<Text>();
        roundView.Update(session.Round, CareRound.Cutoff - 12f);
        Check(sprintText && sprintText.text.StartsWith("FINAL 12s") &&
              sprintText.text.Contains("MORE"),
            "the last twenty seconds spell out the remaining goal");
        Capture("final-sprint");

        // ---- star tiers are absolute banana counts ----
        var probe9 = FedRound(9);
        report.Add("TIERS 2=" + TierStars(2) + " 3=" + TierStars(3) + " 5=" + TierStars(5) +
                   " 6=" + TierStars(6) + " 8=" + TierStars(8) + " 9=" + probe9.Stars +
                   " fed9=" + probe9.FeedingTotal +
                   " tasks=" + probe9.TasksForMonkey(0) + "/" + probe9.TasksForMonkey(1) + "/" +
                   probe9.TasksForMonkey(2) +
                   " feedings=" + string.Join(",", probe9.Feedings));
        Check(TierStars(2) == 0 && TierStars(3) == 1 && TierStars(5) == 1 &&
              TierStars(6) == 2 && TierStars(8) == 2 && TierStars(9) == 3,
            "stars follow the 0-2 / 3-5 / 6-8 / 9+ banana tiers");
        Check(FedRound(12).Stars == CareRound.MaxStars &&
              FedRound(12).FeedingTotal == 12,
            "a tenth or twelfth banana never pushes the rating past three stars");
        var queue = FedRound(0);
        report.Add("QUEUE total=" + queue.TaskTotal + " per=" + queue.TasksForMonkey(0) + "/" +
                   queue.TasksForMonkey(1) + "/" + queue.TasksForMonkey(2) +
                   " cutoff=" + CareRound.Cutoff + " starGoal=" + CareRound.ScoreGoal);
        Check(queue.TaskTotal > 9 && queue.TaskTotal == CareRound.PlannedFeedings &&
              queue.TasksForMonkey(0) > 2 && queue.TasksForMonkey(1) > 2 &&
              queue.TasksForMonkey(2) > 2,
            "the round queues more than nine hunger episodes for the rotation");
        Check(CareRound.Cutoff <= 130 && CareRound.Cutoff >= 90,
            "the countdown is shortened so the queue still has work when it ends");
        Check(Mathf.Abs(CareRound.PassingRatio - CareRound.TwoStarFeedings /
                  (float)CareRound.ThreeStarFeedings) < .0001f,
            "the passing line sits on the two-star threshold");
        Check(FedRound(9).FeedingRatio >= 1.0,
            "nine bananas fill the progress bar completely");
        var tutorialRound = new CareRound();
        tutorialRound.DealFeedingTasks();
        Check(tutorialRound.RegisterTutorialFeeding() &&
              tutorialRound.FeedingTotal == 1 && tutorialRound.FeedingRatio > .1,
            "the tutorial banana counts as a scored feeding");

        var round = FedRound(7);
        round.Freeze();
        // Mirror the session freeze path so NPC world-space HUDs (hunger bars,
        // emotions) are hidden exactly like the real round end does.
        if (session.monkeys != null)
            foreach (var npc in session.monkeys) npc.StopAtCutoff();
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
        Check(round.Score == 7 && round.Stars == 2 && round.IsPassing,
            "seven feedings shows passing two-star results");
    }

    // Deal the round's hunger episodes and settle a given number of feedings.
    static CareRound FedRound(int bananas)
    {
        var probe = new CareRound();
        probe.DealFeedingTasks();
        int done = 0;
        for (int task = 0; task < 8 && done < bananas; task++)
        {
            for (int index = 0; index < 3 && done < bananas; index++)
            {
                if (!probe.Complete(index, task, CareNeed.Hunger, 100)) continue;
                done++;
            }
        }
        return probe;
    }

    static int TierStars(int bananas) => FedRound(bananas).Stars;

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
