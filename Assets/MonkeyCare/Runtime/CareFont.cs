using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    public static class CareFont
    {
        static Font cached;
        static Font readable;

        public static Font Load()
        {
            if (cached) return cached;
            cached = Resources.Load<Font>("Fonts/LemiJiquTi");
            if (cached) return cached;
            string[] preferred =
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "SimHei",
                "Noto Sans CJK SC", "Noto Sans SC", "PingFang SC"
            };
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string candidate in preferred)
            {
                foreach (string available in installed)
                {
                    if (available != candidate) continue;
                    cached = Font.CreateDynamicFontFromOSFont(candidate, 36);
                    if (cached) return cached;
                }
            }
            cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!cached) cached = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return cached;
        }

        // Gameplay instructions need a steady, highly legible Chinese face.
        // Keep the themed font for headings, but prefer the Windows UI font for
        // longer tutorial copy and small controller labels.
        public static Font LoadReadable()
        {
            if (readable) return readable;
            readable = Resources.Load<Font>("Fonts/Fredoka-Variable");
            if (readable) return readable;
            string[] preferred =
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "SimHei",
                "Noto Sans CJK SC", "Noto Sans SC", "PingFang SC"
            };
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string candidate in preferred)
            {
                foreach (string available in installed)
                {
                    if (available != candidate) continue;
                    readable = Font.CreateDynamicFontFromOSFont(candidate, 36);
                    if (readable) return readable;
                }
            }
            readable = Load();
            return readable;
        }
    }
}
