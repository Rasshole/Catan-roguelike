using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class PortAccessTests
    {
        [Test]
        public void SpecificPort_ReducesTradeCost_WhenPlayerOnPortVertex()
        {
            var board = MapPresets.CreateBoard();
            var ports = PortAccess.DiscoverPorts(board);
            Assert.Greater(ports.Count, 0);

            var port = ports[0];
            board.VertexBuildings[port.Vertex] = (BuildingType.Settlement, PlayerId.Human);

            var deal = new ShopDeal(port.SpecificResource!.Value, ShopGenerator.BaseTradeRate, ResourceType.Wheat, 1);
            int cost = PortAccess.GetEffectiveGiveAmount(board, PlayerId.Human, deal, ports);
            Assert.AreEqual(2, cost);
        }
    }
}
