#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC.Editor
{
    public sealed partial class ZooDayTwoValidation
    {
        BinaryWriter audioWriter;
        bool audioRecording;
        long audioSamples;
        public static void Attack()
        {
            SessionState.SetBool("ZooDay2.AttackPose",false);
            SessionState.SetBool("ZooDay2.Attack", true); Launch(true);
        }
        public static void AttackPose() { SessionState.SetBool("ZooDay2.AttackPose",true); SessionState.SetBool("ZooDay2.Attack",true); Launch(false); }
        IEnumerator AttackChecks()
        {
            AttackModelChecks();
            var flow = ZooPCFlow.Instance;
            Click("Start Game"); yield return Frames(35); Click("Next day"); yield return Frames(45);
            Click("Play Day 1"); yield return WaitLoad(); yield return Frames(50);
            var session = FindFirstObjectByType<ZooDayTwoSession>();
            Require(session && session.navigation.Ready, "Attack test loads the roaming Day 02 scene");
            if (SessionState.GetBool("ZooDay2.AttackPose",false))
            {
                session.StartShift();
                session.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Day 02 HUD").gameObject.SetActive(false);
                var study = session.gameObject.AddComponent<ZooMonkeyAttackView>();
                study.Begin(session.monkeys[0],session.playerCamera,session.GetComponentInChildren<Canvas>().transform);
                foreach (string name in new[]{"ATK1","ATK2","ATK3","NormalATK"})
                {
                    var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/Suriyun/Animations/Pspsps Animations/Character Animations/Anim_Chibi@"+name+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                    for(int i=0;i<3;i++) { study.PreviewPose(clip,.15f+i*.3f); Snapshot("pose-"+name+"-"+i); }
                }
                study.Cleanup(); Destroy(study); yield break;
            }
            Click("Begin Day 02 shift");
            while (session.Round.Elapsed < 23) yield return null;
            var culprit = session.monkeys.First(n => session.Round.animals[n.index].state == ZooCareState.Waiting);
            Aim(session, culprit);
            Directory.CreateDirectory(Output + "/frames"); record = true;
            Snapshot("01-patience-running-out");
            Vector3 savedCameraPosition=session.playerCamera.transform.localPosition;
            Quaternion savedCameraRotation=Quaternion.identity;
            float savedFov=session.playerCamera.fieldOfView; int savedMask=session.playerCamera.cullingMask;
            while (!session.IsBeingAttacked) { Aim(session, culprit); savedCameraRotation=session.playerCamera.transform.localRotation; yield return null; }
            float requested = session.Round.animals[culprit.index].requestedAt;
            Require(session.Round.Elapsed - requested >= session.rules.patience && session.AttacksStarted == 1, "The real patience timer triggers exactly one attack at expiry");
            // The attack owns the camera now; inspect its presentation and guard
            // actual public interaction entry points, not a replacement simulation.
            var view = session.AttackView;
            Require(view && view.Actor && session.player.inputLocked && !session.IsRunning, "Attacker close-up is active and player movement is locked");
            int bananas = session.Round.Bananas, score = session.Round.Completed, tool = session.SelectedTool;
            session.SelectTool(1 - tool);
            Require(!session.TryThrow(culprit) && !session.TryScratch(culprit) && !session.TryRestock() && session.SelectedTool == tool, "Attack blocks throw, scratch, refill and tool selection");
            Require(!session.Round.SpendBanana() && !session.Round.Refill() && !session.Round.Stroke(culprit.index) && !session.Round.ReceiveBanana(culprit.index,0), "Round model also rejects care and inventory changes during attack");
            Require(session.Round.Bananas == bananas && session.Round.Completed == score, "Blocked interactions do not consume stock or award points");
            Require(!session.ScratchInProgress && !session.TargetMarker.gameObject.activeSelf, "Attack cancels scratch motion and hides the target indicator");
            float elapsed = session.Round.Elapsed;
            Vector3 keeper = session.player.transform.position;
            if (Keyboard.current!=null) InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W,Key.D,Key.E,Key.Digit2));
            while (view.Age < .4f) yield return null; Snapshot("02-pounce");
            while (view.Hits < 1) yield return null;
            yield return null; Snapshot("03-first-impact");
            Require(view.FlashStrength > 0 && view.ShakeStrength > 0, "Impact synchronizes visible flash and camera shake");
            var impactGraphic=FindFirstObjectByType<ZooAttackImpactGraphic>(); var impactMesh=impactGraphic.canvasRenderer.GetMesh();
            Require(impactMesh && impactMesh.vertexCount>100, "Impact graphic submits flash and speed-line geometry to the canvas renderer");
            while (view.Age < 1.3f) yield return null;
            if (Keyboard.current!=null) InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            Require(Vector3.Distance(keeper, session.player.transform.position) < .03f && session.SelectedTool==tool && session.Round.Bananas==bananas, "Injected movement, refill and tool keys are ignored during attack");
            flow.ShowPause(); float attackAge = view.Age; int hits = view.Hits;
            yield return Frames(20);
            Require(view.Age == attackAge && view.Hits == hits && session.Round.Elapsed == elapsed, "Pause freezes attack timing, impacts and round clock");
            flow.Resume();
            while (view.Hits < 3) yield return null;
            yield return null; Snapshot("04-repeated-palm-strikes");
            Require(Vector3.Distance(keeper, session.player.transform.position) < .03f, "Player stays in place during the close-up");
            while (session.IsBeingAttacked) yield return null;
            Require(view.Hits == 5 && view.Restored, "Five impacts finish after 4.2 active seconds and release presentation resources");
            Require(session.IsRunning && !session.player.inputLocked && session.Round.Elapsed - elapsed < .1f, "Interaction resumes without consuming patience or shift time during the forced sequence");
            Require(session.playerCamera.cullingMask==savedMask && session.playerCamera.fieldOfView==savedFov && Vector3.Distance(session.playerCamera.transform.localPosition,savedCameraPosition)<.001f && Quaternion.Angle(session.playerCamera.transform.localRotation,savedCameraRotation)<.01f, "Exact camera pose, field of view and world rendering are restored");
            Require(culprit.OnWalkableGround, "The real monkey keeps its navigation registration");
            Aim(session, culprit); yield return Frames(18); Snapshot("05-back-to-work");
            yield return Approach(session, culprit, 7); Aim(session, culprit);
            session.SelectTool(0); Require(session.TryThrow(culprit), "The same late monkey can be fed after attacking");
            yield return Frames(175);
            Require(session.Round.Feedings == 1 && session.AttacksStarted == 1, "Late care still completes, without retriggering the same attack");
            Snapshot("06-care-recovered"); yield return Frames(45);
            // Capture a second, naturally expired request with reduced motion.
            PlayerPrefs.SetInt("ZooPC.ReducedMotion",1);
            while (!session.IsBeingAttacked) yield return null;
            var reduced = session.AttackView;
            while (reduced.Hits < 1) yield return null;
            yield return null;
            Require(reduced.ShakeStrength == 0 && reduced.FlashStrength <= .061f, "Reduced motion suppresses shake and softens the flash");
            Snapshot("07-reduced-motion");
            // Leaving during a strike must clean up even before normal completion.
            flow.ReturnToJournal(); yield return WaitLoad(); yield return Frames(35);
            Require(!FindFirstObjectByType<ZooMonkeyAttackView>() && !FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t=>t.name=="Day 02 attack close-up stage"), "Leaving mid-attack removes close-up actor and effects");
            Require(Time.timeScale == 1 && !AudioListener.pause, "Leaving mid-attack restores the normal global pause state");
            record = false; FinishAttackAudio();
            PlayerPrefs.SetInt("ZooPC.ReducedMotion",0);
            Click("Play Day 1"); yield return WaitLoad(); yield return Frames(45);
            session = FindFirstObjectByType<ZooDayTwoSession>();
            Require(session && !session.IsBeingAttacked && !session.Round.Started && session.AttacksStarted == 0, "Re-entering Day 02 starts without stale attack state");
        }
        void AttackModelChecks()
        {
            var rules = ScriptableObject.CreateInstance<ZooDayTwoRules>();
            var round = new ZooDayTwoRound(rules); round.Start(); round.Tick(1);
            round.Tick(rules.patience-.01f); Require(!round.TryBeginAttack(), "No attack before patience reaches zero");
            round.Tick(.02f); Require(round.TryBeginAttack() && round.AttackIndex==0, "Expired request selects the correct attacker");
            float elapsed=round.Elapsed; round.Tick(10);
            Require(round.Elapsed==elapsed && !round.TryBeginAttack(), "Attack cannot stack or advance other request timers");
            round.EndAttack(); round.Tick(1);
            Require(!round.TryBeginAttack() && round.animals[0].late, "Same expired request only attacks once and remains recoverable");
            rules.patience=1; rules.secondRequestAt=0;
            var concurrent=new ZooDayTwoRound(rules); concurrent.Start(); concurrent.Tick(1); concurrent.Tick(2.1f); concurrent.Tick(1.1f);
            Require(concurrent.TryBeginAttack() && concurrent.AttackIndex==0, "Simultaneous overdue requests pick the oldest first");
            concurrent.EndAttack(); Require(concurrent.TryBeginAttack() && concurrent.AttackIndex==1, "Another overdue monkey waits its turn instead of overlapping");
            concurrent.EndAttack(); Require(!concurrent.TryBeginAttack(), "Handled attacks are not queued again");
            var timeout=new ZooDayTwoRound(rules); timeout.Start(); timeout.Tick(1); timeout.Tick(rules.duration);
            Require(timeout.Finished && !timeout.TryBeginAttack(), "Round completion takes priority over a pending attack");
            Destroy(rules);
        }
        void CaptureAttackAudio()
        {
            if (!SessionState.GetBool("ZooDay2.Attack",false)) return;
            if (!audioRecording)
            {
                if (!AudioRenderer.Start()) throw new System.Exception("Could not begin Unity audio capture");
                audioRecording=true; audioWriter=new BinaryWriter(File.Create(Output+"/game-audio.f32"));
            }
            int count=AudioRenderer.GetSampleCountForCaptureFrame()*2;
            if (count<=0) return;
            using (var buffer=new NativeArray<float>(count,Allocator.Temp))
            {
                if (!AudioRenderer.Render(buffer)) throw new System.Exception("Unity audio rendering failed");
                for (int i=0;i<count;i++) audioWriter.Write(buffer[i]); audioSamples+=count;
            }
        }
        void FinishAttackAudio()
        {
            if (!audioRecording) return;
            AudioRenderer.Stop(); audioRecording=false; audioWriter?.Dispose(); audioWriter=null;
            File.WriteAllText(Output+"/audio-info.txt", "rate="+AudioSettings.outputSampleRate+" channels=2 samples="+audioSamples);
        }
    }
}
#endif
