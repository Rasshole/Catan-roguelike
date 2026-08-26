using System.Linq;
using CatanRoguelike.Core;
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
        [TestCase(MapSize.Small)]
        [TestCase(MapSize.Medium)]
        [TestCase(MapSize.Large)]
        public void DiscoverPorts_CreatesAtLeastOneGenericPort(MapSize size)
        {
            var board = MapPresets.CreateBoard(size);
            var ports = PortAccess.DiscoverPorts(board);

            Assert.Greater(ports.Count, 0);
            Assert.Greater(ports.Count(p => p.IsGeneric), 0,
                $"Expected generic 3:1 port on {size}");
            Assert.Greater(ports.Count(p => !p.IsGeneric), 0,
                $"Expected specific 2:1 port on {size}");
        }

        [Test]
        public void DiscoverPorts_DoesNotCoverEveryCoastalEdgeVertex()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            var ports = PortAccess.DiscoverPorts(board);

            int edgeVertices = 0;
            var seen = new System.Collections.Generic.HashSet<Vertex>();
            foreach (var hex in board.Tiles.Values.Where(t => t.IsCoastal))
            {
                for (int c = 0; c < 6; c++)
                {
                    var v = VertexGraph.Canonicalize(new Vertex(hex.Coord, c));
                    if (!seen.Add(v)) continue;
                    int onBoard = VertexGraph.GetHexesForVertex(v).Count(h => board.Tiles.ContainsKey(h));
                    if (onBoard <= 2) edgeVertices++;
                }
            }

            Assert.Less(ports.Count, edgeVertices,
                "Ports should be sparse — not every coastal edge vertex is a harbor");
        }

        [Test]
        public void SpecificPort_ReducesTradeCost_WhenPlayerOnPortVertex()
        {
            var board = MapPresets.CreateBoard();
            var ports = PortAccess.DiscoverPorts(board);
            var port = ports.First(p => !p.IsGeneric);
            board.VertexBuildings[port.Vertex] = (BuildingType.Settlement, PlayerId.Human);

            var deal = new ShopDeal(port.SpecificResource!.Value, ShopGenerator.BaseTradeRate, ResourceType.Wheat, 1);
            int cost = PortAccess.GetEffectiveGiveAmount(board, PlayerId.Human, deal, ports);
            Assert.AreEqual(2, cost);
        }

        [Test]
        public void GenericPort_ReducesTradeCost_ToThreeForOne()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            var ports = PortAccess.DiscoverPorts(board);
            var generic = ports.First(p => p.IsGeneric);
            board.VertexBuildings[generic.Vertex] = (BuildingType.Settlement, PlayerId.Human);

            Assert.IsTrue(PortAccess.HasGenericPort(board, PlayerId.Human, ports));

            var deal = new ShopDeal(ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            int cost = PortAccess.GetEffectiveGiveAmount(board, PlayerId.Human, deal, ports);
            Assert.AreEqual(3, cost);
        }

        [Test]
        public void SpecificPort_BeatsGeneric_WhenPlayerControlsBoth()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            var ports = PortAccess.DiscoverPorts(board);
            var specific = ports.First(p => !p.IsGeneric);
            var generic = ports.First(p => p.IsGeneric);
            board.VertexBuildings[specific.Vertex] = (BuildingType.Settlement, PlayerId.Human);
            board.VertexBuildings[generic.Vertex] = (BuildingType.City, PlayerId.Human);

            var deal = new ShopDeal(specific.SpecificResource!.Value, ShopGenerator.BaseTradeRate,
                ResourceType.Wheat, 1);
            int cost = PortAccess.GetEffectiveGiveAmount(board, PlayerId.Human, deal, ports);
            Assert.AreEqual(2, cost);
        }

        [Test]
        public void NoPort_UsesBaseTradeRate()
        {
            var board = MapPresets.CreateBoard(MapSize.Large);
            var ports = PortAccess.DiscoverPorts(board);

            var deal = new ShopDeal(ResourceType.Wood, ShopGenerator.BaseTradeRate, ResourceType.Brick, 1);
            int cost = PortAccess.GetEffectiveGiveAmount(board, PlayerId.Human, deal, ports);
            Assert.AreEqual(ShopGenerator.BaseTradeRate, cost);
        }
    }
}
