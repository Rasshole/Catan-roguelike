using System;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Yield
{
    public static class ProductionCalculator
    {
        public static ResourceBundle CalculateForPlayer(
            BoardState board,
            PlayerId player,
            System.Collections.Generic.Dictionary<ResourceType, int> rolls)
        {
            var production = ResourceBundle.Zero;

            foreach (var kvp in board.VertexBuildings)
            {
                var (building, owner) = kvp.Value;
                if (owner != player || building == BuildingType.None) continue;

                bool isCity = building == BuildingType.City;

                foreach (var hex in VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (!board.TryGetTile(hex, out var tile)) continue;
                    if (tile.HasRobber) continue;
                    if (!rolls.TryGetValue(tile.Resource, out int rollValue)) continue;

                    int amount = isCity
                        ? (int)Math.Ceiling(rollValue * 1.5)
                        : rollValue;

                    if (amount > 0)
                        production.Add(tile.Resource, amount);
                }
            }

            return production;
        }
    }
}
