using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
namespace CatanRoguelike.Tests.EditMode
{
    public class FishProductionCalculatorTests
    {
        [Test]
        public void FishProduction_OnlyBorderingIntersectionSettlementsReceiveFish()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            Assert.Greater(board.SeaFishGrounds.Count, 0);

            HexCoord sea = default;
            FishShoalPlacement placement = default;
            foreach (var kvp in board.SeaFishGrounds)
            {
                if (SeaFrameSlotLayout.TryGetFishShoalPlacement(board, kvp.Key, 1f, out placement))
                {
                    sea = kvp.Key;
                    break;
                }
            }

            Assert.AreNotEqual(default, sea);
            int token = board.SeaFishGrounds[sea].NumberToken ?? 0;
            Assert.Greater(token, 1);

            var state = new GameState(board)
            {
                FishermenOfCatanEnabled = true,
                FishermenRunSeed = 99,
                FishBagRemaining = new List<int> { 2, 2, 2, 2 }
            };

            var inland = VertexGraph.Canonicalize(placement.BorderVertexInland);
            var left = VertexGraph.Canonicalize(placement.BorderVertexLeft);
            board.VertexBuildings[inland] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[left] = (BuildingType.Settlement, PlayerId.Ai);

            int humanFish = FishProductionCalculator.CalculateForPlayer(
                state,
                PlayerId.Human,
                new List<int> { token });
            int aiFish = FishProductionCalculator.CalculateForPlayer(
                state,
                PlayerId.Ai,
                new List<int> { token });

            Assert.Greater(humanFish, 0, "Settlement on inland border vertex should receive fish.");
            Assert.Greater(aiFish, 0, "Settlement on left border vertex should receive fish.");

        }
    }
}
