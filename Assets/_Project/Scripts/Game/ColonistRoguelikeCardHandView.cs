using System;
using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Turn;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>Fanned roguelike card hand (bottom center) for colonist HUD.</summary>
    public sealed class ColonistRoguelikeCardHandView : MonoBehaviour
    {
        public struct LayoutProbe
        {
            public int CardCount;
            public Vector2 ActionBarTopScreen;
            public float MinCardBottomY;
            public int HoveredSiblingIndex;
        }

        private RectTransform _fanRoot;
        private Text _drawPileText;
        private Text _discardPileText;
        private readonly List<CardWidget> _cards = new();
        private Action<int> _playRequested;
        private int _hoverIndex = -1;

        public static ColonistRoguelikeCardHandView Build(Transform canvasRoot)
        {
            float bottom = RunHudLayout.GetColonistDeckBandBottom();
            float height = RunHudLayout.ColonistRoguelikeHandHeight;
            float left = RunHudLayout.GetContentLeftInset() + RunHudLayout.ColonistResourceHandWidth + RunHudLayout.Margin;
            float right = RunHudLayout.GetContentRightInset();

            var panel = RunHudUiFactory.CreatePanel(
                canvasRoot,
                "RoguelikeCardHand",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(left, bottom),
                new Vector2(-right, bottom + height));
            // The band is only a layout region: an opaque parchment strip here hid the bottom of the
            // board (v109 review). Cards float over the board like Slay the Spire / Balatro.
            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0f, 0f, 0f, 0f);
                panelImage.raycastTarget = false;
            }

            var view = panel.gameObject.AddComponent<ColonistRoguelikeCardHandView>();
            view.Initialize(panel);
            return view;
        }

        private void Initialize(RectTransform panel)
        {
            var pileRow = new GameObject("PileCounters");
            pileRow.transform.SetParent(panel, false);
            var pileRect = pileRow.AddComponent<RectTransform>();
            pileRect.anchorMin = new Vector2(0.5f, 0f);
            pileRect.anchorMax = new Vector2(0.5f, 0f);
            pileRect.pivot = new Vector2(0.5f, 0f);
            pileRect.anchoredPosition = new Vector2(-(RunHudLayout.ColonistResourceHandWidth + RunHudLayout.Margin) * 0.5f, 6f);
            pileRect.sizeDelta = new Vector2(640f, 44f);
            // Parchment chips either side of the fan so the counters stay readable over the sea.
            _drawPileText = CreatePileChip(pileRow.transform, "Draw", "Draw\n0", leftSide: true);
            _discardPileText = CreatePileChip(pileRow.transform, "Discard", "Discard\n0", leftSide: false);

            var fanGo = new GameObject("FanRoot");
            fanGo.transform.SetParent(panel, false);
            _fanRoot = fanGo.AddComponent<RectTransform>();
            _fanRoot.anchorMin = new Vector2(0.5f, 0f);
            _fanRoot.anchorMax = new Vector2(0.5f, 0f);
            _fanRoot.pivot = new Vector2(0.5f, 0f);
            // Centre the fan under the board (play-area centre), not the band centre: the band
            // starts after the resource hand, which pushed the fan right of the board.
            _fanRoot.anchoredPosition = new Vector2(-(RunHudLayout.ColonistResourceHandWidth + RunHudLayout.Margin) * 0.5f, 20f);
            _fanRoot.sizeDelta = new Vector2(640f, ColonistRoguelikeCardHandLayout.CardHeight + 40f);
        }

        private static Text CreatePileChip(Transform parent, string name, string text, bool leftSide)
        {
            var chipGo = new GameObject(name);
            chipGo.transform.SetParent(parent, false);
            var rect = chipGo.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(leftSide ? 0f : 1f, 0f);
            rect.pivot = new Vector2(leftSide ? 0f : 1f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(72f, 44f);
            var bg = chipGo.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyColonistCountBadge(bg);
            bg.raycastTarget = false;
            var label = RunHudUiFactory.CreateText(
                chipGo.transform,
                "Label",
                text,
                13,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.ColonistTextOnPanel,
                FontStyle.Bold);
            label.raycastTarget = false;
            return label;
        }

        public void Refresh(GameState state, Action<int> playRequested)
        {
            _playRequested = playRequested;
            if (state == null)
                return;

            int handCount = state.PlayerHand.Count;
            bool show = !state.Winner.HasValue && handCount > 0;
            gameObject.SetActive(show);
            if (!show)
                return;

            _drawPileText.text = $"Draw\n{Mathf.Max(0, BalanceConfig.MaxHandSize * 3 - handCount)}";
            _discardPileText.text = "Discard\n0";

            while (_cards.Count > handCount)
            {
                int last = _cards.Count - 1;
                ColonistSceneDestroy.SafeDestroy(_cards[last].Root.gameObject);
                _cards.RemoveAt(last);
            }

            while (_cards.Count < handCount)
            {
                int index = _cards.Count;
                _cards.Add(CreateCardWidget(index));
            }

            var poses = ColonistRoguelikeCardHandLayout.ComputeFanPoses(handCount);
            for (int i = 0; i < handCount; i++)
            {
                var cardId = state.PlayerHand[i];
                var def = CardLibrary.Get(cardId);
                _cards[i].Bind(def, i, OnHover, OnPlayRequested);
                ApplyPose(_cards[i], poses[i], hovered: i == _hoverIndex);
            }
        }

        public LayoutProbe GatherLayoutProbe(RectTransform actionBar)
        {
            var probe = new LayoutProbe { CardCount = _cards.Count, HoveredSiblingIndex = -1 };
            if (actionBar != null)
            {
                var corners = new Vector3[4];
                actionBar.GetWorldCorners(corners);
                probe.ActionBarTopScreen = corners[2];
            }

            float minBottom = float.MaxValue;
            for (int i = 0; i < _cards.Count; i++)
            {
                var rect = _cards[i].Root;
                var c = new Vector3[4];
                rect.GetWorldCorners(c);
                minBottom = Mathf.Min(minBottom, c[0].y);
                if (i == _hoverIndex)
                    probe.HoveredSiblingIndex = rect.GetSiblingIndex();
            }

            probe.MinCardBottomY = minBottom == float.MaxValue ? 0f : minBottom;
            return probe;
        }

        private void OnHover(int index, bool entering)
        {
            _hoverIndex = entering ? index : -1;
            var poses = ColonistRoguelikeCardHandLayout.ComputeFanPoses(_cards.Count);
            for (int i = 0; i < _cards.Count; i++)
            {
                ApplyPose(_cards[i], poses[i], hovered: i == _hoverIndex);
                if (i == _hoverIndex)
                    _cards[i].Root.SetAsLastSibling();
            }
        }

        private void OnPlayRequested(int index)
        {
            _playRequested?.Invoke(index);
        }

        private static void ApplyPose(CardWidget card, ColonistRoguelikeCardHandLayout.CardPose pose, bool hovered)
        {
            var rect = card.Root;
            float scale = hovered ? ColonistRoguelikeCardHandLayout.HoverScale : 1f;
            float lift = hovered ? ColonistRoguelikeCardHandLayout.HoverLift : 0f;
            rect.anchoredPosition = new Vector2(pose.X, pose.Y + lift);
            rect.localRotation = Quaternion.Euler(0f, 0f, pose.RotationZ);
            rect.localScale = Vector3.one * scale;
        }

        private CardWidget CreateCardWidget(int index)
        {
            var go = new GameObject($"RoguelikeCard_{index}");
            go.transform.SetParent(_fanRoot, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(
                ColonistRoguelikeCardHandLayout.CardWidth,
                ColonistRoguelikeCardHandLayout.CardHeight);
            rect.pivot = new Vector2(0.5f, 0f);
            // Anchor to the fan root's bottom edge: with the default centre anchor every card sat
            // ~100 px higher, over the bottom hexes (v109 review).
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);

            var bg = go.AddComponent<Image>();
            ColonistKenneyHudArt.ApplyColonistResourceCardBackground(bg, new Color(0.94f, 0.88f, 0.74f, 1f));

            var frameGo = new GameObject("Frame");
            frameGo.transform.SetParent(go.transform, false);
            var frameRect = frameGo.AddComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            // Warm inner parchment over the framed background (the CardBack_blue frame made every
            // card blue, v109 review).
            var frame = frameGo.AddComponent<Image>();
            frame.color = new Color(0.99f, 0.95f, 0.85f, 1f);
            frame.raycastTarget = false;
            frameRect.offsetMin = new Vector2(5f, 5f);
            frameRect.offsetMax = new Vector2(-5f, -5f);

            var border = new GameObject("Accent");
            border.transform.SetParent(go.transform, false);
            var borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0f, 1f);
            borderRect.anchorMax = new Vector2(1f, 1f);
            borderRect.pivot = new Vector2(0.5f, 1f);
            borderRect.sizeDelta = new Vector2(0f, 6f);
            var borderImg = border.AddComponent<Image>();
            borderImg.raycastTarget = false;

            var cost = RunHudUiFactory.CreateText(
                go.transform,
                "Cost",
                "Night",
                10,
                TextAnchor.UpperLeft,
                RunHudUiFactory.ColonistTextMutedOnPanel,
                FontStyle.Bold);
            cost.rectTransform.offsetMin = new Vector2(9f, -21f);
            cost.rectTransform.offsetMax = new Vector2(-9f, -8f);

            var title = RunHudUiFactory.CreateText(
                go.transform,
                "Title",
                "Card",
                13,
                TextAnchor.UpperCenter,
                RunHudUiFactory.ColonistTextOnPanel,
                FontStyle.Bold);
            // Below the category line (they overlapped at -36..-8 vs -22..-6).
            title.rectTransform.offsetMin = new Vector2(6f, -40f);
            title.rectTransform.offsetMax = new Vector2(-6f, -22f);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 10;
            title.resizeTextMaxSize = 13;

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.18f, 0.32f);
            iconRect.anchorMax = new Vector2(0.82f, 0.72f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.raycastTarget = false;

            var body = RunHudUiFactory.CreateText(
                go.transform,
                "Body",
                string.Empty,
                11,
                TextAnchor.LowerCenter,
                RunHudUiFactory.ColonistTextOnPanel);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0.06f, 0.06f);
            bodyRect.anchorMax = new Vector2(0.94f, 0.30f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;

            var drag = go.AddComponent<ColonistRoguelikeCardDrag>();
            drag.SetRelayoutCallback(RelayoutFan);
            return new CardWidget(rect, borderImg, iconImg, title, body, cost, drag);
        }

        private void RelayoutFan()
        {
            var poses = ColonistRoguelikeCardHandLayout.ComputeFanPoses(_cards.Count);
            for (int i = 0; i < _cards.Count; i++)
                ApplyPose(_cards[i], poses[i], hovered: i == _hoverIndex);
        }

        private sealed class CardWidget
        {
            public RectTransform Root { get; }
            private readonly Image _accent;
            private readonly Image _icon;
            private readonly Text _title;
            private readonly Text _body;
            private readonly Text _cost;
            private readonly ColonistRoguelikeCardDrag _drag;

            public CardWidget(
                RectTransform root,
                Image accent,
                Image icon,
                Text title,
                Text body,
                Text cost,
                ColonistRoguelikeCardDrag drag)
            {
                Root = root;
                _accent = accent;
                _icon = icon;
                _title = title;
                _body = body;
                _cost = cost;
                _drag = drag;
            }

            public void Bind(CardDefinition def, int index, Action<int, bool> hover, Action<int> play)
            {
                _title.text = def.Name;
                _body.text = def.Description;
                _cost.text = $"{CategoryLabelEnglish(def.Category)} · Night";
                var rgb = CardVisualStyle.GetCategoryColor(def.Category);
                _accent.color = new Color(rgb.R, rgb.G, rgb.B, 1f);
                _icon.color = new Color(rgb.R, rgb.G, rgb.B, 0.35f);
                _drag.Configure(index, hover, play);
            }
        }

        private static string CategoryLabelEnglish(CardCategory category) => category switch
        {
            CardCategory.RollManipulation => "Roll",
            CardCategory.BuildTempo => "Build",
            CardCategory.Synergy => "Synergy",
            CardCategory.Sabotage => "Sabotage",
            _ => "Card"
        };
    }

    internal sealed class ColonistRoguelikeCardDrag :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler
    {
        private int _index;
        private Action<int, bool> _hover;
        private Action<int> _play;
        private Vector2 _dragStart;
        private bool _dragging;
        private Action _relayout;

        public void SetRelayoutCallback(Action relayout) => _relayout = relayout;

        public void Configure(int index, Action<int, bool> hover, Action<int> play)
        {
            _index = index;
            _hover = hover;
            _play = play;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hover?.Invoke(_index, true);

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_dragging)
                _hover?.Invoke(_index, false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _dragStart = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var rect = transform as RectTransform;
            if (rect == null)
                return;

            rect.anchoredPosition += eventData.delta / rect.lossyScale.y;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            float upward = eventData.position.y - _dragStart.y;
            if (upward > 80f)
                _play?.Invoke(_index);
            else
                _relayout?.Invoke();
            _hover?.Invoke(_index, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_dragging)
                return;
            _play?.Invoke(_index);
        }
    }
}
