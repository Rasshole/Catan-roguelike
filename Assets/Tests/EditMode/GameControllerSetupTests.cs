using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    public class GameControllerSetupTests
    {
        [Timeout(2000)]
        [Test]
        public void ConfirmRunSetup_AiPlacesTwoSettlementsAndTwoRoads()
        {
            var game = new GameController(seed: 1, MapSize.Small);
            game.SelectMap(MapSize.Small);
            game.SelectLeader(LeaderId.Merchant);
            game.ToggleDraftUnique(UniqueBuildingId.Sawmill);
            game.ToggleDraftUnique(UniqueBuildingId.GuildHall);

            game.ConfirmRunSetup();

            Assert.AreEqual(2, game.State.Board.CountBuildings(PlayerId.Ai, BuildingType.Settlement));
            Assert.AreEqual(2, game.State.Board.CountRoads(PlayerId.Ai));
            Assert.AreEqual(0, game.State.Board.CountBuildings(PlayerId.Human, BuildingType.Settlement));
            Assert.AreEqual(GamePhase.SetupPlayerSettlement1, game.State.Phase);
        }
    }
}
