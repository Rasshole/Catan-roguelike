using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Tests
{
    public class Act3EventTests
    {
        private static readonly EventId[] Act3OnlyEvents =
        {
            EventId.PortBlockade,
            EventId.ResourceLevy
        };

        private static GameState CreateState()
        {
            var board = MapPresets.CreateBoard(MapSize.Small);
            var state = new GameState(board);
            state.Ports = PortAccess.DiscoverPorts(board);
            return state;
        }

        [Test]
        public void MaybeRollEvent_Act3OnlyEvents_NeverAppearInAct1OrAct2()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                for (int act = 1; act <= 2; act++)
                {
                    var id = new EventEngine(seed).MaybeRollEvent(act);
                    if (id == EventId.None)
                        continue;

                    foreach (var forbidden in Act3OnlyEvents)
                        Assert.AreNotEqual(forbidden, id,
                            $"Seed {seed} act {act} rolled Act 3-only event {id}");
                }
            }
        }

        [Test]
        public void MaybeRollEvent_Act3_CanRollPortBlockade()
        {
            bool saw = false;
            for (int seed = 0; seed < 500 && !saw; seed++)
            {
                if (new EventEngine(seed).MaybeRollEvent(3) == EventId.PortBlockade)
                    saw = true;
            }

            Assert.IsTrue(saw, "Port Blockade should appear in Act 3 weighted pool");
        }

        [Test]
        public void MaybeRollEvent_Act3_CanRollResourceLevy()
        {
            bool saw = false;
            for (int seed = 0; seed < 500 && !saw; seed++)
            {
                if (new EventEngine(seed).MaybeRollEvent(3) == EventId.ResourceLevy)
                    saw = true;
            }

            Assert.IsTrue(saw, "Resource Levy should appear in Act 3 weighted pool");
        }

        [Test]
        public void ApplyEvent_PortBlockade_SetsBlockedPortVertex()
        {
            var state = CreateState();
            var engine = new EventEngine(42);

            engine.ApplyEvent(state, EventId.PortBlockade);

            Assert.AreEqual(EventId.PortBlockade, state.ActiveEvent);
            Assert.IsTrue(state.EventBlockedPortVertex.HasValue);
            Assert.IsTrue(state.Ports.Any(p => p.Vertex.Equals(state.EventBlockedPortVertex.Value)));
            Assert.That(state.EventMessage, Does.Contain("Port Blockade"));
        }

        [Test]
        public void ApplyEvent_PortBlockade_BlocksPortDiscountOnThatVertex()
        {
            var state = CreateState();
            var port = state.Ports[0];
            var vertex = VertexGraph.Canonicalize(port.Vertex);
            state.Board.VertexBuildings[vertex] = (BuildingType.Settlement, PlayerId.Human);
            state.EventBlockedPortVertex = vertex;

            var deal = new ShopDeal(
                port.SpecificResource ?? ResourceType.Wood,
                ShopGenerator.BaseTradeRate,
                ResourceType.Brick,
                1);

            int cost = PortAccess.GetEffectiveGiveAmount(
                state.Board, PlayerId.Human, deal, state.Ports, state.EventBlockedPortVertex);

            Assert.AreEqual(ShopGenerator.BaseTradeRate, cost,
                "Blockaded port should not reduce trade rate");
        }

        [Test]
        public void ApplyEvent_ResourceLevy_DeductsMostAbundantResource()
        {
            var state = CreateState();
            state.PlayerInventory = new ResourceBundle
            {
                Wood = 2,
                Brick = 5,
                Wheat = 5,
                Sheep = 1,
                Stone = 0
            };
            var engine = new EventEngine();

            engine.ApplyEvent(state, EventId.ResourceLevy);

            Assert.AreEqual(EventId.ResourceLevy, state.ActiveEvent);
            Assert.AreEqual(2, state.PlayerInventory.Wood);
            Assert.AreEqual(5, state.PlayerInventory.Brick);
            Assert.AreEqual(4, state.PlayerInventory.Wheat,
                "Levy should take 1 from the tied highest resource (Wheat wins tie-break)");
            Assert.AreEqual(1, state.PlayerInventory.Sheep);
            Assert.That(state.EventMessage, Does.Contain("Resource Levy"));
        }

        [Test]
        public void ApplyEvent_ResourceLevy_SkipsWhenInventoryEmpty()
        {
            var state = CreateState();
            state.PlayerInventory = ResourceBundle.Zero;
            var engine = new EventEngine();

            engine.ApplyEvent(state, EventId.ResourceLevy);

            Assert.AreEqual(ResourceBundle.Zero.Wood, state.PlayerInventory.Wood);
        }

        [Test]
        public void ClearDailyEventEffects_ClearsPortBlockadeFlag()
        {
            var state = CreateState();
            var engine = new EventEngine(42);

            engine.ApplyEvent(state, EventId.PortBlockade);
            Assert.IsTrue(state.EventBlockedPortVertex.HasValue);

            engine.ClearDailyEventEffects(state);

            Assert.AreEqual(EventId.None, state.ActiveEvent);
            Assert.IsNull(state.EventBlockedPortVertex);
        }
    }
}
