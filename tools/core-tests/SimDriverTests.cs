using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.SimRunner;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests.SimRunner
{
  public class SimDriverTests
  {
    [Test]
    public void TryAllDayBuilds_UpgradesMultipleSettlements_WhenResourcesAllow()
    {
      var game = new GameController(42, MapSize.Small);
      game.State.Phase = GamePhase.DayPlayerActions;
      var v0 = VertexGraph.Canonicalize(new Vertex(new HexCoord(0, 0), 0));
      var v1 = VertexGraph.Canonicalize(new Vertex(new HexCoord(1, 0), 0));
      game.State.Board.VertexBuildings.Clear();
      game.State.Board.VertexBuildings[v0] = (BuildingType.Settlement, PlayerId.Human);
      game.State.Board.VertexBuildings[v1] = (BuildingType.Settlement, PlayerId.Human);

      var inv = new ResourceBundle();
      inv.Set(ResourceType.Wheat, 4);
      inv.Set(ResourceType.Stone, 6);
      game.State.PlayerInventory = inv;

      SimDriver.TryAllDayBuilds(game);

      Assert.AreEqual(BuildingType.City, game.State.Board.VertexBuildings[v0].type);
      Assert.AreEqual(BuildingType.City, game.State.Board.VertexBuildings[v1].type);
    }

    [Test]
    public void TryPlayUsefulNightCard_PlaysYearOfPlenty_FromHand()
    {
      var game = new GameController(7, MapSize.Small);
      game.State.Phase = GamePhase.NightPlayCard;
      game.State.PlayerHand.Clear();
      game.State.PlayerHand.Add(CardId.YearOfPlenty);

      Assert.IsTrue(SimDriver.TryPlayUsefulNightCard(game));
      Assert.AreEqual(GamePhase.DayPlayerActions, game.State.Phase);
      CollectionAssert.DoesNotContain(game.State.PlayerHand, CardId.YearOfPlenty);
      Assert.Greater(game.State.PlayerInventory[ResourceType.Wheat], 0);
    }
  }
}
