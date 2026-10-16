using System.Collections;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
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
        private Button _buildDevButton;
        private bool _resourceAnimLock;
        private ResourceBundle _resourceAnimDisplay;

        public void SetDiceOverlay(string text)
        {
            if (ColonistPresentation.IsActive && _colonistDiePipsA != null && _colonistDiePipsB != null)
            {
                if (string.IsNullOrEmpty(text))
                {
                    if (_controller != null)
                        RefreshColonistDiceFaces(_controller.State);
                    return;
                }

                if (TryParseDiePair(text, out int a, out int b))
                    SetColonistDieValues(a, b);

                return;
            }

            if (_diceWidgetValue != null && !string.IsNullOrEmpty(text))
                _diceWidgetValue.text = text;
        }

        private static bool TryParseDiePair(string text, out int a, out int b)
        {
            a = b = 0;
            if (string.IsNullOrEmpty(text))
                return false;

            int plus = text.IndexOf('+');
            if (plus > 0 && plus < text.Length - 1
                && int.TryParse(text.Substring(0, plus).Trim(), out a)
                && int.TryParse(text.Substring(plus + 1).Split(' ')[0].Trim(), out b))
                return true;

            return false;
        }

        public void BeginResourceAnimationLock() => _resourceAnimLock = true;

        public void EndResourceAnimationLock()
        {
            _resourceAnimLock = false;
            if (_controller != null)
            {
                if (ColonistPresentation.IsActive)
                    RefreshColonistResources(_controller.State.PlayerInventory);
                else
                    RefreshResources(_controller.State.PlayerInventory);
            }
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
                label.text = ColonistPresentation.IsActive
                    ? value.ToString()
                    : HudResourceFormatting.FormatInventoryLine(_resourceAnimDisplay, type);
                float pop = 1f + Mathf.Sin(Time.time * 24f) * 0.04f;
                rect.localScale = baseScale * pop;
            });
            rect.localScale = baseScale;
        }

        private void RefreshActionBarChrome(GameState state)
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
                SetRollPromptActive(showRoll && _rollPinned.interactable);
            }

            RefreshColonistTurnBanner(state);
            RefreshDiceWidget(state);
            RefreshTradePanel(state);
        }

        private Text _buildRoadChip;
        private Text _buildSettlementChip;
        private Text _buildCityChip;

        private void EnsureBuildButtons()
        {
            if (_buildRoadButton != null && _buildRoadButton)
                return;

            _buildRoadButton = null;
            _buildSettlementButton = null;
            _buildCityButton = null;

            RunHudCostTooltip.EnsureShared(_canvas.transform);
            if (ColonistPresentation.IsActive)
            {
                _buildRoadButton = ColonistBuildButtonChrome.Create(
                    _actionBarBuildRoot,
                    ColonistHudCopy.Road,
                    ColonistBuildButtonChrome.IconTintFor(ColonistHudCopy.Road),
                    out _buildRoadChip);
                _buildSettlementButton = ColonistBuildButtonChrome.Create(
                    _actionBarBuildRoot,
                    ColonistHudCopy.Settlement,
                    ColonistBuildButtonChrome.IconTintFor(ColonistHudCopy.Settlement),
                    out _buildSettlementChip);
                _buildCityButton = ColonistBuildButtonChrome.Create(
                    _actionBarBuildRoot,
                    ColonistHudCopy.City,
                    ColonistBuildButtonChrome.IconTintFor(ColonistHudCopy.City),
                    out _buildCityChip);
                Text devChip;
                _buildDevButton = ColonistBuildButtonChrome.Create(
                    _actionBarBuildRoot,
                    "Dev",
                    ColonistBuildButtonChrome.IconTintFor("Dev"),
                    out devChip);
            }
            else
            {
                _buildRoadButton = CreateBuildButton(ColonistHudCopy.Road, out _buildRoadChip);
                _buildSettlementButton = CreateBuildButton(ColonistHudCopy.Settlement, out _buildSettlementChip);
                _buildCityButton = CreateBuildButton(ColonistHudCopy.City, out _buildCityChip);
                Text devChip;
                _buildDevButton = CreateBuildButton(ColonistHudCopy.DevCard, out devChip);
            }
            _buildDevButton.interactable = false;
            var devTip = _buildDevButton.gameObject.AddComponent<RunHudCostTooltip>();
            devTip.SetTooltip(ColonistPresentation.IsActive
                ? "Buy a development card (classic rules coming soon)."
                : "Roguelike cards are played at night.");
            _actionButtons.Add(_buildRoadButton);
            _actionButtons.Add(_buildSettlementButton);
            _actionButtons.Add(_buildCityButton);
            _actionButtons.Add(_buildDevButton);
        }

        private Button CreateBuildButton(string title, out Text chipText)
        {
            var button = RunHudUiFactory.CreateButton(_actionBarBuildRoot, title, RunHudUiFactory.ButtonSecondary);
            if (ColonistPresentation.IsActive)
                RunHudUiFactory.AddLayoutElement(button.gameObject, 48f).preferredWidth = 84f;
            button.gameObject.AddComponent<RunHudCostTooltip>();

            var chipGo = new GameObject("Chip");
            chipGo.transform.SetParent(button.transform, false);
            var chipRect = chipGo.AddComponent<RectTransform>();
            chipRect.anchorMin = new Vector2(1f, 1f);
            chipRect.anchorMax = new Vector2(1f, 1f);
            chipRect.pivot = new Vector2(1f, 1f);
            chipRect.anchoredPosition = new Vector2(-4f, -4f);
            chipRect.sizeDelta = new Vector2(26f, 20f);
            var chipBg = chipGo.AddComponent<Image>();
            chipBg.color = new Color(0.1f, 0.12f, 0.16f, 0.9f);
            chipText = RunHudUiFactory.CreateText(
                chipGo.transform,
                "Count",
                "0",
                13,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);
            chipText.raycastTarget = false;
            return button;
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

            bool roadOk = inv.CanAfford(roadCost) && BuildingPieceLimits.CanPlaceRoad(board, PlayerId.Human);
            bool settlementOk = inv.CanAfford(settlementCost)
                && BuildingPieceLimits.CanPlaceSettlement(board, PlayerId.Human);
            bool cityOk = inv.CanAfford(cityCost) && BuildingPieceLimits.CanPlaceCity(board, PlayerId.Human);

            if (_buildRoadChip != null)
                _buildRoadChip.text = BuildingPieceLimits.GetRoadsRemaining(board, PlayerId.Human).ToString();
            if (_buildSettlementChip != null)
                _buildSettlementChip.text = BuildingPieceLimits.GetSettlementsRemaining(board, PlayerId.Human).ToString();
            if (_buildCityChip != null)
                _buildCityChip.text = BuildingPieceLimits.GetCitiesRemaining(board, PlayerId.Human).ToString();
            if (_buildDevButton != null)
            {
                var devChip = _buildDevButton.GetComponentInChildren<Text>();
                if (devChip != null && devChip.gameObject.name == "Count")
                    devChip.text = state.PlayerHand.Count.ToString();
            }

            bool compact = ColonistPresentation.IsActive;
            ConfigureBuildButton(
                _buildRoadButton,
                compact ? ColonistHudCopy.Road : BuildCostFormatter.FormatButtonLabel(ColonistHudCopy.Road, roadCost, roadOk),
                BuildCostFormatter.FormatTooltip(roadCost),
                roadOk,
                () => _boardInput.SetBuildModeRoad());

            ConfigureBuildButton(
                _buildSettlementButton,
                compact ? ColonistHudCopy.Settlement : BuildCostFormatter.FormatButtonLabel(ColonistHudCopy.Settlement, settlementCost, settlementOk),
                BuildCostFormatter.FormatTooltip(settlementCost),
                settlementOk,
                () => _boardInput.SetBuildModeSettlement());

            ConfigureBuildButton(
                _buildCityButton,
                compact ? ColonistHudCopy.City : BuildCostFormatter.FormatButtonLabel(ColonistHudCopy.City, cityCost, cityOk),
                BuildCostFormatter.FormatTooltip(cityCost),
                cityOk,
                () => _boardInput.SetBuildModeCity());

            if (compact)
            {
                ColonistBuildButtonChrome.SetCost(_buildRoadButton, roadCost, roadOk);
                ColonistBuildButtonChrome.SetCost(_buildSettlementButton, settlementCost, settlementOk);
                ColonistBuildButtonChrome.SetCost(_buildCityButton, cityCost, cityOk);
            }
        }

        private static void ConfigureBuildButton(
            Button button,
            string label,
            string tooltip,
            bool interactable,
            UnityEngine.Events.UnityAction onClick)
        {
            if (button == null)
                return;

            var model = button.GetComponent<ColonistBuildButtonLabel>();
            if (model != null)
                model.SetTitle(label);
            else
            {
                foreach (var text in button.GetComponentsInChildren<Text>())
                {
                    if (text.gameObject.name == "Label" || text.gameObject.name == "Title")
                        text.text = label;
                }
            }

            if (button == _buildSettlementButton)
                ColonistActionBarLabels.SetSettlementTitle(label);

            var tip = button.GetComponent<RunHudCostTooltip>();
            if (tip != null)
                tip.SetTooltip(tooltip);

            button.interactable = interactable;
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
