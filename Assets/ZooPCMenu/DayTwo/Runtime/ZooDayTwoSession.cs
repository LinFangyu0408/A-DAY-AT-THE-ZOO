using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC
{
    [DefaultExecutionOrder(-200)]
    public sealed partial class ZooDayTwoSession : MonoBehaviour
    {
        public ZooDayTwoRules rules;
        public ZooDayTwoNavigation navigation;
        public ZooDayTwoNpc[] monkeys;
        public DesktopPlayerController player;
        public Camera playerCamera;
        public Transform supply;
        public GameObject bananaPrefab, scratcherPrefab;
        public Texture2D bananaIcon, scratcherIcon;
        public ZooDayTwoRound Round { get; private set; }
        public int SelectedTool { get; private set; }
        public ZooDayTwoNpc Target { get; private set; }
        public bool IsRunning => Round != null && Round.Started && !Round.Finished && !(ZooPCFlow.Instance && ZooPCFlow.Instance.Paused);
        Transform heldRoot;
        GameObject bananaView, scratcherView;
        AudioSource music;
        readonly Dictionary<string, AudioClip> sounds = new();
        readonly List<BananaFlight> flights = new();
        Vector3 yardCenter;
        float yardRadius, nextThrow, inputArmedAt;
        bool resultsShown;
        string feedback = "";
        float feedbackUntil;
        sealed class BananaFlight { public GameObject model; public Vector3 origin, destination; public float age, duration; public int monkey = -1, task; }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            if (!ZooPCFlow.Instance)
            {
                new GameObject("Zoo PC navigation").AddComponent<ZooPCFlow>();
                yield return null;
                ZooPCFlow.Instance.EnterGameplay();
            }
            Round = new ZooDayTwoRound(rules);
            foreach (var npc in monkeys) npc.Initialize(this, System.Array.IndexOf(monkeys, npc));
            yardCenter = player.transform.position;
            yardRadius = 12;
            foreach (var npc in monkeys) yardRadius = Mathf.Max(yardRadius, Vector3.Distance(yardCenter, npc.transform.position) + 2.5f);
            yardRadius = Mathf.Max(yardRadius, Vector3.Distance(yardCenter, supply.position) + 3);
            BuildViewModels(); BuildInterface(); BuildTargetMarker();
            music = gameObject.AddComponent<AudioSource>(); music.playOnAwake = false; music.volume = .35f;
            music.clip = Resources.Load<AudioClip>("HighlandLakeAudio/music_care"); music.loop = true;
            foreach (string name in new[] { "feedSuccess", "scratchSuccess", "stroke", "bananaGrab", "scratchGrab", "bite", "jingle_victory_sting" })
                sounds[name] = Resources.Load<AudioClip>("HighlandLakeAudio/" + name);
            player.inputLocked = true;
        }

        public void StartShift()
        {
            if (Round == null || !Round.Start()) return;
            briefing.gameObject.SetActive(false); hud.gameObject.SetActive(true);
            ZooPCFlow.UnlockCollection("back-scratcher");
            player.inputLocked = false; inputArmedAt = Time.unscaledTime + .35f;
            EventSystem.current?.SetSelectedGameObject(null);
            if (music.clip) music.Play();
            Notify("NEW TOOL DISCOVERED  •  BACK SCRATCHER", 4);
        }

        void Update()
        {
            if (Round == null) return;
            bool paused = ZooPCFlow.Instance && (ZooPCFlow.Instance.Paused || ZooPCFlow.Instance.IsLoading);
            player.inputLocked = paused || !Round.Started || Round.Finished;
            if (paused) return;
            var k = Keyboard.current; var mouse = Mouse.current; var pad = Gamepad.current;
            if (!Round.Started)
            {
                if ((k != null && k.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame)) StartShift();
                return;
            }
            if (!Round.Finished)
            {
                Round.Tick(Time.deltaTime);
                UpdateFlights(Time.deltaTime);
                Target = AimTarget(playerCamera.transform.position, playerCamera.transform.forward);
                if (Time.unscaledTime >= inputArmedAt)
                {
                    if ((k != null && k.digit1Key.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) SelectTool(0);
                    if ((k != null && k.digit2Key.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) SelectTool(1);
                    if ((k != null && k.eKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame)) TryRestock();
                    bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
                    bool use = !overUi && ((mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame));
                    if (use) { if (SelectedTool == 0) TryThrow(Target); else TryScratch(Target); }
                }
                ClampToYard();
            }
            foreach (var npc in monkeys) npc.Refresh();
            UpdateInterface(); UpdateViewModels();
            if (Round.Finished && !resultsShown) FinishShift();
        }

        public void SelectTool(int tool)
        {
            if (!IsRunning) return;
            SelectedTool = Mathf.Clamp(tool, 0, 1);
            if (SelectedTool != 1) CancelScratch();
            PlayCareSound(SelectedTool == 0 ? "bananaGrab" : "scratchGrab", playerCamera.transform.position);
            EventSystem.current?.SetSelectedGameObject(null);
        }
        public bool TryRestock()
        {
            if (!IsRunning) return false;
            if (HorizontalDistance(player.transform.position, supply.position) > 3.4f) { Notify("MOVE CLOSER TO THE BANANA STAND", 2); return false; }
            if (!Round.Refill()) { Notify("YOUR BANANA BAG IS FULL", 2); return false; }
            PlayCareSound("bananaGrab", supply.position); Notify("RESTOCKED  •  " + rules.bananaCapacity + " BANANAS", 2); return true;
        }
        public bool TryThrow(ZooDayTwoNpc target)
        {
            if (!IsRunning || SelectedTool != 0 || Time.time < nextThrow) return false;
            if (!Round.SpendBanana()) { Notify("BAG EMPTY  •  RESTOCK AT THE STAND", 2); return false; }
            nextThrow = Time.time + .55f;
            Vector3 from = bananaView.transform.position;
            bool valid = target && Round.animals[target.index].state == ZooCareState.Waiting && Round.animals[target.index].need == CareNeed.Hunger &&
                Vector3.Distance(playerCamera.transform.position, target.AimPoint) <= rules.throwReach && ClearPath(playerCamera.transform.position, target.AimPoint, target);
            Vector3 destination = valid ? target.AimPoint : playerCamera.transform.position + playerCamera.transform.forward * 7 - Vector3.up * .9f;
            if (valid) target.HoldForCare(1.5f);
            var model = Instantiate(bananaPrefab, from, bananaView.transform.rotation);
            float size = .3f; FitModel(model, size);
            flights.Add(new BananaFlight { model = model, origin = from, destination = destination, duration = Mathf.Clamp(Vector3.Distance(from, destination) / 10, .35f, 1), monkey = valid ? target.index : -1, task = valid ? Round.animals[target.index].completed : -1 });
            if (target && !valid) Notify("THIS MONKEY NEEDS THE OTHER TOOL", 1.6f);
            return true;
        }
        public bool TryScratch(ZooDayTwoNpc target)
        {
            if (!IsRunning || SelectedTool != 1 || !target || ScratchInProgress) return false;
            if (Vector3.Distance(playerCamera.transform.position, target.AimPoint) > rules.scratchReach) { Notify("MOVE CLOSER TO SCRATCH", 1.4f); return false; }
            if (!ClearPath(playerCamera.transform.position, target.AimPoint, target)) return false;
            var animal = Round.animals[target.index];
            if (animal.state != ZooCareState.Waiting || animal.need != CareNeed.Itch) return false;
            return BeginScratch(target);
        }
        public ZooDayTwoNpc AimTarget(Vector3 origin, Vector3 direction)
        {
            // NavMesh agents move between physics steps. Query their current
            // body positions rather than the preceding physics snapshot.
            Physics.SyncTransforms();
            ZooDayTwoNpc best = null; float angle = 9;
            foreach (var npc in monkeys)
            {
                Vector3 to = npc.AimPoint - origin;
                float candidate = Vector3.Angle(direction, to);
                if (to.magnitude <= rules.throwReach && candidate < angle && ClearPath(origin, npc.AimPoint, npc)) { best = npc; angle = candidate; }
            }
            return best;
        }
        public bool ClearPath(Vector3 from, Vector3 to, ZooDayTwoNpc npc)
        {
            foreach (var hit in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to) - .15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance < .65f || hit.transform.IsChildOf(player.transform) || hit.collider.GetComponentInParent<ZooDayTwoNpc>() == npc) continue;
                return false;
            }
            return true;
        }
        void UpdateFlights(float delta)
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i]; f.age += delta; float t = Mathf.Clamp01(f.age / f.duration);
                if (f.monkey >= 0 && monkeys[f.monkey]) f.destination = monkeys[f.monkey].AimPoint;
                f.model.transform.position = Vector3.Lerp(f.origin, f.destination, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .7f);
                f.model.transform.Rotate(Vector3.right, delta * 280);
                if (t < 1) continue;
                if (f.monkey >= 0 && Round.ReceiveBanana(f.monkey, f.task)) PlayCareSound("bite", f.destination);
                else Notify("MISSED  •  AIM AT A HUNGRY MONKEY", 1.5f);
                Destroy(f.model); flights.RemoveAt(i);
            }
        }
        void BuildViewModels()
        {
            heldRoot = new GameObject("Day 02 held tools").transform; heldRoot.SetParent(playerCamera.transform, false);
            heldRoot.localPosition = new Vector3(.32f, -.28f, .63f);
            bananaView = Instantiate(bananaPrefab, heldRoot); bananaView.transform.localPosition = Vector3.zero; FitModel(bananaView, .21f);
            scratcherView = Instantiate(scratcherPrefab, heldRoot); scratcherView.transform.localPosition = Vector3.zero; FitModel(scratcherView, .48f);
            scratcherView.transform.localRotation = Quaternion.Euler(-15, 0, -30);
            scratchTip = scratcherView.transform.Find("Scratch contact tip");
            scratchRestScale = scratcherView.transform.localScale;
            bananaView.transform.localRotation = Quaternion.Euler(10, -35, -30);
            UpdateViewModels();
        }
        static void FitModel(GameObject model, float size)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(); if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            model.transform.localScale *= size / Mathf.Max(.001f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
        }
        void UpdateViewModels()
        {
            bool visible = Round != null && Round.Started && !Round.Finished;
            bananaView.SetActive(visible && SelectedTool == 0 && Round.Bananas > 0);
            scratcherView.SetActive(visible && SelectedTool == 1);
            if (!visible) CancelScratch();
        }
        void FinishShift()
        {
            resultsShown = true; player.inputLocked = true; music.Stop();
            CancelScratch();
            ZooPCFlow.SaveDay(2, Round.Stars, Round.Feedings, Round.Scratches);
            foreach (var f in flights) if (f.model) Destroy(f.model); flights.Clear();
            PlayCareSound("jingle_victory_sting", playerCamera.transform.position); ShowResults();
        }
        void ClampToYard()
        {
            if (navigation)
            {
                var bounds = navigation.playArea; var position = player.transform.position;
                Vector3 clamped = new Vector3(Mathf.Clamp(position.x, bounds.min.x, bounds.max.x), position.y, Mathf.Clamp(position.z, bounds.min.z, bounds.max.z));
                player.GetComponent<CharacterController>().Move(clamped - position);
                return;
            }
            Vector3 offset = player.transform.position - yardCenter; offset.y = 0;
            if (offset.magnitude <= yardRadius) return;
            var controller = player.GetComponent<CharacterController>();
            controller.Move(offset.normalized * (yardRadius - offset.magnitude));
        }
        public void PlayCareSound(string name, Vector3 position)
        { if (sounds.TryGetValue(name, out var clip) && clip) AudioSource.PlayClipAtPoint(clip, position, .65f); }
        void Notify(string message, float seconds) { feedback = message; feedbackUntil = Time.time + seconds; }
        static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
    }
}
