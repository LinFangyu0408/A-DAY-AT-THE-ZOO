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
        public static float GameTime => Instance ? Instance.gameTime : Time.time;
        public static float TaskTime => Mathf.Min(GameTime, CareRound.Cutoff);
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
            roundView = new CareRoundView(Head, roundArtwork);
            GloveInventory = new CareGloveInventory(this, Head);
            audioFeedback.FollowCamera(playerCamera);
            audioFeedback.StartMusic(transform);
            TutorialActive = false;
            RoundStarted = false;
            ShowReadyGate();
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
        void ShowReadyGate()
        {
            if (readyCanvas) Destroy(readyCanvas.gameObject);
            readyCanvas = CareUi.Canvas("Care READY GO", Head,
                new Vector3(0, 0, 1.45f), .94f, new Vector2(960, 760), 90);
            var readyArt = CareUiSkin.SvgReadyGo ? CareUiSkin.SvgReadyGo : readyGo;
            if (readyArt)
                CareUi.Image("READY GO", readyCanvas, readyArt, new Vector2(960, 560), Vector2.zero);
            else
                CareUiSkin.AddSliced(readyCanvas, "Ready panel", CareUiSkin.Panel,
                    new Vector2(900, 430), Vector2.zero, new Color(1f, .97f, .86f, 1f));
            CareReadyView.Decorate(readyCanvas, roundArtwork.star, 100000f);
            // The hint sits well below the READY GO artwork so it never covers
            // the big letters.
            CareUiSkin.AddSliced(readyCanvas, "Ready prompt plate", CareUiSkin.Slot,
                new Vector2(620, 108), new Vector2(0, -318), new Color(.08f, .25f, .42f, .94f))
                .transform.SetAsLastSibling();
            var readyKey = CareUiSkin.KeyBadge(readyCanvas, "A", new Vector2(116, 116),
                new Vector2(-206, -318), out readyKeyLabel, 56);
            readyKey.transform.SetAsLastSibling();
            readyPrompt = CareRoundView.Label("Ready prompt",
                readyCanvas, "PRESS A TO START",
                new Vector2(420, 72), new Vector2(56, -318), 42,
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
            readyPrompt.text = "";
            readyKeyLabel.text = "GO";
            readyKeyLabel.color = new Color(1f, .72f, .10f, 1f);
            CareInput.ForgetEdges();
        }
        void BeginAfterReadyGo()
        {
            goSequenceActive = false;
            if (readyCanvas)
            {
                Destroy(readyCanvas.gameObject);
                readyCanvas = null;
            }
            SetPlayerInputLocked(false);
            TutorialActive = showTutorial && !tutorialCompleted;
            if (TutorialActive)
            {
                gameObject.AddComponent<CareTutorial>().Begin(this, Head, Time.unscaledTime);
            }
            else
            {
                RoundStarted = true;
                foreach (var npc in monkeys) if (npc) npc.BeginSession();
                BeginHungerRotation(2.5f);
            }
            roundView.ShowHud();
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
            gameTime = 0;
            if (readyCanvas) Destroy(readyCanvas.gameObject);
            if (readyFx) Destroy(readyFx);
            GloveInventory?.Close();
            roundView.ShowHud();
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
            // 3-6s pause after a successful feeding, then the next monkey asks.
            ScheduleNextHunger(UnityEngine.Random.Range(3f, 6f));
        }
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
            if (begun && (!playerCamera || !playerCamera.isActiveAndEnabled))
            {
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
            gameTime = Mathf.Min(CareRound.Duration, gameTime + Time.deltaTime);
            GloveInventory?.Tick(Input, Round, !Round.Frozen && gameTime >= .1f);
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
            // The care ray belongs to the banana in hand. Empty-hand targeting
            // must stay invisible so it cannot obstruct the scene.
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
        void LateUpdate()
        {            // Track whichever transform is the real eye: the rig activates a few
            // frames after Start() and a controller/hand swap can move it again,
            // so the anchor is re-parented every frame instead of once.
            if (uiHead) uiHead.Follow(HeadSource());
            SuppressHandRays();
            // Start() is a coroutine: LateUpdate can run once before the round
            // object exists, so everything below requires a live round.
            if (!begun || Round == null) return;
            // Freeze at the time cutoff OR as soon as every banana is fed
            // (100% progress ends the round immediately).
            if (RoundStarted && begun && !Round.Frozen &&
                (gameTime >= CareRound.Cutoff || Round.FeedingTotal >= Round.TaskTotal))
            {
                Round.Freeze();
                HungerOwner = null;
                foreach (var npc in monkeys) npc.StopAtCutoff();
                if (held) held.Release(); held = null; StopHaptics();
                GloveInventory?.Close();
                // Results view only: hide every other HUD attached to the
                // head anchor (score, banana, tutorial, glove, ready gate).
                foreach (Transform child in Head)
                    if (child.name.StartsWith("Care ") && child.name != "Care RESULTS")
                        child.gameObject.SetActive(false);
                if (bananaPickup) bananaPickup.gameObject.SetActive(false);
                roundView.ShowResults(); SaveRoundRecord();
            }
            if (begun && !readyGateActive && !goSequenceActive && roundView != null)
            {
                // Final sprint: the last twenty seconds must never be dead air.
                // If nobody owns the hunger slot, hand it out right away so the
                // player always has a target and can still chase the last star.
                if (RoundStarted && !Round.Frozen &&
                    CareRound.Cutoff - gameTime <= FinalSprintSeconds &&
                    Round.FeedingTotal < Round.TaskTotal &&
                    (!HungerOwner || (!HungerOwner.HasDemand && HungerOwner.NextIn > 1.2f)))
                    ScheduleNextHunger(.4f);
                roundView.Update(Round, gameTime);
            }
            audioFeedback.UpdateMusic(Round.Frozen);
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
            if (roundView != null && Round != null && begun) roundView.Update(Round, gameTime);
        }
        public void ContinueExploring() { if (Round.Frozen) roundView.Continue(); }
        public void RestartRound()
        {
            if (!Round.Frozen) return;
            StopHaptics(); if (held) held.Release(); held = null;
            Round = new CareRound(MonkeyStartingNeeds(), feedingOnly);
            Round.DealFeedingTasks();
            records.Clear(); gameTime = 0;
            HungerOwner = null; hungerCursor = -1;
            TutorialActive = false; RoundStarted = false;
            rig.transform.SetPositionAndRotation(rigStartPosition, rigStartRotation);
            // The results screen switched every "Care ..." overlay off; switch
            // them back on so the second round has its full HUD again.
            foreach (Transform child in Head)
                if (child.name.StartsWith("Care ") && child.name != "Care RESULTS")
                    child.gameObject.SetActive(true);
            roundView.Reset();
            if (bananaPickup) bananaPickup.gameObject.SetActive(true);
            GloveInventory?.Reset(Round);
            GloveInventory?.HideHud();
            nextBananaEquipAt = 0;
            SetPlayerInputLocked(true);
            foreach (var item in items) item.RestoreSupply();
            foreach (var npc in monkeys) npc.ResetForRound(false);
            ShowReadyGate();
            suppressUntilRelease = true; arcAiming = false;
            CareInput.ForgetEdges();
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
