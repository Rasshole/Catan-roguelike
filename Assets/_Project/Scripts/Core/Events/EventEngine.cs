using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Yield;

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
                "Wheat rolls are capped at 1 tomorrow."),
            [EventId.GoldRush] = new(EventId.GoldRush, "Gold Rush",
                "Stone production is doubled today."),
            [EventId.MarketDay] = new(EventId.MarketDay, "Market Day",
                "All shop trades are 3:1 today."),
            [EventId.GoodHarvest] = new(EventId.GoodHarvest, "Good Harvest",
                "All resource rolls are +1 tomorrow (respects cap)."),
            [EventId.BanditRaid] = new(EventId.BanditRaid, "Bandit Raid",
                "Robber moves to the tile where you produce the most."),
            [EventId.PortBlockade] = new(EventId.PortBlockade, "Port Blockade",
                "A random port is blockaded — no port trade discount there today."),
            [EventId.ResourceLevy] = new(EventId.ResourceLevy, "Resource Levy",
                "The crown levies 1 of your most abundant resource today.")
        };
    }

    public sealed class EventEngine
    {
        public const float EventChancePerNight = BalanceConfig.Act1EventChance;
        private readonly Random _random;

        private static readonly (EventId id, int act1Weight, int act2Weight, int act3Weight)[] WeightedPool = new[]
        {
            (EventId.Storm, 1, 2, 3),
            (EventId.Famine, 1, 2, 3),
            (EventId.GoldRush, 1, 1, 2),
            (EventId.MarketDay, 1, 1, 1),
            (EventId.GoodHarvest, 1, 1, 1),
            (EventId.BanditRaid, 1, 2, 3),
            (EventId.PortBlockade, 0, 0, 2),
            (EventId.ResourceLevy, 0, 0, 2)
        };

        public EventEngine(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public EventId MaybeRollEvent(int act = 1)
        {
            float chance = ActProgression.GetEventChance(act);
            if (_random.NextDouble() > chance)
                return EventId.None;

            return PickWeightedEvent(act);
        }

        private EventId PickWeightedEvent(int act)
        {
            int total = 0;
            foreach (var entry in WeightedPool)
            {
                total += act switch
                {
                    1 => entry.act1Weight,
                    2 => entry.act2Weight,
                    _ => entry.act3Weight
                };
            }

            int roll = _random.Next(total);
            int acc = 0;
            foreach (var entry in WeightedPool)
            {
                int weight = act switch
                {
                    1 => entry.act1Weight,
                    2 => entry.act2Weight,
                    _ => entry.act3Weight
                };
                acc += weight;
                if (roll < acc)
                    return entry.id;
            }

            return WeightedPool[^1].id;
        }

        public void ApplyEvent(GameState state, EventId eventId)
        {
            state.ActiveEvent = eventId;
            state.EventMessage = eventId == EventId.None
                ? ""
                : EventLibrary.All[eventId].Name + ": " + EventLibrary.All[eventId].Description;

            int maxRoll = ActProgression.GetMaxRollForDay(state.Board.DayNumber);

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
                        state.TomorrowRolls[key] = Math.Min(state.TomorrowRolls[key] + 1, maxRoll);
                    break;
                case EventId.BanditRaid:
                    state.Board.PlaceRobber(PickPlayerBestTile(state));
                    break;
                case EventId.PortBlockade:
                    state.EventBlockedPortVertex = PickRandomPortVertex(state);
                    break;
                case EventId.ResourceLevy:
                    ApplyResourceLevy(state);
                    break;
            }
        }

        public void ClearDailyEventEffects(GameState state)
        {
            state.EventStormTile = null;
            state.EventBlockedPortVertex = null;
            state.EventStoneDouble = false;
            state.EventShopBonus = 0;
            state.ActiveEvent = EventId.None;
        }

        private static void ApplyResourceLevy(GameState state)
        {
            var inv = state.PlayerInventory;
            int maxCount = 0;
            foreach (ResourceType resource in Enum.GetValues(typeof(ResourceType)))
                maxCount = Math.Max(maxCount, inv[resource]);

            if (maxCount <= 0)
                return;

            ResourceType taxed = Enum.GetValues(typeof(ResourceType))
                .Cast<ResourceType>()
                .Where(resource => inv[resource] == maxCount)
                .OrderByDescending(resource => resource)
                .First();

            inv.Set(taxed, maxCount - 1);
            state.PlayerInventory = inv;
        }

        private HexMath.Vertex PickRandomPortVertex(GameState state)
        {
            if (state.Ports.Count == 0)
                return default;

            return state.Ports[_random.Next(state.Ports.Count)].Vertex;
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
            return counts
                .Select(kv =>
                {
                    state.Board.TryGetTile(kv.Key, out var tile);
                    int pip = tile.NumberToken.HasValue
                        ? NumberTokenLibrary.GetPipWeight(tile.NumberToken.Value)
                        : 1;
                    return (coord: kv.Key, score: kv.Value * pip);
                })
                .OrderByDescending(x => x.score)
                .First().coord;
        }
    }
}
