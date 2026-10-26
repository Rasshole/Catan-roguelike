using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// v145 Seafarers AI and board picks: the AI sails ship lines to the islets, moves its open ship, sends the
    /// pirate, spends Road Building on ships; clicks on the Islands board build, sail and pick the pirate.
    /// </summary>
    public class MatchShipsAiV145Tests
    {
        private static Edge E(Vertex a, Vertex b) => VertexGraph.GetEdgeBetween(a, b);

        /// <summary>Rival settlements on every free home spot near <paramref name="v"/>: roads lead nowhere.</summary>
        private static void HemIn(MatchController m, Vertex v, List<Vertex> path)
        {
            var b = m.State.Board;
            var rival = m.State.Seats[2].Owner;
            var near = b.SetupLandHexes.SelectMany(h => Enumerable.Range(0, 6).Select(c => VertexGraph.Canonicalize(new Vertex(h, c))))
                .Distinct().Where(u => !path.Contains(u) && VertexGraph.VertexDistance(v, u) <= 4)
                .OrderBy(u => VertexGraph.VertexDistance(v, u)).ThenBy(u => u.ToString()).ToList();
            foreach (var u in near)
                if (m.Placement.CanPlaceSettlement(b, u, rival, true))
                    b.VertexBuildings[u] = (BuildingType.Settlement, rival);
        }

        [Test]
        public void IslandsAi_FinishesGames_SailsToTheIslets_MovesShips_AndSendsThePirate()
        {
            var islets = new HashSet<HexCoord>(MapPresets.IslandsIsletCoords);
            int built = 0, moved = 0, pirate = 0, isletBuildings = 0, sailingGames = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var m = new MatchController(MatchConfig.Create(3 + seed % 2, 0, 9000 + seed, MapSize.Islands, seed % 3 == 0));
                MatchRunner.RunToEnd(m);
                Assert.IsTrue(m.IsOver, $"seed {seed} finishes");
                Assert.Less(m.State.TurnNumber, 250, $"seed {seed} doesn't drag");
                var log = m.CommandLog;
                int b = log.Count(c => c.Type == MatchCommandType.BuildShip);
                built += b;
                sailingGames += b > 0 ? 1 : 0;
                moved += log.Count(c => c.Type == MatchCommandType.MoveShip);
                pirate += log.Count(c => c.Type == MatchCommandType.MovePirate);
                isletBuildings += m.State.Board.VertexBuildings.Keys.Count(v =>
                    VertexGraph.GetHexesForVertex(v).Any(islets.Contains)
                    && !VertexGraph.GetHexesForVertex(v).Any(m.State.Board.SetupLandHexes.Contains));
                Assert.LessOrEqual(m.State.Seats.Max(s => m.CountShips(s.Index)), MatchController.MaxShips);
            }

            TestContext.Progress.WriteLine($"ships {built} in {sailingGames} games, moved {moved}, pirate {pirate}, islet buildings {isletBuildings}");
            Assert.GreaterOrEqual(sailingGames, 4, "the AI takes to the sea in a fair share of games");
            Assert.Greater(isletBuildings, 0, "ship lines found settlements on the islets");
            Assert.Greater(moved, 0, "the AI sails its open ship");
            Assert.Greater(pirate, 0, "the AI sends the pirate");
        }

        [Test]
        public void IslandsAi_IsQuick()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int seed = 1; seed <= 4; seed++)
                MatchRunner.RunToEnd(new MatchController(MatchConfig.Create(4, 0, 9100 + seed, MapSize.Islands, false)));
            Assert.Less(sw.ElapsedMilliseconds, 20000, "4 Islands AI games");
        }

        [Test]
        public void SettlementDistanceRule_MatchesTheFullDistanceCheck()
        {
            var m = MatchShipsV145Tests.Islands(4, 11);
            var b = m.State.Board;
            var owner = m.State.Seats[0].Owner;
            var all = b.Tiles.Keys.Concat(b.SeaHexes)
                .SelectMany(h => Enumerable.Range(0, 6).Select(c => VertexGraph.Canonicalize(new Vertex(h, c)))).Distinct().ToList();
            Assert.IsNotEmpty(b.VertexBuildings);
            foreach (var v in all)
            {
                bool far = !b.VertexBuildings.ContainsKey(v) && b.VertexBuildings.Keys.All(o => VertexGraph.VertexDistance(v, o) >= 2)
                           && VertexGraph.GetHexesForVertex(v).Any(b.Tiles.ContainsKey);
                Assert.AreEqual(far, m.Placement.CanPlaceSettlement(b, v, owner, true), $"{v}");
            }
        }

        [Test]
        public void Ai_StartsAShipLine_TowardAnIslet_AndItsScoreRisesAsItSails()
        {
            var m = MatchShipsV145Tests.EmptyIslands();
            var b = m.State.Board;
            var v = MatchShipsV145Tests.StraitCorner(b);
            b.VertexBuildings[v] = (BuildingType.Settlement, m.State.Seats[0].Owner);
            var path = MatchShipsV145Tests.SeaPathToIslet(b, v);
            Assert.IsNotNull(path);

            var best = MatchAi.BestShip(m, 0, out int score);
            Assert.IsTrue(best.HasValue);
            Assert.Greater(score, 0);
            Assert.IsTrue(best.Value.A.Equals(v) || best.Value.B.Equals(v), "the ship leaves the settlement");

            m.State.Seats[0].Hand = new ResourceBundle { Wood = 1, Sheep = 1 };
            HemIn(m, v, path);
            var cmd = MatchAi.ChooseCommand(m);
            Assert.AreEqual(MatchCommandType.BuildShip, cmd.Type, "no road leads to a free spot: sail");
            Assert.IsTrue(m.Apply(cmd).Ok);

            m.State.ShipsBuiltThisTurn.Clear();
            var next = MatchAi.BestShip(m, 0, out int nextScore);
            Assert.IsTrue(next.HasValue);
            Assert.Greater(nextScore, score, "one edge closer to the islet");
        }

        [Test]
        public void Ai_MovesAStrayShip_TowardTheIslet()
        {
            var m = MatchShipsV145Tests.EmptyIslands();
            var b = m.State.Board;
            var me = m.State.Seats[0].Owner;
            var v = MatchShipsV145Tests.StraitCorner(b);
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            var path = MatchShipsV145Tests.SeaPathToIslet(b, v);
            // The worst ship spot off the settlement: leads nowhere near as well as the path's first edge.
            var stray = m.ValidShips(0).OrderBy(e => MatchAi.ShipScore(m, e, 0)).First();
            Assert.Less(MatchAi.ShipScore(m, stray, 0), MatchAi.ShipScore(m, E(path[0], path[1]), 0));
            b.Ships[stray] = me;

            var move = MatchAi.ChooseShipMove(m, 0);
            Assert.IsNotNull(move, "the stray ship sails");
            Assert.AreEqual(MatchCommandType.MoveShip, move.Type);
            Assert.IsTrue(m.Apply(move).Ok);
            Assert.IsNull(MatchAi.ChooseShipMove(m, 0), "once a turn");
        }

        [Test]
        public void Ai_SendsThePirate_OntoTheLeadersOpenShip()
        {
            var m = MatchShipsV145Tests.EmptyIslands();
            var b = m.State.Board;
            var rival = m.State.Seats[1];
            var v = MatchShipsV145Tests.StraitCorner(b);
            var path = MatchShipsV145Tests.SeaPathToIslet(b, v);
            // Seat 1 (the leader) sails toward an islet: parking the pirate on its open ship beats the robber.
            b.VertexBuildings[v] = (BuildingType.Settlement, rival.Owner);
            b.Ships[E(path[0], path[1])] = rival.Owner;
            b.Ships[E(path[1], path[2])] = rival.Owner;
            rival.Hand = new ResourceBundle { Stone = 2 };
            m.State.Phase = MatchPhase.MoveRobber;
            m.State.ResumePhase = MatchPhase.Main;

            var cmd = MatchAi.ChooseRobber(m, 0);
            Assert.AreEqual(MatchCommandType.MovePirate, cmd.Type, cmd.ToString());
            Assert.IsTrue(ShipRules.HexEdges(cmd.Hex).Contains(ShipRules.Normalize(E(path[1], path[2]))), "on the open ship");
            Assert.AreEqual(1, cmd.Victim);
            Assert.IsTrue(m.Apply(cmd).Ok);
            Assert.AreEqual(cmd.Hex, b.PirateHex);
            Assert.IsFalse(m.MovableShips(1).Any(), "the leader's ship is stuck");
        }

        [Test]
        public void Ai_RoadBuilding_OnTheIslands_PlacesShipsWhenTheyLeadFurther()
        {
            var m = MatchShipsV145Tests.EmptyIslands();
            var b = m.State.Board;
            var v = MatchShipsV145Tests.StraitCorner(b);
            b.VertexBuildings[v] = (BuildingType.Settlement, m.State.Seats[0].Owner);
            var path = MatchShipsV145Tests.SeaPathToIslet(b, v);
            HemIn(m, v, path);
            m.State.Seats[0].DevCards.Add(MatchDevCard.RoadBuilding);
            m.State.TurnNumber = 5;
            var play = MatchCommand.Simple(MatchCommandType.PlayRoadBuilding, 0);
            Assert.IsTrue(m.Apply(play).Ok, "play Road Building");
            Assert.AreEqual(MatchPhase.RoadBuilding, m.State.Phase);
            var types = new List<MatchCommandType>();
            while (m.State.Phase == MatchPhase.RoadBuilding)
            {
                var c = MatchAi.ChooseCommand(m);
                types.Add(c.Type);
                Assert.IsTrue(m.Apply(c).Ok, c.ToString());
            }

            CollectionAssert.AreEqual(new[] { MatchCommandType.BuildShip, MatchCommandType.BuildShip }, types);
            Assert.AreEqual(2, m.CountShips(0));
        }

        [Test]
        public void BoardPick_BuildsAShip_SailsIt_AndPicksThePirate()
        {
            var m = MatchShipsV145Tests.EmptyIslands();
            var b = m.State.Board;
            var me = m.State.Seats[0].Owner;
            var v = MatchShipsV145Tests.StraitCorner(b);
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            var path = MatchShipsV145Tests.SeaPathToIslet(b, v);
            var first = ShipRules.Normalize(E(path[0], path[1]));
            m.State.Seats[0].Hand = new ResourceBundle { Wood = 1, Sheep = 1 };

            var (x, z) = HexMath.EdgeMidpoint(first);
            Assert.IsTrue(MatchBoardPick.Targets(m, MatchBuildMode.Ship).edges.Contains(first));
            var build = MatchBoardPick.Pick(m, MatchBuildMode.Ship, x, z, 1f);
            Assert.AreEqual(MatchCommandType.BuildShip, build.Type);
            Assert.AreEqual(first, ShipRules.Normalize(build.Edge));
            Assert.IsTrue(m.Apply(build).Ok);

            // Next turn: pick the ship, then a spot to sail it to.
            m.State.ShipsBuiltThisTurn.Clear();
            Assert.AreEqual(first, MatchBoardPick.PickMovableShip(m, x, z, 1f));
            var to = m.ShipMoveTargets(0, first).First();
            var (tx, tz) = HexMath.EdgeMidpoint(to);
            var sail = MatchBoardPick.Pick(m, MatchBuildMode.MoveShip, tx, tz, 1f, first);
            Assert.AreEqual(MatchCommandType.MoveShip, sail.Type);
            Assert.IsTrue(m.Apply(sail).Ok);
            Assert.IsTrue(b.Ships.ContainsKey(ShipRules.Normalize(to)));

            // A 7: a click on a sea hex moves the pirate (nobody to rob there).
            m.State.Phase = MatchPhase.MoveRobber;
            m.State.ResumePhase = MatchPhase.Main;
            var sea = b.SeaHexes.Where(h => !ShipRules.HexEdges(h).Any(b.Ships.ContainsKey)).OrderBy(h => h.Q).ThenBy(h => h.R).First();
            var (sx, sz) = HexMath.ToWorldPosition(sea);
            var pick = MatchBoardPick.Pick(m, MatchBuildMode.Auto, sx, sz, 1f);
            Assert.AreEqual(MatchCommandType.MovePirate, pick.Type);
            Assert.AreEqual(sea, pick.Hex);
            Assert.IsTrue(m.Apply(pick).Ok);
            Assert.AreEqual(sea, b.PirateHex);
        }
    }
}
