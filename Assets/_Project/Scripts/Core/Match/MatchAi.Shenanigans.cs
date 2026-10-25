using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Victory;
using CatanRoguelike.Core.Yield;

namespace CatanRoguelike.Core.Match
{
    /// <summary>v141: AI policy for the shenanigans (ballots, sealed bids, bounties, sabotage, rerolls).</summary>
    public static partial class MatchAi
    {
        /// <summary>The other seat with the most public VP (ties: lowest seat).</summary>
        private static int Leader(MatchState state, int seat) =>
            state.Seats.Where(s => s.Index != seat)
                .OrderByDescending(s => s.PublicVictoryPoints).ThenBy(s => s.Index).First().Index;

        public static MatchCommand ChooseVote(MatchController match, int seat)
        {
            var state = match.State;
            var me = state.Seats[seat];
            bool aye;
            switch (state.BallotDecree)
            {
                case MatchDecreeId.PortsThreeToOne:
                    // v142: closing the harbours hurts their owners and pays everyone else a card.
                    aye = match.HarbourTariffPays(seat);
                    break;
                case MatchDecreeId.NoDesertRobber:
                    // The robber must then sit on someone's production: the bigger producers lose most.
                    aye = ProductionPips(match, seat) * state.Seats.Count < state.Seats.Sum(s => ProductionPips(match, s.Index));
                    break;
                case MatchDecreeId.RichestDiscardsToSeven:
                    aye = !match.WealthTaxPayers().Contains(seat);
                    break;
                default:
                    aye = false;
                    break;
            }

            return MatchCommand.Vote(seat, aye);
        }

        /// <summary>
        /// v142 sealed bids: a VP card is worth a lot (more when it would nearly win), a sabotage card only when
        /// someone is ahead, a reroll only for a poor token. Never below the reserve, never the whole hand.
        /// </summary>
        public static MatchCommand ChooseBid(MatchController match, int seat)
        {
            var state = match.State;
            var me = state.Seats[seat];
            var hand = me.Hand;
            var item = state.AuctionItem;
            int reserve = MatchController.ReservePrice(item);
            int spare = hand.Total - 1;
            int amount = 0;
            switch (item)
            {
                case MatchAuctionItem.VictoryPointCard:
                {
                    bool closing = me.VictoryPoints + 1 >= state.Config.VictoryPoints - 1;
                    amount = System.Math.Min(closing ? 6 : 4, hand.Total / 2 + (closing ? 2 : 0));
                    break;
                }
                case MatchAuctionItem.SabotageCard:
                {
                    var lead = state.Seats[Leader(state, seat)];
                    amount = hand.Total >= 6 && lead.PublicVictoryPoints > me.PublicVictoryPoints && me.Sabotage.Count == 0 ? 2 : 0;
                    break;
                }
                case MatchAuctionItem.TokenReroll:
                {
                    bool poor = match.RerollableTiles(seat).Any(h =>
                        NumberTokenLibrary.GetPipWeight(state.Board.Tiles[h].NumberToken ?? 0) <= 2);
                    amount = poor ? System.Math.Min(2, hand.Total / 3) : 0;
                    break;
                }
            }

            amount = System.Math.Min(amount, spare);
            if (amount < reserve)
                amount = 0;
            return MatchCommand.Bid(seat, amount > 0 ? MatchController.SuggestDiscard(hand, amount) : ResourceBundle.Zero);
        }

        /// <summary>Pip weight a seat collects from its buildings (cities count twice).</summary>
        public static int ProductionPips(MatchController match, int seat)
        {
            var state = match.State;
            var owner = MatchSeats.ToPlayerId(seat);
            int total = 0;
            foreach (var kv in state.Board.VertexBuildings)
            {
                if (kv.Value.owner != owner)
                    continue;
                int mult = kv.Value.type == BuildingType.City ? 2 : 1;
                foreach (var hex in VertexGraph.GetHexesForVertex(kv.Key))
                    if (state.Board.TryGetTile(hex, out var t) && !t.IsDesert)
                        total += mult * NumberTokenLibrary.GetPipWeight(t.NumberToken ?? 0);
            }

            return total;
        }

        /// <summary>Before rolling: pay 1 card to name the likeliest author (the leader) of a face-down card on us.</summary>
        private static MatchCommand ChooseGuess(MatchController match, int seat)
        {
            var hand = match.State.Seats[seat].Hand;
            if (!match.CanGuessSaboteur(seat) || hand.Total < 4)
                return null;
            var pay = MatchController.SuggestDiscard(hand, 1);
            return MatchCommand.Guess(seat, Leader(match.State, seat), pay);
        }

        private static MatchCommand ChooseShenanigan(MatchController match, int seat)
        {
            if (!match.PartyRules)
                return null;
            var state = match.State;
            var me = state.Seats[seat];
            var hand = me.Hand;

            // A won reroll goes on our weakest numbered tile.
            if (me.Rerolls > 0)
            {
                var weakest = match.RerollableTiles(seat)
                    .OrderBy(h => NumberTokenLibrary.GetPipWeight(state.Board.Tiles[h].NumberToken ?? 0))
                    .ThenBy(h => h.Q).ThenBy(h => h.R).Cast<HexCoord?>().FirstOrDefault();
                if (weakest.HasValue && NumberTokenLibrary.GetPipWeight(state.Board.Tiles[weakest.Value].NumberToken ?? 0) <= 3)
                    return MatchCommand.Reroll(seat, weakest.Value);
            }

            int leader = Leader(state, seat);
            var lead = state.Seats[leader];
            bool leaderAhead = lead.PublicVictoryPoints >= me.PublicVictoryPoints + 2;

            if (hand.Total >= 7 && match.BountyBy(seat) == null && leaderAhead && lead.CardCount > 0)
            {
                var reward = ResourceBundle.Zero;
                reward.Add(MatchController.AllResources.OrderByDescending(r => hand[r]).ThenBy(r => (int)r).First(), 1);
                return MatchCommand.Bounty(seat, leader, reward);
            }

            // v142: sabotage only on a leader who is actually ahead.
            if (me.Sabotage.Count > 0 && lead.PublicVictoryPoints > me.PublicVictoryPoints)
            {
                foreach (var kind in me.Sabotage.Distinct().OrderBy(k => (int)k))
                {
                    var play = SabotagePlay(match, seat, leader, kind);
                    if (play != null)
                        return play;
                }
            }

            if (hand.Total >= 8 && me.Sabotage.Count == 0 && match.CanBuySabotage(seat))
                return MatchCommand.Simple(MatchCommandType.BuySabotage, seat);
            return null;
        }

        private static MatchCommand SabotagePlay(MatchController match, int seat, int target, MatchSabotageKind kind)
        {
            var state = match.State;
            switch (kind)
            {
                case MatchSabotageKind.Blight:
                {
                    if (match.PendingSabotageOn(target) != null)
                        return null;
                    var tiles = match.BlightTiles(target).ToList();
                    if (tiles.Count == 0)
                        return null;
                    var best = tiles.OrderByDescending(h => NumberTokenLibrary.GetPipWeight(state.Board.Tiles[h].NumberToken ?? 0))
                        .ThenBy(h => h.Q).ThenBy(h => h.R).First();
                    return MatchCommand.Sabotage(seat, kind, target, best);
                }
                case MatchSabotageKind.SpoiledGrain:
                    return match.PendingSabotageOn(target) != null ? null : MatchCommand.Sabotage(seat, kind, target);
                case MatchSabotageKind.WashedOutRoad:
                {
                    var roads = match.WashableRoads(target).ToList();
                    if (roads.Count == 0)
                        return null;
                    var owner = state.Seats[target].Owner;
                    int full = RouteCalculator.LongestRoadLength(state.Board, owner);
                    HexMath.Edge best = roads[0];
                    int bestLoss = -1;
                    foreach (var e in roads)
                    {
                        // Trial removal, restored at once (the policy leaves the state as it found it).
                        state.Board.DisabledRoads.Add(e);
                        int loss = full - RouteCalculator.LongestRoadLength(state.Board, owner);
                        state.Board.DisabledRoads.Remove(e);
                        if (loss > bestLoss)
                        {
                            bestLoss = loss;
                            best = e;
                        }
                    }

                    return MatchCommand.SabotageRoad(seat, target, best);
                }
                default:
                    return null;
            }
        }
    }
}
