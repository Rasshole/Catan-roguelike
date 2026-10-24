using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v138 icon-first trade kit shared by the single-player trade card, the AI's incoming offer and the
    /// pass-and-play trade board (colonist.io-style layout in the painted parchment look): resource icons with
    /// numbers instead of resource words, a red "you give" arrow pointing away from you and a green "you get"
    /// arrow pointing toward you, harbour / bank rates as small icon badges (4:1, 3:1, 2:1) and ✓ / ✗ / ⇄ answers.
    /// Left-click an icon adds one card, right-click (or the − under it) removes one.
    /// </summary>
    public sealed partial class RunHudController
    {
        /// <summary>The five tradeable resources in board order (the trade rows always show all five).</summary>
        /// <summary>Kicker on the ornate frame's wood band (cream-gold reads on the dark wood).</summary>
        private static readonly Color TradeKickerInk = new(0.98f, 0.86f, 0.56f, 1f);

        /// <summary>✕ on the frame's top band, clear of the carved corner.</summary>
        private static readonly Vector2 TradeClosePos = new(-150f, -54f);
        private static readonly Vector2 TradeCloseSize = new(46f, 40f);

        public static readonly ResourceType[] TradeResources =
        {
            ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat, ResourceType.Sheep, ResourceType.Stone
        };

        private static readonly Color TradeGiveTint = new(1f, 0.86f, 0.80f, 1f);
        private static readonly Color TradeGetTint = new(0.86f, 0.96f, 0.80f, 1f);

        public const float TradeSlot = 84f;
        public const float TradeSlotPitch = 98f;

        private static Image TradeGlyph(RectTransform parent, string name, Sprite sprite, Vector2 anchor, Vector2 pos,
            float size)
        {
            var img = CreateImage(parent, name, sprite);
            img.preserveAspect = true;
            SetRect(img.rectTransform, anchor, pos, new Vector2(size, size));
            return img;
        }

        /// <summary>Warm painted button with an optional glyph (glyph only when <paramref name="label"/> is empty).</summary>
        private static Button TradeButton(RectTransform parent, string name, string label, Sprite glyph, Vector2 anchor,
            Vector2 pos, Vector2 size, bool enabled, bool green, System.Action click, int fontSize = 18)
        {
            var b = RunHudUiFactory.CreateButton(parent, label ?? "", RunHudUiFactory.ButtonFill);
            b.name = name;
            SetRect((RectTransform)b.transform, anchor, pos, size);
            ColonistKenneyHudArt.ApplyColonistWarmButton(b.GetComponent<Image>(), primaryGreen: green);
            var text = b.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.color = green ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                text.fontSize = fontSize;
                text.fontStyle = FontStyle.Bold;
            }

            if (glyph != null)
            {
                float g = Mathf.Min(size.y - 14f, 40f);
                bool alone = string.IsNullOrEmpty(label);
                var icon = TradeGlyph((RectTransform)b.transform, "Glyph", glyph, new Vector2(alone ? 0.5f : 0f, 0.5f),
                    new Vector2(alone ? 0f : 12f + g / 2f, 0f), g);
                if (!alone && text != null)
                {
                    text.rectTransform.offsetMin = new Vector2(g + 14f, 0f);
                    text.rectTransform.offsetMax = new Vector2(-8f, 0f);
                }

                if (!enabled)
                    icon.color = new Color(1f, 1f, 1f, 0.45f);
            }

            b.interactable = enabled;
            if (click != null)
                b.onClick.AddListener(() => click());
            return b;
        }

        /// <summary>
        /// Compact terms: the give / get arrow, then each resource as icon + count (only non-zero ones).
        /// Returns the width used. <paramref name="x"/> is the left edge relative to <paramref name="anchor"/>.
        /// </summary>
        private static float BuildTradeTerms(RectTransform parent, string key, bool give, ResourceBundle b,
            Vector2 anchor, float x, float y, float icon = 40f)
        {
            float cursor = x;
            TradeGlyph(parent, key + "Arrow", give ? TradeGlyphSprites.GiveArrow : TradeGlyphSprites.GetArrow, anchor,
                new Vector2(cursor + icon * 0.4f, y), icon * 0.8f);
            cursor += icon * 0.8f + 6f;
            bool any = false;
            foreach (var r in TradeResources)
            {
                int n = b[r];
                if (n <= 0)
                    continue;
                any = true;
                var img = CreateImage(parent, key + "Icon_" + r, ColonistHudResourceSprites.Get(r));
                SetRect(img.rectTransform, anchor, new Vector2(cursor + icon / 2f, y), new Vector2(icon, icon));
                var count = RunHudUiFactory.CreateText(parent, key + "N_" + r, n.ToString(), Mathf.RoundToInt(icon * 0.5f),
                    TextAnchor.MiddleLeft, give ? ColonistUiKit.InkBad : ColonistUiKit.InkGood, FontStyle.Bold);
                SetRect(count.rectTransform, anchor, new Vector2(cursor + icon + 12f, y), new Vector2(24f, icon));
                cursor += icon + 28f;
            }

            if (!any)
            {
                var none = RunHudUiFactory.CreateText(parent, key + "None", "—", Mathf.RoundToInt(icon * 0.55f),
                    TextAnchor.MiddleCenter, ColonistUiKit.InkMuted, FontStyle.Bold);
                SetRect(none.rectTransform, anchor, new Vector2(cursor + 14f, y), new Vector2(28f, icon));
                cursor += 28f;
            }

            return cursor - x;
        }

        /// <summary>Width <see cref="BuildTradeTerms"/> would use (for centring / right-aligning).</summary>
        private static float TradeTermsWidth(ResourceBundle b, float icon = 40f)
        {
            int kinds = 0;
            foreach (var r in TradeResources)
                if (b[r] > 0)
                    kinds++;
            return icon * 0.8f + 6f + (kinds == 0 ? 28f : kinds * (icon + 28f));
        }

        /// <summary>
        /// One editable row: arrow + five icon slots. Left-click adds (up to <paramref name="limit"/>), right-click or
        /// the − below removes. Buttons are named <c>{key}More_{r}</c> / <c>{key}Less_{r}</c>.
        /// </summary>
        private static void BuildTradeEditRow(RectTransform card, string key, bool give, float y, float x0,
            System.Func<ResourceBundle> get, System.Action<ResourceBundle> set, ResourceBundle limit,
            System.Action changed)
        {
            var anchor = new Vector2(0f, 1f);
            TradeGlyph(card, key + "Arrow", give ? TradeGlyphSprites.GiveArrow : TradeGlyphSprites.GetArrow, anchor,
                new Vector2(x0, y), 58f);
            for (int i = 0; i < TradeResources.Length; i++)
            {
                var r = TradeResources[i];
                float x = x0 + 50f + TradeSlot / 2f + i * TradeSlotPitch;
                int n = get()[r];
                bool canAdd = n < limit[r];

                void Add(int delta)
                {
                    var b = get();
                    int next = Mathf.Clamp(b[r] + delta, 0, limit[r]);
                    if (next == b[r])
                        return;
                    b.Set(r, next);
                    set(b);
                    changed?.Invoke();
                }

                var slot = CreateImage(card, key + "More_" + r, null);
                slot.preserveAspect = false;
                ColonistUiKit.Apply(slot, UiKitPiece.FrameThin, 1f, n > 0 ? (give ? TradeGiveTint : TradeGetTint) : Color.white);
                slot.raycastTarget = true;
                SetRect(slot.rectTransform, anchor, new Vector2(x, y), new Vector2(TradeSlot, TradeSlot));
                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slot;
                button.interactable = canAdd;
                var colors = button.colors;
                colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
                colors.disabledColor = Color.white;
                button.colors = colors;
                button.onClick.AddListener(() => Add(1));
                slot.gameObject.AddComponent<HudRightClickRelay>().OnRightClick = () => Add(-1);

                var icon = CreateImage(slot.rectTransform, "Icon", ColonistHudResourceSprites.Get(r));
                SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
                if (limit[r] <= 0 && n == 0)
                    icon.color = new Color(1f, 1f, 1f, 0.32f);

                if (n > 0)
                {
                    var badge = CreateDisc(slot.rectTransform, "Count", give ? TradeGlyphSprites.GiveRed : TradeGlyphSprites.GetGreen, 34f);
                    SetRect(badge.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(34f, 34f));
                    var t = RunHudUiFactory.CreateText(badge.rectTransform, "N", n.ToString(), 20, TextAnchor.MiddleCenter,
                        ColonistUiKit.Cream, FontStyle.Bold);
                    Stretch(t.rectTransform);
                }

                var less = TradeButton(card, key + "Less_" + r, "−", null, anchor, new Vector2(x, y - TradeSlot / 2f - 18f),
                    new Vector2(46f, 28f), n > 0, false, () => Add(-1), 20);
                less.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(0f, 2f);
            }
        }

        /// <summary>Row of rate badges: resource icon + "4:1" / "3:1" / "2:1" (harbour rates highlighted).</summary>
        private static void BuildTradeRateBadges(RectTransform card, float y, System.Func<ResourceType, int> ratio)
        {
            const float w = 100f, gap = 12f;
            float total = TradeResources.Length * w + (TradeResources.Length - 1) * gap;
            for (int i = 0; i < TradeResources.Length; i++)
            {
                var r = TradeResources[i];
                int k = ratio(r);
                float x = -total / 2f + w / 2f + i * (w + gap);
                var chip = CreateImage(card, "Rate_" + r, null);
                chip.preserveAspect = false;
                ColonistUiKit.Apply(chip, UiKitPiece.FrameThin, 1f,
                    k <= 2 ? new Color(1f, 0.74f, 0.32f, 1f) : k < 4 ? new Color(1f, 0.84f, 0.50f, 1f) : Color.white);
                SetRect(chip.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(w, 46f));
                var icon = CreateImage(chip.rectTransform, "Icon", ColonistHudResourceSprites.Get(r));
                SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(34f, 34f));
                var t = RunHudUiFactory.CreateText(chip.rectTransform, "Ratio", $"{k}:1", 19, TextAnchor.MiddleCenter,
                    k < 4 ? ColonistUiKit.InkGold : ColonistUiKit.Ink, FontStyle.Bold);
                SetRect(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(72f, 0f), new Vector2(52f, 30f));
            }
        }

        /// <summary>Bank trade from two bundles: one resource × (rate·k) for k of one other resource.</summary>
        public static bool TryReadBankTrade(ResourceBundle give, ResourceBundle get, System.Func<ResourceType, int> ratio,
            out ResourceType giveType, out ResourceType getType, out int amount)
        {
            giveType = default;
            getType = default;
            amount = 0;
            int giveKinds = 0, getKinds = 0;
            foreach (var r in TradeResources)
            {
                if (give[r] > 0)
                {
                    giveKinds++;
                    giveType = r;
                }

                if (get[r] > 0)
                {
                    getKinds++;
                    getType = r;
                }
            }

            if (giveKinds != 1 || getKinds != 1 || giveType == getType)
                return false;
            amount = get[getType];
            return give[giveType] == ratio(giveType) * amount;
        }

        /// <summary>Small round status mark for a player's answer.</summary>
        private static void TradeStatusMark(RectTransform parent, string name, Sprite glyph, Vector2 anchor, Vector2 pos,
            float size)
        {
            var bg = CreateDisc(parent, name, new Color(0.98f, 0.94f, 0.84f, 1f), size);
            SetRect(bg.rectTransform, anchor, pos, new Vector2(size, size));
            TradeGlyph(bg.rectTransform, "Glyph", glyph, new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.8f);
        }
    }
}
