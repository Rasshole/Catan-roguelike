using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;

namespace CatanRoguelike.Core.Match
{
    /// <summary>Who drives a seat. Any mix is allowed (e.g. 3 humans + 1 AI for a 4-player game).</summary>
    public enum SeatKind
    {
        Human = 0,
        Ai = 1
    }

    [Serializable]
    public sealed class SeatConfig
    {
        public SeatKind Kind { get; set; } = SeatKind.Ai;
        public string Name { get; set; } = "";

        public SeatConfig()
        {
        }

        public SeatConfig(SeatKind kind, string name)
        {
            Kind = kind;
            Name = name ?? "";
        }
    }

    /// <summary>
    /// v134 Phase 1: configuration of an N-player match (2–5 seats). Seat index = turn order and maps 1:1 to
    /// <see cref="Map.PlayerId"/> ((PlayerId)seat) for board ownership.
    /// </summary>
    [Serializable]
    public sealed class MatchConfig
    {
        public const int MinSeats = 2;
        public const int MaxSeats = 5;
        public const int DefaultVictoryPoints = 10;

        public List<SeatConfig> Seats { get; set; } = new();
        public MapSize MapSize { get; set; } = MapSize.Large;
        public int Seed { get; set; }
        public int VictoryPoints { get; set; } = DefaultVictoryPoints;

        public int SeatCount => Seats.Count;
        public int HumanCount => Seats.Count(s => s.Kind == SeatKind.Human);
        public int AiCount => Seats.Count(s => s.Kind == SeatKind.Ai);

        /// <summary>Recommended board: Base 19 hexes for up to 4 players, Extended 30 for 5.</summary>
        public static MapSize RecommendedMap(int seatCount) => seatCount >= 5 ? MapSize.Extended : MapSize.Large;

        /// <summary>Builds a config with <paramref name="humans"/> human seats first, then AI seats filling the rest.</summary>
        public static MatchConfig Create(int seatCount, int humans, int seed, MapSize? mapSize = null)
        {
            if (humans < 0 || humans > seatCount)
                throw new ArgumentOutOfRangeException(nameof(humans));
            var config = new MatchConfig { Seed = seed, MapSize = mapSize ?? RecommendedMap(seatCount) };
            for (int i = 0; i < seatCount; i++)
            {
                bool human = i < humans;
                config.Seats.Add(new SeatConfig(human ? SeatKind.Human : SeatKind.Ai,
                    human ? $"Player {i + 1}" : $"AI {i + 1}"));
            }

            config.Validate();
            return config;
        }

        /// <summary>Fill every empty (null) seat with an AI player.</summary>
        public void FillEmptySeatsWithAi()
        {
            for (int i = 0; i < Seats.Count; i++)
            {
                if (Seats[i] == null)
                    Seats[i] = new SeatConfig(SeatKind.Ai, $"AI {i + 1}");
            }
        }

        public void Validate()
        {
            if (Seats == null || Seats.Count < MinSeats || Seats.Count > MaxSeats)
                throw new ArgumentException($"A match needs {MinSeats}–{MaxSeats} seats (got {Seats?.Count ?? 0}).");
            if (Seats.Any(s => s == null))
                throw new ArgumentException("Every seat must be a human or an AI (fill empty seats with AI).");
            if (VictoryPoints < 3)
                throw new ArgumentException("Victory target must be at least 3.");
        }
    }
}
