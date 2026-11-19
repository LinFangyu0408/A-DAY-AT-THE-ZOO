using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ADayAtTheZoo.Monkeys.Care
{
    // Authoring-independent UI skin. The project ships a small, curated set of
    // Kenney CC0 slices so the runtime UI keeps the same rounded, casual style
    // in the editor, Windows build and headset without relying on the Asset
    // Store import being present.
    public static class CareUiSkin
    {
        static readonly Dictionary<string, Sprite> cache = new();
        static readonly List<Sprite> casual = new();
        static bool casualScanned;

        public static Sprite Panel => Casual("panel", "window", "frame", "background", "board") ??
            Load("ui_panel_depth", new Vector4(20, 20, 20, 20));
        public static Sprite LightPanel => Casual("panel", "window", "frame", "background", "board") ??
            Load("ui_panel_light", new Vector4(14, 14, 14, 14));
        public static Sprite Header => Casual("header", "title", "ribbon", "tab") ??
            Load("ui_button_blue", new Vector4(24, 24, 24, 24));
        public static Sprite Button => Casual("button", "btn") ??
            Load("ui_button_blue", new Vector4(24, 24, 24, 24));
        public static Sprite RoundButton => Casual("round", "circle", "badge") ??
            Load("ui_round_blue");
        public static Sprite Star => Casual("star") ?? Load("ui_star");
        public static Sprite StarOutline => Casual("star_outline", "star_empty", "star empty") ??
            Load("ui_star_empty");
        public static Sprite DownArrow => Casual("arrow", "down") ?? Load("arrow_basic_s");
        public static Sprite Emblem => Casual("emblem", "badge", "medal", "logo", "icon") ?? Star;
        public static Sprite Slot => Casual("slot", "item", "square") ?? Panel;
        public static Sprite SelectedSlot => Casual("select", "focus", "highlight") ?? Slot;
        public static Sprite Banana => Casual("banana") ?? Casual("fruit") ??
            Load("casual_banana") ?? DownArrow;
        public static Sprite BlueButton => Casual("blue") ?? Load("ui_button_blue",
            new Vector4(24, 24, 24, 24));
        public static Sprite GreenButton => Casual("green") ?? Load("ui_button_green",
            new Vector4(24, 24, 24, 24));
        public static Sprite YellowButton => Casual("yellow") ?? Load("ui_button_yellow",
            new Vector4(24, 24, 24, 24));
        public static Sprite RedButton => Casual("red") ?? Load("ui_button_red",
            new Vector4(24, 24, 24, 24));
        public static Sprite ProgressTrack => Load("ui_progress_track",
            new Vector4(20, 2, 20, 2));
        public static Sprite ProgressFill => Load("ui_progress_fill",
            new Vector4(20, 2, 20, 2));
        public static Sprite Divider => Load("ui_divider");
        public static Sprite BackpackPanel => LoadBackpack("bag");
        public static Sprite BackpackBanana => LoadBackpack("banana");
        public static Sprite BackpackBackScratcher => LoadBackpack("back_scratcher");
        public static Sprite BackpackCount => LoadBackpack("number");
        public static Sprite BackpackButton => LoadBackpack("button_a");
        public static Sprite BackpackIcon => LoadBackpack("bag_icon");
        public static Sprite SvgScorePanel => LoadSvg("stamina");
        public static Sprite SvgScoreFill => LoadSvg("score_fill");
        public static Sprite SvgBananaPanel => LoadSvg("banana");
        public static Sprite SvgHappy => LoadSvg("happy");
        public static Sprite SvgSad => LoadSvg("sad");
        public static Sprite SvgSatisfactionPanel => LoadSvg("satisfaction");
        public static Sprite SvgStaminaPanel => LoadSvg("satisfaction");
        public static Sprite SvgSatisfactionFill => LoadSvg("progress_satisfaction");
        public static Sprite SvgStaminaFill => LoadSvg("progress_stamina");
        public static Sprite SvgSatisfactionLogo => LoadSvg("satisfaction_logo");
        public static Sprite SvgStaminaLogo => LoadSvg("stamina_logo");
        public static Sprite SvgReadyGo => LoadSvg("readygo");
        public static Sprite SvgResultsPanel => LoadSvg("results_blank");
        public static Sprite SvgContinue => LoadSvg("continue");
        public static Sprite SvgRestart => LoadSvg("restart");
        public static Sprite SvgStar => LoadSvg("star");
        public static Sprite SvgLevel1 => LoadSvg("level1");
        public static Sprite SvgLevel2 => LoadSvg("level2");
        public static Sprite SvgLevel3 => LoadSvg("level3");

        public static Image Add(Transform parent, string name, Sprite sprite,
            Vector2 size, Vector2 position, Color color)
        {
            var image = CareUi.Image(name, parent, sprite, size, position);
            image.color = color;
            image.raycastTarget = false;
            if (sprite) image.type = Image.Type.Simple;
            return image;
        }

        public static Image AddSliced(Transform parent, string name, Sprite sprite,
            Vector2 size, Vector2 position, Color color)
        {
            var image = Add(parent, name, sprite, size, position, color);
            if (sprite && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            return image;
        }

        static Sprite Load(string fileName, Vector4 border = default)
        {
            string key = fileName + border;
            if (cache.TryGetValue(key, out var existing) && existing) return existing;
            var texture = Resources.Load<Texture2D>("UI/Kenney/" + fileName);
            if (!texture)
            {
                Debug.LogWarning("[Monkey Care] Missing UI texture: UI/Kenney/" + fileName);
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = "Care UI - " + fileName;
            cache[key] = sprite;
            return sprite;
        }

        static Sprite LoadBackpack(string fileName)
        {
            string key = "UI/Backpack/" + fileName;
            if (cache.TryGetValue(key, out var existing) && existing) return existing;
            var texture = Resources.Load<Texture2D>(key);
            if (!texture)
            {
                Debug.LogWarning("[Monkey Care] Missing backpack texture: " + key);
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "Care Backpack - " + fileName;
            cache[key] = sprite;
            return sprite;
        }

        static Sprite LoadSvg(string fileName)
        {
            string key = "UI/SVG/" + fileName;
            if (cache.TryGetValue(key, out var existing) && existing) return existing;
            var texture = Resources.Load<Texture2D>(key);
            if (!texture)
            {
                Debug.LogWarning("[Monkey Care] Missing SVG-derived texture: " + key);
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "Care SVG - " + fileName;
            cache[key] = sprite;
            return sprite;
        }

        static Sprite Casual(params string[] keywords)
        {
            ScanCasual();
            foreach (string keyword in keywords)
                foreach (var sprite in casual)
                    if (sprite && sprite.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return sprite;
            return null;
        }

        static void ScanCasual()
        {
            if (casualScanned) return;
            casualScanned = true;
            foreach (var sprite in Resources.LoadAll<Sprite>("UI/CasualHD"))
                if (sprite) casual.Add(sprite);
            foreach (var texture in Resources.LoadAll<Texture2D>("UI/CasualHD"))
            {
                if (!texture) continue;
                bool alreadyPresent = false;
                foreach (var existing in casual)
                    if (existing && existing.texture == texture) { alreadyPresent = true; break; }
                if (alreadyPresent) continue;
                var created = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                created.name = texture.name;
                casual.Add(created);
            }
            if (casual.Count > 0)
                Debug.Log("[Monkey Care] 2D Casual UI HD skin found: " + casual.Count + " sprites.");
        }
    }
}
