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
            int land = 0;
            int seaFish = 0;
            foreach (var tile in board.Tiles.Values)
            {
                if (tile.IsFishTile && !tile.IsDesert)
                    seaFish++;
                else
                    land++;
            }

            Assert.AreEqual(19, land);
            Assert.GreaterOrEqual(seaFish, 3);
            Assert.IsTrue(board.Tiles[new HexCoord(0, 0)].IsFishTile);
        }
    }
}
