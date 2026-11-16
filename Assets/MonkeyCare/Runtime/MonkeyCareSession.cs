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
                return rig && rig.gameObject.activeInHierarchy && rig.rightControllerAnchor
                    ? rig.rightControllerAnchor : playerCamera ? playerCamera.transform : null;
            }
        }
#if UNITY_EDITOR
        internal Transform recordingHand;
        internal bool recordingTrigger, recordingGrip;
#endif
        public MonkeyCareNpc RayNpc { get; private set; }
        public CareItem HeldItem => held;
        public bool Begun => begun;
        Camera playerCamera;
        CareItem held;
        CareTeleportPoint highlighted;
        LineRenderer pointer, arc;
        RectTransform readyCanvas;
        GameObject readyFx;
        CareRoundView roundView;
        readonly List<CareTaskRecord> records = new();
        Vector3 rigStartPosition;
        Quaternion rigStartRotation;
        bool begun, focused = true, previousTrigger, previousGrip, arcAiming, arcValid, supplyEnabled, suppressUntilRelease;
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
            pointer = MakeLine("Care Straight Ray"); arc = MakeLine("Care Teleport Arc");
            if (locomotion) locomotion.externalRightHandControl = true;
            begun = true;
            Round = new CareRound(monkeys.Select(n => n.firstNeed).ToArray());
            rigStartPosition = rig.transform.position; rigStartRotation = rig.transform.rotation;
            roundView = new CareRoundView(playerCamera, roundArtwork);
            readyCanvas = CareUi.Canvas("Care READY GO", playerCamera.transform, new Vector3(0, 0, 1.8f), 1.0f, new Vector2(800, 470));
            CareUi.Image("READY GO", readyCanvas, readyGo, new Vector2(800, 470), Vector2.zero);
            CareReadyView.Decorate(readyCanvas, roundArtwork.star, readySeconds);
            audioFeedback.FollowCamera(playerCamera);
            audioFeedback.StartMusic(transform);
            foreach (var npc in monkeys) if (npc) npc.BeginSession();
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
            if (!supplyEnabled && gameTime >= readySeconds)
            {
                supplyEnabled = true; if (readyCanvas) Destroy(readyCanvas.gameObject); if (readyFx) Destroy(readyFx);
                roundView.ShowHud();
            }
            Transform hand = RightHand; if (!hand) return;
            ReadInput(out bool trigger, out bool grip);
            if (suppressUntilRelease)
            {
                if (trigger || grip) { previousTrigger = trigger; previousGrip = grip; return; }
                suppressUntilRelease = false;
            }
            bool triggerDown = trigger && !previousTrigger;
            previousTrigger = trigger; previousGrip = grip;
            if (held && (!held.Available || !held.Held)) { held = null; StopHaptics(); }
            bool justReleased = held && !grip;
            if (justReleased) { held.Release(); held = null; StopHaptics(); }

            RaycastHit? hit = Cast(hand.position, hand.forward, rayDistance, true);
            RayNpc = hit.HasValue ? hit.Value.collider.GetComponentInParent<MonkeyCareNpc>() : null;
            var itemTarget = hit.HasValue ? hit.Value.collider.GetComponentInParent<CareItem>() : null;
            var point = hit.HasValue ? hit.Value.collider.GetComponent<CareTeleportPoint>() : null;
            var resultButton = hit.HasValue ? hit.Value.collider.GetComponent<CareResultButton>() : null;
            if (highlighted && highlighted != point) highlighted.Highlight(false);
            highlighted = point; if (highlighted) highlighted.Highlight(true);

            if (resultButton && triggerDown)
            {
                resultButton.Activate(); arcAiming = false; arcValid = false;
            }
            else if (point && triggerDown && locomotion)
            {
                if (locomotion.TryTeleport(point.Landing)) foreach (var item in items) item.ResetStroke();
                arcAiming = false; arcValid = false;
            }
            else if (!Round.Frozen && !held && !justReleased && itemTarget && trigger && grip)
            {
                if (itemTarget.Grab(hand)) held = itemTarget;
                arcAiming = false; arcValid = false;
            }

            bool groundAim = !held && !justReleased && !itemTarget && !point && trigger && grip && hit.HasValue && hit.Value.normal.y >= .45f;
            if (groundAim)
            {
                arcAiming = true; BuildArc(hand);
            }
            else
            {
                if (arcAiming && (!trigger || !grip) && arcValid && locomotion) locomotion.TryTeleport(landing);
                arcAiming = false; arcValid = false; arc.enabled = false;
            }
            pointer.enabled = !arcAiming;
            pointer.SetPosition(0, hand.position);
            pointer.SetPosition(1, hit.HasValue ? hit.Value.point : hand.position + hand.forward * rayDistance);
            pointer.startColor = pointer.endColor = point || itemTarget || RayNpc ? new Color(.3f, 1f, .8f) : new Color(.7f, .9f, 1, .65f);
        }
        void LateUpdate()
        {
            // All NPC Update calls have settled the final clipped frame before freezing.
            if (begun && !Round.Frozen && gameTime >= CareRound.Cutoff)
            {
                Round.Freeze();
                foreach (var npc in monkeys) npc.StopAtCutoff();
                if (held) held.Release(); held = null; StopHaptics();
                roundView.ShowResults(); SaveRoundRecord();
            }
            if (begun && roundView != null) roundView.Update(Round, gameTime);
            audioFeedback.UpdateMusic(Round.Frozen);
            if (Playing && held && held.Held && !held.Caring) SetHaptics(hapticAmplitude);
            else StopHaptics();
        }
        void ReadInput(out bool trigger, out bool grip)
        {
#if UNITY_EDITOR
            if (recordingHand)
            {
                trigger = recordingTrigger;
                grip = recordingGrip;
                return;
            }
#endif
            trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch) > .55f;
            grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) > .55f;
            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool t)) trigger |= t;
                if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool g)) grip |= g;
            }
            if (desktopPreview && !device.isValid && !OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
            {
                trigger |= Mouse.current != null && Mouse.current.leftButton.isPressed;
                grip |= Mouse.current != null && Mouse.current.rightButton.isPressed;
            }
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
            Round = new CareRound(monkeys.Select(n => n.firstNeed).ToArray()); records.Clear(); gameTime = 0; supplyEnabled = false;
            rig.transform.SetPositionAndRotation(rigStartPosition, rigStartRotation);
            roundView.Reset();
            foreach (var item in items) item.RestoreSupply();
            foreach (var npc in monkeys) npc.ResetForRound();
            readyCanvas = CareUi.Canvas("Care READY GO", playerCamera.transform, new Vector3(0, 0, 1.8f), 1, new Vector2(800, 470));
            CareUi.Image("READY GO", readyCanvas, readyGo, new Vector2(800, 470), Vector2.zero);
            CareReadyView.Decorate(readyCanvas, roundArtwork.star, readySeconds);
            suppressUntilRelease = true; arcAiming = false;
        }
        [System.Serializable]
        sealed class RoundReport
        {
            public string version = "1.0";
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
                arcAiming = arcValid = false; suppressUntilRelease = true; StopHaptics();
                if (highlighted) highlighted.Highlight(false);
            }
        }
        void OnDisable()
        {
            audioFeedback.Stop();
            StopHaptics(); if (held) held.Release();
            if (locomotion) locomotion.externalRightHandControl = false;
            if (Instance == this) Instance = null;
        }
    }
}
