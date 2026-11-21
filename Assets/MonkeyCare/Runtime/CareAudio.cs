using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    [Serializable]
    public sealed class CareAudio
    {
        [InspectorName("回合音乐（欢快）"), Tooltip("确认开始后循环播放的卡通曲。留空使用随项目提供的音频。")] public AudioClip music;
        [InspectorName("待机音乐（慢速）"), Tooltip("READY/GO 待机画面循环播的慢速环境曲。")] public AudioClip ambientMusic;
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
        [InspectorName("按钮点击")] public AudioClip uiClick;
        [InspectorName("结算胜利音效")] public AudioClip victoryMusic;
        [InspectorName("按钮音量"), Range(0, 1)] public float clickVolume = .55f;
        [InspectorName("结算音效音量"), Range(0, 1)] public float victoryVolume = .85f;

        AudioSource musicSource;
        AudioSource ambientSource;
        AudioSource uiSource;
        AudioSource victorySource;
        Transform audioRoot;
        // True from the moment the results card appears until the next round.
        bool victory;
        AudioListener fallbackListener;
        // The watchdog must not resurrect music the session explicitly stopped.
        bool musicStopped;
        // False while the READY gate is up: the slow ambient bed plays instead of
        // the cheerful round track, so pressing confirm is what starts the party.
        bool roundMusic;
        bool alignedLastMinute;
        readonly Dictionary<Transform, AudioSource> effects = new();

        // --- final countdown -------------------------------------------------
        // The last five seconds only. A high tick and a wooden step note are
        // synthesized at runtime (like the whistle) so the warning needs no
        // extra asset and stays in tune with itself.
        AudioSource tickSource;
        AudioSource beatSource;
        AudioSource finalSource;
        AudioClip tickHigh, pulseClip, finalClip;
        float nextTickAt;
        int lastTickStage = -1;
        int tickCount;
        int lastFinalStep = -1;

        public void FollowCamera(Camera camera)
        {
            if (fallbackListener) fallbackListener.enabled = false;
            fallbackListener = null;
            if (!camera) return;
            // Exactly one live listener, on the camera that is actually
            // rendering. The scene ships three disabled AudioListeners (one per
            // camera) and the platform switcher re-enables the ones under its
            // own player root, so "any active listener wins" could leave audio
            // attached to a camera that never renders. Pin the listener to the
            // live camera instead.
            foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (listener.GetComponent<Camera>() != camera && listener.enabled)
                    listener.enabled = false;
            fallbackListener = camera.GetComponent<AudioListener>();
            if (!fallbackListener) fallbackListener = camera.gameObject.AddComponent<AudioListener>();
            fallbackListener.enabled = true;
        }

        // Opening stutter: decoding all eight clips synchronously in Start()
        // froze the first frame (the music clip alone decompresses ~40MB of
        // PCM). Stream them in from the background loader, one per frame.
        public IEnumerator Preload()
        {
            // The ready gate shows before the round starts, so the slow ambient
            // bed is the clip that has to be ready first. The cheerful track
            // follows; it is a fallback for the ambient bed only if the slower
            // wav is missing from the project.
            yield return LoadAsync(nameof(music), clip => ambientMusic = clip ? clip : ambientMusic);
            if (!ambientMusic) yield return LoadAsync("music_care", clip => ambientMusic = clip);
            yield return LoadAsync("music_care", clip => music = clip ? clip : music);
            if (!music) music = ambientMusic;
            // Results fanfare and the UI click. The click has a synthesized
            // fallback (BuildClick) so the button is never silent, but the
            // imported wav is preferred when it exists.
            yield return LoadAsync("jingle_victory_sting", clip => victoryMusic = clip ? clip : victoryMusic);
            yield return LoadAsync(nameof(uiClick), clip => uiClick = clip ? clip : uiClick);
            yield return LoadAsync(nameof(bananaGrab), clip => bananaGrab = bananaGrab ? bananaGrab : clip);
            yield return LoadAsync(nameof(scratchGrab), clip => scratchGrab = scratchGrab ? scratchGrab : clip);
            yield return LoadAsync(nameof(bite), clip => bite = bite ? bite : clip);
            yield return LoadAsync(nameof(stroke), clip => stroke = stroke ? stroke : clip);
            yield return LoadAsync(nameof(feedSuccess), clip => feedSuccess = feedSuccess ? feedSuccess : clip);
            yield return LoadAsync(nameof(scratchSuccess), clip => scratchSuccess = scratchSuccess ? scratchSuccess : clip);
        }

        static IEnumerator LoadAsync(string name, Action<AudioClip> assign)
        {
            var request = Resources.LoadAsync<AudioClip>("HighlandLakeAudio/" + name);
            while (!request.isDone) yield return null;
            var clip = request.asset as AudioClip;
            // Clips imported with preloadAudioData off decode lazily; force
            // the decode up front so the first play can never come out as
            // silence while the data is still streaming.
            if (clip && !clip.preloadAudioData) clip.LoadAudioData();
            assign(clip);
            // One clip per frame keeps the main thread light during preload.
            yield return null;
        }

        public void StartMusic(Transform owner)
        {
            // Two beds share the music slot: the slow ambient loop for the READY
            // gate and the cheerful cartoon track for the round itself. Both
            // sources run from the start at volume 0 and UpdateMusic cross-fades
            // whichever one the game state asks for, so starting the round is a
            // fade instead of a clip swap in the middle of a bar.
            if (!ambientMusic) ambientMusic = Default(ambientMusic, nameof(music));
            if (!music) music = Default(music, "music_care");
            if (!music) music = ambientMusic;
            bananaGrab = Default(bananaGrab, nameof(bananaGrab));
            scratchGrab = Default(scratchGrab, nameof(scratchGrab));
            whistle = Default(whistle, nameof(whistle));
            if (!whistle) whistle = BuildWhistle();
            bite = Default(bite, nameof(bite));
            stroke = Default(stroke, nameof(stroke));
            feedSuccess = Default(feedSuccess, nameof(feedSuccess));
            scratchSuccess = Default(scratchSuccess, nameof(scratchSuccess));
            victoryMusic = Default(victoryMusic, "jingle_victory_sting");
            uiClick = Default(uiClick, nameof(uiClick));
            if (!uiClick) uiClick = BuildClick();
            audioRoot = owner;
            victory = false;
            musicStopped = false;
            if (music)
            {
                if (!musicSource)
                {
                    musicSource = CreateSource(owner, "Care Music");
                    musicSource.loop = true;
                    // Music must keep time with the round, not with the audio
                    // device's buffer, so no doppler and no spatialisation.
                    musicSource.spatialBlend = 0;
                    musicSource.pitch = 1f;
                }
                if (!ambientSource)
                {
                    ambientSource = CreateSource(owner, "Care Ambient Music");
                    ambientSource.loop = true;
                    ambientSource.spatialBlend = 0;
                    ambientSource.pitch = 1f;
                }
                musicSource.clip = music;
                musicSource.volume = 0;
                musicSource.Play();
                ambientSource.clip = ambientMusic ? ambientMusic : music;
                ambientSource.volume = 0;
                ambientSource.Play();
            }
            EnsureCountdown(owner);
            EnsureUi(owner);
        }

        /// <summary>
        /// Called the moment the player confirms the READY gate: the cheerful
        /// round track starts from bar one and the ambient bed fades out.
        /// </summary>
        public void BeginRoundMusic()
        {
            // A restart from the results card has the fanfare looping; cut it
            // before the round track starts so the two never overlap.
            StopVictory();
            roundMusic = true;
            alignedLastMinute = false;
            if (!musicSource) return;
            if (music) musicSource.clip = music;
            musicSource.time = 0f;
            musicSource.pitch = 1f;
            musicSource.volume = 0f;
            musicSource.Play();
        }

        /// <summary>Back to the READY gate: fade the round track, bring the bed back.</summary>
        public void PauseToAmbient()
        {
            StopVictory();
            roundMusic = false;
            alignedLastMinute = false;
            if (musicSource) musicSource.time = 0f;
            if (ambientSource && ambientSource.clip && !ambientSource.isPlaying)
                ambientSource.Play();
        }

        /// <summary>
        /// Builds the countdown voices once. They are plain sines so they cut
        /// through the mix without needing another imported asset.
        /// </summary>
        void EnsureCountdown(Transform owner)
        {
            if (!tickHigh) tickHigh = BuildTick(1318.5f, .13f);
            if (!tickSource)
            {
                tickSource = CreateSource(owner, "Care Countdown");
                tickSource.spatialBlend = 0;
                tickSource.loop = false;
                tickSource.playOnAwake = false;
                tickSource.volume = .55f;
            }
            if (!beatSource)
            {
                beatSource = CreateSource(owner, "Care Countdown Pulse");
                beatSource.spatialBlend = 0;
                beatSource.loop = false;
                beatSource.playOnAwake = false;
                beatSource.volume = .5f;
            }
            // The last five seconds get their own voice on top of the beeps: a
            // wooden "dong" once per second, stepping up five semitones, so the
            // player hears the number counting down rather than just a rate.
            if (!finalClip) finalClip = BuildFinalTick();
            if (!finalSource)
            {
                finalSource = CreateSource(owner, "Care Final Countdown");
                finalSource.spatialBlend = 0;
                finalSource.loop = false;
                finalSource.playOnAwake = false;
                finalSource.volume = .7f;
            }
        }

        /// <summary>
        /// Non-spatial UI voices: the button click and the results fanfare.
        /// Both live on their own sources so the music cross-fade never
        /// touches them.
        /// </summary>
        void EnsureUi(Transform owner)
        {
            if (!uiSource)
            {
                uiSource = CreateSource(owner, "Care Ui Click");
                uiSource.spatialBlend = 0;
                uiSource.loop = false;
                uiSource.playOnAwake = false;
            }
            if (!victorySource)
            {
                // A sting, not a bed: it fires once when the card appears and
                // stops on its own. Nothing loops under the results screen.
                victorySource = CreateSource(owner, "Care Victory Sting");
                victorySource.spatialBlend = 0;
                victorySource.loop = false;
                victorySource.playOnAwake = false;
                victorySource.volume = 0f;
            }
        }

        /// <summary>Short "tok" for every confirmed button press.</summary>
        public void Click()
        {
            if (!uiSource) { if (!audioRoot) return; EnsureUi(audioRoot); }
            if (!uiClick) return;
            uiSource.pitch = UnityEngine.Random.Range(.97f, 1.03f);
            uiSource.PlayOneShot(uiClick, clickVolume);
        }

        static AudioClip BuildClick()
        {
            const int sampleRate = 44100;
            const float duration = .11f;
            int length = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)sampleRate;
                float attack = Mathf.Clamp01(t / .0015f);
                // A bright tick plus a soft wooden body: reads as a physical
                // button on TV speakers, where a pure sine just disappears.
                float tick = Mathf.Sin(2f * Mathf.PI * 1180f * t) * Mathf.Exp(-t / .016f)
                             + .40f * Mathf.Sin(2f * Mathf.PI * 2360f * t) * Mathf.Exp(-t / .007f);
                float body = Mathf.Sin(2f * Mathf.PI * 176f * t) * Mathf.Exp(-t / .035f) * .45f;
                samples[i] = (tick * .55f + body) * attack * .60f;
            }
            var clip = AudioClip.Create("Care Ui Click", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Results screen: the soundtrack drops out and a single "you did it"
        /// sting fires over the card. It plays once and rings out - deliberately
        /// not a loop, so the results screen is quiet while you read the score.
        /// </summary>
        public void PlayVictory()
        {
            victory = true;
            roundMusic = false;
            alignedLastMinute = false;
            if (!victorySource)
            {
                if (!audioRoot) return;
                EnsureUi(audioRoot);
            }
            victoryMusic = Default(victoryMusic, "jingle_victory_sting");
            if (!victoryMusic || !victorySource) return;
            victorySource.Stop();
            victorySource.loop = false;
            victorySource.clip = victoryMusic;
            victorySource.time = 0f;
            // Straight to full level: a sting that fades in loses its punch.
            victorySource.volume = victoryVolume;
            victorySource.Play();
        }

        /// <summary>Back to the READY gate: silence the fanfare.</summary>
        public void StopVictory()
        {
            victory = false;
            if (victorySource) victorySource.Stop();
        }

        static AudioClip BuildFinalTick()
        {
            const int sampleRate = 44100;
            const float duration = .24f;
            int length = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)sampleRate;
                float attack = Mathf.Clamp01(t / .004f);
                // G4 body plus its octave and a fast fourth partial: the same
                // wooden mallet family as the soundtrack, so it sits in the
                // music instead of poking out of it.
                float tone = Mathf.Sin(2f * Mathf.PI * 392f * t) * Mathf.Exp(-t / .075f)
                             + .42f * Mathf.Sin(2f * Mathf.PI * 784f * t) * Mathf.Exp(-t / .045f)
                             + .16f * Mathf.Sin(2f * Mathf.PI * 1587f * t) * Mathf.Exp(-t / .02f);
                float thump = Mathf.Sin(2f * Mathf.PI * 96f * t) * Mathf.Exp(-t / .05f) * .32f;
                samples[i] = (tone * .55f + thump) * attack;
            }
            var clip = AudioClip.Create("Care Final Countdown", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static AudioClip BuildTick(float frequency, float duration = .12f)
        {
            const int sampleRate = 44100;
            int length = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)sampleRate;
                float attack = Mathf.Clamp01(t / .003f);
                float body = Mathf.Exp(-t / .045f);
                float tone = Mathf.Sin(2f * Mathf.PI * frequency * t)
                             + Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * .30f
                             + Mathf.Sin(2f * Mathf.PI * frequency * 3f * t) * .12f;
                // A 2 ms click on top is what makes it read as "beep" instead of
                // "blip" on small speakers and through a headset.
                float click = Mathf.Sin(2f * Mathf.PI * frequency * 4.1f * t) * Mathf.Exp(-t / .008f) * .40f;
                samples[i] = (tone * body + click) * attack * .62f;
            }
            var clip = AudioClip.Create("Care Countdown Beep", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static AudioClip Default(AudioClip clip, string name)
        {
            if (clip) return clip;
            var loaded = Resources.Load<AudioClip>("HighlandLakeAudio/" + name);
            if (loaded && !loaded.preloadAudioData) loaded.LoadAudioData();
            return loaded;
        }

        public void UpdateMusic(bool results, float progress = 0f, float remaining = -1f,
            float roundLength = -1f)
        {
            // Drop audio sources whose owner vanished (monkey disabled/deleted);
            // otherwise silent AudioSources accumulate over long sessions.
            if (effects.Count > 0)
            {
                var stale = new System.Collections.Generic.List<Transform>();
                foreach (var pair in effects)
                    if (!pair.Key || !pair.Value) stale.Add(pair.Key);
                foreach (var key in stale)
                {
                    if (effects[key]) UnityEngine.Object.Destroy(effects[key].gameObject);
                    effects.Remove(key);
                }
            }
            if (!musicSource) return;
            // Music watchdog: if the Windows output endpoint switches (a
            // headset runtime can grab the device mid-play) the source stops
            // playing; restart it so the loop never falls silent for good.
            if (!musicStopped && Time.timeScale > 0f)
            {
                if (musicSource.clip && musicSource.volume > .001f && !musicSource.isPlaying)
                    musicSource.Play();
                if (ambientSource && ambientSource.clip && ambientSource.volume > .001f &&
                    !ambientSource.isPlaying)
                    ambientSource.Play();
            }
            float roundTarget = roundMusic && !victory ? musicVolume * (results ? .35f : 1f) : 0f;
            float ambientTarget = !roundMusic && !victory ? musicVolume * .9f : 0f;
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, roundTarget,
                Time.unscaledDeltaTime * .16f);
            if (ambientSource)
                ambientSource.volume = Mathf.MoveTowards(ambientSource.volume, ambientTarget,
                    Time.unscaledDeltaTime * .16f);
            // Results sting: one shot, never restarted from here. Once it has
            // rung out the card stays silent until CONTINUE/RESTART, which is
            // the point - the score should not fight a looping fanfare.
            if (victorySource && !victory)
                victorySource.volume = Mathf.MoveTowards(victorySource.volume, 0f,
                    Time.unscaledDeltaTime * .9f);

            AlignLastMinute(results, remaining, roundLength);

            // The lift lives in the arrangement (132 -> 156 BPM at 58.2 s and
            // 171.6 in the outro, see CareTempo), not in the playback rate.
            // Speeding the transport up would also mean the 120 s cue finishes
            // before the 120 s clock does, so the music would loop back to the
            // quiet intro a few seconds before the end - which is exactly the
            // "it speeds up in the middle" complaint. Keep the transport at 1:1
            // with the round clock; the hunger queue reads the same tempo map,
            // so the game speeds up where the music does.
            musicSource.pitch = Mathf.MoveTowards(musicSource.pitch, 1f,
                Time.unscaledDeltaTime * .5f);
        }

        /// <summary>
        /// Re-cues a short track so its final bar lands on the final second. A
        /// cue that already covers the whole round is left alone: it is aligned
        /// by construction, and nudging it would only fight the audio clock.
        /// </summary>
        void AlignLastMinute(bool results, float remaining, float roundLength)
        {
            if (!roundMusic || !musicSource || !musicSource.clip || results || remaining < 0f)
                return;
            float length = musicSource.clip.length;
            if (length <= 1f) return;
            if (roundLength > 0f && length >= roundLength - 1.5f) return;
            if (remaining <= length)
            {
                if (alignedLastMinute) return;
                alignedLastMinute = true;
                musicSource.time = Mathf.Clamp(length - remaining, 0f, length - .05f);
                return;
            }
            if (remaining > length + 1f) alignedLastMinute = false;
        }

        /// <summary>
        /// The last five seconds, and nothing before them. The cue used to start
        /// its warnings inside 30 s (1 Hz, then 2 Hz), which meant a two minute
        /// round spent a quarter of its life ticking at the player and the real
        /// countdown was impossible to pick out of the noise. Inside the window
        /// a wooden tick lands four times a second, a low pulse marks every beat,
        /// and a five note G4-to-D5 run underlines each whole second.
        /// </summary>
        public void UpdateCountdown(float remaining, bool running)
        {
            if (!tickSource) return;
            if (!running || remaining > CountdownWindow)
            {
                lastTickStage = -1;
                nextTickAt = 0f;
                return;
            }

            // Probe value: which note of the five the clock is on right now.
            // The audible step only changes once a second (below), but the
            // reading has to follow the clock so the on-device probe can see it.
            lastFinalStep = FinalStep(remaining);
            if (lastTickStage != 2)
            {
                lastTickStage = 2;
                // The first note and the first tick land on the very frame the
                // clock reads five, so the change is audible immediately.
                nextTickAt = 0f;
                tickCount = 0;
                tickSource.volume = .73f;
                PlayFinalTick(remaining);
            }

            float now = Time.unscaledTime;
            if (now < nextTickAt) return;
            nextTickAt = now + .25f;
            tickSource.PlayOneShot(tickHigh, 1f);

            tickCount++;
            if (tickCount % 4 == 0)
            {
                if (beatSource) beatSource.PlayOneShot(pulseClip ? pulseClip : (pulseClip = BuildPulse()), .95f);
                PlayFinalTick(remaining);
            }
        }

        /// <summary>
        /// One wooden hit per second inside the last five seconds, stepping up
        /// five semitones from G4 so 5-4-3-2-1 reads as a countdown melody
        /// rather than five identical knocks.
        /// </summary>
        void PlayFinalTick(float remaining)
        {
            if (!finalSource || !finalClip) return;
            int step = FinalStep(remaining);
            lastFinalStep = step;
            finalSource.clip = finalClip;
            finalSource.pitch = 1f + step * .1225f;
            finalSource.volume = .72f;
            finalSource.Play();
        }

        static int FinalStep(float remaining) =>
            Mathf.Clamp(5 - Mathf.CeilToInt(remaining), 0, 4);

        // Exactly the final five seconds. Changing this to 30 was the "it starts
        // beeping at 25 s left" bug; the window and the melodic run must agree.
        const float CountdownWindow = 5f;

        // Read-only probes: the smoke suite and the on-device probe both need to
        // prove which track is playing and that the warning voices exist,
        // without opening the audio mixer by hand.
        public string MusicName => musicSource && musicSource.clip ? musicSource.clip.name : "none";
        public string AmbientName => ambientSource && ambientSource.clip ? ambientSource.clip.name : "none";
        public bool RoundMusicActive => roundMusic;
        public float MusicPitch => musicSource ? musicSource.pitch : 1f;
        public float MusicLength => musicSource && musicSource.clip ? musicSource.clip.length : 0f;
        public float MusicTime => musicSource ? musicSource.time : 0f;
        public float AmbientVolume => ambientSource ? ambientSource.volume : 0f;
        public bool CountdownReady => tickHigh != null && finalClip != null;
        public bool HungerCallReady => whistle != null;
        public bool ClickReady => uiClick != null;
        public bool VictoryReady => victoryMusic != null && victorySource != null;
        public string VictoryClipName => victoryMusic ? victoryMusic.name : "none";
        public bool VictorySourceReady => victorySource != null;
        public bool VictoryPlaying => victory;
        public float VictoryVolumeNow => victorySource ? victorySource.volume : 0f;
        public bool VictoryLoops => victorySource && victorySource.loop;
        public float VictoryClipLength => victoryMusic ? victoryMusic.length : 0f;
        public bool VictoryAudible => victorySource && victorySource.isPlaying;
        public bool FinalCountdownReady => finalClip != null && finalSource != null;
        public int FinalCountdownStep => lastFinalStep;
        public int CountdownStage => lastTickStage;

        static AudioClip BuildPulse()
        {
            const int sampleRate = 44100;
            const float duration = .26f;
            int length = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)sampleRate;
                // 70 Hz falling to 48 Hz: felt more than heard, keeps the low
                // end under the beeps instead of fighting the bass line.
                float frequency = Mathf.Lerp(70f, 48f, Mathf.Clamp01(t / .18f));
                float attack = Mathf.Clamp01(t / .006f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t / .09f) * attack * .85f;
            }
            var clip = AudioClip.Create("Care Countdown Pulse", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
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
            musicStopped = true;
            if (fallbackListener) fallbackListener.enabled = false;
            if (musicSource) musicSource.Stop();
            if (ambientSource) ambientSource.Stop();
            if (tickSource) tickSource.Stop();
            if (beatSource) beatSource.Stop();
            if (finalSource) finalSource.Stop();
            if (uiSource) uiSource.Stop();
            if (victorySource) victorySource.Stop();
            victory = false;
            lastTickStage = -1;
            lastFinalStep = -1;
            nextTickAt = 0f;
            foreach (var source in effects.Values)
                if (source) source.Stop();
        }
    }
}
