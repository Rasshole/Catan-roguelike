using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Victory;
using UnityEngine;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Game
{
    public sealed class PlaceholderUI : MonoBehaviour
    {
        private GameController _controller;
        private MetaProgression _meta;
        private BoardInputController _boardInput;
        private Vector2 _scroll;
        private int _selectedCardIndex;
        private int _selectedResourceIndex;
        private int _selectedHexIndex;
        private int _selectedRoadIndex;
        private bool _showUnlockPanel;
        private bool _runRewardGranted;
        private int _lastRunStarsEarned;

        public void Initialize(GameController controller, MetaProgression meta, BoardInputController boardInput = null)
        {
            _controller = controller;
            _meta = meta;
            _boardInput = boardInput;
            _runRewardGranted = false;
        }

        public void Refresh(GameState state) { }

        private void OnGUI()
        {
            if (_controller == null) return;
            var state = _controller.State;

            GUILayout.BeginArea(new Rect(10, 10, 400, Screen.height - 20), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            if (state.Phase == GamePhase.RunSelectMap)
            {
                DrawMapSelect(state);
                DrawUnlockPanelIfOpen();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            if (state.Phase == GamePhase.RunSelectLeader)
            {
                DrawLeaderSelect();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            if (state.Phase == GamePhase.RunSelectDraft)
            {
                DrawDraftSelect();
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"<b>Day {state.Board.DayNumber}</b> — {MapPresets.GetDisplayName(state.MapSize)}");
            GUILayout.Label($"<b>{ActProgression.GetActLabel(ActProgression.GetAct(state.Board.DayNumber))}</b>");
            if (!string.IsNullOrEmpty(state.ActUnlockMessage))
                GUILayout.Label($"<color=cyan>{state.ActUnlockMessage}</color>");
            GUILayout.Label($"{state.Phase}");
            GUILayout.Label($"<b>Leader:</b> {LeaderLibrary.Get(state.Leader).Name}");
            if (state.DraftedUniques.Count > 0)
                GUILayout.Label($"<b>Uniques:</b> {string.Join(", ", state.DraftedUniques)}");
            GUILayout.Label(state.StatusMessage);

            if (state.ActiveEvent != EventId.None)
                GUILayout.Label($"<color=orange><b>Event:</b> {state.EventMessage}</color>");

            if (state.PendingCard.HasValue)
                GUILayout.Label($"<color=yellow>Pending: {CardLibrary.Get(state.PendingCard.Value).Name}</color>");

            if (PendingStatusDisplay.TryGetHarborCharterLine(state, out var harborLine))
                GUILayout.Label($"<color=cyan>{harborLine}</color>");

            if (PendingStatusDisplay.TryGetEmbargoLine(state, out var embargoLine))
                GUILayout.Label($"<color=red>{embargoLine}</color>");

            if (PendingStatusDisplay.TryGetLevelUpPreviewLine(state, _controller.RunSeed, out var levelUpPreviewLine))
                GUILayout.Label($"<color=lime>{levelUpPreviewLine}</color>");

            if (state.Winner.HasValue)
            {
                TryGrantRunReward(state);

                if (RunSummaryDisplay.TryGetSummaryLines(state, _controller.RunSeed, out var summaryLines))
                {
                    for (int i = 0; i < summaryLines.Count; i++)
                    {
                        if (i == 0)
                        {
                            var color = state.Winner == PlayerId.Human ? "green" : "red";
                            GUILayout.Label($"<color={color}>{summaryLines[i]}</color>");
                        }
                        else
                        {
                            GUILayout.Label(summaryLines[i]);
                        }
                    }
                }
                else
                {
                    GUILayout.Label(state.Winner == PlayerId.Human ? "<color=green>You Win!</color>" : "<color=red>AI Wins!</color>");
                }

                if (_meta != null)
                {
                    GUILayout.Label($"<b>Stars:</b> {_meta.Stars}");
                    if (_lastRunStarsEarned > 0)
                        GUILayout.Label($"<color=yellow>+{_lastRunStarsEarned} stars earned this run</color>");
                }

                DrawUnlockPanelIfOpen();
                if (GUILayout.Button(_showUnlockPanel ? "Hide Unlocks" : "Spend Stars / Unlocks"))
                    _showUnlockPanel = !_showUnlockPanel;

                if (GUILayout.Button("Restart (reload scene)"))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(8);
            GUILayout.Label($"<b>VP:</b> You {state.PlayerVictoryPoints} | AI {state.AiVictoryPoints}");
            var humanVp = VictoryCalculator.GetBreakdown(state, PlayerId.Human);
            var aiVp = VictoryCalculator.GetBreakdown(state, PlayerId.Ai);
            GUILayout.Label($"  You: {humanVp.FormatLine()}");
            GUILayout.Label($"  AI: {aiVp.FormatLine()}");
            GUILayout.Label($"<b>Level ups:</b> {state.LevelUpsTaken}/{RunProgression.MaxLevelUpsPerRun}");
            GUILayout.Label(FormatResources("You", state.PlayerInventory));
            GUILayout.Label(FormatResources("AI", state.AiInventory));

            DrawRolls(state);

            if (state.Phase == GamePhase.NightPlayCard)
                DrawNightCards(state);

            if (state.IsSetupPhase)
                DrawSetupActions();

            if (state.Phase == GamePhase.DayPlayerActions)
                DrawDayActions(state);

            if (state.Phase == GamePhase.LevelUpChoice)
                DrawLevelUp();

            if (!state.Winner.HasValue)
                DrawSaveLoadButtons();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawMapSelect(GameState state)
        {
            GUILayout.Label("<b>Vælg kort</b>");
            if (_meta != null)
                GUILayout.Label($"<b>Stars:</b> {_meta.Stars}");
            GUILayout.Label($"Nuværende forhåndsvisning: {MapPresets.GetDisplayName(state.MapSize)}");
            GUILayout.Space(8);

            if (_meta == null)
            {
                DrawMapButton(MapSize.Small);
                DrawMapButton(MapSize.Medium);
                DrawMapButton(MapSize.Large);
            }
            else
            {
                foreach (var size in _meta.GetAvailableMapSizes())
                    DrawMapButton(size);

                foreach (MapSize size in System.Enum.GetValues(typeof(MapSize)))
                {
                    if (_meta.IsMapAvailable(size))
                        continue;
                    var unlock = MetaCatalog.MapUnlockFor(size);
                    if (!unlock.HasValue)
                        continue;
                    var def = MetaCatalog.Get(unlock.Value);
                    GUILayout.Label($"<color=grey>{MapPresets.GetDisplayName(size)} — locked ({def.Cost}★)</color>");
                }
            }

            GUILayout.Space(8);
            if (_meta != null && GUILayout.Button(_showUnlockPanel ? "Hide Unlocks" : "Spend Stars / Unlocks"))
                _showUnlockPanel = !_showUnlockPanel;
        }

        private void DrawMapButton(MapSize size)
        {
            if (GUILayout.Button($"{MapPresets.GetDisplayName(size)}\n<i>{MapPresets.GetDescription(size)}</i>"))
                _controller.SelectMap(size);
        }

        private void DrawLeaderSelect()
        {
            GUILayout.Label($"<b>Leader</b> — {MapPresets.GetDisplayName(_controller.State.MapSize)}");
            GUILayout.Space(4);
            GUILayout.Label("<b>Choose your Leader</b>");

            if (_meta == null)
            {
                foreach (var kv in LeaderLibrary.All)
                {
                    var def = kv.Value;
                    if (GUILayout.Button($"{def.Name}\n<i>{def.PassiveDescription}</i>"))
                        _controller.SelectLeader(def.Id);
                }
                return;
            }

            foreach (var leader in _meta.GetAvailableLeaders())
            {
                var def = LeaderLibrary.Get(leader);
                if (GUILayout.Button($"{def.Name}\n<i>{def.PassiveDescription}</i>"))
                    _controller.SelectLeader(leader);
            }

            foreach (LeaderId leader in System.Enum.GetValues(typeof(LeaderId)))
            {
                if (_meta.IsLeaderAvailable(leader))
                    continue;
                var unlock = MetaCatalog.LeaderUnlockFor(leader);
                if (!unlock.HasValue)
                    continue;
                var def = MetaCatalog.Get(unlock.Value);
                var leaderDef = LeaderLibrary.Get(leader);
                GUILayout.Label($"<color=grey>{leaderDef.Name} — locked ({def.Cost}★)</color>");
            }
        }

        private void DrawDraftSelect()
        {
            int pickCount = _meta?.GetDraftPickCount() ?? RunProgression.DraftPickCount;
            GUILayout.Label($"<b>Draft {pickCount} unique buildings</b> ({_controller.State.DraftedUniques.Count} selected)");
            var pool = _meta?.GetDraftPool() ?? UniqueBuildingLibrary.All.Keys;
            foreach (var id in pool)
            {
                var def = UniqueBuildingLibrary.Get(id);
                bool picked = _controller.State.DraftedUniques.Contains(id);
                bool newPicked = GUILayout.Toggle(picked, $"{def.Name}: {def.Description}");
                if (newPicked != picked)
                    _controller.ToggleDraftUnique(id);
            }
            if (GUILayout.Button("Start Run"))
                _controller.ConfirmRunSetup();
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

        private void DrawUnlockPanelIfOpen()
        {
            if (!_showUnlockPanel || _meta == null)
                return;

            GUILayout.Space(8);
            GUILayout.Label("<b>Unlocks</b>");
            GUILayout.Label($"Stars: {_meta.Stars}");

            foreach (var id in _meta.GetPurchasableUnlocks())
            {
                var def = MetaCatalog.Get(id);
                bool canBuy = _meta.CanPurchase(id);
                GUI.enabled = canBuy;
                if (GUILayout.Button($"{def.Title} — {def.Cost}★\n<i>{def.Description}</i>"))
                {
                    if (_meta.TryPurchase(id))
                    {
                        var manager = FindFirstObjectByType<GameManager>();
                        if (manager != null)
                            manager.SaveMeta();
                    }
                }
                GUI.enabled = true;
            }

            if (!_meta.GetPurchasableUnlocks().Any())
                GUILayout.Label("<i>All unlocks purchased.</i>");
        }

        private void DrawLevelUp()
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Level Up!</b> Choose one perk:");
            foreach (var perk in _controller.State.PendingLevelUpChoices)
            {
                if (GUILayout.Button(LevelUpLibrary.GetDescription(perk)))
                    _controller.ChooseLevelUpPerk(perk);
            }
        }

        private void DrawRolls(GameState state)
        {
            GUILayout.Space(8);
            var rolls = state.IsNightPhase ? state.TomorrowRolls : state.TodayRolls;
            string label = state.IsNightPhase ? "Tomorrow's Rolls" : "Today's Rolls";
            GUILayout.Label($"<b>{label}</b>");
            if (rolls == null || rolls.Count == 0)
            {
                GUILayout.Label("  (not rolled yet)");
                return;
            }
            foreach (var kv in rolls.OrderBy(k => k.Key))
                GUILayout.Label($"  {kv.Key}: {kv.Value}");

            var dice = state.IsNightPhase ? state.TomorrowDiceRolls : state.TodayDiceRolls;
            if (dice != null && dice.Count > 0)
            {
                string diceLabel = state.IsNightPhase ? "Tomorrow's Dice" : "Today's Dice";
                GUILayout.Label($"<b>{diceLabel}</b> (2d6): {string.Join(", ", dice)}");
            }
        }

        private void DrawNightCards(GameState state)
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Hand</b> (play 1 or skip)");
            for (int i = 0; i < state.PlayerHand.Count; i++)
            {
                var card = state.PlayerHand[i];
                var def = CardLibrary.Get(card);
                if (GUILayout.Toggle(_selectedCardIndex == i, $"{def.Name}: {def.Description}"))
                    _selectedCardIndex = i;
            }

            if (state.PlayerHand.Count > 0)
            {
                _selectedCardIndex = Mathf.Clamp(_selectedCardIndex, 0, state.PlayerHand.Count - 1);
                var card = state.PlayerHand[_selectedCardIndex];
                if (card != CardId.Forecast)
                    DrawResourcePicker();

                if (card == CardId.BanditRaid)
                    DrawOpponentRoadPicker(state);

                if (GUILayout.Button("Play Selected Card"))
                {
                    var resource = (ResourceType)_selectedResourceIndex;
                    HexCoord? robber = GetSelectedRobberTile(state);
                    Edge? road = GetSelectedOpponentRoad(state);
                    _controller.PlayPlayerCard(card, resource, robber, road);
                }
            }

            if (GUILayout.Button("Skip Card → Start Day"))
                _controller.SkipNightCard();
        }

        private void DrawSetupActions()
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Setup</b> — klik grønt hjørne (settlement) eller gul kant (vej)");
        }

        private void DrawDayActions(GameState state)
        {
            GUILayout.Space(8);
            GUILayout.Label(
                $"<b>Shop</b> (3 trades; 3rd is 2:1 — {ShopDealDisplay.RiskyRobberConsequence})");
            foreach (var deal in state.ShopDeals)
            {
                var pricing = ShopDealPricing.Analyze(state, PlayerId.Human, deal);
                string label = ShopDealDisplay.FormatShopButtonLabel(state, PlayerId.Human, deal, pricing);
                if (GUILayout.Button(label))
                    _controller.BuyShopDeal(deal);
            }

            GUILayout.Space(8);
            GUILayout.Label("<b>Build</b> — klik hjørne eller kant");
            if (_boardInput != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Auto")) _boardInput.SetBuildModeAuto();
                if (GUILayout.Button("Settlement")) _boardInput.SetBuildModeSettlement();
                if (GUILayout.Button("Road")) _boardInput.SetBuildModeRoad();
                if (GUILayout.Button("City")) _boardInput.SetBuildModeCity();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            GUILayout.Label("<b>Robber</b>");
            DrawRobberPicker(state, steal: true);

            if (GUILayout.Button("End Day"))
                _controller.EndPlayerDay();
        }

        private void DrawResourcePicker()
        {
            GUILayout.Label("Target resource:");
            var names = System.Enum.GetNames(typeof(ResourceType));
            _selectedResourceIndex = GUILayout.SelectionGrid(_selectedResourceIndex, names, 3);
        }

        private void DrawRobberPicker(GameState state, bool steal)
        {
            var tiles = state.Board.Tiles.Keys.ToList();
            if (tiles.Count == 0) return;

            _selectedHexIndex = Mathf.Clamp(_selectedHexIndex, 0, tiles.Count - 1);
            var coord = tiles[_selectedHexIndex];
            var tile = state.Board.GetTile(coord);
            GUILayout.Label($"Hex: {coord} ({tile.Resource})");
            if (GUILayout.Button("◀ Hex")) _selectedHexIndex = Mathf.Max(0, _selectedHexIndex - 1);
            if (GUILayout.Button("Move Robber Here"))
                _controller.MoveRobber(coord, PlayerId.Human, steal);
            if (GUILayout.Button("Hex ▶")) _selectedHexIndex = Mathf.Min(tiles.Count - 1, _selectedHexIndex + 1);
        }

        private HexCoord? GetSelectedRobberTile(GameState state)
        {
            var tiles = state.Board.Tiles.Keys.ToList();
            if (tiles.Count == 0) return null;
            _selectedHexIndex = Mathf.Clamp(_selectedHexIndex, 0, tiles.Count - 1);
            return tiles[_selectedHexIndex];
        }

        private void DrawOpponentRoadPicker(GameState state)
        {
            var roads = OpponentRoadSelector.ListOpponentRoads(state.Board, PlayerId.Human);
            if (roads.Count == 0)
            {
                GUILayout.Label("No opponent roads to disable.");
                return;
            }

            _selectedRoadIndex = OpponentRoadSelector.ClampIndex(roads, _selectedRoadIndex);
            var edge = roads[_selectedRoadIndex];
            GUILayout.Label($"Target road: {edge}");
            if (GUILayout.Button("◀ Road"))
                _selectedRoadIndex = OpponentRoadSelector.ClampIndex(roads, _selectedRoadIndex - 1);
            if (GUILayout.Button("Road ▶"))
                _selectedRoadIndex = OpponentRoadSelector.ClampIndex(roads, _selectedRoadIndex + 1);
        }

        private Edge? GetSelectedOpponentRoad(GameState state)
        {
            var roads = OpponentRoadSelector.ListOpponentRoads(state.Board, PlayerId.Human);
            _selectedRoadIndex = OpponentRoadSelector.ClampIndex(roads, _selectedRoadIndex);
            return OpponentRoadSelector.SelectRoad(roads, _selectedRoadIndex);
        }

        private void DrawSaveLoadButtons()
        {
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                var manager = FindFirstObjectByType<GameManager>();
                if (manager != null)
                    manager.SaveRun();
            }

            if (GUILayout.Button("Load"))
            {
                var manager = FindFirstObjectByType<GameManager>();
                if (manager != null)
                    manager.TryLoadRun();
            }
            GUILayout.EndHorizontal();
        }

        private static string FormatResources(string label, ResourceBundle bundle)
        {
            var sb = new StringBuilder($"{label}: ");
            sb.Append($"W{bundle.Wood} B{bundle.Brick} Wh{bundle.Wheat} S{bundle.Sheep} St{bundle.Stone}");
            return sb.ToString();
        }
    }
}
