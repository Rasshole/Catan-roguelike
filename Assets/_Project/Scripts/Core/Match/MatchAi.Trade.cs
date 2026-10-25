using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Trade;
using CatanRoguelike.Core.Yield;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v142 human-like play: player-to-player trading (proposals, answers, counters, closing the deal) and a
    /// knight / robber policy that goes after the leader. Pure functions of the state, so replays stay deterministic.
    /// </summary>
    public static partial class MatchAi
    {
        /// <summary>At most this many offers per turn (a second one only sweetens or changes the first).</summary>
        public const int MaxProposalsPerTurn = 2;

        /// <summary>Nobody trades with a seat this close to winning (public VP within this many of the target).</summary>
        public const int NearWinMargin = 2;

        // ---------- goals and hand value ----------

        /// <summary>What the seat is saving for: a settlement (if a spot is free), a city, else a road / a dev card.</summary>
        public static ResourceBundle PrimaryGoal(MatchController match, int seat)
        {
            var hand = match.State.Seats[seat].Hand;
            bool spot = match.CountSettlements(seat) < BuildingPieceLimits.MaxSettlements && match.ValidSettlements(seat).Any();
            if (spot)
                return match.SettlementCostFor(seat);
            bool upgrade = match.CountCities(seat) < BuildingPieceLimits.MaxCities && match.UpgradableSettlements(seat).Any();
            if (upgrade && (Deficit(hand, match.CityCostFor(seat)) <= Deficit(hand, match.RoadCostFor(seat)) + 1
                            || match.CountRoads(seat) >= BuildingPieceLimits.MaxRoads))
                return match.CityCostFor(seat);
            if (match.CountRoads(seat) < BuildingPieceLimits.MaxRoads && match.ValidRoads(seat).Any())
                return match.RoadCostFor(seat);
            return match.DevCardCostFor(seat);
        }

        /// <summary>The build after the next one: a city (or a dev card while saving for a city / with no city left);
        /// a road leads to a settlement.</summary>
        private static ResourceBundle SecondaryGoal(MatchController match, int seat, ResourceBundle primary)
        {
            if (primary.Equals(match.RoadCostFor(seat)))
                return match.SettlementCostFor(seat);
            bool upgrade = match.CountCities(seat) < BuildingPieceLimits.MaxCities && match.UpgradableSettlements(seat).Any();
            return upgrade && !primary.Equals(match.CityCostFor(seat)) ? match.CityCostFor(seat) : match.DevCardCostFor(seat);
        }

        /// <summary>
        /// How much a hand is worth to the seat right now: being short for the next build costs most, the build after
        /// that a bit, every card a little and holding a variety a little more. Trades are judged by the change.
        /// </summary>
        public static double HandValue(MatchController match, int seat, ResourceBundle hand)
        {
            var goal = PrimaryGoal(match, seat);
            var next = SecondaryGoal(match, seat, goal);
            double v = -2.0 * Deficit(hand, goal) - 0.7 * Deficit(hand, next) + 0.45 * hand.Total;
            v += 0.25 * MatchController.AllResources.Count(r => hand[r] > 0);
            // A hand that a 7 would cut in half is worth less per card.
            if (hand.Total > match.DiscardLimitFor(seat))
                v -= 0.35 * (hand.Total - match.DiscardLimitFor(seat));
            return v;
        }

        private static bool NearWin(MatchController match, int seat) =>
            match.State.Seats[seat].PublicVictoryPoints >= match.State.Config.VictoryPoints - NearWinMargin;

        /// <summary>Extra value a seat wants before it helps <paramref name="other"/>: the runaway leader pays a premium.</summary>
        private static double LeaderPremium(MatchController match, int seat, int other)
        {
            var s = match.State.Seats;
            int gap = s[other].PublicVictoryPoints - s[seat].PublicVictoryPoints;
            bool leader = s.All(o => o.Index == other || o.PublicVictoryPoints < s[other].PublicVictoryPoints);
            return leader && gap >= 2 ? 0.9 + 0.3 * (gap - 2) : 0.0;
        }

        private static ResourceBundle After(ResourceBundle hand, ResourceBundle pay, ResourceBundle take)
        {
            var h = hand;
            h.Pay(pay);
            h.Add(take);
            return h;
        }

        // ---------- answering an offer ----------

        /// <summary>
        /// v142 answer to an open offer, judged like a player would: does it bring the next build closer, is it fair
        /// for what we give, and are we feeding the leader? Never trades with a seat about to win. Sometimes counters
        /// with a 1-for-1 that suits us better. v139 wildcards: picks what it needs from "any resource".
        /// </summary>
        public static MatchCommand ChooseTradeResponse(MatchController match, int seat)
        {
            var state = match.State;
            var trade = state.ActiveTrade;
            var decline = MatchCommand.Respond(seat, TradeResponseKind.Declined);
            if (trade == null || trade.From == seat)
                return decline;
            if (NearWin(match, trade.From))
                return decline;
            var hand = state.Seats[seat].Hand;
            var choice = ResourceBundle.Zero;
            if (trade.GiveAny > 0)
                choice = TradeWildcards.PickForNeed(match.AcceptWildPool(trade), trade.GiveAny, hand);
            double premium = LeaderPremium(match, seat, trade.From);
            double before = HandValue(match, seat, hand);
            if (match.CanAcceptTrade(seat, trade, choice))
            {
                var take = trade.Give;
                take.Add(choice);
                var pay = trade.Get;
                if (trade.GetAny > 0)
                {
                    // The offerer picks: assume they take what we would miss most (pessimistic).
                    var pool = TradeWildcards.Pool(hand, trade.Get, trade.Give, choice);
                    pay.Add(WorstPick(match, seat, hand, pool, trade.GetAny));
                }

                if (HandValue(match, seat, After(hand, pay, take)) - before >= 0.6 + premium)
                    return MatchCommand.AcceptWithChoice(seat, choice);
            }

            var counter = ChooseCounter(match, seat, trade, before, premium);
            return counter ?? decline;
        }

        /// <summary>A 1-for-1 counter: one card we need from what they offer, for one card we can spare.</summary>
        private static MatchCommand ChooseCounter(MatchController match, int seat, MatchTradeOffer trade, double before, double premium)
        {
            var state = match.State;
            // Like people, not every time: about two answers in three consider a counter.
            if ((state.TurnNumber + seat * 2) % 3 == 0)
                return null;
            var hand = state.Seats[seat].Hand;
            var offerer = state.Seats[trade.From].Hand;
            var goal = PrimaryGoal(match, seat);
            var wanted = MatchController.AllResources
                .Where(r => hand[r] < goal[r] && (trade.Give[r] > 0 || (trade.GiveAny > 0 && offerer[r] > 0 && trade.Get[r] == 0)))
                .OrderByDescending(r => goal[r] - hand[r]).ThenBy(r => (int)r).ToList();
            if (wanted.Count == 0)
                return null;
            var need = wanted[0];
            var spare = MatchController.AllResources
                .Where(r => r != need && hand[r] - goal[r] >= 1)
                .OrderByDescending(r => trade.Get[r] > 0)
                .ThenByDescending(r => hand[r] - goal[r]).ThenBy(r => (int)r)
                .Cast<ResourceType?>().FirstOrDefault();
            if (!spare.HasValue)
                return null;
            var give = ResourceBundle.Zero; // the offerer gives
            give.Add(need, 1);
            var get = ResourceBundle.Zero; // the offerer gets
            get.Add(spare.Value, 1);
            if (give.Equals(trade.Give) && get.Equals(trade.Get) && trade.GiveAny == 0 && trade.GetAny == 0)
                return null;
            if (HandValue(match, seat, After(hand, get, give)) - before < 0.6 + premium)
                return null;
            return MatchCommand.Respond(seat, TradeResponseKind.Countered, give, get);
        }

        private static ResourceBundle WorstPick(MatchController match, int seat, ResourceBundle hand, ResourceBundle pool, int n)
        {
            var pick = ResourceBundle.Zero;
            for (int k = 0; k < n; k++)
            {
                ResourceType? worst = null;
                double worstValue = double.MaxValue;
                foreach (var r in MatchController.AllResources)
                {
                    if (pool[r] - pick[r] <= 0)
                        continue;
                    var p = pick;
                    p.Add(r, 1);
                    var h = hand;
                    h.Pay(p);
                    double v = HandValue(match, seat, h);
                    if (v < worstValue)
                    {
                        worstValue = v;
                        worst = r;
                    }
                }

                if (!worst.HasValue)
                    break;
                pick.Add(worst.Value, 1);
            }

            return pick;
        }

        // ---------- making an offer ----------

        /// <summary>Offers made by <paramref name="seat"/> since the turn began (answers by other seats don't reset it).</summary>
        public static int ProposalsThisTurn(MatchController match, int seat) => ThisTurn(match).Count(c =>
            c.Seat == seat && c.Type == MatchCommandType.ProposeTrade);

        private static IEnumerable<MatchCommand> ThisTurn(MatchController match)
        {
            var log = match.CommandLog;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                var c = log[i];
                if (c.Type == MatchCommandType.EndTurn || c.Type == MatchCommandType.RollDice)
                {
                    if (c.Type == MatchCommandType.RollDice)
                        yield return c;
                    yield break;
                }

                yield return c;
            }
        }

        /// <summary>
        /// v142: a 1-for-1 (or, second time round, 2-for-1) offer for a card the next build is short of, paid from
        /// a surplus — only when the build is 1–2 cards away and someone we'd trade with seems to hold the card.
        /// </summary>
        public static MatchCommand ChooseProposal(MatchController match, int seat)
        {
            var state = match.State;
            if (state.Phase != MatchPhase.Main || state.ActiveTrade != null || state.CurrentSeat != seat)
                return null;
            int made = ProposalsThisTurn(match, seat);
            if (made >= MaxProposalsPerTurn)
                return null;
            var hand = state.Seats[seat].Hand;
            var goal = PrimaryGoal(match, seat);
            int deficit = Deficit(hand, goal);
            if (deficit < 1 || deficit > 2 || (made > 0 && deficit > 1))
                return null;
            var partners = Enumerable.Range(0, state.Seats.Count).Where(i => i != seat && !NearWin(match, i)).ToList();
            var needs = MatchController.AllResources.Where(r => hand[r] < goal[r])
                .OrderByDescending(r => goal[r] - hand[r]).ThenBy(r => (int)r).ToList();
            var previous = ThisTurn(match).Where(c => c.Seat == seat && c.Type == MatchCommandType.ProposeTrade).ToList();
            double before = HandValue(match, seat, hand);
            foreach (var need in needs)
            {
                // Card counting, roughly: only ask for what a trade partner has been seen to hold.
                if (!partners.Any(p => state.Seats[p].Hand[need] > 0))
                    continue;
                var spares = MatchController.AllResources
                    .Where(r => r != need && hand[r] - goal[r] >= 1)
                    .OrderByDescending(r => hand[r] - goal[r]).ThenByDescending(r => match.BankRatio(seat, r))
                    .ThenBy(r => (int)r).ToList();
                if (spares.Count == 0)
                    continue;
                var get = ResourceBundle.Zero;
                get.Add(need, 1);
                var give = ResourceBundle.Zero;
                give.Add(spares[0], 1);
                if (previous.Any(p => p.Offer.Equals(give) && p.Request.Equals(get)))
                {
                    // Second try: sweeten it with another spare card.
                    var extra = spares.FirstOrDefault(r => hand[r] - goal[r] - give[r] >= 1);
                    if (hand[extra] - goal[extra] - give[extra] < 1)
                        continue;
                    give.Add(extra, 1);
                }

                if (previous.Any(p => p.Offer.Equals(give) && p.Request.Equals(get)))
                    continue;
                if (HandValue(match, seat, After(hand, give, get)) - before < 0.5)
                    continue;
                return MatchCommand.Propose(seat, give, get, 0, 0);
            }

            return null;
        }

        /// <summary>
        /// v142: the AI's own offer once the answers are in: close the best deal (accepted or countered) with a seat
        /// that isn't about to win, else withdraw. Waits for nobody — the runner only asks once everyone answered.
        /// </summary>
        public static MatchCommand ResolveOwnOffer(MatchController match, int seat)
        {
            var state = match.State;
            var trade = state.ActiveTrade;
            var hand = state.Seats[seat].Hand;
            double before = HandValue(match, seat, hand);
            int best = -1;
            double bestGain = double.MinValue;
            var bestChoice = ResourceBundle.Zero;
            for (int i = 0; i < state.Seats.Count; i++)
            {
                if (i == seat || NearWin(match, i))
                    continue;
                var r = trade.Responses[i];
                ResourceBundle give, get, choice = ResourceBundle.Zero;
                if (r.Kind == TradeResponseKind.Accepted)
                {
                    choice = ChooseConfirmWildcards(match, i);
                    give = trade.Give;
                    give.Add(r.AnyChoice);
                    get = trade.Get;
                    get.Add(choice);
                }
                else if (r.Kind == TradeResponseKind.Countered)
                {
                    give = r.CounterGive;
                    get = r.CounterGet;
                }
                else
                    continue;

                if (!hand.CanAfford(give) || !state.Seats[i].Hand.CanAfford(get))
                    continue;
                double gain = HandValue(match, seat, After(hand, give, get)) - before - LeaderPremium(match, seat, i);
                // Our own terms are taken as agreed; a counter has to be worth it on its own.
                double floor = r.Kind == TradeResponseKind.Accepted ? 0.0 : 0.5;
                if (gain < floor)
                    continue;
                // Prefer the deal worth most, then the partner with the fewest points.
                gain -= 0.05 * state.Seats[i].PublicVictoryPoints;
                if (gain > bestGain)
                {
                    bestGain = gain;
                    best = i;
                    bestChoice = choice;
                }
            }

            return best >= 0
                ? MatchCommand.Confirm(seat, best, bestChoice)
                : MatchCommand.Simple(MatchCommandType.CancelTrade, seat);
        }

        /// <summary>True while an AI's offer waits for a human's answer (the AI must not act until then).</summary>
        public static bool AwaitingHumanAnswer(MatchController match)
        {
            var trade = match.State.ActiveTrade;
            if (trade == null || !match.State.Seats[trade.From].IsAi)
                return false;
            for (int i = 0; i < match.State.Seats.Count; i++)
                if (i != trade.From && !match.State.Seats[i].IsAi && trade.Responses[i].Kind == TradeResponseKind.Pending)
                    return true;
            return false;
        }

        // ---------- knights ----------

        /// <summary>
        /// v142 knights, the way players use them: before rolling when the robber sits on our production; any time
        /// to take or defend Largest Army; after rolling to go after a leader who is pulling away, or simply to
        /// put a card to work when we hold two or more.
        /// </summary>
        public static MatchCommand ChooseKnight(MatchController match, int seat, bool preRoll)
        {
            if (!match.CanPlayDevCard(seat, MatchDevCard.Knight))
                return null;
            var state = match.State;
            var me = state.Seats[seat];
            if (RobberBlocks(state, seat))
                return MatchCommand.Simple(MatchCommandType.PlayKnight, seat);

            int? army = state.LargestArmySeat;
            int best = state.Seats.Where(s => s.Index != seat).Select(s => s.KnightsPlayed).DefaultIfEmpty(0).Max();
            bool take = army != seat && me.KnightsPlayed + 1 >= MatchController.LargestArmyMinimum
                        && (!army.HasValue || me.KnightsPlayed + 1 > state.Seats[army.Value].KnightsPlayed);
            bool defend = army == seat && best >= me.KnightsPlayed - 1;
            // Two short of the army with a rival on it: start building it.
            bool chase = army.HasValue && army != seat && me.KnightsPlayed + 2 >= state.Seats[army.Value].KnightsPlayed;
            if (take || defend)
                return MatchCommand.Simple(MatchCommandType.PlayKnight, seat);
            if (preRoll)
                return null;

            int knights = me.DevCards.Count(c => c == MatchDevCard.Knight);
            int leader = Leader(state, seat);
            var lead = state.Seats[leader];
            bool runaway = lead.PublicVictoryPoints >= me.PublicVictoryPoints + 2
                           || lead.PublicVictoryPoints >= state.Config.VictoryPoints - 3;
            bool robberOnLeader = state.Board.RobberTile.HasValue && TouchesSeat(state, state.Board.RobberTile.Value, leader);
            if ((runaway && !robberOnLeader) || knights >= 2 || chase)
                return MatchCommand.Simple(MatchCommandType.PlayKnight, seat);
            return null;
        }

        private static bool RobberBlocks(MatchState state, int seat)
        {
            if (!state.Board.RobberTile.HasValue || !state.Board.TryGetTile(state.Board.RobberTile.Value, out var tile)
                || tile.IsDesert)
                return false;
            return TouchesSeat(state, tile.Coord, seat);
        }

        private static bool TouchesSeat(MatchState state, HexCoord hex, int seat)
        {
            for (int c = 0; c < 6; c++)
            {
                var v = VertexGraph.Canonicalize(new Vertex(hex, c));
                if (state.Board.VertexBuildings.TryGetValue(v, out var b) && MatchSeats.ToSeat(b.owner) == seat)
                    return true;
            }

            return false;
        }
    }
}
