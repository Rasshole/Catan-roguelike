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
        /// <summary>Kicker on the ornate frame's wood band (cream-gold reads on the dark wood).</summary>
        private static readonly Color TradeKickerInk = new(0.98f, 0.86f, 0.56f, 1f);

        /// <summary>✕ on the frame's top band, clear of the carved corner.</summary>
        private static readonly Vector2 TradeClosePos = new(-150f, -54f);
        private static readonly Vector2 TradeCloseSize = new(46f, 40f);

        /// <summary>The five tradeable resources in board order (the trade rows always show all five).</summary>
        public static readonly ResourceType[] TradeResources =
        {
            ResourceType.Wood, ResourceType.Brick, ResourceType.Wheat, ResourceType.Sheep, ResourceType.Stone
        };

        private static readonly Color TradeGiveTint = new(1f, 0.86f, 0.80f, 1f);
        private static readonly Color TradeGetTint = new(0.86f, 0.96f, 0.80f, 1f);

        // v139: six slots per row (five resources + the wildcard card), so a little tighter than v138.
        public const float TradeSlot = 76f;
        public const float TradeSlotPitch = 86f;
        private const float TradeWildGap = 14f;
        private const float TradeRowArrow = 54f;
        private static readonly Color TradeWildTint = new(1f, 0.90f, 0.62f, 1f);

        /// <summary>Name suffix of the wildcard slot / icon (<c>GiveMore_Any</c>, <c>HeadGetIcon_Any</c>, …).</summary>
        public const string TradeAnyKey = "Any";

        /// <summary>v139: arrow x so a six-slot row sits centred in a card <paramref name="width"/> wide.</summary>
        private static float TradeRowX0(float width, bool wild = true)
        {
            float row = TradeRowArrow / 2f + 46f + TradeSlot / 2f + (wild ? 5 : 4) * TradeSlotPitch + (wild ? TradeWildGap : 0f)
                        + TradeSlot / 2f;
            return (width - row) / 2f + TradeRowArrow / 2f;
        }

        /// <summary>v139: the wildcard slot of an edit row ("N of any resource").</summary>
        private sealed class TradeWildSlot
        {
            public System.Func<int> Get;
            public System.Action<int> Set;
            public int Limit;
        }

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
            Vector2 anchor, float x, float y, float icon = 40f, int any = 0)
        {
            float cursor = x;
            TradeGlyph(parent, key + "Arrow", give ? TradeGlyphSprites.GiveArrow : TradeGlyphSprites.GetArrow, anchor,
                new Vector2(cursor + icon * 0.4f, y), icon * 0.8f);
            cursor += icon * 0.8f + 6f;
            int anyCount = any;
            bool anyShown = false;
            foreach (var r in TradeResources)
            {
                int n = b[r];
                if (n <= 0)
                    continue;
                anyShown = true;
                var img = CreateImage(parent, key + "Icon_" + r, ColonistHudResourceSprites.Get(r));
                SetRect(img.rectTransform, anchor, new Vector2(cursor + icon / 2f, y), new Vector2(icon, icon));
                var count = RunHudUiFactory.CreateText(parent, key + "N_" + r, n.ToString(), Mathf.RoundToInt(icon * 0.5f),
                    TextAnchor.MiddleLeft, give ? ColonistUiKit.InkBad : ColonistUiKit.InkGood, FontStyle.Bold);
                SetRect(count.rectTransform, anchor, new Vector2(cursor + icon + 12f, y), new Vector2(24f, icon));
                cursor += icon + 28f;
            }

            if (anyCount > 0)
            {
                anyShown = true;
                var img = CreateImage(parent, key + "Icon_" + TradeAnyKey, TradeGlyphSprites.AnyResource);
                img.preserveAspect = true;
                SetRect(img.rectTransform, anchor, new Vector2(cursor + icon / 2f, y), new Vector2(icon, icon));
                var count = RunHudUiFactory.CreateText(parent, key + "N_" + TradeAnyKey, anyCount.ToString(),
                    Mathf.RoundToInt(icon * 0.5f), TextAnchor.MiddleLeft, give ? ColonistUiKit.InkBad : ColonistUiKit.InkGood,
                    FontStyle.Bold);
                SetRect(count.rectTransform, anchor, new Vector2(cursor + icon + 12f, y), new Vector2(24f, icon));
                cursor += icon + 28f;
            }

            if (!anyShown)
            {
                var none = RunHudUiFactory.CreateText(parent, key + "None", "—", Mathf.RoundToInt(icon * 0.55f),
                    TextAnchor.MiddleCenter, ColonistUiKit.InkMuted, FontStyle.Bold);
                SetRect(none.rectTransform, anchor, new Vector2(cursor + 14f, y), new Vector2(28f, icon));
                cursor += 28f;
            }

            return cursor - x;
        }

        /// <summary>Width <see cref="BuildTradeTerms"/> would use (for centring / right-aligning).</summary>
        private static float TradeTermsWidth(ResourceBundle b, float icon = 40f, int any = 0)
        {
            int kinds = any > 0 ? 1 : 0;
            foreach (var r in TradeResources)
                if (b[r] > 0)
                    kinds++;
            return icon * 0.8f + 6f + (kinds == 0 ? 28f : kinds * (icon + 28f));
        }

        /// <summary>
        /// One editable row: arrow + five icon slots (+ the v139 wildcard card when <paramref name="wild"/> is set).
        /// Left-click adds (up to <paramref name="limit"/>), right-click or the − below removes. Buttons are named
        /// <c>{key}More_{r}</c> / <c>{key}Less_{r}</c> (<c>{key}More_Any</c> for the wildcard).
        /// </summary>
        private static void BuildTradeEditRow(RectTransform card, string key, bool give, float y, float x0,
            System.Func<ResourceBundle> get, System.Action<ResourceBundle> set, ResourceBundle limit,
            System.Action changed, TradeWildSlot wild = null, System.Func<ResourceType, bool> locked = null)
        {
            var anchor = new Vector2(0f, 1f);
            TradeGlyph(card, key + "Arrow", give ? TradeGlyphSprites.GiveArrow : TradeGlyphSprites.GetArrow, anchor,
                new Vector2(x0, y), TradeRowArrow);
            for (int i = 0; i < TradeResources.Length; i++)
            {
                var r = TradeResources[i];
                float x = x0 + 46f + TradeSlot / 2f + i * TradeSlotPitch;

                void Add(int delta)
                {
                    var b = get();
                    int next = Mathf.Clamp(b[r] + delta, 0, Mathf.Max(b[r], limit[r]));
                    if (next == b[r])
                        return;
                    b.Set(r, next);
                    set(b);
                    changed?.Invoke();
                }

                int have = get()[r];
                BuildTradeSlot(card, key, r.ToString(), give, x, y, have, limit[r],
                    ColonistHudResourceSprites.Get(r), false, Add,
                    locked == null ? limit[r] <= 0 && have == 0 : locked(r) && have == 0, locked != null);
            }

            if (wild == null)
                return;
            float wx = x0 + 46f + TradeSlot / 2f + TradeResources.Length * TradeSlotPitch + TradeWildGap;

            void AddAny(int delta)
            {
                int n = wild.Get();
                int next = Mathf.Clamp(n + delta, 0, Mathf.Max(n, wild.Limit));
                if (next == n)
                    return;
                wild.Set(next);
                changed?.Invoke();
            }

            BuildTradeSlot(card, key, TradeAnyKey, give, wx, y, wild.Get(), wild.Limit, TradeGlyphSprites.AnyResource,
                true, AddAny, wild.Limit <= 0 && wild.Get() == 0, false);
        }

        private static void BuildTradeSlot(RectTransform card, string key, string suffix, bool give, float x, float y,
            int n, int limit, Sprite sprite, bool wild, System.Action<int> add, bool dim, bool strike)
        {
            var anchor = new Vector2(0f, 1f);
            var slot = CreateImage(card, key + "More_" + suffix, null);
            slot.preserveAspect = false;
            var tint = n > 0 ? (give ? TradeGiveTint : TradeGetTint) : wild ? TradeWildTint : Color.white;
            ColonistUiKit.Apply(slot, UiKitPiece.FrameThin, 1f, tint);
            slot.raycastTarget = true;
            SetRect(slot.rectTransform, anchor, new Vector2(x, y), new Vector2(TradeSlot, TradeSlot));
            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot;
            button.interactable = n < limit;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
            colors.disabledColor = Color.white;
            button.colors = colors;
            button.onClick.AddListener(() => add(1));
            slot.gameObject.AddComponent<HudRightClickRelay>().OnRightClick = () => add(-1);

            var icon = CreateImage(slot.rectTransform, "Icon", sprite);
            icon.preserveAspect = true;
            float iconSize = wild ? 62f : 54f;
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
            if (dim)
            {
                icon.color = strike ? new Color(0.55f, 0.55f, 0.55f, 0.38f) : new Color(1f, 1f, 1f, 0.32f);
                if (strike)
                {
                    // v140: only what the other player cannot spare is struck through; allowed slots stay full colour.
                    slot.color = new Color(0.78f, 0.74f, 0.68f, 1f);
                    var bar = CreateImage(slot.rectTransform, "Locked", null);
                    bar.color = new Color(0.62f, 0.16f, 0.12f, 0.9f);
                    bar.raycastTarget = false;
                    SetRect(bar.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TradeSlot * 0.92f, 5f));
                    bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                }
            }

            if (n > 0)
            {
                var badge = CreateDisc(slot.rectTransform, "Count", give ? TradeGlyphSprites.GiveRed : TradeGlyphSprites.GetGreen, 32f);
                SetRect(badge.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(32f, 32f));
                var t = RunHudUiFactory.CreateText(badge.rectTransform, "N", n.ToString(), 19, TextAnchor.MiddleCenter,
                    ColonistUiKit.Cream, FontStyle.Bold);
                Stretch(t.rectTransform);
            }

            var less = TradeButton(card, key + "Less_" + suffix, "−", null, anchor, new Vector2(x, y - TradeSlot / 2f - 17f),
                new Vector2(44f, 26f), n > 0, false, () => add(-1), 20);
            less.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(0f, 2f);
        }

        // ---------- v139: leader portraits + the wildcard picker ----------

        /// <summary>
        /// A player's leader portrait in a small seat-coloured ring (as on the seat strip), with the seat number /
        /// AI tag on a small badge. Without a leader (party rules off) it is the plain coloured disc with the tag.
        /// The ring is named <paramref name="name"/>; the portrait is its child "Face".
        /// </summary>
        private static Image BuildTradePortrait(RectTransform parent, string name, Color ring, Sprite portrait, string tag,
            Color tagInk, Vector2 anchor, Vector2 pos, float size)
        {
            var disc = CreateDisc(parent, name, ring, size);
            SetRect(disc.rectTransform, anchor, pos, new Vector2(size, size));
            if (portrait == null)
            {
                if (!string.IsNullOrEmpty(tag))
                {
                    var t = RunHudUiFactory.CreateText(disc.rectTransform, "Tag", tag,
                        Mathf.RoundToInt(size * (tag.Length > 1 ? 0.32f : 0.42f)), TextAnchor.MiddleCenter, tagInk,
                        FontStyle.Bold);
                    t.raycastTarget = false;
                    Stretch(t.rectTransform);
                }

                return disc;
            }

            var face = CreateImage(disc.rectTransform, "Face", portrait);
            face.preserveAspect = true;
            face.raycastTarget = false;
            SetRect(face.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.86f, size * 0.86f));
            if (!string.IsNullOrEmpty(tag))
            {
                float b = Mathf.Round(size * 0.40f);
                var badge = CreateDisc(disc.rectTransform, "Badge", ring, b);
                SetRect(badge.rectTransform, new Vector2(1f, 0f), new Vector2(-b * 0.22f, b * 0.22f), new Vector2(b, b));
                badge.raycastTarget = false;
                var t = RunHudUiFactory.CreateText(badge.rectTransform, "Tag", tag,
                    Mathf.RoundToInt(b * (tag.Length > 1 ? 0.44f : 0.58f)), TextAnchor.MiddleCenter, tagInk, FontStyle.Bold);
                t.raycastTarget = false;
                Stretch(t.rectTransform);
            }

            return disc;
        }

        /// <summary>Makes a portrait ring clickable (the "hand me the device" switch on the trade board).</summary>
        private static Button MakeTradePortraitButton(Image ring, bool enabled, System.Action click)
        {
            ring.raycastTarget = true;
            var b = ring.gameObject.AddComponent<Button>();
            b.targetGraphic = ring;
            var colors = b.colors;
            colors.disabledColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.8f, 1f);
            b.colors = colors;
            b.interactable = enabled;
            if (click != null)
                b.onClick.AddListener(() => click());
            return b;
        }

        public const float TradePickerHeight = 186f;

        /// <summary>
        /// v139 wildcard picker: choose exactly <paramref name="n"/> cards from <paramref name="pool"/> (what the other
        /// player can spare). Slots <c>WildPickMore_{r}</c>, buttons <c>WildPickConfirm</c> / <c>WildPickCancel</c>,
        /// all under a "WildPicker" container placed <paramref name="top"/> from the card's top.
        /// </summary>
        private static RectTransform BuildTradeWildPicker(RectTransform card, string title, float top, float width, int n,
            ResourceBundle pool, System.Func<ResourceBundle> get, System.Action<ResourceBundle> set, System.Action changed,
            System.Action confirm, System.Action cancel)
        {
            var box = new GameObject("WildPicker").AddComponent<RectTransform>();
            box.SetParent(card, false);
            box.anchorMin = new Vector2(0f, 1f);
            box.anchorMax = new Vector2(0f, 1f);
            box.pivot = new Vector2(0f, 1f);
            box.anchoredPosition = new Vector2(0f, top);
            box.sizeDelta = new Vector2(width, TradePickerHeight);
            var bg = CreateImage(box, "Bg", null);
            bg.preserveAspect = false;
            ColonistUiKit.Apply(bg, UiKitPiece.FrameThin, 0.7f, new Color(1f, 0.93f, 0.74f, 1f));
            bg.raycastTarget = false;
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = new Vector2(58f, 4f);
            bg.rectTransform.offsetMax = new Vector2(-58f, -4f);

            var anchor = new Vector2(0f, 1f);
            var any = TradeGlyph(box, "AnyIcon", TradeGlyphSprites.AnyResource, anchor, new Vector2(98f, -34f), 40f);
            any.raycastTarget = false;
            var picked = get();
            var text = RunHudUiFactory.CreateText(box, "Title", $"{title}  ·  {picked.Total}/{n}", 18, TextAnchor.MiddleLeft,
                ColonistUiKit.Ink, FontStyle.Bold);
            SetRect(text.rectTransform, anchor, new Vector2(126f + 190f, -34f), new Vector2(380f, 28f));

            var limit = ResourceBundle.Zero;
            foreach (var r in TradeResources)
                limit.Set(r, Mathf.Min(pool[r], picked[r] + Mathf.Max(0, n - picked.Total)));
            BuildTradeEditRow(box, "WildPick", false, -106f, TradeRowX0(width, false), get, set, limit,
                changed, null, r => pool[r] <= 0);
            TradeButton(box, "WildPickConfirm", "", TradeGlyphSprites.CheckCream, new Vector2(1f, 1f),
                new Vector2(-148f, -34f), new Vector2(72f, 44f), picked.Total == n && pool.CanAfford(picked), true,
                confirm);
            TradeButton(box, "WildPickCancel", "", TradeGlyphSprites.Cross, new Vector2(1f, 1f), new Vector2(-92f, -34f),
                new Vector2(44f, 44f), true, false, cancel);
            return box;
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
