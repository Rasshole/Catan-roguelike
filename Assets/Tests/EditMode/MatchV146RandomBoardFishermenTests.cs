using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Core.Shop;
using NUnit.Framework;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// v146: every pass &amp; play match deals a new board (terrain, numbers and harbour types per seed, same seed =
    /// same board) and new pick offers; Fishermen of Catan follows the official rules (lake on 2/3/11/12, fish tokens
    /// 1–3 and the old boot, spend 2/3/4/5/7, the robber never on the lake).
    /// </summary>
    public class MatchV146RandomBoardFishermenTests
    {
        private static string Layout(BoardState b) =>
            string.Join(";", b.Tiles.Values.OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R)
                .Select(t => $"{t.Coord.Q},{t.Coord.R}:{(t.IsDesert ? "D" : t.Resource.ToString())}{t.NumberToken}"));

        private static string Ports(IEnumerable<PortDefinition> ports) =>
            string.Join(";", ports.Select(p => p.SpecificResource?.ToString() ?? "3:1"));

        private static List<MatchCommand> Play(MatchController match, int maxTurns)
        {
            var applied = new List<MatchCommand>();
            int guard = 0;
            while (!match.IsOver && match.State.TurnNumber <= maxTurns && guard++ < 200000)
            {
                var command = MatchRunner.PendingResponse(match) ?? MatchAi.ChooseCommand(match);
                var result = match.Apply(command);
                Assert.IsTrue(result.Ok, $"{command}: {result.Error}");
                applied.Add(command);
            }

            return applied;
        }

        private static MatchController Fishermen(int seed, MapSize map = MapSize.Large, int seats = 4)
        {
            var config = MatchConfig.Create(seats, 0, seed, map);
            config.Fishermen = true;
            return new MatchController(config);
        }

        private static void Produce(MatchController match, int roll) =>
            typeof(MatchController).GetMethod("ProduceFish", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(match, new object[] { roll });

        [Test]
        public void EveryMap_NewSeedNewBoard_SameSeedSameBoard_No68Neighbours(
            [Values(MapSize.Small, MapSize.Medium, MapSize.Large, MapSize.Extended, MapSize.Huge, MapSize.Islands)] MapSize map)
        {
            var layouts = new HashSet<string>();
            var portSets = new HashSet<string>();
            for (int seed = 1; seed <= 8; seed++)
            {
                var a = new MatchController(MatchConfig.Create(3, 0, seed, map));
                var b = new MatchController(MatchConfig.Create(3, 0, seed, map));
                Assert.AreEqual(Layout(a.State.Board), Layout(b.State.Board), "same seed, same board");
                Assert.AreEqual(Ports(a.State.Ports), Ports(b.State.Ports), "same seed, same harbours");
                layouts.Add(Layout(a.State.Board));
                portSets.Add(Ports(a.State.Ports));
                foreach (var t in a.State.Board.Tiles.Values.Where(t => t.NumberToken is 6 or 8))
                foreach (var d in HexCoord.Directions)
                    if (a.State.Board.TryGetTile(t.Coord + d, out var n))
                        Assert.IsFalse(n.NumberToken is 6 or 8, $"{map} seed {seed}: 6/8 next to each other at {t.Coord}");
            }

            Assert.GreaterOrEqual(layouts.Count, 6, $"{map}: boards differ between matches");
            Assert.GreaterOrEqual(portSets.Count, 3, $"{map}: harbour types are shuffled");
        }

        [Test]
        public void HarbourShuffle_IsMatchOnly_TheSinglePlayerBoardKeepsItsHarbours()
        {
            var plain = MapPresets.CreateBoard(MapSize.Large, 5);
            Assert.IsNull(plain.PortShuffleSeed);
            var match = MapPresets.CreateBoard(MapSize.Large, 5, new MapGenerationOptions(shuffleTerrain: true));
            Assert.AreEqual(5, match.PortShuffleSeed);
            var a = PortAccess.DiscoverPorts(match);
            Assert.AreEqual(a.Select(p => p.Vertex), PortAccess.DiscoverPorts(match).Select(p => p.Vertex), "positions fixed");
            Assert.AreEqual(Ports(a), Ports(PortAccess.DiscoverPorts(match)), "the 3D view and the rules agree");
            // The multiset of harbour types is unchanged by the shuffle.
            var unshuffled = match.WithoutShuffle();
            CollectionAssert.AreEquivalent(unshuffled.Select(p => p.SpecificResource?.ToString() ?? "g"),
                a.Select(p => p.SpecificResource?.ToString() ?? "g"));
        }

        [Test]
        public void PickOffers_DifferBetweenMatches()
        {
            var civs = new HashSet<string>();
            var traits = new HashSet<string>();
            var quests = new HashSet<string>();
            for (int seed = 1; seed <= 8; seed++)
            {
                var match = new MatchController(MatchConfig.Create(3, 0, seed, MapSize.Large, partyRules: true));
                civs.Add(string.Join(",", match.State.Current.CivOffer));
                int guard = 0;
                while (match.State.Phase != MatchPhase.QuestPick && guard++ < 500)
                    Assert.IsTrue(match.Apply(MatchRunner.PendingResponse(match) ?? MatchAi.ChooseCommand(match)).Ok);
                traits.Add(string.Join(",", match.State.Current.TraitOffer));
                quests.Add(string.Join(",", match.State.Current.QuestOffer));
            }

            Assert.GreaterOrEqual(civs.Count, 5, "leader offers vary");
            Assert.GreaterOrEqual(traits.Count, 5, "trait offers vary");
            Assert.GreaterOrEqual(quests.Count, 3, "quest offers vary");
        }

        [Test]
        public void Lake_ReplacesTheDesert_RobberStartsOffBoard_AndNeverGoesThere(
            [Values(MapSize.Large, MapSize.Extended, MapSize.Huge, MapSize.Islands)] MapSize map)
        {
            var match = Fishermen(3, map);
            var board = match.State.Board;
            Assert.IsTrue(board.LakeHex.HasValue, "a lake");
            var lake = board.Tiles[board.LakeHex.Value];
            Assert.IsTrue(lake.IsDesert && lake.IsFishTile);
            Assert.AreEqual(0, board.Tiles.Values.Count(t => t.IsDesert && !t.IsFishTile), "no desert left");
            Assert.IsNull(board.RobberTile, "the robber starts off the board");

            match.State.Phase = MatchPhase.MoveRobber;
            var r = match.Apply(MatchCommand.Robber(match.State.CurrentSeat, board.LakeHex.Value, -1));
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("lake", r.Error);
            var ai = MatchAi.ChooseRobber(match, match.State.CurrentSeat);
            Assert.AreNotEqual(board.LakeHex.Value, ai.Hex, "the AI never picks the lake");
        }

        [Test]
        public void Lake_FishesOn2_3_11_12_WithTokens_CityDrawsTwo()
        {
            var match = Fishermen(9);
            var board = match.State.Board;
            var corners = match.LakeCorners();
            Assert.AreEqual(6, corners.Length);
            board.VertexBuildings[corners[0]] = (BuildingType.Settlement, match.State.Seats[0].Owner);
            board.VertexBuildings[corners[3]] = (BuildingType.City, match.State.Seats[1].Owner);
            int bag = match.State.FishBag.Count;
            Assert.AreEqual(30, bag, "11×1 + 10×2 + 8×3 + the old boot");

            foreach (int roll in new[] { 4, 5, 6, 7, 8, 9, 10 })
            {
                if (board.SeaFishGrounds.Values.Any(g => g.NumberToken == roll))
                    continue;
                Produce(match, roll);
            }

            Assert.AreEqual(bag, match.State.FishBag.Count, "the lake is quiet on other numbers");
            foreach (int roll in MatchController.LakeNumbers)
            {
                int before = match.State.FishBag.Count;
                Produce(match, roll);
                Assert.AreEqual(before - 3, match.State.FishBag.Count, $"roll {roll}: settlement 1 + city 2 tokens");
            }

            int total = match.State.Seats[0].Fish + match.State.Seats[1].Fish;
            Assert.Greater(total, 0);
            Assert.LessOrEqual(match.State.Seats[0].Fish, 4 * 3, "tokens are worth 1–3");
            Assert.LessOrEqual(match.State.Seats[1].Fish, 8 * 3);
        }

        [Test]
        public void OldBoot_NeedsOneMoreVp_AndPassesToEqualOrHigher()
        {
            var match = Fishermen(4, seats: 3);
            var s = match.State;
            s.Phase = MatchPhase.Main;
            s.CurrentSeat = 0;
            s.OldBootSeat = 0;
            Assert.AreEqual(s.Config.VictoryPoints + 1, match.VictoryTarget(0));
            Assert.AreEqual(s.Config.VictoryPoints, match.VictoryTarget(1));
            // Seat 1 has fewer visible VP than seat 0 → can't take it; seat 2 has more → can.
            s.Seats[0].VictoryPoints = 3;
            s.Seats[1].VictoryPoints = 2;
            s.Seats[2].VictoryPoints = 4;
            Assert.IsFalse(match.CanPassOldBoot(0, 1));
            Assert.IsTrue(match.CanPassOldBoot(0, 2));
            Assert.IsFalse(match.Apply(MatchCommand.PassBoot(0, 1)).Ok);
            Assert.IsTrue(match.Apply(MatchCommand.PassBoot(0, 2)).Ok);
            Assert.AreEqual(2, s.OldBootSeat);
            Assert.AreEqual(s.Config.VictoryPoints + 1, match.VictoryTarget(2));
            var back = MatchCommand.FromJson(MatchCommand.PassBoot(0, 1).ToJson());
            Assert.AreEqual(MatchCommandType.PassOldBoot, back.Type);
        }

        [Test]
        public void SpendFish_2_3_4_5_7_DoWhatTheRulesSay()
        {
            var match = Fishermen(6, seats: 3);
            var s = match.State;
            Play(match, 2);
            Assume.That(!match.IsOver);
            s.CurrentSeat = 0;
            s.Phase = MatchPhase.Main;
            s.ActiveTrade = null;
            var me = s.Seats[0];

            // 2: robber off the board.
            s.Board.PlaceRobber(s.Board.Tiles.Values.First(t => !t.IsDesert).Coord);
            me.Fish = 1;
            Assert.IsFalse(match.CanSpendFish(0, 2));
            me.Fish = 20;
            Assert.IsTrue(match.Apply(MatchCommand.SpendFish(0, 2)).Ok);
            Assert.IsNull(s.Board.RobberTile);
            Assert.AreEqual(18, me.Fish);
            Assert.IsFalse(match.CanSpendFish(0, 2), "nothing to chase away now");

            // 3: steal one card.
            var h1 = s.Seats[1].Hand;
            h1.Add(ResourceType.Wood, 2);
            s.Seats[1].Hand = h1;
            int mine = me.Hand.Total, theirs = s.Seats[1].Hand.Total;
            Assert.IsFalse(match.Apply(MatchCommand.SpendFish(0, 3)).Ok, "needs a victim");
            Assert.IsTrue(match.Apply(MatchCommand.SpendFish(0, 3, 1)).Ok);
            Assert.AreEqual(mine + 1, me.Hand.Total);
            Assert.AreEqual(theirs - 1, s.Seats[1].Hand.Total);
            Assert.AreEqual(15, me.Fish);

            // 4: a card of your choice.
            int stone = me.Hand.Stone;
            Assert.IsTrue(match.Apply(MatchCommand.SpendFish(0, 4, card: ResourceType.Stone)).Ok);
            Assert.AreEqual(stone + 1, me.Hand.Stone);
            Assert.AreEqual(11, me.Fish);

            // 7: a development card.
            int dev = me.NewDevCards.Count, deck = s.DevDeck.Count;
            Assert.IsTrue(match.Apply(MatchCommand.SpendFish(0, 7)).Ok);
            Assert.AreEqual(dev + 1, me.NewDevCards.Count);
            Assert.AreEqual(deck - 1, s.DevDeck.Count);
            Assert.AreEqual(4, me.Fish);

            // 5: a free road (road-building phase with one piece).
            me.Fish = 5;
            if (match.CanSpendFish(0, 5))
            {
                Assert.IsTrue(match.Apply(MatchCommand.SpendFish(0, 5)).Ok);
                Assert.AreEqual(MatchPhase.RoadBuilding, s.Phase);
                Assert.AreEqual(1, s.FreeRoadsLeft);
                Assert.AreEqual(0, me.Fish);
                var road = match.ValidRoads(0).First();
                int roads = match.CountRoads(0);
                Assert.IsTrue(match.Apply(MatchCommand.AtEdge(MatchCommandType.BuildRoad, 0, road)).Ok);
                Assert.AreEqual(roads + 1, match.CountRoads(0));
                Assert.AreEqual(MatchPhase.Main, s.Phase);
            }

            Assert.IsFalse(match.Apply(MatchCommand.SpendFish(0, 6)).Ok, "only 2/3/4/5/7");
            Assert.IsFalse(match.Apply(MatchCommand.SpendFish(1, 2)).Ok, "not your turn");
        }

        [Test]
        public void AiGames_WithFishermen_FinishDeterministically_AndUseTheNewSpends()
        {
            var spends = new HashSet<int>();
            int boots = 0;
            for (int seed = 1; seed <= 8; seed++)
            {
                var match = Fishermen(seed);
                var commands = Play(match, 400);
                Assert.IsTrue(match.IsOver, $"seed {seed} finishes");
                foreach (var c in commands.Where(c => c.Type == MatchCommandType.SpendFish))
                    spends.Add(c.Pick);
                if (commands.Any(c => c.Type == MatchCommandType.FishForCard))
                    spends.Add(4);
                boots += commands.Count(c => c.Type == MatchCommandType.PassOldBoot);
                if (match.State.Winner is int w)
                    Assert.GreaterOrEqual(match.State.Seats[w].VictoryPoints, match.VictoryTarget(w));
            }

            TestContext.WriteLine($"spends used: {string.Join(",", spends.OrderBy(x => x))}; boot passes {boots}");
            Assert.GreaterOrEqual(spends.Count, 3, "the AI uses several fish actions");

            var x = Fishermen(77);
            var y = Fishermen(77);
            Play(x, 60);
            Play(y, 60);
            Assert.AreEqual(x.State.ComputeHash(), y.State.ComputeHash());
        }
    }

    internal static class BoardTestExt
    {
        /// <summary>The same board's harbours without the v146 shuffle (for comparing the type multiset).</summary>
        public static List<PortDefinition> WithoutShuffle(this BoardState board)
        {
            var seed = board.PortShuffleSeed;
            board.PortShuffleSeed = null;
            var ports = PortAccess.DiscoverPorts(board);
            board.PortShuffleSeed = seed;
            return ports;
        }
    }
}
