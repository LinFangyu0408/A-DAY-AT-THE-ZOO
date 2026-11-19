using System;
using System.Collections.Generic;
using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    [Serializable]
    public sealed class CareAudio
    {
        [InspectorName("背景音乐"), Tooltip("留空使用随项目提供的音频。")] public AudioClip music;
        [InspectorName("抓起香蕉")] public AudioClip bananaGrab;
        [InspectorName("抓起痒痒挠")] public AudioClip scratchGrab;
        [InspectorName("猴子饥饿哨声")] public AudioClip whistle;
        [InspectorName("开始投喂")] public AudioClip bite;
        [InspectorName("有效下挠")] public AudioClip stroke;
        [InspectorName("投喂成功")] public AudioClip feedSuccess;
        [InspectorName("挠痒成功")] public AudioClip scratchSuccess;
        [InspectorName("音乐音量"), Range(0, 1)] public float musicVolume = .16f;
        [InspectorName("抓取音量"), Range(0, 1)] public float grabVolume = .50f;
        [InspectorName("饥饿哨声音量"), Range(0, 1)] public float whistleVolume = .95f;
        [InspectorName("任务反馈音量"), Range(0, 1)] public float feedbackVolume = .58f;

        AudioSource musicSource;
        AudioListener fallbackListener;
        readonly Dictionary<Transform, AudioSource> effects = new();

        public void FollowCamera(Camera camera)
        {
            if (fallbackListener) fallbackListener.enabled = false;
            if (!camera) return;
            foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled) return;

            fallbackListener = camera.GetComponent<AudioListener>();
            if (!fallbackListener) fallbackListener = camera.gameObject.AddComponent<AudioListener>();
            fallbackListener.enabled = true;
        }

        public void StartMusic(Transform owner)
        {
            music = Default(music, nameof(music));
            bananaGrab = Default(bananaGrab, nameof(bananaGrab));
            scratchGrab = Default(scratchGrab, nameof(scratchGrab));
            whistle = Default(whistle, nameof(whistle));
            if (!whistle) whistle = BuildWhistle();
            bite = Default(bite, nameof(bite));
            stroke = Default(stroke, nameof(stroke));
            feedSuccess = Default(feedSuccess, nameof(feedSuccess));
            scratchSuccess = Default(scratchSuccess, nameof(scratchSuccess));
            if (!music) return;
            musicSource = CreateSource(owner, "Care Music");
            musicSource.loop = true;
            musicSource.clip = music;
            musicSource.volume = 0;
            musicSource.Play();
        }

        static AudioClip Default(AudioClip clip, string name) =>
            clip ? clip : Resources.Load<AudioClip>("HighlandLakeAudio/" + name);

        public void UpdateMusic(bool results)
        {
            if (!musicSource) return;
            float target = musicVolume * (results ? .35f : 1);
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * .16f);
        }

        public void Grab(Transform hand, CareNeed kind) =>
            Play(hand, kind == CareNeed.Hunger ? bananaGrab : scratchGrab, grabVolume);

        public void Bite(Transform npc) => Play(npc, bite, feedbackVolume);

        // Spatial audio is attached to the hungry monkey, so the player can
        // use the stereo direction of the whistle to find which animal called.
        public void Whistle(Transform npc) => Play(npc, whistle, whistleVolume);

        public void Stroke(Transform npc, int count) =>
            Play(npc, stroke, feedbackVolume * .8f, 1 + Mathf.Clamp(count - 1, 0, 5) * .055f);

        public void Complete(Transform npc, CareNeed kind) =>
            Play(npc, kind == CareNeed.Hunger ? feedSuccess : scratchSuccess, feedbackVolume);

        static AudioClip BuildWhistle()
        {
            const int sampleRate = 44100;
            const float duration = 1.18f;
            int length = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)sampleRate;
                float pulse = t is >= .05f and < .43f ? Mathf.InverseLerp(.05f, .12f, t) *
                              Mathf.Clamp01((.43f - t) / .12f) :
                              t is >= .57f and < .99f ? Mathf.InverseLerp(.57f, .64f, t) *
                              Mathf.Clamp01((.99f - t) / .13f) : 0f;
                float local = t < .5f ? t : t - .52f;
                float glide = t < .5f ? Mathf.Lerp(1780f, 2380f, Mathf.Clamp01(local / .34f)) :
                    Mathf.Lerp(2450f, 2020f, Mathf.Clamp01(local / .4f));
                float vibrato = Mathf.Sin(2f * Mathf.PI * 17f * local) * 35f;
                float fundamental = Mathf.Sin(2f * Mathf.PI * (glide + vibrato) * local);
                float harmonic = Mathf.Sin(2f * Mathf.PI * (glide * 1.48f + vibrato) * local) * .22f;
                float breath = Mathf.Sin(2f * Mathf.PI * (glide * .49f) * local) * .08f;
                samples[i] = (fundamental + harmonic + breath) * pulse * .62f;
            }
            var clip = AudioClip.Create("Monkey Hunger Whistle", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void Play(Transform origin, AudioClip clip, float volume, float pitch = 1)
        {
            if (!origin || !clip || volume <= 0) return;
            if (!effects.TryGetValue(origin, out var source) || !source)
            {
                source = CreateSource(origin, "Care Spatial Audio");
                source.spatialBlend = 1;
                source.dopplerLevel = 0;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1.25f;
                source.maxDistance = 45;
                effects[origin] = source;
            }
            bool hungerCall = clip == whistle;
            source.spatialBlend = hungerCall ? .72f : 1f;
            source.minDistance = hungerCall ? 2.2f : 1.25f;
            source.maxDistance = hungerCall ? 45f : 24f;
            source.pitch = pitch;
            source.volume = volume;
            source.clip = clip;
            source.Play();
#if UNITY_EDITOR
            CareDemoRecorder.RecordSound(source, clip);
#endif
        }

        static AudioSource CreateSource(Transform parent, string name)
        {
            var emitter = new GameObject(name);
            emitter.transform.SetParent(parent, false);
            var source = emitter.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        public void Stop()
        {
            if (fallbackListener) fallbackListener.enabled = false;
            if (musicSource) musicSource.Stop();
            foreach (var source in effects.Values)
                if (source) source.Stop();
        }
    }
}
