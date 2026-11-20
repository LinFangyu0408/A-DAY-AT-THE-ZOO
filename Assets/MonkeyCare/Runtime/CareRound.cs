using System;
using System.Collections.Generic;

namespace ADayAtTheZoo.Monkeys.Care
{
    public sealed class CareRound
    {
        public const int MixedTasksPerMonkey = 6;
        // Four hunger episodes per monkey (twelve in total). The queue must
        // outlive the clock: with more episodes than a player can realistically
        // finish there is never a stretch near the end with nobody to feed.
        public const int PlannedFeedings = 12;
        public const int FeedingsPerMonkey = PlannedFeedings / 3;
        public const int PlannedScratches = 9;
        public const int PlannedTasks = 18;
        public const int StartingBananas = 10;
        public const int BananaPickupAmount = 10;
        // Star tiers are absolute banana counts so the bar, the passing line and
        // the result card can never disagree: 0-2 none, 3-5 one, 6-8 two, 9+ three.
        // Three stars stay the ceiling: feeding a tenth or twelfth banana is pure
        // bonus and can never push the rating past three.
        public const int MaxStars = 3;
        public const int OneStarFeedings = 3;
        public const int TwoStarFeedings = 6;
        public const int ThreeStarFeedings = 9;
        // The top bar measures progress towards the third star, so a full bar and
        // three stars always happen together. The passing line sits on the
        // two-star threshold (six of nine bananas).
        public const int ScoreGoal = ThreeStarFeedings;
        public const float PassingRatio = (float)TwoStarFeedings / ScoreGoal;
        // Shortened round: two minutes. Twelve queued episodes mean the hunger
        // rotation is still handing out targets when the whistle blows.
        public const float Cutoff = 120;
        public const float Duration = 130;
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
        // Deal the hunger episodes evenly across the three monkeys. Called from
        // Awake/Start so Unity's random source is safe to use.
        public void DealFeedingTasks()
        {
            if (!FeedingOnly) return;
            for (int i = 0; i < feedingTasks.Length; i++) feedingTasks[i] = FeedingsPerMonkey;
        }
        public int FeedingTotal => Feedings[0] + Feedings[1] + Feedings[2] + TutorialFeedings;
        public int ScratchTotal => Scratches[0] + Scratches[1] + Scratches[2];
        public int CompletedTotal => FeedingTotal + ScratchTotal;
        // The guided tutorial banana is a real feeding: it scores and it moves
        // the top progress bar, so the opening lesson is never "wasted".
        public int TutorialFeedings { get; private set; }
        public int TasksForMonkey(int monkey)
        {
            int index = Math.Max(0, Math.Min(2, monkey));
            return FeedingOnly ? feedingTasks[index] : MixedTasksPerMonkey;
        }
        // Every banana fed raises the score one point; the top progress bar is
        // exactly this value. Patience penalties are recorded for the report
        // but no longer deduct from the displayed rating.
        public double Score => Math.Max(0, Math.Min(TaskTotal, FeedingTotal));
        // The bar tracks the three-star goal, never the (larger) number of
        // queued episodes, so 100% and the third star land together.
        public double FeedingRatio => ScoreGoal == 0 ? 0
            : Math.Min(1.0, (double)FeedingTotal / ScoreGoal);
        public int PassingFeedings => TwoStarFeedings;
        public bool IsPassing => FeedingTotal >= TwoStarFeedings;
        public double ResolvedPercent => FeedingOnly
            ? Math.Min(100.0, 100.0 * FeedingTotal / ScoreGoal)
            : 100.0 * ScratchTotal / PlannedScratches;
        public int Stars => Math.Min(MaxStars, FeedingTotal >= ThreeStarFeedings ? 3 :
            FeedingTotal >= TwoStarFeedings ? 2 :
            FeedingTotal >= OneStarFeedings ? 1 : 0);
        public int NextStarFeedings => Stars >= MaxStars ? 0 :
            (Stars == 0 ? OneStarFeedings : Stars == 1 ? TwoStarFeedings : ThreeStarFeedings) - FeedingTotal;
        public int NextStarPoints => NextStarFeedings;
        public bool RegisterTutorialFeeding()
        {
            if (Frozen || !FeedingOnly || TutorialFeedings >= 1) return false;
            TutorialFeedings++;
            return true;
        }

        public bool Complete(int monkey, int task, CareNeed need, float timestamp)
        {
            // A banana swallowed a heartbeat after the whistle still counts: the
            // resolution timestamp can land a fraction past the cutoff because
            // feeding is sampled once per frame.
            if (Frozen || monkey < 0 || monkey > 2 || task < 0 ||
                task >= TasksForMonkey(monkey) || timestamp > Cutoff + .75f) return false;
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
