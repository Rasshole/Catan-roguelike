using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

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

            foreach (var seaKvp in board.SeaFishGrounds)
            {
                var tile = seaKvp.Value;
                if (!tile.IsFishTile || !tile.NumberToken.HasValue || tile.FishValue < 0)
                    continue;

                if (!SeaFrameSlotLayout.TryGetFishShoalPlacement(board, seaKvp.Key, 1f, out FishShoalPlacement placement))
                    continue;

                if (IsRobberBlockingFishGround(state, board, placement, player))
                    continue;

                int hits = CountDiceHits(tile.NumberToken.Value, diceRolls);
                if (hits <= 0)
                    continue;

                foreach (var buildingKvp in board.VertexBuildings)
                {
                    var (building, owner) = buildingKvp.Value;
                    if (owner != player || building == BuildingType.None)
                        continue;
                    if (!placement.BordersVertex(buildingKvp.Key))
                        continue;

                    for (int h = 0; h < hits; h++)
                    {
                        int drawn = FishTokenBag.Draw(state, state.FishermenRunSeed);
                        if (drawn <= 0)
                            continue;

                        int yield = building == BuildingType.City ? drawn + 1 : drawn;
                        fish += yield;
                    }
                }
            }

            return fish;
        }

        private static bool IsRobberBlockingFishGround(
            GameState state,
            BoardState board,
            FishShoalPlacement placement,
            PlayerId player)
        {
            foreach (var hex in VertexGraph.GetHexesForVertex(placement.BorderVertexInland))
            {
                if (!board.Tiles.ContainsKey(hex))
                    continue;
                if (ModifierService.IsRobberBlocking(state, hex, player))
                    return true;
            }

            return false;
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
