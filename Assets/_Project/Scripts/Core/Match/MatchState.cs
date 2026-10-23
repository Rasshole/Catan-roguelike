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
        GameOver = 5
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

        public MatchSeatState Current => Seats[CurrentSeat];

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
                          + $"{s.ExtraTimeCharges},{(s.ExtraTimeUsedThisTurn ? 1 : 0)};");
            }

            foreach (var line in Board.VertexBuildings
                         .Select(kv => $"B{kv.Key.Hex.Q},{kv.Key.Hex.R},{kv.Key.CornerIndex}={(int)kv.Value.type}/{(int)kv.Value.owner}")
                         .OrderBy(x => x, StringComparer.Ordinal))
                sb.Append(line).Append(';');
            foreach (var line in Board.Roads
                         .Select(kv => $"R{kv.Key.A.Hex.Q},{kv.Key.A.Hex.R},{kv.Key.A.CornerIndex}-{kv.Key.B.Hex.Q},{kv.Key.B.Hex.R},{kv.Key.B.CornerIndex}={(int)kv.Value}")
                         .OrderBy(x => x, StringComparer.Ordinal))
                sb.Append(line).Append(';');

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
