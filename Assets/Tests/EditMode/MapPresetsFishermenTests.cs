using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class MapPresetsFishermenTests
    {
        [Test]
        public void CreateBoard_Large_WithFishermen_StillHasNineteenLandHexes()
        {
            var board = MapPresets.CreateBoard(MapSize.Large, 42, new MapGenerationOptions(true));
            Assert.AreEqual(19, board.Tiles.Count);
            Assert.GreaterOrEqual(board.SeaFishGrounds.Count, 3);
            Assert.IsTrue(board.Tiles[new HexCoord(0, 0)].IsFishTile);
        }
    }
}
