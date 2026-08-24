using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class PlacementValidatorTests
    {
        [Test]
        public void CannotPlaceTwoAdjacentSettlements()
        {
            var board = MapPresets.CreateBoard();
            var validator = new PlacementValidator();

            var v0 = FindAnyVertex(board);
            Assert.IsTrue(validator.CanPlaceSettlement(board, v0, PlayerId.Human, setupPhase: true));

            board.VertexBuildings[VertexGraph.Canonicalize(v0)] = (BuildingType.Settlement, PlayerId.Human);

            foreach (var adjacent in VertexGraph.GetAdjacentVertices(v0))
            {
                Assert.IsFalse(validator.CanPlaceSettlement(board, adjacent, PlayerId.Ai, setupPhase: true));
            }
        }

        private static Vertex FindAnyVertex(BoardState board)
        {
            foreach (var hex in board.Tiles.Keys)
                return new Vertex(hex, 0);
            Assert.Fail("No tiles");
            return default;
        }
    }
}
