using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Yield;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class FishTokenBagTests
    {
        [Test]
        public void Draw_RefillsAndReturnsBootOrFish()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var state = new GameState(board) { FishermenOfCatanEnabled = true, FishermenRunSeed = 42 };
            FishTokenBag.EnsureInitialized(state, 42);

            int draws = 0;
            for (int i = 0; i < 12; i++)
            {
                int v = FishTokenBag.Draw(state, 42);
                if (v >= 0 && v <= 3)
                    draws++;
            }

            Assert.Greater(draws, 6);
        }
    }
}
