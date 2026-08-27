using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class NumberTokenTests
    {
        [Test]
        public void CreateBoard_Small_AssignsSevenTokens_NoSevens()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);

            var tokens = board.Tiles.Values
                .Where(t => t.NumberToken.HasValue)
                .Select(t => t.NumberToken.Value)
                .ToList();

            Assert.AreEqual(7, tokens.Count);
            CollectionAssert.DoesNotContain(tokens, 7);
        }

        [Test]
        public void CreateBoard_Large_DesertHasNoToken_OthersHaveEighteen()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            var center = new HexCoord(0, 0);

            Assert.IsTrue(board.Tiles[center].IsDesert);
            Assert.IsFalse(board.Tiles[center].NumberToken.HasValue);

            int assigned = board.Tiles.Values.Count(t => t.NumberToken.HasValue);
            Assert.AreEqual(18, assigned);
        }

        [Test]
        public void AssignMissingTokens_AvoidsAdjacentRedSixAndEight()
        {
            var board = MapPresets.CreateBoard(MapSize.Medium);

            foreach (var tile in board.Tiles.Values)
            {
                if (!tile.NumberToken.HasValue || !NumberTokenLibrary.IsRedNumber(tile.NumberToken.Value))
                    continue;

                foreach (var dir in HexCoord.Directions)
                {
                    var neighbor = tile.Coord + dir;
                    if (!board.TryGetTile(neighbor, out var other)) continue;
                    if (!other.NumberToken.HasValue) continue;
                    Assert.IsFalse(NumberTokenLibrary.IsRedNumber(other.NumberToken.Value),
                        $"Red {tile.NumberToken} at {tile.Coord} touches red {other.NumberToken} at {neighbor}");
                }
            }
        }

        [Test]
        public void ExpandBoard_AssignsTokensToNewHexes()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            int before = board.Tiles.Values.Count(t => t.NumberToken.HasValue);

            MapPresets.ExpandBoard(board, MapSize.Medium);

            int after = board.Tiles.Values.Count(t => t.NumberToken.HasValue);
            Assert.AreEqual(13, after);
            Assert.Greater(after, before);
            Assert.IsFalse(board.Tiles.Values.Any(t => t.NumberToken == 7));
        }

        [Test]
        public void GetPipWeight_MatchesClassicDistribution()
        {
            Assert.AreEqual(5, NumberTokenLibrary.GetPipWeight(6));
            Assert.AreEqual(5, NumberTokenLibrary.GetPipWeight(8));
            Assert.AreEqual(1, NumberTokenLibrary.GetPipWeight(2));
            Assert.AreEqual(0, NumberTokenLibrary.GetPipWeight(7));
        }
    }
}
