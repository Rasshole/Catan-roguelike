using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// v148: like official Fishermen of Catan, the coastal fishing grounds only carry 4, 5, 6, 8, 9 and 10. The rolls
    /// 2, 3, 11 and 12 belong to the lake alone.
    /// </summary>
    public class MatchV148FishingGroundNumbersTests
    {
        private static readonly int[] Allowed = { 4, 5, 6, 8, 9, 10 };

        [Test]
        public void CoastalFishingGrounds_OnlyUse4To10_NeverTheLakeNumbers(
            [Values(MapSize.Medium, MapSize.Large, MapSize.Extended)] MapSize map)
        {
            CollectionAssert.AreEquivalent(Allowed, SeaFrameSlotLayout.SeaFishDisplayTokens);
            for (int seed = 1; seed <= 40; seed++)
            {
                var config = MatchConfig.Create(map == MapSize.Extended ? 5 : 4, 0, seed, map);
                config.Fishermen = true;
                var board = new MatchController(config).State.Board;
                Assert.Greater(board.SeaFishGrounds.Count, 0, $"{map} seed {seed}: fishing grounds");
                var numbers = board.SeaFishGrounds.Values.Select(g => g.NumberToken ?? 0).ToList();
                foreach (int n in numbers)
                {
                    CollectionAssert.Contains(Allowed, n, $"{map} seed {seed}: a coastal ground on {n}");
                    CollectionAssert.DoesNotContain(MatchController.LakeNumbers, n);
                }

                Assert.AreEqual(numbers.Count, numbers.Distinct().Count(), $"{map} seed {seed}: no number twice on the coast");
                if (numbers.Count == 6)
                    CollectionAssert.AreEquivalent(Allowed, numbers, "all six official numbers");
                if (map != MapSize.Medium) // the 13-hex map has no desert, so no lake
                    Assert.IsTrue(board.LakeHex.HasValue, "the lake keeps 2, 3, 11 and 12");
            }
        }

        [Test]
        public void SinglePlayerFishermenBoard_UsesTheSameCoastNumbers()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var board = MapPresets.CreateBoard(MapSize.Large, seed, new MapGenerationOptions(fishermenOfCatan: true));
                Assert.Greater(board.SeaFishGrounds.Count, 0);
                Assert.IsTrue(board.SeaFishGrounds.Values.All(g => Allowed.Contains(g.NumberToken ?? 0)), $"seed {seed}");
            }
        }
    }
}
