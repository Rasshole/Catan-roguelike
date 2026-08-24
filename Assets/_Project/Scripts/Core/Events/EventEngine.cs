using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Events
{
    public sealed class EventDefinition
    {
        public EventId Id { get; }
        public string Name { get; }
        public string Description { get; }

        public EventDefinition(EventId id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    public static class EventLibrary
    {
        public static readonly Dictionary<EventId, EventDefinition> All = new()
        {
            [EventId.Storm] = new(EventId.Storm, "Storm",
                "A random tile produces nothing today (as if robber were there)."),
            [EventId.Famine] = new(EventId.Famine, "Famine",
                "Wheat rolls are capped at 1 today."),
            [EventId.GoldRush] = new(EventId.GoldRush, "Gold Rush",
                "Stone production is doubled today."),
            [EventId.MarketDay] = new(EventId.MarketDay, "Market Day",
                "All shop trades are 3:1 today."),
            [EventId.GoodHarvest] = new(EventId.GoodHarvest, "Good Harvest",
                "All resource rolls are +1 today (respects cap)."),
            [EventId.BanditRaid] = new(EventId.BanditRaid, "Bandit Raid",
                "Robber moves to the tile where you produce the most.")
        };
    }

    public sealed class EventEngine
    {
        public const float EventChancePerNight = 0.22f;
        private readonly Random _random;

        public EventEngine(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public EventId MaybeRollEvent()
        {
            if (_random.NextDouble() > EventChancePerNight)
                return EventId.None;

            var pool = new[]
            {
                EventId.Storm, EventId.Famine, EventId.GoldRush,
                EventId.MarketDay, EventId.GoodHarvest, EventId.BanditRaid
            };
            return pool[_random.Next(pool.Length)];
        }

        public void ApplyEvent(GameState state, EventId eventId)
        {
            state.ActiveEvent = eventId;
            state.EventMessage = eventId == EventId.None
                ? ""
                : EventLibrary.All[eventId].Name + ": " + EventLibrary.All[eventId].Description;

            switch (eventId)
            {
                case EventId.Storm:
                    state.EventStormTile = PickRandomTile(state);
                    break;
                case EventId.Famine:
                    if (state.TomorrowRolls.ContainsKey(ResourceType.Wheat))
                        state.TomorrowRolls[ResourceType.Wheat] =
                            Math.Min(state.TomorrowRolls[ResourceType.Wheat], 1);
                    break;
                case EventId.GoldRush:
                    state.EventStoneDouble = true;
                    break;
                case EventId.MarketDay:
                    state.EventShopBonus = 1;
                    break;
                case EventId.GoodHarvest:
                    foreach (var key in state.TomorrowRolls.Keys.ToList())
                        state.TomorrowRolls[key] = Math.Min(state.TomorrowRolls[key] + 1, 2);
                    break;
                case EventId.BanditRaid:
                    state.Board.PlaceRobber(PickPlayerBestTile(state));
                    break;
            }
        }

        public void ClearDailyEventEffects(GameState state)
        {
            state.EventStormTile = null;
            state.EventStoneDouble = false;
            state.EventShopBonus = 0;
            state.ActiveEvent = EventId.None;
        }

        private HexCoord PickRandomTile(GameState state)
        {
            var tiles = state.Board.Tiles.Keys.ToList();
            return tiles[_random.Next(tiles.Count)];
        }

        private HexCoord PickPlayerBestTile(GameState state)
        {
            var counts = new Dictionary<HexCoord, int>();
            foreach (var kvp in state.Board.VertexBuildings)
            {
                if (kvp.Value.owner != PlayerId.Human) continue;
                foreach (var hex in Map.VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (!state.Board.Tiles.ContainsKey(hex)) continue;
                    counts.TryGetValue(hex, out int c);
                    counts[hex] = c + 1;
                }
            }
            if (counts.Count == 0) return PickRandomTile(state);
            return counts.OrderByDescending(kv => kv.Value).First().Key;
        }
    }
}
