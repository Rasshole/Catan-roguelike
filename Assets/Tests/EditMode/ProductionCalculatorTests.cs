using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class ProductionCalculatorTests
    {
        [Test]
        public void SettlementOnThreeHexCorner_ProducesFromAllAdjacentResources()
        {
            var board = MapPresets.CreateBoard();
            var rolls = new Dictionary<ResourceType, int>
            {
                [ResourceType.Wood] = 2,
                [ResourceType.Brick] = 1,
                [ResourceType.Wheat] = 2,
                [ResourceType.Sheep] = 1,
                [ResourceType.Stone] = 1
            };

            // Center hex (0,0) is wood — place on its vertex 0 which touches multiple tiles
            var vertex = VertexGraph.Canonicalize(new Vertex(new HexCoord(0, 0), 0));
            board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);

            var production = ProductionCalculator.CalculateForPlayer(board, PlayerId.Human, rolls);

            // Should produce from every adjacent hex tile at this vertex, not just one
            Assert.Greater(production.Total, 2, "Settlement should yield from multiple adjacent hexes");
        }
    }
}
