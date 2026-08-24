using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using NUnit.Framework;

namespace CatanRoguelike.Tests.EditMode
{
    public class MapPresetsTests
    {
        [TestCase(MapSize.Small, 7)]
        [TestCase(MapSize.Medium, 13)]
        [TestCase(MapSize.Large, 19)]
        public void CreateBoard_HasExpectedTileCount(MapSize size, int expectedCount)
        {
            var board = MapPresets.CreateBoard(size);
            Assert.AreEqual(expectedCount, board.Tiles.Count);
            Assert.AreEqual(expectedCount, MapPresets.GetHexCount(size));
        }

        [Test]
        public void MediumThirteenHex_RemovesOuterCornersFromClassicBoard()
        {
            var coords = MapPresets.ThirteenHexCoords().ToList();
            var full = HexMath.Spiral(new HexCoord(0, 0), 2).ToHashSet();
            var set = coords.ToHashSet();

            Assert.AreEqual(13, coords.Count);
            Assert.AreEqual(19, full.Count);

            foreach (var coord in full)
            {
                if (HexMath.Distance(new HexCoord(0, 0), coord) < 2)
                    Assert.IsTrue(set.Contains(coord), $"Inner tile {coord} should remain.");
            }

            int cornerCount = 0;
            foreach (var coord in full)
            {
                if (HexMath.Distance(new HexCoord(0, 0), coord) != 2)
                    continue;

                int neighbors = HexCoord.Directions.Count(dir => full.Contains(coord + dir));
                if (neighbors == 2)
                {
                    cornerCount++;
                    Assert.IsFalse(set.Contains(coord), $"Corner tile {coord} should be removed.");
                }
                else if (neighbors == 3)
                {
                    Assert.IsTrue(set.Contains(coord), $"Edge tile {coord} should remain.");
                }
            }

            Assert.AreEqual(6, cornerCount);
        }

        [Test]
        public void MediumThirteenHex_IsConnectedShape()
        {
            var coords = MapPresets.ThirteenHexCoords().ToList();
            Assert.AreEqual(13, coords.Count);

            var set = coords.ToHashSet();
            foreach (var coord in coords)
            {
                if (coord.Equals(new HexCoord(0, 0)))
                    continue;

                bool hasNeighbor = false;
                foreach (var dir in HexCoord.Directions)
                {
                    if (set.Contains(coord + dir))
                    {
                        hasNeighbor = true;
                        break;
                    }
                }

                Assert.IsTrue(hasNeighbor, $"Tile {coord} has no neighbor in the 13-hex map.");
            }
        }

        [Test]
        public void LargeNineteenHex_MatchesFullRadiusTwoSpiral()
        {
            var preset = MapPresets.LargeNineteenHex();
            var spiral = HexMath.Spiral(new HexCoord(0, 0), 2).ToList();

            Assert.AreEqual(19, preset.Count);
            CollectionAssert.AreEquivalent(
                spiral.Select(c => (c.Q, c.R)),
                preset.Select(p => (p.coord.Q, p.coord.R)));
        }

        [Test]
        public void CreateBoard_MarksCoastalTiles()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            Assert.IsTrue(board.Tiles.Values.Any(t => t.IsCoastal));
            Assert.IsFalse(board.Tiles[new HexCoord(0, 0)].IsCoastal);
        }
    }
}
