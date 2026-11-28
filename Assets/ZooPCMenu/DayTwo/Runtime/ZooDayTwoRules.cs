using System;
using UnityEngine;
using ADayAtTheZoo.Monkeys.Care;

namespace ADayAtTheZoo.PC
{
    [CreateAssetMenu(menuName = "Zoo/Day Two Rules")]
    public sealed class ZooDayTwoRules : ScriptableObject
    {
        public float duration = 180;
        public int monkeyCount = 5;
        public int tasksPerMonkey = 4;
        public int bananaCapacity = 6;
        public float patience = 28;
        public float feedSeconds = 3.5f;
        public int scratchStrokes = 3;
        public float strokeInterval = .45f;
        public float secondRequestAt = 45;
        public float requestGap = 2;
        public float scratchReach = 3;
        public float throwReach = 14;
        public int oneStar = 7, twoStars = 14, threeStars = 20;
    }

    public enum ZooCareState { Resting, Waiting, Eating, Celebrating, Done }

    public sealed class ZooDayTwoRound
    {
        public sealed class Animal
        {
            public ZooCareState state;
            public CareNeed need;
            public int completed, strokes;
            public float requestedAt, eatingTime, availableAt, lastStroke = -100;
            public bool late;
        }

        public readonly ZooDayTwoRules rules;
        public readonly Animal[] animals;
        public float Elapsed { get; private set; }
        public int Bananas { get; private set; }
        public int Feedings { get; private set; }
        public int Scratches { get; private set; }
        public int LateRequests { get; private set; }
        public int Completed => Feedings + Scratches;
        public int Total => animals.Length * rules.tasksPerMonkey;
        public bool Finished { get; private set; }
        public bool Started { get; private set; }
        public int Stars => Completed >= rules.threeStars ? 3 : Completed >= rules.twoStars ? 2 : Completed >= rules.oneStar ? 1 : 0;
        public bool Passing => Completed >= rules.twoStars;
        public int ActiveCount { get { int n = 0; foreach (var a in animals) if (a.state == ZooCareState.Waiting || a.state == ZooCareState.Eating) n++; return n; } }
        int cursor = -1;
        float nextRequest = 1;

        public ZooDayTwoRound(ZooDayTwoRules settings)
        {
            rules = settings; Bananas = rules.bananaCapacity;
            animals = new Animal[Mathf.Max(1, rules.monkeyCount)];
            for (int i = 0; i < animals.Length; i++) animals[i] = new Animal();
        }
        public bool Start() { if (Started) return false; Started = true; return true; }
        public int StrokeGoal(Animal animal) => rules.scratchStrokes + (animal.late ? 1 : 0);
        public float FeedGoal(Animal animal) => rules.feedSeconds + (animal.late ? 1 : 0);
        public float Patience(Animal animal) => Mathf.Clamp01(1 - (Elapsed - animal.requestedAt) / rules.patience);
        public void Tick(float seconds)
        {
            if (!Started || Finished || seconds <= 0) return;
            float step = Mathf.Min(seconds, Mathf.Max(0, rules.duration - Elapsed));
            Elapsed += step;
            for (int i = 0; i < animals.Length; i++)
            {
                var a = animals[i];
                if (a.state == ZooCareState.Celebrating && Elapsed >= a.availableAt)
                    a.state = a.completed >= rules.tasksPerMonkey ? ZooCareState.Done : ZooCareState.Resting;
                if (a.state == ZooCareState.Waiting && !a.late && Elapsed - a.requestedAt > rules.patience)
                { a.late = true; LateRequests++; }
                if (a.state == ZooCareState.Eating)
                {
                    a.eatingTime += step;
                    if (a.eatingTime + .0001f >= FeedGoal(a)) Complete(i);
                }
            }
            if (Elapsed >= rules.duration || Completed >= Total) { Finished = true; return; }
            int limit = Elapsed >= rules.secondRequestAt ? 2 : 1;
            if (ActiveCount >= limit || Elapsed < nextRequest) return;
            for (int n = 1; n <= animals.Length; n++)
            {
                int i = (cursor + n) % animals.Length;
                var a = animals[i];
                if (a.state != ZooCareState.Resting || a.completed >= rules.tasksPerMonkey || Elapsed < a.availableAt) continue;
                a.state = ZooCareState.Waiting;
                a.need = (i + a.completed) % 2 == 0 ? CareNeed.Hunger : CareNeed.Itch;
                a.requestedAt = Elapsed; a.strokes = 0; a.eatingTime = 0; a.late = false; a.lastStroke = -100;
                cursor = i; nextRequest = Elapsed + rules.requestGap; break;
            }
        }
        public bool SpendBanana()
        {
            if (!Started || Finished || Bananas <= 0) return false;
            Bananas--; return true;
        }
        public bool Refill()
        {
            if (!Started || Finished || Bananas >= rules.bananaCapacity) return false;
            Bananas = rules.bananaCapacity; return true;
        }
        public bool ReceiveBanana(int index, int task)
        {
            if (!Valid(index)) return false;
            var a = animals[index];
            if (a.completed != task || a.state != ZooCareState.Waiting || a.need != CareNeed.Hunger) return false;
            a.state = ZooCareState.Eating; a.eatingTime = 0; return true;
        }
        public bool Stroke(int index)
        {
            if (!Valid(index)) return false;
            var a = animals[index];
            if (a.state != ZooCareState.Waiting || a.need != CareNeed.Itch || Elapsed - a.lastStroke < rules.strokeInterval) return false;
            a.lastStroke = Elapsed; a.strokes++;
            if (a.strokes >= StrokeGoal(a)) Complete(index);
            return true;
        }
        bool Valid(int index) => Started && !Finished && index >= 0 && index < animals.Length;
        void Complete(int index)
        {
            var a = animals[index];
            if (a.need == CareNeed.Hunger) Feedings++; else Scratches++;
            a.completed++; a.state = ZooCareState.Celebrating; a.availableAt = Elapsed + 3;
            nextRequest = Mathf.Min(nextRequest, Elapsed + rules.requestGap);
        }
    }
}
