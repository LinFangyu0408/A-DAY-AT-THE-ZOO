using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [InspectorName("震动强度"), Range(0, 1)] public float hapticAmplitude = .15f;
        [InspectorName("震动频率"), Range(0, 1)] public float hapticFrequency = .3f;
        [InspectorName("桌面调试输入")] public bool desktopPreview = true;
        public Material pointerMaterial;
        [Header("游戏音频")]
        public CareAudio audioFeedback = new();
        [Header("计分文档 v1.0 / 回合UI")]
        public CareRoundArtwork roundArtwork = new();
        [HideInInspector] public int rulesVersion;
        [HideInInspector] public bool testAllowUnfocused;
        public static MonkeyCareSession Instance { get; private set; }
        public static bool Playing => Instance && Instance.begun && (Instance.focused || Instance.testAllowUnfocused) && Time.timeScale > 0;
        public static Camera PlayerCamera => Instance ? Instance.playerCamera : null;
        public static float GameTime => Instance ? Instance.gameTime : Time.time;
        public static float TaskTime => Mathf.Min(GameTime, CareRound.Cutoff);
        public CareRound Round { get; private set; } = new CareRound();
        public Transform RightHand
        {
            get
            {
#if UNITY_EDITOR
                if (recordingHand) return recordingHand;
#endif
                bool tracked = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch) ||
                               InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;
                return tracked && rig && rig.gameObject.activeInHierarchy && rig.rightControllerAnchor
                    ? rig.rightControllerAnchor : DesktopRightHand();
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
        Camera playerCamera;
        DesktopPlayerController desktopController;
        Transform desktopRightHand;
        CareItem held;
        CareTeleportPoint highlighted;
        LineRenderer pointer, arc;
        RectTransform readyCanvas;
        GameObject readyFx;
        CareRoundView roundView;
        readonly List<CareTaskRecord> records = new();
        Vector3 rigStartPosition;
        Quaternion rigStartRotation;
        bool begun, focused = true, arcAiming, arcValid, supplyEnabled, suppressUntilRelease;
        Vector3 lastThrowVelocity;
        bool hasThrowSolution;
        Vector3 landing;
        float gameTime;
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
            pointer = MakeLine("Care Straight Ray"); arc = MakeLine("Care Teleport Arc");
            if (locomotion) locomotion.externalRightHandControl = true;
            begun = true;
            Round = new CareRound(MonkeyStartingNeeds(), feedingOnly);
            rigStartPosition = rig.transform.position; rigStartRotation = rig.transform.rotation;
            SetupFeedingOnlyScene();
            roundView = new CareRoundView(playerCamera, roundArtwork);
            GloveInventory = new CareGloveInventory(this, playerCamera);
            // The guided first run starts immediately. Keep the Ready/Go flourish only for unguided rounds.
            if (!showTutorial)
            {
                readyCanvas = CareUi.Canvas("Care READY GO", playerCamera.transform,
                    new Vector3(0, 0, 1.8f), 1.0f, new Vector2(800, 470));
                CareUi.Image("READY GO", readyCanvas, readyGo, new Vector2(800, 470), Vector2.zero);
                CareReadyView.Decorate(readyCanvas, roundArtwork.star, readySeconds);
            }
            audioFeedback.FollowCamera(playerCamera);
            audioFeedback.StartMusic(transform);
            TutorialActive = showTutorial;
            if (showTutorial)
                gameObject.AddComponent<CareTutorial>().Begin(this, playerCamera, Time.unscaledTime);
            else
            {
                RoundStarted = true;
                foreach (var npc in monkeys) if (npc) npc.BeginSession();
            }
        }
        CareNeed[] MonkeyStartingNeeds()
        {
            if (monkeys == null) return new[] { CareNeed.Hunger, CareNeed.Hunger, CareNeed.Hunger };
            return monkeys.Select(npc => feedingOnly || !npc ? CareNeed.Hunger : npc.firstNeed).ToArray();
        }
        void SetupFeedingOnlyScene()
        {
            if (!feedingOnly) return;
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
            HideSupplyToolbox("banana box");
            HideSupplyToolbox("Sractch");
            roundArtwork.bananaAnchor = playerCamera.transform;
            roundArtwork.bananaOffset = new Vector3(.43f, .37f, 1.35f);
            roundArtwork.bananaWidth = .32f;
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
        public void FinishTutorial()
        {
            if (RoundStarted) return;
            TutorialActive = false;
            RoundStarted = true;
            gameTime = 0;
            supplyEnabled = true;
            if (readyCanvas) Destroy(readyCanvas.gameObject);
            if (readyFx) Destroy(readyFx);
            GloveInventory?.Close();
            roundView.ShowHud();
            foreach (var npc in monkeys)
            {
                if (!npc) continue;
                npc.EndGuidedTutorial();
                npc.BeginSession();
            }
        }
        public void BeginTutorialHunger()
        {
            if (!TutorialActive || RoundStarted) return;
            TutorialMonkey?.BeginGuidedTutorial(CareNeed.Hunger);
        }
        public void SetGloveMenuOpen(bool open)
        {
            if (locomotion) locomotion.inputLocked = open;
            if (desktopController) desktopController.inputLocked = open;
        }
        public bool EquipBananaFromGlove()
        {
            if (!Playing || Round.Frozen || Round.Bananas <= 0 || held || items == null) return false;
            CareItem banana = items.FirstOrDefault(item => item && item.kind == CareNeed.Hunger && item.Available);
            if (!banana || !banana.EquipFromGlove(RightHand)) return false;
            held = banana;
            hasThrowSolution = false;
            arcAiming = arcValid = false;
            return true;
        }
        public bool ConsumeThrownBanana() => Round != null && Round.ConsumeBanana();
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
            gameTime = Mathf.Min(CareRound.Duration, gameTime + Time.deltaTime);
            Input = ReadSessionInput();
            if (!supplyEnabled && gameTime >= readySeconds)
            {
                supplyEnabled = true; if (readyCanvas) Destroy(readyCanvas.gameObject); if (readyFx) Destroy(readyFx);
                if (RoundStarted) roundView.ShowHud();
            }
            GloveInventory?.Tick(Input, Round, !Round.Frozen && gameTime >= .1f);
            Transform hand = RightHand; if (!hand) return;
            if (suppressUntilRelease)
            {
                if (Input.TriggerHeld || Input.GripHeld) return;
                suppressUntilRelease = false;
            }
            if (held && (!held.Available || !held.Held)) { held = null; StopHaptics(); }
            bool menuOpen = GloveInventory != null && GloveInventory.IsOpen;

            RaycastHit? hit;
            Vector3 throwVelocity = Vector3.zero;
            int throwPointCount = 0;
            bool hasThrowArc = held && !menuOpen && Input.TriggerHeld;
            if (hasThrowArc)
            {
                Vector3 destination = ThrowAimPoint(hand);
                throwVelocity = hand == desktopRightHand
                    ? CareItem.BallisticVelocity(hand.position, destination, throwSpeed)
                    : hand.forward * throwSpeed;
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
                held.Throw(hasThrowSolution ? lastThrowVelocity : hand.forward * throwSpeed);
                held = null;
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
            pointer.enabled = !arcAiming && !menuOpen && (held != null || point || itemTarget || RayNpc);
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
        void LateUpdate()
        {
            // All NPC Update calls have settled the final clipped frame before freezing.
            if (RoundStarted && begun && !Round.Frozen && gameTime >= CareRound.Cutoff)
            {
                Round.Freeze();
                foreach (var npc in monkeys) npc.StopAtCutoff();
                if (held) held.Release(); held = null; StopHaptics();
                roundView.ShowResults(); SaveRoundRecord();
            }
            if (RoundStarted && begun && roundView != null) roundView.Update(Round, gameTime);
            audioFeedback.UpdateMusic(Round.Frozen);
            bool foodAimed = held && held.Held && Input.TriggerHeld &&
                             RayNpc && RayNpc.Accepts(CareNeed.Hunger);
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
            if (hand != desktopRightHand)
                return hand.position + hand.forward * Mathf.Max(3f, rayDistance * .5f);
            var aimHit = Cast(playerCamera.transform.position, playerCamera.transform.forward, rayDistance, true);
            return aimHit.HasValue
                ? aimHit.Value.point
                : playerCamera.transform.position + playerCamera.transform.forward * Mathf.Max(8f, rayDistance * .5f);
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
            CareXboxInput.SetMotorSpeeds(amplitude * .3f, amplitude);
            if (Gamepad.current != null)
            {
                Gamepad.current.SetMotorSpeeds(amplitude * .3f, amplitude);
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
        public void StopHaptics()
        {
            CareXboxInput.SetMotorSpeeds(0, 0);
            Gamepad.current?.SetMotorSpeeds(0, 0);
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
            if (timestamp < 10) return false;
            return Round.Complete(index, npc.CurrentTaskIndex, npc.Need, timestamp);
        }
        public void ContinueExploring() { if (Round.Frozen) roundView.Continue(); }
        public void RestartRound()
        {
            if (!Round.Frozen) return;
            StopHaptics(); if (held) held.Release(); held = null;
            Round = new CareRound(MonkeyStartingNeeds(), feedingOnly);
            records.Clear(); gameTime = 0; supplyEnabled = false;
            TutorialActive = false; RoundStarted = true;
            rig.transform.SetPositionAndRotation(rigStartPosition, rigStartRotation);
            roundView.Reset();
            GloveInventory?.Reset(Round);
            if (locomotion) locomotion.inputLocked = false;
            if (desktopController) desktopController.inputLocked = false;
            foreach (var item in items) item.RestoreSupply();
            foreach (var npc in monkeys) npc.ResetForRound();
            readyCanvas = CareUi.Canvas("Care READY GO", playerCamera.transform, new Vector3(0, 0, 1.8f), 1, new Vector2(800, 470));
            CareUi.Image("READY GO", readyCanvas, readyGo, new Vector2(800, 470), Vector2.zero);
            CareReadyView.Decorate(readyCanvas, roundArtwork.star, readySeconds);
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
            if (locomotion) locomotion.inputLocked = false;
            if (desktopController) desktopController.inputLocked = false;
            if (Instance == this) Instance = null;
        }
    }
}
