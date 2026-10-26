using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

using CatanRoguelike.Core.Trade;
using CatanRoguelike.Core.Shop;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v134 Phase 1: deterministic AI for match seats. It never touches state; it only proposes the next
    /// <see cref="MatchCommand"/>, which goes through <see cref="MatchController.Apply"/> like a human's.
    /// Also used as the timeout policy (a human's timer runs out) and as a scripted stand-in human in tests.
    /// </summary>
    public static partial class MatchAi
    {
        public static MatchCommand ChooseCommand(MatchController match)
        {
            var state = match.State;
            int seat = state.ActingSeat;
            if (state.HarbourPickSeat == seat && seat >= 0)
                return MatchCommand.HarbourPick(seat, HarbourPilotPick(match, seat));
            switch (state.Phase)
            {
                case MatchPhase.SetupSettlement:
                    return MatchCommand.AtVertex(MatchCommandType.PlaceSetupSettlement, seat,
                        Best(match.ValidSetupSettlements(), v => PlacementScore(match, v, seat)));
                case MatchPhase.SetupRoad:
                    return MatchCommand.AtEdge(MatchCommandType.PlaceSetupRoad, seat,
                        Best(match.ValidSetupRoads(), e => RoadScore(match, e, seat)));
                case MatchPhase.Roll:
                    return ChooseGuess(match, seat) ?? ChooseKnight(match, seat, preRoll: true)
                        ?? MatchCommand.Simple(MatchCommandType.RollDice, seat);
                case MatchPhase.Vote:
                    return ChooseVote(match, seat);
                case MatchPhase.Auction:
                    return ChooseBid(match, seat);
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
                    // v142: the quest with the best expected points for this start (first trait).
                    var me = state.Seats[seat];
                    var quest = me.QuestOffer.OrderByDescending(q => QuestOutlook(match, seat, q)).ThenBy(q => (int)q).First();
                    return MatchCommand.PickQuestTrait(seat, quest, me.TraitOffer[0]);
                }
                default:
                    return null;
            }
        }

        /// <summary>v139: default pick for the offerer's get wildcards when confirming with <paramref name="partner"/>.</summary>
        public static ResourceBundle ChooseConfirmWildcards(MatchController match, int partner)
        {
            var trade = match.State.ActiveTrade;
            if (trade == null || trade.GetAny == 0)
                return ResourceBundle.Zero;
            var pool = MatchController.PartnerWildPool(match.State.Seats[partner].Hand, trade, trade.Responses[partner].AnyChoice);
            return TradeWildcards.PickForNeed(pool, trade.GetAny, match.State.Seats[trade.From].Hand);
        }

        private static MatchCommand ChooseMainAction(MatchController match, int seat)
        {
            var state = match.State;
            var hand = state.Seats[seat].Hand;
            if (state.ActiveTrade != null)
                return state.ActiveTrade.From == seat ? ResolveOwnOffer(match, seat) : null;

            var knight = ChooseKnight(match, seat, preRoll: false);
            if (knight != null)
                return knight;

            var spots = match.CountSettlements(seat) < BuildingPieceLimits.MaxSettlements
                ? match.ValidSettlements(seat).ToList()
                : new List<Vertex>();
            if (spots.Count > 0 && hand.CanAfford(match.SettlementCostFor(seat)))
                return MatchCommand.AtVertex(MatchCommandType.BuildSettlement, seat,
                    Best(spots, v => PlacementScore(match, v, seat)));

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

            // v142: buy a development card when the cards are worth more as a card than toward the next builds
            // (or the hand is at risk on a 7) — players buy knights to chase the army and guard against the robber.
            if (match.CanBuyDevCard(seat))
            {
                var after = hand;
                after.Pay(match.DevCardCostFor(seat));
                var me = match.State.Seats[seat];
                bool knightQuest = !me.QuestDone && (me.Quest == MatchQuestId.Knighthood || me.Quest == MatchQuestId.GrandArmy);
                double worth = DevCardWorth + (knightQuest ? 1.5 : 0.0);
                if (hand.Total > match.DiscardLimitFor(seat)
                    || HandValue(match, seat, after) + worth >= HandValue(match, seat, hand))
                    return MatchCommand.Simple(MatchCommandType.BuyDevCard, seat);
            }

            // v141 shenanigans (party rules): rerolls, bounties, sabotage.
            var mischief = ChooseShenanigan(match, seat);
            if (mischief != null)
                return mischief;

            // v142: ask the table first (1-for-1 beats the bank), at most twice a turn.
            var proposal = ChooseProposal(match, seat);
            if (proposal != null)
                return proposal;

            // Bank trade 4:1 toward the current goal (capped per turn so a flipping goal can never loop).
            ResourceBundle goal;
            if (spots.Count > 0)
                goal = match.SettlementCostFor(seat);
            else if (upgradable.Count > 0
                     && (Deficit(hand, match.CityCostFor(seat)) <= Deficit(hand, match.RoadCostFor(seat)) + 1
                         || match.CountRoads(seat) >= BuildingPieceLimits.MaxRoads))
                goal = match.CityCostFor(seat);
            else
                goal = match.RoadCostFor(seat);
            // v143 Fishermen: fish buy the missing card (bounded by the fish held, so this can't loop).
            if (match.CanFishForCard(seat))
            {
                foreach (var need in MatchController.AllResources)
                {
                    if (hand[need] < goal[need])
                        return MatchCommand.Fish(seat, need);
                }
            }

            if (TradesThisTurn(match, seat) >= MaxBankTradesPerTurn)
                return null;
            foreach (var need in MatchController.AllResources)
            {
                if (hand[need] >= goal[need])
                    continue;
                var give = MatchController.AllResources
                    .Where(r => r != need && hand[r] - goal[r] >= match.BankRatio(seat, r))
                    .OrderByDescending(r => hand[r] - goal[r]).ThenBy(r => (int)r)
                    .Cast<ResourceType?>().FirstOrDefault();
                if (!give.HasValue)
                    continue;
                var batch = PlanBankBatch(match, seat, hand, goal);
                if (batch != null)
                    return batch;
                return MatchCommand.Trade(seat, give.Value, need);
            }

            return null;
        }

        /// <summary>
        /// v143: plan the same greedy one-for-one bank trades the AI would make in sequence, and if there are two or
        /// more, fold them into one <see cref="MatchCommandType.BankTradeBatch"/> (validated with the controller's
        /// own per-batch rates so first-trade perks count exactly like single trades).
        /// </summary>
        private static MatchCommand PlanBankBatch(MatchController match, int seat, ResourceBundle hand, ResourceBundle goal)
        {
            var give = new ResourceBundle();
            var get = new ResourceBundle();
            var sim = hand;
            int room = MaxBankTradesPerTurn - TradesThisTurn(match, seat);
            for (int guard = 0; guard < room; guard++)
            {
                var need = MatchController.AllResources.Where(r => sim[r] < goal[r] && give[r] == 0)
                    .Cast<ResourceType?>().FirstOrDefault();
                if (!need.HasValue)
                    break;
                var from = MatchController.AllResources
                    .Where(r => r != need.Value && get[r] == 0 && sim[r] - goal[r] >= match.BankRatio(seat, r))
                    .OrderByDescending(r => sim[r] - goal[r]).ThenBy(r => (int)r)
                    .Cast<ResourceType?>().FirstOrDefault();
                if (!from.HasValue)
                    break;
                int rate = match.BankRatio(seat, from.Value);
                sim.Add(from.Value, -rate);
                sim.Add(need.Value, 1);
                give.Add(from.Value, rate);
                get.Add(need.Value, 1);
            }

            if (get.Total < 2 || match.BankBatchCards(seat, give) != get.Total || !hand.CanAfford(give))
                return null;
            return MatchCommand.BankBatch(seat, give, get);
        }

        public const int MaxBankTradesPerTurn = 3;

        /// <summary>v142: how much a face-down development card is worth in <see cref="HandValue"/> terms.</summary>
        public const double DevCardWorth = 2.0;

        private static int TradesThisTurn(MatchController match, int seat) =>
            ThisTurn(match).Count(c => c.Seat == seat && (c.Type == MatchCommandType.BankTrade || c.Type == MatchCommandType.BankTradeBatch));

        private static int Deficit(ResourceBundle hand, ResourceBundle cost) =>
            MatchController.AllResources.Sum(r => System.Math.Max(0, cost[r] - hand[r]));

        /// <summary>
        /// v142 robber: the leader's (and other high scorers') best-producing hexes, never a hex with our own pieces
        /// unless there is no other choice; bounties make a target juicier. Steals from the bounty target, else the leader.
        /// </summary>
        public static MatchCommand ChooseRobber(MatchController match, int seat)
        {
            var state = match.State;
            int leader = Leader(state, seat);
            HexCoord bestHex = default;
            int bestScore = int.MinValue;
            foreach (var tile in state.Board.Tiles.Values.OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R))
            {
                if (state.Board.RobberTile == tile.Coord)
                    continue;
                if (tile.IsDesert && state.ActiveDecree == MatchDecreeId.NoDesertRobber)
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
                    if (owner == seat)
                    {
                        score -= 1000;
                        continue;
                    }

                    var o = state.Seats[owner];
                    score += weight * (1 + o.PublicVictoryPoints) + (owner == leader ? 3 * weight : 0);
                    if (o.CardCount > 0)
                        score += 2;
                    // v141: a bounty on a robbable owner is worth chasing.
                    if (o.CardCount > 0)
                        score += 6 * match.BountiesOn(owner).Where(x => x.Poster != seat).Sum(x => x.Reward.Total);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestHex = tile.Coord;
                }
            }

            int victim = match.RobberVictims(seat, bestHex)
                .OrderByDescending(s => match.BountiesOn(s).Any(b => b.Poster != seat))
                .ThenByDescending(s => s == leader)
                .ThenByDescending(s => state.Seats[s].PublicVictoryPoints)
                .ThenByDescending(s => state.Seats[s].CardCount)
                .ThenBy(s => s)
                .DefaultIfEmpty(-1).First();
            return MatchCommand.Robber(seat, bestHex, victim);
        }

        /// <summary>
        /// v142 settlement spot: production (<see cref="VertexScore"/>) plus resources we don't produce yet and a
        /// harbour that suits what we produce.
        /// </summary>
        public static int PlacementScore(MatchController match, Vertex v, int seat)
        {
            var state = match.State;
            int score = VertexScore(state, v, seat);
            var owner = MatchSeats.ToPlayerId(seat);
            var produced = new Dictionary<ResourceType, int>();
            foreach (var kv in state.Board.VertexBuildings)
            {
                if (kv.Value.owner != owner)
                    continue;
                foreach (var hex in VertexGraph.GetHexesForVertex(kv.Key))
                    if (state.Board.TryGetTile(hex, out var t) && !t.IsDesert)
                        produced[t.Resource] = (produced.TryGetValue(t.Resource, out int had) ? had : 0)
                                               + NumberTokenLibrary.GetPipWeight(t.NumberToken ?? 0);
            }

            var fresh = new HashSet<ResourceType>();
            foreach (var hex in VertexGraph.GetHexesForVertex(v))
                if (state.Board.TryGetTile(hex, out var t) && !t.IsDesert && !produced.ContainsKey(t.Resource)
                    && (t.NumberToken ?? 0) > 0)
                    fresh.Add(t.Resource);
            score += 3 * fresh.Count;

            var cv = VertexGraph.Canonicalize(v);
            foreach (var port in state.Ports)
            {
                if (!PortAccess.Serves(state.Board, port, cv))
                    continue;
                if (port.IsGeneric)
                    score += 3;
                else if (port.SpecificResource.HasValue)
                    score += 1 + System.Math.Min(6, produced.TryGetValue(port.SpecificResource.Value, out int made) ? made : 0);
            }

            return score;
        }

        /// <summary>v142 quest pick: points times a rough chance — a base rate per tier plus the progress already made.</summary>
        public static double QuestOutlook(MatchController match, int seat, MatchQuestId id)
        {
            var q = MatchPartyLibrary.Quest(id);
            if (q == null)
                return -1;
            double progress = q.Target > 0 ? (double)q.CurrentProgress(match, seat) / q.Target : 0;
            double chance = (q.Points >= 2 ? 0.2 : 0.45) + 0.6 * progress;
            if (!q.Plausible(match, seat))
                chance *= 0.3;
            return q.Points * System.Math.Min(1.0, chance);
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

        /// <summary>v144 Harbour Pilot: the resource furthest short of the AI's next build (least held if none is short).</summary>
        public static ResourceType HarbourPilotPick(MatchController match, int seat)
        {
            var hand = match.State.Seats[seat].Hand;
            var goal = PrimaryGoal(match, seat);
            ResourceType best = MatchController.LeastHeld(hand);
            int bestShort = 0;
            foreach (var r in MatchController.AllResources)
            {
                int shortBy = goal[r] - hand[r];
                if (shortBy > bestShort)
                {
                    bestShort = shortBy;
                    best = r;
                }
            }

            return best;
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
                    // v142: an AI's offer waits until the humans at the table have answered.
                    if (!match.State.Acting.IsAi || MatchAi.AwaitingHumanAnswer(match))
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
            if (trade == null || match.State.HarbourPickSeat >= 0)
                return null;
            for (int i = 0; i < match.State.Seats.Count; i++)
                if (i != trade.From && match.State.Seats[i].IsAi && trade.Responses[i].Kind == TradeResponseKind.Pending)
                    return MatchAi.ChooseTradeResponse(match, i);
            return null;
        }

        public static bool HasAiWork(MatchController match) =>
            !match.IsOver && ((match.State.Acting.IsAi && !MatchAi.AwaitingHumanAnswer(match)) || PendingAiResponse(match) != null);

        /// <summary>v142: any seat (AI or a scripted stand-in human) that still owes an answer, answered by the policy.</summary>
        public static MatchCommand PendingResponse(MatchController match)
        {
            var trade = match.State.ActiveTrade;
            if (trade == null)
                return null;
            for (int i = 0; i < match.State.Seats.Count; i++)
                if (i != trade.From && trade.Responses[i].Kind == TradeResponseKind.Pending)
                    return MatchAi.ChooseTradeResponse(match, i);
            return null;
        }

        /// <summary>
        /// Play the whole match, using <see cref="MatchAi"/> for every seat (humans included, as scripted stand-ins).
        /// Stops at a winner or after <paramref name="maxTurns"/> turns.
        /// </summary>
        public static void RunToEnd(MatchController match, int maxTurns = 2000)
        {
            int guard = 0;
            while (!match.IsOver && match.State.TurnNumber <= maxTurns && guard++ < 1000000)
            {
                var command = PendingResponse(match) ?? MatchAi.ChooseCommand(match);
                var result = match.Apply(command);
                if (!result.Ok)
                    throw new System.InvalidOperationException($"Policy proposed an invalid command {command}: {result.Error}");
            }
        }

        /// <summary>A human's turn timer ran out: resolve forced choices with the AI policy, then end the turn.</summary>
        public static void ResolveTimeout(MatchController match)
        {
            int seat = match.State.ActingSeat;
            // v144: an owed Harbour Pilot pick is auto-picked first.
            int pickGuard = 0;
            while (match.State.HarbourPickSeat == seat && pickGuard++ < 16)
                if (!match.Apply(MatchAi.ChooseCommand(match)).Ok)
                    break;
            if (MatchAi.AwaitingHumanAnswer(match))
            {
                // v142: the humans ran out of time to answer an AI's offer: their silence is a no.
                var trade = match.State.ActiveTrade;
                for (int i = 0; i < match.State.Seats.Count; i++)
                    if (i != trade.From && trade.Responses[i].Kind == TradeResponseKind.Pending)
                        match.Apply(MatchCommand.Respond(i, TradeResponseKind.Declined));
                return;
            }

            if (match.State.ActiveTrade != null)
                match.Apply(MatchCommand.Simple(MatchCommandType.CancelTrade, match.State.ActiveTrade.From));
            if (match.State.Phase == MatchPhase.Discard || match.State.IsBallot)
            {
                // Only the timed-out discarder is resolved; the next one gets their own clock.
                match.Apply(MatchAi.ChooseCommand(match));
                return;
            }

            int guard = 0;
            while (!match.IsOver && match.State.CurrentSeat == seat && match.State.ActingSeat == seat && guard++ < 64)
            {
                MatchCommand command = match.State.Phase == MatchPhase.Main && match.State.HarbourPickSeat < 0
                    ? MatchCommand.Simple(MatchCommandType.EndTurn, seat)
                    : MatchAi.ChooseCommand(match);
                if (!match.Apply(command).Ok)
                    break;
            }

            pickGuard = 0;
            while (!match.IsOver && match.State.HarbourPickSeat == seat && pickGuard++ < 16)
                if (!match.Apply(MatchAi.ChooseCommand(match)).Ok)
                    break;
        }
    }
}
