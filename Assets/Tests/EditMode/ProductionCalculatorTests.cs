using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class ProductionCalculatorTests
    {
        [Test]
        public void SettlementOnThreeHexCorner_ProducesFromMatchingDiceAndRolls()
        {
            var board = MapPresets.CreateBoard();
            var state = new GameState(board);
            var rolls = new Dictionary<ResourceType, int>
            {
                [ResourceType.Wood] = 2,
                [ResourceType.Brick] = 1,
                [ResourceType.Wheat] = 2,
                [ResourceType.Sheep] = 1,
                [ResourceType.Stone] = 1
            };

            var vertex = VertexGraph.Canonicalize(
                new HexMath.Vertex(new HexCoord(0, 0), 0));

            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (!board.TryGetTile(hex, out var tile)) continue;
                tile.NumberToken = 6;
            }

            state.TodayDiceRolls = new List<int> { 6 };
            board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);

            var production = ProductionCalculator.CalculateForPlayer(state, PlayerId.Human, rolls);

            Assert.Greater(production.Total, 2, "Settlement should yield when dice match adjacent tokens");
        }
    }
}
