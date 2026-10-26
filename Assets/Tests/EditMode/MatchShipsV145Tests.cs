using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Match;
using CatanRoguelike.Core.Victory;
using NUnit.Framework;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Tests
{
    /// <summary>
    /// v145 Seafarers: the Islands map, ship placement and connection (roads and ships only meet at your own
    /// settlement or city), moving the open ship, the pirate, Longest Trade Route, Road Building / Road Crew /
    /// Five Harvests with ships — and classic maps unchanged.
    /// </summary>
    public class MatchShipsV145Tests
    {
        private static MatchController Islands(int seats = 3, int seed = 145, bool party = false)
        {
            var match = new MatchController(MatchConfig.Create(seats, 0, seed, MapSize.Islands, party));
            while (match.IsSetup || match.State.IsPicking)
                Assert.IsTrue(match.Apply(MatchAi.ChooseCommand(match)).Ok);
            return match;
        }

        /// <summary>An empty Islands board at seat 0's main phase.</summary>
        private static MatchController EmptyIslands(bool party = false)
        {
            var m = Islands(party: party);
            var b = m.State.Board;
            b.VertexBuildings.Clear();
            b.Roads.Clear();
            b.Ships.Clear();
            m.State.CurrentSeat = 0;
            m.State.Phase = MatchPhase.Main;
            m.State.ShipsBuiltThisTurn.Clear();
            m.State.ShipMovedThisTurn = false;
            foreach (var s in m.State.Seats)
                s.Hand = new ResourceBundle();
            return m;
        }

        private static PlayerId Owner(MatchController m, int seat) => m.State.Seats[seat].Owner;

        private static IEnumerable<Vertex> AllVertices(BoardState b)
        {
            var seen = new HashSet<Vertex>();
            foreach (var hex in b.Tiles.Keys.Concat(b.SeaHexes))
                for (int c = 0; c < 6; c++)
                {
                    var v = VertexGraph.Canonicalize(new Vertex(hex, c));
                    if (seen.Add(v))
                        yield return v;
                }
        }

        /// <summary>A home-island coast corner on the strait (q = 1): touches home land and sea.</summary>
        private static Vertex StraitCorner(BoardState b) =>
            AllVertices(b).Where(v =>
                {
                    var hexes = VertexGraph.GetHexesForVertex(v).ToList();
                    return hexes.Any(h => b.SetupLandHexes.Contains(h)) && hexes.Any(h => b.SeaHexes.Contains(h) && h.Q == 1);
                })
                .OrderBy(v => v.Hex.R).ThenBy(v => v.Hex.Q).ThenBy(v => v.CornerIndex).First();

        private static Edge E(Vertex a, Vertex b) => VertexGraph.GetEdgeBetween(a, b);

        /// <summary>
        /// Shortest corner path over ship edges from <paramref name="from"/> to a corner on an islet that is at
        /// least <paramref name="minEdges"/> ships away.
        /// </summary>
        private static List<Vertex> SeaPathToIslet(BoardState b, Vertex from, int minEdges = 3)
        {
            var islets = new HashSet<HexCoord>(MapPresets.IslandsIsletCoords);
            var prev = new Dictionary<Vertex, Vertex> { [from] = from };
            var dist = new Dictionary<Vertex, int> { [from] = 0 };
            var queue = new Queue<Vertex>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                if (dist[v] >= minEdges && VertexGraph.GetHexesForVertex(v).Any(islets.Contains))
                {
                    var path = new List<Vertex> { v };
                    while (!path[0].Equals(from))
                        path.Insert(0, prev[path[0]]);
                    return path;
                }

                foreach (var n in VertexGraph.GetAdjacentVertices(v))
                    if (!prev.ContainsKey(n) && ShipRules.IsShipEdge(b, E(v, n)))
                    {
                        prev[n] = v;
                        dist[n] = dist[v] + 1;
                        queue.Enqueue(n);
                    }
            }

            return null;
        }

        [Test]
        public void IslandsMap_HasAHomeIsland_TwoIslets_AndPlayableSea()
        {
            var board = MapPresets.CreateBoard(MapSize.Islands, 7, new MapGenerationOptions(shuffleTerrain: true));
            Assert.AreEqual(29, board.Tiles.Count);
            Assert.AreEqual(29, MapPresets.GetHexCount(MapSize.Islands));
            Assert.AreEqual(22, board.SetupLandHexes.Count);
            Assert.AreEqual(61 - 29, board.SeaHexes.Count);
            Assert.IsTrue(board.IsSeafarers);
            Assert.IsFalse(board.SeaHexes.Any(board.Tiles.ContainsKey));
            Assert.AreEqual(1, board.Tiles.Values.Count(t => t.IsDesert));
            Assert.IsTrue(board.Tiles[MapPresets.IslandsDesert].IsDesert);
            Assert.AreEqual(MapPresets.IslandsDesert, board.RobberTile);
            Assert.IsNull(board.PirateHex);
            Assert.IsTrue(board.Tiles.Values.Where(t => !t.IsDesert).All(t => t.NumberToken.HasValue));
            foreach (var islet in MapPresets.IslandsIsletCoords)
                Assert.IsFalse(HexCoord.Directions.Any(d => board.SetupLandHexes.Contains(islet + d)), $"{islet} is across the sea");

            foreach (MapSize size in new[] { MapSize.Small, MapSize.Medium, MapSize.Large, MapSize.Extended, MapSize.Huge })
            {
                var classic = MapPresets.CreateBoard(size, 7, new MapGenerationOptions(shuffleTerrain: true));
                Assert.IsFalse(classic.IsSeafarers, $"{size}: no ships on classic maps");
                Assert.AreEqual((int)size, classic.Tiles.Count);
            }
        }

        [Test]
        public void Setup_StartsOnTheHomeIsland()
        {
            var m = Islands(4, 3);
            Assert.AreEqual(8, m.State.Board.VertexBuildings.Count);
            foreach (var v in m.State.Board.VertexBuildings.Keys)
                Assert.IsTrue(VertexGraph.GetHexesForVertex(v).Where(m.State.Board.Tiles.ContainsKey)
                    .All(m.State.Board.SetupLandHexes.Contains), $"{v} is on the home island");
        }

        [Test]
        public void Ships_GoOnSeaEdges_FromYourOwnBuildingOrShip()
        {
            var m = EmptyIslands();
            var b = m.State.Board;
            var me = Owner(m, 0);
            var v = StraitCorner(b);
            Assert.IsEmpty(m.ValidShips(0), "no building, no ship");
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            var ships = m.ValidShips(0).ToList();
            Assert.IsNotEmpty(ships);
            foreach (var e in ships)
            {
                Assert.IsTrue(ShipRules.EdgeHexes(e).Any(b.SeaHexes.Contains), $"{e} borders the sea");
                Assert.IsTrue(e.A.Equals(v) || e.B.Equals(v), "joins the settlement");
            }

            var inland = VertexGraph.GetAdjacentVertices(v).Select(n => E(v, n))
                .FirstOrDefault(e => ShipRules.EdgeHexes(e).All(b.Tiles.ContainsKey));
            if (!inland.Equals(default(Edge)))
                Assert.IsFalse(ShipRules.CanPlaceShip(b, inland, me), "an inland edge takes roads only");

            var path = SeaPathToIslet(b, v);
            Assert.IsNotNull(path);
            var first = E(path[0], path[1]);
            Assert.IsFalse(m.Apply(MatchCommand.Ship(0, first)).Ok, "can't afford");
            m.State.Seats[0].Hand = new ResourceBundle { Wood = 1, Sheep = 1, Brick = 1 };
            Assert.IsTrue(m.Apply(MatchCommand.Ship(0, first)).Ok);
            Assert.AreEqual(new ResourceBundle { Brick = 1 }, m.State.Seats[0].Hand, "a ship costs 1 Lumber + 1 Wool");
            Assert.AreEqual(me, b.Ships[first]);
            Assert.AreEqual(1, m.CountShips(0));
            Assert.IsFalse(m.Placement.CanPlaceRoad(b, first, me), "the edge is taken by the ship");

            // The ship's open end extends ships, not roads.
            var tip = path[1];
            Assert.IsTrue(ShipRules.CanPlaceShip(b, E(tip, path[2]), me));
            foreach (var n in VertexGraph.GetAdjacentVertices(tip))
                if (ShipRules.BordersLand(b, E(tip, n)) && !E(tip, n).Equals(first))
                    Assert.IsFalse(m.Placement.CanPlaceRoad(b, E(tip, n), me), "a road can't hang off a ship");

            // A road end (no building) doesn't launch a ship either.
            b.Ships.Clear();
            b.VertexBuildings.Remove(v);
            var w = VertexGraph.GetAdjacentVertices(v).First(n => ShipRules.BordersLand(b, E(v, n)));
            b.Roads[E(v, w)] = me;
            Assert.IsFalse(ShipRules.CanPlaceShip(b, first, me), "roads and ships only meet at a settlement or city");
            b.Roads.Clear();

            // A rival building on the corner cuts the line.
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            b.Ships[first] = me;
            b.VertexBuildings[tip] = (BuildingType.Settlement, Owner(m, 1));
            Assert.IsFalse(ShipRules.CanPlaceShip(b, E(tip, path[2]), me));
        }

        [Test]
        public void ShipLine_ReachesAnIslet_AndFoundsASettlementThere()
        {
            var m = EmptyIslands();
            var b = m.State.Board;
            var me = Owner(m, 0);
            var v = StraitCorner(b);
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            var path = SeaPathToIslet(b, v);
            m.State.Seats[0].Hand = new ResourceBundle { Wood = 20, Sheep = 20, Brick = 1, Wheat = 1 };
            for (int i = 0; i + 1 < path.Count; i++)
                Assert.IsTrue(m.Apply(MatchCommand.Ship(0, E(path[i], path[i + 1]))).Ok, $"ship {i + 1}");
            var landing = path[path.Count - 1];
            Assert.GreaterOrEqual(VertexGraph.VertexDistance(v, landing), 2);
            CollectionAssert.Contains(m.ValidSettlements(0).ToList(), landing, "a ship line carries settlers");
            Assert.IsTrue(m.Apply(MatchCommand.AtVertex(MatchCommandType.BuildSettlement, 0, landing)).Ok);
        }

        [Test]
        public void MoveShip_OnlyTheOpenEnd_OncePerTurn_NotBuiltThisTurn()
        {
            var m = EmptyIslands();
            var b = m.State.Board;
            var me = Owner(m, 0);
            var v = StraitCorner(b);
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            var path = SeaPathToIslet(b, v);
            Assert.GreaterOrEqual(path.Count, 3);
            m.State.Seats[0].Hand = new ResourceBundle { Wood = 2, Sheep = 2 };
            var s1 = E(path[0], path[1]);
            var s2 = E(path[1], path[2]);
            Assert.IsTrue(m.Apply(MatchCommand.Ship(0, s1)).Ok);
            Assert.IsTrue(m.Apply(MatchCommand.Ship(0, s2)).Ok);
            Assert.IsEmpty(m.MovableShips(0), "ships built this turn stay put");
            var target = m.ShipMoveTargets(0, s2).First();
            StringAssert.Contains("this turn", m.Apply(MatchCommand.MoveShipTo(0, s2, target)).Error);

            m.State.ShipsBuiltThisTurn.Clear(); // next turn
            CollectionAssert.AreEqual(new[] { s2 }, m.MovableShips(0).ToList(), "only the open end moves");
            StringAssert.Contains("open end", m.Apply(MatchCommand.MoveShipTo(0, s1, target)).Error);
            Assert.IsFalse(m.Apply(MatchCommand.MoveShipTo(0, s2, s2)).Ok, "must go somewhere else");
            var far = E(path[path.Count - 2], path[path.Count - 1]);
            if (path.Count > 3)
                Assert.IsFalse(m.Apply(MatchCommand.MoveShipTo(0, s2, far)).Ok, "must stay connected");
            Assert.IsTrue(m.Apply(MatchCommand.MoveShipTo(0, s2, target)).Ok);
            Assert.IsFalse(b.Ships.ContainsKey(s2));
            Assert.AreEqual(me, b.Ships[ShipRules.Normalize(target)]);
            Assert.IsTrue(m.State.ShipMovedThisTurn);
            var back = m.ShipMoveTargets(0, target).FirstOrDefault();
            StringAssert.Contains("already moved", m.Apply(MatchCommand.MoveShipTo(0, target, back)).Error);

            Assert.IsTrue(m.Apply(MatchCommand.Simple(MatchCommandType.EndTurn, 0)).Ok);
            Assert.IsFalse(m.State.ShipMovedThisTurn, "the move resets each turn");
            Assert.IsEmpty(m.State.ShipsBuiltThisTurn);
        }

        [Test]
        public void MoveShip_AClosedRouteCannotMove()
        {
            var m = EmptyIslands();
            var b = m.State.Board;
            var me = Owner(m, 0);
            var v = StraitCorner(b);
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            var path = SeaPathToIslet(b, v);
            for (int i = 0; i + 1 < path.Count; i++)
                b.Ships[E(path[i], path[i + 1])] = me;
            var last = E(path[path.Count - 2], path[path.Count - 1]);
            CollectionAssert.Contains(m.MovableShips(0).ToList(), last, "open while the islet end is empty");
            b.VertexBuildings[path[path.Count - 1]] = (BuildingType.Settlement, me);
            Assert.IsEmpty(m.MovableShips(0), "both ends anchored: the route is closed");
            Assert.IsFalse(m.Apply(MatchCommand.MoveShipTo(0, last, m.ShipMoveTargets(0, last).FirstOrDefault())).Ok);
        }

        [Test]
        public void Pirate_GoesOnSea_StealsFromShips_AndBlocksItsEdges()
        {
            var m = EmptyIslands(party: true);
            var b = m.State.Board;
            var me = Owner(m, 0);
            var v = StraitCorner(b);
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            var path = SeaPathToIslet(b, v);
            var rivalShip = E(path[1], path[2]);
            b.Ships[rivalShip] = Owner(m, 1);
            var sea = ShipRules.EdgeHexes(rivalShip).First(b.SeaHexes.Contains);
            m.State.Seats[1].Hand = new ResourceBundle { Wheat = 1 };
            m.State.Seats[1].HasTakenFirstTurn = true;

            m.State.Phase = MatchPhase.MoveRobber;
            m.State.ResumePhase = MatchPhase.Main;
            StringAssert.Contains("sea hex", m.Apply(MatchCommand.Pirate(0, MapPresets.IslandsDesert, -1)).Error);
            CollectionAssert.AreEqual(new[] { 1 }, m.PirateVictims(0, sea).ToList());
            StringAssert.Contains("Choose", m.Apply(MatchCommand.Pirate(0, sea, -1)).Error);

            // Head Start protection covers the pirate too.
            m.State.Seats[1].Trait = MatchTraitId.HeadStart;
            m.State.Seats[1].HasTakenFirstTurn = false;
            Assert.IsEmpty(m.PirateVictims(0, sea), "Head Start: the pirate can't steal either");
            m.State.Seats[1].Trait = MatchTraitId.None;

            Assert.IsTrue(m.Apply(MatchCommand.Pirate(0, sea, 1)).Ok);
            Assert.AreEqual(sea, b.PirateHex);
            Assert.AreEqual(0, m.State.Seats[1].Hand.Total);
            Assert.AreEqual(1, m.State.Seats[0].Hand.Wheat, "stole the Grain");
            Assert.AreEqual(MatchPhase.Main, m.State.Phase);
            Assert.AreEqual(MapPresets.IslandsDesert, b.RobberTile, "the robber stays");

            // Blocked: no ship on the pirate's edges, and a ship there can't sail away.
            m.State.Seats[0].Hand = new ResourceBundle { Wood = 1, Sheep = 1 };
            foreach (var e in ShipRules.HexEdges(sea))
                Assert.IsFalse(ShipRules.CanPlaceShip(b, e, me), $"{e} next to the pirate");
            Assert.IsFalse(m.ValidShips(0).Any(e => ShipRules.EdgeHexes(e).Contains(sea)));
            m.State.CurrentSeat = 1;
            Assert.IsEmpty(m.MovableShips(1), "the pirate pins the rival ship");

            m.State.CurrentSeat = 0;
            m.State.Phase = MatchPhase.MoveRobber;
            StringAssert.Contains("different", m.Apply(MatchCommand.Pirate(0, sea, -1)).Error);
            var land = b.Tiles.Values.First(t => !t.IsDesert && t.Coord != b.RobberTile).Coord;
            Assert.IsTrue(m.Apply(MatchCommand.Robber(0, land, -1)).Ok, "the robber is still an option");
        }

        [Test]
        public void ClassicMaps_HaveNoShipsOrPirate()
        {
            var m = new MatchController(MatchConfig.Create(3, 0, 5, MapSize.Large));
            while (m.IsSetup)
                Assert.IsTrue(m.Apply(MatchAi.ChooseCommand(m)).Ok);
            m.State.Phase = MatchPhase.Main;
            m.State.Current.Hand = new ResourceBundle { Wood = 5, Sheep = 5 };
            Assert.IsFalse(m.SeafarersOn);
            Assert.IsEmpty(m.ValidShips(m.State.CurrentSeat));
            var anyEdge = m.ValidRoads(m.State.CurrentSeat).First();
            Assert.IsFalse(m.Apply(MatchCommand.Ship(m.State.CurrentSeat, anyEdge)).Ok);
            m.State.Phase = MatchPhase.MoveRobber;
            Assert.IsFalse(m.Apply(MatchCommand.Pirate(m.State.CurrentSeat, new HexCoord(5, 0), -1)).Ok);
        }

        [Test]
        public void TradeRoute_RoadsAndShipsJoinOnlyAtYourOwnBuilding()
        {
            var m = EmptyIslands();
            var b = m.State.Board;
            var me = Owner(m, 0);
            var v = StraitCorner(b);
            var path = SeaPathToIslet(b, v, 4);
            for (int i = 0; i < 3; i++)
                b.Ships[E(path[i], path[i + 1])] = me;
            // Three roads along the land from v (depth-first so a dead end can back off).
            var road = new List<Vertex> { v };
            bool Grow()
            {
                if (road.Count == 4) return true;
                var cur = road[road.Count - 1];
                foreach (var n in VertexGraph.GetAdjacentVertices(cur))
                {
                    if (road.Contains(n) || path.Contains(n)) continue;
                    var e = E(cur, n);
                    if (!ShipRules.BordersLand(b, e) || b.Ships.ContainsKey(e)) continue;
                    road.Add(n);
                    if (Grow()) return true;
                    road.RemoveAt(road.Count - 1);
                }
                return false;
            }
            Assert.IsTrue(Grow(), "a 3-road line from the strait corner");
            for (int i = 0; i < 3; i++)
                b.Roads[E(road[i], road[i + 1])] = me;

            Assert.AreEqual(3, RouteCalculator.LongestRoadLength(b, me));
            Assert.AreEqual(3, RouteCalculator.LongestTradeRouteLength(b, me), "no building at the joint: two lines of 3");
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            Assert.AreEqual(6, RouteCalculator.LongestTradeRouteLength(b, me), "own settlement joins road and ships");
            b.VertexBuildings[v] = (BuildingType.Settlement, Owner(m, 1));
            Assert.AreEqual(3, RouteCalculator.LongestTradeRouteLength(b, me), "a rival building cuts it");

            b.VertexBuildings[v] = (BuildingType.City, me);
            b.Ships[E(path[3], path[path.Count > 4 ? 4 : 3])] = me;
            int six = RouteCalculator.LongestTradeRouteLength(b, me);
            Assert.GreaterOrEqual(six, 6);
            m.RecomputeScores();
            Assert.AreEqual(0, m.State.LongestRoadSeat, "Longest Trade Route (5+) counts ships");
            StringAssert.Contains("Longest Trade Route", string.Join("\n", m.State.Log));
        }

        [Test]
        public void TradeRoute_WithoutShips_IsTheLongestRoad()
        {
            var m = new MatchController(MatchConfig.Create(4, 0, 77, MapSize.Large));
            int guard = 0;
            while (!m.IsOver && m.State.Round < 8 && guard++ < 4000)
                Assert.IsTrue(m.Apply(MatchAi.ChooseCommand(m)).Ok);
            foreach (var s in m.State.Seats)
                Assert.AreEqual(RouteCalculator.LongestRoadLength(m.State.Board, s.Owner),
                    RouteCalculator.LongestTradeRouteLength(m.State.Board, s.Owner));
        }

        [Test]
        public void RoadBuilding_TakesRoadsAndShipsMixed_AndRoadCrewCountsShips()
        {
            var m = EmptyIslands();
            var b = m.State.Board;
            var me = Owner(m, 0);
            var v = StraitCorner(b);
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            var path = SeaPathToIslet(b, v);
            var seat = m.State.Seats[0];
            seat.DevCards.Add(MatchDevCard.RoadBuilding);
            Assert.IsTrue(m.Apply(MatchCommand.Simple(MatchCommandType.PlayRoadBuilding, 0)).Ok);
            Assert.AreEqual(MatchPhase.RoadBuilding, m.State.Phase);
            Assert.AreEqual(2, m.State.FreeRoadsLeft);
            Assert.IsTrue(m.Apply(MatchCommand.Ship(0, E(path[0], path[1]))).Ok, "a free ship");
            Assert.AreEqual(MatchPhase.RoadBuilding, m.State.Phase);
            var roadEdge = m.ValidRoads(0).First();
            Assert.IsTrue(m.Apply(MatchCommand.AtEdge(MatchCommandType.BuildRoad, 0, roadEdge)).Ok, "and a free road");
            Assert.AreEqual(MatchPhase.Main, m.State.Phase);
            Assert.AreEqual(0, seat.Hand.Total, "both pieces were free");
            Assert.AreEqual(1, m.CountShips(0));
            Assert.AreEqual(1, m.CountRoads(0));

            seat.Trait = MatchTraitId.RoadCrew;
            Assert.AreEqual(2, m.RoadCrewPiecesBuilt(0), "a road and a ship");
            Assert.IsTrue(m.IsNextRoadCrewPieceFree(0));
            Assert.AreEqual(0, m.ShipCostFor(0).Total, "the third piece — a ship — is free");
            Assert.IsTrue(m.Apply(MatchCommand.Ship(0, E(path[1], path[2]))).Ok);
            Assert.AreEqual(MatchController.ShipCost, m.ShipCostFor(0), "the fourth costs Lumber + Wool");
        }

        [Test]
        public void FiveHarvests_ShipLinesJoinTheNetwork()
        {
            var m = EmptyIslands();
            var b = m.State.Board;
            var me = Owner(m, 0);
            var v = StraitCorner(b);
            var path = SeaPathToIslet(b, v);
            var landing = path[path.Count - 1];
            b.VertexBuildings[v] = (BuildingType.Settlement, me);
            b.VertexBuildings[landing] = (BuildingType.Settlement, me);
            HashSet<ResourceType> Res(Vertex x) => new(VertexGraph.GetHexesForVertex(x)
                .Where(h => b.Tiles.TryGetValue(h, out var t) && !t.IsDesert).Select(h => b.Tiles[h].Resource));
            int apart = System.Math.Max(Res(v).Count, Res(landing).Count);
            var union = Res(v);
            union.UnionWith(Res(landing));
            Assert.AreEqual(apart, MatchPartyLibrary.ConnectedDistinctResources(m, 0), "not joined yet");
            for (int i = 0; i + 1 < path.Count; i++)
                b.Ships[E(path[i], path[i + 1])] = me;
            Assert.AreEqual(union.Count, MatchPartyLibrary.ConnectedDistinctResources(m, 0), "the ship line joins them");
        }

        [Test]
        public void ShipCommands_RoundTripJson_AndHash()
        {
            var from = new Edge(new Vertex(new HexCoord(1, 0), 0), new Vertex(new HexCoord(1, 0), 1));
            var to = new Edge(new Vertex(new HexCoord(1, 0), 1), new Vertex(new HexCoord(1, 0), 2));
            var c = MatchCommand.MoveShipTo(2, from, to);
            var back = MatchCommand.FromJson(c.ToJson());
            Assert.AreEqual(c, back);
            Assert.AreEqual(to, back.ToEdge);
            Assert.AreNotEqual(c, MatchCommand.MoveShipTo(2, from, from));
            StringAssert.DoesNotContain("ToQ", MatchCommand.Simple(MatchCommandType.EndTurn, 0).ToJson(), "old commands keep their wire format");
            Assert.AreEqual(32, (int)MatchCommandType.BuildShip);
            Assert.AreEqual(33, (int)MatchCommandType.MoveShip);
            Assert.AreEqual(34, (int)MatchCommandType.MovePirate);

            var m = EmptyIslands();
            ulong h0 = m.State.ComputeHash();
            m.State.Board.Ships[from] = m.State.Seats[0].Owner;
            ulong h1 = m.State.ComputeHash();
            Assert.AreNotEqual(h0, h1, "ships are part of the hash");
            m.State.Board.PirateHex = new HexCoord(1, 0);
            Assert.AreNotEqual(h1, m.State.ComputeHash(), "so is the pirate");
        }
    }
}
