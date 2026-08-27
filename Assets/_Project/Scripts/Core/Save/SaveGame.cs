using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using CatanRoguelike.Core.Yield;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Save
{
    /// <summary>
    /// Versioned JSON save/load for <see cref="GameState"/> + <see cref="BoardState"/> + run progression fields.
    /// Path-agnostic: string in/out so EditMode tests avoid UnityEngine IO.
    /// </summary>
    /// <remarks>
    /// RNG note (v1): each subsystem owns a separate <see cref="Random"/> seeded with <see cref="GameController.RunSeed"/>.
    /// Saves restore <see cref="GameState.TodayRolls"/> / <see cref="GameState.TomorrowRolls"/> exactly, but future RNG
    /// (nightly rolls, cards, shop, events, AI, robber steal) re-seeds from RunSeed only — not bit-identical to
    /// uninterrupted play. Per-engine roll counters can be added in a later format version if needed.
    /// Autosave point: end of night resolution when phase becomes <see cref="GamePhase.DayPlayerActions"/>
    /// (<see cref="GameController.OnAutosavePoint"/>).
    /// Army fields (<see cref="GameStateSaveData.PlayerKnightsPlayed"/>, etc.) are optional v1 properties defaulting to 0/null.
    /// </remarks>
    public static class SaveGame
    {
        public const int CurrentFormatVersion = 1;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static string Serialize(GameController controller, SaveWriteOptions options = null) =>
            Serialize(controller.RunSeed, controller.State, controller.GetLastPlacedSettlement(),
                controller.GetMetaStartCardGrantedForSave(), options);

        public static string Serialize(int runSeed, GameState state, Vertex? lastPlacedSettlement,
            bool metaStartCardGranted = false, SaveWriteOptions options = null)
        {
            options ??= SaveWriteOptions.Manual;
            var doc = new GameSaveDocument
            {
                FormatVersion = CurrentFormatVersion,
                RunSeed = runSeed,
                State = BuildStateSaveData(state),
                LastPlacedSettlement = lastPlacedSettlement.HasValue
                    ? ToVertexSaveData(lastPlacedSettlement.Value)
                    : null,
                MetaStartCardGranted = metaStartCardGranted ? true : null,
                SavedAtUtc = options.SavedAtUtc?.ToUniversalTime().ToString("o"),
                IsAutosave = options.IsAutosave
            };
            return JsonConvert.SerializeObject(doc, JsonSettings);
        }

        public static GameController LoadGame(string json) => GameController.FromSave(json);

        public static GameSaveDocument Parse(string json)
        {
            var doc = JsonConvert.DeserializeObject<GameSaveDocument>(json, JsonSettings);
            if (doc == null)
                throw new InvalidOperationException("Save JSON was empty or invalid.");

            if (doc.FormatVersion != CurrentFormatVersion)
                throw new InvalidOperationException(
                    $"Unsupported save format version {doc.FormatVersion}. Expected {CurrentFormatVersion}.");

            if (doc.State?.Board == null)
                throw new InvalidOperationException("Save JSON missing board state.");

            return doc;
        }

        public static GameState RestoreState(GameSaveDocument doc)
        {
            var board = RestoreBoard(doc.State.Board);
            var state = new GameState(board);
            ApplyStateSaveData(state, doc.State);
            return state;
        }

        internal static GameStateSaveData BuildStateSaveData(GameState state)
        {
            return new GameStateSaveData
            {
                MapSize = state.MapSize,
                Phase = state.Phase,
                PlayerInventory = ToBundleSaveData(state.PlayerInventory),
                AiInventory = ToBundleSaveData(state.AiInventory),
                TomorrowRolls = ToRollSaveList(state.TomorrowRolls),
                TodayRolls = ToRollSaveList(state.TodayRolls),
                TomorrowDiceRolls = state.TomorrowDiceRolls.Count > 0
                    ? new List<int>(state.TomorrowDiceRolls)
                    : null,
                TodayDiceRolls = state.TodayDiceRolls.Count > 0
                    ? new List<int>(state.TodayDiceRolls)
                    : null,
                PlayerHand = new List<CardId>(state.PlayerHand),
                AiHand = new List<CardId>(state.AiHand),
                ShopDeals = state.ShopDeals.Select(ToShopDealSaveData).ToList(),
                Ports = state.Ports.Select(ToPortSaveData).ToList(),
                PlayerVictoryPoints = state.PlayerVictoryPoints,
                AiVictoryPoints = state.AiVictoryPoints,
                PlayerBonusVictoryPoints = state.PlayerBonusVictoryPoints,
                AiBonusVictoryPoints = state.AiBonusVictoryPoints,
                PlayerKnightsPlayed = state.PlayerKnightsPlayed,
                AiKnightsPlayed = state.AiKnightsPlayed,
                LargestArmyOwner = state.LargestArmyOwner,
                Winner = state.Winner,
                StatusMessage = state.StatusMessage,
                PendingCard = state.PendingCard,
                HarborCharterPending = state.HarborCharterPending,
                AiShopEmbargo = state.AiShopEmbargo,
                AiEmbargoDaysLeft = state.AiEmbargoDaysLeft,
                PlayerShopEmbargo = state.PlayerShopEmbargo,
                PlayerEmbargoDaysLeft = state.PlayerEmbargoDaysLeft,
                Leader = state.Leader,
                DraftedUniques = new List<UniqueBuildingId>(state.DraftedUniques),
                RunSetupComplete = state.RunSetupComplete,
                AcquiredPerks = new List<LevelUpPerkId>(state.AcquiredPerks),
                PendingLevelUpChoices = new List<LevelUpPerkId>(state.PendingLevelUpChoices),
                LevelUpsTaken = state.LevelUpsTaken,
                LastLevelUpDay = state.LastLevelUpDay,
                PioneerFreeRoadAvailable = state.PioneerFreeRoadAvailable,
                FreeRoadCharges = state.FreeRoadCharges,
                FirstCityBuiltThisRun = state.FirstCityBuiltThisRun,
                MonasteryUsed = state.MonasteryUsed,
                ActiveEvent = state.ActiveEvent,
                EventMessage = state.EventMessage,
                EventStormTile = state.EventStormTile.HasValue
                    ? ToHexCoordSaveData(state.EventStormTile.Value)
                    : null,
                EventStoneDouble = state.EventStoneDouble,
                EventShopBonus = state.EventShopBonus,
                Board = BuildBoardSaveData(state.Board)
            };
        }

        internal static void ApplyStateSaveData(GameState state, GameStateSaveData data)
        {
            state.MapSize = data.MapSize;
            state.Phase = data.Phase;
            state.PlayerInventory = ToResourceBundle(data.PlayerInventory);
            state.AiInventory = ToResourceBundle(data.AiInventory);
            state.TomorrowRolls = ToRollDictionary(data.TomorrowRolls);
            state.TodayRolls = ToRollDictionary(data.TodayRolls);
            state.TomorrowDiceRolls = data.TomorrowDiceRolls != null
                ? new List<int>(data.TomorrowDiceRolls)
                : new List<int>();
            state.TodayDiceRolls = data.TodayDiceRolls != null
                ? new List<int>(data.TodayDiceRolls)
                : new List<int>();

            state.PlayerHand.Clear();
            state.PlayerHand.AddRange(data.PlayerHand ?? new List<CardId>());
            state.AiHand.Clear();
            state.AiHand.AddRange(data.AiHand ?? new List<CardId>());

            state.ShopDeals = (data.ShopDeals ?? new List<ShopDealSaveData>())
                .Select(ToShopDeal)
                .ToList();
            state.Ports = (data.Ports ?? new List<PortSaveData>())
                .Select(ToPortDefinition)
                .ToList();

            state.PlayerVictoryPoints = data.PlayerVictoryPoints;
            state.AiVictoryPoints = data.AiVictoryPoints;
            state.PlayerBonusVictoryPoints = data.PlayerBonusVictoryPoints;
            state.AiBonusVictoryPoints = data.AiBonusVictoryPoints;
            state.PlayerKnightsPlayed = data.PlayerKnightsPlayed;
            state.AiKnightsPlayed = data.AiKnightsPlayed;
            state.LargestArmyOwner = data.LargestArmyOwner;
            state.Winner = data.Winner;
            state.StatusMessage = data.StatusMessage ?? "";
            state.PendingCard = data.PendingCard;
            state.HarborCharterPending = data.HarborCharterPending;
            state.AiShopEmbargo = data.AiShopEmbargo;
            state.AiEmbargoDaysLeft = data.AiEmbargoDaysLeft;
            state.PlayerShopEmbargo = data.PlayerShopEmbargo;
            state.PlayerEmbargoDaysLeft = data.PlayerEmbargoDaysLeft;
            state.Leader = data.Leader;
            state.DraftedUniques.Clear();
            state.DraftedUniques.AddRange(data.DraftedUniques ?? new List<UniqueBuildingId>());
            state.RunSetupComplete = data.RunSetupComplete;
            state.AcquiredPerks.Clear();
            state.AcquiredPerks.AddRange(data.AcquiredPerks ?? new List<LevelUpPerkId>());
            state.PendingLevelUpChoices.Clear();
            state.PendingLevelUpChoices.AddRange(data.PendingLevelUpChoices ?? new List<LevelUpPerkId>());
            state.LevelUpsTaken = data.LevelUpsTaken;
            state.LastLevelUpDay = data.LastLevelUpDay;
            state.PioneerFreeRoadAvailable = data.PioneerFreeRoadAvailable;
            state.FreeRoadCharges = data.FreeRoadCharges;
            state.FirstCityBuiltThisRun = data.FirstCityBuiltThisRun;
            state.MonasteryUsed = data.MonasteryUsed;
            state.ActiveEvent = data.ActiveEvent;
            state.EventMessage = data.EventMessage ?? "";
            state.EventStormTile = data.EventStormTile != null
                ? ToHexCoord(data.EventStormTile)
                : null;
            state.EventStoneDouble = data.EventStoneDouble;
            state.EventShopBonus = data.EventShopBonus;

            ApplyBoardSaveData(state.Board, data.Board);
        }

        private static BoardSaveData BuildBoardSaveData(BoardState board)
        {
            var data = new BoardSaveData
            {
                DayNumber = board.DayNumber,
                RobberTile = board.RobberTile.HasValue
                    ? ToHexCoordSaveData(board.RobberTile.Value)
                    : null
            };

            foreach (var tile in board.Tiles.Values)
            {
                data.Tiles.Add(new HexTileSaveData
                {
                    Q = tile.Coord.Q,
                    R = tile.Coord.R,
                    Resource = tile.Resource,
                    HasRobber = tile.HasRobber,
                    Building = tile.Building,
                    Owner = tile.Owner,
                    VertexIndex = tile.VertexIndex,
                    IsCoastal = tile.IsCoastal,
                    IsDesert = tile.IsDesert,
                    NumberToken = tile.NumberToken
                });
            }

            foreach (var kv in board.Roads)
            {
                data.Roads.Add(new RoadSaveData
                {
                    Edge = ToEdgeSaveData(kv.Key),
                    Owner = kv.Value
                });
            }

            foreach (var kv in board.VertexBuildings)
            {
                data.VertexBuildings.Add(new VertexBuildingSaveData
                {
                    Vertex = ToVertexSaveData(kv.Key),
                    Type = kv.Value.type,
                    Owner = kv.Value.owner
                });
            }

            foreach (var edge in board.DisabledRoads)
                data.DisabledRoads.Add(ToEdgeSaveData(edge));

            return data;
        }

        private static BoardState RestoreBoard(BoardSaveData data)
        {
            var board = new BoardState { DayNumber = data.DayNumber };

            foreach (var tileData in data.Tiles ?? new List<HexTileSaveData>())
            {
                var coord = new HexCoord(tileData.Q, tileData.R);
                var tile = new HexTileData(coord, tileData.Resource, tileData.IsCoastal)
                {
                    HasRobber = tileData.HasRobber,
                    Building = tileData.Building,
                    Owner = tileData.Owner,
                    VertexIndex = tileData.VertexIndex,
                    IsDesert = tileData.IsDesert,
                    NumberToken = tileData.NumberToken
                };
                board.Tiles[coord] = tile;
            }

            foreach (var roadData in data.Roads ?? new List<RoadSaveData>())
                board.Roads[ToEdge(roadData.Edge)] = roadData.Owner;

            foreach (var buildingData in data.VertexBuildings ?? new List<VertexBuildingSaveData>())
            {
                var vertex = ToVertex(buildingData.Vertex);
                board.VertexBuildings[vertex] = (buildingData.Type, buildingData.Owner);
            }

            foreach (var edgeData in data.DisabledRoads ?? new List<EdgeSaveData>())
                board.DisabledRoads.Add(ToEdge(edgeData));

            if (data.RobberTile != null)
                board.PlaceRobber(ToHexCoord(data.RobberTile));

            return board;
        }

        private static void ApplyBoardSaveData(BoardState board, BoardSaveData data)
        {
            board.Tiles.Clear();
            board.Roads.Clear();
            board.VertexBuildings.Clear();
            board.DisabledRoads.Clear();
            board.DayNumber = data.DayNumber;

            foreach (var tileData in data.Tiles ?? new List<HexTileSaveData>())
            {
                var coord = new HexCoord(tileData.Q, tileData.R);
                var tile = new HexTileData(coord, tileData.Resource, tileData.IsCoastal)
                {
                    HasRobber = tileData.HasRobber,
                    Building = tileData.Building,
                    Owner = tileData.Owner,
                    VertexIndex = tileData.VertexIndex,
                    IsDesert = tileData.IsDesert,
                    NumberToken = tileData.NumberToken
                };
                board.Tiles[coord] = tile;
            }

            foreach (var roadData in data.Roads ?? new List<RoadSaveData>())
                board.Roads[ToEdge(roadData.Edge)] = roadData.Owner;

            foreach (var buildingData in data.VertexBuildings ?? new List<VertexBuildingSaveData>())
            {
                var vertex = ToVertex(buildingData.Vertex);
                board.VertexBuildings[vertex] = (buildingData.Type, buildingData.Owner);
            }

            foreach (var edgeData in data.DisabledRoads ?? new List<EdgeSaveData>())
                board.DisabledRoads.Add(ToEdge(edgeData));

            if (data.RobberTile != null)
                board.PlaceRobber(ToHexCoord(data.RobberTile));
            else
            {
                // Clear robber marker when absent in save.
                foreach (var tile in board.Tiles.Values)
                    tile.HasRobber = false;
            }
        }

        private static ResourceBundleSaveData ToBundleSaveData(ResourceBundle bundle) => new()
        {
            Wood = bundle.Wood,
            Brick = bundle.Brick,
            Wheat = bundle.Wheat,
            Sheep = bundle.Sheep,
            Stone = bundle.Stone
        };

        private static ResourceBundle ToResourceBundle(ResourceBundleSaveData data) => new()
        {
            Wood = data.Wood,
            Brick = data.Brick,
            Wheat = data.Wheat,
            Sheep = data.Sheep,
            Stone = data.Stone
        };

        private static List<ResourceRollSaveData> ToRollSaveList(Dictionary<ResourceType, int> rolls) =>
            rolls.Select(kv => new ResourceRollSaveData { Resource = kv.Key, Count = kv.Value }).ToList();

        private static Dictionary<ResourceType, int> ToRollDictionary(List<ResourceRollSaveData> rolls)
        {
            var dict = new Dictionary<ResourceType, int>();
            foreach (var roll in rolls ?? new List<ResourceRollSaveData>())
                dict[roll.Resource] = roll.Count;
            return dict;
        }

        private static ShopDealSaveData ToShopDealSaveData(ShopDeal deal) => new()
        {
            Give = deal.Give,
            GiveAmount = deal.GiveAmount,
            Receive = deal.Receive,
            ReceiveAmount = deal.ReceiveAmount,
            IsRisky = deal.IsRisky,
            RiskDescription = deal.RiskDescription
        };

        private static ShopDeal ToShopDeal(ShopDealSaveData data) =>
            new ShopDeal(data.Give, data.GiveAmount, data.Receive, data.ReceiveAmount,
                data.IsRisky, data.RiskDescription ?? "");

        private static PortSaveData ToPortSaveData(PortDefinition port) => new()
        {
            Vertex = ToVertexSaveData(port.Vertex),
            HasSpecificResource = port.SpecificResource.HasValue,
            SpecificResource = port.SpecificResource ?? ResourceType.Wood
        };

        private static PortDefinition ToPortDefinition(PortSaveData data) =>
            data.HasSpecificResource
                ? new PortDefinition(ToVertex(data.Vertex), data.SpecificResource)
                : new PortDefinition(ToVertex(data.Vertex));

        private static HexCoordSaveData ToHexCoordSaveData(HexCoord coord) => new() { Q = coord.Q, R = coord.R };

        private static HexCoord ToHexCoord(HexCoordSaveData data) => new HexCoord(data.Q, data.R);

        private static VertexSaveData ToVertexSaveData(Vertex vertex)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            return new VertexSaveData
            {
                HexQ = vertex.Hex.Q,
                HexR = vertex.Hex.R,
                CornerIndex = vertex.CornerIndex
            };
        }

        private static Vertex ToVertex(VertexSaveData data) =>
            VertexGraph.Canonicalize(new Vertex(new HexCoord(data.HexQ, data.HexR), data.CornerIndex));

        private static EdgeSaveData ToEdgeSaveData(Edge edge) => new()
        {
            A = ToVertexSaveData(edge.A),
            B = ToVertexSaveData(edge.B)
        };

        private static Edge ToEdge(EdgeSaveData data) =>
            new Edge(ToVertex(data.A), ToVertex(data.B));
    }
}
