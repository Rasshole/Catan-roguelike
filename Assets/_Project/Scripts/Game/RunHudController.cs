using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.UI;
using CatanRoguelike.Core.Victory;
using UnityEngine;
using UnityEngine.UI;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Game
{
    /// <summary>Colonist.io-inspired uGUI HUD for in-run play (side resources, bottom card hand).</summary>
    public sealed partial class RunHudController : MonoBehaviour
    {
        public void BindFeel(GameplayFeelController feel) => _feel = feel;
        private GameController _controller;
        private MetaProgression _meta;
        private BoardInputController _boardInput;

        private Canvas _canvas;
        private Text _dayPhaseText;
        private Text _statusText;
        private Text _vpText;
        private Text _aiText;
        private readonly List<Text> _resourceAmountTexts = new();
        private readonly List<Image> _resourceSwatches = new();
        private RectTransform _shopListRoot;
        private RectTransform _handPanel;
        private RectTransform _handRow;
        private RectTransform _actionBar;
        private readonly List<GameObject> _handCardObjects = new();
        private readonly List<Button> _actionButtons = new();

        private int _selectedCardIndex;
        private int _selectedResourceIndex;
        private int _selectedHexIndex;
        private int _selectedRoadIndex;
        private bool _runRewardGranted;
        private int _lastRunStarsEarned;

        public void Initialize(GameController controller, MetaProgression meta, BoardInputController boardInput)
        {
            _controller = controller;
            _meta = meta;
            _boardInput = boardInput;
            _runRewardGranted = false;
            EnsureCanvasBuilt();
            Refresh(controller.State);
        }

        public void Refresh(GameState state)
        {
            if (state == null)
                return;

            EnsureCanvasBuilt();

            bool inRun = !PlaceholderHudLayout.IsPreGamePhase(state.Phase);
            _canvas.gameObject.SetActive(inRun);

            if (!inRun)
                return;

            if (ColonistPresentation.IsActive)
                PlaceholderHudLayout.SetActivePanelWidth(0f);
            else
                PlaceholderHudLayout.SetActivePanelWidth(RunHudLayout.SidePanelWidth);

            if (state.Winner.HasValue)
                TryGrantRunReward(state);

            if (!ColonistPresentation.IsActive && _dayPhaseText != null)
            {
                _dayPhaseText.text =
                    $"Day {state.Board.DayNumber} · {MapPresets.GetDisplayName(state.MapSize)}\n{state.Phase}";
            }

            var status = new StringBuilder();
            if (!string.IsNullOrEmpty(state.StatusMessage))
                status.AppendLine(ColonistPresentation.IsActive
                    ? ColonistHudCopy.TranslateStatus(state.StatusMessage)
                    : state.StatusMessage);
            if (state.ActiveEvent != Core.Events.EventId.None)
                status.AppendLine(state.EventMessage);
            if (state.PendingCard.HasValue)
                status.AppendLine($"Pending: {CardLibrary.Get(state.PendingCard.Value).Name}");
            if (state.IsSetupPhase)
                status.AppendLine("Setup: click a green corner (settlement) or yellow edge (road).");
            if (state.Winner.HasValue)
            {
                if (RunSummaryDisplay.TryGetSummaryLines(state, _controller.RunSeed, out var summaryLines)
                    && summaryLines.Count > 0)
                {
                    status.AppendLine(summaryLines[0]);
                }
                else
                {
                    status.AppendLine(state.Winner == PlayerId.Human ? "You win!" : "AI wins!");
                }

                if (_lastRunStarsEarned > 0)
                    status.AppendLine($"+{_lastRunStarsEarned}★");
            }
            if (ColonistPresentation.IsActive)
            {
                RefreshColonistPresentation(state);
                RefreshColonistResources(
                    _resourceAnimLock ? _resourceAnimDisplay : state.PlayerInventory);
            }
            else
            {
                if (_statusText != null)
                    _statusText.text = status.ToString().Trim();

                if (_vpText != null)
                {
                    _vpText.text =
                        $"VP: You {state.PlayerVictoryPoints}  ·  AI {state.AiVictoryPoints}\n" +
                        $"  {VictoryCalculator.GetBreakdown(state, PlayerId.Human).FormatLine()}\n" +
                        $"  {VictoryCalculator.GetBreakdown(state, PlayerId.Ai).FormatLine()}";
                }

                RefreshResourcesPartial(state.PlayerInventory);
                if (_aiText != null)
                    _aiText.text = FormatAiSummary(state);

                RebuildShopList(state);
                RebuildHand(state);
            }

            RebuildActionBar(state);
            RefreshActionBarChrome(state);
            RefreshBuildButtons(state);
            if (!ColonistPresentation.IsActive)
                RefreshColonistChrome(state);
            RefreshToastWidget(state);
            RefreshIncomingTradePanel(state);
        }

        private void EnsureCanvasBuilt()
        {
            if (_canvas != null)
                return;

            if (ColonistPresentation.IsActive)
            {
                EnsureColonistCanvasBuilt();
                return;
            }

            _canvas = RunHudUiFactory.CreateCanvas(transform);

            BuildSidePanel(_canvas.transform);
            BuildHandRow(_canvas.transform);
            BuildActionBarChrome(_canvas.transform);
            BuildColonistChrome(_canvas.transform);
            EnsureTradeDialogBuilt();
            BuildToastWidget(_canvas.transform);
            BuildIncomingTradePanel(_canvas.transform);
            EnsureFlyLayer();
            RunHudCostTooltip.EnsureShared(_canvas.transform);
            SetLayerRecursively(_canvas.gameObject, HudCaptureUiLayer);
        }

        private void BuildSidePanel(Transform root)
        {
            float margin = RunHudLayout.Margin;
            var panel = RunHudUiFactory.CreatePanel(
                root,
                "SidePanel",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(margin, margin),
                new Vector2(margin + RunHudLayout.SidePanelWidth, -margin));

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 6f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            _dayPhaseText = AddPanelText(panel, 20, FontStyle.Bold);
            _statusText = AddPanelText(panel, 15, FontStyle.Normal);
            _vpText = AddPanelText(panel, 15, FontStyle.Normal);

            AddPanelLabel(panel, "Ressourcer", 17);
            BuildResourceRows(panel);
            BuildTradePanel(panel);

            _aiText = AddPanelText(panel, 15, FontStyle.Normal);

            AddPanelLabel(panel, "Butik", 17);
            var shopScrollGo = new GameObject("ShopScroll");
            shopScrollGo.transform.SetParent(panel, false);
            var shopScrollRect = shopScrollGo.AddComponent<RectTransform>();
            RunHudUiFactory.AddLayoutElement(shopScrollGo, 160f, 1f);

            var scroll = shopScrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(shopScrollGo.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

            _shopListRoot = new GameObject("ShopContent").AddComponent<RectTransform>();
            _shopListRoot.SetParent(viewport.transform, false);
            _shopListRoot.anchorMin = new Vector2(0f, 1f);
            _shopListRoot.anchorMax = new Vector2(1f, 1f);
            _shopListRoot.pivot = new Vector2(0.5f, 1f);
            _shopListRoot.offsetMin = Vector2.zero;
            _shopListRoot.offsetMax = Vector2.zero;

            var shopLayout = _shopListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            shopLayout.spacing = 4f;
            shopLayout.childForceExpandWidth = true;
            shopLayout.childControlWidth = true;
            shopLayout.childControlHeight = true;

            var fitter = _shopListRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = _shopListRoot;
            scroll.viewport = viewportRect;
        }

        private void BuildResourceRows(RectTransform panel)
        {
            var gridGo = new GameObject("ResourceChipGrid");
            gridGo.layer = panel.gameObject.layer;
            gridGo.transform.SetParent(panel, false);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(96f, 30f);
            grid.spacing = new Vector2(6f, 6f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            RunHudUiFactory.AddLayoutElement(gridGo, 108f);

            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                var chip = new GameObject($"Chip_{resource}");
                chip.layer = gridGo.layer;
                chip.transform.SetParent(gridGo.transform, false);
                var chipBg = chip.AddComponent<Image>();
                chipBg.color = new Color(0.11f, 0.14f, 0.19f, 0.95f);

                var rowLayout = chip.AddComponent<HorizontalLayoutGroup>();
                rowLayout.padding = new RectOffset(6, 6, 4, 4);
                rowLayout.spacing = 6f;
                rowLayout.childAlignment = TextAnchor.MiddleLeft;

                var swatchGo = new GameObject("Swatch");
                swatchGo.layer = chip.layer;
                swatchGo.transform.SetParent(chip.transform, false);
                var swatchRect = swatchGo.AddComponent<RectTransform>();
                swatchRect.sizeDelta = new Vector2(18f, 18f);
                var swatch = swatchGo.AddComponent<Image>();
                swatch.color = BoardResourceColors.GetHudSwatch(resource);
                RunHudUiFactory.AddLayoutElement(swatchGo, 18f).preferredWidth = 18f;

                var amount = RunHudUiFactory.CreateText(
                    chip.transform,
                    "Amount",
                    HudResourceFormatting.FormatQuantity(0, resource),
                    15,
                    TextAnchor.MiddleLeft,
                    RunHudUiFactory.TextPrimary,
                    FontStyle.Bold);
                _resourceAmountTexts.Add(amount);
                _resourceSwatches.Add(swatch);
            }
        }

        private void BuildHandRow(Transform root)
        {
            float margin = RunHudLayout.Margin;
            float bottom = RunHudLayout.GetHandRowBottomInset();
            _handPanel = RunHudUiFactory.CreatePanel(
                root,
                "CardHand",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(margin + RunHudLayout.SidePanelWidth + margin, bottom),
                new Vector2(-(margin + RunHudLayout.RightChromePanelWidth + margin), bottom + RunHudLayout.BottomHandHeight));
            _handPanel.GetComponent<Image>().color = RunHudUiFactory.HandBackdrop;

            var scrollGo = new GameObject("HandScroll");
            scrollGo.transform.SetParent(_handPanel, false);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(8f, 8f);
            scrollRect.offsetMax = new Vector2(-8f, -8f);

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.05f);

            _handRow = new GameObject("HandContent").AddComponent<RectTransform>();
            _handRow.SetParent(viewport.transform, false);
            _handRow.anchorMin = new Vector2(0f, 0.5f);
            _handRow.anchorMax = new Vector2(0f, 0.5f);
            _handRow.pivot = new Vector2(0f, 0.5f);
            _handRow.sizeDelta = new Vector2(400f, RunHudLayout.CardHeight);

            var handLayout = _handRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = RunHudLayout.CardSpacing;
            handLayout.childAlignment = TextAnchor.MiddleLeft;
            handLayout.childForceExpandHeight = true;
            handLayout.childControlHeight = true;
            handLayout.childControlWidth = false;

            var fitter = _handRow.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = _handRow;
            scroll.viewport = viewportRect;
        }

        private static Text AddPanelText(RectTransform panel, int size, FontStyle style)
        {
            var go = new GameObject("TextBlock");
            go.transform.SetParent(panel, false);
            RunHudUiFactory.AddLayoutElement(go, size + 10f);
            return RunHudUiFactory.CreateText(
                go.transform,
                "Text",
                string.Empty,
                size,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextPrimary,
                style);
        }

        private static void AddPanelLabel(RectTransform panel, string label, int size)
        {
            var text = AddPanelText(panel, size, FontStyle.Bold);
            text.text = label;
            text.color = RunHudUiFactory.TextMuted;
        }

        private void RefreshResources(ResourceBundle bundle)
        {
            int i = 0;
            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                if (i >= _resourceAmountTexts.Count)
                    break;
                _resourceAmountTexts[i].text = HudResourceFormatting.FormatInventoryLine(bundle, resource);
                i++;
            }
        }

        private static string FormatAiSummary(GameState state)
        {
            var sb = new StringBuilder("AI\n");
            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                int amount = state.AiInventory[resource];
                if (amount > 0)
                    sb.AppendLine(HudResourceFormatting.FormatQuantity(amount, resource));
            }
            return sb.ToString().TrimEnd();
        }

        private static string BuildRollsBlock(GameState state)
        {
            var rolls = state.IsNightPhase ? state.TomorrowRolls : state.TodayRolls;
            var sb = new StringBuilder();
            if (rolls == null || rolls.Count == 0)
            {
                sb.Append("(ingen terninger endnu)");
            }
            else
            {
                foreach (var kv in rolls.OrderBy(k => k.Key))
                    sb.AppendLine($"{kv.Key}: {kv.Value}");
            }

            var dice = state.IsNightPhase ? state.TomorrowDiceRolls : state.TodayDiceRolls;
            if (dice != null && dice.Count > 0)
                sb.AppendLine($"2d6: {string.Join(", ", dice)}");
            return sb.ToString().TrimEnd();
        }

        private void RebuildShopList(GameState state)
        {
            if (_shopListRoot == null)
                return;

            for (int i = _shopListRoot.childCount - 1; i >= 0; i--)
                Destroy(_shopListRoot.GetChild(i).gameObject);

            if (state.Phase != GamePhase.DayPlayerActions || state.Winner.HasValue)
                return;

            foreach (var deal in state.ShopDeals)
            {
                var pricing = ShopDealPricing.Analyze(state, PlayerId.Human, deal);
                string label = ShopDealDisplay.FormatShopButtonLabel(state, PlayerId.Human, deal, pricing);
                var button = RunHudUiFactory.CreateButton(_shopListRoot, label, RunHudUiFactory.ButtonSecondary);
                RunHudUiFactory.AddLayoutElement(button.gameObject, 36f);
                var captured = deal;
                button.onClick.AddListener(() => _controller.BuyShopDeal(captured));
            }
        }

        private void RebuildHand(GameState state)
        {
            foreach (var go in _handCardObjects)
                Destroy(go);
            _handCardObjects.Clear();

            bool showHand = !state.Winner.HasValue
                && (state.Phase == GamePhase.NightPlayCard || state.PlayerHand.Count > 0);
            if (_handPanel != null)
                _handPanel.gameObject.SetActive(showHand);

            if (!showHand)
                return;

            for (int i = 0; i < state.PlayerHand.Count; i++)
            {
                int index = i;
                var cardId = state.PlayerHand[i];
                var def = CardLibrary.Get(cardId);
                var rgb = CardVisualStyle.GetCategoryColor(def.Category);
                var frameColor = new Color(rgb.R, rgb.G, rgb.B, 1f);

                var cardGo = new GameObject($"Card_{cardId}");
                cardGo.transform.SetParent(_handRow, false);
                var rect = cardGo.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(RunHudLayout.CardWidth, RunHudLayout.CardHeight);

                var bg = cardGo.AddComponent<Image>();
                bg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

                var border = new GameObject("Border");
                border.transform.SetParent(cardGo.transform, false);
                var borderRect = border.AddComponent<RectTransform>();
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = Vector2.zero;
                borderRect.offsetMax = Vector2.zero;
                var borderImg = border.AddComponent<Image>();
                borderImg.color = frameColor;

                var inner = new GameObject("Inner");
                inner.transform.SetParent(cardGo.transform, false);
                var innerRect = inner.AddComponent<RectTransform>();
                innerRect.anchorMin = Vector2.zero;
                innerRect.anchorMax = Vector2.one;
                innerRect.offsetMin = new Vector2(4f, 4f);
                innerRect.offsetMax = new Vector2(-4f, -4f);
                inner.AddComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 0.98f);

                RunHudUiFactory.CreateText(
                    inner.transform,
                    "Title",
                    def.Name,
                    15,
                    TextAnchor.UpperLeft,
                    RunHudUiFactory.TextPrimary,
                    FontStyle.Bold);

                RunHudUiFactory.CreateText(
                    inner.transform,
                    "Category",
                    CardVisualStyle.GetCategoryLabel(def.Category),
                    12,
                    TextAnchor.UpperRight,
                    RunHudUiFactory.TextMuted);

                var body = RunHudUiFactory.CreateText(
                    inner.transform,
                    "Body",
                    def.Description,
                    13,
                    TextAnchor.UpperLeft,
                    RunHudUiFactory.TextMuted);
                var bodyRect = body.rectTransform;
                bodyRect.offsetMin = new Vector2(8f, 8f);
                bodyRect.offsetMax = new Vector2(-8f, -36f);

                var button = cardGo.AddComponent<Button>();
                button.targetGraphic = bg;
                bool selected = _selectedCardIndex == index;
                bg.color = selected
                    ? Color.Lerp(bg.color, frameColor, 0.35f)
                    : new Color(0.12f, 0.14f, 0.18f, 0.95f);
                button.onClick.AddListener(() =>
                {
                    _selectedCardIndex = index;
                    Refresh(_controller.State);
                });

                _handCardObjects.Add(cardGo);
            }

            if (state.PlayerHand.Count > 0)
                _selectedCardIndex = Mathf.Clamp(_selectedCardIndex, 0, state.PlayerHand.Count - 1);

            SetLayerRecursively(_canvas.gameObject, HudCaptureUiLayer);
        }

        private void RebuildActionBar(GameState state)
        {
            foreach (var button in _actionButtons)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }
            if (_actionBarBuildRoot != null)
            {
                for (int i = _actionBarBuildRoot.childCount - 1; i >= 0; i--)
                    Destroy(_actionBarBuildRoot.GetChild(i).gameObject);
            }

            _actionButtons.Clear();
            _buildRoadButton = null;
            _buildSettlementButton = null;
            _buildCityButton = null;

            if (state.Winner.HasValue)
            {
                var restart = RunHudUiFactory.CreateButton(_actionBarBuildRoot, ColonistHudCopy.Restart, RunHudUiFactory.ButtonFill);
                restart.onClick.AddListener(() =>
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
                _actionButtons.Add(restart);
                return;
            }

            if (state.Phase == GamePhase.LevelUpChoice)
            {
                foreach (var perk in state.PendingLevelUpChoices)
                {
                    string label = LevelUpLibrary.GetDescription(perk);
                    var button = RunHudUiFactory.CreateButton(_actionBarBuildRoot, label, RunHudUiFactory.ButtonFill);
                    var captured = perk;
                    button.onClick.AddListener(() => _controller.ChooseLevelUpPerk(captured));
                    _actionButtons.Add(button);
                }
                return;
            }

            if (state.Phase == GamePhase.NightPlayCard)
            {
                if (!ColonistPresentation.IsActive && state.PlayerHand.Count > 0)
                {
                    var play = RunHudUiFactory.CreateButton(_actionBarBuildRoot, "Play card", RunHudUiFactory.ButtonFill);
                    play.onClick.AddListener(PlaySelectedCard);
                    _actionButtons.Add(play);
                }

                var skip = RunHudUiFactory.CreateButton(
                    _actionBarBuildRoot,
                    ColonistPresentation.IsActive ? "Continue" : "Skip to day",
                    RunHudUiFactory.ButtonSecondary);
                skip.onClick.AddListener(() => _controller.SkipNightCard());
                _actionButtons.Add(skip);
                return;
            }

            if (state.Phase == GamePhase.DayPlayerActions)
            {
                EnsureBuildButtons();
                RefreshBuildButtons(state);

                if (_boardInput != null && _boardInput.IsPlacementModeActive)
                {
                    var cancel = RunHudUiFactory.CreateButton(_actionBarBuildRoot, ColonistHudCopy.CancelPlacement, RunHudUiFactory.ButtonSecondary);
                    cancel.onClick.AddListener(() => _boardInput.CancelBuildMode());
                    _actionButtons.Add(cancel);
                }
            }
        }

        private void AddBuildModeButton(string label, UnityEngine.Events.UnityAction action)
        {
            var button = RunHudUiFactory.CreateButton(_actionBarBuildRoot, label, RunHudUiFactory.ButtonSecondary);
            button.onClick.AddListener(action);
            _actionButtons.Add(button);
        }

        private void AddResourcePickerButtons()
        {
            foreach (ResourceType resource in System.Enum.GetValues(typeof(ResourceType)))
            {
                int index = (int)resource;
                string label = ResourceDisplayNames.GetDanish(resource);
                var button = RunHudUiFactory.CreateButton(_actionBarBuildRoot, label, RunHudUiFactory.ButtonSecondary);
                bool selected = _selectedResourceIndex == index;
                if (selected)
                    button.GetComponent<Image>().color = BoardResourceColors.GetHudSwatch(resource);
                button.onClick.AddListener(() =>
                {
                    _selectedResourceIndex = index;
                    Refresh(_controller.State);
                });
                _actionButtons.Add(button);
            }
        }

        private void AddRobberButtons(GameState state, bool steal)
        {
            var tiles = state.Board.Tiles.Keys.ToList();
            if (tiles.Count == 0)
                return;

            _selectedHexIndex = Mathf.Clamp(_selectedHexIndex, 0, tiles.Count - 1);
            var coord = tiles[_selectedHexIndex];

            var prev = RunHudUiFactory.CreateButton(_actionBarBuildRoot, "◀ Hex", RunHudUiFactory.ButtonSecondary);
            prev.onClick.AddListener(() =>
            {
                _selectedHexIndex = Mathf.Max(0, _selectedHexIndex - 1);
                Refresh(_controller.State);
            });
            _actionButtons.Add(prev);

            var move = RunHudUiFactory.CreateButton(_actionBarBuildRoot, $"Røver @ {coord}", RunHudUiFactory.ButtonSecondary);
            move.onClick.AddListener(() => _controller.MoveRobber(coord, PlayerId.Human, steal));
            _actionButtons.Add(move);

            var next = RunHudUiFactory.CreateButton(_actionBarBuildRoot, "Hex ▶", RunHudUiFactory.ButtonSecondary);
            next.onClick.AddListener(() =>
            {
                _selectedHexIndex = Mathf.Min(tiles.Count - 1, _selectedHexIndex + 1);
                Refresh(_controller.State);
            });
            _actionButtons.Add(next);
        }

        private void AddRoadPickerButtons(GameState state)
        {
            var card = state.PlayerHand.Count > 0
                ? state.PlayerHand[Mathf.Clamp(_selectedCardIndex, 0, state.PlayerHand.Count - 1)]
                : CardId.BanditRaid;
            if (card != CardId.BanditRaid)
                return;

            var roads = OpponentRoadSelector.ListOpponentRoads(state.Board, PlayerId.Human);
            if (roads.Count == 0)
                return;

            _selectedRoadIndex = OpponentRoadSelector.ClampIndex(roads, _selectedRoadIndex);
            var edge = roads[_selectedRoadIndex];

            var prev = RunHudUiFactory.CreateButton(_actionBarBuildRoot, "◀ Vej", RunHudUiFactory.ButtonSecondary);
            prev.onClick.AddListener(() =>
            {
                _selectedRoadIndex = OpponentRoadSelector.ClampIndex(roads, _selectedRoadIndex - 1);
                Refresh(_controller.State);
            });
            _actionButtons.Add(prev);

            var next = RunHudUiFactory.CreateButton(_actionBarBuildRoot, "Vej ▶", RunHudUiFactory.ButtonSecondary);
            next.onClick.AddListener(() =>
            {
                _selectedRoadIndex = OpponentRoadSelector.ClampIndex(roads, _selectedRoadIndex + 1);
                Refresh(_controller.State);
            });
            _actionButtons.Add(next);
        }

        private void PlaySelectedCard()
        {
            var state = _controller.State;
            if (state.PlayerHand.Count == 0)
                return;

            _selectedCardIndex = Mathf.Clamp(_selectedCardIndex, 0, state.PlayerHand.Count - 1);
            var card = state.PlayerHand[_selectedCardIndex];
            var resource = (ResourceType)_selectedResourceIndex;
            HexCoord? robber = GetSelectedRobberTile(state);
            Edge? road = GetSelectedOpponentRoad(state);
            _controller.PlayPlayerCard(card, resource, robber, road);
        }

        private HexCoord? GetSelectedRobberTile(GameState state)
        {
            var tiles = state.Board.Tiles.Keys.ToList();
            if (tiles.Count == 0)
                return null;
            _selectedHexIndex = Mathf.Clamp(_selectedHexIndex, 0, tiles.Count - 1);
            return tiles[_selectedHexIndex];
        }

        private Edge? GetSelectedOpponentRoad(GameState state)
        {
            var roads = OpponentRoadSelector.ListOpponentRoads(state.Board, PlayerId.Human);
            _selectedRoadIndex = OpponentRoadSelector.ClampIndex(roads, _selectedRoadIndex);
            return OpponentRoadSelector.SelectRoad(roads, _selectedRoadIndex);
        }

        private void TryGrantRunReward(GameState state)
        {
            if (_meta == null || _runRewardGranted || !state.Winner.HasValue)
                return;

            if (_meta.TryAwardRun(
                    _controller.RunSeed,
                    state.PlayerVictoryPoints,
                    state.Board.DayNumber,
                    state.Winner.Value,
                    out int earned))
            {
                _lastRunStarsEarned = earned;
                var manager = FindFirstObjectByType<GameManager>();
                if (manager != null)
                    manager.SaveMeta();
            }

            _runRewardGranted = true;
        }
    }
}
