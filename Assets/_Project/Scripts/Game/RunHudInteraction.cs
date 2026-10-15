using System.Collections;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Trade;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>Pinned controls, trade panel, and resource animation hooks for <see cref="RunHudController"/>.</summary>
    public sealed partial class RunHudController
    {
        private GameplayFeelController _feel;
        private Button _buildRoadButton;
        private Button _buildSettlementButton;
        private Button _buildCityButton;
        private Button _endTurnPinned;
        private Button _rollPinned;
        private Text _diceOverlay;
        private Text _tradeInfo;
        private ResourceType _tradeGive = ResourceType.Wood;
        private ResourceType _tradeReceive = ResourceType.Brick;
        private bool _resourceAnimLock;
        private ResourceBundle _resourceAnimDisplay;

        public void SetDiceOverlay(string text)
        {
            if (_diceOverlay != null)
                _diceOverlay.text = text ?? string.Empty;
        }

        public void BeginResourceAnimationLock() => _resourceAnimLock = true;

        public void EndResourceAnimationLock()
        {
            _resourceAnimLock = false;
            if (_controller != null)
                RefreshResources(_controller.State.PlayerInventory);
        }

        public IEnumerator AnimateResourceCounts(ResourceBundle from, ResourceBundle to, float duration)
        {
            BeginResourceAnimationLock();
            yield return AnimateOne(ResourceType.Wood, from.Wood, to.Wood, duration);
            yield return AnimateOne(ResourceType.Brick, from.Brick, to.Brick, duration);
            yield return AnimateOne(ResourceType.Wheat, from.Wheat, to.Wheat, duration);
            yield return AnimateOne(ResourceType.Sheep, from.Sheep, to.Sheep, duration);
            yield return AnimateOne(ResourceType.Stone, from.Stone, to.Stone, duration);
            EndResourceAnimationLock();
        }

        private IEnumerator AnimateOne(ResourceType type, int from, int to, float duration)
        {
            if (from == to)
                yield break;

            int index = (int)type;
            if (index < 0 || index >= _resourceAmountTexts.Count)
                yield break;

            var label = _resourceAmountTexts[index];
            var rect = label.rectTransform;
            Vector3 baseScale = rect.localScale;
            yield return SimpleTweens.AnimateInt(from, to, duration, value =>
            {
                _resourceAnimDisplay.Set(type, value);
                label.text = HudResourceFormatting.FormatInventoryLine(_resourceAnimDisplay, type);
                float pop = 1f + Mathf.Sin(Time.time * 24f) * 0.04f;
                rect.localScale = baseScale * pop;
            });
            rect.localScale = baseScale;
        }

        private void BuildPinnedControls(Transform canvasRoot)
        {
            var bar = RunHudUiFactory.CreatePanel(
                canvasRoot,
                "PinnedControls",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-280f, 12f),
                new Vector2(-12f, 64f));
            bar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.childAlignment = TextAnchor.MiddleRight;

            _rollPinned = RunHudUiFactory.CreateButton(bar, "Rul (Space)", RunHudUiFactory.ButtonFill);
            _rollPinned.onClick.AddListener(() => _feel?.RequestMorningRoll());

            _endTurnPinned = RunHudUiFactory.CreateButton(bar, "Afslut dag", RunHudUiFactory.ButtonSecondary);
            _endTurnPinned.onClick.AddListener(() =>
            {
                if (_controller != null)
                    _controller.EndPlayerDay();
            });

            var overlayGo = new GameObject("DiceOverlay");
            overlayGo.transform.SetParent(canvasRoot, false);
            var overlayRect = overlayGo.AddComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
            overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRect.sizeDelta = new Vector2(420f, 80f);
            _diceOverlay = RunHudUiFactory.CreateText(
                overlayGo.transform,
                "Dice",
                string.Empty,
                42,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);
        }

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
            tradeBtn.onClick.AddListener(ExecuteTrade);
            RunHudUiFactory.AddLayoutElement(tradeBtn.gameObject, 36f);
            UpdateTradeInfo();
        }

        private void UpdateTradeInfo()
        {
            if (_tradeInfo == null || _controller == null)
                return;

            int rate = BankTradeCalculator.GetGiveRateForResource(_controller.State, PlayerId.Human, _tradeGive);
            string rateLabel = rate == BankTradeCalculator.SpecificPortRate ? "2:1 havn"
                : rate == BankTradeCalculator.GenericPortRate ? "3:1 havn" : "4:1 bank";
            _tradeInfo.text =
                $"Giv {_tradeGive} → modtag {_tradeReceive}\n{rateLabel} (betaler {rate} {ResourceDisplayNames.GetDanish(_tradeGive)})";
        }

        private void ExecuteTrade()
        {
            if (_controller == null)
                return;
            if (_controller.TryBankTrade(_tradeGive, _tradeReceive, 1))
                UpdateTradeInfo();
        }

        private void RefreshPinnedControls(GameState state)
        {
            if (_endTurnPinned != null)
            {
                bool showEnd = state.Phase == GamePhase.DayPlayerActions && !state.Winner.HasValue;
                _endTurnPinned.gameObject.SetActive(showEnd);
                _endTurnPinned.interactable = showEnd;
            }

            if (_rollPinned != null)
            {
                bool showRoll = state.Phase == GamePhase.DayProduction && !state.Winner.HasValue;
                _rollPinned.gameObject.SetActive(showRoll);
                _rollPinned.interactable = showRoll && (_feel == null || !_feel.IsBlockingInput);
            }

            UpdateTradeInfo();
        }

        private void EnsureBuildButtons()
        {
            if (_buildRoadButton != null)
                return;

            _buildRoadButton = RunHudUiFactory.CreateButton(_actionBar, "Vej", RunHudUiFactory.ButtonSecondary);
            _buildSettlementButton = RunHudUiFactory.CreateButton(_actionBar, "By", RunHudUiFactory.ButtonSecondary);
            _buildCityButton = RunHudUiFactory.CreateButton(_actionBar, "Byudvid", RunHudUiFactory.ButtonSecondary);
            _actionButtons.Add(_buildRoadButton);
            _actionButtons.Add(_buildSettlementButton);
            _actionButtons.Add(_buildCityButton);
        }

        private void RefreshBuildButtons(GameState state)
        {
            if (state.Phase != GamePhase.DayPlayerActions || state.Winner.HasValue || _boardInput == null)
                return;

            var board = state.Board;
            var inv = state.PlayerInventory;

            ResourceBundle roadCost = BalanceConfig.GetRoadCost(board, PlayerId.Human);
            ResourceBundle settlementCost = BalanceConfig.GetSettlementCost(board, PlayerId.Human);
            ResourceBundle cityCost = BalanceConfig.GetCityCost(board, PlayerId.Human);

            ConfigureBuildButton(
                _buildRoadButton,
                BuildCostFormatter.FormatButtonLabel("Vej", roadCost, inv.CanAfford(roadCost)),
                inv.CanAfford(roadCost),
                () => _boardInput.SetBuildModeRoad());

            ConfigureBuildButton(
                _buildSettlementButton,
                BuildCostFormatter.FormatButtonLabel("By", settlementCost, inv.CanAfford(settlementCost)),
                inv.CanAfford(settlementCost),
                () => _boardInput.SetBuildModeSettlement());

            ConfigureBuildButton(
                _buildCityButton,
                BuildCostFormatter.FormatButtonLabel("Byudvid", cityCost, inv.CanAfford(cityCost)),
                inv.CanAfford(cityCost),
                () => _boardInput.SetBuildModeCity());
        }

        private static void ConfigureBuildButton(Button button, string label, bool canAfford, UnityEngine.Events.UnityAction onClick)
        {
            if (button == null)
                return;

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
                text.text = label;
            button.interactable = canAfford;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
        }

        private void RefreshResourcesPartial(ResourceBundle bundle)
        {
            if (_resourceAnimLock)
                bundle = _resourceAnimDisplay;
            RefreshResources(bundle);
        }
    }
}
