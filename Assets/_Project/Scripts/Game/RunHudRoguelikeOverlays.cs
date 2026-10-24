using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Turn;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v131 roguelike overlays inside the run HUD canvas: the civilization pick (3 painted cards), the
    /// event card announced at night, and the civilization emblem in the "You" panel.
    /// </summary>
    public sealed partial class RunHudController
    {
        private static readonly Color OverlayDim = new(0.05f, 0.04f, 0.03f, 0.72f);
        private static readonly Color InkBrown = new(0.24f, 0.15f, 0.08f, 1f);
        private static readonly Color InkMuted = new(0.42f, 0.32f, 0.22f, 1f);
        private static readonly Color InkGood = new(0.16f, 0.36f, 0.14f, 1f);
        private static readonly Color InkBad = new(0.58f, 0.17f, 0.10f, 1f);
        private static readonly Color TitleCream = new(0.98f, 0.92f, 0.76f, 1f);

        // v132: the event card is a compact side card (top-left, beside the board) that hugs its text, so
        // the swapped numbers / highlighted hexes on the board stay visible while it is up.
        public const float EventCardWidth = 560f;
        public const float EventCardMinHeight = 236f;
        public const float EventCardMargin = 24f;

        private RectTransform _civPickRoot;
        private string _civPickOfferKey;
        private RectTransform _eventCardRoot;
        private string _eventCardKey;
        private string _eventDismissedKey;
        private Image _civEmblemInPanel;
        private int _humanNameBaseFontSize;

        public bool IsCivilizationPickVisibleForTests => _civPickRoot != null && _civPickRoot.gameObject.activeSelf;
        public RectTransform CivilizationPickRootForTests => _civPickRoot;
        public bool IsEventCardVisibleForTests => _eventCardRoot != null && _eventCardRoot.gameObject.activeSelf;

        /// <summary>Screen-space (canvas) rect of the event card, for layout tests.</summary>
        public Rect GetEventCardCanvasRectForTests()
        {
            if (_eventCardRoot == null)
                return default;
            var card = _eventCardRoot.Find("Card") as RectTransform;
            return card != null ? card.rect : _eventCardRoot.rect;
        }

        public RectTransform GetEventCardRectTransformForTests() =>
            _eventCardRoot != null ? _eventCardRoot.Find("Card") as RectTransform : null;

        private void RefreshRoguelikeOverlays(GameState state)
        {
            if (_canvas == null)
                return;
            RefreshCivilizationPick(state);
            RefreshEventCard(state);
            RefreshCivilizationBadge(state);
            RefreshRewardOverlays(state);
        }

        // ---------- civilization pick ----------

        private void RefreshCivilizationPick(GameState state)
        {
            bool show = state.Phase == GamePhase.RunSelectCivilization && state.CivilizationOffer.Count > 0;
            if (!show)
            {
                if (_civPickRoot != null)
                    _civPickRoot.gameObject.SetActive(false);
                return;
            }

            string key = string.Join(",", state.CivilizationOffer);
            if (_civPickRoot == null || _civPickOfferKey != key)
            {
                if (_civPickRoot != null)
                    ColonistSceneDestroy.SafeDestroy(_civPickRoot.gameObject);
                _civPickRoot = BuildCivilizationPick(state.CivilizationOffer);
                _civPickOfferKey = key;
            }

            _civPickRoot.gameObject.SetActive(true);
            _civPickRoot.SetAsLastSibling();
        }

        private RectTransform BuildCivilizationPick(IReadOnlyList<CivilizationId> offer)
        {
            var root = CreateFullscreenDim("CivilizationPick", OverlayDim);

            // v134: civs and leaders are one pick — step 2, right after the map.
            var kicker = RunHudUiFactory.CreateText(root, "Step", "STEP 2 OF 3  ·  CIVILIZATION & LEADER", 16,
                TextAnchor.MiddleCenter, new Color(0.93f, 0.80f, 0.50f, 1f), FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(900f, 24f));
            AddShadow(kicker.gameObject, 1.5f);

            var title = RunHudUiFactory.CreateText(root, "Title", "Choose your civilization", 46,
                TextAnchor.MiddleCenter, TitleCream, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(1200f, 64f));
            AddShadow(title.gameObject, 2f);

            var subtitle = RunHudUiFactory.CreateText(root, "Subtitle",
                "Each people follows its leader. Three have answered your call.", 21,
                TextAnchor.MiddleCenter, new Color(0.90f, 0.84f, 0.70f, 1f), FontStyle.Italic);
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -174f), new Vector2(1200f, 32f));
            AddShadow(subtitle.gameObject, 1.5f);

            const float cardW = 404f, cardH = 660f, gap = 40f;
            float total = offer.Count * cardW + (offer.Count - 1) * gap;
            for (int i = 0; i < offer.Count; i++)
            {
                float x = -total / 2f + cardW / 2f + i * (cardW + gap);
                BuildCivilizationCard(root, offer[i], new Vector2(x, -66f), new Vector2(cardW, cardH));
            }

            return root;
        }

        private void BuildCivilizationCard(RectTransform parent, CivilizationId id, Vector2 pos, Vector2 size)
        {
            var def = CivilizationLibrary.Get(id);
            var card = CreateCardFrame(parent, "Civ_" + id, wide: false);
            SetRect(card, new Vector2(0.5f, 0.5f), pos, size);
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
            colors.pressedColor = new Color(0.86f, 0.80f, 0.70f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                if (_controller != null && _controller.ChooseCivilization(id))
                    Refresh(_controller.State);
            });

            // The civ's face is its leader (portrait); the emblem rides on the medallion as a crest.
            bool hasPortrait = ColonistRoguelikeArt.HasCivilizationPortrait(id);
            var portrait = CreateImage(card, "Portrait", ColonistRoguelikeArt.GetCivilizationPortrait(id));
            SetRect(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -148f), new Vector2(214f, 214f));
            if (hasPortrait)
            {
                var crest = CreateImage(card, "Crest", ColonistRoguelikeArt.GetCivilizationEmblem(id));
                SetRect(crest.rectTransform, new Vector2(0.5f, 1f), new Vector2(92f, -224f), new Vector2(78f, 78f));
            }

            var name = RunHudUiFactory.CreateText(card, "Name", def.Name, 29, TextAnchor.MiddleCenter, InkBrown,
                FontStyle.Bold);
            SetRect(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -284f), new Vector2(size.x - 70f, 38f));

            var ledBy = RunHudUiFactory.CreateText(card, "LedBy", "Led by " + def.LeaderName, 18,
                TextAnchor.MiddleCenter, InkBrown, FontStyle.Normal);
            SetRect(ledBy.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -314f), new Vector2(size.x - 70f, 24f));

            var tagline = RunHudUiFactory.CreateText(card, "Tagline", def.Tagline, 16, TextAnchor.MiddleCenter,
                InkMuted, FontStyle.Italic);
            SetRect(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -338f), new Vector2(size.x - 70f, 22f));

            var rule = CreateImage(card, "Rule", null);
            rule.color = new Color(0.55f, 0.40f, 0.22f, 0.55f);
            SetRect(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -358f), new Vector2(size.x - 120f, 2f));

            var passive = RunHudUiFactory.CreateText(card, "Passive", def.Passive, 18, TextAnchor.UpperCenter,
                InkGood, FontStyle.Bold);
            SetRect(passive.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -408f), new Vector2(size.x - 80f, 82f));

            var abilityLabel = RunHudUiFactory.CreateText(card, "LeaderLabel",
                "LEADER  ·  " + def.LeaderTitle.ToUpperInvariant(), 13, TextAnchor.MiddleCenter,
                new Color(0.55f, 0.38f, 0.12f, 1f), FontStyle.Bold);
            SetRect(abilityLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -462f), new Vector2(size.x - 80f, 20f));

            var ability = RunHudUiFactory.CreateText(card, "LeaderAbility", def.LeaderAbility, 16,
                TextAnchor.UpperCenter, InkBrown, FontStyle.Normal);
            SetRect(ability.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -497f), new Vector2(size.x - 80f, 46f));

            var drawback = RunHudUiFactory.CreateText(card, "Drawback",
                def.HasDrawback ? "Drawback: " + def.Drawback : "No drawback.", 16, TextAnchor.UpperCenter,
                def.HasDrawback ? InkBad : InkMuted, def.HasDrawback ? FontStyle.Normal : FontStyle.Italic);
            SetRect(drawback.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -566f), new Vector2(size.x - 80f, 52f));
        }

        // ---------- event card ----------

        private void RefreshEventCard(GameState state)
        {
            bool fresh = state.ActiveEvent != EventId.None
                         && state.EventDay == state.Board.DayNumber
                         && (state.Phase == GamePhase.NightPlayCard || state.Phase == GamePhase.NightRoll
                             || state.Phase == GamePhase.DayProduction);
            string key = fresh ? $"{state.EventDay}:{state.ActiveEvent}" : null;
            if (!fresh || key == _eventDismissedKey)
            {
                if (_eventCardRoot != null)
                    _eventCardRoot.gameObject.SetActive(false);
                return;
            }

            if (_eventCardRoot == null || _eventCardKey != key)
            {
                if (_eventCardRoot != null)
                    ColonistSceneDestroy.SafeDestroy(_eventCardRoot.gameObject);
                _eventCardRoot = BuildEventCard(state);
                _eventCardKey = key;
            }

            _eventCardRoot.gameObject.SetActive(true);
            _eventCardRoot.SetAsLastSibling();
        }

        private RectTransform BuildEventCard(GameState state)
        {
            var def = EventLibrary.Get(state.ActiveEvent);
            string key = $"{state.EventDay}:{state.ActiveEvent}";

            // Non-blocking holder: no full-screen dim, the board stays visible and clickable.
            var root = new GameObject("EventCard").AddComponent<RectTransform>();
            root.SetParent(_canvas.transform, false);
            root.gameObject.layer = _canvas.gameObject.layer;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            const float w = EventCardWidth;
            var card = CreateCardFrame(root, "Card", wide: true);
            var cardImage = card.GetComponent<Image>();
            var sliced = ColonistRoguelikeArt.GetCardFrameSliced(true);
            if (sliced != null)
            {
                cardImage.sprite = sliced;
                cardImage.type = Image.Type.Sliced;
                cardImage.preserveAspect = false;
                cardImage.pixelsPerUnitMultiplier = sliced.texture.width / w;
            }

            var cardButton = card.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = cardImage;
            cardButton.onClick.AddListener(() => DismissEventCard(key));

            float border = sliced != null ? 96f * w / sliced.texture.width : 40f;
            const float emblemSize = 124f;
            float textX = border + 18f + emblemSize + 18f;
            float textW = w - textX - border - 16f;
            float y = border + 14f;

            Text AddLine(string name, string text, int size, Color color, FontStyle style, float gapAfter)
            {
                var t = RunHudUiFactory.CreateText(card, name, text, size, TextAnchor.UpperLeft, color, style);
                var r = t.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 1f);
                r.sizeDelta = new Vector2(textW, 400f);
                t.verticalOverflow = VerticalWrapMode.Overflow;
                float h = Mathf.Ceil(t.preferredHeight);
                r.sizeDelta = new Vector2(textW, h);
                r.anchoredPosition = new Vector2(textX, -y);
                y += h + gapAfter;
                return t;
            }

            AddLine("Kicker", $"EVENT  ·  DAY {state.EventDay}  ·  {CategoryLabel(def.Category)}", 13,
                ColonistUiKit.InkGold, FontStyle.Bold, 2f);
            AddLine("Name", def.Name, 28, InkBrown, FontStyle.Bold, 6f);
            AddLine("Description", def.Description, 16, InkBrown, FontStyle.Normal, 6f);
            if (!string.IsNullOrEmpty(state.EventDetail))
            {
                AddLine("Detail", state.EventDetail, 16,
                    def.Category == EventCategory.Hardship || def.Category == EventCategory.Robber ? InkBad : InkGood,
                    FontStyle.Bold, 6f);
            }

            int act = Core.Progression.ActProgression.GetAct(state.Board.DayNumber);
            int next = EventEngine.DaysUntilNextEvent(state.Board.DayNumber + 1, act) + 1;
            AddLine("Hint", $"Next event in {next} day{(next == 1 ? "" : "s")}  ·  click to close", 12, InkMuted,
                FontStyle.Italic, 0f);

            float height = Mathf.Max(EventCardMinHeight, y + border + 10f);
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.sizeDelta = new Vector2(w, height);
            card.anchoredPosition = new Vector2(EventCardMargin, -EventCardMargin);

            // Centre the text block vertically when the card is taller than its text (short cards).
            float spare = height - (y + border + 10f);
            if (spare > 0f)
            {
                foreach (Transform child in card)
                {
                    if (child is RectTransform r && child.GetComponent<Text>() != null)
                        r.anchoredPosition += new Vector2(0f, -spare * 0.5f);
                }
            }

            var emblem = CreateImage(card, "Emblem", ColonistRoguelikeArt.GetEventEmblem(state.ActiveEvent));
            var er = emblem.rectTransform;
            er.anchorMin = er.anchorMax = new Vector2(0f, 0.5f);
            er.pivot = new Vector2(0.5f, 0.5f);
            er.sizeDelta = new Vector2(emblemSize, emblemSize);
            er.anchoredPosition = new Vector2(border + 18f + emblemSize / 2f, 0f);
            return root;
        }

        private void DismissEventCard(string key)
        {
            _eventDismissedKey = key;
            if (_eventCardRoot != null)
                _eventCardRoot.gameObject.SetActive(false);
        }

        private static string CategoryLabel(EventCategory category) => category switch
        {
            EventCategory.Board => "THE LAND SHIFTS",
            EventCategory.Bounty => "BOUNTY",
            EventCategory.Hardship => "HARDSHIP",
            EventCategory.Gift => "GIFT",
            EventCategory.Robber => "ROBBER",
            EventCategory.Sea => "AT SEA",
            EventCategory.Trade => "TRADE",
            _ => category.ToString().ToUpperInvariant()
        };

        // ---------- civilization in the player panel ----------

        private void RefreshCivilizationBadge(GameState state)
        {
            if (_colonistHumanNameText == null)
                return;

            bool hasCiv = state.Civilization != CivilizationId.None
                          && CivilizationLibrary.TryGet(state.Civilization, out _);
            if (hasCiv && _civEmblemInPanel == null)
            {
                var nameRow = _colonistHumanNameText.transform.parent?.parent;
                if (nameRow != null)
                {
                    var go = new GameObject("CivEmblem");
                    go.transform.SetParent(nameRow, false);
                    go.transform.SetSiblingIndex(1);
                    var le = RunHudUiFactory.AddLayoutElement(go, 30f);
                    le.preferredWidth = 30f;
                    _colonistHumanNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
                    _civEmblemInPanel = go.AddComponent<Image>();
                    _civEmblemInPanel.preserveAspect = true;
                    // v138: the emblem is the way into the civ info popup (no always-on civ panel).
                    _civEmblemInPanel.raycastTarget = true;
                    _spCivEmblemButton = go.AddComponent<Button>();
                    _spCivEmblemButton.targetGraphic = _civEmblemInPanel;
                    _spCivEmblemButton.onClick.AddListener(ToggleSinglePlayerCivInfo);
                }
            }

            if (!hasCiv && _spCivInfoOpen)
            {
                _spCivInfoOpen = false;
                RefreshSinglePlayerCivInfo(state);
            }

            if (_civEmblemInPanel != null)
            {
                _civEmblemInPanel.gameObject.SetActive(hasCiv);
                if (hasCiv)
                    _civEmblemInPanel.sprite = ColonistRoguelikeArt.GetCivilizationEmblem(state.Civilization);
            }

            if (_humanNameBaseFontSize == 0)
                _humanNameBaseFontSize = _colonistHumanNameText.fontSize;
            _colonistHumanNameText.text = hasCiv
                ? $"{ColonistHudCopy.You} · {ShortCivName(state.Civilization)}"
                : ColonistHudCopy.You;
            FitNameToWidth(_colonistHumanNameText, _humanNameBaseFontSize);
        }

        /// <summary>Shrinks a one-line label until it fits its rect (the rail is narrow).</summary>
        private static void FitNameToWidth(Text label, int baseSize)
        {
            label.fontSize = baseSize;
            Canvas.ForceUpdateCanvases();
            float width = label.rectTransform.rect.width;
            if (width <= 0f)
                return;
            while (label.preferredWidth > width && label.fontSize > 11)
                label.fontSize--;
        }

        /// <summary>Compact civilization label for the narrow player panel.</summary>
        public static string ShortCivName(CivilizationId id) => id switch
        {
            CivilizationId.SeaFolk => "Sea Folk",
            CivilizationId.ForestFolk => "Forest Folk",
            CivilizationId.MerchantRepublic => "Merchants",
            CivilizationId.SteppeNomads => "Nomads",
            CivilizationId.HighlandClans => "Highlanders",
            CivilizationId.ShepherdKingdom => "Shepherds",
            CivilizationId.Brickwrights => "Brickwrights",
            CivilizationId.Academy => "Academy",
            CivilizationId.NorthernRaiders => "Raiders",
            CivilizationId.SunKingdom => "Sun Kingdom",
            _ => CivilizationLibrary.TryGet(id, out var def) ? def.Name : ""
        };

        // ---------- helpers ----------

        private RectTransform CreateFullscreenDim(string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            go.layer = _canvas.gameObject.layer;
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return rect;
        }

        private static RectTransform CreateCardFrame(Transform parent, string name, bool wide)
        {
            var img = CreateImage(parent, name, ColonistRoguelikeArt.GetCardFrame(wide));
            img.raycastTarget = true;
            if (img.sprite == null)
            {
                img.color = RunHudUiFactory.PanelFill;
                ColonistKenneyHudArt.ApplyPanelImage(img);
            }

            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(6f, -8f);
            return img.rectTransform;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = sprite != null;
            img.raycastTarget = false;
            return img;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static void AddShadow(GameObject go, float distance)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.8f);
            s.effectDistance = new Vector2(distance, -distance);
        }
    }
}
