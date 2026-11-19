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

        for (int count = 0; count <= CareRound.PlannedFeedings; count++)
        {
            var round = new CareRound();
            int completed = 0;
            for (int monkey = 0; monkey < 3 && completed < count; monkey++)
            {
                for (int task = 0; task < round.TasksForMonkey(monkey) && completed < count; task++)
                {
                    Check(round.Complete(monkey, task, CareNeed.Hunger, 200), "accept unique feeding task");
                    Check(round.ConsumeBanana(), "successful feeding consumes one banana");
                    completed++;
                }
            }
            Check(round.FeedingOnly, "new rounds are feeding-only");
            Check(round.CompletedTotal == count, "feeding-only completion count " + count);
            Check(Math.Abs(round.Score - count) < .00001, "score equals fed bananas " + count);
            Check(round.Bananas == CareRound.StartingBananas - round.FeedingTotal,
                "only completed feeding consumes a banana");
            Check(!round.Complete(0, 0, CareNeed.Hunger, 231), "completion after cutoff rejected");
            round.Freeze();
            Check(!round.Complete(0, 0, CareNeed.Hunger, 200), "frozen round rejects changes");
        }

        // Random hunger plan: three monkeys, nine episodes, everyone involved.
        for (int seed = 0; seed < 12; seed++)
        {
            var random = new CareRound();
            int sum = random.TasksForMonkey(0) + random.TasksForMonkey(1) + random.TasksForMonkey(2);
            Check(sum == CareRound.PlannedFeedings,
                "random hunger plan totals nine episodes (run " + seed + ")");
            Check(random.TasksForMonkey(0) >= 2 && random.TasksForMonkey(1) >= 2 &&
                  random.TasksForMonkey(2) >= 2,
                "every monkey keeps at least two hunger episodes (run " + seed + ")");
        }

        var exact = new CareRound();
        Check(exact.Complete(0, 0, CareNeed.Hunger, 230), "cutoff equality accepted before freeze");
        Check(exact.TaskTotal == 9, "feeding-only rounds plan nine hunger episodes");
        Check(!exact.Complete(0, 1, CareNeed.Itch, 40), "feeding-only round rejects itching");
        var ignoredNeeds = new CareRound(new[] { CareNeed.Itch, CareNeed.Itch, CareNeed.Itch });
        Check(ignoredNeeds.Complete(1, 0, CareNeed.Hunger, 20), "feeding-only mode ignores old scene itch defaults");

        var pickup = new CareRound();
        Check(!pickup.RefillBananas(), "banana pickup is unavailable while stock remains");
        for (int i = 0; i < CareRound.StartingBananas; i++)
            Check(pickup.ConsumeBanana(), "starting banana can be consumed");
        Check(pickup.Bananas == 0 && pickup.RefillBananas(), "empty stock refills by ten at the pickup point");
        Check(pickup.Bananas == CareRound.BananaPickupAmount, "pickup restores ten bananas");

        var penalties = new CareRound();
        Check(penalties.RegisterPatiencePenalty(1, 0), "first patience timeout records a penalty");
        Check(!penalties.RegisterPatiencePenalty(1, 0), "same timeout cannot deduct twice");
        Check(penalties.PatiencePenalties == 1, "patience timeout is recorded for the report");
        Check(Math.Abs(penalties.Score) < .00001, "score stays zero before any feeding");
        for (int i = 0; i < CareRound.PlannedFeedings; i++)
        {
            int monkey = 0, task = i;
            while (task >= penalties.TasksForMonkey(monkey)) task -= penalties.TasksForMonkey(monkey++);
            Check(penalties.Complete(monkey, task, CareNeed.Hunger, 100),
                "completes a full nine-banana run");
        }
        Check(penalties.Score == CareRound.PlannedFeedings && penalties.IsPassing &&
              penalties.Stars == 3,
            "nine successful feeds reach one hundred percent and three stars");
        Check(penalties.ResolvedPercent > 99.999, "full run reports one hundred percent");

        var passing = new CareRound();
        for (int i = 0; i < passing.PassingFeedings; i++)
        {
            int monkey = 0, task = i;
            while (task >= passing.TasksForMonkey(monkey)) task -= passing.TasksForMonkey(monkey++);
            Check(passing.Complete(monkey, task, CareNeed.Hunger, 100), "sixtieth-percent feed completes");
        }
        Check(passing.IsPassing && passing.Stars == 1, "sixty percent is the one-star passing line");

        var twoStar = new CareRound();
        int twoStarTarget = (int)Math.Ceiling(CareRound.TwoStarRatio * CareRound.PlannedFeedings);
        for (int i = 0; i < twoStarTarget; i++)
        {
            int monkey = 0, task = i;
            while (task >= twoStar.TasksForMonkey(monkey)) task -= twoStar.TasksForMonkey(monkey++);
            Check(twoStar.Complete(monkey, task, CareNeed.Hunger, 100), "eighty-percent feed completes");
        }
        Check(twoStar.Stars == 2 && !twoStar.Frozen, "eighty percent earns two stars");

        var mixed = new CareRound(new[] { CareNeed.Hunger, CareNeed.Itch, CareNeed.Hunger }, false);
        Check(mixed.TasksForMonkey(1) == 6 && mixed.Complete(1, 0, CareNeed.Itch, 40),
            "legacy mixed mode remains available");

        var stroke = new ScratchStroke();
        Check(!stroke.Sample(1, true, .08f, .06f), "initial touch not stroke");
        Check(stroke.Sample(.9f, true, .08f, .06f), "legacy scratching logic remains intact");

        Directory.CreateDirectory("work/monkey-care");
        File.WriteAllText("work/monkey-care/self-tests.txt",
            "PASS " + checks + " assertions; feeding-only v2\n" + DateTime.Now.ToString("s"));
        Debug.Log("[Monkey Care] " + checks + " logic assertions passed.");
    }
}
