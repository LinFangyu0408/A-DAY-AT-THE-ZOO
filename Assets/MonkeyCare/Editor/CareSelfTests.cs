using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ADayAtTheZoo.Monkeys.Care;

public static class CareSelfTests
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception("Care self-test failed: " + message); }
    static CareProgress New() => new CareProgress(30, 8, 10, 3, 4);
    [MenuItem("Tools/猴子照顾/运行逻辑自检")]
    public static void Run()
    {
        checks = 0;
        var p = New(); Check(p.Patience == 1, "patience starts full"); p.Wait(15);
        Check(Mathf.Approximately(p.Patience, .75f), "quadratic patience");
        Check(!p.Feed(2), "food does not finish immediately");
        p.Wait(2); Check(p.Fed == 2 && !p.Late, "pause preserves effective feeding");
        Check(p.Feed(6) && !p.Late, "normal eight seconds");
        p = New(); p.Wait(22); Check(p.Feed(8) && !p.Late && Mathf.Approximately(p.Waited, 30), "exactly thirty seconds finishes normally");
        p = New(); p.Wait(24); p.Feed(6);
        Check(p.Fed == 6 && !p.Late, "six seconds progress at deadline");
        p.Wait(.01f); Check(p.Late && p.FeedTarget == 10 && p.Fed == 6, "rescue retains food and increases total to ten");
        p.Wait(100); Check(p.FeedTarget == 10, "rescue does not stack");
        Check(p.Feed(4), "four remaining seconds complete rescue");
        p = New(); p.Wait(29); p.Stroke(); p.Wait(1); p.Stroke();
        Check(p.StrokeTarget == 3 && p.Strokes == 2 && !p.Late, "stroke deadline equality");
        p.Wait(.01f); Check(p.StrokeTarget == 4 && p.Strokes == 2, "rescue keeps two strokes");
        Check(!p.Stroke() && p.Stroke(), "two more strokes finish rescue");
        p = New(); p.Feed(.1f); Check(p.FeedTarget == 8 && p.StrokeTarget == 3, "next task restores normal requirements");
        Check(CareRound.NextTime(25, 1, false) == 43, "normal refresh eighteen seconds");
        Check(CareRound.NextTime(25, 1, true) == 47, "rescue refresh twenty-two seconds");
        Check(CareRound.NextTime(53, 2, false) == 85, "third task gate");
        Check(CareRound.NextTime(131, 4, false) == 155, "fifth task gate");
        Check(CareRound.NextTime(80, 2, true) == 102, "late recovery can move pressure node");
        for (int count = 0; count <= 18; count++)
        {
            var round = new CareRound(); int completed = 0;
            for (int task = 0; task < 6; task++)
                for (int monkey = 0; monkey < 3 && completed < count; monkey++)
                {
                    CareNeed need = (task + (monkey == 1 ? 1 : 0)) % 2 == 0 ? CareNeed.Hunger : CareNeed.Itch;
                    Check(round.Complete(monkey, task, need, 200), "accept unique task"); completed++;
                }
            Check(round.CompletedTotal == count && Math.Abs(round.Score - (48 + 52.0 * count / 18)) < .00001, "score from integer count " + count);
            Check(round.Stars == (count == 0 ? 0 : count <= 12 ? 1 : count < 18 ? 2 : 3), "star count boundary " + count);
            Check(round.Bananas == 12 - round.FeedingTotal, "consume only completed feeding");
            Check(!round.Complete(0, 0, CareNeed.Hunger, 231), "completion after cutoff rejected");
            round.Freeze(); Check(!round.Complete(0, 0, CareNeed.Hunger, 200), "frozen round rejects changes");
        }
        var exact = new CareRound(); Check(exact.Complete(0, 0, CareNeed.Hunger, 230), "cutoff equality accepted before freeze");
        var custom = new CareRound(new[] { CareNeed.Itch, CareNeed.Hunger, CareNeed.Hunger });
        Check(custom.Complete(0, 0, CareNeed.Itch, 40) && custom.Complete(0, 1, CareNeed.Hunger, 70), "Inspector first need changes the alternating sequence");
        Check(!exact.Complete(0, 0, CareNeed.Hunger, 230) && exact.Bananas == 11, "duplicate task cannot consume twice");
        var s = new ScratchStroke();
        Check(!s.Sample(1, true, .08f, .06f), "initial touch not stroke");
        Check(!s.Sample(.98f, true, .08f, .06f), "jitter ignored");
        Check(s.Sample(.9f, true, .08f, .06f), "downstroke accepted");
        Check(!s.Sample(.8f, true, .08f, .06f), "continued down is not second stroke");
        Check(!s.Sample(.9f, true, .08f, .06f), "upward rearm not counted");
        Check(s.Sample(.81f, true, .08f, .06f), "second downstroke");
        s.Sample(.8f, false, .08f, .06f); s.Sample(.79f, true, .08f, .06f);
        Check(!s.Sample(.69f, true, .08f, .06f), "broken contact cannot bypass upward rearm");
        s.Reset(); s.Sample(1, true, .08f, .06f); s.Sample(.96f, true, .08f, .06f); s.Sample(.92f, false, .08f, .06f);
        Check(!s.Sample(.9f, true, .08f, .06f), "broken contact clears partial stroke");
        int[] done = new int[3]; float[] due = { 15, 25, 35 }; float now = 0; float maxLatency = 0;
        for (int i = 0; i < 18; i++)
        {
            int monkey = due[0] <= due[1] && due[0] <= due[2] ? 0 : due[1] <= due[2] ? 1 : 2;
            bool hunger = (done[monkey] + (monkey == 1 ? 1 : 0)) % 2 == 0;
            now = Mathf.Max(now, due[monkey]) + 2 + (hunger ? 8 : 6);
            maxLatency = Mathf.Max(maxLatency, now - due[monkey]); done[monkey]++;
            due[monkey] = done[monkey] == 6 ? float.PositiveInfinity : CareRound.NextTime(now, done[monkey], false);
        }
        Check(now == 209 && maxLatency <= 30, "document ideal schedule finishes at 209 without rescue");
        Directory.CreateDirectory("work/monkey-care");
        File.WriteAllText("work/monkey-care/self-tests.txt", "PASS " + checks + " assertions; scoring rules v1.0\n" + DateTime.Now.ToString("s"));
        Debug.Log("[Monkey Care] " + checks + " logic assertions passed.");
    }
}
