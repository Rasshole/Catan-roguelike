using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using NUnit.Framework;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class BuildingPieceLimitsTests
    {
        [Test]
        public void Defaults_MatchClassicPieceBank()
        {
            var board = MapPresets.CreateBoard();
            Assert.AreEqual(15, BuildingPieceLimits.GetRoadsRemaining(board, PlayerId.Human));
            Assert.AreEqual(5, BuildingPieceLimits.GetSettlementsRemaining(board, PlayerId.Human));
            Assert.AreEqual(4, BuildingPieceLimits.GetCitiesRemaining(board, PlayerId.Human));
        }

        [Test]
        public void Counts_DecrementAfterPlacement()
        {
            var board = MapPresets.CreateBoard();
            var vertex = VertexGraph.Canonicalize(new Vertex(new HexCoord(0, 0), 0));
            board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);
            board.Roads[new Edge(vertex, VertexGraph.Canonicalize(new Vertex(new HexCoord(0, 0), 1)))] = PlayerId.Human;

            Assert.AreEqual(14, BuildingPieceLimits.GetRoadsRemaining(board, PlayerId.Human));
            Assert.AreEqual(4, BuildingPieceLimits.GetSettlementsRemaining(board, PlayerId.Human));
            Assert.IsTrue(BuildingPieceLimits.CanPlaceCity(board, PlayerId.Human));
        }
    }
}
