using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace ADayAtTheZoo.Monkeys.Care
{
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed class MonkeyCareSession : MonoBehaviour
    {
        [Header("场景绑定")]
        public OVRCameraRig rig;
        public Locomotion locomotion;
        public Transform trackingSpace;
        public MonkeyCareNpc[] monkeys;
        public CareItem[] items;
        [Header("开场")]
        public Sprite readyGo;
        [InspectorName("开场显示时间（秒）"), Min(.1f)] public float readySeconds = 3;
        public CareEffect readyEffect = new() { duration = 2, position = new Vector3(0, -.35f, 1.8f), scale = Vector3.one * .12f };
        [Header("当前玩法")]
        [InspectorName("只喂香蕉")] public bool feedingOnly = true;
        [InspectorName("显示新手导览")] public bool showTutorial = true;
        [InspectorName("新手练习猴子")] public int tutorialMonkeyIndex = 2;
        [InspectorName("空手投掷速度（米/秒）"), Min(.1f)] public float throwSpeed = 9;
        [Header("射线与反馈")]
        [InspectorName("射线距离（米）"), Min(1)] public float rayDistance = 30;
        [InspectorName("直线宽度（米）"), Min(.001f)] public float lineWidth = .006f;
        [InspectorName("震动强度"), Range(0, 1)] public float hapticAmplitude = .62f;
        [InspectorName("震动频率"), Range(0, 1)] public float hapticFrequency = .55f;
        [InspectorName("桌面调试输入")] public bool desktopPreview = true;
        public Material pointerMaterial;
        [Header("游戏音频")]
        public CareAudio audioFeedback = new();
        [Header("计分文档 v1.0 / 回合UI")]
        public CareRoundArtwork roundArtwork = new();
        [HideInInspector] public int rulesVersion;
        [HideInInspector] public bool testAllowUnfocused;
        public static MonkeyCareSession Instance { get; private set; }
        public static bool Playing =>
            Instance && Instance.begun &&
#if UNITY_EDITOR
            // Clicking Console, Hierarchy or Inspector must not hide the game UI
            // during desktop/controller preview.
            (Instance.focused || Instance.testAllowUnfocused || Application.isEditor) &&
#else
            (Instance.focused || Instance.testAllowUnfocused) &&
#endif
            Time.timeScale > 0;
        public static Camera PlayerCamera => Instance ? Instance.playerCamera : null;
        // Every world-space plate hangs off this transform. In VR the camera the
        // session grabbed during Start() can be the desktop stand-in, which the
        // headset rig disables a few frames later — parenting the HUD to it made
        // the whole interface vanish inside the headset. The anchor always rides
        // the real eye, so the UI follows the player instead of a dead camera.
        public Transform Head => uiHead ? uiHead.transform
            : playerCamera ? playerCamera.transform : transform;
        Transform HeadSource() =>
            rig && rig.gameObject.activeInHierarchy && rig.centerEyeAnchor
                ? rig.centerEyeAnchor
                : playerCamera ? playerCamera.transform : transform;
        // Root that actually carries the player: the VR rig in headset play, the
        // character controller on desktop. Used by the invisible play-area fence.
        public Transform PlayerRoot =>
            rig && rig.gameObject.activeInHierarchy ? rig.transform :
            desktopController ? desktopController.transform :
            playerCamera ? playerCamera.transform : null;
        // Below this many seconds left the HUD switches to "final sprint" mode.
        public const float FinalSprintSeconds = 20;
        // The continuous session clock. Animations, bite progress and every
        // "wait N seconds from now" timestamp run on it, so nothing can stall
        // while a rule is being changed elsewhere.
        public static float GameTime => Instance ? Instance.gameTime : Time.time;
        // The round clock: the two minute countdown, the 120 s music cue, the
        // star goals and the sprint banner all count this one. The guided
        // lesson is part of the round, exactly like a real service counter - a
        // player who spends half a minute learning still spent half a minute -
        // so this is the session clock capped at the cutoff, nothing else.
        public float RoundElapsed => Mathf.Min(gameTime, CareRound.Cutoff);
        // Every "wait N seconds from now" timestamp reads the same clock the
        // countdown does. The lesson used to get its own domain here; that is
        // what let the round clock sit at zero while the lesson's banana was
        // being eaten.
        public static float TaskTime => !Instance
            ? Mathf.Min(Time.time, CareRound.Cutoff)
            : Instance.RoundElapsed;
        public CareRound Round { get; private set; }
        public bool UsingVrHands =>
            rig && rig.gameObject.activeInHierarchy &&
            rig.leftControllerAnchor && rig.rightControllerAnchor;
        public Transform RightHand
        {
            get
            {
#if UNITY_EDITOR
                if (recordingHand) return recordingHand;
#endif
                // VrPlatformSwitcher only enables the VR rig when the headset is
                // active. Once active, follow Meta's controller anchor directly:
                // input-device detection can lag behind controller tracking.
                return UsingVrHands
                    ? rig.rightControllerAnchor : DesktopRightHand();
            }
        }
        public Transform LeftHand
        {
            get
            {
                return UsingVrHands
                    ? rig.leftControllerAnchor : DesktopLeftHand();
            }
        }
#if UNITY_EDITOR
        internal Transform recordingHand;
        internal bool recordingTrigger, recordingGrip, recordingA, recordingB;
        bool previousRecordingTrigger, previousRecordingA, previousRecordingB;
#endif
        public MonkeyCareNpc RayNpc { get; private set; }
        public CareItem HeldItem => held;
        public bool Begun => begun;
        public bool TutorialActive { get; private set; }
        public MonkeyCareNpc TutorialMonkey =>
            monkeys != null && monkeys.Length > 0
                ? monkeys[Mathf.Clamp(tutorialMonkeyIndex, 0, monkeys.Length - 1)]
                : null;
        public bool TutorialHungerSatisfied => TutorialMonkey && TutorialMonkey.TutorialSatisfied;
        public bool RoundStarted { get; private set; }
        public CareInputSnapshot Input { get; private set; }
        public CareGloveInventory GloveInventory { get; private set; }
        public CareHandVisuals Hands { get; private set; }
        Camera playerCamera;
        DesktopPlayerController desktopController;
        Transform desktopRightHand, desktopLeftHand;
        CareItem held;
        CareTeleportPoint highlighted;
        LineRenderer pointer, arc;
        RectTransform readyCanvas;
        RectTransform pcHud;
        CarePadStyleToggle padToggle;
        Text readyPrompt;
        Text readyKeyLabel;
        GameObject readyFx;
        CareRoundView roundView;
        CareDirectionHint standHint;
        CareHeadAnchor uiHead;
        CareFarmEnvironment farmEnvironment;
        CarePlayAreaBarrier playArea;
        CareEyeHeight eyeHeight;
        CarePickupMarker bananaPickup;
        readonly List<CareTaskRecord> records = new();
        Vector3 rigStartPosition;
        Quaternion rigStartRotation;
        bool begun, focused = true, arcAiming, arcValid, suppressUntilRelease;
        bool tutorialCompleted;
        bool readyGateActive, goSequenceActive;
        float goUntil, readyArmedAt;
        bool hapticControllerLogged;
        Vector3 lastThrowVelocity;
        bool hasThrowSolution;
        Vector3 landing;
        float gameTime;
        float nextBananaEquipAt;
        float nextCameraProbe;
        float nextHandRayCheck;
        readonly Vector3[] arcPoints = new Vector3[40];

        IEnumerator Start()
        {
            Instance = this;
            while (!playerCamera)
            {
                playerCamera = Camera.allCameras.FirstOrDefault(c => c.enabled && c.gameObject.activeInHierarchy && c.cameraType == CameraType.Game);
                yield return null;
            }
            // A frame after the game camera becomes available also works with Scene view focused.
            yield return null;
            if (!trackingSpace) trackingSpace = rig ? rig.trackingSpace : transform;
            desktopController = Object.FindFirstObjectByType<DesktopPlayerController>(FindObjectsInactive.Include);
            DesktopRightHand();
            DesktopLeftHand();
            Hands = gameObject.AddComponent<CareHandVisuals>();
            pointer = MakeLine("Care Straight Ray"); arc = MakeLine("Care Teleport Arc");
            if (locomotion) locomotion.externalRightHandControl = true;
            // Walk-only mode: the care yard is played on foot. Teleporting onto
            // the animals, the stall roof or a rock was the other half of the
            // "I am floating in the air" report.
            if (locomotion) { locomotion.groundOnly = true; locomotion.allowTeleport = false; }
            begun = true;
            Round = new CareRound(MonkeyStartingNeeds(), feedingOnly);
            Round.DealFeedingTasks();
            // The HUD anchor must exist before any artwork is parented to it.
            var headObject = new GameObject("Care Head Anchor");
            var headAnchor = headObject.AddComponent<CareHeadAnchor>();
            headAnchor.Follow(HeadSource());
            uiHead = headAnchor;
            rigStartPosition = rig.transform.position; rigStartRotation = rig.transform.rotation;
            SetupFeedingOnlyScene();
            farmEnvironment = CareFarmEnvironment.Create(playerCamera);
            bananaPickup = farmEnvironment ? farmEnvironment.PickupPoint : null;
            if (!bananaPickup) bananaPickup = FindFirstObjectByType<CarePickupMarker>(FindObjectsInactive.Include);
            // Fence the yard off behind the monkeys and behind the stall so the
            // player cannot wander past the animals and lose the scene.
            playArea = CarePlayAreaBarrier.Create(this, playerCamera,
                farmEnvironment && farmEnvironment.Stand
                    ? farmEnvironment.Stand
                    : bananaPickup ? bananaPickup.transform : null);
            // The rig runs on a device-level origin, so the eye height has to be
            // placed explicitly or the player ends up sitting on the grass.
            eyeHeight = gameObject.AddComponent<CareEyeHeight>();
            eyeHeight.Begin(rig);
            Hands.Begin(this);
            // Detect the input device before a single badge exists: the READY
            // gate must already say "PRESS ✕" on a PS5 pad instead of waiting
            // for the first input frame (it used to flash A for one frame).
            CareInput.ApplyAutoStyle();
            Debug.Log("[Zoo Platform] 运行模式=" + (CarePlatform.FlatUi ? "PC 平面 HUD" : "VR 世界 HUD") +
                      " 输入=" + CareInput.DescribeDevice() +
                      " 确认/返回=" + CareInput.ConfirmLabel + "/" + CareInput.BackLabel);
            roundView = new CareRoundView(Head, roundArtwork);
            GloveInventory = new CareGloveInventory(this, Head);
            audioFeedback.FollowCamera(playerCamera);
            // The READY prompt names the confirm button; relabel it when the
            // auto-detected pad style settles or the user flips the chip.
            CareInput.LabelsChanged += RefreshReadyPrompt;
            TutorialActive = false;
            RoundStarted = false;
            ShowReadyGate();
            // Opening stutter: synchronously decoding eight wav clips used to
            // land in the single Start frame (the music clip alone decompresses
            // ~40MB of PCM). Show the ready gate first, then stream the audio
            // in from the background loader so the first seconds stay smooth.
            yield return audioFeedback.Preload();
            audioFeedback.StartMusic(transform);
        }
        CareNeed[] MonkeyStartingNeeds()
        {
            if (monkeys == null) return new[] { CareNeed.Hunger, CareNeed.Hunger, CareNeed.Hunger };
            return monkeys.Select(npc => feedingOnly || !npc ? CareNeed.Hunger : npc.firstNeed).ToArray();
        }
        void SetupFeedingOnlyScene()
        {
            if (!feedingOnly) return;
            EnsureBananaSupplyPool();
            Vector3 hidden = transform.position + Vector3.down * 3f;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (!item) continue;
                    if (item.kind == CareNeed.Hunger)
                    {
                        item.throwSpeed = throwSpeed;
                        item.EnterGloveSupply(transform, hidden);
                    }
                    else item.gameObject.SetActive(false);
                }
            }
            HideSupplyToolbox("Sractch");
            roundArtwork.bananaAnchor = Head;
            roundArtwork.bananaOffset = new Vector3(.50f, .26f, 1.12f);
            roundArtwork.bananaWidth = .24f;
        }

        void EnsureBananaSupplyPool()
        {
            if (items == null) return;
            var allItems = new List<CareItem>(items);
            var bananaSources = allItems.Where(item => item && item.kind == CareNeed.Hunger).ToList();
            if (bananaSources.Count == 0) return;
            for (int i = bananaSources.Count; i < CareRound.StartingBananas; i++)
            {
                CareItem source = bananaSources[i % bananaSources.Count];
                var cloneObject = Instantiate(source.gameObject, transform);
                cloneObject.name = source.name + " Glove Supply " + (i + 1);
                var clone = cloneObject.GetComponent<CareItem>();
                if (!clone) continue;
                allItems.Add(clone);
                bananaSources.Add(clone);
            }
            items = allItems.ToArray();
        }
        static void HideSupplyToolbox(string objectName)
        {
            foreach (var root in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (root.name != objectName) continue;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            }
        }
        // Tear the ready gate down completely: the PC layout wraps its plate in
        // a whole ScreenSpaceOverlay root, and leaving that root alive stacked a
        // new empty canvas every restart.
        void DestroyReadyGate()
        {
            if (readyCanvas) { Destroy(readyCanvas.gameObject); readyCanvas = null; }
            if (pcHud) { Destroy(pcHud.gameObject); pcHud = null; }
        }

        // The READY gate names the confirm button, so the copy has to follow
        // the live pad style (✕ on a DualSense, A on Xbox pads/keyboard).
        string ReadyGateCopy() => "PRESS " + CareInput.ConfirmLabel + " TO START";
        // Would confirming the gate lead into the guided lesson? Both the gate
        // (which track to start) and the GO hand-over (whether to park the
        // clock) have to answer this the same way.
        bool TutorialNext => showTutorial && !tutorialCompleted;
        void RefreshReadyPrompt()
        {
            if (readyGateActive && readyPrompt) readyPrompt.text = ReadyGateCopy();
        }

        void ShowReadyGate()
        {
            DestroyReadyGate();
            // Home/standby means the slow bed, not the round track.
            audioFeedback.PauseToAmbient();
            if (CarePlatform.FlatUi)
            {
                pcHud = CareUi.OverlayRoot("Care PC Ready Root", 90);
                readyCanvas = CareUi.Plate("Care READY GO", pcHud,
                    new Vector2(0, 40), new Vector2(660, 520));
            }
            else
                readyCanvas = CareUi.Canvas("Care READY GO", Head,
                    new Vector3(0, 0, 1.45f), .94f, new Vector2(960, 760), 90);
            // The artwork keeps its authored size; on the smaller PC plate it is
            // simply scaled down by the parent so the proportions stay right.
            var readyArt = CareUiSkin.SvgReadyGo ? CareUiSkin.SvgReadyGo : readyGo;
            if (readyArt)
                CareUi.Image("READY GO", readyCanvas, readyArt, new Vector2(660, 385), Vector2.zero);
            else
                CareUiSkin.AddSliced(readyCanvas, "Ready panel", CareUiSkin.Panel,
                    new Vector2(620, 300), Vector2.zero, new Color(1f, .97f, .86f, 1f));
            CareReadyView.Decorate(readyCanvas, roundArtwork.star, 100000f,
                CarePlatform.FlatUi ? .69f : 1f);
            // The hint sits well below the READY GO artwork so it never covers
            // the big letters.
            CareUiSkin.AddSliced(readyCanvas, "Ready prompt plate", CareUiSkin.Slot,
                new Vector2(430, 75), new Vector2(0, -222), new Color(.08f, .25f, .42f, .94f))
                .transform.SetAsLastSibling();
            var readyKey = CareUiSkin.KeyBadge(readyCanvas, CareInput.ConfirmLabel, new Vector2(80, 80),
                new Vector2(-143, -222), out readyKeyLabel, 40);
            readyKey.transform.SetAsLastSibling();
            readyPrompt = CareRoundView.Label("Ready prompt",
                readyCanvas, ReadyGateCopy(),
                new Vector2(290, 50), new Vector2(39, -222), 32,
                TextAnchor.MiddleCenter, Color.white);
            readyPrompt.transform.SetAsLastSibling();
            readyGateActive = true;
            goSequenceActive = false;
            // Starting the round must be a deliberate press: drop any edge the
            // movement stick / keyboard left behind and ignore A for a moment.
            CareInput.ForgetEdges();
            readyArmedAt = Time.unscaledTime + .45f;
            SetPlayerInputLocked(true);
        }
        void AcceptReadyGate()
        {
            if (!readyGateActive) return;
            readyGateActive = false;
            goSequenceActive = true;
            gameTime = 0;
            goUntil = .9f;
            // Confirming the gate starts the round's soundtrack, and it starts
            // the round clock in the same frame - the lesson included. The cue
            // is mapped 1:1 onto the clock, so every tempo lift in the music
            // (58 s, 107 s) lands on the matching second of play. The click
            // lands first so the press feels physical.
            audioFeedback.Click();
            audioFeedback.BeginRoundMusic();
            readyPrompt.text = "";
            readyKeyLabel.text = "GO";
            readyKeyLabel.color = new Color(1f, .72f, .10f, 1f);
            CareInput.ForgetEdges();
        }
        void BeginAfterReadyGo()
        {
            goSequenceActive = false;
            DestroyReadyGate();
            SetPlayerInputLocked(false);
            TutorialActive = TutorialNext;
            // The lesson is the opening minute of the round, not a time-out:
            // the clock and the cheerful track have been running since the A
            // press, so the lesson simply inherits them. It used to swap back
            // to the slow bed and restart the cue on hand-over, which now
            // would knock the two out of step and cost a second intro.
            if (TutorialActive)
                gameObject.AddComponent<CareTutorial>().Begin(this, Head, Time.unscaledTime);
            else
            {
                RoundStarted = true;
                foreach (var npc in monkeys) if (npc) npc.BeginSession();
                BeginHungerRotation(2.5f);
            }
            roundView?.ShowHud();
            GloveInventory?.ShowHud();
            // The stall is behind the opening view, so turn the very first head
            // movement into a "this way" plate instead of leaving players lost.
            if (!standHint && bananaPickup)
                standHint = CareDirectionHint.Create(Head, this, bananaPickup.transform);
        }
        void SetPlayerInputLocked(bool locked)
        {
            if (locomotion) locomotion.inputLocked = locked;
            if (desktopController) desktopController.inputLocked = locked;
        }
        public void FinishTutorial()
        {
            if (RoundStarted) return;
            tutorialCompleted = true;
            TutorialActive = false;
            RoundStarted = true;
            // No clock bookkeeping here: the lesson has been charging the round
            // clock from the A press, and the soundtrack has been running with
            // it. Handing over only opens the hunger rotation - restarting the
            // cue or zeroing the clock would break the 1:1 mapping and give
            // back time the player already spent.
            DestroyReadyGate();
            if (readyFx) Destroy(readyFx);
            GloveInventory?.Close();
            roundView?.ShowHud();
            GloveInventory?.ShowHud();
            foreach (var npc in monkeys)
            {
                if (!npc) continue;
                npc.EndGuidedTutorial();
                npc.BeginSession();
            }
            BeginHungerRotation(2.5f);
        }
        public void BeginTutorialHunger()
        {
            if (!TutorialActive || RoundStarted) return;
            TutorialMonkey?.BeginGuidedTutorial(CareNeed.Hunger);
        }
        // The banana the player feeds during the lesson counts on the round
        // score, so the progress bar moves before the clock even starts.
        public void RegisterTutorialFeeding()
        {
            if (Round == null || Round.Frozen) return;
            if (Round.RegisterTutorialFeeding()) SyncHud();
        }
        public CarePickupMarker BananaPickup => bananaPickup;
        // ---- hunger rotation -------------------------------------------------
        // Exactly one monkey may be hungry at a time. Feeding it hands the
        // request to the next monkey 3-6 seconds later, so three monkeys never
        // stack up and the round always shows a single clear target.
        public MonkeyCareNpc HungerOwner { get; private set; }
        int hungerCursor;
        public bool MayDemand(MonkeyCareNpc npc) => npc && HungerOwner == npc;
        public void BeginHungerRotation(float delay = 2.5f)
        {
            hungerCursor = -1;
            ScheduleNextHunger(delay);
        }
        public void NotifyFed(MonkeyCareNpc fed)
        {
            if (Round == null || Round.Frozen) return;
            if (Round.FeedingTotal >= Round.TaskTotal) { HungerOwner = null; return; }
            // Breathing room after a successful feeding: 3-6 s while the cue is
            // still at 132 BPM, tightening with each musical lift after that.
            // See the pace table below.
            ScheduleNextHunger(RollHungerGap(TaskTime));
        }
        // ---- hunger pace -----------------------------------------------------
        // The queue follows the soundtrack. The 120 s cue is written in four
        // sections, and each one is a step tighter here:
        //
        //   0:00 - 0:58  A / A2  132 BPM  3.0-6.0 s  (the authored pace: the
        //                                              lesson and practice keep
        //                                              exactly what they always
        //                                              had)
        //   0:58 - 1:23  B       156 BPM  2.4-4.8 s
        //   1:23 - 1:47  B2      156 BPM  2.0-4.0 s  (full kit, denser fills)
        //   1:47 - 2:00  T     171.6 BPM  1.6-3.2 s  (the nine-banana pace)
        //
        // Timing, not just numbers: every step is a smoothstep that starts 3 s
        // before the musical downbeat and lands 3 s after it, so the queue
        // breathes up with the band instead of changing gear on its own second.
        // A hard cut on the boundary is exactly the "the middle suddenly sped
        // up" complaint, and a linear ramp corners at both ends.
        //
        // The end value is the old 1.6-3.2 s target, so the last minute is as
        // busy as it ever was; it now just gets there in three musical steps.
        static readonly float[] PaceStepAt = { 0f, CareTempo.LiftAt, CareTempo.SecondLiftAt, CareTempo.OutroAt };
        static readonly float[] PaceStepMin = { 3f, 2.4f, 2f, 1.6f };
        static readonly float[] PaceStepMax = { 6f, 4.8f, 4f, 3.2f };
        const float HungerGapMaxStart = 6f;
        // Below this the queue stops being a queue and starts being a spam;
        // also the floor that keeps a random roll from ever producing a
        // near-zero gap.
        public const float HungerGapFloor = 1.6f;
        public float HungerPace => HungerPaceAt(TaskTime);
        // 0 at the authored pace, 1 at the final sprint pace - a single number
        // the HUD/probe can report without caring about the staircase.
        public static float HungerPaceAt(float time) =>
            1f - HungerGapMaxAt(time) / HungerGapMaxStart;
        public static float HungerGapMinAt(float time) => StepAt(time, PaceStepMin);
        public static float HungerGapMaxAt(float time) =>
            Mathf.Max(HungerGapFloor, StepAt(time, PaceStepMax));
        static float StepAt(float time, float[] values)
        {
            float value = values[0];
            for (int i = 1; i < PaceStepAt.Length; i++)
                value = Mathf.Lerp(value, values[i], CareTempo.Bridge01(time, PaceStepAt[i]));
            return value;
        }
        // One roll of the dial, clamped so neither end of the curve can produce
        // an unfair gap (a zero-length queue or a long silent stretch).
        public static float RollHungerGap(float time) =>
            Mathf.Clamp(UnityEngine.Random.Range(HungerGapMinAt(time), HungerGapMaxAt(time)),
                HungerGapFloor, HungerGapMaxStart);
        void ScheduleNextHunger(float delay)
        {
            if (monkeys == null || monkeys.Length == 0 || Round == null) { HungerOwner = null; return; }
            MonkeyCareNpc repeat = null;
            for (int step = 1; step <= monkeys.Length; step++)
            {
                int index = (hungerCursor + step + monkeys.Length) % monkeys.Length;
                var npc = monkeys[index];
                if (!npc || npc.CurrentTaskIndex >= Round.TasksForMonkey(index)) continue;
                if (npc == HungerOwner) { repeat = npc; continue; }
                hungerCursor = index;
                HungerOwner = npc;
                npc.QueueDemand(TaskTime + delay);
                return;
            }
            // Only the monkey that was just fed still has episodes left.
            HungerOwner = repeat;
            if (repeat) repeat.QueueDemand(TaskTime + delay);
        }
        public void SetGloveMenuOpen(bool open)
        {
            if (locomotion) locomotion.inputLocked = open;
            if (desktopController) desktopController.inputLocked = open;
        }
        public bool EquipBananaFromGlove(bool requireSelection = true)
        {
            if (!Playing || Round.Frozen || Round.Bananas <= 0 || held || items == null ||
                GloveInventory == null || (requireSelection && !GloveInventory.BananaSelected)) return false;
            CareItem banana = items.FirstOrDefault(item => item && item.kind == CareNeed.Hunger &&
                                                           item.Available && !item.Held && !item.Thrown);
            Transform grip = Hands ? Hands.RightGripSocket : RightHand;
            if (!grip) grip = RightHand;
            if (!banana || !banana.EquipFromGlove(grip)) return false;
            held = banana;
            hasThrowSolution = false;
            arcAiming = arcValid = false;
            Hands?.AttachHeldItem(banana);
            return true;
        }
        public void CancelSelectedBanana()
        {
            if (held && held.kind == CareNeed.Hunger)
            {
                held.Release();
                held = null;
                Hands?.ReleaseHeldItem();
            }
            hasThrowSolution = false;
            arcAiming = arcValid = false;
            StopHaptics();
        }
        public bool ConsumeThrownBanana()
        {
            // The glove selection is deliberately sticky: running out of bananas
            // must not send the player back into the bag.
            return Round != null && Round.ConsumeBanana();
        }
        public bool TryPickupBananas()
        {
            if (!Playing || Round == null || Round.Frozen ||
                !bananaPickup || !bananaPickup.PlayerInRange || !Round.RefillBananas()) return false;
            // Refilling re-arms the hand on its own (one frame later, so the
            // pickup press does not also throw the new banana).
            nextBananaEquipAt = Time.time + .15f;
            return true;
        }
        Transform DesktopRightHand()
        {
            if (!playerCamera) return null;
            if (!desktopRightHand)
            {
                desktopRightHand = new GameObject("Care Desktop Right Hand").transform;
                desktopRightHand.SetParent(playerCamera.transform, false);
                desktopRightHand.localPosition = new Vector3(.34f, -.24f, .52f);
                desktopRightHand.localRotation = Quaternion.Euler(4f, -3f, -3f);
            }
            return desktopRightHand;
        }
        Transform DesktopLeftHand()
        {
            if (!playerCamera) return null;
            if (!desktopLeftHand)
            {
                desktopLeftHand = new GameObject("Care Desktop Left Hand").transform;
                desktopLeftHand.SetParent(playerCamera.transform, false);
                desktopLeftHand.localPosition = new Vector3(-.34f, -.08f, .58f);
                desktopLeftHand.localRotation = Quaternion.Euler(4f, 3f, 3f);
            }
            return desktopLeftHand;
        }
        LineRenderer MakeLine(string name)
        {
            var go = new GameObject(name); go.transform.SetParent(transform); go.layer = 2;
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = pointerMaterial;
            line.startWidth = line.endWidth = lineWidth; line.positionCount = 2; line.enabled = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return line;
        }
        void Update()
        {
            // Camera.allCameras + LINQ allocates every call; while the XR loader
            // was being torn down the active camera vanished for seconds and this
            // ran on every frame. Probe at most twice a second.
            if (begun && (!playerCamera || !playerCamera.isActiveAndEnabled) &&
                Time.unscaledTime >= nextCameraProbe)
            {
                nextCameraProbe = Time.unscaledTime + .5f;
                var camera = Camera.allCameras.FirstOrDefault(c => c.enabled && c.gameObject.activeInHierarchy && c.cameraType == CameraType.Game);
                if (camera != playerCamera)
                {
                    playerCamera = camera;
                    audioFeedback.FollowCamera(playerCamera);
                }
            }
            if (!Playing)
            {
                if (pointer) pointer.enabled = false; if (arc) arc.enabled = false;
                StopHaptics(); return;
            }
            Input = ReadSessionInput();
            if (readyGateActive)
            {
                if (pointer) pointer.enabled = false;
                if (arc) arc.enabled = false;
                StopHaptics();
                // Keep the instruction alive and pulsing so it can never be
                // mistaken for a decorative label.
                if (readyPrompt)
                {
                    float pulse = .72f + .28f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 3.4f));
                    readyPrompt.color = new Color(1f, 1f, 1f, pulse);
                }
                if (Input.AWasPressed && Time.unscaledTime >= readyArmedAt) AcceptReadyGate();
                return;
            }
            if (goSequenceActive)
            {
                gameTime = Mathf.Min(CareRound.Duration, gameTime + Time.deltaTime);
                if (pointer) pointer.enabled = false;
                if (arc) arc.enabled = false;
                StopHaptics();
                if (gameTime >= goUntil) BeginAfterReadyGo();
                return;
            }
            // One clock, one soundtrack, one set of timestamps: the lesson
            // burns round time just like the rest of the round, so bite progress
            // ("task appeared 3.5 s ago") and the countdown never disagree.
            // Parking this clock during the lesson was the bug where the first
            // banana never finished being eaten and the countdown sat at 02:00.
            gameTime = Mathf.Min(CareRound.Duration, gameTime + Time.deltaTime);
            // A player who idles away the whole clock inside the lesson still
            // gets a clean whistle: hand the round over at the cutoff instead
            // of letting the tutorial card float over the results screen.
            if (TutorialActive && gameTime >= CareRound.Cutoff) FinishTutorial();
            // Results card shortcuts, matching the badges printed on the two
            // pills: A (✕) = CONTINUE back home, B (○) = RESTART right away.
            // Aiming the look ray at the card still works as a fallback.
            if (Round.Frozen && !readyGateActive && !goSequenceActive)
            {
                if (Input.BWasPressed) { RestartRound(); return; }
                if (Input.AWasPressed) { ReturnHome(); return; }
            }
            // The bag is available for the whole live round, the lesson
            // included: gating it on the round clock (gameTime >= .1f) meant the
            // tutorial's "open the bag" step could never be satisfied while the
            // clock was parked at zero.
            GloveInventory?.Tick(Input, Round, !Round.Frozen);
            bool menuOpen = GloveInventory != null && GloveInventory.IsOpen;
            if (!menuOpen && Input.AWasPressed) TryPickupBananas();
            Transform hand = RightHand; if (!hand) return;
            if (suppressUntilRelease)
            {
                if (Input.TriggerHeld || Input.GripHeld) return;
                suppressUntilRelease = false;
            }
            if (held && (!held.Available || !held.Held)) { held = null; StopHaptics(); }
            menuOpen = GloveInventory != null && GloveInventory.IsOpen;
            if (!menuOpen && !held && GloveInventory != null && GloveInventory.BananaSelected &&
                !Round.Frozen && Round.Bananas > 0 && Time.time >= nextBananaEquipAt)
                EquipBananaFromGlove();

            RaycastHit? hit;
            Vector3 throwVelocity = Vector3.zero;
            int throwPointCount = 0;
            bool hasThrowArc = held && !menuOpen && Input.TriggerHeld;
            if (hasThrowArc)
            {
                Vector3 destination = ThrowAimPoint(hand);
                // Always solve the arc for the aimed point: a straight palm
                // forward vector dropped short of the monkey every time.
                throwVelocity = CareItem.BallisticVelocity(hand.position, destination, throwSpeed);
                throwPointCount = TraceThrowArc(hand, throwVelocity, out hit, out MonkeyCareNpc trajectoryNpc);
                RayNpc = trajectoryNpc;
                lastThrowVelocity = throwVelocity;
                hasThrowSolution = true;
            }
            else
            {
                hit = Cast(hand.position, hand.forward, rayDistance, true);
                RayNpc = hit.HasValue ? hit.Value.collider.GetComponentInParent<MonkeyCareNpc>() : null;
            }
            var itemTarget = hit.HasValue ? hit.Value.collider.GetComponentInParent<CareItem>() : null;
            var point = hit.HasValue ? hit.Value.collider.GetComponent<CareTeleportPoint>() : null;
            var resultButton = hit.HasValue ? hit.Value.collider.GetComponent<CareResultButton>() : null;
            if (highlighted && highlighted != point) highlighted.Highlight(false);
            highlighted = point; if (highlighted) highlighted.Highlight(true);

            if (!menuOpen && held && Input.TriggerReleased)
            {
                // The reticle was on a hungry monkey when the trigger opened, so
                // that monkey is the intended receiver: the banana gets a short
                // assist window instead of being lost to a graze on the way.
                var receiver = RayNpc && RayNpc.Accepts(CareNeed.Hunger) ? RayNpc : null;
                held.Throw(hasThrowSolution ? lastThrowVelocity : hand.forward * throwSpeed, receiver);
                held = null;
                nextBananaEquipAt = 0;
                EquipBananaFromGlove(false);
                hasThrowSolution = false;
                pointer.enabled = false;
                StopHaptics();
            }

            if (!menuOpen && resultButton && Input.TriggerPressed)
            {
                resultButton.Activate(); arcAiming = false; arcValid = false;
            }
            else if (!menuOpen && point && Input.TriggerPressed && !held && locomotion)
            {
                if (locomotion.TryTeleport(point.Landing)) foreach (var item in items) item.ResetStroke();
                arcAiming = false; arcValid = false;
            }
            else if (!feedingOnly && !menuOpen && !Round.Frozen && !held && itemTarget && Input.TriggerHeld && Input.GripHeld)
            {
                if (itemTarget.Grab(hand)) held = itemTarget;
                arcAiming = false; arcValid = false;
            }

            bool groundAim = !menuOpen && !held && !itemTarget && !point && Input.TriggerHeld && Input.GripHeld &&
                locomotion && locomotion.allowTeleport && !locomotion.groundOnly &&
                hit.HasValue && hit.Value.normal.y >= .45f;
            if (groundAim)
            {
                arcAiming = true; BuildArc(hand);
            }
            else
            {
                if (arcAiming && (!Input.TriggerHeld || !Input.GripHeld) && arcValid && locomotion) locomotion.TryTeleport(landing);
                arcAiming = false; arcValid = false; arc.enabled = false;
            }
            // The care ray belongs to the banana in hand: it shows while a banana
            // is held, turns orange and vibrates on a hungry monkey (both VR and
            // PC), and stops the moment the monkey starts eating.
            pointer.enabled = !arcAiming && !menuOpen && held != null;
            if (hasThrowArc)
            {
                pointer.positionCount = throwPointCount;
                for (int i = 0; i < throwPointCount; i++) pointer.SetPosition(i, arcPoints[i]);
            }
            else
            {
                pointer.positionCount = 2;
                pointer.SetPosition(0, hand.position);
                pointer.SetPosition(1, hit.HasValue ? hit.Value.point : hand.position + hand.forward * rayDistance);
            }
            bool validTarget = held && RayNpc && RayNpc.Accepts(CareNeed.Hunger);
            pointer.startColor = pointer.endColor = validTarget ? new Color(1f, .63f, .18f, 1) :
                point || itemTarget || RayNpc ? new Color(.3f, 1f, .8f) : new Color(.7f, .9f, 1, .65f);
        }
        // The Meta interaction rig draws its own pointing ray out of each hand.
        // The care scene only needs the banana aim line, so those hand rays are
        // switched off (twice a second, because the interactor re-enables its
        // visual whenever it starts or stops hovering something).
        void SuppressHandRays()
        {
            if (!rig || !rig.gameObject.activeInHierarchy) return;
            if (Time.unscaledTime < nextHandRayCheck) return;
            nextHandRayCheck = Time.unscaledTime + .5f;
            foreach (var ray in rig.GetComponentsInChildren<Oculus.Interaction.RayInteractor>(true))
            {
                foreach (var line in ray.GetComponentsInChildren<LineRenderer>(true))
                    if (line && line.enabled) line.enabled = false;
            }
        }
        // The headset can be put on (or removed) after Play started: the HUD must
        // follow, otherwise the flat PC plates keep covering the VR view.
        void RebuildHudOnPlatformFlip()
        {
            if (roundView == null || roundView.BuiltForVr == CarePlatform.IsVr) return;
            bool vrNow = CarePlatform.IsVr;
            roundView.DestroyPlates();
            roundView = new CareRoundView(Head, roundArtwork);
            // The glove inventory carries its own plates (screen-space bag
            // button on PC, world-space in VR), so it has to switch layouts
            // with the rest of the HUD or the wrong-layout button lingers.
            if (GloveInventory != null)
            {
                GloveInventory.DestroyPlates();
                GloveInventory = new CareGloveInventory(this, Head);
                if (!Round.Frozen) GloveInventory.ShowHud();
            }
            if (RoundStarted && !Round.Frozen) roundView.ShowHud();
            Debug.Log("[Monkey Care] HUD rebuilt for " + (vrNow ? "VR" : "PC") + " layout.");
        }
        /// <summary>Rebuild the plates for whichever platform is active right now.</summary>
        public void RebuildHudForPlatform(bool showPlates = false)
        {
            RebuildHudOnPlatformFlip();
            if (showPlates && roundView != null) roundView.ShowHud();
        }
        /// <summary>The live round HUD (PC flat overlay or VR world plates).</summary>
        public CareRoundView RoundView => roundView;
        void LateUpdate()
        {            // Track whichever transform is the real eye: the rig activates a few
            // frames after Start() and a controller/hand swap can move it again,
            // so the anchor is re-parented every frame instead of once.
            if (uiHead) uiHead.Follow(HeadSource());
            SuppressHandRays();
            RebuildHudOnPlatformFlip();
            // Start() is a coroutine: LateUpdate can run once before the round
            // object exists, so everything below requires a live round.
            if (!begun || Round == null) return;
            // Freeze at the time cutoff OR as soon as every banana is fed
            // (100% progress ends the round immediately).
            if (RoundStarted && begun && !Round.Frozen &&
                (RoundElapsed >= CareRound.Cutoff || Round.FeedingTotal >= Round.TaskTotal))
            {
                Round.Freeze();
                HungerOwner = null;
                foreach (var npc in monkeys) npc.StopAtCutoff();
                if (held) held.Release(); held = null; StopHaptics();
                GloveInventory?.Close();
                // The PC bag button lives in its own screen-space root (not
                // under the head anchor), so the child loop above never sees
                // it; hide it here or it floats over the results card.
                GloveInventory?.HideHud();
                // Results view only: hide every other HUD attached to the
                // head anchor (score, banana, tutorial, ready gate). The bag
                // panel is owned by CareGloveInventory and closed right above,
                // so it is deliberately left out of both loops — force-enabling
                // it here used to park an open bag over the results and home
                // screens.
                foreach (Transform child in Head)
                    if (child.name.StartsWith("Care ") && child.name != "Care RESULTS" &&
                        child.name != "Care Backpack")
                        child.gameObject.SetActive(false);
                if (bananaPickup) bananaPickup.gameObject.SetActive(false);
                roundView.ShowResults(); SaveRoundRecord();
                // Overcooked hands the results screen its own victory loop;
                // the round track fades out underneath it.
                audioFeedback.PlayVictory();
            }
            if (begun && !readyGateActive && !goSequenceActive && roundView != null)
            {
                // Final sprint: the last twenty seconds must never be dead air.
                // If nobody owns the hunger slot, hand it out right away so the
                // player always has a target and can still chase the last star.
                if (RoundStarted && !Round.Frozen &&
                    CareRound.Cutoff - RoundElapsed <= FinalSprintSeconds &&
                    Round.FeedingTotal < Round.TaskTotal &&
                    (!HungerOwner || (!HungerOwner.HasDemand && HungerOwner.NextIn > 1.2f)))
                    ScheduleNextHunger(.4f);
                roundView.Update(Round, RoundElapsed);
            }
            // The HUD clock counts down from Cutoff, and the round freezes there,
            // not at Duration (that is only the scoring denominator). Audio has
            // to use the same clock or the warning beeps and the music cue land
            // ten seconds late - the five second countdown would never fire.
            float roundProgress = Mathf.Clamp01(RoundElapsed / CareRound.Cutoff);
            float roundRemaining = CareRound.Cutoff - RoundElapsed;
            audioFeedback.UpdateMusic(Round.Frozen, roundProgress, roundRemaining, CareRound.Cutoff);
            // The clock beeps for the last five seconds only, and it does so
            // from the moment play starts - the lesson spends real time too, so
            // a player still reading the card must hear the same warning as
            // everyone else. The ready gate and the results card stay silent.
            audioFeedback.UpdateCountdown(roundRemaining,
                begun && !Round.Frozen && !readyGateActive && !goSequenceActive);
            // Haptics mean "a hungry monkey is locked in your sights". While
            // that monkey is already eating the caught banana the buzz stops —
            // the request has been answered and the player needs no cue.
            bool foodAimed = held && held.Held &&
                             RayNpc && RayNpc.Accepts(CareNeed.Hunger) &&
                             !RayNpc.EatingNow;
            LogHapticDevicesOnce();
            if (Playing && foodAimed && (GloveInventory == null || !GloveInventory.IsOpen)) SetHaptics(hapticAmplitude);
            else StopHaptics();
        }
        CareInputSnapshot ReadSessionInput()
        {
#if UNITY_EDITOR
            if (recordingHand)
            {
                var result = new CareInputSnapshot(
                    recordingA && !previousRecordingA,
                    recordingB && !previousRecordingB,
                    recordingTrigger,
                    recordingTrigger && !previousRecordingTrigger,
                    !recordingTrigger && previousRecordingTrigger,
                    recordingGrip, Vector2.zero, Vector2.zero);
                previousRecordingTrigger = recordingTrigger;
                previousRecordingA = recordingA;
                previousRecordingB = recordingB;
                return result;
            }
#endif
            return CareInput.Read(desktopPreview);
        }
        RaycastHit? Cast(Vector3 origin, Vector3 direction, float distance, bool includeTriggers)
        {
            RaycastHit? nearest = null;
            foreach (var hit in Physics.RaycastAll(origin, direction, distance, ~((1 << 2) | (1 << 5)),
                         includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore))
            {
                // Legacy SDK selection surfaces must not occlude the scene's care ray.
                if (includeTriggers && hit.collider.GetComponent<Oculus.Interaction.Surfaces.ColliderSurface>() && !hit.collider.GetComponentInParent<CareItem>()) continue;
                if (hit.distance < .03f || (rig && hit.transform.IsChildOf(rig.transform)) ||
                    (held && hit.collider.GetComponentInParent<CareItem>() == held)) continue;
                if (hit.collider.isTrigger && !hit.collider.GetComponentInParent<MonkeyCareNpc>() && !hit.collider.GetComponent<CareTeleportPoint>() && !hit.collider.GetComponent<CareResultButton>()) continue;
                if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
            }
            return nearest;
        }
        Vector3 ThrowAimPoint(Transform hand)
        {
            // Aim exactly where the reticle points. The VR branch used to guess a
            // point straight out of the palm, so the banana flew to empty air
            // while the ray was already highlighting a monkey.
            bool desktop = hand == desktopRightHand;
            Vector3 origin = desktop ? playerCamera.transform.position : hand.position;
            Vector3 direction = desktop ? playerCamera.transform.forward : hand.forward;
            var aimHit = Cast(origin, direction, rayDistance, true);
            if (aimHit.HasValue)
            {
                var npc = aimHit.Value.collider.GetComponentInParent<MonkeyCareNpc>();
                if (npc)
                {
                    // Throw into the feeding trigger, not at the collider skin:
                    // the contact box is what actually accepts the banana.
                    var contact = npc.transform.Find("Care Contact");
                    if (contact && contact.TryGetComponent(out Collider contactBox))
                        return contactBox.bounds.center;
                }
                return aimHit.Value.point;
            }
            return origin + direction * Mathf.Max(8f, rayDistance * .5f);
        }
        int TraceThrowArc(Transform hand, Vector3 velocity, out RaycastHit? impact, out MonkeyCareNpc npc)
        {
            int count = 1;
            impact = null;
            npc = null;
            arcPoints[0] = hand.position;
            for (int i = 1; i < arcPoints.Length; i++)
            {
                float t = i * .065f;
                Vector3 next = hand.position + velocity * t + Physics.gravity * (.5f * t * t);
                Vector3 segment = next - arcPoints[i - 1];
                if (segment.sqrMagnitude < .000001f) break;
                var hit = Cast(arcPoints[i - 1], segment.normalized, segment.magnitude, true);
                arcPoints[i] = hit.HasValue ? hit.Value.point : next;
                count++;
                if (!hit.HasValue) continue;
                impact = hit;
                npc = hit.Value.collider.GetComponentInParent<MonkeyCareNpc>();
                break;
            }
            return count;
        }
        void BuildArc(Transform hand)
        {
            arcValid = false; int count = 1; arcPoints[0] = hand.position;
            Vector3 velocity = hand.forward * (locomotion ? locomotion.launchSpeed : 7);
            for (int i = 1; i < arcPoints.Length; i++)
            {
                float t = i * .08f;
                Vector3 p = hand.position + velocity * t + Physics.gravity * (.5f * t * t);
                Vector3 segment = p - arcPoints[i - 1];
                var hit = Cast(arcPoints[i - 1], segment.normalized, segment.magnitude, false);
                arcPoints[i] = hit.HasValue ? hit.Value.point : p; count++;
                if (hit.HasValue)
                {
                    landing = hit.Value.point;
                    arcValid = hit.Value.normal.y >= .45f && locomotion && locomotion.IsLandingClear(landing) && landing.y >= -.12f;
                    break;
                }
            }
            arc.enabled = true; arc.positionCount = count;
            for (int i = 0; i < count; i++) arc.SetPosition(i, arcPoints[i]);
            arc.startColor = arc.endColor = arcValid ? Color.cyan : new Color(1, .3f, .3f);
        }
        void SetHaptics(float amplitude)
        {
            CareXboxInput.SetMotorSpeeds(amplitude * .68f, amplitude);
            if (Gamepad.all.Count > 0)
            {
                foreach (var pad in Gamepad.all) pad.SetMotorSpeeds(amplitude * .68f, amplitude);
                return;
            }
            if (OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
                OVRInput.SetControllerVibration(hapticFrequency, amplitude, OVRInput.Controller.RTouch);
            else
            {
                var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse) device.SendHapticImpulse(0, amplitude, .05f);
            }
        }
        void LogHapticDevicesOnce()
        {
            if (hapticControllerLogged) return;
            hapticControllerLogged = true;
            if (Gamepad.all.Count == 0)
            {
                Debug.Log("[Monkey Care] 未检测到 Input System 手柄，暂停增强震动日志。");
                return;
            }
            foreach (var pad in Gamepad.all)
                Debug.Log("[Monkey Care] 震动控制器: " + pad.displayName + " | layout=" + pad.layout +
                          " | interface=" + pad.description.interfaceName);
        }
        public void StopHaptics()
        {
            CareXboxInput.SetMotorSpeeds(0, 0);
            foreach (var pad in Gamepad.all) pad.SetMotorSpeeds(0, 0);
            if (OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)) OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand); if (device.isValid) device.StopHaptics();
        }
        public CareTaskRecord RegisterTask(int monkey, int task, CareNeed need, float appearedAt)
        {
            var record = new CareTaskRecord { monkey = monkey + 1, task = task + 1, need = need.ToString(), appearedAt = appearedAt };
            records.Add(record); return record;
        }
        public bool CompleteTask(MonkeyCareNpc npc, float timestamp)
        {
            int index = System.Array.IndexOf(monkeys, npc);
            if (Round == null || Round.Frozen || index < 0 || timestamp < 1) return false;
            bool completed = Round.Complete(index, npc.CurrentTaskIndex, npc.Need, timestamp);
            if (!completed) return false;
            // The score bar must reflect a finished feeding in the same frame:
            // repaint the HUD now instead of waiting for the next LateUpdate.
            SyncHud();
            NotifyFed(npc);
            return true;
        }
        public void SyncHud()
        {
            if (roundView != null && Round != null && begun)
                roundView.Update(Round, RoundElapsed);
        }
        /// <summary>
        /// CONTINUE on the results card: leave the finished round and return to
        /// the home/standby screen. The real home UI is not in the project yet,
        /// so the READY gate stands in for it — when the home artwork arrives,
        /// swap ShowReadyGate() for the home view here and nothing else changes.
        /// </summary>
        public void ReturnHome() => ResetFromResults(false);
        public void RestartRound() => ResetFromResults(true);
        void ResetFromResults(bool startImmediately)
        {
            if (Round == null || !Round.Frozen) return;
            // Both results pills are real buttons: click before the screen
            // changes, and cut the fanfare here so CONTINUE doesn't leave it
            // looping under the READY gate.
            audioFeedback.Click();
            audioFeedback.StopVictory();
            StopHaptics(); if (held) held.Release(); held = null;
            Round = new CareRound(MonkeyStartingNeeds(), feedingOnly);
            Round.DealFeedingTasks();
            records.Clear(); gameTime = 0;
            HungerOwner = null; hungerCursor = -1;
            TutorialActive = false; RoundStarted = false;
            rig.transform.SetPositionAndRotation(rigStartPosition, rigStartRotation);
            // The results screen switched every "Care ..." overlay off; switch
            // them back on so the next round has its full HUD again. The bag is
            // excluded: it must stay closed until the player opens it.
            foreach (Transform child in Head)
                if (child.name.StartsWith("Care ") && child.name != "Care RESULTS" &&
                    child.name != "Care Backpack")
                    child.gameObject.SetActive(true);
            roundView.Reset();
            if (bananaPickup) bananaPickup.gameObject.SetActive(true);
            // The bag panel is a world-space plate that happily stays open over
            // the home/results screen; close it before showing either.
            GloveInventory?.Close();
            GloveInventory?.Reset(Round);
            GloveInventory?.HideHud();
            nextBananaEquipAt = 0;
            SetPlayerInputLocked(true);
            foreach (var item in items) item.RestoreSupply();
            foreach (var npc in monkeys) npc.ResetForRound(false);
            ShowReadyGate();
            suppressUntilRelease = true; arcAiming = false;
            CareInput.ForgetEdges();
            if (startImmediately)
            {
                // RESTART is an explicit "again, now": skip the arming delay and
                // roll straight into the GO beat.
                readyArmedAt = 0;
                AcceptReadyGate();
            }
            Debug.Log("[Monkey Care] " + (CarePlatform.FlatUi ? "PC " : "VR ") +
                      (startImmediately ? "RESTART：立即重开一局" : "CONTINUE：返回主页（占位 READY 待机门）"));
        }
        [System.Serializable]
        sealed class RoundReport
        {
            public string version = "2.0-feeding-only";
            public int tasksCompleted, feedingsCompleted, scratchesCompleted, bananasRemaining, stars;
            public double score;
            public CareTaskRecord[] tasks;
        }
        void SaveRoundRecord()
        {
            var report = new RoundReport { tasksCompleted = Round.CompletedTotal, feedingsCompleted = Round.FeedingTotal,
                scratchesCompleted = Round.ScratchTotal, bananasRemaining = Round.Bananas, stars = Round.Stars,
                score = Round.Score, tasks = records.ToArray() };
#if UNITY_EDITOR
            string folder = "work/monkey-care/rounds";
#else
            string folder = Path.Combine(Application.persistentDataPath, "MonkeyCare");
#endif
            try { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "round-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"), JsonUtility.ToJson(report, true)); }
            catch (System.Exception ex) { Debug.LogWarning("[Monkey Care] 无法保存试玩记录: " + ex.Message); }
        }
        void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value)
            {
                if (held) held.Release(); held = null;
                GloveInventory?.Close();
                arcAiming = arcValid = false; suppressUntilRelease = true; StopHaptics();
                if (highlighted) highlighted.Highlight(false);
            }
        }
        void OnDisable()
        {
            CareInput.LabelsChanged -= RefreshReadyPrompt;
            audioFeedback.Stop();
            StopHaptics(); if (held) held.Release();
            GloveInventory?.Close();
            if (locomotion) locomotion.externalRightHandControl = false;
            if (locomotion) { locomotion.groundOnly = false; locomotion.allowTeleport = true; }
            if (locomotion) locomotion.inputLocked = false;
            if (desktopController) desktopController.inputLocked = false;
            if (Instance == this) Instance = null;
        }
    }
}
