using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    public enum CareNeed { [InspectorName("饥饿")] Hunger, [InspectorName("发痒")] Itch }

    // One instance per demand; late requirements never leak into the next demand.
    public sealed class CareProgress
    {
        public float Waited { get; private set; }
        public float Fed { get; private set; }
        public int Strokes { get; private set; }
        public bool Locked { get; private set; }
        public bool Late { get; private set; }
        public bool Completed { get; private set; }
        public float CompletionOffset { get; private set; }
        public float FeedTarget { get; private set; }
        public int StrokeTarget { get; private set; }
        public float Deadline { get; }
        readonly float feedSeconds, lateFeedSeconds;
        readonly int strokes, lateStrokes;
        public CareProgress(float deadline, float feed, float lateFeed, int count, int lateCount)
        {
            Deadline = Mathf.Max(.01f, deadline);
            feedSeconds = Mathf.Max(.01f, feed); lateFeedSeconds = Mathf.Max(.01f, lateFeed);
            strokes = Mathf.Max(1, count); lateStrokes = Mathf.Max(1, lateCount);
            UpdateTargets();
        }
        void UpdateTargets()
        {
            FeedTarget = Late ? lateFeedSeconds : feedSeconds;
            StrokeTarget = Late ? lateStrokes : strokes;
        }
        public void Wait(float delta) => Advance(delta, false);
        public bool Feed(float delta) => Advance(delta, true);
        // Split a frame at the deadline: completion exactly at L still counts as normal.
        public bool Advance(float delta, bool feeding)
        {
            if (Completed || delta <= 0) return false;
            float elapsed = 0;
            while (delta > .000001f)
            {
                if (!Late && Waited >= Deadline - .000001f) { Late = true; UpdateTargets(); }
                float chunk = Late ? delta : Mathf.Min(delta, Deadline - Waited);
                if (feeding)
                {
                    Locked = true;
                    float remaining = Mathf.Max(0, FeedTarget - Fed);
                    if (remaining <= chunk + .000001f)
                    {
                        Waited += remaining; Fed = FeedTarget; Completed = true;
                        CompletionOffset = elapsed + remaining; return true;
                    }
                    Fed += chunk;
                }
                Waited += chunk; elapsed += chunk; delta -= chunk;
            }
            return false;
        }
        public bool Stroke()
        {
            if (Completed) return false;
            Locked = true; Strokes = Mathf.Min(StrokeTarget, Strokes + 1);
            Completed = Strokes >= StrokeTarget; return Completed;
        }
        // Linear drain: the overhead hunger bar must visibly slide down every
        // second (a squared curve looked frozen for the first half of the wait).
        public float Patience => 1 - Mathf.Clamp01(Waited / Deadline);
        public float Satisfaction(CareNeed need) => !Locked ? 0 : Mathf.Pow(Mathf.Clamp01(
            need == CareNeed.Hunger ? Fed / FeedTarget : (float)Strokes / StrokeTarget), 2);
    }

    public sealed class ScratchStroke
    {
        bool initialized, armed = true;
        float top, bottom;
        public void Reset() { initialized = false; armed = true; }
        public bool Sample(float y, bool touching, float down, float up)
        {
            if (!touching)
            {
                // Leaving contact cancels a partial stroke, but never bypasses the upward reset.
                if (!armed)
                {
                    bottom = Mathf.Min(bottom, y);
                    if (y - bottom >= Mathf.Max(.01f, up)) armed = true;
                }
                initialized = false; return false;
            }
            if (!initialized) { top = bottom = y; initialized = true; return false; }
            if (armed)
            {
                top = Mathf.Max(top, y);
                if (top - y < Mathf.Max(.01f, down)) return false;
                armed = false; bottom = y; return true;
            }
            bottom = Mathf.Min(bottom, y);
            if (y - bottom >= Mathf.Max(.01f, up)) { armed = true; top = y; }
            return false;
        }
    }
}
