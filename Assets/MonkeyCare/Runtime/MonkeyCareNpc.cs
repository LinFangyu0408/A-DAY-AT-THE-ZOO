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
            panel = new CarePanel(transform, ui);
            statusText = CareRoundView.Label("Care Status", panel.Root, "", new Vector2(900, 70), new Vector2(0, -170), 36);
            statusText.color = Color.white; statusText.gameObject.AddComponent<Shadow>();
            emotion = CareUi.Canvas("Care Emotion", transform, Vector3.zero, ui.emotionSize, new Vector2(256, 256));
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
            waitUntil = MonkeyCareSession.GameTime + Mathf.Max(10, firstDelay);
            PlayPhase(MonkeyAction.Hi, wave);
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
        }
        public void EndGuidedTutorial()
        {
            if (!guidedTutorial) return;
            guidedTutorial = false;
            started = false;
            HasDemand = false;
            Celebrating = false;
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
            int tasksPerMonkey = MonkeyCareSession.Instance.Round.TasksPerMonkey;
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
                if (CurrentTaskIndex < tasksPerMonkey && MonkeyCareSession.TaskTime >= waitUntil && MonkeyCareSession.TaskTime < CareRound.Cutoff) BeginDemand(nextNeed);
                return;
            }
            float delta = Mathf.Max(0, MonkeyCareSession.TaskTime - appearedAt - Progress.Waited);
            if (Need == CareNeed.Hunger)
            {
                bool feeding = feedingFrame == Time.frameCount && feedingItem && feedingItem.Available;
                if (feeding)
                {
                    if (!smokeShown) { smokeEffect.Spawn(transform); smokeShown = true; }
                    PlayPhase(MonkeyAction.Eating, eating);
                    if (record.startedAt < 0)
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
            else Progress.Advance(delta, false);
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
            if (feeding)
            {
                if (!smokeShown) { smokeEffect.Spawn(transform); smokeShown = true; }
                PlayPhase(MonkeyAction.Eating, eating);
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
            Need = need; HasDemand = true; Celebrating = false; smokeShown = false; feedingFrame = -1;
            Progress = new CareProgress(patienceSeconds, feedingSeconds, lateFeedingSeconds, scratchCount, lateScratchCount);
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
            waitUntil = CareRound.NextTime(completedAt, CurrentTaskIndex, Progress.Late,
                nextNeed == CareNeed.Hunger ? hungerInterval : itchInterval, recoveryWait);
            HasDemand = false; Celebrating = true;
            item.Consume();
            if (demandFx) Destroy(demandFx);
            if (teleportPoint) teleportPoint.gameObject.SetActive(false);
            PlayPhase(Need == CareNeed.Hunger ? MonkeyAction.Happy : MonkeyAction.Cute1,
                Need == CareNeed.Hunger ? victory : cute);
            MonkeyCareSession.Instance.audioFeedback.Complete(transform, Need);
            // A success animation cannot wait for a state that it must itself finish.
            if (float.IsPositiveInfinity(animationLength)) animationLength = currentClip.length / Mathf.Max(.01f, currentAnimation.speed);
            RefreshUi();
        }
        void CompleteGuided(CareItem item)
        {
            if (!HasDemand) return;
            HasDemand = false;
            Celebrating = true;
            smokeShown = true;
            item.Consume();
            if (panel != null) panel.Root.gameObject.SetActive(false);
            if (emotion)
            {
                emotion.gameObject.SetActive(false);
            }
            PlayPhase(MonkeyAction.Happy, victory);
            MonkeyCareSession.Instance?.audioFeedback.Complete(transform, Need);
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
            statusText.text = Celebrating ? (Progress.Late ? "补救成功！下一次恢复正常要求" : "喂饱了！") :
                Progress.Late ? (Need == CareNeed.Hunger ? "补救：吃 " + feedingSeconds.ToString("0") + " → " + lateFeedingSeconds.ToString("0") + " 秒" : "补救：" + scratchCount + " → " + lateScratchCount + " 次") :
                remaining <= 10 ? "快一点！还剩 " + Mathf.CeilToInt(remaining) + " 秒" : "";
            statusText.color = !Celebrating && remaining <= 10 ? Color.Lerp(Color.white, new Color(1, .65f, .2f), .5f + .5f * Mathf.Sin(MonkeyCareSession.GameTime * 6)) : Color.white;
        }
        void LateUpdate()
        {
            if (panel == null) return;
            if (MonkeyCareSession.Instance && MonkeyCareSession.Instance.Round.Frozen) return;
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
            animator.CrossFadeInFixedTime("Base Layer." + phase, .12f, 0, 0);
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
            if (record == null || Progress == null) return;
            if (Progress.Late && !rescueRecorded)
            {
                rescueRecorded = true; consecutiveRescues++;
                record.rescueAt = appearedAt + patienceSeconds; record.consecutiveRescues = consecutiveRescues;
            }
            record.effectiveFeedingSeconds = Progress.Fed; record.effectiveStrokes = Progress.Strokes;
            if (record.startedAt >= 0) record.switchDelay = Mathf.Max(0, record.startedAt - appearedAt);
        }
        public void StopAtCutoff()
        {
            UpdateRecord(); HasDemand = false; Celebrating = false;
            if (demandFx) Destroy(demandFx);
            if (teleportPoint) teleportPoint.gameObject.SetActive(false);
            if (panel != null) panel.Root.gameObject.SetActive(false);
            if (emotion) emotion.gameObject.SetActive(false);
            if (animator) animator.speed = 0;
        }
        public void ResetForRound()
        {
            guidedTutorial = false;
            StopAtCutoff(); CurrentTaskIndex = 0; Progress = null; consecutiveRescues = 0; currentAnimation = null;
            started = false; BeginSession();
        }
        void OnDestroy()
        {
            if (animator && originalController) animator.runtimeAnimatorController = originalController;
            if (overrides) Destroy(overrides);
        }
    }
}


