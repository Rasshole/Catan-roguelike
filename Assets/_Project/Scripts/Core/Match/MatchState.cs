using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Events;
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
        RoadBuilding = 7,
        // ---- v136 party rules (appended: the int values are the wire format) ----
        /// <summary>Before placement: each seat in turn picks 1 of 3 civilizations (privately).</summary>
        CivPick = 8,
        /// <summary>After placement: each seat (last placer first) picks a secret quest (1 of 3) and a trait (1 of 2).</summary>
        QuestPick = 9,
        // ---- v141 shenanigans ----
        /// <summary>Secret ballot on a decree: each seat in <see cref="MatchState.PickOrder"/> votes in private.</summary>
        Vote = 10,
        /// <summary>Night Market: each seat in <see cref="MatchState.PickOrder"/> places a sealed bid in private.</summary>
        Auction = 11
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
        /// <summary>v139: on an accept, the resources the accepter takes for the offerer's give wildcards.</summary>
        public ResourceBundle AnyChoice { get; set; }
    }

    /// <summary>v135: the open player-to-player offer (offer/counter-offer board). Give/Get are the offerer's view.</summary>
    [Serializable]
    public sealed class MatchTradeOffer
    {
        public int From { get; set; }
        public ResourceBundle Give { get; set; }
        public ResourceBundle Get { get; set; }
        /// <summary>v139: wildcards the offerer gives — "N of any resource you choose" (the accepter picks).</summary>
        public int GiveAny { get; set; }
        /// <summary>v139: wildcards the offerer asks for — "N of anything" (the offerer picks when confirming).</summary>
        public int GetAny { get; set; }
        public List<MatchTradeResponse> Responses { get; set; } = new();
    }

    /// <summary>Per-seat state of an N-player match.</summary>
    [Serializable]
    public sealed partial class MatchSeatState
    {
        public int Index { get; set; }
        public SeatKind Kind { get; set; }
        public string Name { get; set; } = "";
        public ResourceBundle Hand { get; set; }
        public int VictoryPoints { get; set; }
        public int ExtraTimeCharges { get; set; } = TurnClock.StartingCharges;
        public bool ExtraTimeUsedThisTurn { get; set; }
        /// <summary>v143 Fishermen of Catan: fish caught on the coast (spent 4 for a card; public).</summary>
        public int Fish { get; set; }

        /// <summary>v135: playable development cards (hidden from other seats).</summary>
        public List<MatchDevCard> DevCards { get; set; } = new();
        /// <summary>v135: cards bought this turn — not playable until the next turn.</summary>
        public List<MatchDevCard> NewDevCards { get; set; } = new();
        public int KnightsPlayed { get; set; }
        public bool PlayedDevCardThisTurn { get; set; }

        // ---- v136 party rules ----
        public CivilizationId Civilization { get; set; }
        public List<CivilizationId> CivOffer { get; set; } = new();
        public List<MatchQuestId> QuestOffer { get; set; } = new();
        public List<MatchTraitId> TraitOffer { get; set; } = new();
        /// <summary>Secret quest: only its owner sees it until the game-over reveal.</summary>
        public MatchQuestId Quest { get; set; }
        public MatchTraitId Trait { get; set; }
        public bool QuestDone { get; set; }
        /// <summary>Completed player-to-player trades (either side).</summary>
        public int PlayerTrades { get; set; }
        public int BankTradesThisTurn { get; set; }
        /// <summary>Round in which the Prospector trait last paid out (-1 = never).</summary>
        public int ProspectorRound { get; set; } = -1;

        public int QuestVictoryPoints => QuestDone ? MatchPartyLibrary.Quest(Quest)?.Points ?? 0 : 0;

        /// <summary>Hidden VP: Victory Point cards + a completed secret quest (counted for winning, not shown to others).</summary>
        public int HiddenVictoryPoints =>
            DevCards.Count(c => c == MatchDevCard.VictoryPoint) + NewDevCards.Count(c => c == MatchDevCard.VictoryPoint)
            + QuestVictoryPoints;
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
    public sealed partial class MatchState
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
        /// <summary>v143: the two dice of <see cref="LastRoll"/> (presentation only; not part of the hash).</summary>
        public int LastDieA { get; set; }
        public int LastDieB { get; set; }
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

        // ---- v136 party rules ----
        /// <summary>Seats in pick order for <see cref="MatchPhase.CivPick"/> / <see cref="MatchPhase.QuestPick"/>.</summary>
        public List<int> PickOrder { get; } = new();
        public int PickStep { get; set; }
        public List<CivilizationId> CivDeck { get; } = new();
        public List<MatchQuestId> QuestDeck { get; } = new();
        public List<MatchTraitId> TraitDeck { get; } = new();
        public List<EventId> EventDeck { get; } = new();
        /// <summary>This round's event card (None before round 2).</summary>
        public EventId ActiveEvent { get; set; }
        /// <summary>Storm: the hex that produces nothing this round.</summary>
        public HexCoord? EventTile { get; set; }
        /// <summary>Separate stream for dealing offers / events so dice sequences don't depend on party rules.</summary>
        public MatchRng PartyRng { get; set; }

        public bool IsPicking => Phase == MatchPhase.CivPick || Phase == MatchPhase.QuestPick;

        public MatchSeatState Current => Seats[CurrentSeat];

        /// <summary>
        /// v135: the seat that must act now. Usually <see cref="CurrentSeat"/>; during <see cref="MatchPhase.Discard"/>
        /// it is the lowest seat that still owes a discard.
        /// </summary>
        public int ActingSeat => Phase == MatchPhase.Discard && PendingDiscards.Count > 0
            ? PendingDiscards.Keys.First()
            : (Phase == MatchPhase.Vote || Phase == MatchPhase.Auction) && PickStep < PickOrder.Count
                ? PickOrder[PickStep]
                : CurrentSeat;

        /// <summary>v141: a secret ballot or sealed-bid auction is running (one private input per seat).</summary>
        public bool IsBallot => Phase == MatchPhase.Vote || Phase == MatchPhase.Auction;

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
                if (t.GiveAny > 0 || t.GetAny > 0)
                    sb.Append($"W{t.GiveAny}/{t.GetAny}:");
                foreach (var r in t.Responses)
                {
                    sb.Append($"{(int)r.Kind}{Bundle(r.CounterGive)}>{Bundle(r.CounterGet)}");
                    if (r.AnyChoice.Total > 0)
                        sb.Append('w').Append(Bundle(r.AnyChoice));
                    sb.Append(',');
                }
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

            if (Config.Fishermen)
                sb.Append("F").Append(string.Join(",", Seats.Select(s => s.Fish))).Append('|');
            if (Config.PartyRules)
            {
                sb.Append("P").Append(PickStep).Append(':').Append(PartyRng?.State ?? 0).Append(':')
                    .Append((int)ActiveEvent).Append(':').Append(EventTile.HasValue ? $"{EventTile.Value.Q},{EventTile.Value.R}" : "-")
                    .Append(':').Append(string.Join(",", CivDeck.Select(c => (int)c)))
                    .Append(':').Append(string.Join(",", QuestDeck.Select(c => (int)c)))
                    .Append(':').Append(string.Join(",", TraitDeck.Select(c => (int)c)))
                    .Append(':').Append(string.Join(",", EventDeck.Select(c => (int)c))).Append('|');
                foreach (var s in Seats)
                    sb.Append($"Q{s.Index}:{(int)s.Civilization},{(int)s.Quest},{(int)s.Trait},{(s.QuestDone ? 1 : 0)},"
                              + $"{s.PlayerTrades},{s.BankTradesThisTurn},{s.ProspectorRound},"
                              + $"{string.Join(".", s.CivOffer.Select(c => (int)c))}/{string.Join(".", s.QuestOffer.Select(c => (int)c))}/"
                              + $"{string.Join(".", s.TraitOffer.Select(c => (int)c))};");
                AppendShenanigans(sb);
            }

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
