using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Yield;

namespace CatanRoguelike.Core.Events
{
    /// <summary>Event family: picks the painted emblem (Resources/Art/Events/{Category}) and the card tint.</summary>
    public enum EventCategory
    {
        Board,
        Bounty,
        Hardship,
        Gift,
        Robber,
        Sea,
        Trade
    }

    /// <summary>Everything an event effect may touch. <see cref="Random"/> is the engine's seeded RNG.</summary>
    public sealed class EventContext
    {
        public GameState State { get; }
        public Random Random { get; }
        public CardEngine Cards { get; }
        public int MaxRoll { get; }

        public EventContext(GameState state, Random random, CardEngine cards, int maxRoll)
        {
            State = state;
            Random = random;
            Cards = cards;
            MaxRoll = maxRoll;
        }
    }

    /// <summary>
    /// A data-driven run event: name, card text, per-act weight, optional availability check and the
    /// effect itself. Adding an event = one <see cref="EventId"/> value + one entry in <see cref="EventLibrary"/>.
    /// </summary>
    public sealed class EventDefinition
    {
        public EventId Id { get; }
        public string Name { get; }
        public string Description { get; }
        public EventCategory Category { get; }
        public int Act1Weight { get; }
        public int Act2Weight { get; }
        public int Act3Weight { get; }
        /// <summary>True when the effect outlives the day (token changes, gifts, robber moves).</summary>
        public bool Permanent { get; }
        internal Action<EventContext> Apply { get; }
        internal Func<GameState, bool> IsAvailable { get; }

        public EventDefinition(EventId id, string name, string description)
            : this(id, name, description, EventCategory.Hardship, 0, 0, 0, false, null)
        {
        }

        public EventDefinition(EventId id, string name, string description, EventCategory category,
            int act1Weight, int act2Weight, int act3Weight, bool permanent, Action<EventContext> apply,
            Func<GameState, bool> isAvailable = null)
        {
            Id = id;
            Name = name;
            Description = description;
            Category = category;
            Act1Weight = act1Weight;
            Act2Weight = act2Weight;
            Act3Weight = act3Weight;
            Permanent = permanent;
            Apply = apply;
            IsAvailable = isAvailable;
        }

        public int WeightForAct(int act) => act switch
        {
            1 => Act1Weight,
            2 => Act2Weight,
            _ => Act3Weight
        };
    }

    public static class EventLibrary
    {
        /// <summary>Ordered pool (order matters for seeded picks).</summary>
        public static readonly IReadOnlyList<EventDefinition> Pool = new[]
        {
            new EventDefinition(EventId.Storm, "Storm",
                "A random tile produces nothing today (as if the robber were there).",
                EventCategory.Hardship, 1, 2, 3, false, EventEffects.Storm),
            new EventDefinition(EventId.Famine, "Famine",
                "Wheat rolls are capped at 1 tomorrow.",
                EventCategory.Hardship, 1, 2, 3, false, EventEffects.Famine),
            new EventDefinition(EventId.GoldRush, "Gold Rush",
                "Ore production is doubled today.",
                EventCategory.Bounty, 1, 1, 2, false, c => c.State.EventStoneDouble = true),
            new EventDefinition(EventId.MarketDay, "Market Day",
                "All shop trades are 3:1 today.",
                EventCategory.Trade, 1, 1, 1, false, c => c.State.EventShopBonus = 1),
            new EventDefinition(EventId.GoodHarvest, "Good Harvest",
                "All resource rolls are +1 tomorrow (respects cap).",
                EventCategory.Bounty, 1, 1, 1, false, EventEffects.GoodHarvest),
            new EventDefinition(EventId.BanditRaid, "Bandit Raid",
                "The robber moves to the tile where you produce the most.",
                EventCategory.Robber, 1, 2, 3, true, EventEffects.BanditRaid),
            new EventDefinition(EventId.PortBlockade, "Port Blockade",
                "A random port is blockaded — no port trade discount there today.",
                EventCategory.Sea, 0, 0, 2, false, EventEffects.PortBlockade),
            new EventDefinition(EventId.ResourceLevy, "Resource Levy",
                "The crown levies 1 of your most abundant resource.",
                EventCategory.Hardship, 0, 0, 2, true, EventEffects.ResourceLevy),

            // v131
            new EventDefinition(EventId.Earthquake, "Earthquake",
                "The ground shakes: two number tokens swap places for the rest of the run.",
                EventCategory.Board, 1, 1, 2, true, EventEffects.Earthquake, EventEffects.HasTwoTokens),
            new EventDefinition(EventId.ShiftingFortunes, "Shifting Fortunes",
                "One number token shifts by one (never onto 7) for the rest of the run.",
                EventCategory.Board, 1, 1, 1, true, EventEffects.ShiftingFortunes, EventEffects.HasTwoTokens),
            new EventDefinition(EventId.DoubleHarvest, "Double Harvest",
                "All production is doubled today.",
                EventCategory.Bounty, 1, 1, 1, false, c => c.State.EventProductionPercent = 200),
            new EventDefinition(EventId.LeanTimes, "Lean Times",
                "All production is halved today (rounded down).",
                EventCategory.Hardship, 1, 1, 2, false, c => c.State.EventProductionPercent = 50),
            new EventDefinition(EventId.RoyalPatronage, "Royal Patronage",
                "The crown is generous: every player draws a card.",
                EventCategory.Gift, 1, 1, 1, true, EventEffects.RoyalPatronage),
            new EventDefinition(EventId.HarvestFestival, "Harvest Festival",
                "Every player receives 1 of the festival's resource.",
                EventCategory.Gift, 2, 1, 1, true, EventEffects.HarvestFestival),
            new EventDefinition(EventId.Blight, "Blight",
                "One resource is blighted: it produces nothing today.",
                EventCategory.Hardship, 1, 1, 2, false, EventEffects.Blight),
            new EventDefinition(EventId.Squall, "Squall",
                "A storm sweeps the fishing grounds: no fish are caught today.",
                EventCategory.Sea, 1, 1, 1, false, c => c.State.EventFishStorm = true,
                s => s != null && s.FishermenOfCatanEnabled),
            new EventDefinition(EventId.Bountiful, "Bountiful Land",
                "One terrain flourishes: +1 per hit on it today.",
                EventCategory.Bounty, 1, 1, 1, false, EventEffects.Bountiful),
            new EventDefinition(EventId.RestlessRobber, "Restless Robber",
                "The robber wanders off to a random tile.",
                EventCategory.Robber, 1, 1, 1, true, EventEffects.RestlessRobber)
        };

        private static readonly Dictionary<EventId, EventDefinition> ById = Pool.ToDictionary(d => d.Id);

        /// <summary>Back-compat dictionary view (EventId → definition).</summary>
        public static IReadOnlyDictionary<EventId, EventDefinition> All => ById;

        public static EventDefinition Get(EventId id) => ById[id];
    }

    public sealed class EventEngine
    {
        public const float EventChancePerNight = BalanceConfig.Act1EventChance;

        /// <summary>A guaranteed event is announced every N days (N = 3 in acts 1–2, 2 in act 3).</summary>
        public const int CadenceDays = 3;
        public const int Act3CadenceDays = 2;

        private readonly Random _random;

        /// <summary>Card engine used by card-granting events (set by <see cref="GameController"/>).</summary>
        public CardEngine Cards { get; set; }

        public EventEngine(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public static int GetCadence(int act) => act >= 3 ? Act3CadenceDays : CadenceDays;

        /// <summary>True when the night before <paramref name="day"/> announces an event.</summary>
        public static bool IsEventDay(int day, int act) => day > 0 && day % GetCadence(act) == 0;

        /// <summary>Days until the next event night (0 = tonight).</summary>
        public static int DaysUntilNextEvent(int day, int act)
        {
            int cadence = GetCadence(act);
            int mod = ((day % cadence) + cadence) % cadence;
            return mod == 0 ? 0 : cadence - mod;
        }

        /// <summary>Nightly roll: an event on cadence days, otherwise none.</summary>
        public EventId RollNightEvent(GameState state, int act)
        {
            int day = state?.Board.DayNumber ?? 0;
            return IsEventDay(day, act) ? PickWeightedEvent(act, state) : EventId.None;
        }

        /// <summary>Legacy chance-based roll (per-act chance, then a weighted pick).</summary>
        public EventId MaybeRollEvent(int act = 1)
        {
            float chance = ActProgression.GetEventChance(act);
            if (_random.NextDouble() > chance)
                return EventId.None;

            return PickWeightedEvent(act, null);
        }

        public EventId PickWeightedEvent(int act, GameState state)
        {
            var candidates = EventLibrary.Pool
                .Where(d => d.WeightForAct(act) > 0 && (d.IsAvailable == null || state == null || d.IsAvailable(state)))
                .ToList();
            int total = candidates.Sum(d => d.WeightForAct(act));
            if (total <= 0)
                return EventId.None;

            int roll = _random.Next(total);
            int acc = 0;
            foreach (var def in candidates)
            {
                acc += def.WeightForAct(act);
                if (roll < acc)
                    return def.Id;
            }

            return candidates[^1].Id;
        }

        public void ApplyEvent(GameState state, EventId eventId)
        {
            state.ActiveEvent = eventId;
            state.EventDetail = "";
            state.EventHighlightHexes.Clear();
            if (eventId == EventId.None)
            {
                state.EventMessage = "";
                return;
            }

            var def = EventLibrary.Get(eventId);
            state.EventDay = state.Board.DayNumber;
            int maxRoll = ActProgression.GetMaxRollForDay(state.Board.DayNumber);
            var cards = Cards ?? new CardEngine(state.Board.DayNumber * 977 + 13);
            def.Apply?.Invoke(new EventContext(state, _random, cards, maxRoll));

            state.EventMessage = def.Name + ": " + def.Description
                + (string.IsNullOrEmpty(state.EventDetail) ? "" : " " + state.EventDetail);
        }

        public void ClearDailyEventEffects(GameState state)
        {
            state.EventStormTile = null;
            state.EventBlockedPortVertex = null;
            state.EventStoneDouble = false;
            state.EventShopBonus = 0;
            state.EventProductionPercent = 100;
            state.EventBlockedResource = null;
            state.EventBonusResource = null;
            state.EventFishStorm = false;
            state.EventHighlightHexes.Clear();
            state.ActiveEvent = EventId.None;
        }
    }

    /// <summary>The effect implementations referenced by <see cref="EventLibrary"/>.</summary>
    public static class EventEffects
    {
        public static void Storm(EventContext c)
        {
            var tiles = c.State.Board.Tiles.Keys.ToList();
            var tile = tiles[c.Random.Next(tiles.Count)];
            c.State.EventStormTile = tile;
            c.State.EventHighlightHexes.Add(tile);
        }

        public static void Famine(EventContext c)
        {
            var rolls = c.State.TomorrowRolls;
            if (rolls.ContainsKey(ResourceType.Wheat))
                rolls[ResourceType.Wheat] = Math.Min(rolls[ResourceType.Wheat], 1);
        }

        public static void GoodHarvest(EventContext c)
        {
            var rolls = c.State.TomorrowRolls;
            foreach (var key in rolls.Keys.ToList())
                rolls[key] = Math.Min(rolls[key] + 1, c.MaxRoll);
        }

        public static void BanditRaid(EventContext c)
        {
            var hex = PickPlayerBestTile(c);
            c.State.Board.PlaceRobber(hex);
            c.State.EventHighlightHexes.Add(hex);
        }

        public static void PortBlockade(EventContext c)
        {
            var ports = c.State.Ports;
            c.State.EventBlockedPortVertex = ports.Count == 0 ? default : ports[c.Random.Next(ports.Count)].Vertex;
        }

        public static void ResourceLevy(EventContext c)
        {
            var inv = c.State.PlayerInventory;
            int maxCount = 0;
            foreach (ResourceType resource in ResourceTypes.Productive)
                maxCount = Math.Max(maxCount, inv[resource]);
            if (maxCount <= 0)
                return;

            ResourceType taxed = ResourceTypes.Productive
                .Where(resource => inv[resource] == maxCount)
                .OrderByDescending(resource => resource)
                .First();
            inv.Set(taxed, maxCount - 1);
            c.State.PlayerInventory = inv;
            c.State.EventDetail = $"You paid 1 {EventText.ResourceName(taxed)}.";
        }

        public static bool HasTwoTokens(GameState state) => TokenTiles(state).Count >= 2;

        /// <summary>Land tiles carrying a number token (no desert, no fish), in a stable order.</summary>
        public static List<HexTileData> TokenTiles(GameState state) =>
            state.Board.Tiles.Values
                .Where(t => !t.IsDesert && !t.IsFishTile && t.NumberToken.HasValue)
                .OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R)
                .ToList();

        public static void Earthquake(EventContext c)
        {
            var tiles = TokenTiles(c.State);
            if (tiles.Count < 2)
                return;
            // Prefer two different numbers so the swap is visible.
            var a = tiles[c.Random.Next(tiles.Count)];
            var others = tiles.Where(t => t != a && t.NumberToken != a.NumberToken).ToList();
            if (others.Count == 0)
                others = tiles.Where(t => t != a).ToList();
            var b = others[c.Random.Next(others.Count)];
            (a.NumberToken, b.NumberToken) = (b.NumberToken, a.NumberToken);
            c.State.EventHighlightHexes.Add(a.Coord);
            c.State.EventHighlightHexes.Add(b.Coord);
            c.State.EventDetail =
                $"The {a.NumberToken} on {EventText.TerrainName(a.Resource)} swapped with the {b.NumberToken} on {EventText.TerrainName(b.Resource)}.";
        }

        public static void ShiftingFortunes(EventContext c)
        {
            var tiles = TokenTiles(c.State);
            if (tiles.Count == 0)
                return;
            var tile = tiles[c.Random.Next(tiles.Count)];
            int before = tile.NumberToken.Value;
            tile.NumberToken = ShiftToken(before, c.Random.Next(2) == 0 ? -1 : 1);
            c.State.EventHighlightHexes.Add(tile.Coord);
            c.State.EventDetail = $"The {before} on {EventText.TerrainName(tile.Resource)} became a {tile.NumberToken}.";
        }

        /// <summary>Shift a token by ±1 within 2..12, skipping 7 (2 can only go up, 12 only down).</summary>
        public static int ShiftToken(int token, int direction)
        {
            if (token <= 2) direction = 1;
            if (token >= 12) direction = -1;
            int next = token + direction;
            if (next == 7)
                next += direction;
            return Math.Max(2, Math.Min(12, next));
        }

        public static void RoyalPatronage(EventContext c)
        {
            c.Cards.DrawToHand(c.State, PlayerId.Human, 1);
            c.Cards.DrawToHand(c.State, PlayerId.Ai, 1);
        }

        public static void HarvestFestival(EventContext c)
        {
            var resource = ResourceTypes.Productive[c.Random.Next(ResourceTypes.Productive.Length)];
            var human = c.State.PlayerInventory;
            human.Add(resource, 1);
            c.State.PlayerInventory = human;
            var ai = c.State.AiInventory;
            ai.Add(resource, 1);
            c.State.AiInventory = ai;
            c.State.EventDetail = $"Everyone receives 1 {EventText.ResourceName(resource)}.";
        }

        public static void Blight(EventContext c)
        {
            var resource = ResourceTypes.Productive[c.Random.Next(ResourceTypes.Productive.Length)];
            c.State.EventBlockedResource = resource;
            HighlightTerrain(c, resource);
            c.State.EventDetail = $"{EventText.ResourceName(resource)} produces nothing today.";
        }

        public static void Bountiful(EventContext c)
        {
            var resource = ResourceTypes.Productive[c.Random.Next(ResourceTypes.Productive.Length)];
            c.State.EventBonusResource = resource;
            HighlightTerrain(c, resource);
            c.State.EventDetail = $"{EventText.TerrainName(resource)} yields +1 {EventText.ResourceName(resource)} per hit today.";
        }

        public static void RestlessRobber(EventContext c)
        {
            var candidates = c.State.Board.Tiles.Values
                .Where(t => !t.HasRobber && !t.IsFishTile)
                .OrderBy(t => t.Coord.Q).ThenBy(t => t.Coord.R)
                .ToList();
            if (candidates.Count == 0)
                return;
            var tile = candidates[c.Random.Next(candidates.Count)];
            c.State.Board.PlaceRobber(tile.Coord);
            c.State.EventHighlightHexes.Add(tile.Coord);
            c.State.EventDetail = tile.IsDesert
                ? "It settles in the desert."
                : $"It now blocks the {tile.NumberToken} on {EventText.TerrainName(tile.Resource)}.";
        }

        private static void HighlightTerrain(EventContext c, ResourceType resource)
        {
            foreach (var t in c.State.Board.Tiles.Values)
            {
                if (!t.IsDesert && !t.IsFishTile && t.Resource == resource)
                    c.State.EventHighlightHexes.Add(t.Coord);
            }
        }

        private static HexCoord PickPlayerBestTile(EventContext c)
        {
            var state = c.State;
            var counts = new Dictionary<HexCoord, int>();
            foreach (var kvp in state.Board.VertexBuildings)
            {
                if (kvp.Value.owner != PlayerId.Human) continue;
                foreach (var hex in VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (!state.Board.Tiles.ContainsKey(hex)) continue;
                    counts.TryGetValue(hex, out int n);
                    counts[hex] = n + 1;
                }
            }

            if (counts.Count == 0)
            {
                var tiles = state.Board.Tiles.Keys.ToList();
                return tiles[c.Random.Next(tiles.Count)];
            }

            return counts
                .Select(kv =>
                {
                    state.Board.TryGetTile(kv.Key, out var tile);
                    int pip = tile.NumberToken.HasValue ? NumberTokenLibrary.GetPipWeight(tile.NumberToken.Value) : 1;
                    return (coord: kv.Key, score: kv.Value * pip);
                })
                .OrderByDescending(x => x.score)
                .First().coord;
        }
    }

    /// <summary>Player-facing resource / terrain names (matches the HUD labels).</summary>
    public static class EventText
    {
        public static string ResourceName(ResourceType r) => r switch
        {
            ResourceType.Wood => "Lumber",
            ResourceType.Brick => "Brick",
            ResourceType.Wheat => "Wheat",
            ResourceType.Sheep => "Wool",
            ResourceType.Stone => "Ore",
            _ => r.ToString()
        };

        public static string TerrainName(ResourceType r) => r switch
        {
            ResourceType.Wood => "Forest",
            ResourceType.Brick => "Hills",
            ResourceType.Wheat => "Fields",
            ResourceType.Sheep => "Pasture",
            ResourceType.Stone => "Mountains",
            _ => r.ToString()
        };
    }
}
