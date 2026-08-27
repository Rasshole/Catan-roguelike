using System.Linq;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class MapExpansionTests
    {
        [Test]
        public void ExpandBoard_SmallToMedium_AddsSixHexes()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            int before = board.Tiles.Count;

            int added = MapPresets.ExpandBoard(board, MapSize.Medium);

            Assert.AreEqual(6, added);
            Assert.AreEqual(before + 6, board.Tiles.Count);
            Assert.AreEqual(MapPresets.GetHexCount(MapSize.Medium), board.Tiles.Count);
        }

        [Test]
        public void ExpandBoard_MediumToLarge_AddsSixCornerHexes()
        {
            var board = MapPresets.CreateBoard(MapSize.Medium);
            int added = MapPresets.ExpandBoard(board, MapSize.Large);

            Assert.AreEqual(6, added);
            Assert.AreEqual(19, board.Tiles.Count);
        }

        [Test]
        public void ExpandBoard_PreservesExistingBuildings()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var vertex = VertexGraph.Canonicalize(new Vertex(new HexCoord(0, 0), 0));
            board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);

            MapPresets.ExpandBoard(board, MapSize.Medium);

            Assert.IsTrue(board.VertexBuildings.ContainsKey(vertex));
            Assert.AreEqual(BuildingType.Settlement, board.VertexBuildings[vertex].type);
        }

        [Test]
        public void ExpandBoard_MarksNewCoastalTiles()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            MapPresets.ExpandBoard(board, MapSize.Medium);

            Assert.IsTrue(board.Tiles.Values.Any(t => t.IsCoastal));
        }

        [Test]
        public void ExpandBoard_SecondCall_AddsNoDuplicates()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            MapPresets.ExpandBoard(board, MapSize.Medium);
            int addedAgain = MapPresets.ExpandBoard(board, MapSize.Medium);

            Assert.AreEqual(0, addedAgain);
            Assert.AreEqual(13, board.Tiles.Count);
        }
    }
}
