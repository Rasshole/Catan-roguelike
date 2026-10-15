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
        private Text _tradeInfo;
        private ResourceType _tradeGive = ResourceType.Wood;
        private ResourceType _tradeReceive = ResourceType.Brick;
        private RectTransform _playerTradeResponsesRoot;

        private void BuildTradePanel(RectTransform sidePanel)
        {
            AddPanelLabel(sidePanel, "Bytte", 17);
            _tradeInfo = AddPanelText(sidePanel, 14, FontStyle.Normal);

            var row = new GameObject("TradeGiveRow");
            row.transform.SetParent(sidePanel, false);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 4f;
            RunHudUiFactory.AddLayoutElement(row, 32f);

            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                var captured = resource;
                var btn = RunHudUiFactory.CreateButton(row.transform, ResourceDisplayNames.GetDanish(captured), RunHudUiFactory.ButtonSecondary);
                btn.onClick.AddListener(() =>
                {
                    _tradeGive = captured;
                    UpdateTradeInfo();
                });
            }

            var row2 = new GameObject("TradeReceiveRow");
            row2.transform.SetParent(sidePanel, false);
            var row2Layout = row2.AddComponent<HorizontalLayoutGroup>();
            row2Layout.spacing = 4f;
            RunHudUiFactory.AddLayoutElement(row2, 32f);

            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                var captured = resource;
                var btn = RunHudUiFactory.CreateButton(row2.transform, "→ " + ResourceDisplayNames.GetDanish(captured), RunHudUiFactory.ButtonSecondary);
                btn.onClick.AddListener(() =>
                {
                    _tradeReceive = captured;
                    UpdateTradeInfo();
                });
            }

            var tradeBtn = RunHudUiFactory.CreateButton(sidePanel, "Byt 1 (bank/havn)", RunHudUiFactory.ButtonFill);
            tradeBtn.onClick.AddListener(ExecuteBankTrade);
            RunHudUiFactory.AddLayoutElement(tradeBtn.gameObject, 36f);

            var proposeBtn = RunHudUiFactory.CreateButton(sidePanel, "Tilbyd 1:1 til AI", RunHudUiFactory.ButtonSecondary);
            proposeBtn.onClick.AddListener(ExecutePlayerTradeProposal);
            RunHudUiFactory.AddLayoutElement(proposeBtn.gameObject, 36f);

            var responsesGo = new GameObject("PlayerTradeResponses");
            responsesGo.transform.SetParent(sidePanel, false);
            _playerTradeResponsesRoot = responsesGo.AddComponent<RectTransform>();
            var responsesLayout = responsesGo.AddComponent<VerticalLayoutGroup>();
            responsesLayout.spacing = 4f;
            responsesLayout.childControlWidth = true;
            responsesLayout.childForceExpandWidth = true;
            RunHudUiFactory.AddLayoutElement(responsesGo, 8f);

            UpdateTradeInfo();
        }

        private void UpdateTradeInfo()
        {
            if (_tradeInfo == null || _controller == null)
                return;

            int rate = BankTradeCalculator.GetGiveRateForResource(_controller.State, PlayerId.Human, _tradeGive);
            string rateLabel = rate == BankTradeCalculator.SpecificPortRate ? "2:1 havn"
                : rate == BankTradeCalculator.GenericPortRate ? "3:1 havn" : "4:1 bank";
            var sb = new StringBuilder();
            sb.AppendLine($"Giv {_tradeGive} → modtag {_tradeReceive}");
            sb.AppendLine($"{rateLabel} (betaler {rate} {ResourceDisplayNames.GetDanish(_tradeGive)})");

            if (_controller.State.PendingPlayerTrade.HasValue)
            {
                var offer = _controller.State.PendingPlayerTrade.Value;
                sb.AppendLine($"Dit bud: {FormatTradeBundle(offer.Give)} → {FormatTradeBundle(offer.Receive)}");
            }

            _tradeInfo.text = sb.ToString().TrimEnd();
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
                    var acceptBtn = RunHudUiFactory.CreateButton(row.transform, "Gennemfør", RunHudUiFactory.ButtonFill);
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
                sb.Append(ResourceDisplayNames.GetDanish(pair.type)[0]);
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
                return;
            UpdateTradeInfo();
        }
    }
}
