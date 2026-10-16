using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Yield
{
    public static class FishProductionCalculator
    {
        public static int CalculateForPlayer(GameState state, PlayerId player, IReadOnlyList<int> diceRolls)
        {
            if (!FishermenRules.IsEnabled(state) || diceRolls == null || diceRolls.Count == 0)
                return 0;

            var board = state.Board;
            int fish = 0;

            foreach (var kvp in board.VertexBuildings)
            {
                var (building, owner) = kvp.Value;
                if (owner != player || building == BuildingType.None)
                    continue;

                foreach (var hex in VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (!board.TryGetTile(hex, out var tile))
                        continue;
                    if (!tile.IsFishTile || !tile.NumberToken.HasValue || tile.FishValue <= 0)
                        continue;
                    if (ModifierService.IsRobberBlocking(state, hex, player))
                        continue;

                    int hits = CountDiceHits(tile.NumberToken.Value, diceRolls);
                    if (hits <= 0)
                        continue;

                    int perHit = building == BuildingType.City
                        ? tile.FishValue + 1
                        : tile.FishValue;
                    fish += perHit * hits;
                }
            }

            return fish;
        }

        private static int CountDiceHits(int token, IReadOnlyList<int> diceRolls)
        {
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
