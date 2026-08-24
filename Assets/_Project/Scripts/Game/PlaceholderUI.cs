using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using UnityEngine;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Game
{
    /// <summary>Simple IMGUI placeholder until proper UI is chosen.</summary>
    public sealed class PlaceholderUI : MonoBehaviour
    {
        private GameController _controller;
        private Vector2 _scroll;
        private int _selectedCardIndex;
        private int _selectedResourceIndex;
        private int _selectedHexIndex;
        private int _selectedRoadIndex;
        private int _settlementSpotIndex;
        private int _roadSpotIndex;

        public void Initialize(GameController controller)
        {
            _controller = controller;
        }

        public void Refresh(GameState state) { }

        private void OnGUI()
        {
            if (_controller == null) return;
            var state = _controller.State;

            GUILayout.BeginArea(new Rect(10, 10, 380, Screen.height - 20), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label($"<b>Day {state.Board.DayNumber}</b> — {state.Phase}");
            GUILayout.Label(state.StatusMessage);

            if (state.PendingCard.HasValue)
                GUILayout.Label($"<color=yellow>Pending: {CardLibrary.Get(state.PendingCard.Value).Name}</color>");

            if (state.Winner.HasValue)
            {
                GUILayout.Label(state.Winner == PlayerId.Human ? "<color=green>You Win!</color>" : "<color=red>AI Wins!</color>");
                if (GUILayout.Button("Restart (reload scene)"))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(8);
            GUILayout.Label($"<b>VP:</b> You {state.PlayerVictoryPoints} | AI {state.AiVictoryPoints}");
            GUILayout.Label(FormatResources("You", state.PlayerInventory));
            GUILayout.Label(FormatResources("AI", state.AiInventory));

            DrawRolls(state);

            if (state.Phase == GamePhase.NightPlayCard)
                DrawNightCards(state);

            if (state.IsSetupPhase)
                DrawSetupActions();

            if (state.Phase == GamePhase.DayPlayerActions)
                DrawDayActions(state);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
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

            DrawResourcePicker();

            if (state.PlayerHand.Count > 0 && GUILayout.Button("Play Selected Card"))
            {
                var card = state.PlayerHand[_selectedCardIndex];
                var resource = (ResourceType)_selectedResourceIndex;
                HexCoord? robber = GetSelectedRobberTile(state);
                Edge? road = GetSelectedOpponentRoad(state);
                _controller.PlayPlayerCard(card, resource, robber, road);
            }

            if (GUILayout.Button("Skip Card → Start Day"))
                _controller.SkipNightCard();
        }

        private void DrawSetupActions()
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Setup</b>");
            DrawPlacementButtons();
        }

        private void DrawDayActions(GameState state)
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Shop</b>");
            foreach (var deal in state.ShopDeals)
            {
                if (GUILayout.Button($"Buy: {deal}"))
                    _controller.BuyShopDeal(deal);
            }

            GUILayout.Space(8);
            GUILayout.Label("<b>Build</b>");
            DrawPlacementButtons();

            var cities = _controller.GetUpgradeableCities(PlayerId.Human).ToList();
            if (cities.Count > 0 && GUILayout.Button("Upgrade City (first valid)"))
                _controller.UpgradeCity(cities[0], PlayerId.Human);

            GUILayout.Space(8);
            GUILayout.Label("<b>Robber</b>");
            DrawRobberPicker(state, steal: false);

            if (GUILayout.Button("End Day"))
                _controller.EndPlayerDay();
        }

        private void DrawPlacementButtons()
        {
            var settlements = _controller.GetValidSettlements(PlayerId.Human).ToList();
            var roads = _controller.GetValidRoads(PlayerId.Human).ToList();

            if (settlements.Count > 0)
            {
                _settlementSpotIndex = Mathf.Clamp(_settlementSpotIndex, 0, settlements.Count - 1);
                GUILayout.Label($"Settlement spot {_settlementSpotIndex + 1}/{settlements.Count}");
                if (GUILayout.Button("◀")) _settlementSpotIndex = Mathf.Max(0, _settlementSpotIndex - 1);
                if (GUILayout.Button("Place Settlement ▶"))
                    _controller.PlaceSettlement(settlements[_settlementSpotIndex], PlayerId.Human);
                if (GUILayout.Button("▶")) _settlementSpotIndex = Mathf.Min(settlements.Count - 1, _settlementSpotIndex + 1);
            }
            else
            {
                GUILayout.Label("No valid settlement spots.");
            }

            if (roads.Count > 0)
            {
                _roadSpotIndex = Mathf.Clamp(_roadSpotIndex, 0, roads.Count - 1);
                GUILayout.Label($"Road spot {_roadSpotIndex + 1}/{roads.Count}");
                if (GUILayout.Button("◀ Road")) _roadSpotIndex = Mathf.Max(0, _roadSpotIndex - 1);
                if (GUILayout.Button("Place Road ▶"))
                    _controller.PlaceRoad(roads[_roadSpotIndex], PlayerId.Human);
                if (GUILayout.Button("Road ▶")) _roadSpotIndex = Mathf.Min(roads.Count - 1, _roadSpotIndex + 1);
            }
            else
            {
                GUILayout.Label("No valid road spots.");
            }
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

        private Edge? GetSelectedOpponentRoad(GameState state)
        {
            var roads = state.Board.Roads
                .Where(kv => kv.Value == PlayerId.Ai)
                .Select(kv => kv.Key)
                .ToList();
            if (roads.Count == 0) return null;
            _selectedRoadIndex = Mathf.Clamp(_selectedRoadIndex, 0, roads.Count - 1);
            return roads[_selectedRoadIndex];
        }

        private static string FormatResources(string label, ResourceBundle bundle)
        {
            var sb = new StringBuilder($"{label}: ");
            sb.Append($"W{bundle.Wood} B{bundle.Brick} Wh{bundle.Wheat} S{bundle.Sheep} St{bundle.Stone}");
            return sb.ToString();
        }
    }
}
