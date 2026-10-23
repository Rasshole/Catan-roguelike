using System;
using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using Newtonsoft.Json;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Match
{
    public enum MatchCommandType
    {
        PlaceSetupSettlement = 0,
        PlaceSetupRoad = 1,
        RollDice = 2,
        MoveRobber = 3,
        BuildRoad = 4,
        BuildSettlement = 5,
        BuildCity = 6,
        BankTrade = 7,
        UseExtraTime = 8,
        EndTurn = 9
    }

    /// <summary>
    /// v134: one player intent. Every match action goes through <see cref="MatchController.Apply"/> as one of
    /// these, so a match is fully described by its config + ordered command log (replays, desync checks and,
    /// later, the network wire format). Plain fields only, so it round-trips through JSON.
    /// </summary>
    [Serializable]
    public sealed class MatchCommand : IEquatable<MatchCommand>
    {
        public MatchCommandType Type { get; set; }
        public int Seat { get; set; }

        // Vertex (settlement/city) or edge end A (road).
        public int Q { get; set; }
        public int R { get; set; }
        public int Corner { get; set; }

        // Edge end B (road).
        public int Q2 { get; set; }
        public int R2 { get; set; }
        public int Corner2 { get; set; }

        /// <summary>MoveRobber: seat to steal from, or -1 for none.</summary>
        public int Victim { get; set; } = -1;

        public ResourceType Give { get; set; }
        public ResourceType Receive { get; set; }

        [JsonIgnore]
        public Vertex Vertex => new(new HexCoord(Q, R), Corner);

        [JsonIgnore]
        public Edge Edge => new(new Vertex(new HexCoord(Q, R), Corner), new Vertex(new HexCoord(Q2, R2), Corner2));

        [JsonIgnore]
        public HexCoord Hex => new(Q, R);

        public static MatchCommand Simple(MatchCommandType type, int seat) => new() { Type = type, Seat = seat };

        public static MatchCommand AtVertex(MatchCommandType type, int seat, Vertex v) => new()
        {
            Type = type, Seat = seat, Q = v.Hex.Q, R = v.Hex.R, Corner = v.CornerIndex
        };

        public static MatchCommand AtEdge(MatchCommandType type, int seat, Edge e) => new()
        {
            Type = type, Seat = seat,
            Q = e.A.Hex.Q, R = e.A.Hex.R, Corner = e.A.CornerIndex,
            Q2 = e.B.Hex.Q, R2 = e.B.Hex.R, Corner2 = e.B.CornerIndex
        };

        public static MatchCommand Robber(int seat, HexCoord hex, int victim) => new()
        {
            Type = MatchCommandType.MoveRobber, Seat = seat, Q = hex.Q, R = hex.R, Victim = victim
        };

        public static MatchCommand Trade(int seat, ResourceType give, ResourceType receive) => new()
        {
            Type = MatchCommandType.BankTrade, Seat = seat, Give = give, Receive = receive
        };

        public string ToJson() => JsonConvert.SerializeObject(this);

        public static MatchCommand FromJson(string json) => JsonConvert.DeserializeObject<MatchCommand>(json);

        public static string LogToJson(IEnumerable<MatchCommand> log) => JsonConvert.SerializeObject(log);

        public static List<MatchCommand> LogFromJson(string json) =>
            JsonConvert.DeserializeObject<List<MatchCommand>>(json) ?? new List<MatchCommand>();

        public bool Equals(MatchCommand other) =>
            other != null && Type == other.Type && Seat == other.Seat && Q == other.Q && R == other.R
            && Corner == other.Corner && Q2 == other.Q2 && R2 == other.R2 && Corner2 == other.Corner2
            && Victim == other.Victim && Give == other.Give && Receive == other.Receive;

        public override bool Equals(object obj) => obj is MatchCommand other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Type, Seat, Q, R, Corner, Q2, R2, Victim);

        public override string ToString() => Type switch
        {
            MatchCommandType.PlaceSetupSettlement or MatchCommandType.BuildSettlement or MatchCommandType.BuildCity =>
                $"{Type}(seat {Seat}, {Vertex})",
            MatchCommandType.PlaceSetupRoad or MatchCommandType.BuildRoad => $"{Type}(seat {Seat}, {Edge})",
            MatchCommandType.MoveRobber => $"{Type}(seat {Seat}, {Hex}, victim {Victim})",
            MatchCommandType.BankTrade => $"{Type}(seat {Seat}, {Give}->{Receive})",
            _ => $"{Type}(seat {Seat})"
        };
    }

    /// <summary>Result of applying a command: accepted, or rejected with a reason (state unchanged).</summary>
    public readonly struct MatchCommandResult
    {
        public bool Ok { get; }
        public string Error { get; }

        private MatchCommandResult(bool ok, string error)
        {
            Ok = ok;
            Error = error ?? "";
        }

        public static MatchCommandResult Accepted => new(true, "");
        public static MatchCommandResult Rejected(string reason) => new(false, reason);
        public override string ToString() => Ok ? "Ok" : "Rejected: " + Error;
    }
}
