using System;
using System.Collections.Generic;

namespace ADayAtTheZoo.Monkeys.Care
{
    public sealed class CareRound
    {
        public const int FeedingTasksPerMonkey = 3;
        public const int MixedTasksPerMonkey = 6;
        public const int PlannedFeedings = 9;
        public const int PlannedScratches = 9;
        public const int PlannedTasks = 18;
        public const float Cutoff = 230;
        public const float Duration = 240;
        public bool FeedingOnly { get; }
        public int TasksPerMonkey => FeedingOnly ? FeedingTasksPerMonkey : MixedTasksPerMonkey;
        public int TaskTotal => FeedingOnly ? PlannedFeedings : PlannedTasks;
        public int[] Feedings { get; } = new int[3];
        public int[] Scratches { get; } = new int[3];
        public int Bananas { get; private set; } = 12;
        public bool Frozen { get; private set; }
        readonly HashSet<string> completedIds = new();
        readonly CareNeed[] firstNeeds;
        public CareRound(CareNeed[] startingNeeds = null, bool feedingOnly = true)
        {
            FeedingOnly = feedingOnly;
            firstNeeds = feedingOnly ? new[] { CareNeed.Hunger, CareNeed.Hunger, CareNeed.Hunger } :
                startingNeeds != null && startingNeeds.Length == 3 ? (CareNeed[])startingNeeds.Clone() :
                new[] { CareNeed.Hunger, CareNeed.Hunger, CareNeed.Hunger };
        }
        public int FeedingTotal => Feedings[0] + Feedings[1] + Feedings[2];
        public int ScratchTotal => Scratches[0] + Scratches[1] + Scratches[2];
        public int CompletedTotal => FeedingTotal + ScratchTotal;
        public double Score => 48.0 + 52.0 * CompletedTotal / TaskTotal;
        public double ResolvedPercent => FeedingOnly
            ? 100.0 * FeedingTotal / PlannedFeedings
            : 100.0 * ScratchTotal / PlannedScratches;
        public int Stars => CompletedTotal == 0 ? 0 :
            CompletedTotal <= (FeedingOnly ? 5 : 12) ? 1 :
            CompletedTotal < TaskTotal ? 2 : 3;
        public double NextStarPoints => Stars == 3 ? 0 : Math.Max(0, (Stars == 0 ? 50 : Stars == 1 ? 85 : 100) - Score);

        public bool Complete(int monkey, int task, CareNeed need, float timestamp)
        {
            if (Frozen || monkey < 0 || monkey > 2 || task < 0 || task >= TasksPerMonkey || timestamp > Cutoff + .00001f) return false;
            CareNeed expected = FeedingOnly ? CareNeed.Hunger :
                task % 2 == 0 ? firstNeeds[monkey] : firstNeeds[monkey] == CareNeed.Hunger ? CareNeed.Itch : CareNeed.Hunger;
            if (need != expected || task != Feedings[monkey] + Scratches[monkey]) return false;
            if (!completedIds.Add(monkey + ":" + task)) return false;
            if (need == CareNeed.Hunger) Feedings[monkey]++;
            else Scratches[monkey]++;
            return true;
        }
        public bool ConsumeBanana()
        {
            if (Frozen || Bananas <= 0) return false;
            Bananas--;
            return true;
        }
        public void Freeze() { Frozen = true; }
        public static float NextTime(float completedAt, int nextTaskIndex, bool rescued, float delay = 18, float recovery = 4)
        {
            float earliest = nextTaskIndex == 2 ? 85 : nextTaskIndex == 4 ? 155 : 0;
            return Math.Max(completedAt + delay + (rescued ? recovery : 0), earliest);
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
