using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Civilizations;
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

        private const float EventCardWidth = 960f;
        private const float EventCardHeight = 540f;

        private RectTransform _civPickRoot;
        private string _civPickOfferKey;
        private RectTransform _eventCardRoot;
        private string _eventCardKey;
        private string _eventDismissedKey;
        private Image _civEmblemInPanel;
        private int _humanNameBaseFontSize;

        public bool IsCivilizationPickVisibleForTests => _civPickRoot != null && _civPickRoot.gameObject.activeSelf;
        public bool IsEventCardVisibleForTests => _eventCardRoot != null && _eventCardRoot.gameObject.activeSelf;

        private void RefreshRoguelikeOverlays(GameState state)
        {
            if (_canvas == null)
                return;
            RefreshCivilizationPick(state);
            RefreshEventCard(state);
            RefreshCivilizationBadge(state);
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

            var title = RunHudUiFactory.CreateText(root, "Title", "Choose your civilization", 46,
                TextAnchor.MiddleCenter, TitleCream, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1200f, 64f));
            AddShadow(title.gameObject, 2f);

            var subtitle = RunHudUiFactory.CreateText(root, "Subtitle",
                "Your people shape the whole run. Three have answered your call.", 21,
                TextAnchor.MiddleCenter, new Color(0.90f, 0.84f, 0.70f, 1f), FontStyle.Italic);
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(1200f, 32f));
            AddShadow(subtitle.gameObject, 1.5f);

            const float cardW = 392f, cardH = 560f, gap = 44f;
            float total = offer.Count * cardW + (offer.Count - 1) * gap;
            for (int i = 0; i < offer.Count; i++)
            {
                float x = -total / 2f + cardW / 2f + i * (cardW + gap);
                BuildCivilizationCard(root, offer[i], new Vector2(x, -36f), new Vector2(cardW, cardH));
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

            var emblem = CreateImage(card, "Emblem", ColonistRoguelikeArt.GetCivilizationEmblem(id));
            SetRect(emblem.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(206f, 206f));

            var name = RunHudUiFactory.CreateText(card, "Name", def.Name, 29, TextAnchor.MiddleCenter, InkBrown,
                FontStyle.Bold);
            SetRect(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -278f), new Vector2(size.x - 70f, 38f));

            var tagline = RunHudUiFactory.CreateText(card, "Tagline", def.Tagline, 17, TextAnchor.MiddleCenter,
                InkMuted, FontStyle.Italic);
            SetRect(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -308f), new Vector2(size.x - 70f, 24f));

            var rule = CreateImage(card, "Rule", null);
            rule.color = new Color(0.55f, 0.40f, 0.22f, 0.55f);
            SetRect(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(size.x - 120f, 2f));

            var passive = RunHudUiFactory.CreateText(card, "Passive", def.Passive, 19, TextAnchor.UpperCenter,
                InkGood, FontStyle.Bold);
            SetRect(passive.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -384f), new Vector2(size.x - 80f, 90f));

            var drawback = RunHudUiFactory.CreateText(card, "Drawback",
                def.HasDrawback ? "Drawback: " + def.Drawback : "No drawback.", 17, TextAnchor.UpperCenter,
                def.HasDrawback ? InkBad : InkMuted, def.HasDrawback ? FontStyle.Normal : FontStyle.Italic);
            SetRect(drawback.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -462f), new Vector2(size.x - 80f, 52f));
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
            var root = CreateFullscreenDim("EventCard", new Color(0.04f, 0.03f, 0.02f, 0.45f));
            var dimButton = root.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => DismissEventCard(key));

            var card = CreateCardFrame(root, "Card", wide: true);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(-40f, 60f), new Vector2(EventCardWidth, EventCardHeight));
            var cardButton = card.gameObject.AddComponent<Button>();
            cardButton.targetGraphic = card.GetComponent<Image>();
            cardButton.onClick.AddListener(() => DismissEventCard(key));

            var emblem = CreateImage(card, "Emblem", ColonistRoguelikeArt.GetEventEmblem(state.ActiveEvent));
            SetRect(emblem.rectTransform, new Vector2(0f, 0.5f), new Vector2(215f, 4f), new Vector2(250f, 250f));

            float textX = 368f, textW = EventCardWidth - textX - 100f;
            var kicker = RunHudUiFactory.CreateText(card, "Kicker",
                $"EVENT  ·  DAY {state.EventDay}  ·  {CategoryLabel(def.Category)}", 18, TextAnchor.MiddleLeft,
                new Color(0.56f, 0.40f, 0.14f, 1f), FontStyle.Bold);
            SetRect(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(textX + textW / 2f, -112f),
                new Vector2(textW, 24f));

            var name = RunHudUiFactory.CreateText(card, "Name", def.Name, 46, TextAnchor.MiddleLeft, InkBrown,
                FontStyle.Bold);
            SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(textX + textW / 2f, -156f),
                new Vector2(textW, 52f));

            var body = RunHudUiFactory.CreateText(card, "Description", def.Description, 23, TextAnchor.UpperLeft,
                InkBrown);
            SetRect(body.rectTransform, new Vector2(0f, 1f), new Vector2(textX + textW / 2f, -236f),
                new Vector2(textW, 84f));

            if (!string.IsNullOrEmpty(state.EventDetail))
            {
                var detail = RunHudUiFactory.CreateText(card, "Detail", state.EventDetail, 23, TextAnchor.UpperLeft,
                    def.Category == EventCategory.Hardship || def.Category == EventCategory.Robber ? InkBad : InkGood,
                    FontStyle.Bold);
                SetRect(detail.rectTransform, new Vector2(0f, 1f), new Vector2(textX + textW / 2f, -322f),
                    new Vector2(textW, 64f));
            }

            int act = Core.Progression.ActProgression.GetAct(state.Board.DayNumber);
            int next = EventEngine.DaysUntilNextEvent(state.Board.DayNumber + 1, act) + 1;
            var hint = RunHudUiFactory.CreateText(card, "Hint",
                $"Next event in {next} day{(next == 1 ? "" : "s")}   ·   click to continue", 17, TextAnchor.MiddleLeft, InkMuted,
                FontStyle.Italic);
            SetRect(hint.rectTransform, new Vector2(0f, 0f), new Vector2(textX + textW / 2f, 104f),
                new Vector2(textW, 22f));
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
                    _civEmblemInPanel.raycastTarget = false;
                }
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
