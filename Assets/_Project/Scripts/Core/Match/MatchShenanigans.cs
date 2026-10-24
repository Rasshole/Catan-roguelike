using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatanRoguelike.Core.Hex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Match
{
    /// <summary>v141: decrees put to a secret ballot (Decree event). Values are the wire format (append only).</summary>
    public enum MatchDecreeId
    {
        None = 0,
        /// <summary>Every harbour trades at 3:1 for the day (2:1 harbours lose their edge).</summary>
        PortsThreeToOne = 1,
        /// <summary>The robber can't be moved onto a desert for the day.</summary>
        NoDesertRobber = 2,
        /// <summary>Whoever holds the most cards (above 7) discards down to 7 at once.</summary>
        RichestDiscardsToSeven = 3
    }

    /// <summary>v141: rare items sold at the Night Market (sealed-bid, second price).</summary>
    public enum MatchAuctionItem
    {
        None = 0,
        /// <summary>The right to reroll the number token of one tile touching your own building.</summary>
        TokenReroll = 1,
        /// <summary>A face-down sabotage card from the deck.</summary>
        SabotageCard = 2,
        /// <summary>A rare Victory Point card (hidden VP, like the development card).</summary>
        VictoryPointCard = 3
    }

    /// <summary>v141: secret sabotage cards (bought or won, played face-down).</summary>
    public enum MatchSabotageKind
    {
        None = 0,
        /// <summary>One hex produces nothing for the target on their next roll.</summary>
        Blight = 1,
        /// <summary>One of the target's roads doesn't count (Longest Road, building) for the day.</summary>
        WashedOutRoad = 2,
        /// <summary>The target discards 1 Grain at their next dawn (before their roll).</summary>
        SpoiledGrain = 3
    }

    /// <summary>v141: a resource bounty posted on another seat; the reward sits in escrow until claimed or returned.</summary>
    [Serializable]
    public sealed class MatchBounty
    {
        public int Poster { get; set; }
        public int Target { get; set; }
        public ResourceBundle Reward { get; set; }
        public int Round { get; set; }
    }

    /// <summary>v141: a face-down sabotage card lying on a target (Blight / Spoiled Grain) until it triggers.</summary>
    [Serializable]
    public sealed class MatchSabotage
    {
        public int Author { get; set; }
        public int Target { get; set; }
        public MatchSabotageKind Kind { get; set; }
        public int Q { get; set; }
        public int R { get; set; }
        /// <summary>The target already spent their one guess on this card.</summary>
        public bool Guessed { get; set; }
        public HexCoord Hex => new(Q, R);
    }

    /// <summary>v141: a washed-out road (back at the next night).</summary>
    [Serializable]
    public sealed class MatchWashedRoad
    {
        public Edge Edge { get; set; }
        public int Owner { get; set; }
        public int Round { get; set; }
    }

    /// <summary>v141: the public result of the last ballot: only the totals and the outcome.</summary>
    public sealed class MatchBallotResult
    {
        public MatchDecreeId Decree { get; set; }
        public int Yes { get; set; }
        public int No { get; set; }
        public bool Passed { get; set; }
        /// <summary>Seat whose vote broke a tie (fewest VP), or -1.</summary>
        public int TieBreaker { get; set; } = -1;
        public int Round { get; set; }
    }

    /// <summary>v141: the public result of the last Night Market: winner and price (bids stay sealed).</summary>
    public sealed class MatchAuctionResult
    {
        public MatchAuctionItem Item { get; set; }
        public int Winner { get; set; } = -1;
        public int Price { get; set; }
        public int Bidders { get; set; }
        public int Round { get; set; }
    }

    /// <summary>v141: a sabotage card that was revealed (it triggered, or its author was guessed).</summary>
    public sealed class MatchSabotageReveal
    {
        public MatchSabotageKind Kind { get; set; }
        public int Author { get; set; }
        public int Target { get; set; }
        /// <summary>True when the target guessed the author and cancelled the card.</summary>
        public bool Cancelled { get; set; }
        /// <summary>Increments with every reveal, so the HUD can show each one once.</summary>
        public int Serial { get; set; }
    }

    public sealed partial class MatchSeatState
    {
        /// <summary>v141: token rerolls won at the Night Market and not used yet.</summary>
        public int Rerolls { get; set; }
        /// <summary>v141: secret sabotage cards in hand (only their owner sees them).</summary>
        public List<MatchSabotageKind> Sabotage { get; set; } = new();
    }

    public sealed partial class MatchState
    {
        /// <summary>v141: the decree on today's ballot (Vote phase).</summary>
        public MatchDecreeId BallotDecree { get; set; }
        /// <summary>v141: sealed votes per seat (-1 = not cast, 0 = nay, 1 = aye). Cleared after the tally.</summary>
        public List<int> Votes { get; } = new();
        /// <summary>v141: the decree in force for the rest of this round.</summary>
        public MatchDecreeId ActiveDecree { get; set; }
        public MatchBallotResult LastBallot { get; set; }

        /// <summary>v141: the item at tonight's Night Market (Auction phase).</summary>
        public MatchAuctionItem AuctionItem { get; set; }
        /// <summary>v141: sealed bids per seat (cards offered; empty = pass). Cleared after the auction.</summary>
        public List<ResourceBundle> Bids { get; } = new();
        public MatchAuctionResult LastAuction { get; set; }

        public List<MatchBounty> Bounties { get; } = new();
        public List<MatchSabotage> PendingSabotage { get; } = new();
        public List<MatchSabotageKind> SabotageDeck { get; } = new();
        public List<MatchWashedRoad> WashedRoads { get; } = new();
        public MatchSabotageReveal LastSabotageReveal { get; set; }
        /// <summary>v141: Blight in effect for the current roll (target seat + hex), set only while producing.</summary>
        public MatchSabotage ActiveBlight { get; set; }

        private void AppendShenanigans(StringBuilder sb)
        {
            static string B(ResourceBundle b) => $"{b.Wood}.{b.Brick}.{b.Wheat}.{b.Sheep}.{b.Stone}";
            sb.Append("V").Append((int)BallotDecree).Append(':').Append(string.Join(",", Votes))
                .Append(':').Append((int)ActiveDecree)
                .Append(":A").Append((int)AuctionItem).Append(':').Append(string.Join(",", Bids.Select(B)))
                .Append(":K").Append(string.Join(",", SabotageDeck.Select(k => (int)k))).Append('|');
            foreach (var b in Bounties)
                sb.Append($"BO{b.Poster}>{b.Target}:{B(b.Reward)}@{b.Round};");
            foreach (var p in PendingSabotage)
                sb.Append($"SB{p.Author}>{p.Target}:{(int)p.Kind}@{p.Q},{p.R}{(p.Guessed ? "g" : "")};");
            foreach (var w in WashedRoads)
                sb.Append($"WR{w.Edge.A.Hex.Q},{w.Edge.A.Hex.R},{w.Edge.A.CornerIndex}-{w.Edge.B.Hex.Q},{w.Edge.B.Hex.R},{w.Edge.B.CornerIndex}/{w.Owner}@{w.Round};");
            foreach (var s in Seats)
                sb.Append($"Z{s.Index}:{s.Rerolls}/{string.Join("", s.Sabotage.Select(k => (int)k))};");
        }
    }
}
