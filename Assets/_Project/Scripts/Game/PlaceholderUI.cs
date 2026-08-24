using System.Linq;
using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Turn;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Simple IMGUI placeholder until proper UI is chosen.</summary>
    public sealed class PlaceholderUI : MonoBehaviour
    {
        private GameController _controller;
        private Vector2 _scroll;
        private int _selectedCardIndex;

        public void Initialize(GameController controller)
        {
            _controller = controller;
        }

        public void Refresh(GameState state) { }

        private void OnGUI()
        {
            if (_controller == null) return;
            var state = _controller.State;

            GUILayout.BeginArea(new Rect(10, 10, 360, Screen.height - 20), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label($"<b>Day {state.Board.DayNumber}</b> — {state.Phase}");
            GUILayout.Label(state.StatusMessage);

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

            GUILayout.Space(8);
            GUILayout.Label("<b>Tomorrow's Rolls</b>");
            foreach (var kv in state.TomorrowRolls.OrderBy(k => k.Key))
                GUILayout.Label($"  {kv.Key}: {kv.Value}");

            if (state.Phase == GamePhase.NightPlayCard)
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

                if (state.PlayerHand.Count > 0 && GUILayout.Button("Play Selected Card"))
                {
                    var card = state.PlayerHand[_selectedCardIndex];
                    var resource = PickDefaultResource(state);
                    _controller.PlayPlayerCard(card, resource);
                }

                if (GUILayout.Button("Skip Card → Start Day"))
                    _controller.SkipNightCard();
            }

            if (state.Phase == GamePhase.DayPlayerActions)
            {
                GUILayout.Space(8);
                GUILayout.Label("<b>Shop</b>");
                for (int i = 0; i < state.ShopDeals.Count; i++)
                {
                    var deal = state.ShopDeals[i];
                    if (GUILayout.Button($"Buy: {deal}"))
                        _controller.BuyShopDeal(deal);
                }

                GUILayout.Space(8);
                GUILayout.Label("<b>Setup / Build</b> (click hex in scene — use first valid spot)");
                if (GUILayout.Button("Place Settlement (auto valid spot)"))
                    TryAutoPlaceSettlement();

                if (GUILayout.Button("Place Road (auto valid spot)"))
                    TryAutoPlaceRoad();

                if (GUILayout.Button("End Day"))
                    _controller.EndPlayerDay();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void TryAutoPlaceSettlement()
        {
            var player = PlayerId.Human;
            var spots = _controller.GetValidSettlements(player).ToList();
            if (spots.Count == 0) return;
            _controller.PlaceSettlement(spots[0], player);
        }

        private void TryAutoPlaceRoad()
        {
            var player = PlayerId.Human;
            var roads = _controller.GetValidRoads(player).ToList();
            if (roads.Count == 0) return;
            _controller.PlaceRoad(roads[0], player);
        }

        private static ResourceType PickDefaultResource(GameState state)
        {
            if (state.TomorrowRolls.Count == 0) return ResourceType.Wood;
            return state.TomorrowRolls.OrderBy(kv => kv.Value).First().Key;
        }

        private static string FormatResources(string label, ResourceBundle bundle)
        {
            var sb = new StringBuilder($"{label}: ");
            sb.Append($"W{bundle.Wood} B{bundle.Brick} Wh{bundle.Wheat} S{bundle.Sheep} St{bundle.Stone}");
            return sb.ToString();
        }
    }
}
