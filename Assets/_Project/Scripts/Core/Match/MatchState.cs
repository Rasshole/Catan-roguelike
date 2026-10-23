using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Match
{
    public enum MatchPhase
    {
        SetupSettlement = 0,
        SetupRoad = 1,
        Roll = 2,
        MoveRobber = 3,
        Main = 4,
        GameOver = 5,
        /// <summary>v135: a 7 was rolled and some seats (see <see cref="MatchState.PendingDiscards"/>) must discard.</summary>
        Discard = 6,
        /// <summary>v135: Road Building dev card — place up to <see cref="MatchState.FreeRoadsLeft"/> free roads.</summary>
        RoadBuilding = 7
    }

    /// <summary>v135: classic development cards.</summary>
    public enum MatchDevCard
    {
        Knight = 0,
        VictoryPoint = 1,
        RoadBuilding = 2,
        YearOfPlenty = 3,
        Monopoly = 4
    }

    public enum TradeResponseKind
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2,
        Countered = 3
    }

    /// <summary>One seat's answer on the trade board. A counter is stored from the OFFERER's point of view.</summary>
    [Serializable]
    public sealed class MatchTradeResponse
    {
        public TradeResponseKind Kind { get; set; }
        public ResourceBundle CounterGive { get; set; }
        public ResourceBundle CounterGet { get; set; }
    }

    /// <summary>v135: the open player-to-player offer (offer/counter-offer board). Give/Get are the offerer's view.</summary>
    [Serializable]
    public sealed class MatchTradeOffer
    {
        public int From { get; set; }
        public ResourceBundle Give { get; set; }
        public ResourceBundle Get { get; set; }
        public List<MatchTradeResponse> Responses { get; set; } = new();
    }

    /// <summary>Per-seat state of an N-player match.</summary>
    [Serializable]
    public sealed class MatchSeatState
    {
        public int Index { get; set; }
        public SeatKind Kind { get; set; }
        public string Name { get; set; } = "";
        public ResourceBundle Hand { get; set; }
        public int VictoryPoints { get; set; }
        public int ExtraTimeCharges { get; set; } = TurnClock.StartingCharges;
        public bool ExtraTimeUsedThisTurn { get; set; }

        /// <summary>v135: playable development cards (hidden from other seats).</summary>
        public List<MatchDevCard> DevCards { get; set; } = new();
        /// <summary>v135: cards bought this turn — not playable until the next turn.</summary>
        public List<MatchDevCard> NewDevCards { get; set; } = new();
        public int KnightsPlayed { get; set; }
        public bool PlayedDevCardThisTurn { get; set; }

        /// <summary>Hidden VP from Victory Point cards (counted for winning, not shown to others).</summary>
        public int HiddenVictoryPoints =>
            DevCards.Count(c => c == MatchDevCard.VictoryPoint) + NewDevCards.Count(c => c == MatchDevCard.VictoryPoint);
        /// <summary>VP everyone can see (buildings, longest road, largest army).</summary>
        public int PublicVictoryPoints => VictoryPoints - HiddenVictoryPoints;
        public int DevCardCount => DevCards.Count + NewDevCards.Count;

        public PlayerId Owner => MatchSeats.ToPlayerId(Index);
        public bool IsAi => Kind == SeatKind.Ai;
        public int CardCount => Hand.Total;
    }

    /// <summary>Seat index ↔ board owner. Seat 0 = PlayerId.Human, seat 1 = PlayerId.Ai, seats 2–4 = (PlayerId)2..4.</summary>
    public static class MatchSeats
    {
        public static PlayerId ToPlayerId(int seat) => (PlayerId)seat;
        public static int ToSeat(PlayerId owner) => (int)owner;
    }

    /// <summary>v134 Phase 1: full state of an N-player match. Mutated only by <see cref="MatchController"/>.</summary>
    public sealed class MatchState
    {
        public MatchConfig Config { get; }
        public BoardState Board { get; }
        public List<MatchSeatState> Seats { get; } = new();
        public MatchPhase Phase { get; set; } = MatchPhase.SetupSettlement;

        /// <summary>Seat that must act next (setup order during setup, the turn owner afterwards).</summary>
        public int CurrentSeat { get; set; }

        /// <summary>Index into <see cref="SetupOrder"/> during setup.</summary>
        public int SetupStep { get; set; }
        public List<int> SetupOrder { get; } = new();
        public Vertex? LastSetupSettlement { get; set; }

        /// <summary>Completed full rounds of normal turns (0 during setup and the first round).</summary>
        public int Round { get; set; }
        public int TurnNumber { get; set; }
        public int LastRoll { get; set; }
        public int? LongestRoadSeat { get; set; }
        public int? Winner { get; set; }
        public MatchRng Rng { get; set; }
        public List<string> Log { get; } = new();

        // ---- v135 ----
        /// <summary>Harbours of this board (2:1 specific, 3:1 generic).</summary>
        public List<Shop.PortDefinition> Ports { get; } = new();
        /// <summary>Development card draw pile (top = last).</summary>
        public List<MatchDevCard> DevDeck { get; } = new();
        public int? LargestArmySeat { get; set; }
        /// <summary>Seat → cards still to discard after a 7.</summary>
        public SortedDictionary<int, int> PendingDiscards { get; } = new();
        public int FreeRoadsLeft { get; set; }
        /// <summary>Phase to return to after the robber (knight) or road building (Roll or Main).</summary>
        public MatchPhase ResumePhase { get; set; } = MatchPhase.Main;
        public MatchTradeOffer ActiveTrade { get; set; }

        public MatchSeatState Current => Seats[CurrentSeat];

        /// <summary>
        /// v135: the seat that must act now. Usually <see cref="CurrentSeat"/>; during <see cref="MatchPhase.Discard"/>
        /// it is the lowest seat that still owes a discard.
        /// </summary>
        public int ActingSeat => Phase == MatchPhase.Discard && PendingDiscards.Count > 0
            ? PendingDiscards.Keys.First()
            : CurrentSeat;

        public MatchSeatState Acting => Seats[ActingSeat];

        public MatchState(MatchConfig config, BoardState board)
        {
            Config = config;
            Board = board;
        }

        /// <summary>
        /// Order-independent 64-bit FNV-1a hash of everything that affects play. Two matches with the same config and
        /// command log must hash equal (desync / replay check).
        /// </summary>
        public ulong ComputeHash()
        {
            var sb = new StringBuilder();
            sb.Append(Phase).Append('|').Append(CurrentSeat).Append('|').Append(SetupStep).Append('|')
                .Append(Round).Append('|').Append(TurnNumber).Append('|').Append(LastRoll).Append('|')
                .Append(LongestRoadSeat?.ToString() ?? "-").Append('|').Append(Winner?.ToString() ?? "-").Append('|')
                .Append(Rng?.State ?? 0).Append('|');
            var robber = Board.RobberTile;
            sb.Append(robber.HasValue ? $"{robber.Value.Q},{robber.Value.R}" : "-").Append('|');
            foreach (var s in Seats)
            {
                var h = s.Hand;
                sb.Append($"S{s.Index}:{h.Wood},{h.Brick},{h.Wheat},{h.Sheep},{h.Stone},{s.VictoryPoints},"
                          + $"{s.ExtraTimeCharges},{(s.ExtraTimeUsedThisTurn ? 1 : 0)},"
                          + $"{string.Join("", s.DevCards.Select(c => (int)c))}/{string.Join("", s.NewDevCards.Select(c => (int)c))},"
                          + $"{s.KnightsPlayed},{(s.PlayedDevCardThisTurn ? 1 : 0)};");
            }

            sb.Append("D").Append(string.Join("", DevDeck.Select(c => (int)c))).Append('|')
                .Append(LargestArmySeat?.ToString() ?? "-").Append('|').Append(FreeRoadsLeft).Append('|')
                .Append(ResumePhase).Append('|');
            foreach (var kv in PendingDiscards)
                sb.Append($"X{kv.Key}={kv.Value};");
            if (ActiveTrade != null)
            {
                var t = ActiveTrade;
                sb.Append($"T{t.From}:{Bundle(t.Give)}>{Bundle(t.Get)}:");
                foreach (var r in t.Responses)
                    sb.Append($"{(int)r.Kind}{Bundle(r.CounterGive)}>{Bundle(r.CounterGet)},");
                sb.Append('|');
            }

            foreach (var line in Board.VertexBuildings
                         .Select(kv => $"B{kv.Key.Hex.Q},{kv.Key.Hex.R},{kv.Key.CornerIndex}={(int)kv.Value.type}/{(int)kv.Value.owner}")
                         .OrderBy(x => x, StringComparer.Ordinal))
                sb.Append(line).Append(';');
            foreach (var line in Board.Roads
                         .Select(kv => $"R{kv.Key.A.Hex.Q},{kv.Key.A.Hex.R},{kv.Key.A.CornerIndex}-{kv.Key.B.Hex.Q},{kv.Key.B.Hex.R},{kv.Key.B.CornerIndex}={(int)kv.Value}")
                         .OrderBy(x => x, StringComparer.Ordinal))
                sb.Append(line).Append(';');

            static string Bundle(ResourceBundle b) => $"{b.Wood}.{b.Brick}.{b.Wheat}.{b.Sheep}.{b.Stone}";

            ulong hash = 14695981039346656037UL;
            foreach (char c in sb.ToString())
            {
                hash ^= c;
                hash = unchecked(hash * 1099511628211UL);
            }

            return hash;
        }
    }
}
