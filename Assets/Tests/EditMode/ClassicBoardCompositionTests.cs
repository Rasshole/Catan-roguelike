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

        [Test]
        public void ExtendedThirtyHexCoords_AreMirrorSymmetric_AndCentred()
        {
            // v135: the 5–6 player island must not lean. In doubled pointy-top x (2q + r) every row is centred on
            // x = −1, and the shape mirrors left/right and top/bottom.
            var coords = MapPresets.ExtendedThirtyHexCoords().ToList();
            var set = coords.ToHashSet();
            Assert.IsTrue(set.Contains(new HexCoord(0, 0)), "desert hex is on the island");
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 5, 4, 3 },
                coords.GroupBy(c => c.R).OrderBy(g => g.Key).Select(g => g.Count()).ToArray());
            foreach (var c in coords)
            {
                Assert.IsTrue(set.Contains(new HexCoord(-1 - c.Q - c.R, c.R)), $"left/right mirror of {c}");
                Assert.IsTrue(set.Contains(new HexCoord(c.Q + c.R, -c.R)), $"top/bottom mirror of {c}");
            }

            double meanX = coords.Average(c => HexMath.ToWorldPosition(c).x);
            double meanZ = coords.Average(c => HexMath.ToWorldPosition(c).z);
            Assert.AreEqual(-0.5 * 1.7320508 * HexMath.OuterRadius, meanX, 1e-3);
            Assert.AreEqual(0.0, meanZ, 1e-3);
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
