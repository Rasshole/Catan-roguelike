using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core;

namespace CatanRoguelike.Core.Yield
{
    /// <summary>Hex tiles that yield for a player on the current dice + weather rolls.</summary>
    public static class ProductionHexResolver
    {
        public static List<HexCoord> GetProducingHexesForPlayer(
            GameState state,
            PlayerId player,
            Dictionary<ResourceType, int> resourceRolls)
        {
            var result = new List<HexCoord>();
            var seen = new HashSet<HexCoord>();
            var board = state.Board;
            var diceRolls = state.TodayDiceRolls;

            foreach (var kvp in board.VertexBuildings)
            {
                var (building, owner) = kvp.Value;
                if (owner != player || building == BuildingType.None)
                    continue;

                foreach (var hex in VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (seen.Contains(hex))
                        continue;
                    if (!board.TryGetTile(hex, out var tile))
                        continue;
                    if (tile.IsDesert || !tile.NumberToken.HasValue)
                        continue;
                    if (ModifierService.IsRobberBlocking(state, hex, player))
                        continue;
                    if (state.EventStormTile.HasValue && state.EventStormTile.Value.Equals(hex))
                        continue;
                    if (!resourceRolls.TryGetValue(tile.Resource, out int rollValue) || rollValue <= 0)
                        continue;
                    if (CountDiceHits(tile.NumberToken.Value, diceRolls) <= 0)
                        continue;

                    seen.Add(hex);
                    result.Add(hex);
                }
            }

            return result;
        }

        private static int CountDiceHits(int token, IReadOnlyList<int> diceRolls)
        {
            if (diceRolls == null || diceRolls.Count == 0)
                return 0;

            int hits = 0;
            foreach (var roll in diceRolls)
            {
                if (roll == token)
                    hits++;
            }

            return hits;
        }
    }
}
