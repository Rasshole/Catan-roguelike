using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Yield;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v141 Phase 4 shenanigans (party rules only): secret-ballot decrees, the Night Market (sealed-bid, second-price
    /// auctions of rare items), bounties claimed with the robber, face-down sabotage cards and the Chaos event.
    /// Everything is a plain serializable <see cref="MatchCommand"/>; every random draw uses <see cref="MatchState.PartyRng"/>.
    /// A night is the round boundary, a day is one round, an act is <see cref="ActLength"/> rounds.
    /// </summary>
    public sealed partial class MatchController
    {
        public const int ActLength = 5;
        public const int MaxBountyReward = 3;
        public const int MaxSabotageHeld = 2;
        public const int SabotageCopies = 4;
        public static readonly ResourceBundle SabotageCost = new() { Wheat = 1, Stone = 1 };

        // ---------- night ----------

        /// <summary>Start of a new round (before the event card): yesterday's decree and washed-out roads end; at
        /// the end of an act, unclaimed bounties go back to their posters.</summary>
        private void BeginNight()
        {
            State.ActiveDecree = MatchDecreeId.None;
            if (State.WashedRoads.Count > 0)
            {
                foreach (var w in State.WashedRoads)
                    State.Board.DisabledRoads.Remove(w.Edge);
                State.WashedRoads.Clear();
                AppendLog("The washed-out roads are repaired.");
                RefreshVictoryPoints();
            }

            if (State.Round % ActLength == 0 && State.Bounties.Count > 0)
            {
                foreach (var b in State.Bounties)
                {
                    var poster = State.Seats[b.Poster];
                    var h = poster.Hand;
                    h.Add(b.Reward);
                    poster.Hand = h;
                }

                State.Bounties.Clear();
                AppendLog("End of the act: unclaimed bounties return to their posters.");
            }
        }

        // ---------- secret ballot ----------

        private void BeginVote()
        {
            var decrees = new[] { MatchDecreeId.PortsThreeToOne, MatchDecreeId.NoDesertRobber, MatchDecreeId.RichestDiscardsToSeven };
            State.BallotDecree = decrees[State.PartyRng.Next(decrees.Length)];
            State.Votes.Clear();
            for (int i = 0; i < State.Seats.Count; i++)
                State.Votes.Add(-1);
            BeginBallotOrder(MatchPhase.Vote);
            AppendLog($"Decree on the ballot: {DecreeText(State.BallotDecree)} Everyone votes in secret.");
        }

        private void BeginBallotOrder(MatchPhase phase)
        {
            State.PickOrder.Clear();
            for (int i = 0; i < State.Seats.Count; i++)
                State.PickOrder.Add(i);
            State.PickStep = 0;
            State.Phase = phase;
        }

        public static string DecreeName(MatchDecreeId id) => id switch
        {
            MatchDecreeId.PortsThreeToOne => "Harbour Tariff",
            MatchDecreeId.NoDesertRobber => "Desert Watch",
            MatchDecreeId.RichestDiscardsToSeven => "Wealth Tax",
            _ => "Decree"
        };

        public static string DecreeText(MatchDecreeId id) => id switch
        {
            MatchDecreeId.PortsThreeToOne => $"Harbours close today; players without one holding {HarbourTariffHandCap} or fewer cards take 1.",
            MatchDecreeId.NoDesertRobber => "The robber can't enter a desert today.",
            MatchDecreeId.RichestDiscardsToSeven => $"Everyone holding at least the average hand ({WealthTaxFloor}+ cards) pays 1 card.",
            _ => ""
        };

        private MatchCommandResult CastVote(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Vote)
                return MatchCommandResult.Rejected("No ballot is open.");
            if (c.Pick != 0 && c.Pick != 1)
                return MatchCommandResult.Rejected("Vote aye or nay.");
            State.Votes[c.Seat] = c.Pick;
            State.PickStep++;
            if (State.PickStep >= State.PickOrder.Count)
                TallyVote();
            return MatchCommandResult.Accepted;
        }

        /// <summary>Seat that breaks ties (ballots and auctions): fewest public VP, then fewest cards, then lowest seat.</summary>
        public int TieBreakSeat(IEnumerable<int> among) =>
            among.OrderBy(i => State.Seats[i].PublicVictoryPoints).ThenBy(i => State.Seats[i].CardCount).ThenBy(i => i).First();

        private void TallyVote()
        {
            int yes = State.Votes.Count(v => v == 1);
            int no = State.Votes.Count(v => v == 0);
            int tieBreaker = -1;
            bool passed;
            if (yes == no)
            {
                tieBreaker = TieBreakSeat(Enumerable.Range(0, State.Seats.Count));
                passed = State.Votes[tieBreaker] == 1;
            }
            else
                passed = yes > no;

            var decree = State.BallotDecree;
            State.LastBallot = new MatchBallotResult
            {
                Decree = decree, Yes = yes, No = no, Passed = passed, TieBreaker = tieBreaker, Round = State.Round
            };
            State.Votes.Clear();
            State.BallotDecree = MatchDecreeId.None;
            State.PickOrder.Clear();
            State.PickStep = 0;
            State.Phase = MatchPhase.Roll;
            string tie = tieBreaker >= 0 ? $" (tie: {State.Seats[tieBreaker].Name}, with the fewest points, decides)" : "";
            AppendLog($"Ballot: {yes} aye, {no} nay{tie} – {DecreeName(decree)} {(passed ? "passes" : "fails")}.");
            if (!passed)
                return;

            if (decree == MatchDecreeId.RichestDiscardsToSeven)
            {
                // v142: the richer half of the table pays 1 card each (their most plentiful).
                foreach (int i in WealthTaxPayers().ToList())
                {
                    var seat = State.Seats[i];
                    var h = seat.Hand;
                    var r = MostHeld(h);
                    h.Add(r, -1);
                    seat.Hand = h;
                    AppendLog($"{seat.Name} pays 1 {r} in tax.");
                }
            }
            else
            {
                State.ActiveDecree = decree;
                if (decree == MatchDecreeId.PortsThreeToOne)
                {
                    // v142: the harbours close for the day; everyone without one takes a card.
                    foreach (var seat in State.Seats.Where(s => HarbourTariffPays(s.Index)).ToList())
                    {
                        var r = LeastHeld(seat.Hand);
                        Give(seat, r, 1);
                        AppendLog($"{seat.Name} takes 1 {r}.");
                    }
                }
            }
        }

        /// <summary>v142 Wealth Tax: nobody with fewer cards pays.</summary>
        public const int WealthTaxFloor = 4;

        /// <summary>v142: seats the Wealth Tax would hit — hands at or above the table average (and at least
        /// <see cref="WealthTaxFloor"/> cards), so roughly half the table pays and the vote is contested.</summary>
        public IEnumerable<int> WealthTaxPayers()
        {
            double avg = State.Seats.Average(s => (double)s.CardCount);
            return State.Seats.Where(s => s.CardCount >= WealthTaxFloor && s.CardCount >= avg).Select(s => s.Index);
        }

        /// <summary>v142 Harbour Tariff: only small hands without a harbour get the card.</summary>
        public const int HarbourTariffHandCap = 4;

        public bool HarbourTariffPays(int seat) =>
            !OwnsHarbour(seat) && State.Seats[seat].CardCount <= HarbourTariffHandCap;

        /// <summary>v142: whether <paramref name="seat"/> has a settlement or city on any harbour.</summary>
        public bool OwnsHarbour(int seat)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            return State.Ports.Any(p => PortAccess.PlayerControlsPort(State.Board, owner, p));
        }

        // ---------- Night Market (sealed-bid, second-price) ----------

        private void BeginAuction()
        {
            // Rare items only: token reroll (twice as likely), a sabotage card, a Victory Point card.
            int roll = State.PartyRng.Next(4);
            State.AuctionItem = roll < 2 ? MatchAuctionItem.TokenReroll
                : roll == 2 ? MatchAuctionItem.SabotageCard : MatchAuctionItem.VictoryPointCard;
            State.Bids.Clear();
            for (int i = 0; i < State.Seats.Count; i++)
                State.Bids.Add(ResourceBundle.Zero);
            BeginBallotOrder(MatchPhase.Auction);
            AppendLog($"Night Market: {AuctionItemName(State.AuctionItem)} goes to sealed bids.");
        }

        /// <summary>v142: minimum bid (and price) per item — a VP card 3, a sabotage card 2, a reroll 1.</summary>
        public static int ReservePrice(MatchAuctionItem item) => item switch
        {
            MatchAuctionItem.VictoryPointCard => 3,
            MatchAuctionItem.SabotageCard => 2,
            _ => 1
        };

        public static string AuctionItemName(MatchAuctionItem item) => item switch
        {
            MatchAuctionItem.TokenReroll => "a token reroll",
            MatchAuctionItem.SabotageCard => "a sabotage card",
            MatchAuctionItem.VictoryPointCard => "a Victory Point card",
            _ => "nothing"
        };

        private MatchCommandResult PlaceBid(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Auction)
                return MatchCommandResult.Rejected("No auction is open.");
            if (!IsValidBundle(c.Offer))
                return MatchCommandResult.Rejected("Invalid bid.");
            if (!State.Seats[c.Seat].Hand.CanAfford(c.Offer))
                return MatchCommandResult.Rejected("You don't have those cards.");
            int reserve = ReservePrice(State.AuctionItem);
            if (c.Offer.Total > 0 && c.Offer.Total < reserve)
                return MatchCommandResult.Rejected($"The minimum bid is {reserve} cards.");
            State.Bids[c.Seat] = c.Offer;
            State.PickStep++;
            if (State.PickStep >= State.PickOrder.Count)
                ResolveAuction();
            return MatchCommandResult.Accepted;
        }

        private void ResolveAuction()
        {
            var totals = State.Bids.Select(b => b.Total).ToList();
            var bidders = Enumerable.Range(0, totals.Count).Where(i => totals[i] > 0).ToList();
            var item = State.AuctionItem;
            var result = new MatchAuctionResult { Item = item, Bidders = bidders.Count, Round = State.Round };
            if (bidders.Count == 0)
                AppendLog($"Night Market: no bids – {AuctionItemName(item)} goes back.");
            else
            {
                int top = bidders.Max(i => totals[i]);
                int winner = TieBreakSeat(bidders.Where(i => totals[i] == top));
                int second = bidders.Where(i => i != winner).Select(i => totals[i]).DefaultIfEmpty(0).Max();
                int price = System.Math.Max(ReservePrice(item), second);
                var seat = State.Seats[winner];
                var h = seat.Hand;
                h.Pay(SuggestDiscard(State.Bids[winner], price));
                seat.Hand = h;
                result.Winner = winner;
                result.Price = price;
                switch (item)
                {
                    case MatchAuctionItem.TokenReroll:
                        seat.Rerolls++;
                        break;
                    case MatchAuctionItem.SabotageCard:
                        seat.Sabotage.Add(DrawSabotage());
                        break;
                    case MatchAuctionItem.VictoryPointCard:
                        seat.DevCards.Add(MatchDevCard.VictoryPoint);
                        RefreshVictoryPoints();
                        break;
                }

                AppendLog($"Night Market: {seat.Name} wins {AuctionItemName(item)} for {price} card{(price == 1 ? "" : "s")}.");
            }

            State.LastAuction = result;
            State.Bids.Clear();
            State.AuctionItem = MatchAuctionItem.None;
            State.PickOrder.Clear();
            State.PickStep = 0;
            State.Phase = MatchPhase.Roll;
        }

        /// <summary>Tiles whose token <paramref name="seat"/> may reroll (numbered, touching one of its buildings).</summary>
        public IEnumerable<HexCoord> RerollableTiles(int seat)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            foreach (var tile in State.Board.Tiles.Values.OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R))
            {
                if (tile.IsDesert || !tile.NumberToken.HasValue)
                    continue;
                for (int c = 0; c < 6; c++)
                {
                    var v = VertexGraph.Canonicalize(new Vertex(tile.Coord, c));
                    if (State.Board.VertexBuildings.TryGetValue(v, out var b) && b.owner == owner)
                    {
                        yield return tile.Coord;
                        break;
                    }
                }
            }
        }

        private MatchCommandResult RerollToken(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            var seat = State.Seats[c.Seat];
            if (seat.Rerolls <= 0)
                return MatchCommandResult.Rejected("You have no token reroll.");
            if (!RerollableTiles(c.Seat).Contains(c.Hex))
                return MatchCommandResult.Rejected("Pick a numbered tile next to one of your buildings.");
            var tile = State.Board.Tiles[c.Hex];
            int old = tile.NumberToken.Value;
            bool redNeighbour = false;
            for (int d = 0; d < 6; d++)
                if (State.Board.TryGetTile(HexMath.GetNeighbor(c.Hex, d), out var n) && n.NumberToken.HasValue
                    && NumberTokenLibrary.IsRedNumber(n.NumberToken.Value))
                    redNeighbour = true;
            // Drawn from the classic token pool; a 6/8 never lands next to another 6/8.
            var pool = NumberTokenLibrary.ClassicPool.Where(v => v != old && !(redNeighbour && NumberTokenLibrary.IsRedNumber(v))).ToList();
            int value = pool[State.PartyRng.Next(pool.Count)];
            tile.NumberToken = value;
            seat.Rerolls--;
            AppendLog($"{seat.Name} rerolled a token: {old} → {value}.");
            return MatchCommandResult.Accepted;
        }

        // ---------- bounties ----------

        public MatchBounty BountyBy(int poster) => State.Bounties.FirstOrDefault(b => b.Poster == poster);

        public IEnumerable<MatchBounty> BountiesOn(int target) => State.Bounties.Where(b => b.Target == target);

        private MatchCommandResult PostBounty(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            if (c.Victim < 0 || c.Victim >= State.Seats.Count || c.Victim == c.Seat)
                return MatchCommandResult.Rejected("Post the bounty on another player.");
            if (!IsValidBundle(c.Offer) || c.Offer.Total < 1 || c.Offer.Total > MaxBountyReward)
                return MatchCommandResult.Rejected($"A bounty pays 1–{MaxBountyReward} cards.");
            if (BountyBy(c.Seat) != null)
                return MatchCommandResult.Rejected("You already have a bounty up.");
            if (!TryPay(c.Offer))
                return MatchCommandResult.Rejected("You don't have those cards.");
            State.Bounties.Add(new MatchBounty { Poster = c.Seat, Target = c.Victim, Reward = c.Offer, Round = State.Round });
            AppendLog($"{State.Seats[c.Seat].Name} posts a bounty of {c.Offer.Total} on {State.Seats[c.Victim].Name}.");
            return MatchCommandResult.Accepted;
        }

        /// <summary>The robber stole from <paramref name="victim"/>: the mover collects every bounty on them (not their own).</summary>
        private void ClaimBounties(int mover, int victim)
        {
            foreach (var b in State.Bounties.Where(b => b.Target == victim && b.Poster != mover).ToList())
            {
                var me = State.Seats[mover];
                var h = me.Hand;
                h.Add(b.Reward);
                me.Hand = h;
                State.Bounties.Remove(b);
                AppendLog($"{me.Name} claims {State.Seats[b.Poster].Name}'s bounty on {State.Seats[victim].Name} (+{b.Reward.Total}).");
            }
        }

        // ---------- sabotage ----------

        public static string SabotageName(MatchSabotageKind kind) => kind switch
        {
            MatchSabotageKind.Blight => "Blight",
            MatchSabotageKind.WashedOutRoad => "Washed-out Road",
            MatchSabotageKind.SpoiledGrain => "Spoiled Grain",
            _ => "Sabotage"
        };

        public static string SabotageText(MatchSabotageKind kind) => kind switch
        {
            MatchSabotageKind.Blight => "One hex produces nothing for the target on their next roll.",
            MatchSabotageKind.WashedOutRoad => "One of the target's roads doesn't count until the night.",
            MatchSabotageKind.SpoiledGrain => "The target discards 1 Grain at their next dawn.",
            _ => ""
        };

        private MatchSabotageKind DrawSabotage()
        {
            if (State.SabotageDeck.Count == 0)
            {
                var all = new List<MatchSabotageKind>();
                foreach (var k in new[] { MatchSabotageKind.Blight, MatchSabotageKind.WashedOutRoad, MatchSabotageKind.SpoiledGrain })
                    for (int i = 0; i < SabotageCopies; i++)
                        all.Add(k);
                State.SabotageDeck.AddRange(Shuffled(all));
            }

            var card = State.SabotageDeck[State.SabotageDeck.Count - 1];
            State.SabotageDeck.RemoveAt(State.SabotageDeck.Count - 1);
            return card;
        }

        public bool CanBuySabotage(int seat) =>
            ShenanigansOn && seat == State.CurrentSeat && State.Phase == MatchPhase.Main
            && State.Seats[seat].Sabotage.Count < MaxSabotageHeld && State.Seats[seat].Hand.CanAfford(SabotageCost);

        public MatchSabotage PendingSabotageOn(int target) => State.PendingSabotage.FirstOrDefault(p => p.Target == target);

        private MatchCommandResult BuySabotage(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            if (State.Seats[c.Seat].Sabotage.Count >= MaxSabotageHeld)
                return MatchCommandResult.Rejected($"You can hold {MaxSabotageHeld} sabotage cards.");
            if (!TryPay(SabotageCost))
                return MatchCommandResult.Rejected("Can't afford a sabotage card.");
            State.Seats[c.Seat].Sabotage.Add(DrawSabotage());
            AppendLog($"{State.Seats[c.Seat].Name} bought a sabotage card.");
            return MatchCommandResult.Accepted;
        }

        /// <summary>Hexes a Blight on <paramref name="target"/> may hit (numbered, touching one of their buildings).</summary>
        public IEnumerable<HexCoord> BlightTiles(int target) => RerollableTiles(target);

        /// <summary>Roads of <paramref name="target"/> a Washed-out Road may hit.</summary>
        public IEnumerable<HexMath.Edge> WashableRoads(int target)
        {
            var owner = MatchSeats.ToPlayerId(target);
            return State.Board.Roads.Where(kv => kv.Value == owner && !State.Board.DisabledRoads.Contains(kv.Key))
                .Select(kv => kv.Key)
                .OrderBy(e => $"{e.A.Hex.Q},{e.A.Hex.R},{e.A.CornerIndex},{e.B.Hex.Q},{e.B.Hex.R},{e.B.CornerIndex}", System.StringComparer.Ordinal);
        }

        private MatchCommandResult PlaySabotage(MatchCommand c)
        {
            if (State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Roll first.");
            var author = State.Seats[c.Seat];
            var kind = (MatchSabotageKind)c.Pick;
            if (!author.Sabotage.Contains(kind))
                return MatchCommandResult.Rejected("You don't hold that card.");
            if (c.Victim < 0 || c.Victim >= State.Seats.Count || c.Victim == c.Seat)
                return MatchCommandResult.Rejected("Pick another player.");
            var target = State.Seats[c.Victim];
            switch (kind)
            {
                case MatchSabotageKind.Blight:
                case MatchSabotageKind.SpoiledGrain:
                {
                    if (PendingSabotageOn(c.Victim) != null)
                        return MatchCommandResult.Rejected("A face-down card already lies on that player.");
                    if (kind == MatchSabotageKind.Blight && !BlightTiles(c.Victim).Contains(c.Hex))
                        return MatchCommandResult.Rejected("Blight a numbered hex next to their buildings.");
                    State.PendingSabotage.Add(new MatchSabotage
                    {
                        Author = c.Seat, Target = c.Victim, Kind = kind,
                        Q = kind == MatchSabotageKind.Blight ? c.Q : 0, R = kind == MatchSabotageKind.Blight ? c.R : 0
                    });
                    // Anonymous: the author stays hidden until the card triggers (or is guessed).
                    AppendLog($"A face-down card lands on {target.Name}.");
                    break;
                }
                case MatchSabotageKind.WashedOutRoad:
                {
                    var e = NormalizeEdge(c.Edge);
                    var key = WashableRoads(c.Victim).Where(r => NormalizeEdge(r).Equals(e)).Cast<HexMath.Edge?>().FirstOrDefault();
                    if (!key.HasValue)
                        return MatchCommandResult.Rejected("Wash out one of their roads.");
                    State.Board.DisabledRoads.Add(key.Value);
                    State.WashedRoads.Add(new MatchWashedRoad { Edge = key.Value, Owner = c.Victim, Round = State.Round });
                    Reveal(kind, c.Seat, c.Victim, false);
                    AppendLog($"Washed-out Road! {author.Name} washed out a road of {target.Name} until the night.");
                    RefreshVictoryPoints();
                    break;
                }
                default:
                    return MatchCommandResult.Rejected("Unknown sabotage card.");
            }

            author.Sabotage.Remove(kind);
            return MatchCommandResult.Accepted;
        }

        private void Reveal(MatchSabotageKind kind, int author, int target, bool cancelled) =>
            State.LastSabotageReveal = new MatchSabotageReveal
            {
                Kind = kind, Author = author, Target = target, Cancelled = cancelled,
                Serial = (State.LastSabotageReveal?.Serial ?? 0) + 1
            };

        /// <summary>Whether <paramref name="seat"/> may name the author of the face-down card on them now.</summary>
        public bool CanGuessSaboteur(int seat)
        {
            var p = PendingSabotageOn(seat);
            return ShenanigansOn && p != null && !p.Guessed && seat == State.CurrentSeat && State.Phase == MatchPhase.Roll
                   && State.Seats[seat].CardCount > 0;
        }

        /// <summary>Hot-seat adaptation of the fish rule: pay 1 card to name the author; right = cancelled and unmasked.</summary>
        private MatchCommandResult GuessSaboteur(MatchCommand c)
        {
            if (!CanGuessSaboteur(c.Seat))
                return MatchCommandResult.Rejected("No face-down card to guess on you right now.");
            if (c.Victim < 0 || c.Victim >= State.Seats.Count || c.Victim == c.Seat)
                return MatchCommandResult.Rejected("Name another player.");
            if (!IsValidBundle(c.Offer) || c.Offer.Total != 1)
                return MatchCommandResult.Rejected("A guess costs exactly 1 card.");
            if (!TryPay(c.Offer))
                return MatchCommandResult.Rejected("You don't have that card.");
            var p = PendingSabotageOn(c.Seat);
            var me = State.Seats[c.Seat];
            if (p.Author == c.Victim)
            {
                State.PendingSabotage.Remove(p);
                Reveal(p.Kind, p.Author, p.Target, true);
                AppendLog($"{me.Name} unmasks {State.Seats[p.Author].Name}: the {SabotageName(p.Kind)} is cancelled.");
            }
            else
            {
                p.Guessed = true;
                AppendLog($"{me.Name} guessed {State.Seats[c.Victim].Name} – wrong. The card stays face-down.");
            }

            return MatchCommandResult.Accepted;
        }

        /// <summary>The current seat's dawn (just before their roll): a face-down card on them triggers and is revealed.</summary>
        private void OnDawnSabotage()
        {
            var p = PendingSabotageOn(State.CurrentSeat);
            if (p == null)
                return;
            State.PendingSabotage.Remove(p);
            var target = State.Current;
            string author = State.Seats[p.Author].Name;
            Reveal(p.Kind, p.Author, p.Target, false);
            if (p.Kind == MatchSabotageKind.SpoiledGrain)
            {
                bool lost = target.Hand.Wheat > 0;
                if (lost)
                    Give(target, ResourceType.Wheat, -1);
                AppendLog($"Spoiled Grain by {author}: {target.Name} {(lost ? "loses 1 Grain" : "has no Grain to lose")}.");
            }
            else if (p.Kind == MatchSabotageKind.Blight)
            {
                State.ActiveBlight = p;
                State.Board.TryGetTile(p.Hex, out var tile);
                AppendLog($"Blight by {author}: the {tile?.Resource} {tile?.NumberToken} produces nothing for {target.Name} this roll.");
            }
        }

        private void AfterRollSabotage() => State.ActiveBlight = null;

        // ---------- Chaos ----------

        private void DoChaos()
        {
            var tiles = State.Board.Tiles.Values.Where(t => t.Coord != State.Board.RobberTile && t.Coord != State.Board.LakeHex)
                .OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R).ToList();
            if (tiles.Count > 0)
                State.Board.PlaceRobber(tiles[State.PartyRng.Next(tiles.Count)].Coord);

            // Everyone picks one random card first, then all pass to the left at once.
            int n = State.Seats.Count;
            var picks = new ResourceType?[n];
            for (int i = 0; i < n; i++)
            {
                var hand = State.Seats[i].Hand;
                if (hand.Total == 0)
                    continue;
                int pick = State.PartyRng.Next(hand.Total);
                foreach (var r in AllResources)
                {
                    if (pick < hand[r])
                    {
                        picks[i] = r;
                        break;
                    }

                    pick -= hand[r];
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (!picks[i].HasValue)
                    continue;
                Give(State.Seats[i], picks[i].Value, -1);
                Give(State.Seats[(i + 1) % n], picks[i].Value, 1);
            }
        }
    }
}
