using UnityEngine;

namespace ADayAtTheZoo.Monkeys.Care
{
    public static class CareFont
    {
        static Font cached;

        public static Font Load()
        {
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
    }
}
