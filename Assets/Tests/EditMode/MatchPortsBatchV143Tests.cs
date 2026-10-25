using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Civilizations;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// v143: a harbour serves BOTH pier corners (the playtest bug: a settlement on the second pier of the 2:1 Wool
    /// harbour got no 2:1), Market Day never overrides a better harbour rate, and multi-batch bank trades.
    /// </summary>
    public class MatchPortsBatchV143Tests
    {
        private static ResourceBundle Cards(int wood = 0, int brick = 0, int wheat = 0, int sheep = 0, int stone = 0) =>
            new() { Wood = wood, Brick = brick, Wheat = wheat, Sheep = sheep, Stone = stone };

        private static void ToMain(MatchController match, int seat)
        {
            match.State.CurrentSeat = seat;
            match.State.Phase = MatchPhase.Main;
        }

        private static void ClearBuildings(MatchController match) => match.State.Board.VertexBuildings.Clear();

        [Test]
        public void EveryHarbour_ServesBothPierCorners_OnEveryMapSizeAndSeed(
            [Values(2, 3, 4, 5)] int seats, [Values(1, 2, 3, 7, 11, 19, 42, 99)] int seed)
        {
            var match = new MatchController(MatchConfig.Create(seats, 1, seed));
            var board = match.State.Board;
            Assert.Greater(match.State.Ports.Count, 0);
            foreach (var port in match.State.Ports)
            {
                var served = PortAccess.ServedVertices(board, port);
                Assert.IsTrue(served.Contains(VertexGraph.Canonicalize(port.Vertex)));
                if (board.TryGetSeaSlotForPort(port.Vertex, out _))
                    Assert.AreEqual(2, served.Count, $"{port.SpecificResource?.ToString() ?? "3:1"} harbour has two piers");
                foreach (var corner in served)
                {
                    ClearBuildings(match);
                    match.State.ActiveEvent = EventId.None;
                    board.VertexBuildings[corner] = (BuildingType.Settlement, MatchSeats.ToPlayerId(0));
                    foreach (var r in MatchController.AllResources)
                    {
                        int expected = port.SpecificResource.HasValue
                            ? (port.SpecificResource.Value == r ? 2 : 4)
                            : 3;
                        if (match.State.Seats[0].Civilization != CivilizationId.None)
                            continue;
                        Assert.AreEqual(expected, match.BankRatio(0, r),
                            $"seats {seats} seed {seed}: {port.SpecificResource?.ToString() ?? "3:1"} harbour at {corner}, give {r}");
                    }
                }
            }
        }

        [Test]
        public void SecondPierCorner_OfAWoolHarbour_TradesWoolTwoToOne_EvenOnMarketDay()
        {
            for (int seed = 1; seed < 60; seed++)
            {
                var match = new MatchController(MatchConfig.Create(4, 4, seed));
                var board = match.State.Board;
                var wool = match.State.Ports.FirstOrDefault(p => p.SpecificResource == ResourceType.Sheep
                                                                 && PortAccess.ServedVertices(board, p).Count == 2);
                if (wool.SpecificResource == null)
                    continue;
                var second = PortAccess.ServedVertices(board, wool).First(v => !v.Equals(VertexGraph.Canonicalize(wool.Vertex)));
                ClearBuildings(match);
                board.VertexBuildings[second] = (BuildingType.Settlement, MatchSeats.ToPlayerId(0));
                ToMain(match, 0);
                Assert.AreEqual(2, match.BankRatio(0, ResourceType.Sheep), "the other pier counts too");
                match.State.ActiveEvent = EventId.MarketDay;
                Assert.AreEqual(2, match.BankRatio(0, ResourceType.Sheep), "Market Day + 2:1 harbour => 2:1");
                Assert.AreEqual(3, match.BankRatio(0, ResourceType.Wood), "Market Day caps the rest at 3:1");
                match.State.Seats[0].Hand = Cards(sheep: 2);
                Assert.IsTrue(match.Apply(MatchCommand.Trade(0, ResourceType.Sheep, ResourceType.Stone)).Ok);
                Assert.AreEqual(1, match.State.Seats[0].Hand.Stone);
                return;
            }

            Assert.Inconclusive("no wool harbour with a sea slot found");
        }

        private static MatchController PlainMain()
        {
            var match = new MatchController(MatchConfig.Create(4, 4, 5));
            ClearBuildings(match);
            ToMain(match, 0);
            return match;
        }

        private static HexMath.Vertex Corner(MatchController m, ResourceType? specific)
        {
            var port = m.State.Ports.First(p => p.SpecificResource == specific);
            return VertexGraph.Canonicalize(port.Vertex);
        }

        [Test]
        public void BankBatch_FourToOne_EightForTwo()
        {
            var m = PlainMain();
            m.State.Seats[0].Hand = Cards(wood: 8);
            Assert.AreEqual(2, m.BankBatchCards(0, Cards(wood: 8)));
            Assert.AreEqual(-1, m.BankBatchCards(0, Cards(wood: 6)), "not whole batches");
            Assert.IsFalse(m.Apply(MatchCommand.BankBatch(0, Cards(wood: 8), Cards(stone: 3))).Ok, "8 at 4:1 buys 2");
            Assert.IsTrue(m.Apply(MatchCommand.BankBatch(0, Cards(wood: 8), Cards(stone: 1, wheat: 1))).Ok);
            Assert.AreEqual(Cards(wheat: 1, stone: 1), m.State.Seats[0].Hand);
            Assert.AreEqual(2, m.State.Seats[0].BankTradesThisTurn);
        }

        [Test]
        public void BankBatch_TwoToOneHarbour_FourForTwo_EightForFour()
        {
            var m = PlainMain();
            var port = m.State.Ports.First(p => p.SpecificResource.HasValue);
            var r = port.SpecificResource.Value;
            var other = MatchController.AllResources.First(x => x != r);
            m.State.Board.VertexBuildings[VertexGraph.Canonicalize(port.Vertex)] = (BuildingType.Settlement, MatchSeats.ToPlayerId(0));
            var give = Cards();
            give.Add(r, 8);
            m.State.Seats[0].Hand = give;
            var four = Cards();
            four.Add(r, 4);
            var two = Cards();
            two.Add(other, 2);
            Assert.IsTrue(m.Apply(MatchCommand.BankBatch(0, four, two)).Ok, "give 4 get 2 at 2:1");
            var get4 = Cards();
            get4.Add(other, 4);
            m.State.Seats[0].Hand = give;
            Assert.IsTrue(m.Apply(MatchCommand.BankBatch(0, give, get4)).Ok, "give 8 get 4 at 2:1");
            Assert.AreEqual(4, m.State.Seats[0].Hand[other]);
        }

        [Test]
        public void BankBatch_MixesRates_TwoAtTwoToOne_PlusThreeAtThreeToOne_ForTwoDifferentCards()
        {
            var m = PlainMain();
            var specific = m.State.Ports.First(p => p.SpecificResource.HasValue);
            var generic = m.State.Ports.First(p => p.IsGeneric);
            var r = specific.SpecificResource.Value;
            var other = MatchController.AllResources.First(x => x != r);
            var gets = MatchController.AllResources.Where(x => x != r && x != other).Take(2).ToList();
            m.State.Board.VertexBuildings[VertexGraph.Canonicalize(specific.Vertex)] = (BuildingType.Settlement, MatchSeats.ToPlayerId(0));
            m.State.Board.VertexBuildings[VertexGraph.Canonicalize(generic.Vertex)] = (BuildingType.Settlement, MatchSeats.ToPlayerId(0));
            var give = Cards();
            give.Add(r, 2);
            give.Add(other, 3);
            m.State.Seats[0].Hand = give;
            var get = Cards();
            get.Add(gets[0], 1);
            get.Add(gets[1], 1);
            Assert.AreEqual(2, m.BankBatchCards(0, give));
            Assert.IsTrue(m.Apply(MatchCommand.BankBatch(0, give, get)).Ok);
            Assert.AreEqual(get, m.State.Seats[0].Hand);
        }

        [Test]
        public void BankBatch_RejectsOverlapAndUnaffordable()
        {
            var m = PlainMain();
            m.State.Seats[0].Hand = Cards(wood: 4);
            Assert.IsFalse(m.Apply(MatchCommand.BankBatch(0, Cards(wood: 4), Cards(wood: 1))).Ok);
            Assert.IsFalse(m.Apply(MatchCommand.BankBatch(0, Cards(wood: 8), Cards(stone: 2))).Ok);
            m.State.Phase = MatchPhase.Roll;
            Assert.IsFalse(m.Apply(MatchCommand.BankBatch(0, Cards(wood: 4), Cards(stone: 1))).Ok);
        }

        [Test]
        public void BankBatch_RoundTripsThroughJson_AndIsDeterministic()
        {
            var cmd = MatchCommand.BankBatch(0, Cards(wood: 8), Cards(stone: 1, wheat: 1));
            var back = MatchCommand.FromJson(cmd.ToJson());
            Assert.AreEqual(MatchCommandType.BankTradeBatch, back.Type);
            Assert.AreEqual(cmd.Offer, back.Offer);
            Assert.AreEqual(cmd.Request, back.Request);
            var a = PlainMain();
            var b = PlainMain();
            a.State.Seats[0].Hand = Cards(wood: 8);
            b.State.Seats[0].Hand = Cards(wood: 8);
            Assert.IsTrue(a.Apply(cmd).Ok);
            Assert.IsTrue(b.Apply(back).Ok);
            Assert.AreEqual(a.State.ComputeHash(), b.State.ComputeHash());
        }

        [Test]
        public void Ai_UsesBatchedBankTrade_WhenOneSurplusCoversSeveralMissingCards()
        {
            var m = PlainMain();
            m.State.Seats[0].Hand = Cards(stone: 12);
            m.State.Phase = MatchPhase.Main;
            MatchCommand chosen = null;
            var seen = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 6 && chosen == null; i++)
            {
                var c = MatchAi.ChooseCommand(m);
                seen.Add(c.ToString());
                if (c.Type == MatchCommandType.BankTradeBatch) chosen = c;
                else if (c.Type == MatchCommandType.BankTrade) Assert.IsTrue(m.Apply(c).Ok);
                else break;
            }

            Assert.IsNotNull(chosen, "a 12-ore hand short a road (wood+brick) should batch: " + string.Join(" | ", seen));
            Assert.IsTrue(m.Apply(chosen).Ok);
        }
    }
}
