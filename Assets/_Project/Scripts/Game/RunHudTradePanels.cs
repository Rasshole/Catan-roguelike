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
                new Vector2(-220f, -160f),
                new Vector2(220f, 160f));
            _tradeDialogRoot.GetComponent<Image>().color = new Color(0.10f, 0.14f, 0.20f, 0.97f);

            var layout = _tradeDialogRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            var title = AddPanelText(_tradeDialogRoot, 18, FontStyle.Bold);
            title.alignment = TextAnchor.MiddleCenter;
            title.text = ColonistHudCopy.TradeDialogTitle;

            _tradeDialogInfo = AddPanelText(_tradeDialogRoot, 14, FontStyle.Normal);
            _tradeDialogInfo.alignment = TextAnchor.UpperLeft;

            BuildTradeDialogResourceRow(_tradeDialogRoot, "You give:", true);
            BuildTradeDialogResourceRow(_tradeDialogRoot, "You receive:", false);

            var actions = new GameObject("TradeDialogActions");
            actions.layer = _tradeDialogRoot.gameObject.layer;
            actions.transform.SetParent(_tradeDialogRoot, false);
            var actionsLayout = actions.AddComponent<HorizontalLayoutGroup>();
            actionsLayout.spacing = 8f;
            actionsLayout.childAlignment = TextAnchor.MiddleCenter;
            RunHudUiFactory.AddLayoutElement(actions, 40f);

            var bankBtn = RunHudUiFactory.CreateButton(actions.transform, "Bank", RunHudUiFactory.ButtonFill);
            bankBtn.onClick.AddListener(ExecuteBankTrade);
            var offerBtn = RunHudUiFactory.CreateButton(actions.transform, ColonistHudCopy.OfferAi, RunHudUiFactory.ButtonSecondary);
            offerBtn.onClick.AddListener(ExecutePlayerTradeProposal);
            var closeBtn = RunHudUiFactory.CreateButton(actions.transform, ColonistHudCopy.Close, RunHudUiFactory.ButtonSecondary);
            closeBtn.onClick.AddListener(() => SetTradeDialogVisible(false));

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
            RunHudUiFactory.AddLayoutElement(row, 52f);

            var header = RunHudUiFactory.CreateText(
                row.transform,
                "Header",
                label,
                13,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextMuted);

            var chips = new GameObject("Chips");
            chips.layer = row.layer;
            chips.transform.SetParent(row.transform, false);
            var chipLayout = chips.AddComponent<HorizontalLayoutGroup>();
            chipLayout.spacing = 4f;
            chipLayout.childAlignment = TextAnchor.MiddleLeft;

            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
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
            string rateLabel = rate == BankTradeCalculator.SpecificPortRate ? "2:1 havn"
                : rate == BankTradeCalculator.GenericPortRate ? "3:1 havn" : "4:1 bank";
            var sb = new StringBuilder();
            sb.AppendLine($"{ResourceDisplayNames.GetEnglish(_tradeGive)} → {ResourceDisplayNames.GetEnglish(_tradeReceive)}");
            sb.AppendLine($"{rateLabel} (pay {rate} {ResourceDisplayNames.GetEnglish(_tradeGive)})");

            if (_controller.State.PendingPlayerTrade.HasValue)
            {
                var offer = _controller.State.PendingPlayerTrade.Value;
                sb.AppendLine($"Dit bud: {FormatTradeBundle(offer.Give)} → {FormatTradeBundle(offer.Receive)}");
            }

            string body = sb.ToString().TrimEnd();
            if (_tradeSummary != null)
                _tradeSummary.text = body;
            if (_tradeDialogInfo != null)
                _tradeDialogInfo.text = body;
            RebuildPlayerTradeResponses(_controller.State);
        }

        private void RebuildPlayerTradeResponses(GameState state)
        {
            if (_playerTradeResponsesRoot == null)
                return;

            for (int i = _playerTradeResponsesRoot.childCount - 1; i >= 0; i--)
                Destroy(_playerTradeResponsesRoot.GetChild(i).gameObject);

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
                    RunHudUiFactory.TextMuted);
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
