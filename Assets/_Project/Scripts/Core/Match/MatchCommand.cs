using System;
using System.Collections.Generic;
using CatanRoguelike.Core.Civilizations;
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
        EndTurn = 9,
        // ---- v135 (appended: the int values are the wire format) ----
        BuyDevCard = 10,
        PlayKnight = 11,
        PlayRoadBuilding = 12,
        /// <summary>Give = first resource, Receive = second (may be equal).</summary>
        PlayYearOfPlenty = 13,
        /// <summary>Receive = the resource to monopolise.</summary>
        PlayMonopoly = 14,
        /// <summary>Any seat that owes a discard after a 7; Offer = the cards discarded.</summary>
        Discard = 15,
        /// <summary>Offerer posts Offer (they give) / Request (they get) on the trade board.</summary>
        ProposeTrade = 16,
        /// <summary>Another seat answers: Response = Accept/Decline/Counter (counter terms in Offer/Request, offerer's view).</summary>
        RespondTrade = 17,
        /// <summary>Offerer closes the deal with Victim = the partner seat (their accepted or countered terms).</summary>
        ConfirmTrade = 18,
        CancelTrade = 19,
        // ---- v136 party rules ----
        /// <summary>Pick = the CivilizationId (one of the seat's offer).</summary>
        PickCivilization = 20,
        /// <summary>Pick = the MatchQuestId, Pick2 = the MatchTraitId (from the seat's offers).</summary>
        PickQuest = 21,
        // ---- v141 shenanigans ----
        /// <summary>Vote phase: Pick = 1 aye, 0 nay (sealed until everyone has voted).</summary>
        CastVote = 22,
        /// <summary>Auction phase: Offer = the sealed bid (cards; empty = pass).</summary>
        PlaceBid = 23,
        /// <summary>Main: Victim = the seat the bounty is on, Offer = the reward (1–3 cards, paid into escrow).</summary>
        PostBounty = 24,
        /// <summary>Main: buy one face-down sabotage card.</summary>
        BuySabotage = 25,
        /// <summary>Main: Pick = MatchSabotageKind, Victim = target; Blight: Q/R = hex; Washed-out Road: the edge fields.</summary>
        PlaySabotage = 26,
        /// <summary>Roll (before rolling): the target of a face-down card names its author. Victim = guessed author, Offer = 1 card paid.</summary>
        GuessSaboteur = 27,
        /// <summary>Main: spend a reroll won at the Night Market on the tile at Q/R.</summary>
        RerollToken = 28
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

        /// <summary>v135: Discard cards / trade: what the offerer gives.</summary>
        public ResourceBundle Offer { get; set; }
        /// <summary>v135: trade: what the offerer gets.</summary>
        public ResourceBundle Request { get; set; }
        public TradeResponseKind Response { get; set; }

        /// <summary>v136: civ / quest id picked.</summary>
        public int Pick { get; set; }
        /// <summary>v136: trait id picked with the quest.</summary>
        public int Pick2 { get; set; }

        /// <summary>v139 ProposeTrade: wildcards the offerer gives ("N of any resource you choose").</summary>
        public int GiveAny { get; set; }
        /// <summary>v139 ProposeTrade: wildcards the offerer asks for ("N of anything").</summary>
        public int GetAny { get; set; }
        /// <summary>
        /// v139 wildcard pick. RespondTrade(Accepted): the resources the accepter takes for the offerer's give
        /// wildcards. ConfirmTrade: the resources the offerer takes for the get wildcards.
        /// </summary>
        public ResourceBundle Choice { get; set; }

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

        public static MatchCommand Card(MatchCommandType type, int seat, ResourceType a = default, ResourceType b = default) =>
            new() { Type = type, Seat = seat, Give = a, Receive = b };

        public static MatchCommand DiscardCards(int seat, ResourceBundle cards) =>
            new() { Type = MatchCommandType.Discard, Seat = seat, Offer = cards };

        public static MatchCommand Propose(int seat, ResourceBundle give, ResourceBundle get, int giveAny = 0,
            int getAny = 0) =>
            new()
            {
                Type = MatchCommandType.ProposeTrade, Seat = seat, Offer = give, Request = get, GiveAny = giveAny,
                GetAny = getAny
            };

        /// <summary>v139: accept, picking <paramref name="choice"/> for the offerer's give wildcards.</summary>
        public static MatchCommand AcceptWithChoice(int seat, ResourceBundle choice) =>
            new() { Type = MatchCommandType.RespondTrade, Seat = seat, Response = TradeResponseKind.Accepted, Choice = choice };

        public static MatchCommand Respond(int seat, TradeResponseKind response, ResourceBundle counterGive = default,
            ResourceBundle counterGet = default) =>
            new() { Type = MatchCommandType.RespondTrade, Seat = seat, Response = response, Offer = counterGive, Request = counterGet };

        public static MatchCommand PickCiv(int seat, CivilizationId civ) =>
            new() { Type = MatchCommandType.PickCivilization, Seat = seat, Pick = (int)civ };

        public static MatchCommand PickQuestTrait(int seat, MatchQuestId quest, MatchTraitId trait) =>
            new() { Type = MatchCommandType.PickQuest, Seat = seat, Pick = (int)quest, Pick2 = (int)trait };

        public static MatchCommand Confirm(int seat, int partner, ResourceBundle choice = default) =>
            new() { Type = MatchCommandType.ConfirmTrade, Seat = seat, Victim = partner, Choice = choice };

        // ---- v141 ----
        public static MatchCommand Vote(int seat, bool aye) =>
            new() { Type = MatchCommandType.CastVote, Seat = seat, Pick = aye ? 1 : 0 };

        public static MatchCommand Bid(int seat, ResourceBundle cards) =>
            new() { Type = MatchCommandType.PlaceBid, Seat = seat, Offer = cards };

        public static MatchCommand Bounty(int seat, int target, ResourceBundle reward) =>
            new() { Type = MatchCommandType.PostBounty, Seat = seat, Victim = target, Offer = reward };

        public static MatchCommand Sabotage(int seat, MatchSabotageKind kind, int target, HexCoord hex = default) =>
            new() { Type = MatchCommandType.PlaySabotage, Seat = seat, Pick = (int)kind, Victim = target, Q = hex.Q, R = hex.R };

        public static MatchCommand SabotageRoad(int seat, int target, Edge edge) =>
            new()
            {
                Type = MatchCommandType.PlaySabotage, Seat = seat, Pick = (int)MatchSabotageKind.WashedOutRoad, Victim = target,
                Q = edge.A.Hex.Q, R = edge.A.Hex.R, Corner = edge.A.CornerIndex,
                Q2 = edge.B.Hex.Q, R2 = edge.B.Hex.R, Corner2 = edge.B.CornerIndex
            };

        public static MatchCommand Guess(int seat, int author, ResourceBundle paid) =>
            new() { Type = MatchCommandType.GuessSaboteur, Seat = seat, Victim = author, Offer = paid };

        public static MatchCommand Reroll(int seat, HexCoord hex) =>
            new() { Type = MatchCommandType.RerollToken, Seat = seat, Q = hex.Q, R = hex.R };

        public string ToJson() => JsonConvert.SerializeObject(this);

        public static MatchCommand FromJson(string json) => JsonConvert.DeserializeObject<MatchCommand>(json);

        public static string LogToJson(IEnumerable<MatchCommand> log) => JsonConvert.SerializeObject(log);

        public static List<MatchCommand> LogFromJson(string json) =>
            JsonConvert.DeserializeObject<List<MatchCommand>>(json) ?? new List<MatchCommand>();

        public bool Equals(MatchCommand other) =>
            other != null && Type == other.Type && Seat == other.Seat && Q == other.Q && R == other.R
            && Corner == other.Corner && Q2 == other.Q2 && R2 == other.R2 && Corner2 == other.Corner2
            && Victim == other.Victim && Give == other.Give && Receive == other.Receive
            && SameBundle(Offer, other.Offer) && SameBundle(Request, other.Request) && Response == other.Response
            && Pick == other.Pick && Pick2 == other.Pick2 && GiveAny == other.GiveAny && GetAny == other.GetAny
            && SameBundle(Choice, other.Choice);

        private static bool SameBundle(ResourceBundle a, ResourceBundle b) =>
            a.Wood == b.Wood && a.Brick == b.Brick && a.Wheat == b.Wheat && a.Sheep == b.Sheep && a.Stone == b.Stone;

        public override bool Equals(object obj) => obj is MatchCommand other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Type, Seat, Q, R, Corner, Q2, R2, Victim);

        public override string ToString() => Type switch
        {
            MatchCommandType.PlaceSetupSettlement or MatchCommandType.BuildSettlement or MatchCommandType.BuildCity =>
                $"{Type}(seat {Seat}, {Vertex})",
            MatchCommandType.PlaceSetupRoad or MatchCommandType.BuildRoad => $"{Type}(seat {Seat}, {Edge})",
            MatchCommandType.MoveRobber => $"{Type}(seat {Seat}, {Hex}, victim {Victim})",
            MatchCommandType.BankTrade => $"{Type}(seat {Seat}, {Give}->{Receive})",
            MatchCommandType.Discard => $"{Type}(seat {Seat}, {Offer.Total} cards)",
            MatchCommandType.ProposeTrade => $"{Type}(seat {Seat}, give {Offer.Total}+{GiveAny}* get {Request.Total}+{GetAny}*)",
            MatchCommandType.RespondTrade => $"{Type}(seat {Seat}, {Response})",
            MatchCommandType.ConfirmTrade => $"{Type}(seat {Seat} with {Victim})",
            MatchCommandType.PlayYearOfPlenty => $"{Type}(seat {Seat}, {Give}+{Receive})",
            MatchCommandType.PlayMonopoly => $"{Type}(seat {Seat}, {Receive})",
            MatchCommandType.PickCivilization => $"{Type}(seat {Seat}, {(CivilizationId)Pick})",
            MatchCommandType.PickQuest => $"{Type}(seat {Seat}, {(MatchQuestId)Pick}+{(MatchTraitId)Pick2})",
            MatchCommandType.CastVote => $"{Type}(seat {Seat}, sealed)",
            MatchCommandType.PlaceBid => $"{Type}(seat {Seat}, sealed)",
            MatchCommandType.PostBounty => $"{Type}(seat {Seat} on {Victim}, {Offer.Total} cards)",
            MatchCommandType.PlaySabotage => $"{Type}(seat {Seat}, face-down)",
            MatchCommandType.GuessSaboteur => $"{Type}(seat {Seat} names {Victim})",
            MatchCommandType.RerollToken => $"{Type}(seat {Seat}, {Hex})",
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
