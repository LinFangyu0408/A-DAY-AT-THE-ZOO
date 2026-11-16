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
        var progress = New();
        Check(progress.Patience == 1, "patience starts full");
        progress.Wait(15);
        Check(Mathf.Approximately(progress.Patience, .75f), "quadratic patience");
        Check(!progress.Feed(2), "food does not finish immediately");
        progress.Wait(2);
        Check(progress.Fed == 2 && !progress.Late, "pause preserves effective feeding");
        Check(progress.Feed(6) && !progress.Late, "normal eight seconds");

        progress = New(); progress.Wait(24); progress.Feed(6);
        Check(progress.Fed == 6 && !progress.Late, "six seconds progress at deadline");
        progress.Wait(.01f);
        Check(progress.Late && progress.FeedTarget == 10 && progress.Fed == 6, "rescue retains food and raises target");
        Check(progress.Feed(4), "rescue completes after remaining feeding time");

        Check(CareRound.NextTime(25, 1, false) == 43, "normal refresh eighteen seconds");
        Check(CareRound.NextTime(25, 1, true) == 47, "rescue refresh twenty-two seconds");

        for (int count = 0; count <= 9; count++)
        {
            var round = new CareRound();
            int completed = 0;
            for (int task = 0; task < round.TasksPerMonkey; task++)
            {
                for (int monkey = 0; monkey < 3 && completed < count; monkey++)
                {
                    Check(round.Complete(monkey, task, CareNeed.Hunger, 200), "accept unique feeding task");
                    completed++;
                }
            }
            Check(round.FeedingOnly, "new rounds are feeding-only");
            Check(round.CompletedTotal == count, "feeding-only completion count " + count);
            Check(Math.Abs(round.Score - (48 + 52.0 * count / 9)) < .00001, "score from nine feeding tasks " + count);
            Check(round.Bananas == 12 - round.FeedingTotal, "only completed feeding consumes a banana");
            Check(!round.Complete(0, 0, CareNeed.Hunger, 231), "completion after cutoff rejected");
            round.Freeze();
            Check(!round.Complete(0, 0, CareNeed.Hunger, 200), "frozen round rejects changes");
        }

        var exact = new CareRound();
        Check(exact.Complete(0, 0, CareNeed.Hunger, 230), "cutoff equality accepted before freeze");
        Check(exact.TasksPerMonkey == 3 && exact.TaskTotal == 9, "three feedings per monkey / nine total");
        Check(!exact.Complete(0, 1, CareNeed.Itch, 40), "feeding-only round rejects itching");
        var ignoredNeeds = new CareRound(new[] { CareNeed.Itch, CareNeed.Itch, CareNeed.Itch });
        Check(ignoredNeeds.Complete(1, 0, CareNeed.Hunger, 20), "feeding-only mode ignores old scene itch defaults");

        var mixed = new CareRound(new[] { CareNeed.Hunger, CareNeed.Itch, CareNeed.Hunger }, false);
        Check(mixed.TasksPerMonkey == 6 && mixed.Complete(1, 0, CareNeed.Itch, 40), "legacy mixed mode remains available");

        var stroke = new ScratchStroke();
        Check(!stroke.Sample(1, true, .08f, .06f), "initial touch not stroke");
        Check(stroke.Sample(.9f, true, .08f, .06f), "legacy scratching logic remains intact");

        Directory.CreateDirectory("work/monkey-care");
        File.WriteAllText("work/monkey-care/self-tests.txt",
            "PASS " + checks + " assertions; feeding-only v2\n" + DateTime.Now.ToString("s"));
        Debug.Log("[Monkey Care] " + checks + " logic assertions passed.");
    }
}
