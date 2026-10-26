using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Victory;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Core.Match
{
    /// <summary>v136: secret quests for pass-and-play (one per seat, hidden VP until the game-over reveal).</summary>
    public enum MatchQuestId
    {
        None = 0,
        // 1 VP (easier)
        Harbourmaster = 1,
        RoadBuilder = 2,
        Knighthood = 3,
        Expansion = 4,
        OreBaron = 5,
        Breadbasket = 6,
        Diverse = 7,
        TownPlanner = 8,
        Trader = 9,
        // 2 VP (harder)
        TwinHarbours = 20,
        GrandArmy = 21,
        Metropolis = 22,
        Highway = 23,
        OreEmpire = 24,
        MerchantPrince = 25,
        Sprawl = 26
    }

    /// <summary>v136: small per-seat traits picked with the quest (two offered).</summary>
    public enum MatchTraitId
    {
        None = 0,
        LuckyDice = 1,
        HarbourPilot = 2,
        ToughHide = 3,
        HeadStart = 4,
        Granary = 5,
        RoadCrew = 6,
        Quartermaster = 7,
        Prospector = 8
    }

    public sealed class MatchQuestDefinition
    {
        public MatchQuestId Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Points { get; }
        /// <summary>Target for the progress readout ("2/3").</summary>
        public int Target { get; }
        internal Func<MatchController, int, int> Progress { get; }
        /// <summary>Board-position filter: only offered when the seat's start makes it plausible.</summary>
        internal Func<MatchController, int, bool> Plausible { get; }

        internal MatchQuestDefinition(MatchQuestId id, string name, string description, int points, int target,
            Func<MatchController, int, int> progress, Func<MatchController, int, bool> plausible = null)
        {
            Id = id;
            Name = name;
            Description = description;
            Points = points;
            Target = target;
            Progress = progress;
            Plausible = plausible ?? ((_, _) => true);
        }

        public int CurrentProgress(MatchController match, int seat) => Math.Min(Target, Progress(match, seat));
        public bool IsComplete(MatchController match, int seat) => Progress(match, seat) >= Target;
    }

    public sealed class MatchTraitDefinition
    {
        public MatchTraitId Id { get; }
        public string Name { get; }
        public string Description { get; }

        internal MatchTraitDefinition(MatchTraitId id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    /// <summary>v136: a match event card (flipped at the start of every round from round 2, lasts that round).</summary>
    public sealed class MatchEventDefinition
    {
        public EventId Id { get; }
        public string Name { get; }
        public string Description { get; }
        /// <summary>Picks the painted emblem (Resources/Art/Events/{Category}).</summary>
        public EventCategory Category { get; }
        /// <summary>True = resolves once when flipped; false = a modifier for the whole round.</summary>
        public bool Instant { get; }

        internal MatchEventDefinition(EventId id, string name, string description, EventCategory category, bool instant)
        {
            Id = id;
            Name = name;
            Description = description;
            Category = category;
            Instant = instant;
        }
    }

    /// <summary>v136: quest, trait and event pools + the board-position helpers they use.</summary>
    public static class MatchPartyLibrary
    {
        public const int CivOfferSize = 3;
        public const int QuestOfferSize = 3;
        public const int TraitOfferSize = 2;

        public static readonly IReadOnlyList<MatchQuestDefinition> Quests = new[]
        {
            new MatchQuestDefinition(MatchQuestId.Harbourmaster, "Harbourmaster", "Own a settlement or city on a harbour.", 1, 1,
                (m, s) => OwnedHarbours(m, s), (m, s) => HarbourWithin(m, s, 3)),
            new MatchQuestDefinition(MatchQuestId.RoadBuilder, "Road Builder", "Build a continuous road of 5 or more.", 1, 5,
                (m, s) => LongestRoad(m, s)),
            new MatchQuestDefinition(MatchQuestId.Knighthood, "Knighthood", "Play a Knight.", 1, 1,
                (m, s) => m.State.Seats[s].KnightsPlayed),
            new MatchQuestDefinition(MatchQuestId.Expansion, "Expansion", "Own 5 settlements or cities.", 1, 5,
                (m, s) => Buildings(m, s)),
            new MatchQuestDefinition(MatchQuestId.OreBaron, "Ore Baron", "Border 2 different Ore hexes and a Wool hex.", 1, 3,
                (m, s) => Math.Min(2, Bordered(m, s, ResourceType.Stone)) + Math.Min(1, Bordered(m, s, ResourceType.Sheep)), (m, s) => Bordered(m, s, ResourceType.Stone) >= 1),
            new MatchQuestDefinition(MatchQuestId.Breadbasket, "Breadbasket", "Border 2 different Grain hexes and a Brick hex.", 1, 3,
                (m, s) => Math.Min(2, Bordered(m, s, ResourceType.Wheat)) + Math.Min(1, Bordered(m, s, ResourceType.Brick)), (m, s) => Bordered(m, s, ResourceType.Wheat) >= 1),
            new MatchQuestDefinition(MatchQuestId.Diverse, "Five Harvests",
                "Border all five resources with settlements and cities joined by one road network of your own.", 1, 5,
                (m, s) => ConnectedDistinctResources(m, s)),
            new MatchQuestDefinition(MatchQuestId.TownPlanner, "Town Planner", "Own a city and 4 settlements or cities.", 1, 5,
                (m, s) => Math.Min(1, m.CountCities(s)) + Math.Min(4, Buildings(m, s))),
            new MatchQuestDefinition(MatchQuestId.Trader, "Trader", "Complete 5 trades with other players.", 1, 5,
                (m, s) => m.State.Seats[s].PlayerTrades),

            new MatchQuestDefinition(MatchQuestId.TwinHarbours, "Harbour Road", "Own a harbour and a continuous road of 5.", 2, 6,
                (m, s) => Math.Min(1, OwnedHarbours(m, s)) + Math.Min(5, LongestRoad(m, s)), (m, s) => HarbourWithin(m, s, 3)),
            new MatchQuestDefinition(MatchQuestId.GrandArmy, "Grand Army", "Play 2 Knights.", 2, 2,
                (m, s) => m.State.Seats[s].KnightsPlayed),
            new MatchQuestDefinition(MatchQuestId.Metropolis, "Metropolis", "Own 3 cities and 1 more settlement or city.", 2, 7,
                (m, s) => Math.Min(3, m.CountCities(s)) + Math.Min(4, Buildings(m, s))),
            new MatchQuestDefinition(MatchQuestId.Highway, "Highway", "Build a continuous road of 6 or more.", 2, 6,
                (m, s) => LongestRoad(m, s)),
            new MatchQuestDefinition(MatchQuestId.OreEmpire, "Ore Empire",
                "Own 3 settlements or cities on Ore, at least one of them a city.", 2, 4,
                (m, s) => Math.Min(3, BuildingsBordering(m, s, ResourceType.Stone))
                          + Math.Min(1, CitiesBordering(m, s, ResourceType.Stone)), (m, s) => Bordered(m, s, ResourceType.Stone) >= 1),
            new MatchQuestDefinition(MatchQuestId.MerchantPrince, "Merchant Prince", "Complete 7 trades with other players.", 2, 7,
                (m, s) => m.State.Seats[s].PlayerTrades),
            new MatchQuestDefinition(MatchQuestId.Sprawl, "Sprawl", "Own 6 settlements or cities and a continuous road of 5.", 2, 11,
                (m, s) => Math.Min(6, Buildings(m, s)) + Math.Min(5, LongestRoad(m, s))),
        };

        public static readonly IReadOnlyList<MatchTraitDefinition> Traits = new[]
        {
            new MatchTraitDefinition(MatchTraitId.LuckyDice, "Lucky Dice",
                "When you roll 2, 3, 11 or 12, take 1 of the resource you hold least of (random among ties)."),
            new MatchTraitDefinition(MatchTraitId.HarbourPilot, "Harbour Pilot",
                "Your first bank trade each turn is 2:1; the rest are 1 card cheaper (never below 2:1). Each settlement or city you build on a harbour lets you take 1 resource of your choice (setup settlements count)."),
            new MatchTraitDefinition(MatchTraitId.ToughHide, "Tough Hide", "A 7 only makes you discard from 11 cards; every second 7 rolled (by anyone) gives you 1 random resource."),
            new MatchTraitDefinition(MatchTraitId.HeadStart, "Head Start", "Start with 1 of each resource. Until your first turn ends, a 7 can't make you discard and the robber can't steal from you. Your first production card is lost."),
            new MatchTraitDefinition(MatchTraitId.Granary, "Granary", "A 7 only makes you discard above 8 cards, and every 7 gives you 1 Grain."),
            new MatchTraitDefinition(MatchTraitId.RoadCrew, "Road Crew", "Every third road you build is free."),
            new MatchTraitDefinition(MatchTraitId.Quartermaster, "Quartermaster", "Development cards cost you no Wool, and every Knight you play gives you 1 Grain."),
            new MatchTraitDefinition(MatchTraitId.Prospector, "Prospector",
                "When your own roll gives you nothing (a 7 counts), take 1 of the resource you hold least of. Once per round."),
        };

        public static readonly IReadOnlyList<MatchEventDefinition> Events = new[]
        {
            new MatchEventDefinition(EventId.GoldRush, "Gold Rush", "Ore hexes produce double this round.", EventCategory.Bounty, false),
            new MatchEventDefinition(EventId.GoodHarvest, "Good Harvest", "Grain hexes produce double this round.", EventCategory.Bounty, false),
            new MatchEventDefinition(EventId.Famine, "Famine", "Grain hexes produce nothing this round.", EventCategory.Hardship, false),
            new MatchEventDefinition(EventId.MarketDay, "Market Day", "Bank trades are at most 3:1 for everyone this round.", EventCategory.Trade, false),
            new MatchEventDefinition(EventId.Storm, "Storm", "A storm sits on one hex: it produces nothing this round.", EventCategory.Board, false),
            new MatchEventDefinition(EventId.BanditRaid, "Bandit Raid", "The robber jumps to the busiest hex (no steal).", EventCategory.Robber, true),
            new MatchEventDefinition(EventId.ResourceLevy, "Resource Levy", "Everyone holding 8+ cards pays 1 of their most plentiful resource.", EventCategory.Hardship, true),
            new MatchEventDefinition(EventId.RoyalPatronage, "Royal Patronage", "Whoever shows the fewest points takes 1 of the resource they hold least of.", EventCategory.Gift, true),
            new MatchEventDefinition(EventId.HarvestFestival, "Harvest Festival", "Everyone takes 1 Grain.", EventCategory.Gift, true),
            // v141 shenanigans
            new MatchEventDefinition(EventId.Decree, "Decree", "A decree goes to a secret ballot. Ties go to the player with the fewest points.", EventCategory.Trade, true),
            new MatchEventDefinition(EventId.NightMarket, "Night Market", "A rare item goes to sealed bids. The winner pays the second-highest bid to the bank.", EventCategory.Bounty, true),
            new MatchEventDefinition(EventId.Chaos, "Chaos", "The robber jumps to a random tile and everyone passes 1 random card to the left.", EventCategory.Robber, true),
        };

        public static MatchQuestDefinition Quest(MatchQuestId id) => Quests.FirstOrDefault(q => q.Id == id);
        public static MatchTraitDefinition Trait(MatchTraitId id) => Traits.FirstOrDefault(t => t.Id == id);
        public static MatchEventDefinition Event(EventId id) => Events.FirstOrDefault(e => e.Id == id);

        // ---------- board helpers ----------

        public static int Buildings(MatchController m, int seat) => m.CountSettlements(seat) + m.CountCities(seat);

        public static int LongestRoad(MatchController m, int seat) =>
            RouteCalculator.LongestRoadLength(m.State.Board, MatchSeats.ToPlayerId(seat));

        public static int OwnedHarbours(MatchController m, int seat)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            return m.State.Ports.Count(p => PortAccess.PlayerControlsPort(m.State.Board, owner, p));
        }

        /// <summary>Distinct hexes of <paramref name="resource"/> touching one of the seat's buildings.</summary>
        public static int Bordered(MatchController m, int seat, ResourceType resource) =>
            BorderedTiles(m, seat).Count(t => t.Resource == resource);

        /// <summary>v142: cities with at least one <paramref name="resource"/> hex next to them.</summary>
        /// <summary>v144 Ore Empire: own settlements and cities that border at least one hex of <paramref name="resource"/>.</summary>
        public static int BuildingsBordering(MatchController m, int seat, ResourceType resource)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            return m.State.Board.VertexBuildings.Count(kv => kv.Value.owner == owner
                && VertexGraph.GetHexesForVertex(kv.Key).Any(h => m.State.Board.TryGetTile(h, out var t) && !t.IsDesert
                                                                 && t.Resource == resource));
        }

        public static int CitiesBordering(MatchController m, int seat, ResourceType resource)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            int n = 0;
            foreach (var kv in m.State.Board.VertexBuildings)
            {
                if (kv.Value.owner != owner || kv.Value.type != BuildingType.City)
                    continue;
                if (VertexGraph.GetHexesForVertex(kv.Key).Any(h => m.State.Board.TryGetTile(h, out var t) && !t.IsDesert
                                                                  && t.Resource == resource))
                    n++;
            }

            return n;
        }

        public static int DistinctResources(MatchController m, int seat) =>
            BorderedTiles(m, seat).Select(t => t.Resource).Where(r => MatchController.AllResources.Contains(r)).Distinct().Count();

        /// <summary>
        /// v144 Five Harvests: the most distinct resources bordered by one group of the seat's settlements/cities that
        /// its own roads join (a rival building on a corner cuts the network there). Ships will join the network too.
        /// </summary>
        public static int ConnectedDistinctResources(MatchController m, int seat)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            var board = m.State.Board;
            var parent = new Dictionary<Vertex, Vertex>();
            Vertex Find(Vertex v)
            {
                if (!parent.TryGetValue(v, out var p))
                    return v;
                var root = Find(p);
                parent[v] = root;
                return root;
            }

            bool Blocked(Vertex v) => board.VertexBuildings.TryGetValue(v, out var b) && b.owner != owner;
            foreach (var kv in board.Roads)
            {
                if (kv.Value != owner)
                    continue;
                var a = VertexGraph.Canonicalize(kv.Key.A);
                var b = VertexGraph.Canonicalize(kv.Key.B);
                if (Blocked(a) || Blocked(b))
                    continue;
                var ra = Find(a);
                var rb = Find(b);
                if (!ra.Equals(rb))
                    parent[ra] = rb;
            }

            var groups = new Dictionary<Vertex, HashSet<ResourceType>>();
            foreach (var kv in board.VertexBuildings)
            {
                if (kv.Value.owner != owner)
                    continue;
                var root = Find(VertexGraph.Canonicalize(kv.Key));
                if (!groups.TryGetValue(root, out var set))
                    groups[root] = set = new HashSet<ResourceType>();
                foreach (var hex in VertexGraph.GetHexesForVertex(kv.Key))
                    if (board.TryGetTile(hex, out var tile) && !tile.IsDesert && MatchController.AllResources.Contains(tile.Resource))
                        set.Add(tile.Resource);
            }

            return groups.Count == 0 ? 0 : groups.Values.Max(g => g.Count);
        }

        private static IEnumerable<HexTileData> BorderedTiles(MatchController m, int seat)
        {
            var owner = MatchSeats.ToPlayerId(seat);
            var seen = new HashSet<HexCoord>();
            foreach (var kv in m.State.Board.VertexBuildings)
            {
                if (kv.Value.owner != owner)
                    continue;
                foreach (var hex in VertexGraph.GetHexesForVertex(kv.Key))
                    if (seen.Add(hex) && m.State.Board.TryGetTile(hex, out var tile) && !tile.IsDesert)
                        yield return tile;
            }
        }

        /// <summary>A free harbour vertex within <paramref name="steps"/> edges of the seat's buildings.</summary>
        public static bool HarbourWithin(MatchController m, int seat, int steps)
        {
            var board = m.State.Board;
            var owner = MatchSeats.ToPlayerId(seat);
            var frontier = board.VertexBuildings.Where(kv => kv.Value.owner == owner).Select(kv => kv.Key).ToList();
            var seen = new HashSet<Vertex>(frontier);
            var ports = new HashSet<Vertex>(m.State.Ports.SelectMany(p => PortAccess.ServedVertices(board, p)));
            for (int d = 0; d <= steps; d++)
            {
                foreach (var v in frontier)
                    if (ports.Contains(v) && (!board.VertexBuildings.TryGetValue(v, out var b) || b.owner == owner))
                        return true;
                var next = new List<Vertex>();
                foreach (var v in frontier)
                foreach (var a in VertexGraph.GetAdjacentVertices(v))
                {
                    var c = VertexGraph.Canonicalize(a);
                    if (seen.Add(c))
                        next.Add(c);
                }

                frontier = next;
            }

            return false;
        }
    }

    /// <summary>
    /// v136: the ten civilizations as pass-and-play rules. Most mirror the single-player passive; Sea Folk and the
    /// Academy are adapted (no fish / night cycle in a match) — the pick card shows <see cref="Passive"/>.
    /// </summary>
    public static class MatchCivRules
    {
        public static string Passive(CivilizationId civ) => civ switch
        {
            CivilizationId.SeaFolk => "Start with 1 Lumber and 1 Brick. Bank trades are 3:1, and every harbour you own trades any resource at 2:1.",
            CivilizationId.ForestFolk => "On a 2 or 12, take 1 Lumber per forest you border (at least 1).",
            CivilizationId.MerchantRepublic => "Bank trades are 3:1 without a harbour, and your first bank trade each turn is 2:1.",
            CivilizationId.SteppeNomads => "Settlements cost no Brick.",
            CivilizationId.HighlandClans => "Mountains give +1 Ore per hit.",
            CivilizationId.ShepherdKingdom => "Start with 2 Wool. Pastures give +1 Wool per hit.",
            CivilizationId.Brickwrights => "Roads cost no Lumber.",
            CivilizationId.Academy => "Development cards cost no Grain.",
            CivilizationId.NorthernRaiders => "On every 7 you roll — or any 7 that finds a rival holding 7 or more cards — steal 1 card from the richest opponent.",
            CivilizationId.SunKingdom => "The robber never blocks your production, and nobody can rob you while you hold 4 or fewer cards.",
            _ => ""
        };

        public static string Drawback(CivilizationId civ) => civ switch
        {
            CivilizationId.SteppeNomads => "Development cards cost 1 extra Ore.",
            CivilizationId.Academy => "Settlements cost 1 extra Grain.",
            CivilizationId.Brickwrights => "Development cards cost 1 extra Grain.",
            _ => ""
        };

        public static ResourceBundle RoadCost(CivilizationId civ, MatchTraitId trait)
        {
            var c = MatchController.RoadCost;
            if (civ == CivilizationId.Brickwrights) c.Set(ResourceType.Wood, 0);
            return c;
        }

        public static ResourceBundle SettlementCost(CivilizationId civ, MatchTraitId trait)
        {
            var c = MatchController.SettlementCost;
            if (civ == CivilizationId.SteppeNomads) c.Set(ResourceType.Brick, 0);
            if (civ == CivilizationId.Academy) c.Add(ResourceType.Wheat, 1);
            return c;
        }

        public static ResourceBundle CityCost(CivilizationId civ, MatchTraitId trait)
        {
            var c = MatchController.CityCost;
            return c;
        }

        public static ResourceBundle DevCardCost(CivilizationId civ, MatchTraitId trait)
        {
            var c = MatchController.DevCardCost;
            if (civ == CivilizationId.Academy) c.Set(ResourceType.Wheat, 0);
            if (civ == CivilizationId.SteppeNomads) c.Add(ResourceType.Stone, 1);
            if (civ == CivilizationId.Brickwrights) c.Add(ResourceType.Wheat, 1);
            if (trait == MatchTraitId.Quartermaster) c.Set(ResourceType.Sheep, 0);
            return c;
        }

        /// <summary>Cards one building collects from one hit on a <paramref name="resource"/> hex.</summary>
        public static int HitAmount(CivilizationId civ, ResourceType resource, int baseAmount)
        {
            int amount = baseAmount;
            if (civ == CivilizationId.HighlandClans && resource == ResourceType.Stone) amount++;
            if (civ == CivilizationId.ShepherdKingdom && resource == ResourceType.Sheep) amount++;
            return Math.Max(0, amount);
        }
    }
}
