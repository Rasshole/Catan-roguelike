using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Map;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// Fishermen of Catan in pass &amp; play (setup-screen toggle). v143: six fishing grounds sit in the sea along the
    /// coast with number tokens. v146 (official rules): the desert becomes a lake that fishes on 2, 3, 11 and 12; each
    /// building on a rolled ground / the lake draws a fish token (1–3 fish, a city two tokens) from a shared bag that
    /// also hides the old boot (its holder needs one extra VP and may pass it on). Fish buy: 2 robber off the board,
    /// 3 steal a card, 4 a card of your choice (<see cref="FishPerCard"/>), 5 a free road or ship, 7 a dev card.
    /// </summary>
    public sealed partial class MatchController
    {
        public const int FishPerCard = 4;

        /// <summary>Coast corners each fishing ground feeds (deterministic order).</summary>
        public static Vertex[] FishingCorners(BoardState board, Hex.HexCoord ground)
        {
            if (!SeaFrameSlotLayout.TryGetFishShoalPlacement(board, ground, 1f, out var p))
                return System.Array.Empty<Vertex>();
            return new[] { p.BorderVertexInland, p.BorderVertexLeft, p.BorderVertexRight }
                .Select(VertexGraph.Canonicalize).Distinct().ToArray();
        }

        public bool CanFishForCard(int seat) =>
            State.Config.Fishermen && seat == State.CurrentSeat && State.Phase == MatchPhase.Main
            && State.Seats[seat].Fish >= FishPerCard;

        // ---------- v146: official Fishermen of Catan ----------

        /// <summary>The lake (where the desert was) catches fish on these rolls.</summary>
        public static readonly int[] LakeNumbers = { 2, 3, 11, 12 };

        /// <summary>Spend options by cost (official): 2 robber off, 3 steal, 4 a card, 5 a road/ship, 7 a dev card.</summary>
        public static readonly int[] FishSpendCosts = { 2, 3, 4, 5, 7 };

        public const int FishSpendRobberOff = 2, FishSpendSteal = 3, FishSpendCard = 4, FishSpendRoad = 5, FishSpendDevCard = 7;

        /// <summary>Official token mix: 11 one-fish, 10 two-fish and 8 three-fish tokens, plus the old boot (0).</summary>
        public static int[] FishTokenSet()
        {
            var list = new List<int>();
            list.AddRange(Enumerable.Repeat(1, 11));
            list.AddRange(Enumerable.Repeat(2, 10));
            list.AddRange(Enumerable.Repeat(3, 8));
            list.Add(0);
            return list.ToArray();
        }

        /// <summary>The seat needs this many VP to win (one more while holding the old boot).</summary>
        public int VictoryTarget(int seat) => State.Config.VictoryPoints + (State.OldBootSeat == seat ? 1 : 0);

        private void SetUpFishermen()
        {
            State.FishRng = new MatchRng(State.Config.Seed, 0xF15E);
            RefillFishBag();
            // The robber starts off the board: the lake replaced its desert and it may never stand there.
            if (State.Board.LakeHex.HasValue && State.Board.RobberTile == State.Board.LakeHex)
                State.Board.ClearRobber();
        }

        private void RefillFishBag()
        {
            State.FishBag.Clear();
            State.FishBag.AddRange(FishTokenSet().Where(v => v != 0 || State.OldBootSeat < 0));
            for (int i = State.FishBag.Count - 1; i > 0; i--)
            {
                int j = State.FishRng.Next(i + 1);
                (State.FishBag[i], State.FishBag[j]) = (State.FishBag[j], State.FishBag[i]);
            }
        }

        /// <summary>Corners of the lake hex (buildings there fish on 2, 3, 11 and 12).</summary>
        public Vertex[] LakeCorners()
        {
            if (!State.Board.LakeHex.HasValue)
                return System.Array.Empty<Vertex>();
            var lake = State.Board.LakeHex.Value;
            return Enumerable.Range(0, 6).Select(c => VertexGraph.Canonicalize(new Vertex(lake, c))).Distinct().ToArray();
        }

        public bool CanSpendFish(int seat, int cost)
        {
            if (!State.Config.Fishermen || seat != State.CurrentSeat || State.Phase != MatchPhase.Main
                || State.ActiveTrade != null || State.Seats[seat].Fish < cost)
                return false;
            return cost switch
            {
                FishSpendRobberOff => State.Board.RobberTile.HasValue,
                FishSpendSteal => State.Seats.Any(o => o.Index != seat && o.CardCount > 0),
                FishSpendCard => true,
                FishSpendRoad => FreePiecesInSupply(seat) > 0 && AnyFreePieceSpot(seat),
                FishSpendDevCard => State.DevDeck.Count > 0,
                _ => false
            };
        }

        /// <summary>The old-boot holder may hand it to a player with at least as many visible VP (own turn, main).</summary>
        public bool CanPassOldBoot(int seat, int to) =>
            State.Config.Fishermen && State.OldBootSeat == seat && seat == State.CurrentSeat
            && State.Phase == MatchPhase.Main && to >= 0 && to < State.Seats.Count && to != seat
            && State.Seats[to].PublicVictoryPoints >= State.Seats[seat].PublicVictoryPoints;

        private int DrawFishToken(int seat)
        {
            if (State.FishBag.Count == 0)
                RefillFishBag();
            if (State.FishBag.Count == 0)
                return 0;
            int token = State.FishBag[State.FishBag.Count - 1];
            State.FishBag.RemoveAt(State.FishBag.Count - 1);
            if (token == 0)
            {
                State.OldBootSeat = seat;
                AppendLog($"{State.Seats[seat].Name} fished up the old boot (needs 1 more VP to win).");
            }

            return token;
        }

        private void ProduceFish(int roll)
        {
            // Each building on a fishing ground / the lake draws one token (a city two), in board order.
            var draws = new List<int>();
            foreach (var kv in State.Board.SeaFishGrounds.OrderBy(k => k.Key.Q).ThenBy(k => k.Key.R))
            {
                var ground = kv.Value;
                if (!ground.IsFishTile || ground.NumberToken != roll)
                    continue;
                AddFishDraws(draws, FishingCorners(State.Board, kv.Key));
            }

            if (LakeNumbers.Contains(roll))
                AddFishDraws(draws, LakeCorners());

            var caught = new int[State.Seats.Count];
            foreach (int seat in draws)
                caught[seat] += DrawFishToken(seat);
            for (int i = 0; i < caught.Length; i++)
            {
                if (caught[i] <= 0)
                    continue;
                State.Seats[i].Fish += caught[i];
                AppendLog($"{State.Seats[i].Name} caught {caught[i]} fish.");
            }
        }

        private void AddFishDraws(List<int> draws, IEnumerable<Vertex> corners)
        {
            foreach (var v in corners)
            {
                if (!State.Board.VertexBuildings.TryGetValue(v, out var b) || b.type == BuildingType.None)
                    continue;
                int seat = MatchSeats.ToSeat(b.owner);
                draws.Add(seat);
                if (b.type == BuildingType.City)
                    draws.Add(seat);
            }
        }

        private MatchCommandResult SpendFish(MatchCommand c)
        {
            if (!State.Config.Fishermen)
                return MatchCommandResult.Rejected("Fishermen is off for this match.");
            if (c.Seat != State.CurrentSeat || State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Spend fish on your own turn, after rolling.");
            if (State.ActiveTrade != null)
                return MatchCommandResult.Rejected("Close the trade offer first.");
            int cost = c.Pick;
            if (!FishSpendCosts.Contains(cost))
                return MatchCommandResult.Rejected("Pick a fish action (2, 3, 4, 5 or 7).");
            var seat = State.Current;
            if (seat.Fish < cost)
                return MatchCommandResult.Rejected($"You need {cost} fish.");
            if (cost == FishSpendCard)
                return FishForCard(c);
            if (!CanSpendFish(c.Seat, cost))
                return MatchCommandResult.Rejected("That fish action isn't possible now.");
            switch (cost)
            {
                case FishSpendRobberOff:
                    seat.Fish -= cost;
                    State.Board.ClearRobber();
                    AppendLog($"{seat.Name} paid {cost} fish to chase the robber off the board.");
                    break;
                case FishSpendSteal:
                    if (c.Victim < 0 || c.Victim >= State.Seats.Count || c.Victim == c.Seat || State.Seats[c.Victim].CardCount == 0)
                        return MatchCommandResult.Rejected("Pick a player with cards to steal from.");
                    seat.Fish -= cost;
                    StealOne(c.Seat, c.Victim, $"{cost} fish");
                    break;
                case FishSpendRoad:
                    seat.Fish -= cost;
                    State.ResumePhase = MatchPhase.Main;
                    State.FreeRoadsLeft = 1;
                    State.Phase = MatchPhase.RoadBuilding;
                    AppendLog($"{seat.Name} paid {cost} fish for a free road.");
                    break;
                case FishSpendDevCard:
                {
                    seat.Fish -= cost;
                    var card = State.DevDeck[State.DevDeck.Count - 1];
                    State.DevDeck.RemoveAt(State.DevDeck.Count - 1);
                    seat.NewDevCards.Add(card);
                    AppendLog($"{seat.Name} paid {cost} fish for a development card.");
                    AfterBuild();
                    break;
                }
            }

            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult PassOldBoot(MatchCommand c)
        {
            if (!CanPassOldBoot(c.Seat, c.Victim))
                return MatchCommandResult.Rejected("Pass the old boot on your turn to a player with as many VP or more.");
            State.OldBootSeat = c.Victim;
            AppendLog($"{State.Seats[c.Seat].Name} passed the old boot to {State.Seats[c.Victim].Name}.");
            RefreshVictoryPoints();
            return MatchCommandResult.Accepted;
        }

        private MatchCommandResult FishForCard(MatchCommand c)
        {
            if (!State.Config.Fishermen)
                return MatchCommandResult.Rejected("Fishermen is off for this match.");
            if (c.Seat != State.CurrentSeat || State.Phase != MatchPhase.Main)
                return MatchCommandResult.Rejected("Trade fish on your own turn, after rolling.");
            var get = c.Request;
            if (!IsValidBundle(get) || get.Total != 1)
                return MatchCommandResult.Rejected("Pick one card.");
            var seat = State.Current;
            if (seat.Fish < FishPerCard)
                return MatchCommandResult.Rejected($"You need {FishPerCard} fish.");
            seat.Fish -= FishPerCard;
            var hand = seat.Hand;
            hand.Add(get);
            seat.Hand = hand;
            AppendLog($"{seat.Name} traded {FishPerCard} fish for a card.");
            return MatchCommandResult.Accepted;
        }
    }
}
