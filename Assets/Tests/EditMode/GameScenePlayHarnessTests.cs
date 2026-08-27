using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Game;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class GameScenePlayHarnessTests
    {
        [Test]
        public void CompleteRunSelectAndSetup_ReachesFirstNight()
        {
            var meta = MetaProgression.CreateFresh();
            var game = new GameController(42, MapSize.Small, meta);

            GameScenePlayHarness.CompleteRunSelectAndSetup(game, meta);

            Assert.True(game.State.RunSetupComplete);
            Assert.AreEqual(GamePhase.NightPlayCard, game.State.Phase);
            Assert.IsNull(game.State.Winner);
        }
    }
}
