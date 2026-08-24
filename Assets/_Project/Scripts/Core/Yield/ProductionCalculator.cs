using System;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Yield
{
    public static class ProductionCalculator
    {
        public static ResourceBundle CalculateForPlayer(
            GameState state,
            PlayerId player,
            System.Collections.Generic.Dictionary<ResourceType, int> rolls)
        {
            var board = state.Board;
            var production = ResourceBundle.Zero;

            foreach (var kvp in board.VertexBuildings)
            {
                var (building, owner) = kvp.Value;
                if (owner != player || building == BuildingType.None) continue;

                bool isCity = building == BuildingType.City;
                int bestOnVertex = 0;

                foreach (var hex in VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (!board.TryGetTile(hex, out var tile)) continue;
                    if (IsBlocked(state, hex, player)) continue;
                    if (state.EventStormTile.HasValue && state.EventStormTile.Value.Equals(hex)) continue;
                    if (!rolls.TryGetValue(tile.Resource, out int rollValue)) continue;

                    int amount = isCity
                        ? (int)Math.Ceiling(rollValue * 1.5)
                        : rollValue;

                    if (state.EventStoneDouble && tile.Resource == ResourceType.Stone)
                        amount *= 2;

                    if (player == PlayerId.Human && state.HasUnique(UniqueBuildingId.Sawmill)
                        && tile.Resource == ResourceType.Wood)
                        amount += 1;

                    if (amount > 0)
                    {
                        production.Add(tile.Resource, amount);
                        bestOnVertex = Math.Max(bestOnVertex, amount);
                    }
                }

                if (player == PlayerId.Human && isCity && state.HasPerk(LevelUpPerkId.CityProductionBoost)
                    && bestOnVertex > 0)
                {
                    // +1 on best resource already counted per hex; add flat +1 once per city
                    production.Add(ResourceType.Wheat, 0); // no-op placeholder — apply below
                }
            }

            if (player == PlayerId.Human && state.HasPerk(LevelUpPerkId.CityProductionBoost))
            {
                foreach (var kvp in board.VertexBuildings)
                {
                    if (kvp.Value.owner != player || kvp.Value.type != BuildingType.City) continue;
                    ResourceType? best = null;
                    int bestAmt = 0;
                    foreach (var hex in VertexGraph.GetHexesForVertex(kvp.Key))
                    {
                        if (!board.TryGetTile(hex, out var tile)) continue;
                        if (!rolls.TryGetValue(tile.Resource, out int rv)) continue;
                        int amt = (int)Math.Ceiling(rv * 1.5);
                        if (amt > bestAmt) { bestAmt = amt; best = tile.Resource; }
                    }
                    if (best.HasValue) production.Add(best.Value, 1);
                }
            }

            return production;
        }

        private static bool IsBlocked(GameState state, HexCoord hex, PlayerId player)
        {
            if (!state.Board.TryGetTile(hex, out var tile)) return true;
            return ModifierService.IsRobberBlocking(state, hex, player);
        }
    }
}
