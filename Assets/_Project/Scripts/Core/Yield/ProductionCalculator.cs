using System;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Yield
{
    public static class ProductionCalculator
    {
        public static ResourceBundle CalculateDailyProduction(
            BoardState board,
            System.Collections.Generic.Dictionary<ResourceType, int> rolls)
        {
            var production = ResourceBundle.Zero;

            foreach (var vertex in board.VertexBuildings)
            {
                var (building, owner) = vertex.Value;
                if (building == BuildingType.None) continue;

                var hex = GetAdjacentHexForVertex(board, vertex.Key);
                if (!hex.HasValue) continue;

                if (!board.TryGetTile(hex.Value, out var tile)) continue;
                if (tile.HasRobber) continue;

                if (!rolls.TryGetValue(tile.Resource, out int rollValue)) continue;

                int amount = building == BuildingType.City
                    ? (int)Math.Ceiling(rollValue * 1.5)
                    : rollValue;

                if (amount <= 0) continue;

                // Production goes to owner — caller distributes per player
                // This method returns total per resource type for a single owner call
            }

            return production;
        }

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

                var hex = GetAdjacentHexForVertex(board, kvp.Key);
                if (!hex.HasValue) continue;
                if (!board.TryGetTile(hex.Value, out var tile)) continue;
                if (tile.HasRobber) continue;
                if (!rolls.TryGetValue(tile.Resource, out int rollValue)) continue;

                int amount = building == BuildingType.City
                    ? (int)Math.Ceiling(rollValue * 1.5)
                    : rollValue;

                production.Add(tile.Resource, amount);
            }

            return production;
        }

        private static Hex.HexCoord? GetAdjacentHexForVertex(BoardState board, Hex.HexMath.Vertex vertex)
        {
            // Vertex sits on a hex corner — building is associated with nearest tile under vertex
            // Simplified: use the hex the vertex is defined on
            if (board.TryGetTile(vertex.Hex, out _))
                return vertex.Hex;

            foreach (var dir in Hex.HexCoord.Directions)
            {
                var neighbor = vertex.Hex + dir;
                if (board.TryGetTile(neighbor, out _))
                    return neighbor;
            }

            return null;
        }
    }
}
