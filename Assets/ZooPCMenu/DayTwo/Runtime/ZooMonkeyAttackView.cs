using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace ADayAtTheZoo.PC
{
    // A render-only copy preserves the culprit's skin while leaving its real
    // collider, navigation path and task in the clearing untouched.
    public sealed class ZooMonkeyAttackView : MonoBehaviour
    {
        public const float Duration = 4.2f;
        const float FirstHit = .84f, HitGap = .62f;
        const int HitCount = 5, StageLayer = 28;
        static readonly Vector3 Stage = new Vector3(6000, 300, 6000);
        public float Age { get; private set; }
        public int Hits { get; private set; }
        public float FlashStrength { get; private set; }
        public float ShakeStrength { get; private set; }
        public GameObject Actor => actor;
        public bool Restored { get; private set; }
        Camera view;
        GameObject stage, actor, overlay;
        Vector3 oldPosition;
        Quaternion oldRotation;
        float oldFov, oldFar, oldNear;
        int oldMask;
        CameraClearFlags oldClear;
        Color oldBackground;
        bool oldFog;
        PlayableGraph graph;
        AnimationClipPlayable attackClip, angryClip;
        AnimationMixerPlayable mixer;
        ZooMonkeyAttackAssets assets;
        AnimationClip activeClip;
        ZooAttackImpactGraphic impact;
        Text caption;
        AudioSource audioSource;
        AudioClip thump, swish;
        float lastHit = -100;
        Gamepad rumblePad;

        public void Begin(ZooDayTwoNpc culprit, Camera camera, Transform ui)
        {
            view = camera;
            oldPosition = view.transform.localPosition; oldRotation = view.transform.localRotation;
            oldFov = view.fieldOfView; oldFar = view.farClipPlane; oldNear = view.nearClipPlane;
            oldMask = view.cullingMask; oldClear = view.clearFlags; oldBackground = view.backgroundColor; oldFog = RenderSettings.fog;
            assets = Resources.Load<ZooMonkeyAttackAssets>("ZooPCMenu/DayTwoAttack");
            if (!assets || !assets.attack || !assets.angry) throw new InvalidOperationException("Prepare Day Two attack assets before playing.");
            stage = new GameObject("Day 02 attack close-up stage"); stage.SetActive(false); stage.transform.position = Stage;
            actor = Instantiate(culprit.gameObject, stage.transform);
            actor.name = "Close-up of " + culprit.name;
            foreach (var behaviour in actor.GetComponentsInChildren<Behaviour>(true))
                if (!(behaviour is Animator)) behaviour.enabled = false;
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in actor.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (var transform in actor.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = StageLayer;
            actor.transform.localPosition = Vector3.zero; actor.transform.localRotation = Quaternion.Euler(0, 180, 0);
            actor.transform.localScale = culprit.transform.lossyScale;
            var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var skin in skins)
            {
                skin.updateWhenOffscreen = true;
                var materials = skin.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] && materials[i].name.StartsWith("M_Chibi_Emo_") && assets.angryFace) materials[i] = assets.angryFace;
                skin.sharedMaterials = materials;
            }
            // Standardize framing without changing the in-world animal size.
            var bounds = skins[0].bounds; foreach (var skin in skins) bounds.Encapsulate(skin.bounds);
            actor.transform.localScale *= 2 / Mathf.Max(.1f, bounds.size.y);
            var animator = actor.GetComponent<Animator>(); animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Day 02 patience attack"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            activeClip = assets.attack;
            attackClip = AnimationClipPlayable.Create(graph, activeClip); angryClip = AnimationClipPlayable.Create(graph, assets.angry);
            attackClip.SetApplyFootIK(false); angryClip.SetApplyFootIK(false);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(angryClip, 0, mixer, 0); graph.Connect(attackClip, 0, mixer, 1);
            AnimationPlayableOutput.Create(graph, "Close-up pose", animator).SetSourcePlayable(mixer);
            graph.Play();
            AddLight("Warm key", new Vector3(25, 150, 0), new Color(1, .93f, .83f), .7f);
            AddLight("Soft fill", new Vector3(15, 25, 0), new Color(.72f, .84f, 1), .3f);
            stage.SetActive(true);
            view.cullingMask = (1 << StageLayer) | (1 << 29);
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.13f, .075f, .045f);
            view.nearClipPlane = .04f; view.farClipPlane = 15; RenderSettings.fog = false;
            overlay = ZooMenuUI.Group("Patience attack feedback", ui).gameObject;
            impact = ZooMenuUI.Group("Impact flash and speed lines", overlay.transform).gameObject.AddComponent<ZooAttackImpactGraphic>();
            impact.raycastTarget = true;
            caption = ZooMenuUI.Label("Attack cue", overlay.transform, "OUT OF PATIENCE!", 300, 65, 1320, 80, 46, ZooMenuUI.Cream, TextAnchor.MiddleCenter, true);
            caption.gameObject.AddComponent<Shadow>().effectColor = new Color(.2f, .06f, .02f, 1);
            ZooMenuUI.Label("Attack control lock", overlay.transform, "HANG ON!  •  CONTROLS TEMPORARILY LOCKED", 300, 982, 1320, 46, 25, ZooMenuUI.Cream, TextAnchor.MiddleCenter, true);
            audioSource = stage.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.spatialBlend = 0; audioSource.volume = .72f;
            thump = MakeSound(false); swish = MakeSound(true);
            audioSource.PlayOneShot(swish, .65f);
            Present();
        }
        void AddLight(string label, Vector3 rotation, Color color, float intensity)
        {
            var light = new GameObject(label, typeof(Light)).GetComponent<Light>(); light.transform.SetParent(stage.transform, false);
            light.transform.rotation = Quaternion.Euler(rotation); light.type = LightType.Directional;
            light.color = color; light.intensity = intensity; light.cullingMask = 1 << StageLayer; light.shadows = LightShadows.None;
        }
        public bool Advance(float seconds)
        {
            Age += Mathf.Max(0, seconds);
            while (Hits < HitCount && Age >= FirstHit + Hits * HitGap)
            {
                lastHit = FirstHit + Hits * HitGap; Hits++;
                audioSource.pitch = .94f + (Hits % 3) * .065f; audioSource.PlayOneShot(thump);
            }
            return Age >= Duration;
        }
        public void Present()
        {
            if (Restored || !actor || !view) return;
            bool reduced = ZooPCFlow.ReducedMotion;
            float hitAge = Age - lastHit;
            float punch = Mathf.Exp(-hitAge * 15);
            float recovery = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Age - 3.55f) / .65f));
            float approach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Age / .58f));
            actor.transform.localPosition = new Vector3(Mathf.Sin(Age * 4) * .035f, Mathf.Sin(approach * Mathf.PI) * .2f, Mathf.Lerp(3.5f, 0, approach) + recovery * .8f - punch * .08f);
            actor.transform.localRotation = Quaternion.Euler(0, 180 + Mathf.Sin(Age * 5) * 3, Mathf.Sin(Age * 8) * 2);
            float cycle = Mathf.Repeat(Mathf.Max(0, Age - .58f), HitGap) / HitGap;
            float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Age - .48f) / .12f)) * (1 - recovery);
            angryClip.SetTime(Age % assets.angry.length);
            attackClip.SetTime(cycle * activeClip.length);
            mixer.SetInputWeight(0, 1 - weight); mixer.SetInputWeight(1, weight); graph.Evaluate(0);
            ShakeStrength = reduced ? 0 : punch;
            Vector3 shake = reduced ? Vector3.zero : new Vector3(Mathf.Sin(hitAge * 83) * .055f, Mathf.Cos(hitAge * 71) * .038f, -punch * .055f) * punch;
            view.transform.position = Stage + new Vector3(0, 1.1f, -2.7f) + shake;
            view.transform.rotation = Quaternion.Euler(reduced ? 0 : Mathf.Sin(hitAge * 57) * punch * 3.3f, reduced ? 0 : Mathf.Sin(hitAge * 63) * punch * 2, reduced ? 0 : Mathf.Cos(hitAge * 48) * punch * 4.8f);
            view.fieldOfView = 49 + (reduced ? 0 : punch * 4);
            FlashStrength = Mathf.Clamp01(1 - hitAge / .14f) * (reduced ? .06f : .26f);
            impact.SetImpact(FlashStrength, reduced ? .1f : .32f + punch * .5f, Hits, Age);
            caption.text = Age < .58f ? "OUT OF PATIENCE!" : recovery > .1f ? "PHEW... BACK TO WORK" : "HEY! HEY! HEY!";
            if (ZooPCFlow.Instance && ZooPCFlow.Instance.Paused) { SuspendFeedback(); return; }
            var pad = Gamepad.current;
            if (rumblePad != pad) { rumblePad?.SetMotorSpeeds(0, 0); rumblePad = pad; }
            rumblePad?.SetMotorSpeeds(reduced ? 0 : punch * .4f, reduced ? 0 : punch * .65f);
        }
        public void SuspendFeedback() { rumblePad?.SetMotorSpeeds(0, 0); }
        public void Cleanup()
        {
            if (Restored) return; Restored = true;
            SuspendFeedback(); if (graph.IsValid()) graph.Destroy();
            if (view)
            {
                view.transform.localPosition = oldPosition; view.transform.localRotation = oldRotation;
                view.fieldOfView = oldFov; view.farClipPlane = oldFar; view.nearClipPlane = oldNear;
                view.cullingMask = oldMask; view.clearFlags = oldClear; view.backgroundColor = oldBackground;
            }
            RenderSettings.fog = oldFog;
            if (stage) { stage.SetActive(false); Destroy(stage); }
            if (overlay) { overlay.SetActive(false); Destroy(overlay); }
            if (thump) Destroy(thump); if (swish) Destroy(swish);
        }
        void OnDisable() { Cleanup(); }
#if UNITY_EDITOR
        public void PreviewPose(AnimationClip clip, float phase)
        {
            graph.Disconnect(mixer,1); graph.DestroyPlayable(attackClip);
            activeClip=clip; attackClip=AnimationClipPlayable.Create(graph,clip); attackClip.SetApplyFootIK(false);
            graph.Connect(attackClip,0,mixer,1); Age=.58f+phase*HitGap; Present();
        }
#endif
        static AudioClip MakeSound(bool whoosh)
        {
            const int rate = 48000;
            float length = whoosh ? .6f : .29f;
            var samples = new float[Mathf.CeilToInt(length * rate)];
            var noise = new System.Random(whoosh ? 184 : 917); float low = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, n = (float)noise.NextDouble() * 2 - 1;
                low = Mathf.Lerp(low, n, whoosh ? .12f : .4f);
                float value = whoosh
                    ? low * Mathf.Pow(Mathf.Sin(Mathf.PI * t / length), 1.3f) * .85f
                    : (Mathf.Sin(2 * Mathf.PI * (105 * t - 90 * t * t)) * Mathf.Exp(-t * 25) * .68f + low * Mathf.Exp(-t * 48) * .7f + n * Mathf.Exp(-t * 120) * .22f) * Mathf.Min(1, t * 1800);
                samples[i] = Mathf.Clamp(value, -.9f, .9f);
            }
            var clip = AudioClip.Create(whoosh ? "Monkey pounce whoosh" : "Cartoon palm impact", samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }
    }
}
