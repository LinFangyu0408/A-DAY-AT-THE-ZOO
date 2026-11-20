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
        // Extra-large corner radius white plate for the results card. The
        // Casual HD panel only has a subtle radius; this procedural sprite
        // gives the card a clearly rounded, friendly silhouette. The 96px
        // radius stays visible even on a tall (880x1290) card.
        public static Sprite RoundedCard => Rounded("RoundedCard", 256, 96);
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
        // The reference HUD uses the new game-UI slice set: a white SCORE plate
        // with a baked rainbow bar. The bar region is re-sliced at runtime so
        // the live progress fill reuses the exact gradient.
        public static Sprite SvgScorePanel => LoadGameUi("UI SCORE") ?? LoadSvg("stamina");
        public static Sprite SvgScoreFill =>
            LoadGameUiRect("UI SCORE", new Rect(50f, 72f, 738f, 74f)) ?? LoadSvg("score_fill");
        public static Sprite SvgBananaPanel => LoadGameUi("banana") ?? LoadSvg("banana");
        public static Sprite SvgStaminaBar => LoadGameUi("progress bar background STAMINA");
        public static Sprite SvgHappy => LoadSvg("happy");
        public static Sprite SvgSad => LoadSvg("sad");
        public static Sprite SvgSatisfactionPanel => LoadSvg("satisfaction");
        // The old SVG set had no dedicated STAMINA panel and mistakenly reused
        // the satisfaction art. The newer game-UI slice set ships a proper one.
        public static Sprite SvgStaminaPanel => LoadGameUi("STAMINA") ?? LoadSvg("stamina");
        public static Sprite SvgSatisfactionFill => LoadSvg("progress_satisfaction");
        public static Sprite SvgStaminaFill => LoadSvg("progress_stamina");
        public static Sprite SvgSatisfactionLogo => LoadSvg("satisfaction_logo");
        public static Sprite SvgStaminaLogo => LoadGameUi("STAMINA LOGO") ?? LoadSvg("stamina_logo");
        // Numbered star badges (1-5) from the new game-UI slice set.
        public static Sprite SvgNumberStar(int number) => LoadGameUi(number.ToString());
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

        // Every controller key gets its own colour so a prompt can never be
        // mistaken for body copy: A green, B red, RT orange, sticks blue.
        public static Color KeyColor(string key)
        {
            switch ((key ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "A": case "✕": case "X": return new Color(.20f, .72f, .36f, 1f);
                case "B": case "○": case "O": return new Color(.90f, .24f, .32f, 1f);
                case "RT": return new Color(.98f, .62f, .12f, 1f);
                case "LT": return new Color(.98f, .45f, .16f, 1f);
                case "L":
                case "R": return new Color(.24f, .58f, .95f, 1f);
                case "GO": return new Color(1f, .72f, .16f, 1f);
                default: return new Color(.16f, .34f, .55f, 1f);
            }
        }

        // Round key cap with the printed label, the visual language used by the
        // reference artwork (a letter inside a coloured circle). The cap is
        // generated at 256px: a 128px cap looked soft once the HUD plates were
        // scaled down for the flat layout (the badge edge fuzzed out).
        public static Sprite KeyCapSprite => Rounded("KeyCap", 256, 128);
        static readonly System.Collections.Generic.List<(Text label, string key)> liveKeyLabels = new();
        static bool labelsHooked;

        public static Image KeyBadge(Transform parent, string key, Vector2 size,
            Vector2 position, out Text label, int fontSize = 44, string objectName = null)
        {
            // A plain white cap keeps the tint exact; the casual art carries its
            // own colour and would multiply the key tint into a muddy tone.
            var badge = Add(parent, string.IsNullOrEmpty(objectName) ? "Key " + key : objectName,
                KeyCapSprite, size, position, KeyColor(key));
            label = CareRoundView.Label("Key " + key + " label", badge.transform,
                key, size * .94f, Vector2.zero, fontSize, TextAnchor.MiddleCenter, Color.white);
            // Best-fit re-samples the glyph at whatever size happens to fit, so
            // a single letter inside a small cap came out soft and uneven. The
            // badge always renders one glyph: keep the authored size exactly and
            // give it a tight dark edge so it reads against the coloured cap.
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var glyphEdge = label.GetComponent<Outline>();
            if (glyphEdge)
            {
                glyphEdge.effectColor = new Color(.10f, .02f, .05f, .62f);
                glyphEdge.effectDistance = new Vector2(1.6f, -1.6f);
            }
            // The glyph set can be swapped at runtime (A/B ⇄ ✕/○); register so a
            // swap relabels every badge without touching layout or wiring.
            if (!labelsHooked)
            {
                labelsHooked = true;
                CareInput.LabelsChanged += () =>
                {
                    for (int i = liveKeyLabels.Count - 1; i >= 0; i--)
                    {
                        var pair = liveKeyLabels[i];
                        if (!pair.label) { liveKeyLabels.RemoveAt(i); continue; }
                        pair.label.text = pair.key == "A" ? CareInput.ConfirmLabel :
                            pair.key == "B" ? CareInput.BackLabel : pair.key;
                        // Glyph stays white: the cap is already tinted with the
                        // key colour, so tinting the letter too turned the badge
                        // into a red-on-red blob (the "blurry B" report).
                        pair.label.color = Color.white;
                    }
                };
            }
            liveKeyLabels.Add((label, key));
            return badge;
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

        // Procedural rounded-rectangle sprite with a generous corner radius
        // and anti-aliased edges. Sliced borders keep the corners crisp at
        // any card size.
        static Sprite Rounded(string name, int size, int radius)
        {
            string key = "Rounded/" + name + "/" + radius;
            if (cache.TryGetValue(key, out var existing) && existing) return existing;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name };
            float half = size * .5f;
            float inner = half - radius;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                float qx = Mathf.Abs(x + .5f - half) - inner;
                float qy = Mathf.Abs(y + .5f - half) - inner;
                // Rounded-rect signed distance: distance to the inner box
                // MINUS the corner radius (the radius term is what actually
                // rounds the corners; without it the sprite is a square).
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude +
                                Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                byte a = (byte)(Mathf.Clamp01(.5f - outside) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "Care UI - " + name;
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

        static Sprite LoadGameUi(string fileName)
        {
            string key = "UI/GameUI2/" + fileName;
            if (cache.TryGetValue(key, out var existing) && existing) return existing;
            var texture = Resources.Load<Texture2D>(key);
            if (!texture)
            {
                Debug.LogWarning("[Monkey Care] Missing game-UI texture: " + key);
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "Care GameUI - " + fileName;
            cache[key] = sprite;
            return sprite;
        }

        // Slices a sub-region of a game-UI texture (y measured from the bottom,
        // matching Sprite.Create) so baked artwork can double as live fills.
        static Sprite LoadGameUiRect(string fileName, Rect rect)
        {
            string key = "UI/GameUI2/" + fileName + "#" + rect;
            if (cache.TryGetValue(key, out var existing) && existing) return existing;
            var texture = Resources.Load<Texture2D>("UI/GameUI2/" + fileName);
            if (!texture)
            {
                Debug.LogWarning("[Monkey Care] Missing game-UI texture: UI/GameUI2/" + fileName);
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, rect,
                new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "Care GameUI - " + fileName + " slice";
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
