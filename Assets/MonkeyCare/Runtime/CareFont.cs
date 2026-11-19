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

        // Gameplay instructions must read clearly at a glance. Fredoka-Variable
        // is a variable font whose Bold weight Unity's legacy Text ignores, so
        // it renders hairline-thin in VR. Prefer a genuine static bold face from
        // the OS instead: "Arial Bold" for Latin, "Microsoft YaHei UI Bold" for
        // CJK. These have real bold glyphs, so FontStyle.Bold now looks heavy.
        public static Font LoadReadable()
        {
            if (readable) return readable;
            // Shipped static bold face first: real bold glyphs that Unity's
            // legacy Text renders heavy (unlike the variable Fredoka font).
            readable = Resources.Load<Font>("Fonts/Arial-Bold");
            if (readable) return readable;
            // Otherwise try a genuine bold OS family by exact name.
            string[] installed = Font.GetOSInstalledFontNames();
            string[] boldPreferred =
            {
                "Arial Bold", "Microsoft YaHei UI Bold", "Microsoft YaHei Bold",
                "Segoe UI Bold", "Noto Sans SC Bold"
            };
            foreach (string candidate in boldPreferred)
                foreach (string available in installed)
                    if (string.Equals(available, candidate, System.StringComparison.OrdinalIgnoreCase))
                    {
                        readable = Font.CreateDynamicFontFromOSFont(candidate, 36);
                        if (readable) return readable;
                    }
            // Fall back to regular system faces, then the shipped themed font.
            string[] preferred =
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial",
                "Noto Sans CJK SC", "Noto Sans SC", "PingFang SC"
            };
            foreach (string candidate in preferred)
                foreach (string available in installed)
                    if (string.Equals(available, candidate, System.StringComparison.OrdinalIgnoreCase))
                    {
                        readable = Font.CreateDynamicFontFromOSFont(candidate, 36);
                        if (readable) return readable;
                    }
            readable = Resources.Load<Font>("Fonts/Fredoka-Variable");
            if (readable) return readable;
            readable = Load();
            return readable;
        }
    }
}
