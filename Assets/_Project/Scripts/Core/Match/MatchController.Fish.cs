using System.Linq;
using CatanRoguelike.Core.Map;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Match
{
    /// <summary>
    /// v143 Fishermen of Catan in pass &amp; play (setup-screen toggle). Six fishing grounds sit in the sea along the
    /// coast with number tokens and the desert becomes a lake (no yield). When a fishing ground's number is rolled,
    /// every settlement on its three coast corners catches 1 fish (a city 2). Fish are public and buy a card from
    /// the bank at <see cref="FishPerCard"/> fish each (main phase, any number of times).
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

        private void ProduceFish(int roll)
        {
            var caught = new int[State.Seats.Count];
            foreach (var kv in State.Board.SeaFishGrounds.OrderBy(k => k.Key.Q).ThenBy(k => k.Key.R))
            {
                var ground = kv.Value;
                if (!ground.IsFishTile || ground.NumberToken != roll)
                    continue;
                foreach (var v in FishingCorners(State.Board, kv.Key))
                {
                    if (!State.Board.VertexBuildings.TryGetValue(v, out var b) || b.type == BuildingType.None)
                        continue;
                    caught[MatchSeats.ToSeat(b.owner)] += b.type == BuildingType.City ? 2 : 1;
                }
            }

            for (int i = 0; i < caught.Length; i++)
            {
                if (caught[i] <= 0)
                    continue;
                State.Seats[i].Fish += caught[i];
                AppendLog($"{State.Seats[i].Name} caught {caught[i]} fish.");
            }
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
