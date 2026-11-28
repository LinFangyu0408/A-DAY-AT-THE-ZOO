#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC.Editor
{
    [DefaultExecutionOrder(10000)]
    public sealed class ZooDayTwoValidation : MonoBehaviour
    {
        static readonly string Output = Path.GetFullPath("../.menu-tools/day2-validation");
        readonly List<string> checks = new();
        readonly Dictionary<string, int?> saved = new();
        Camera captureCamera;
        RenderTexture target;
        Texture2D pixels;
        int frame;
        bool record, finishing;
        double deadline;
        public static void Preview() { Launch(false); }
        public static void Full() { Launch(true); }
        static void Launch(bool full)
        {
            Directory.CreateDirectory(Output); SessionState.SetBool("ZooDay2.Validate", true); SessionState.SetBool("ZooDay2.Full", full);
            EditorSceneManager.OpenScene("Assets/ZooPCMenu/Scenes/PC_MainMenu.unity"); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("ZooDay2.Validate", false))
                { SessionState.SetBool("ZooDay2.Validate", false); new GameObject("Day 02 validation").AddComponent<ZooDayTwoValidation>(); }
            };
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject); Application.runInBackground = true; deadline = EditorApplication.timeSinceStartup + 1200;
            foreach (var key in new[] { "ZooPC.Day1.Completed", "ZooPC.Day1.Stars", "ZooPC.Day1.Feedings", "ZooPC.Day1.Scratches", "ZooPC.Day2.Completed", "ZooPC.Day2.Stars", "ZooPC.Day2.Feedings", "ZooPC.Day2.Scratches", "ZooPC.Collection.back-scratcher", "ZooPC.ReducedMotion" })
            { saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null; PlayerPrefs.DeleteKey(key); }
            Application.logMessageReceived += Error;
            Time.captureFramerate = 30;
            captureCamera = new GameObject("Day 02 capture camera", typeof(Camera)).GetComponent<Camera>(); captureCamera.transform.SetParent(transform);
            captureCamera.enabled = false; captureCamera.transform.position = new Vector3(0, 0, -10); captureCamera.cullingMask = 1 << 29;
            captureCamera.clearFlags = CameraClearFlags.SolidColor; captureCamera.backgroundColor = ZooMenuUI.Ink; captureCamera.nearClipPlane = .01f;
            target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 }; target.Create(); pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            ModelChecks(); yield return Frames(40);
            var flow = ZooPCFlow.Instance;
            Require(flow && flow.HasDayTwo, "Day 02 is configured without replacing Day 01");
            record = SessionState.GetBool("ZooDay2.Full", false); if (record) Directory.CreateDirectory(Output + "/frames");
            Click("Start Game"); yield return Frames(40); Require(flow.Journal.Day == 0, "Start still opens Day 01");
            Click("Next day"); yield return Frames(50); Snapshot("01-day2-journal"); yield return Frames(50);
            Click("Play Day 1"); yield return WaitLoad(); yield return Frames(50);
            var session = FindFirstObjectByType<ZooDayTwoSession>();
            Require(session && session.Round != null && flow.ActiveDay == 2, "Journal loads the dedicated Day 02 scene");
            Require(!FindObjectsByType<MonkeyCareSession>(FindObjectsSortMode.None).Any(s => s.isActiveAndEnabled), "Day 02 runs without the Day 01 session");
            Require(session.bananaIcon && session.scratcherIcon && session.monkeys.Length == 5, "Tool artwork and all five animals are bound");
            Snapshot("02-day2-briefing"); yield return Frames(80);
            Click("Begin Day 02 shift"); yield return Frames(50);
            Require(session.Round.Started && ZooPCFlow.CollectionUnlocked("back-scratcher"), "Beginning the shift discovers the reusable care tool");
            var stationaryPositions = session.monkeys.Where(n => !n.roams).Select(n => n.transform.position).ToArray();
            yield return Frames(110); Snapshot("02b-five-monkeys-strolling");
            Require(session.monkeys.Count(n => n.roams) == 3 && session.monkeys.Where(n => n.roams).All(n => n.DistanceWalked > .5f), "Three monkeys actually stroll, at gentle walking speeds");
            Require(session.monkeys.Where(n => !n.roams).Select(n => n.transform.position).SequenceEqual(stationaryPositions), "Two resting monkeys remain in their clearings");
            Require(session.monkeys.Where(n => n.roams).All(n => n.walkSpeed >= .4f && n.walkSpeed <= .6f), "Walking speed stays between 0.4 and 0.6 metres per second");
            var first = session.monkeys.First(n => session.Round.animals[n.index].state == ZooCareState.Waiting);
            yield return Approach(session, first, 7); Aim(session, first); yield return Frames(35); Snapshot("03-hunger-request");
            Debug.Log("[Day 02 aim probe] " + session.playerCamera.transform.position + " -> " + first.AimPoint + " target=" + session.Target + " ray hits=" + string.Join("; ", Physics.RaycastAll(session.playerCamera.transform.position, (first.AimPoint-session.playerCamera.transform.position).normalized, Vector3.Distance(session.playerCamera.transform.position,first.AimPoint),~0,QueryTriggerInteraction.Ignore).Select(h=>h.collider.name+" parent="+h.collider.transform.parent?.name+" distance="+h.distance+" npc="+h.collider.GetComponentInParent<ZooDayTwoNpc>())));
            int beforeBananas = session.Round.Bananas;
            Require(session.TryThrow(first), "Actual aimed banana throw launched at a roaming monkey");
            Require(session.Round.Bananas == beforeBananas - 1, "Throw consumes exactly one banana");
            yield return Frames(150);
            Require(session.Round.Feedings == 1, "Flight arrival and eating animation resolve a real feeding"); Snapshot("04-feeding-success");
            var itchy = session.monkeys.First(n => session.Round.animals[n.index].state == ZooCareState.Waiting && session.Round.animals[n.index].need == CareNeed.Itch);
            session.SelectTool(1); Aim(session, itchy);
            Require(!session.TryScratch(itchy), "Scratching is rejected outside close range");
            yield return Approach(session, itchy, 2.2f); Aim(session, itchy); yield return Frames(20); Snapshot("05-close-scratch");
            int beforeStrokes = session.Round.animals[itchy.index].strokes;
            Require(session.TryScratch(itchy), "Nearby scratch animation starts");
            Require(session.Round.animals[itchy.index].strokes == beforeStrokes, "Starting a swing does not award a stroke before contact");
            session.SelectTool(0); yield return Frames(25);
            Require(session.Round.animals[itchy.index].strokes == beforeStrokes && !session.ScratchInProgress, "Switching tools cancels a swing without awarding care");
            session.SelectTool(1); Require(session.TryScratch(itchy), "First nearby scratch stroke accepted");
            Require(!session.TryScratch(itchy), "Repeated stroke in the same frame is rejected"); yield return Frames(10);
            Require(session.ConfirmedScratchContacts == 1 && session.LastScratchContactDistance < .015f, "Central claw tip contacts the animated body within 15 millimetres");
            Require(session.Round.animals[itchy.index].strokes == beforeStrokes + 1, "A stroke is awarded only after visible mesh contact");
            Snapshot("05b-claw-touching-body"); yield return Frames(12);
            Require(session.TryScratch(itchy), "Second separate stroke accepted"); yield return Frames(22);
            Require(session.TryScratch(itchy), "Third separate stroke accepted"); yield return Frames(25);
            Require(session.Round.Scratches == 1 && session.Round.Completed == 2, "Scratching and feeding both increase the care score"); Snapshot("06-scratch-success");
            var pausedPositions = session.monkeys.Select(n => n.transform.position).ToArray();
            flow.ShowPause(); float elapsed = session.Round.Elapsed; yield return Frames(40);
            Require(Mathf.Approximately(elapsed, session.Round.Elapsed) && !session.TryThrow(first), "Pause freezes the shift and blocks care actions"); Require(session.monkeys.Select(n => n.transform.position).SequenceEqual(pausedPositions), "Pause also freezes all wandering monkeys"); Snapshot("07-day2-pause");
            flow.Resume(); yield return Frames(15);
            session.SelectTool(0); Require(!session.TryRestock(), "Restocking is rejected away from the stand");
            yield return WalkTo(session, session.supply.position, 2.7f); session.playerCamera.transform.LookAt(session.supply.position + Vector3.up * 1.3f);
            Require(session.TryRestock() && session.Round.Bananas == session.rules.bananaCapacity, "Walking to the stand refills the bag"); Snapshot("08-restock"); yield return Frames(35);
            if (!SessionState.GetBool("ZooDay2.Full", false)) { Finish(); yield break; }
            int safety = 0;
            while (!session.Round.Finished && safety++ < 100)
            {
                var pending = session.monkeys.Where(n => session.Round.animals[n.index].state == ZooCareState.Waiting).OrderBy(n => session.Round.animals[n.index].requestedAt).FirstOrDefault();
                if (!pending) { yield return Frames(15); continue; }
                var animal = session.Round.animals[pending.index];
                float range = animal.need == CareNeed.Itch ? 2.2f : 7;
                yield return Approach(session, pending, range); Aim(session, pending); yield return Frames(5);
                if (animal.need == CareNeed.Hunger)
                {
                    session.SelectTool(0); if (session.Round.Bananas == 0) { yield return WalkTo(session, session.supply.position, 2.7f); session.TryRestock(); yield return Approach(session, pending, 7); Aim(session, pending); }
                    session.TryThrow(pending); yield return Frames(140);
                }
                else
                {
                    session.SelectTool(1);
                    for (int i = 0; i < 4 && animal.state == ZooCareState.Waiting && !session.Round.Finished; i++) { session.TryScratch(pending); yield return Frames(22); }
                }
            }
            while (!session.Round.Finished) yield return Frames(10);
            yield return Frames(45); Snapshot("09-day2-results");
            Require(ZooPCFlow.DayIsCompleted(2), "Day 02 result saved independently");
            Require(!ZooPCFlow.DayIsCompleted(1), "Day 02 does not overwrite Day 01 progress");
            Require(session.Round.Feedings == 10 && session.Round.Scratches == 10 && session.Round.Stars == 3, "Real playthrough completes all twenty tasks for three stars");
            Require(session.ConfirmedScratchContacts >= 30, "All ten scratching tasks are resolved by physical claw contact");
            Require(session.monkeys.Where(n => n.roams).All(n => Vector3.Distance(n.Home, n.transform.position) < 1.7f), "Wandering monkeys stay inside their nearby patrol areas");
            Click("Day 02 retry"); yield return WaitLoad(); yield return Frames(35);
            session = FindFirstObjectByType<ZooDayTwoSession>(); Require(session && !session.Round.Started && session.Round.Completed == 0 && session.Round.Bananas == session.rules.bananaCapacity, "Retry resets Day 02 without loading Day 01");
            flow.ReturnToJournal(); yield return WaitLoad(); yield return Frames(45);
            Require(flow.Journal.Day == 1, "Return opens the Day 02 journal entry"); Snapshot("10-day2-record");
            Click("Facilities tab"); yield return Frames(55); Require(flow.Journal.DiscoveredCount(false) == 2, "Facilities reveals the discovered tool and keeps the other slots locked"); Snapshot("11-tool-discovered"); yield return Frames(70);
            flow.SelectSection(0); yield return Frames(45); flow.TurnDay(-1); yield return Frames(45); Click("Play Day 1"); yield return WaitLoad(); yield return Frames(70);
            Require(flow.ActiveDay == 1 && MonkeyCareSession.Instance, "Original PC Day 01 still loads from the book");
            flow.ReturnToJournal(); yield return WaitLoad(); yield return Frames(40); Require(flow.Journal.Day == 0, "Day 01 return route remains on Day 01");
            Finish();
        }
        void ModelChecks()
        {
            var rules = ScriptableObject.CreateInstance<ZooDayTwoRules>(); var round = new ZooDayTwoRound(rules);
            round.Tick(20); Require(round.Elapsed == 0 && round.ActiveCount == 0, "Briefing time never consumes shift time");
            round.Start(); round.Tick(1); Require(round.ActiveCount == 1, "Shift opens with one request");
            round.Tick(44); Require(round.ActiveCount == 2, "Busy phase allows two simultaneous requests");
            round.Tick(1); Require(round.ActiveCount == 2, "Concurrent requests never exceed two");
            Require(round.animals[0].late && round.FeedGoal(round.animals[0]) == 4.5f, "Late feeding remains recoverable with one extra second");
            var perfect = new ZooDayTwoRound(rules); perfect.Start();
            for (int step = 0; step < 1200 && !perfect.Finished; step++)
            {
                perfect.Tick(.1f);
                for (int i = 0; i < perfect.animals.Length; i++)
                {
                    var a = perfect.animals[i]; if (a.state != ZooCareState.Waiting) continue;
                    if (a.need == CareNeed.Hunger) { if (perfect.Bananas == 0) perfect.Refill(); perfect.SpendBanana(); perfect.ReceiveBanana(i, a.completed); }
                    else perfect.Stroke(i);
                }
            }
            Require(perfect.Feedings == 10 && perfect.Scratches == 10 && perfect.Stars == 3, "All twenty mixed tasks produce three stars");
            Require(!perfect.SpendBanana() && !perfect.Refill() && !perfect.Stroke(0), "Finished shifts reject inventory and care mutations");
            var timeout = new ZooDayTwoRound(rules); timeout.Start(); timeout.Tick(rules.duration + 1);
            Require(timeout.Finished && timeout.Elapsed == rules.duration && timeout.Stars == 0, "Untended shift ends at the deadline with zero stars");
            Destroy(rules);
        }
        static void Aim(ZooDayTwoSession session, ZooDayTwoNpc npc)
        {
            Vector3 offset = npc.AimPoint - session.player.transform.position; offset.y = 0;
            session.player.transform.rotation = Quaternion.LookRotation(offset);
            session.playerCamera.transform.LookAt(npc.AimPoint);
        }
        IEnumerator Approach(ZooDayTwoSession session, ZooDayTwoNpc npc, float range)
        {
            var controller = session.player.GetComponent<CharacterController>();
            for (int frames = 0; frames < 500 && !session.Round.Finished; frames++)
            {
                Vector3 direction = npc.transform.position - session.player.transform.position; direction.y = 0;
                if (direction.magnitude <= range && session.ClearPath(session.playerCamera.transform.position, npc.AimPoint, npc)) yield break;
                if (direction.magnitude <= range) direction = Vector3.Cross(Vector3.up, direction);
                direction = AvoidObstacle(session, direction);
                session.player.transform.rotation = Quaternion.LookRotation(direction);
                session.playerCamera.transform.localRotation = Quaternion.Euler(5, 0, 0);
                controller.Move(direction.normalized * (4.5f / 30)); yield return null;
            }
            if (!session.Round.Finished) throw new Exception("Unable to approach moving monkey: " + npc.name);
        }
        IEnumerator WalkTo(ZooDayTwoSession session, Vector3 destination, float range)
        {
            var controller = session.player.GetComponent<CharacterController>(); int frames = 0;
            while (!session.Round.Finished && ++frames < 400)
            {
                Vector3 direction = destination - session.player.transform.position; direction.y = 0;
                if (direction.magnitude <= range) yield break;
                direction = AvoidObstacle(session, direction);
                session.player.transform.rotation = Quaternion.LookRotation(direction); session.playerCamera.transform.localRotation = Quaternion.Euler(5, 0, 0);
                controller.Move(direction.normalized * (4.5f / 30)); yield return null;
            }
            if (!session.Round.Finished) throw new Exception("Unable to walk to care target: " + destination);
        }
        static Vector3 AvoidObstacle(ZooDayTwoSession session, Vector3 direction)
        {
            foreach (float angle in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f })
            {
                var candidate = Quaternion.Euler(0, angle, 0) * direction.normalized;
                bool blocked = Physics.SphereCastAll(session.player.transform.position + Vector3.up * .85f, .32f, candidate, .65f, ~((1 << 2) | (1 << 5)), QueryTriggerInteraction.Ignore)
                    .Any(h => !h.transform.IsChildOf(session.player.transform) && h.normal.y < .65f);
                if (!blocked) return candidate;
            }
            return -direction.normalized;
        }
        IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        IEnumerator WaitLoad() { int count = 0; do { yield return null; if (++count > 18000) throw new Exception("Day 02 scene load timed out"); } while (ZooPCFlow.Instance.IsLoading); }
        void Require(bool valid, string message) { if (!valid) throw new Exception("DAY 02 VALIDATION FAILED: " + message); checks.Add("PASS: " + message); Debug.Log("[Zoo Day 2 Validation] PASS: " + message); }
        void Click(string name)
        {
            var b = FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name && b.isActiveAndEnabled);
            Require(b && b.interactable, "Button available: " + name); Canvas.ForceUpdateCanvases(); var r = (RectTransform)b.transform;
            var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == b, "Mouse ray reaches: " + name);
            ExecuteEvents.Execute(b.gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        void Snapshot(string name) { Capture(); File.WriteAllBytes(Output + "/" + name + ".png", pixels.EncodeToPNG()); }
        void LateUpdate() { if (record) { Capture(); File.WriteAllBytes(Output + "/frames/" + (frame++).ToString("D5") + ".jpg", pixels.EncodeToJPG(88)); } }
        void Capture()
        {
            var flow = ZooPCFlow.Instance; if (!flow) return;
            if (flow.Journal.BookCamera && flow.Journal.BookCamera.gameObject.activeInHierarchy) flow.Journal.BookCamera.Render();
            var session = FindFirstObjectByType<ZooDayTwoSession>();
            var camera = session ? session.playerCamera : flow.InGameplay && MonkeyCareSession.PlayerCamera ? MonkeyCareSession.PlayerCamera : captureCamera;
            var old = camera.targetTexture; camera.targetTexture = target;
            var states = new List<(Canvas c, RenderMode mode, Camera camera, float distance, float scale)>();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                states.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance, canvas.scaleFactor));
                Layer(canvas.gameObject, 29); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .2f;
                var scaler = canvas.GetComponent<CanvasScaler>(); if (scaler) canvas.scaleFactor = Mathf.Min(1600 / scaler.referenceResolution.x, 900 / scaler.referenceResolution.y);
            }
            foreach (var label in FindObjectsByType<Text>(FindObjectsSortMode.None)) label.SetAllDirty(); Canvas.ForceUpdateCanvases(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply(false); RenderTexture.active = previous;
            foreach (var s in states) { s.c.renderMode = s.mode; s.c.worldCamera = s.camera; s.c.planeDistance = s.distance; s.c.scaleFactor = s.scale; } camera.targetTexture = old;
        }
        static void Layer(GameObject go, int layer) { go.layer = layer; foreach (Transform child in go.transform) Layer(child.gameObject, layer); }
        void RestorePrefs() { foreach (var item in saved) { if (item.Value.HasValue) PlayerPrefs.SetInt(item.Key, item.Value.Value); else PlayerPrefs.DeleteKey(item.Key); } PlayerPrefs.Save(); Time.captureFramerate = 0; Time.timeScale = 1; AudioListener.pause = false; }
        void Finish()
        {
            finishing = true; record = false; RestorePrefs(); Application.logMessageReceived -= Error;
            File.WriteAllLines(Output + "/validation.txt", checks); File.WriteAllText(Output + "/frame-count.txt", frame.ToString());
            Debug.Log("[Zoo Day 2 Validation] COMPLETE " + checks.Count + " checks, " + frame + " frames."); EditorApplication.Exit(0);
        }
        void Error(string message, string stack, LogType type)
        {
            if (finishing || type != LogType.Exception) return; finishing = true; record = false;
            File.WriteAllLines(Output + "/failed-checks.txt", checks); File.WriteAllText(Output + "/failure.txt", message + "\n" + stack); RestorePrefs(); EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
        void Update() { if (!finishing && deadline > 0 && EditorApplication.timeSinceStartup > deadline) { Error("Validation timeout", "", LogType.Exception); } }
    }
}
#endif
