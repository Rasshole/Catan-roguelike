using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v132: the pre-game run setup (map → leader → draft) as painted cards on the HUD canvas, in the same
    /// parchment/wood language as the civilization pick. Replaces the IMGUI box of <see cref="PlaceholderUI"/>
    /// in Colonist mode (the IMGUI path stays for the classic presentation).
    /// </summary>
    public sealed partial class RunHudController
    {
        private RectTransform _preGameRoot;
        private string _preGameKey;
        private bool _inRunHudHidden;
        private bool _unlockPanelOpen;

        /// <summary>True while the painted pre-game menus are on screen (PlaceholderUI then skips its IMGUI).</summary>
        public static bool PaintedPreGameMenusVisible { get; private set; }

        public bool IsPreGameMenuVisibleForTests => _preGameRoot != null && _preGameRoot.gameObject.activeSelf;
        public RectTransform PreGameMenuRootForTests => _preGameRoot;

        private static readonly Dictionary<string, Sprite> HexThumbCache = new();

        /// <summary>Returns true when the painted menus handle this pre-game phase.</summary>
        private bool RefreshPreGameMenus(GameState state, bool preGame)
        {
            bool painted = preGame && ColonistPresentation.IsActive && _controller != null;
            SetInRunHudHidden(painted);
            PaintedPreGameMenusVisible = painted;
            if (!painted)
            {
                if (_preGameRoot != null)
                    _preGameRoot.gameObject.SetActive(false);
                return false;
            }

            PlaceholderHudLayout.SetActivePanelWidth(0f);
            string key = BuildPreGameKey(state);
            if (_preGameRoot == null || _preGameKey != key)
            {
                if (_preGameRoot != null)
                    ColonistSceneDestroy.SafeDestroy(_preGameRoot.gameObject);
                _preGameRoot = BuildPreGame(state);
                _preGameKey = key;
            }

            _preGameRoot.gameObject.SetActive(true);
            _preGameRoot.SetAsLastSibling();
            return true;
        }

        private void OnDisable() => PaintedPreGameMenusVisible = false;

        private string BuildPreGameKey(GameState state)
        {
            string drafted = string.Join(",", state.DraftedUniques.OrderBy(x => x));
            return $"{state.Phase}|{state.MapSize}|{drafted}|{_meta?.Stars}|{_unlockPanelOpen}|" +
                   $"{_controller.FishermenMapOption}|{OnboardingTipsStore.TipsEnabled}|{HotSeatLobbyKey()}|" +
                   $"{(_mainMenuOpen ? "main" : "")}|{_controller.UnlockAll}|" +
                   (_meta != null ? string.Join(",", _meta.GetPurchasableUnlocks()) : "");
        }

        private void RebuildPreGame()
        {
            _preGameKey = null;
            if (_controller != null)
                Refresh(_controller.State);
        }

        /// <summary>Hides (alpha 0, no raycasts) every in-run HUD element while the pre-game menus are up.</summary>
        private void SetInRunHudHidden(bool hidden)
        {
            // While hidden, re-apply every refresh: the HUD may rebuild children (e.g. after the map changes).
            if (_canvas == null || (!hidden && !_inRunHudHidden))
                return;
            _inRunHudHidden = hidden;
            foreach (Transform child in _canvas.transform)
            {
                if (_preGameRoot != null && child == _preGameRoot)
                    continue;
                if (_civPickRoot != null && child == _civPickRoot)
                    continue;
                var group = child.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    if (!hidden)
                        continue;
                    group = child.gameObject.AddComponent<CanvasGroup>();
                }

                group.alpha = hidden ? 0f : 1f;
                group.blocksRaycasts = !hidden;
                group.interactable = !hidden;
            }
        }

        private RectTransform BuildPreGame(GameState state)
        {
            bool frontScreen = state.Phase == GamePhase.RunSelectMap && (_hotSeatLobbyOpen || _mainMenuOpen);
            var root = CreateFullscreenDim("PreGameMenu", frontScreen
                ? new Color(0.05f, 0.04f, 0.03f, 0.93f)
                : new Color(0.05f, 0.04f, 0.03f, 0.58f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;

            // v143: the front door — main menu, then the setup screen (map + rules + seats) — owns the whole screen.
            if (frontScreen)
            {
                if (_hotSeatLobbyOpen)
                    BuildGameSetup(root);
                else
                    BuildMainMenu(root);
                return root;
            }

            string step, title;
            switch (state.Phase)
            {
                case GamePhase.RunSelectLeader:
                    step = "STEP 2 OF 3  ·  LEADER";
                    title = "Choose your leader";
                    break;
                case GamePhase.RunSelectDraft:
                    step = "STEP 3 OF 3  ·  UNIQUE BUILDINGS";
                    title = "Draft your buildings";
                    break;
                default:
                    step = "STEP 1 OF 3  ·  MAP";
                    title = "Choose your map";
                    break;
            }

            var kicker = RunHudUiFactory.CreateText(root, "Step", step, 16, TextAnchor.MiddleCenter,
                new Color(0.93f, 0.80f, 0.50f, 1f), FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(900f, 24f));
            AddShadow(kicker.gameObject, 1.5f);
            var titleText = RunHudUiFactory.CreateText(root, "Title", title, 46, TextAnchor.MiddleCenter, TitleCream,
                FontStyle.Bold);
            SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1200f, 64f));
            AddShadow(titleText.gameObject, 2f);

            string subtitle = PreGameSubtitle(state);
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = RunHudUiFactory.CreateText(root, "Subtitle", subtitle, 20, TextAnchor.MiddleCenter,
                    new Color(0.90f, 0.84f, 0.70f, 1f), FontStyle.Italic);
                SetRect(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1300f, 30f));
                AddShadow(sub.gameObject, 1.5f);
            }

            if (_meta != null)
                BuildStarsBadge(root);

            switch (state.Phase)
            {
                case GamePhase.RunSelectLeader:
                    BuildLeaderCards(root);
                    break;
                case GamePhase.RunSelectDraft:
                    BuildDraftCards(root, state);
                    break;
                default:
                    BuildMapCards(root, state);
                    break;
            }

            BuildPreGameFooter(root, state);
            if (state.Phase == GamePhase.RunSelectMap && _unlockPanelOpen && _meta != null)
                BuildUnlockPanel(root);
            return root;
        }

        private string PreGameSubtitle(GameState state)
        {
            var ctx = new OnboardingCopy.Context(state.Phase, state.Board.DayNumber, OnboardingTipsStore.TipsEnabled,
                false, 0);
            if (OnboardingCopy.TryGetPhaseBanner(ctx, out var line) && !string.IsNullOrEmpty(line))
                return line;
            return state.Phase switch
            {
                GamePhase.RunSelectLeader => $"{MapPresets.GetDisplayName(state.MapSize)} — who leads this run?",
                GamePhase.RunSelectDraft => "Pick the unique buildings you can construct this run.",
                _ => "Bigger maps mean longer runs and more rivals."
            };
        }

        private void BuildStarsBadge(RectTransform root)
        {
            var badge = CreateImage(root, "Stars", null);
            badge.preserveAspect = false;
            ColonistUiKit.Apply(badge, UiKitPiece.Badge, 0.7f);
            SetRect(badge.rectTransform, new Vector2(1f, 1f), new Vector2(-120f, -70f), new Vector2(170f, 56f));
            var t = RunHudUiFactory.CreateText(badge.rectTransform, "Label", $"★ {_meta.Stars} stars", 21,
                TextAnchor.MiddleCenter, ColonistUiKit.TextOn(UiKitPiece.Badge), FontStyle.Bold);
            Stretch(t.rectTransform);
        }

        // ---------- shared card helpers ----------

        private RectTransform BuildMenuCard(RectTransform parent, string name, Vector2 pos, Vector2 size,
            bool enabled, bool selected, System.Action onClick)
        {
            var card = CreateCardFrame(parent, name, wide: false);
            SetRect(card, new Vector2(0.5f, 0.5f), pos, size);
            var img = card.GetComponent<Image>();
            // v133: 9-slice the portrait frame so taller cards (leader medallion) keep undistorted corners.
            var sliced = ColonistRoguelikeArt.GetCardFrameSliced(false);
            if (sliced != null)
            {
                img.sprite = sliced;
                img.type = Image.Type.Sliced;
                img.preserveAspect = false;
                img.pixelsPerUnitMultiplier = sliced.texture.width / size.x;
            }

            if (!enabled)
                img.color = new Color(0.72f, 0.68f, 0.62f, 1f);
            else if (selected)
                img.color = new Color(0.90f, 0.98f, 0.84f, 1f);

            if (enabled && onClick != null)
            {
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
                colors.pressedColor = new Color(0.86f, 0.80f, 0.70f, 1f);
                button.colors = colors;
                button.onClick.AddListener(() => onClick());
            }

            return card;
        }

        private static Text CardLine(RectTransform card, string name, string text, int size, Color color,
            FontStyle style, float top, float height, float width, TextAnchor anchor = TextAnchor.UpperCenter)
        {
            var t = RunHudUiFactory.CreateText(card, name, text, size, anchor, color, style);
            SetRect(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -top - height / 2f),
                new Vector2(width, height));
            return t;
        }

        private static void CardRule(RectTransform card, float top, float width)
        {
            var rule = CreateImage(card, "Rule", null);
            rule.color = ColonistUiKit.Rule;
            SetRect(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -top), new Vector2(width, 2f));
        }

        private static List<float> RowPositions(int count, float cardW, float gap)
        {
            float total = count * cardW + (count - 1) * gap;
            var xs = new List<float>();
            for (int i = 0; i < count; i++)
                xs.Add(-total / 2f + cardW / 2f + i * (cardW + gap));
            return xs;
        }

        // ---------- step 1: map ----------

        private void BuildMapCards(RectTransform root, GameState state)
        {
            var sizes = new List<(MapSize size, bool available, int cost)>();
            foreach (MapSize size in System.Enum.GetValues(typeof(MapSize)))
            {
                bool available = _controller != null && _controller.UnlockAll
                                 || (_meta == null
                                     ? size == MapSize.Small || size == MapSize.Medium || size == MapSize.Large
                                     : _meta.IsMapAvailable(size));
                int cost = 0;
                if (!available)
                {
                    var unlock = MetaCatalog.MapUnlockFor(size);
                    if (!unlock.HasValue)
                        continue;
                    cost = MetaCatalog.Get(unlock.Value).Cost;
                }

                sizes.Add((size, available, cost));
            }

            float cardW = Mathf.Min(300f, (1760f - 24f * (sizes.Count - 1)) / Mathf.Max(1, sizes.Count));
            float cardH = cardW * 1.4f;
            float s = cardW / 300f;
            var xs = RowPositions(sizes.Count, cardW, 24f);
            for (int i = 0; i < sizes.Count; i++)
            {
                var (size, available, cost) = sizes[i];
                var card = BuildMenuCard(root, "Map_" + size, new Vector2(xs[i], -20f), new Vector2(cardW, cardH),
                    available, false, () =>
                    {
                        _controller.SelectMap(size);
                        RebuildPreGame();
                    });
                float inner = cardW - 70f * s;
                SplitMapName(size, out string shortName, out string players);
                string kickerText = available
                    ? (string.IsNullOrEmpty(players) ? $"{(int)size} LAND HEXES" : $"{(int)size} HEXES  ·  {players}")
                    : $"LOCKED  ·  {cost} ★";
                CardLine(card, "Kicker", kickerText,
                    Mathf.RoundToInt(13 * s + 1), available ? ColonistUiKit.InkGold : InkBad, FontStyle.Bold,
                    70f * s, 20f, inner);
                BuildHexCluster(card, size, new Vector2(0f, -(100f + 62f) * s), new Vector2(inner - 30f * s, 124f * s),
                    available);
                CardLine(card, "Name", shortName, Mathf.RoundToInt(27 * s), InkBrown,
                    FontStyle.Bold, 232f * s, 34f * s, inner, TextAnchor.MiddleCenter);
                CardRule(card, 272f * s, inner - 40f * s);
                CardLine(card, "Description", MapPresets.GetDescription(size), Mathf.RoundToInt(15 * s + 1),
                    InkMuted, FontStyle.Normal, 282f * s, 74f * s, inner);
                CardLine(card, "Hint", available ? "click to choose" : "unlock with stars", Mathf.RoundToInt(13 * s),
                    InkMuted, FontStyle.Italic, cardH - 66f * s, 20f, inner, TextAnchor.MiddleCenter);
            }
        }

        /// <summary>"Base — 19 hex (3–4 players)" → "Base" and "3–4 PLAYERS" (the hex count sits in the kicker).</summary>
        public static void SplitMapName(MapSize size, out string shortName, out string players)
        {
            string full = MapPresets.GetDisplayName(size);
            int dash = full.IndexOf(" — ", System.StringComparison.Ordinal);
            shortName = dash > 0 ? full.Substring(0, dash) : full;
            players = "";
            int open = full.IndexOf('(');
            int close = full.IndexOf(')');
            if (open >= 0 && close > open)
                players = full.Substring(open + 1, close - open - 1).ToUpperInvariant();
        }

        private void BuildHexCluster(RectTransform card, MapSize size, Vector2 center, Vector2 box, bool available)
        {
            var holder = new GameObject("HexCluster", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(card, false);
            holder.gameObject.layer = card.gameObject.layer;
            SetRect(holder, new Vector2(0.5f, 1f), center, box);

            var layout = PresetLayout(size);
            const float sqrt3 = 1.7320508f;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            var pts = new List<Vector2>();
            foreach (var (coord, _) in layout)
            {
                // Pointy-top axial → unit-circumradius layout; screen y grows upward as board z does.
                var p = new Vector2(sqrt3 * (coord.Q + coord.R / 2f), 1.5f * coord.R);
                pts.Add(p);
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            float unit = Mathf.Min(box.x / (maxX - minX + sqrt3), box.y / (maxY - minY + 2f));
            var mid = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
            bool desertCenter = size != MapSize.Small && size != MapSize.Medium;
            // Presets list resources ring by ring; the real board shuffles them, so the thumbnail does too
            // (deterministic per map size, keeping the preset's terrain mix).
            var resources = layout.Select(t => t.resource).ToList();
            var rng = new System.Random(1000 + (int)size);
            for (int i = resources.Count - 1; i > 1; i--)
            {
                int j = 1 + rng.Next(i);
                (resources[i], resources[j]) = (resources[j], resources[i]);
            }

            for (int i = 0; i < pts.Count; i++)
            {
                var coord = layout[i].coord;
                var resource = resources[i];
                string slot = desertCenter && coord.Q == 0 && coord.R == 0 ? "Desert" : HexSlot(resource);
                var img = CreateImage(holder, "Hex" + i, LoadHexThumb(slot));
                img.preserveAspect = false;
                if (!available)
                    img.color = new Color(0.62f, 0.60f, 0.58f, 0.85f);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = (pts[i] - mid) * unit;
                rt.sizeDelta = new Vector2(sqrt3 * unit * 1.04f, 2f * unit * 1.04f);
            }
        }

        private static IReadOnlyList<(Core.Hex.HexCoord coord, ResourceType resource)> PresetLayout(MapSize size) =>
            size switch
            {
                MapSize.Small => MapPresets.SmallSevenHex(),
                MapSize.Medium => MapPresets.MediumThirteenHex(),
                MapSize.Extended => MapPresets.ExtendedThirtyHex(),
                MapSize.Huge => MapPresets.HugeThirtySevenHex(),
                _ => MapPresets.LargeNineteenHex()
            };

        private static string HexSlot(ResourceType resource) => resource switch
        {
            ResourceType.Wood => "Wood",
            ResourceType.Brick => "Brick",
            ResourceType.Wheat => "Wheat",
            ResourceType.Sheep => "Sheep",
            ResourceType.Stone => "Ore",
            _ => "Desert"
        };

        private static Sprite LoadHexThumb(string slot)
        {
            if (HexThumbCache.TryGetValue(slot, out var cached) && cached != null)
                return cached;
            var tex = Resources.Load<Texture2D>("Art/UIKit/Hex" + slot);
            Sprite sprite = null;
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = "Hex" + slot;
            }

            HexThumbCache[slot] = sprite;
            return sprite;
        }

        // ---------- step 2: leader ----------

        private void BuildLeaderCards(RectTransform root)
        {
            var leaders = new List<(LeaderId id, bool available, int cost)>();
            foreach (LeaderId leader in System.Enum.GetValues(typeof(LeaderId)))
            {
                bool available = _controller != null && _controller.UnlockAll || _meta == null || _meta.IsLeaderAvailable(leader);
                int cost = 0;
                if (!available)
                {
                    var unlock = MetaCatalog.LeaderUnlockFor(leader);
                    if (!unlock.HasValue)
                        continue;
                    cost = MetaCatalog.Get(unlock.Value).Cost;
                }

                leaders.Add((leader, available, cost));
            }

            // v133: taller card with the leader's painted medallion above the name.
            const float cardW = 320f, cardH = 584f;
            var xs = RowPositions(leaders.Count, cardW, 30f);
            for (int i = 0; i < leaders.Count; i++)
            {
                var (id, available, cost) = leaders[i];
                var def = LeaderLibrary.Get(id);
                var card = BuildMenuCard(root, "Leader_" + id, new Vector2(xs[i], -20f), new Vector2(cardW, cardH),
                    available, false, () =>
                    {
                        _controller.SelectLeader(id);
                        RebuildPreGame();
                    });
                const float inner = 244f;
                var portraitSprite = ColonistRoguelikeArt.GetLeaderPortrait(id);
                if (portraitSprite != null)
                {
                    var portrait = CreateImage(card, "Portrait", portraitSprite);
                    portrait.preserveAspect = true;
                    if (!available)
                        portrait.color = new Color(0.62f, 0.58f, 0.54f, 1f);
                    SetRect(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -54f - 74f),
                        new Vector2(148f, 148f));
                    var ps = portrait.gameObject.AddComponent<Shadow>();
                    ps.effectColor = new Color(0.20f, 0.12f, 0.05f, 0.45f);
                    ps.effectDistance = new Vector2(2f, -3f);
                }

                const float y0 = 206f;
                CardLine(card, "Kicker", available ? "LEADER" : $"LOCKED  ·  {cost} ★", 14,
                    available ? ColonistUiKit.InkGold : InkBad, FontStyle.Bold, y0, 20f, inner);
                CardLine(card, "Name", def.Name, 28, InkBrown, FontStyle.Bold, y0 + 22f, 40f, inner,
                    TextAnchor.MiddleCenter);
                CardRule(card, y0 + 70f, inner - 40f);
                CardLine(card, "Passive", def.PassiveDescription, 17, available ? InkGood : InkMuted, FontStyle.Bold,
                    y0 + 82f, 96f, inner);
                if (def.PerkPool != null && def.PerkPool.Count > 0)
                {
                    CardLine(card, "PerksHeader", "LEVEL-UP PERKS", 12, ColonistUiKit.InkGold, FontStyle.Bold,
                        y0 + 184f, 18f, inner);
                    CardLine(card, "Perks", string.Join("\n", def.PerkPool.Take(4).Select(PerkDisplayName)), 14,
                        InkMuted, FontStyle.Normal, y0 + 204f, 76f, inner);
                }

                CardLine(card, "Hint", available ? "click to choose" : "unlock with stars", 13, InkMuted,
                    FontStyle.Italic, cardH - 72f, 20f, inner, TextAnchor.MiddleCenter);
            }
        }

        // ---------- step 3: draft ----------

        private void BuildDraftCards(RectTransform root, GameState state)
        {
            var pool = (_meta?.GetDraftPool() ?? UniqueBuildingLibrary.All.Keys).ToList();
            float cardW = Mathf.Min(320f, (1760f - 24f * (pool.Count - 1)) / Mathf.Max(1, pool.Count));
            float cardH = cardW * 1.4f;
            float s = cardW / 280f;
            var xs = RowPositions(pool.Count, cardW, 24f);
            for (int i = 0; i < pool.Count; i++)
            {
                var id = pool[i];
                var def = UniqueBuildingLibrary.Get(id);
                bool picked = state.DraftedUniques.Contains(id);
                var card = BuildMenuCard(root, "Draft_" + id, new Vector2(xs[i], -30f), new Vector2(cardW, cardH),
                    true, picked, () =>
                    {
                        _controller.ToggleDraftUnique(id);
                        RebuildPreGame();
                    });
                float inner = cardW - 70f * s;
                CardLine(card, "Kicker", picked ? "✓ DRAFTED" : "UNIQUE BUILDING", Mathf.RoundToInt(13 * s),
                    picked ? InkGood : ColonistUiKit.InkGold, FontStyle.Bold, 70f * s, 20f, inner);
                CardLine(card, "Name", def.Name, Mathf.RoundToInt(25 * s), InkBrown, FontStyle.Bold, 94f * s, 64f * s,
                    inner, TextAnchor.MiddleCenter);
                CardRule(card, 164f * s, inner - 40f * s);
                // Centred between the rule and the hint so short descriptions don't leave a hole below.
                CardLine(card, "Description", def.Description, Mathf.RoundToInt(17 * s), InkGood, FontStyle.Bold,
                    174f * s, cardH - 254f * s, inner, TextAnchor.MiddleCenter);
                CardLine(card, "Hint", picked ? "click to remove" : "click to draft", Mathf.RoundToInt(13 * s),
                    InkMuted, FontStyle.Italic, cardH - 66f * s, 20f, inner, TextAnchor.MiddleCenter);
            }
        }

        // ---------- footer + unlocks ----------

        private void BuildPreGameFooter(RectTransform root, GameState state)
        {
            var buttons = new List<(string label, bool green, bool enabled, System.Action click)>();
            if (state.Phase == GamePhase.RunSelectMap)
            {
                buttons.Add(($"Fishermen of Catan: {(_controller.FishermenMapOption ? "On" : "Off")}", false, true, () =>
                {
                    _controller.FishermenMapOption = !_controller.FishermenMapOption;
                    RebuildPreGame();
                }));
                // v143: everything is unlocked, so the star shop stays hidden (its code is kept for later).
                if (_meta != null && !_controller.UnlockAll)
                    buttons.Add((_unlockPanelOpen ? "Hide unlocks" : "Spend stars / Unlocks", false, true, () =>
                    {
                        _unlockPanelOpen = !_unlockPanelOpen;
                        RebuildPreGame();
                    }));
                buttons.Add(("Main menu", false, true, () =>
                {
                    _mainMenuOpen = true;
                    _unlockPanelOpen = false;
                    RebuildPreGame();
                }));
            }

            if (state.Phase == GamePhase.RunSelectDraft)
            {
                int pickCount = _meta?.GetDraftPickCount() ?? RunProgression.DraftPickCount;
                int picked = state.DraftedUniques.Count;
                var info = RunHudUiFactory.CreateText(root, "DraftCount", $"{picked} of {pickCount} drafted", 22,
                    TextAnchor.MiddleCenter, TitleCream, FontStyle.Bold);
                SetRect(info.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(600f, 30f));
                AddShadow(info.gameObject, 1.5f);
                buttons.Add(("Start run", true, true, () =>
                {
                    _controller.ConfirmRunSetup();
                    RebuildPreGame();
                }));
            }

            buttons.Add(($"Tips: {(OnboardingTipsStore.TipsEnabled ? "On" : "Off")}", false, true, () =>
            {
                OnboardingTipsStore.TipsEnabled = !OnboardingTipsStore.TipsEnabled;
                RebuildPreGame();
            }));

            const float h = 54f, gap = 18f;
            var widths = buttons.Select(b => Mathf.Max(170f, 26f + b.label.Length * 11.5f)).ToList();
            float total = widths.Sum() + gap * (buttons.Count - 1);
            float x = -total / 2f;
            for (int i = 0; i < buttons.Count; i++)
            {
                var (label, green, enabled, click) = buttons[i];
                var button = RunHudUiFactory.CreateButton(root, label, RunHudUiFactory.ButtonFill);
                button.name = "Footer_" + i;
                var rt = (RectTransform)button.transform;
                SetRect(rt, new Vector2(0.5f, 0f), new Vector2(x + widths[i] / 2f, 96f), new Vector2(widths[i], h));
                ColonistKenneyHudArt.ApplyColonistWarmButton(button.GetComponent<Image>(), primaryGreen: green);
                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.color = green ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                    text.fontSize = 19;
                    text.fontStyle = FontStyle.Bold;
                }

                button.interactable = enabled;
                button.onClick.AddListener(() => click());
                x += widths[i] + gap;
            }
        }

        private void BuildUnlockPanel(RectTransform root)
        {
            var ids = _meta.GetPurchasableUnlocks().ToList();
            const float w = 1080f;
            const float rowH = 64f, colW = 424f, top = 178f;
            int rows = Mathf.Max(1, (ids.Count + 1) / 2);
            float h = top + rows * (rowH + 10f) + 116f;

            var blocker = CreateImage(root, "UnlockBlocker", null);
            blocker.color = new Color(0f, 0f, 0f, 0.45f);
            blocker.raycastTarget = true;
            Stretch(blocker.rectTransform);

            var card = CreateCardFrame(root, "Unlocks", wide: true);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(w, h));
            var cardImage = card.GetComponent<Image>();
            var sliced = ColonistRoguelikeArt.GetCardFrameSliced(true);
            if (sliced != null)
            {
                cardImage.sprite = sliced;
                cardImage.type = Image.Type.Sliced;
                cardImage.preserveAspect = false;
                cardImage.pixelsPerUnitMultiplier = sliced.texture.width / 680f;
            }

            CardLine(card, "Kicker", $"META UNLOCKS  ·  {_meta.Stars} ★ TO SPEND", 14, ColonistUiKit.InkGold,
                FontStyle.Bold, 76f, 20f, w - 200f);
            CardLine(card, "Title", "Spend your stars", 30, InkBrown, FontStyle.Bold, 98f, 40f, w - 200f,
                TextAnchor.MiddleCenter);
            CardRule(card, 150f, w - 300f);
            if (ids.Count == 0)
                CardLine(card, "Empty", "All unlocks purchased.", 18, InkMuted, FontStyle.Italic, 150f, 30f, w - 160f);

            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var def = MetaCatalog.Get(id);
                bool canBuy = _meta.CanPurchase(id);
                int col = i % 2, row = i / 2;
                var chip = CreateImage(card, "Unlock_" + id, null);
                chip.preserveAspect = false;
                ColonistUiKit.Apply(chip, UiKitPiece.FrameThin, 0.6f);
                if (!canBuy)
                    chip.color = new Color(0.80f, 0.76f, 0.70f, 1f);
                chip.raycastTarget = true;
                SetRect(chip.rectTransform, new Vector2(0.5f, 1f),
                    new Vector2((col == 0 ? -1f : 1f) * (colW / 2f + 8f), -top - row * (rowH + 10f) - rowH / 2f),
                    new Vector2(colW, rowH));
                if (canBuy)
                {
                    var button = chip.gameObject.AddComponent<Button>();
                    button.targetGraphic = chip;
                    button.onClick.AddListener(() =>
                    {
                        if (_meta.TryPurchase(id))
                        {
                            var manager = FindFirstObjectByType<GameManager>();
                            if (manager != null)
                                manager.SaveMeta();
                        }

                        RebuildPreGame();
                    });
                }

                var titleT = RunHudUiFactory.CreateText(chip.rectTransform, "Title", def.Title, 16,
                    TextAnchor.UpperLeft, canBuy ? InkBrown : InkMuted, FontStyle.Bold);
                SetRect(titleT.rectTransform, new Vector2(0f, 1f), new Vector2(16f + 140f, -20f),
                    new Vector2(280f, 22f));
                var cost = RunHudUiFactory.CreateText(chip.rectTransform, "Cost", $"{def.Cost} ★", 17,
                    TextAnchor.UpperRight, canBuy ? ColonistUiKit.InkGold : InkBad, FontStyle.Bold);
                SetRect(cost.rectTransform, new Vector2(1f, 1f), new Vector2(-16f - 40f, -20f), new Vector2(80f, 22f));
                var desc = RunHudUiFactory.CreateText(chip.rectTransform, "Description", def.Description, 13,
                    TextAnchor.UpperLeft, InkMuted, FontStyle.Normal);
                SetRect(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(colW - 32f, 32f));
            }

            var close = RunHudUiFactory.CreateButton(card, "Close", RunHudUiFactory.ButtonFill);
            SetRect((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 76f), new Vector2(180f, 48f));
            ColonistKenneyHudArt.ApplyColonistWarmButton(close.GetComponent<Image>(), primaryGreen: false);
            var closeText = close.GetComponentInChildren<Text>();
            if (closeText != null)
            {
                closeText.color = ColonistUiKit.Ink;
                closeText.fontSize = 18;
                closeText.fontStyle = FontStyle.Bold;
            }

            close.onClick.AddListener(() =>
            {
                _unlockPanelOpen = false;
                RebuildPreGame();
            });
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
