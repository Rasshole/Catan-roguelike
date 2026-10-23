using System.Collections.Generic;
using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Trade;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private Text _tradeSummary;
        private Button _openTradeDialogButton;
        private RectTransform _tradeDialogRoot;
        private Text _tradeDialogInfo;
        private RectTransform _playerTradeResponsesRoot;
        private ResourceType _tradeGive = ResourceType.Wood;
        private ResourceType _tradeReceive = ResourceType.Brick;
        private readonly List<(ResourceType resource, Image card)> _tradeGiveChips = new();
        private readonly List<(ResourceType resource, Image card)> _tradeReceiveChips = new();
        private static readonly Color TradeChipSelected = new(0.70f, 0.90f, 0.60f, 1f);

        private void BuildTradePanel(RectTransform sidePanel)
        {
            AddPanelLabel(sidePanel, ColonistHudCopy.Trade, 17);
            _tradeSummary = AddPanelText(sidePanel, 14, FontStyle.Normal);
            _openTradeDialogButton = RunHudUiFactory.CreateButton(sidePanel, ColonistHudCopy.Trade, RunHudUiFactory.ButtonSecondary);
            _openTradeDialogButton.onClick.AddListener(() => SetTradeDialogVisible(true));
            RunHudUiFactory.AddLayoutElement(_openTradeDialogButton.gameObject, 36f);
            UpdateTradeInfo();
        }

        private void EnsureTradeDialogBuilt()
        {
            if (_tradeDialogRoot != null || _canvas == null)
                return;

            _tradeDialogRoot = RunHudUiFactory.CreatePanel(
                _canvas.transform,
                "TradeDialog",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                ColonistPresentation.IsActive ? new Vector2(-290f, -230f) : new Vector2(-220f, -160f),
                ColonistPresentation.IsActive ? new Vector2(290f, 230f) : new Vector2(220f, 160f));
            if (ColonistPresentation.IsActive)
            {
                // v132: painted card dialog, shifted left of centre so it does not sit on the rail.
                ColonistKenneyHudArt.ApplyOrnatePanelImage(_tradeDialogRoot.GetComponent<Image>());
                _tradeDialogRoot.anchoredPosition = new Vector2(-150f, 40f);
                var dialogShadow = _tradeDialogRoot.gameObject.AddComponent<Shadow>();
                dialogShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
                dialogShadow.effectDistance = new Vector2(5f, -7f);
            }
            else
                _tradeDialogRoot.GetComponent<Image>().color = new Color(0.10f, 0.14f, 0.20f, 0.97f);

            var layout = _tradeDialogRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = ColonistPresentation.IsActive ? new RectOffset(34, 34, 30, 30) : new RectOffset(14, 14, 12, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            bool painted = ColonistPresentation.IsActive;
            var title = AddPanelText(_tradeDialogRoot, painted ? 26 : 18, FontStyle.Bold);
            title.alignment = TextAnchor.MiddleCenter;
            title.text = ColonistHudCopy.TradeDialogTitle;
            if (painted)
            {
                title.color = ColonistUiKit.Ink;
                var ruleGo = new GameObject("TitleRule");
                ruleGo.transform.SetParent(_tradeDialogRoot, false);
                ruleGo.AddComponent<RectTransform>();
                var rule = ruleGo.AddComponent<Image>();
                rule.color = ColonistUiKit.Rule;
                RunHudUiFactory.AddLayoutElement(ruleGo, 2f);
            }

            _tradeDialogInfo = AddPanelText(_tradeDialogRoot, painted ? 16 : 14, FontStyle.Normal);
            _tradeDialogInfo.alignment = painted ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            if (painted)
            {
                _tradeDialogInfo.color = ColonistUiKit.Ink;
                _tradeDialogInfo.transform.parent.GetComponent<LayoutElement>().preferredHeight = 44f;
            }

            BuildTradeDialogResourceRow(_tradeDialogRoot, "You give:", true);
            BuildTradeDialogResourceRow(_tradeDialogRoot, "You receive:", false);

            var actions = new GameObject("TradeDialogActions");
            actions.layer = _tradeDialogRoot.gameObject.layer;
            actions.transform.SetParent(_tradeDialogRoot, false);
            var actionsLayout = actions.AddComponent<HorizontalLayoutGroup>();
            actionsLayout.spacing = 8f;
            actionsLayout.childAlignment = TextAnchor.MiddleCenter;
            RunHudUiFactory.AddLayoutElement(actions, painted ? 46f : 40f);

            var bankBtn = RunHudUiFactory.CreateButton(actions.transform, "Bank", RunHudUiFactory.ButtonFill);
            bankBtn.onClick.AddListener(ExecuteBankTrade);
            var offerBtn = RunHudUiFactory.CreateButton(actions.transform, ColonistHudCopy.OfferAi, RunHudUiFactory.ButtonSecondary);
            offerBtn.onClick.AddListener(ExecutePlayerTradeProposal);
            var closeBtn = RunHudUiFactory.CreateButton(actions.transform, ColonistHudCopy.Close, RunHudUiFactory.ButtonSecondary);
            closeBtn.onClick.AddListener(() => SetTradeDialogVisible(false));
            if (painted)
            {
                foreach (var (btn, green) in new[] { (bankBtn, true), (offerBtn, false), (closeBtn, false) })
                {
                    ColonistKenneyHudArt.ApplyColonistWarmButton(btn.GetComponent<Image>(), primaryGreen: green);
                    var label = btn.GetComponentInChildren<Text>();
                    if (label == null)
                        continue;
                    label.color = green ? ColonistUiKit.Cream : ColonistUiKit.Ink;
                    label.fontSize = 17;
                    label.fontStyle = FontStyle.Bold;
                }
            }

            var responsesGo = new GameObject("PlayerTradeResponses");
            responsesGo.layer = _tradeDialogRoot.gameObject.layer;
            responsesGo.transform.SetParent(_tradeDialogRoot, false);
            _playerTradeResponsesRoot = responsesGo.AddComponent<RectTransform>();
            var responsesLayout = responsesGo.AddComponent<VerticalLayoutGroup>();
            responsesLayout.spacing = 4f;
            responsesLayout.childControlWidth = true;
            responsesLayout.childForceExpandWidth = true;
            RunHudUiFactory.AddLayoutElement(responsesGo, 8f);

            SetTradeDialogVisible(false);
        }

        private void BuildTradeDialogResourceRow(RectTransform parent, string label, bool giveRow)
        {
            var row = new GameObject(giveRow ? "GiveRow" : "ReceiveRow");
            row.layer = parent.gameObject.layer;
            row.transform.SetParent(parent, false);
            var col = row.AddComponent<VerticalLayoutGroup>();
            col.spacing = 4f;
            bool painted = ColonistPresentation.IsActive;
            RunHudUiFactory.AddLayoutElement(row, painted ? 112f : 52f);

            var header = RunHudUiFactory.CreateText(
                row.transform,
                "Header",
                painted ? label.ToUpperInvariant() : label,
                13,
                TextAnchor.UpperLeft,
                painted ? ColonistUiKit.InkGold : RunHudUiFactory.TextMuted,
                painted ? FontStyle.Bold : FontStyle.Normal);
            if (painted)
                RunHudUiFactory.AddLayoutElement(header.gameObject, 18f);

            var chips = new GameObject("Chips");
            chips.layer = row.layer;
            chips.transform.SetParent(row.transform, false);
            var chipLayout = chips.AddComponent<HorizontalLayoutGroup>();
            chipLayout.spacing = 4f;
            chipLayout.childAlignment = TextAnchor.MiddleLeft;

            if (painted)
            {
                chipLayout.spacing = 10f;
                chipLayout.childAlignment = TextAnchor.MiddleCenter;
                chipLayout.childForceExpandWidth = false;
                chipLayout.childControlWidth = true;
                chipLayout.childControlHeight = true;
                RunHudUiFactory.AddLayoutElement(chips, 86f);
                foreach (ResourceType resource in ResourceTypes.Productive)
                    BuildTradeResourceCard(chips.transform, resource, giveRow);
                return;
            }

            foreach (ResourceType resource in ResourceTypes.Productive)
            {
                var captured = resource;
                string shortName = ResourceDisplayNames.GetEnglish(captured);
                var btn = RunHudUiFactory.CreateButton(chips.transform, shortName, RunHudUiFactory.ButtonSecondary);
                RunHudUiFactory.AddLayoutElement(btn.gameObject, 30f).preferredWidth = 72f;
                btn.onClick.AddListener(() =>
                {
                    if (giveRow)
                        _tradeGive = captured;
                    else
                        _tradeReceive = captured;
                    UpdateTradeInfo();
                });
            }
        }

        /// <summary>Painted mini resource card (same as the hand) that selects the give / receive resource.</summary>
        private void BuildTradeResourceCard(Transform parent, ResourceType resource, bool giveRow)
        {
            var go = new GameObject($"Trade_{resource}");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            RunHudUiFactory.AddLayoutElement(go, 86f).preferredWidth = 84f;
            var bg = go.AddComponent<Image>();
            ColonistUiKit.Apply(bg, UiKitPiece.FrameThin);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
            colors.pressedColor = new Color(0.86f, 0.80f, 0.70f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() =>
            {
                if (giveRow)
                    _tradeGive = resource;
                else
                    _tradeReceive = resource;
                UpdateTradeInfo();
            });

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            var icon = iconGo.AddComponent<Image>();
            // Icon sits in the upper part of the card so the name label below stays readable.
            ColonistHudIconGraphic.ApplyCardIcon(icon, ColonistHudResourceSprites.Get(resource), fill: 0.56f,
                centerY: 0.60f);
            icon.raycastTarget = false;

            var name = RunHudUiFactory.CreateText(go.transform, "Name", ResourceDisplayNames.GetEnglish(resource), 12,
                TextAnchor.LowerCenter, ColonistUiKit.Ink, FontStyle.Bold);
            name.rectTransform.offsetMin = new Vector2(4f, 7f);
            name.rectTransform.offsetMax = new Vector2(-4f, 0f);
            name.raycastTarget = false;

            (giveRow ? _tradeGiveChips : _tradeReceiveChips).Add((resource, bg));
        }

        private void RefreshTradeChipSelection()
        {
            foreach (var (resource, card) in _tradeGiveChips)
                if (card != null)
                    card.color = resource == _tradeGive ? TradeChipSelected : Color.white;
            foreach (var (resource, card) in _tradeReceiveChips)
                if (card != null)
                    card.color = resource == _tradeReceive ? TradeChipSelected : Color.white;
        }

        public void OpenTradeDialogForTests()
        {
            EnsureTradeDialogBuilt();
            SetTradeDialogVisible(true);
        }

        public bool IsTradeDialogVisibleForTests => _tradeDialogRoot != null && _tradeDialogRoot.gameObject.activeInHierarchy;

        private void SetTradeDialogVisible(bool visible)
        {
            if (_tradeDialogRoot != null)
                _tradeDialogRoot.gameObject.SetActive(visible);
            if (visible)
                UpdateTradeInfo();
        }

        private void UpdateTradeInfo()
        {
            if (_controller == null)
                return;

            int rate = BankTradeCalculator.GetGiveRateForResource(_controller.State, PlayerId.Human, _tradeGive);
            string rateLabel = rate == BankTradeCalculator.SpecificPortRate ? "2:1 port"
                : rate == BankTradeCalculator.GenericPortRate ? "3:1 port" : "4:1 bank";
            var sb = new StringBuilder();
            sb.AppendLine($"{ResourceDisplayNames.GetEnglish(_tradeGive)} → {ResourceDisplayNames.GetEnglish(_tradeReceive)}");
            sb.AppendLine($"{rateLabel} (pay {rate} {ResourceDisplayNames.GetEnglish(_tradeGive)})");

            if (_controller.State.PendingPlayerTrade.HasValue)
            {
                var offer = _controller.State.PendingPlayerTrade.Value;
                sb.AppendLine($"Your offer: {FormatTradeBundle(offer.Give)} → {FormatTradeBundle(offer.Receive)}");
            }

            string body = sb.ToString().TrimEnd();
            if (_tradeSummary != null)
                _tradeSummary.text = body;
            if (_tradeDialogInfo != null)
                _tradeDialogInfo.text = body;
            RefreshTradeChipSelection();
            RebuildPlayerTradeResponses(_controller.State);
        }

        private void RebuildPlayerTradeResponses(GameState state)
        {
            if (_playerTradeResponsesRoot == null)
                return;

            for (int i = _playerTradeResponsesRoot.childCount - 1; i >= 0; i--)
                ColonistSceneDestroy.SafeDestroy(_playerTradeResponsesRoot.GetChild(i).gameObject);

            if (!state.PendingPlayerTrade.HasValue || state.PendingAiTradeResponses == null)
                return;

            foreach (var response in state.PendingAiTradeResponses)
            {
                string aiLabel = response.Ai == PlayerId.Ai ? "AI" : response.Ai.ToString();
                string status = response.Accept ? "accepterer" : "afviser";
                var row = new GameObject($"Response_{aiLabel}");
                row.layer = _playerTradeResponsesRoot.gameObject.layer;
                row.transform.SetParent(_playerTradeResponsesRoot, false);
                var layout = row.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 6f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                RunHudUiFactory.AddLayoutElement(row, 32f);

                var label = RunHudUiFactory.CreateText(
                    row.transform,
                    "Label",
                    $"{aiLabel}: {status}",
                    14,
                    TextAnchor.MiddleLeft,
                    ColonistPresentation.IsActive ? ColonistUiKit.Ink : RunHudUiFactory.TextMuted);
                RunHudUiFactory.AddLayoutElement(label.gameObject, 28f).flexibleWidth = 1f;

                if (response.Accept)
                {
                    var acceptBtn = RunHudUiFactory.CreateButton(row.transform, ColonistHudCopy.Accept, RunHudUiFactory.ButtonFill);
                    var captured = response.Ai;
                    acceptBtn.onClick.AddListener(() =>
                    {
                        if (_controller != null)
                            _controller.AcceptPlayerTradeWith(captured);
                    });
                }
            }
        }

        private static string FormatTradeBundle(ResourceBundle bundle)
        {
            var sb = new StringBuilder();
            foreach (var pair in bundle.EnumerateNonZero())
            {
                if (sb.Length > 0)
                    sb.Append('+');
                sb.Append(pair.amount);
                sb.Append(ResourceDisplayNames.GetEnglish(pair.type)[0]);
            }

            return sb.Length == 0 ? "—" : sb.ToString();
        }

        private void ExecuteBankTrade()
        {
            if (_controller == null)
                return;
            if (_controller.TryBankTrade(_tradeGive, _tradeReceive, 1))
                UpdateTradeInfo();
        }

        private void ExecutePlayerTradeProposal()
        {
            if (_controller == null)
                return;

            var give = ResourceBundle.Zero;
            give.Set(_tradeGive, 1);
            var receive = ResourceBundle.Zero;
            receive.Set(_tradeReceive, 1);
            var offer = new PlayerTradeOffer(give, receive);
            if (_controller.ProposePlayerTrade(offer))
                UpdateTradeInfo();
            else
            {
                HudToastBuffer.Push(_controller.State, "Kunne ikke sende byttebud.");
                Refresh(_controller.State);
            }
        }

        private void RefreshTradePanel(GameState state)
        {
            if (state.Phase != GamePhase.DayPlayerActions || state.Winner.HasValue)
            {
                SetTradeDialogVisible(false);
                return;
            }

            UpdateTradeInfo();
        }
    }
}
