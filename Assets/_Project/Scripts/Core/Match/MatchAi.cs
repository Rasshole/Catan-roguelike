using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v134 Phase 1: deterministic AI for match seats. It never touches state; it only proposes the next
    /// <see cref="MatchCommand"/>, which goes through <see cref="MatchController.Apply"/> like a human's.
    /// Also used as the timeout policy (a human's timer runs out) and as a scripted stand-in human in tests.
    /// </summary>
    public static class MatchAi
    {
        public static MatchCommand ChooseCommand(MatchController match)
        {
            var state = match.State;
            int seat = state.ActingSeat;
            switch (state.Phase)
            {
                case MatchPhase.SetupSettlement:
                    return MatchCommand.AtVertex(MatchCommandType.PlaceSetupSettlement, seat,
                        Best(match.ValidSetupSettlements(), v => VertexScore(state, v, seat)));
                case MatchPhase.SetupRoad:
                    return MatchCommand.AtEdge(MatchCommandType.PlaceSetupRoad, seat,
                        Best(match.ValidSetupRoads(), e => RoadScore(match, e, seat)));
                case MatchPhase.Roll:
                    return KnightIfBlocked(match, seat) ?? MatchCommand.Simple(MatchCommandType.RollDice, seat);
                case MatchPhase.Discard:
                    return MatchCommand.DiscardCards(seat,
                        MatchController.SuggestDiscard(state.Seats[seat].Hand, state.PendingDiscards[seat]));
                case MatchPhase.RoadBuilding:
                    return MatchCommand.AtEdge(MatchCommandType.BuildRoad, seat,
                        Best(match.ValidRoads(seat), e => RoadScore(match, e, seat)));
                case MatchPhase.MoveRobber:
                    return ChooseRobber(match, seat);
                case MatchPhase.Main:
                    return ChooseMainAction(match, seat) ?? MatchCommand.Simple(MatchCommandType.EndTurn, seat);
                case MatchPhase.CivPick:
                    return MatchCommand.PickCiv(seat, state.Seats[seat].CivOffer[0]);
                case MatchPhase.QuestPick:
                {
                    // Easiest quest offered (the 1-VP one), first trait.
                    var me = state.Seats[seat];
                    var quest = me.QuestOffer.OrderBy(q => MatchPartyLibrary.Quest(q)?.Points ?? 9).First();
                    return MatchCommand.PickQuestTrait(seat, quest, me.TraitOffer[0]);
                }
                default:
                    return null;
            }
        }

        /// <summary>v135: AI answer to an open offer: accept when it can pay and doesn't lose cards, else decline.</summary>
        public static MatchCommand ChooseTradeResponse(MatchController match, int seat)
        {
            var trade = match.State.ActiveTrade;
            var hand = match.State.Seats[seat].Hand;
            bool accept = trade != null && hand.CanAfford(trade.Get) && trade.Give.Total >= trade.Get.Total;
            return MatchCommand.Respond(seat, accept ? TradeResponseKind.Accepted : TradeResponseKind.Declined);
        }

        /// <summary>Play a knight when the robber sits on one of our producing tiles (or it clinches Largest Army).</summary>
        private static MatchCommand KnightIfBlocked(MatchController match, int seat)
        {
            if (!match.CanPlayDevCard(seat, MatchDevCard.Knight))
                return null;
            var state = match.State;
            var me = state.Seats[seat];
            bool blocked = false;
            for (int c = 0; c < 6 && !blocked && state.Board.RobberTile.HasValue; c++)
            {
                var v = VertexGraph.Canonicalize(new Vertex(state.Board.RobberTile.Value, c));
                blocked = state.Board.VertexBuildings.TryGetValue(v, out var b) && MatchSeats.ToSeat(b.owner) == seat;
            }

            bool army = state.LargestArmySeat != seat && me.KnightsPlayed + 1 >= MatchController.LargestArmyMinimum
                        && (!state.LargestArmySeat.HasValue
                            || me.KnightsPlayed + 1 > state.Seats[state.LargestArmySeat.Value].KnightsPlayed);
            return blocked || army ? MatchCommand.Simple(MatchCommandType.PlayKnight, seat) : null;
        }

        private static MatchCommand ChooseMainAction(MatchController match, int seat)
        {
            var state = match.State;
            var hand = state.Seats[seat].Hand;
            if (state.ActiveTrade != null)
                return null;

            var knight = KnightIfBlocked(match, seat);
            if (knight != null)
                return knight;

            var spots = match.CountSettlements(seat) < BuildingPieceLimits.MaxSettlements
                ? match.ValidSettlements(seat).ToList()
                : new List<Vertex>();
            if (spots.Count > 0 && hand.CanAfford(match.SettlementCostFor(seat)))
                return MatchCommand.AtVertex(MatchCommandType.BuildSettlement, seat,
                    Best(spots, v => VertexScore(state, v, seat)));

            var upgradable = match.CountCities(seat) < BuildingPieceLimits.MaxCities
                ? match.UpgradableSettlements(seat).ToList()
                : new List<Vertex>();
            if (upgradable.Count > 0 && hand.CanAfford(match.CityCostFor(seat)))
                return MatchCommand.AtVertex(MatchCommandType.BuildCity, seat,
                    Best(upgradable, v => VertexScore(state, v, seat)));

            // Roads only when they lead somewhere: no free spot yet, or racing for longest road.
            bool wantsRoad = false;
            if (spots.Count == 0 && match.CountRoads(seat) < BuildingPieceLimits.MaxRoads)
            {
                var roads = match.ValidRoads(seat).ToList();
                if (roads.Count > 0)
                {
                    var best = Best(roads, e => RoadScore(match, e, seat));
                    wantsRoad = RoadScore(match, best, seat) > 0;
                    if (wantsRoad && match.CanPlayDevCard(seat, MatchDevCard.RoadBuilding))
                        return MatchCommand.Simple(MatchCommandType.PlayRoadBuilding, seat);
                    if (wantsRoad && hand.CanAfford(match.RoadCostFor(seat)))
                        return MatchCommand.AtEdge(MatchCommandType.BuildRoad, seat, best);
                }
            }

            // v135 development cards: Monopoly when the table holds 3+ of something we lack,
            // Year of Plenty toward the current build, buy cards with spare grain/wool/ore.
            if (match.CanPlayDevCard(seat, MatchDevCard.Monopoly))
            {
                var target = MatchController.AllResources
                    .OrderByDescending(r => state.Seats.Where(o => o.Index != seat).Sum(o => o.Hand[r]))
                    .ThenBy(r => (int)r).First();
                if (state.Seats.Where(o => o.Index != seat).Sum(o => o.Hand[target]) >= 3)
                    return MatchCommand.Card(MatchCommandType.PlayMonopoly, seat, target, target);
            }

            if (match.CanPlayDevCard(seat, MatchDevCard.YearOfPlenty))
            {
                var cost = spots.Count > 0 ? match.SettlementCostFor(seat)
                    : upgradable.Count > 0 ? match.CityCostFor(seat) : match.DevCardCostFor(seat);
                var needs = new List<ResourceType>();
                var h = hand;
                foreach (var r in MatchController.AllResources)
                    for (int i = h[r]; i < cost[r] && needs.Count < 2; i++)
                        needs.Add(r);
                if (needs.Count > 0)
                    return MatchCommand.Card(MatchCommandType.PlayYearOfPlenty, seat, needs[0],
                        needs.Count > 1 ? needs[1] : needs[0]);
            }

            if (match.CanBuyDevCard(seat) && (spots.Count == 0 || hand.Total >= 7)
                                          && (upgradable.Count == 0 || hand.Stone > 3 || hand.Wheat > 2))
                return MatchCommand.Simple(MatchCommandType.BuyDevCard, seat);

            // Bank trade 4:1 toward the current goal (capped per turn so a flipping goal can never loop).
            if (TradesThisTurn(match, seat) >= MaxBankTradesPerTurn)
                return null;
            ResourceBundle goal;
            if (spots.Count > 0)
                goal = match.SettlementCostFor(seat);
            else if (upgradable.Count > 0
                     && (Deficit(hand, match.CityCostFor(seat)) <= Deficit(hand, match.RoadCostFor(seat)) + 1
                         || match.CountRoads(seat) >= BuildingPieceLimits.MaxRoads))
                goal = match.CityCostFor(seat);
            else
                goal = match.RoadCostFor(seat);
            foreach (var need in MatchController.AllResources)
            {
                if (hand[need] >= goal[need])
                    continue;
                var give = MatchController.AllResources
                    .Where(r => r != need && hand[r] - goal[r] >= match.BankRatio(seat, r))
                    .OrderByDescending(r => hand[r] - goal[r]).ThenBy(r => (int)r)
                    .Cast<ResourceType?>().FirstOrDefault();
                if (give.HasValue)
                    return MatchCommand.Trade(seat, give.Value, need);
            }

            return null;
        }

        public const int MaxBankTradesPerTurn = 3;

        private static int TradesThisTurn(MatchController match, int seat)
        {
            int count = 0;
            var log = match.CommandLog;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                var c = log[i];
                if (c.Seat != seat || c.Type == MatchCommandType.EndTurn)
                    break;
                if (c.Type == MatchCommandType.BankTrade)
                    count++;
            }

            return count;
        }

        private static int Deficit(ResourceBundle hand, ResourceBundle cost) =>
            MatchController.AllResources.Sum(r => System.Math.Max(0, cost[r] - hand[r]));

        private static MatchCommand ChooseRobber(MatchController match, int seat)
        {
            var state = match.State;
            HexCoord bestHex = default;
            int bestScore = int.MinValue;
            foreach (var tile in state.Board.Tiles.Values.OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R))
            {
                if (state.Board.RobberTile == tile.Coord)
                    continue;
                int pips = tile.IsDesert ? 0 : NumberTokenLibrary.GetPipWeight(tile.NumberToken ?? 0);
                int score = 0;
                for (int c = 0; c < 6; c++)
                {
                    var v = VertexGraph.Canonicalize(new Vertex(tile.Coord, c));
                    if (!state.Board.VertexBuildings.TryGetValue(v, out var b))
                        continue;
                    int owner = MatchSeats.ToSeat(b.owner);
                    int weight = (b.type == BuildingType.City ? 2 : 1) * (pips + 1);
                    score += owner == seat ? -3 * weight : weight * (1 + state.Seats[owner].VictoryPoints);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestHex = tile.Coord;
                }
            }

            int victim = match.RobberVictims(seat, bestHex)
                .OrderByDescending(s => state.Seats[s].PublicVictoryPoints)
                .ThenByDescending(s => state.Seats[s].CardCount)
                .ThenBy(s => s)
                .DefaultIfEmpty(-1).First();
            return MatchCommand.Robber(seat, bestHex, victim);
        }

        /// <summary>Production value of a vertex: pip weight sum + a bonus per distinct resource.</summary>
        public static int VertexScore(MatchState state, Vertex v, int seat)
        {
            int score = 0;
            var kinds = new HashSet<ResourceType>();
            foreach (var hex in VertexGraph.GetHexesForVertex(v))
            {
                if (!state.Board.TryGetTile(hex, out var tile) || tile.IsDesert)
                    continue;
                score += 3 * NumberTokenLibrary.GetPipWeight(tile.NumberToken ?? 0);
                kinds.Add(tile.Resource);
            }

            return score + 2 * kinds.Count;
        }

        private static int RoadScore(MatchController match, Edge e, int seat)
        {
            var state = match.State;
            var owner = MatchSeats.ToPlayerId(seat);
            int best = 0;
            foreach (var end in new[] { e.A, e.B })
            {
                if (match.Placement.CanPlaceSettlement(state.Board, end, owner, true))
                    best = System.Math.Max(best, 2 * VertexScore(state, end, seat));
                foreach (var next in VertexGraph.GetAdjacentVertices(end))
                {
                    var n = VertexGraph.Canonicalize(next);
                    if (match.Placement.CanPlaceSettlement(state.Board, n, owner, true))
                        best = System.Math.Max(best, VertexScore(state, n, seat));
                }
            }

            return best;
        }

        private static T Best<T>(IEnumerable<T> options, System.Func<T, int> score)
        {
            // Deterministic: highest score, ties broken by the stable string order of the option.
            return options
                .Select(o => (o, s: score(o), k: o.ToString()))
                .OrderByDescending(x => x.s)
                .ThenBy(x => x.k, System.StringComparer.Ordinal)
                .First().o;
        }
    }

    /// <summary>Drives AI seats until a human must act (or the match ends).</summary>
    public static class MatchRunner
    {
        /// <summary>Apply AI commands while the acting seat is an AI. Returns the number of commands applied.</summary>
        public static int AdvanceAi(MatchController match, int maxCommands = 100000)
        {
            int applied = 0;
            while (!match.IsOver && applied < maxCommands)
            {
                var command = PendingAiResponse(match);
                if (command == null)
                {
                    if (!match.State.Acting.IsAi)
                        break;
                    command = MatchAi.ChooseCommand(match);
                }

                var result = match.Apply(command);
                if (!result.Ok)
                    throw new System.InvalidOperationException($"AI proposed an invalid command {command}: {result.Error}");
                applied++;
            }

            return applied;
        }

        /// <summary>v135: an AI seat that still owes an answer to the open trade offer, answered by the policy.</summary>
        public static MatchCommand PendingAiResponse(MatchController match)
        {
            var trade = match.State.ActiveTrade;
            if (trade == null)
                return null;
            for (int i = 0; i < match.State.Seats.Count; i++)
                if (i != trade.From && match.State.Seats[i].IsAi && trade.Responses[i].Kind == TradeResponseKind.Pending)
                    return MatchAi.ChooseTradeResponse(match, i);
            return null;
        }

        public static bool HasAiWork(MatchController match) =>
            !match.IsOver && (match.State.Acting.IsAi || PendingAiResponse(match) != null);

        /// <summary>
        /// Play the whole match, using <see cref="MatchAi"/> for every seat (humans included, as scripted stand-ins).
        /// Stops at a winner or after <paramref name="maxTurns"/> turns.
        /// </summary>
        public static void RunToEnd(MatchController match, int maxTurns = 2000)
        {
            int guard = 0;
            while (!match.IsOver && match.State.TurnNumber <= maxTurns && guard++ < 1000000)
            {
                var command = MatchAi.ChooseCommand(match);
                var result = match.Apply(command);
                if (!result.Ok)
                    throw new System.InvalidOperationException($"Policy proposed an invalid command {command}: {result.Error}");
            }
        }

        /// <summary>A human's turn timer ran out: resolve forced choices with the AI policy, then end the turn.</summary>
        public static void ResolveTimeout(MatchController match)
        {
            int seat = match.State.ActingSeat;
            if (match.State.ActiveTrade != null)
                match.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, match.State.ActiveTrade.From));
            if (match.State.Phase == MatchPhase.Discard)
            {
                // Only the timed-out discarder is resolved; the next one gets their own clock.
                match.Apply(MatchAi.ChooseCommand(match));
                return;
            }

            int guard = 0;
            while (!match.IsOver && match.State.CurrentSeat == seat && match.State.ActingSeat == seat && guard++ < 64)
            {
                MatchCommand command = match.State.Phase == MatchPhase.Main
                    ? MatchCommand.Simple(MatchCommandType.EndTurn, seat)
                    : MatchAi.ChooseCommand(match);
                if (!match.Apply(command).Ok)
                    break;
            }
        }
    }
}
