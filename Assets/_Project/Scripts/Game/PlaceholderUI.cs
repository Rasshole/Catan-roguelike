using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Shop;
using UnityEngine;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Game
{
    /// <summary>Simple IMGUI placeholder until proper UI is chosen.</summary>
    public sealed class PlaceholderUI : MonoBehaviour
    {
        private GameController _controller;
        private BoardInputController _boardInput;
        private Vector2 _scroll;
        private int _selectedCardIndex;
        private int _selectedResourceIndex;
        private int _selectedHexIndex;
        private int _selectedRoadIndex;

        public void Initialize(GameController controller, BoardInputController boardInput = null)
        {
            _controller = controller;
            _boardInput = boardInput;
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
            GUILayout.Label("<b>Setup</b> — klik på grøn markør (hjørne) eller gul markør (vej)");
        }

        private void DrawDayActions(GameState state)
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Shop</b> (3 daglige trades, 4:1 — bedre med port)");
            foreach (var deal in state.ShopDeals)
            {
                int effective = _controller.GetShopDealCost(deal);
                if (GUILayout.Button($"Buy: {deal.Format(effective)}"))
                    _controller.BuyShopDeal(deal);
            }

            GUILayout.Space(8);
            GUILayout.Label("<b>Build</b> — klik hjørne (settlement) eller kant (vej)");
            if (_boardInput != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Auto")) _boardInput.SetBuildModeAuto();
                if (GUILayout.Button("Settlement")) _boardInput.SetBuildModeSettlement();
                if (GUILayout.Button("Road")) _boardInput.SetBuildModeRoad();
                if (GUILayout.Button("City")) _boardInput.SetBuildModeCity();
                GUILayout.EndHorizontal();
            }

            var cities = _controller.GetUpgradeableCities(PlayerId.Human).ToList();
            if (cities.Count > 0)
                GUILayout.Label("Eller klik settlement i by-mode for at opgradere.");

            GUILayout.Space(8);
            GUILayout.Label("<b>Robber</b>");
            DrawRobberPicker(state, steal: false);

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
