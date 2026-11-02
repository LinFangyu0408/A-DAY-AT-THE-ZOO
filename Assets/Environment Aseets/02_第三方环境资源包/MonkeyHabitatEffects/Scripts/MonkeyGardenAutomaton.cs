using System;

namespace ADayAtTheZoo.Habitat
{
    /// <summary>
    /// Four-state, synchronous Moore-neighbour garden adapted from Sky City's
    /// SkyCityGardenAutomaton. It deliberately has no Unity object dependency.
    /// </summary>
    public sealed class MonkeyGardenAutomaton
    {
        public enum Phase : byte
        {
            Rest,
            Bud,
            Bloom,
            Recover
        }

        public readonly int Width;
        public readonly int Height;
        public readonly bool[] Soil;
        public Phase[] Cells { get; private set; }
        public float[] Ages { get; private set; }

        private Phase[] next;
        private float[] nextAges;

        public int Generation { get; private set; }
        public int BloomCount { get; private set; }
        public int Awakened { get; private set; }

        public MonkeyGardenAutomaton(int width, int height, bool[] soil)
        {
            if (width < 1 || height < 1 || soil == null || soil.Length != width * height)
                throw new ArgumentException("Garden dimensions do not match its soil mask.");

            Width = width;
            Height = height;
            Soil = (bool[])soil.Clone();
            Cells = new Phase[soil.Length];
            Ages = new float[soil.Length];
            next = new Phase[soil.Length];
            nextAges = new float[soil.Length];
        }

        public bool Sow(int x, int y, int radius = 1)
        {
            bool changed = false;
            for (int z = Math.Max(0, y - radius); z <= Math.Min(Height - 1, y + radius); z++)
            {
                for (int a = Math.Max(0, x - radius); a <= Math.Min(Width - 1, x + radius); a++)
                {
                    int index = z * Width + a;
                    if (!Soil[index])
                        continue;
                    Cells[index] = Phase.Bud;
                    Ages[index] = 0f;
                    changed = true;
                }
            }
            return changed;
        }

        public void Tick(float dt, int threshold, float budSeconds, float bloomSeconds, float recoverySeconds)
        {
            threshold = Math.Max(1, Math.Min(4, threshold));
            dt = Math.Max(0f, dt);
            BloomCount = 0;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int index = y * Width + x;
                    Phase phase = Cells[index];
                    float age = Ages[index] + dt;
                    if (!Soil[index])
                    {
                        next[index] = Phase.Rest;
                        nextAges[index] = 0f;
                        continue;
                    }

                    int blooms = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < Width && ny >= 0 && ny < Height &&
                                Cells[ny * Width + nx] == Phase.Bloom)
                                blooms++;
                        }
                    }

                    if (phase == Phase.Rest && blooms >= threshold)
                    {
                        phase = Phase.Bud;
                        age = 0f;
                        Awakened++;
                    }
                    else if (phase == Phase.Bud && age >= Math.Max(0.2f, budSeconds))
                    {
                        phase = Phase.Bloom;
                        age = 0f;
                    }
                    else if (phase == Phase.Bloom && age >= Math.Max(0.2f, bloomSeconds))
                    {
                        phase = Phase.Recover;
                        age = 0f;
                    }
                    else if (phase == Phase.Recover && age >= Math.Max(0.2f, recoverySeconds))
                    {
                        phase = Phase.Rest;
                        age = 0f;
                    }

                    next[index] = phase;
                    nextAges[index] = age;
                    if (phase == Phase.Bloom)
                        BloomCount++;
                }
            }

            Phase[] oldCells = Cells;
            Cells = next;
            next = oldCells;
            float[] oldAges = Ages;
            Ages = nextAges;
            nextAges = oldAges;
            Generation++;
        }
    }
}
