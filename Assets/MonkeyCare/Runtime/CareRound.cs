using System;
using System.Collections.Generic;

namespace ADayAtTheZoo.Monkeys.Care
{
    public sealed class CareRound
    {
        public const int MixedTasksPerMonkey = 6;
        // Three monkeys ask for food at random; nine hunger episodes in total
        // complete the round. Each monkey keeps at least two episodes so all
        // of them stay involved, the rest is dealt randomly per round.
        public const int PlannedFeedings = 9;
        public const int PlannedScratches = 9;
        public const int PlannedTasks = 18;
        public const int StartingBananas = 10;
        public const int BananaPickupAmount = 10;
        // 60% of the bar is the passing line; stars scale with the ratio.
        public const float PassingRatio = .6f;
        public const float TwoStarRatio = .8f;
        // Shortened round: two and a half minutes keeps the nine random
        // hunger episodes comfortably reachable while staying snappy.
        public const float Cutoff = 150;
        public const float Duration = 160;
        public bool FeedingOnly { get; }
        // Tasks are dealt by DealFeedingTasks(), never inside the constructor:
        // UnityEngine.Random throws when called from a MonoBehaviour field
        // initializer, which used to abort the rest of the session's fields.
        readonly int[] feedingTasks = { PlannedFeedings / 3, PlannedFeedings / 3, PlannedFeedings / 3 };
        public int TaskTotal => FeedingOnly ? PlannedFeedings : PlannedTasks;
        public int[] Feedings { get; } = new int[3];
        public int[] Scratches { get; } = new int[3];
        public int Bananas { get; private set; } = StartingBananas;
        public int PatiencePenalties { get; private set; }
        public bool Frozen { get; private set; }
        readonly HashSet<string> completedIds = new();
        readonly HashSet<string> penaltyIds = new();
        readonly CareNeed[] firstNeeds;
        public CareRound(CareNeed[] startingNeeds = null, bool feedingOnly = true)
        {
            FeedingOnly = feedingOnly;
            firstNeeds = feedingOnly ? new[] { CareNeed.Hunger, CareNeed.Hunger, CareNeed.Hunger } :
                startingNeeds != null && startingNeeds.Length == 3 ? (CareNeed[])startingNeeds.Clone() :
                new[] { CareNeed.Hunger, CareNeed.Hunger, CareNeed.Hunger };
        }
        // Deal the nine hunger episodes across the three monkeys. Called from
        // Awake/Start so Unity's random source is safe to use.
        public void DealFeedingTasks()
        {
            if (!FeedingOnly) return;
            int[] tasks = { 2, 2, 2 };
            for (int i = 0; i < PlannedFeedings - tasks[0] - tasks[1] - tasks[2]; i++)
                tasks[UnityEngine.Random.Range(0, 3)]++;
            for (int i = 0; i < feedingTasks.Length; i++) feedingTasks[i] = tasks[i];
        }
        public int FeedingTotal => Feedings[0] + Feedings[1] + Feedings[2];
        public int ScratchTotal => Scratches[0] + Scratches[1] + Scratches[2];
        public int CompletedTotal => FeedingTotal + ScratchTotal;
        public int TasksForMonkey(int monkey)
        {
            int index = Math.Max(0, Math.Min(2, monkey));
            return FeedingOnly ? feedingTasks[index] : MixedTasksPerMonkey;
        }
        // Every banana fed raises the score one point; the top progress bar is
        // exactly this value. Patience penalties are recorded for the report
        // but no longer deduct from the displayed rating.
        public double Score => Math.Max(0, Math.Min(TaskTotal, FeedingTotal));
        public double FeedingRatio => TaskTotal == 0 ? 0 : (double)FeedingTotal / TaskTotal;
        public int PassingFeedings => (int)Math.Ceiling(PassingRatio * TaskTotal);
        public bool IsPassing => FeedingRatio >= PassingRatio;
        public double ResolvedPercent => FeedingOnly
            ? 100.0 * FeedingTotal / PlannedFeedings
            : 100.0 * ScratchTotal / PlannedScratches;
        public int Stars => FeedingRatio >= 1 ? 3 : FeedingRatio >= TwoStarRatio ? 2 :
            FeedingRatio >= PassingRatio ? 1 : 0;
        public double NextStarPoints => Stars == 3 ? 0 : Math.Max(0,
            (int)Math.Ceiling((Stars == 0 ? PassingRatio : Stars == 1 ? TwoStarRatio : 1f) * TaskTotal) - FeedingTotal);

        public bool Complete(int monkey, int task, CareNeed need, float timestamp)
        {
            if (Frozen || monkey < 0 || monkey > 2 || task < 0 ||
                task >= TasksForMonkey(monkey) || timestamp > Cutoff + .00001f) return false;
            CareNeed expected = FeedingOnly ? CareNeed.Hunger :
                task % 2 == 0 ? firstNeeds[monkey] : firstNeeds[monkey] == CareNeed.Hunger ? CareNeed.Itch : CareNeed.Hunger;
            if (need != expected || task != Feedings[monkey] + Scratches[monkey]) return false;
            if (!completedIds.Add(monkey + ":" + task)) return false;
            if (need == CareNeed.Hunger) Feedings[monkey]++;
            else Scratches[monkey]++;
            return true;
        }
        public bool RegisterPatiencePenalty(int monkey, int task)
        {
            if (Frozen || monkey < 0 || monkey > 2 || task < 0 || !penaltyIds.Add(monkey + ":" + task))
                return false;
            PatiencePenalties++;
            return true;
        }
        public bool ConsumeBanana()
        {
            if (Frozen || Bananas <= 0) return false;
            Bananas--;
            return true;
        }
        public bool RefillBananas(int amount = BananaPickupAmount)
        {
            if (Frozen || Bananas >= amount || amount <= 0) return false;
            Bananas = amount;
            return true;
        }
        public void Freeze() { Frozen = true; }
        // The next hunger episode follows shortly after the previous one; no
        // hidden long gates, so requests keep coming while the round runs.
        public static float NextTime(float completedAt, int nextTaskIndex, bool rescued, float delay = 18, float recovery = 4)
        {
            return completedAt + delay + (rescued ? recovery : 0);
        }
    }

    [Serializable]
    public sealed class CareTaskRecord
    {
        public int monkey, task;
        public string need;
        public float appearedAt, startedAt = -1, rescueAt = -1, completedAt = -1;
        public float effectiveFeedingSeconds;
        public int effectiveStrokes;
        public float switchDelay;
        public int consecutiveRescues;
    }
}
