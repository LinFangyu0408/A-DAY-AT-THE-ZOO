using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ADayAtTheZoo.Monkeys.Care;

[InitializeOnLoad]
public static class CareSelfTests
{
    const string Work = "work/monkey-care/";
    const string Request = Work + "self-tests.request";
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception("Care self-test failed: " + message); }
    static CareProgress New() => new CareProgress(30, 8, 10, 3, 4);

    static CareSelfTests() => EditorApplication.update += Tick;

    // One-shot request file so the automation session can run the pure logic
    // assertions (no play mode, no scene changes) without opening a menu.
    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists(Request)) return;
        File.Delete(Request);
        try
        {
            Run();
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Work);
            File.WriteAllText(Work + "self-tests.txt", "FAIL " + exception);
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/猴子照顾/运行逻辑自检")]
    public static void Run()
    {
        checks = 0;
        var progress = New();
        Check(progress.Patience == 1, "patience starts full");
        progress.Wait(15);
        Check(Mathf.Approximately(progress.Patience, .5f), "linear patience drains every second");
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

        for (int count = 0; count <= CareRound.StartingBananas; count++)
        {
            var round = new CareRound();
            int completed = 0;
            for (int monkey = 0; monkey < 3 && completed < count; monkey++)
            {
                for (int task = 0; task < round.TasksForMonkey(monkey) && completed < count; task++)
                {
                    Check(round.Complete(monkey, task, CareNeed.Hunger, CareRound.Cutoff - 20), "accept unique feeding task");
                    Check(round.ConsumeBanana(), "successful feeding consumes one banana");
                    completed++;
                }
            }
            Check(round.FeedingOnly, "new rounds are feeding-only");
            Check(round.CompletedTotal == count, "feeding-only completion count " + count);
            Check(Math.Abs(round.Score - count) < .00001, "score equals fed bananas " + count);
            Check(round.Bananas == CareRound.StartingBananas - round.FeedingTotal,
                "only completed feeding consumes a banana");
            Check(!round.Complete(0, 0, CareNeed.Hunger, CareRound.Cutoff + 5), "completion after cutoff rejected");
            round.Freeze();
            Check(!round.Complete(0, 0, CareNeed.Hunger, CareRound.Cutoff - 20), "frozen round rejects changes");
        }

        // Random hunger plan: three monkeys, twelve episodes, everyone involved.
        for (int seed = 0; seed < 12; seed++)
        {
            var random = new CareRound();
            int sum = random.TasksForMonkey(0) + random.TasksForMonkey(1) + random.TasksForMonkey(2);
            Check(sum == CareRound.PlannedFeedings,
                "hunger plan totals twelve episodes (run " + seed + ")");
            Check(random.TasksForMonkey(0) >= 2 && random.TasksForMonkey(1) >= 2 &&
                  random.TasksForMonkey(2) >= 2,
                "every monkey keeps several hunger episodes (run " + seed + ")");
        }

        var exact = new CareRound();
        Check(exact.Complete(0, 0, CareNeed.Hunger, CareRound.Cutoff), "cutoff equality accepted before freeze");
        Check(exact.TaskTotal == CareRound.PlannedFeedings && exact.TaskTotal > 9,
            "feeding-only rounds queue more than nine hunger episodes");
        Check(CareRound.Cutoff <= 130 && CareRound.Cutoff >= 90,
            "the countdown is shorter so the queue outlives the clock");
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
        Check(penalties.FeedingTotal > CareRound.ThreeStarFeedings &&
              penalties.Stars == CareRound.MaxStars &&
              penalties.NextStarFeedings == 0,
            "bananas past the ninth still cap the rating at three stars");
        Check(penalties.ResolvedPercent > 99.999, "full run reports one hundred percent");

        var passing = new CareRound();
        for (int i = 0; i < CareRound.TwoStarFeedings; i++)
        {
            int monkey = 0, task = i;
            while (task >= passing.TasksForMonkey(monkey)) task -= passing.TasksForMonkey(monkey++);
            Check(passing.Complete(monkey, task, CareNeed.Hunger, 100), "sixtieth-percent feed completes");
        }
        Check(passing.IsPassing && passing.Stars == 2, "six bananas pass the round with two stars");

        var oneStar = new CareRound();
        for (int i = 0; i < CareRound.OneStarFeedings; i++)
        {
            int monkey = 0, task = i;
            while (task >= oneStar.TasksForMonkey(monkey)) task -= oneStar.TasksForMonkey(monkey++);
            Check(oneStar.Complete(monkey, task, CareNeed.Hunger, 100), "three-banana feed completes");
        }
        Check(oneStar.Stars == 1 && !oneStar.IsPassing, "three bananas already earn one star");

        var threeStar = new CareRound();
        for (int i = 0; i < CareRound.ThreeStarFeedings; i++)
        {
            int monkey = 0, task = i;
            while (task >= threeStar.TasksForMonkey(monkey)) task -= threeStar.TasksForMonkey(monkey++);
            Check(threeStar.Complete(monkey, task, CareNeed.Hunger, 100), "nine-banana feed completes");
        }
        Check(threeStar.Stars == 3 && threeStar.FeedingRatio >= 1,
            "nine bananas fill the bar and earn three stars");

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
