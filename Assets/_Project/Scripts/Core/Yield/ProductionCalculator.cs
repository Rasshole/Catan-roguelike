using System;
using System.Collections.Generic;
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
            Dictionary<ResourceType, int> resourceRolls)
        {
            var board = state.Board;
            var production = ResourceBundle.Zero;
            var diceRolls = state.TodayDiceRolls;

            foreach (var kvp in board.VertexBuildings)
            {
                var (building, owner) = kvp.Value;
                if (owner != player || building == BuildingType.None) continue;

                bool isCity = building == BuildingType.City;
                int bestOnVertex = 0;

                foreach (var hex in VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (!board.TryGetTile(hex, out var tile)) continue;
                    if (tile.IsDesert || !tile.NumberToken.HasValue) continue;
                    if (IsBlocked(state, hex, player)) continue;
                    if (state.EventStormTile.HasValue && state.EventStormTile.Value.Equals(hex)) continue;
                    if (!resourceRolls.TryGetValue(tile.Resource, out int rollValue) || rollValue <= 0) continue;
                    int hits = CountDiceHits(tile.NumberToken.Value, diceRolls);
                    if (hits <= 0) continue;

                    int perHit = isCity
                        ? (int)Math.Ceiling(rollValue * 1.5)
                        : rollValue;
                    int amount = perHit * hits;

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
                    production.Add(ResourceType.Wheat, 0);
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
                        if (tile.IsDesert || !tile.NumberToken.HasValue) continue;
                        if (!resourceRolls.TryGetValue(tile.Resource, out int rv) || rv <= 0) continue;
                        if (CountDiceHits(tile.NumberToken.Value, state.TodayDiceRolls) <= 0) continue;
                        int amt = (int)Math.Ceiling(rv * 1.5);
                        if (amt > bestAmt) { bestAmt = amt; best = tile.Resource; }
                    }
                    if (best.HasValue) production.Add(best.Value, 1);
                }
            }

            return production;
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

        private static bool IsBlocked(GameState state, HexCoord hex, PlayerId player)
        {
            if (!state.Board.TryGetTile(hex, out var tile)) return true;
            return ModifierService.IsRobberBlocking(state, hex, player);
        }
    }
}
