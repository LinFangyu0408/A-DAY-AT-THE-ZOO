using System;
using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    [DisallowMultipleComponent]
    public sealed class MonkeyCareNpc : MonoBehaviour
    {
        [Header("需求时间")]
        [InspectorName("首次需求")] public CareNeed firstNeed;
        [InspectorName("首次延迟（秒）"), Min(0)] public float firstDelay = 15;
        [InspectorName("下一次饥饿等待（秒）"), Min(0)] public float hungerInterval = 18;
        [InspectorName("下一次发痒等待（秒）"), Min(0)] public float itchInterval = 18;
        [InspectorName("补救后额外等待（秒）"), Min(0)] public float recoveryWait = 4;
        [InspectorName("耐心时限（秒）"), Min(.01f)] public float patienceSeconds = 30;
        [InspectorName("首次耐心时限（秒）"), Min(.01f)] public float firstPatienceSeconds = 55;
        [Header("照顾要求")]
        [InspectorName("正常喂食（秒）"), Min(.01f)] public float feedingSeconds = 8;
        [InspectorName("超时喂食（秒）"), Min(.01f)] public float lateFeedingSeconds = 10;
        [InspectorName("正常挠痒次数"), Min(1)] public int scratchCount = 3;
        [InspectorName("超时挠痒次数"), Min(1)] public int lateScratchCount = 4;
        [InspectorName("下挠距离（米）"), Min(.01f)] public float strokeDistance = .08f;
        [InspectorName("抬回距离（米）"), Min(.01f)] public float resetDistance = .06f;
        [Header("动画")]
        public CareAnimation wave = new() { action = MonkeyAction.Hi };
        public CareAnimation hungry = new() { action = MonkeyAction.Hungry };
        public CareAnimation eating = new() { action = MonkeyAction.Eating };
        public CareAnimation scratching = new() { action = MonkeyAction.Scratching };
        public CareAnimation victory = new() { action = MonkeyAction.Happy, finish = CareFinish.Repeats, repeats = 3 };
        public CareAnimation cute = new() { action = MonkeyAction.Cute1, finish = CareFinish.Repeats, repeats = 2 };
        [Header("特效")]
        public CareEffect hungerEffect = new();
        public CareEffect itchEffect = new();
        public CareEffect smokeEffect = new() { duration = 2.5f, position = new Vector3(0, .6f, .5f) };
        [Header("UI")]
        public CareUiStyle ui = new();
        [Header("场景绑定")]
        public Animator animator;
        public MonkeyAnimationLibrary library;
        public CareTeleportPoint teleportPoint;

        public CareProgress Progress { get; private set; }
        public CareNeed Need { get; private set; }
        public bool HasDemand { get; private set; }
        public bool Celebrating { get; private set; }
        public int CurrentTaskIndex { get; private set; }
        public bool Accepts(CareNeed need) => HasDemand && Need == need;
        // True while a caught banana is being eaten. The care ray may still
        // touch the monkey during this window, but haptics must stay silent.
        public bool EatingNow { get; private set; }
        public MonkeyAction Phase { get; private set; }
        public float NextIn => Mathf.Max(0, waitUntil - MonkeyCareSession.GameTime);
        CarePanel panel;
        RectTransform emotion;
        Image emotionImage;
        Text statusText;
        CareTaskRecord record;
        float appearedAt;
        int consecutiveRescues;
        bool rescueRecorded;
        GameObject demandFx;
        AnimatorOverrideController overrides;
        RuntimeAnimatorController originalController;
        AnimationClip currentClip;
        CareAnimation currentAnimation;
        MonkeyFace face;
        float animationStarted, animationLength, waitUntil, shownSatisfaction, fillFrom, fillTarget, fillStarted;
        int feedingFrame = -1;
        CareItem feedingItem;
        bool started, smokeShown, guidedTutorial;
        CareNeed nextNeed;

        void Awake()
        {
            if (!animator) animator = GetComponent<Animator>();
            if (!animator || !library || !animator.runtimeAnimatorController)
            { Debug.LogError("[Monkey Care] 缺少动画绑定: " + name, this); enabled = false; return; }
            originalController = animator.runtimeAnimatorController;
            overrides = new AnimatorOverrideController(originalController);
            animator.runtimeAnimatorController = overrides;
            animator.applyRootMotion = false;
            face = GetComponent<MonkeyFace>();
            if (CareUiSkin.SvgStaminaPanel) ui.stamina = CareUiSkin.SvgStaminaPanel;
            if (CareUiSkin.SvgSatisfactionPanel) ui.satisfaction = CareUiSkin.SvgSatisfactionPanel;
            if (CareUiSkin.SvgStaminaFill) ui.staminaFill = CareUiSkin.SvgStaminaFill;
            if (CareUiSkin.SvgSatisfactionFill) ui.satisfactionFill = CareUiSkin.SvgSatisfactionFill;
            if (CareUiSkin.SvgStaminaLogo) ui.staminaLogo = CareUiSkin.SvgStaminaLogo;
            if (CareUiSkin.SvgSatisfactionLogo) ui.satisfactionLogo = CareUiSkin.SvgSatisfactionLogo;
            if (CareUiSkin.SvgSad) ui.sad = CareUiSkin.SvgSad;
            if (CareUiSkin.SvgHappy) ui.happy = CareUiSkin.SvgHappy;
            // Keep the hunger/stamina plate hugging the monkey's head: a plate
            // floating a metre higher drifted into the SCORE HUD.
            ui.width = Mathf.Clamp(ui.width, 1.0f, 1.1f);
            ui.barOffset.y = Mathf.Clamp(ui.barOffset.y, 1.25f, 1.45f);
            panel = new CarePanel(transform, ui);
            statusText = CareRoundView.Label("Care Status", panel.Root, "", new Vector2(900, 70), new Vector2(0, -170), 36);
            statusText.color = Color.white; statusText.gameObject.AddComponent<Shadow>();
            emotion = CareUi.Canvas("Care Emotion", transform, Vector3.zero, ui.emotionSize, new Vector2(256, 256), 61);
            emotionImage = CareUi.Image("Emotion", emotion, ui.sad, new Vector2(256, 256), Vector2.zero);
            emotion.gameObject.SetActive(false); panel.Root.gameObject.SetActive(false);
            if (teleportPoint) teleportPoint.gameObject.SetActive(false);
        }
        public void BeginSession()
        {
            if (!enabled || started) return;
            var round = MonkeyCareSession.Instance ? MonkeyCareSession.Instance.Round : null;
            started = true;
            nextNeed = round != null && round.FeedingOnly ? CareNeed.Hunger : firstNeed;
            // Hunger is handed out by the session's rotation, one monkey at a
            // time; until then this monkey simply waves at the player.
            waitUntil = float.PositiveInfinity;
            PlayPhase(MonkeyAction.Hi, wave);
        }
        // Called by the session rotation: become hungry once the clock passes
        // the scheduled moment.
        public void QueueDemand(float at)
        {
            if (!enabled || guidedTutorial) return;
            if (!started) { started = true; waitUntil = at; PlayPhase(MonkeyAction.Hi, wave); return; }
            waitUntil = at;
        }
        public bool TutorialSatisfied => guidedTutorial && Celebrating;
        public void BeginGuidedTutorial(CareNeed need)
        {
            if (!enabled || guidedTutorial) return;
            started = true;
            guidedTutorial = true;
            Need = need;
            HasDemand = true;
            Celebrating = false;
            smokeShown = false;
            feedingFrame = -1;
            Progress = new CareProgress(Mathf.Max(patienceSeconds, 60), feedingSeconds, lateFeedingSeconds,
                scratchCount, lateScratchCount);
            appearedAt = MonkeyCareSession.TaskTime;
            rescueRecorded = false;
            record = null;
            shownSatisfaction = fillFrom = fillTarget = 0;
            PlayPhase(need == CareNeed.Hunger ? MonkeyAction.Hungry : MonkeyAction.Scratching,
                need == CareNeed.Hunger ? hungry : scratching);
            if (teleportPoint) teleportPoint.gameObject.SetActive(false);
            panel.Root.gameObject.SetActive(true);
            RefreshUi();
            if (need == CareNeed.Hunger)
                MonkeyCareSession.Instance?.audioFeedback.Whistle(transform);
        }
        public void EndGuidedTutorial()
        {
            if (!guidedTutorial) return;
            guidedTutorial = false;
            started = false;
            HasDemand = false;
            Celebrating = false;
            EatingNow = false;
            Progress = null;
            smokeShown = false;
            feedingFrame = -1;
            feedingItem = null;
            if (panel != null) panel.Root.gameObject.SetActive(false);
            if (emotion) emotion.gameObject.SetActive(false);
            if (teleportPoint) teleportPoint.gameObject.SetActive(false);
            if (animator && enabled)
            {
                PlayPhase(MonkeyAction.Hi, wave);
            }
            face?.ClearReaction();
        }
        void Update()
        {
            if (!started || !MonkeyCareSession.Playing) return;
            bool animationFinished = UpdateAnimation();
            if (guidedTutorial)
            {
                UpdateGuidedTutorial(animationFinished);
                return;
            }
            if (MonkeyCareSession.Instance.Round.Frozen) return;
            int tasksPerMonkey = MonkeyCareSession.Instance.Round.TasksForMonkey(
                Array.IndexOf(MonkeyCareSession.Instance.monkeys, this));
            if (Celebrating)
            {
                if (animationFinished || (CurrentTaskIndex < tasksPerMonkey && MonkeyCareSession.TaskTime >= waitUntil))
                {
                    Celebrating = false; Progress = null;
                    panel.Root.gameObject.SetActive(false); emotion.gameObject.SetActive(false);
                    PlayPhase(MonkeyAction.Hi, wave);
                }
                return;
            }
            if (!HasDemand)
            {
                // Only the monkey that currently owns the hunger slot may ask
                // for food, so the three of them never starve at once.
                if (MonkeyCareSession.Instance.MayDemand(this) &&
                    CurrentTaskIndex < tasksPerMonkey && MonkeyCareSession.TaskTime >= waitUntil &&
                    MonkeyCareSession.TaskTime < CareRound.Cutoff) BeginDemand(nextNeed);
                return;
            }
            float delta = Mathf.Max(0, MonkeyCareSession.TaskTime - appearedAt - Progress.Waited);
            if (Need == CareNeed.Hunger)
            {
                bool feeding = feedingFrame == Time.frameCount && feedingItem && feedingItem.Available;
                EatingNow = feeding;
                if (feeding)
                {
                    if (!smokeShown) { smokeEffect.Spawn(transform); smokeShown = true; }
                    PlayPhase(MonkeyAction.Eating, eating);
                    // Eating is self-explanatory: hide the overhead satisfaction
                    // bar and emotion icon until the banana is finished.
                    if (panel.Root.gameObject.activeSelf) panel.Root.gameObject.SetActive(false);
                    if (emotion.gameObject.activeSelf) emotion.gameObject.SetActive(false);
                    if (record != null && record.startedAt < 0)
                    {
                        record.startedAt = MonkeyCareSession.TaskTime - delta;
                        MonkeyCareSession.Instance.audioFeedback.Bite(transform);
                    }
                    bool completed = Progress.Advance(delta, true);
                    UpdateRecord();
                    if (completed) Complete(feedingItem, appearedAt + Progress.Waited);
                }
                else { Progress.Advance(delta, false); PlayPhase(MonkeyAction.Hungry, hungry); }
            }
            else { EatingNow = false; Progress.Advance(delta, false); }
            UpdateRecord();
            RefreshUi();
        }
        void UpdateGuidedTutorial(bool animationFinished)
        {
            if (Celebrating)
            {
                if (animationFinished && panel != null) panel.Root.gameObject.SetActive(false);
                return;
            }
            if (!HasDemand || Progress == null) return;

            float delta = Mathf.Max(0, MonkeyCareSession.TaskTime - appearedAt - Progress.Waited);
            bool feeding = feedingFrame == Time.frameCount && feedingItem && feedingItem.Available;
            EatingNow = feeding;
            if (feeding)
            {
                if (!smokeShown) { smokeEffect.Spawn(transform); smokeShown = true; }
                PlayPhase(MonkeyAction.Eating, eating);
                if (panel.Root.gameObject.activeSelf) panel.Root.gameObject.SetActive(false);
                if (emotion.gameObject.activeSelf) emotion.gameObject.SetActive(false);
                if (Progress.Advance(delta, true)) CompleteGuided(feedingItem);
            }
            else
            {
                Progress.Advance(delta, false);
                PlayPhase(MonkeyAction.Hungry, hungry);
            }
            RefreshUi();
        }
        public void BeginDemand(CareNeed need)
        {
            if (MonkeyCareSession.Instance.Round.FeedingOnly) need = CareNeed.Hunger;
            Need = need; HasDemand = true; Celebrating = false; smokeShown = false; feedingFrame = -1; EatingNow = false;
            // The first monkey of a round starves slowly so the player can
            // learn the controls; later requests use the normal patience.
            float patience = CurrentTaskIndex == 0
                ? Mathf.Max(patienceSeconds, firstPatienceSeconds) : patienceSeconds;
            Progress = new CareProgress(patience, feedingSeconds, lateFeedingSeconds, scratchCount, lateScratchCount);
            appearedAt = MonkeyCareSession.TaskTime; rescueRecorded = false;
            record = MonkeyCareSession.Instance.RegisterTask(Array.IndexOf(MonkeyCareSession.Instance.monkeys, this), CurrentTaskIndex, need, appearedAt);
            shownSatisfaction = fillFrom = fillTarget = 0;
            PlayPhase(need == CareNeed.Hunger ? MonkeyAction.Hungry : MonkeyAction.Scratching,
                need == CareNeed.Hunger ? hungry : scratching);
            if (demandFx) Destroy(demandFx);
            // Temporarily disabled demand effects (StarBurst / StunExplosion).
            // demandFx = (need == CareNeed.Hunger ? hungerEffect : itchEffect).Spawn(transform);
            // Hide the wide wave in StunExplosion; retain the small demand stars.
            if (demandFx && need == CareNeed.Itch) foreach (var r in demandFx.GetComponentsInChildren<ParticleSystemRenderer>(true)) if (r.sharedMaterial && r.sharedMaterial.name == "wave2") r.enabled = false;
            if (teleportPoint) teleportPoint.gameObject.SetActive(true);
            panel.Root.gameObject.SetActive(true); RefreshUi();
            if (need == CareNeed.Hunger)
                MonkeyCareSession.Instance?.audioFeedback.Whistle(transform);
        }
        public void OfferFood(CareItem item)
        {
            if (!Accepts(CareNeed.Hunger) || !item || !item.Available) return;
            if (feedingFrame == Time.frameCount) return;
            feedingFrame = Time.frameCount; feedingItem = item;
        }
        public void AddStroke(CareItem item)
        {
            if (MonkeyCareSession.GameTime > CareRound.Cutoff) return;
            if (MonkeyCareSession.Instance.Round.FeedingOnly) return;
            if (!Accepts(CareNeed.Itch) || !item || !item.Held || !item.Available) return;
            Progress.Advance(Mathf.Max(0, MonkeyCareSession.TaskTime - appearedAt - Progress.Waited), false);
            if (record.startedAt < 0) record.startedAt = MonkeyCareSession.TaskTime;
            bool completed = Progress.Stroke(); UpdateRecord();
            if (completed) Complete(item, MonkeyCareSession.TaskTime);
            else MonkeyCareSession.Instance.audioFeedback.Stroke(transform, Progress.Strokes);
            RefreshUi();
        }
        void Complete(CareItem item, float completedAt)
        {
            if (!HasDemand) return;
            if (guidedTutorial)
            {
                CompleteGuided(item);
                return;
            }
            if (!MonkeyCareSession.Instance.CompleteTask(this, completedAt)) return;
            record.completedAt = completedAt;
            if (!Progress.Late) consecutiveRescues = 0;
            CurrentTaskIndex++;
            nextNeed = MonkeyCareSession.Instance.Round.FeedingOnly ? CareNeed.Hunger :
                Need == CareNeed.Hunger ? CareNeed.Itch : CareNeed.Hunger;
            // The session owns the schedule: it hands the next hunger request to
            // another monkey 3-6 seconds from now (see NotifyFed).
            waitUntil = float.PositiveInfinity;
            HasDemand = false; Celebrating = true; EatingNow = false;
            item.Consume();
            if (demandFx) Destroy(demandFx);
            if (teleportPoint) teleportPoint.gameObject.SetActive(false);
            Celebrate(Need);
            RefreshUi();
        }
        void CompleteGuided(CareItem item)
        {
            if (!HasDemand) return;
            HasDemand = false;
            Celebrating = true;
            smokeShown = true;
            item.Consume();
            Celebrate(CareNeed.Hunger);
            if (panel != null) panel.Root.gameObject.SetActive(true);
            RefreshUi();
        }
        void Celebrate(CareNeed completedNeed)
        {
            Celebrating = true;
            smokeShown = true;
            CareAnimation reaction = completedNeed == CareNeed.Hunger ? victory : cute;
            MonkeyAction action = completedNeed == CareNeed.Hunger ? MonkeyAction.Happy : MonkeyAction.Cute1;
            PlayPhase(action, reaction);
            if (float.IsPositiveInfinity(animationLength) && currentClip)
                animationLength = currentClip.length / Mathf.Max(.01f, reaction.speed);
            face?.ShowReaction(action, Mathf.Max(3.2f, float.IsPositiveInfinity(animationLength)
                ? 3.2f : animationLength));
            MonkeyCareSession.Instance?.audioFeedback.Complete(transform, completedNeed);
        }
        void RefreshUi()
        {
            if (Progress == null || panel == null) return;
            bool satisfied = Progress.Locked;
            float target = Need == CareNeed.Itch && satisfied ? (float)Progress.Strokes / Progress.StrokeTarget : Progress.Satisfaction(Need);
            if (Need == CareNeed.Itch && !Mathf.Approximately(fillTarget, target))
            { fillFrom = shownSatisfaction; fillTarget = target; fillStarted = MonkeyCareSession.GameTime; }
            shownSatisfaction = Need == CareNeed.Itch ? Mathf.Lerp(fillFrom, fillTarget, Mathf.Pow(Mathf.Clamp01((MonkeyCareSession.GameTime - fillStarted) / .2f), 2)) : target;
            if (Celebrating) shownSatisfaction = 1;
            int segments = Progress.StrokeTarget;
            panel.Show(satisfied, satisfied ? shownSatisfaction : Progress.Patience, satisfied ? segments : 1);
            bool showEmotion = Celebrating || Progress.Late;
            emotion.gameObject.SetActive(showEmotion); emotionImage.sprite = Celebrating ? ui.happy : ui.sad;
            float remaining = Progress.Deadline - Progress.Waited;
            statusText.text = Celebrating
                ? (Progress.Late ? "RESCUED! Normal speed next time." : "FED!")
                : Progress.Late
                    ? (Need == CareNeed.Hunger
                        ? "PATIENCE LOST -1: EAT " + feedingSeconds.ToString("0") +
                          " → " + lateFeedingSeconds.ToString("0") + " SEC"
                        : "PATIENCE LOST -1: SCRATCH " + scratchCount +
                          " → " + lateScratchCount + " TIMES")
                    : remaining <= 10
                        ? "HURRY! " + Mathf.CeilToInt(remaining) + " SEC"
                        : "";
            statusText.color = !Celebrating && remaining <= 10 ? Color.Lerp(Color.white, new Color(1, .65f, .2f), .5f + .5f * Mathf.Sin(MonkeyCareSession.GameTime * 6)) : Color.white;
        }
        void LateUpdate()
        {
            if (panel == null) return;
            var camera = MonkeyCareSession.PlayerCamera;
            if (!camera) return;
            var view = camera.transform;
            panel.Root.position = transform.position + Vector3.up * ui.barOffset.y + view.right * ui.barOffset.x + view.forward * ui.barOffset.z;
            panel.Root.rotation = view.rotation;
            Vector3 offset = Celebrating ? ui.happyOffset : ui.sadOffset;
            emotion.position = transform.position + view.right * offset.x + Vector3.up * offset.y + view.forward * offset.z;
            emotion.rotation = view.rotation;
            if (Progress != null && Need == CareNeed.Itch) RefreshUi();
        }
        void PlayPhase(MonkeyAction phase, CareAnimation setting)
        {
            if (currentAnimation == setting && Phase == phase) return;
            var clip = setting.clip ? setting.clip : library.GetClip(setting.action);
            var placeholder = library.GetClip(phase);
            if (!clip || !placeholder || !clip.isHumanMotion)
            { Debug.LogError("[Monkey Care] 无效Humanoid动作: " + phase, this); return; }
            Phase = phase; currentAnimation = setting; currentClip = clip;
            overrides[placeholder.name] = clip;
            animationLength = setting.Length(clip); animationStarted = MonkeyCareSession.GameTime;
            animator.speed = Mathf.Max(.01f, setting.speed);
            // Play the named state after replacing the override motion. Cross
            // fades can otherwise remain in Eating when a banana completes in
            // the same frame, hiding the Happy reaction from the player.
            animator.Play("Base Layer." + phase, 0, 0);
        }
        bool UpdateAnimation()
        {
            if (currentAnimation == null || !currentClip) return false;
            if (MonkeyCareSession.GameTime - animationStarted >= animationLength)
            {
                if (animator.speed != 0)
                { animator.Play("Base Layer." + Phase, 0, .9999f); animator.Update(0); animator.speed = 0; }
                return true;
            }
            if (!currentClip.isLooping && !animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1)
                animator.Play("Base Layer." + Phase, 0, 0);
            return false;
        }
        void OnDisable()
        {
            if (demandFx) Destroy(demandFx);
            if (teleportPoint) teleportPoint.gameObject.SetActive(false);
        }
        void UpdateRecord()
        {
            if (Progress == null) return;
            if (Progress.Late && !rescueRecorded)
            {
                rescueRecorded = true; consecutiveRescues++;
                MonkeyCareSession.Instance?.Round?.RegisterPatiencePenalty(
                    Array.IndexOf(MonkeyCareSession.Instance.monkeys, this), CurrentTaskIndex);
            }
            if (record == null) return;
            if (rescueRecorded)
            {
                record.rescueAt = appearedAt + patienceSeconds;
                record.consecutiveRescues = consecutiveRescues;
            }
            record.effectiveFeedingSeconds = Progress.Fed; record.effectiveStrokes = Progress.Strokes;
            if (record.startedAt >= 0) record.switchDelay = Mathf.Max(0, record.startedAt - appearedAt);
        }
        public void StopAtCutoff()
        {
            UpdateRecord(); HasDemand = false; Celebrating = false; EatingNow = false;
            if (demandFx) Destroy(demandFx);
            if (teleportPoint) teleportPoint.gameObject.SetActive(false);
            if (panel != null) panel.Root.gameObject.SetActive(false);
            if (emotion) emotion.gameObject.SetActive(false);
            if (animator) animator.speed = 0;
        }
        public void ResetForRound(bool beginImmediately = true)
        {
            guidedTutorial = false;
            StopAtCutoff(); CurrentTaskIndex = 0; Progress = null; consecutiveRescues = 0; currentAnimation = null;
            started = false;
            if (beginImmediately) BeginSession();
        }
        void OnDestroy()
        {
            if (animator && originalController) animator.runtimeAnimatorController = originalController;
            if (overrides) Destroy(overrides);
        }
    }
}


