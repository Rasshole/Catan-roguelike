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
        private Button _endTurnPinned;
        private Button _rollPinned;
        private Text _diceOverlay;
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

            RefreshTradePanel(state);
        }

        private Text _buildRoadChip;
        private Text _buildSettlementChip;
        private Text _buildCityChip;

        private void EnsureBuildButtons()
        {
            if (_buildRoadButton != null)
                return;

            RunHudCostTooltip.EnsureShared(_canvas.transform);
            _buildRoadButton = CreateBuildButton("Vej", out _buildRoadChip);
            _buildSettlementButton = CreateBuildButton("By", out _buildSettlementChip);
            _buildCityButton = CreateBuildButton("Byudvid", out _buildCityChip);
            _actionButtons.Add(_buildRoadButton);
            _actionButtons.Add(_buildSettlementButton);
            _actionButtons.Add(_buildCityButton);
        }

        private Button CreateBuildButton(string title, out Text chipText)
        {
            var button = RunHudUiFactory.CreateButton(_actionBar, title, RunHudUiFactory.ButtonSecondary);
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

            ConfigureBuildButton(
                _buildRoadButton,
                BuildCostFormatter.FormatButtonLabel("Vej", roadCost, roadOk),
                BuildCostFormatter.FormatTooltip(roadCost),
                roadOk,
                () => _boardInput.SetBuildModeRoad());

            ConfigureBuildButton(
                _buildSettlementButton,
                BuildCostFormatter.FormatButtonLabel("By", settlementCost, settlementOk),
                BuildCostFormatter.FormatTooltip(settlementCost),
                settlementOk,
                () => _boardInput.SetBuildModeSettlement());

            ConfigureBuildButton(
                _buildCityButton,
                BuildCostFormatter.FormatButtonLabel("Byudvid", cityCost, cityOk),
                BuildCostFormatter.FormatTooltip(cityCost),
                cityOk,
                () => _boardInput.SetBuildModeCity());
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

            foreach (var text in button.GetComponentsInChildren<Text>())
            {
                if (text.gameObject.name == "Label")
                    text.text = label;
            }

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
