using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;

namespace CatanRoguelike.Tests.EditMode
{
    public class ClassicBoardCompositionTests
    {
        [Test]
        public void LargeNineteenHex_HasStandardResourceCounts()
        {
            var preset = MapPresets.LargeNineteenHex();
            Assert.AreEqual(19, preset.Count);

            var center = new HexCoord(0, 0);
            var productive = preset.Where(p => p.coord != center);
            int wood = productive.Count(p => p.resource == ResourceType.Wood);
            int sheep = productive.Count(p => p.resource == ResourceType.Sheep);
            int wheat = productive.Count(p => p.resource == ResourceType.Wheat);
            int brick = productive.Count(p => p.resource == ResourceType.Brick);
            int stone = productive.Count(p => p.resource == ResourceType.Stone);

            Assert.AreEqual(4, wood);
            Assert.AreEqual(4, sheep);
            Assert.AreEqual(4, wheat);
            Assert.AreEqual(3, brick);
            Assert.AreEqual(3, stone);
        }

        [Test]
        public void CreateBoard_Large_CenterIsDesertTerrain()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            Assert.IsTrue(board.Tiles[new HexCoord(0, 0)].IsDesert);
            Assert.IsFalse(board.Tiles[new HexCoord(0, 0)].NumberToken.HasValue);
        }

        [Test]
        public void CreateBoard_Large_HasNinePorts()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            var ports = PortAccess.DiscoverPorts(board);
            Assert.AreEqual(9, ports.Count);
            Assert.AreEqual(5, ports.Count(p => !p.IsGeneric));
            Assert.AreEqual(4, ports.Count(p => p.IsGeneric));
        }

        [Test]
        public void ExtendedThirtyHexCoords_HasThirtyTiles()
        {
            var coords = MapPresets.ExtendedThirtyHexCoords().ToList();
            Assert.AreEqual(30, coords.Count);
        }

        [TestCase(MapSize.Extended, 30)]
        [TestCase(MapSize.Huge, 37)]
        public void CreateBoard_HasExpectedLandCount(MapSize size, int expected)
        {
            var board = MapPresets.CreateBoard(size);
            int land = board.Tiles.Values.Count(t => !t.IsFishTile);
            Assert.AreEqual(expected, land);
        }
    }
}
