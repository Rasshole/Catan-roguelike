using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;

namespace CatanRoguelike.Core.Save
{
    /// <summary>Root JSON document for a single save slot.</summary>
    public sealed class GameSaveDocument
    {
        public int FormatVersion { get; set; }
        public int RunSeed { get; set; }
        public GameStateSaveData State { get; set; }
        public VertexSaveData LastPlacedSettlement { get; set; }
    }

    public sealed class HexCoordSaveData
    {
        public int Q { get; set; }
        public int R { get; set; }
    }

    public sealed class VertexSaveData
    {
        public int HexQ { get; set; }
        public int HexR { get; set; }
        public int CornerIndex { get; set; }
    }

    public sealed class EdgeSaveData
    {
        public VertexSaveData A { get; set; }
        public VertexSaveData B { get; set; }
    }

    public sealed class HexTileSaveData
    {
        public int Q { get; set; }
        public int R { get; set; }
        public ResourceType Resource { get; set; }
        public bool HasRobber { get; set; }
        public BuildingType Building { get; set; }
        public PlayerId? Owner { get; set; }
        public int VertexIndex { get; set; }
        public bool IsCoastal { get; set; }
        public bool IsDesert { get; set; }
        /// <summary>Optional v1 — classic number token 2–12.</summary>
        public int? NumberToken { get; set; }
    }

    public sealed class RoadSaveData
    {
        public EdgeSaveData Edge { get; set; }
        public PlayerId Owner { get; set; }
    }

    public sealed class VertexBuildingSaveData
    {
        public VertexSaveData Vertex { get; set; }
        public BuildingType Type { get; set; }
        public PlayerId Owner { get; set; }
    }

    public sealed class BoardSaveData
    {
        public List<HexTileSaveData> Tiles { get; set; } = new();
        public List<RoadSaveData> Roads { get; set; } = new();
        public List<VertexBuildingSaveData> VertexBuildings { get; set; } = new();
        public List<EdgeSaveData> DisabledRoads { get; set; } = new();
        public HexCoordSaveData RobberTile { get; set; }
        public int DayNumber { get; set; }
    }

    public sealed class ResourceRollSaveData
    {
        public ResourceType Resource { get; set; }
        public int Count { get; set; }
    }

    public sealed class ResourceBundleSaveData
    {
        public int Wood { get; set; }
        public int Brick { get; set; }
        public int Wheat { get; set; }
        public int Sheep { get; set; }
        public int Stone { get; set; }
    }

    public sealed class ShopDealSaveData
    {
        public ResourceType Give { get; set; }
        public int GiveAmount { get; set; }
        public ResourceType Receive { get; set; }
        public int ReceiveAmount { get; set; }
        public bool IsRisky { get; set; }
        public string RiskDescription { get; set; }
    }

    public sealed class PortSaveData
    {
        public VertexSaveData Vertex { get; set; }
        public ResourceType SpecificResource { get; set; }
        public bool HasSpecificResource { get; set; }
    }

    public sealed class GameStateSaveData
    {
        public MapSize MapSize { get; set; }
        public GamePhase Phase { get; set; }
        public ResourceBundleSaveData PlayerInventory { get; set; }
        public ResourceBundleSaveData AiInventory { get; set; }
        public List<ResourceRollSaveData> TomorrowRolls { get; set; } = new();
        public List<ResourceRollSaveData> TodayRolls { get; set; } = new();
        public List<int> TomorrowDiceRolls { get; set; }
        public List<int> TodayDiceRolls { get; set; }
        public List<CardId> PlayerHand { get; set; } = new();
        public List<CardId> AiHand { get; set; } = new();
        public List<ShopDealSaveData> ShopDeals { get; set; } = new();
        public List<PortSaveData> Ports { get; set; } = new();
        public int PlayerVictoryPoints { get; set; }
        public int AiVictoryPoints { get; set; }
        public int PlayerBonusVictoryPoints { get; set; }
        public int AiBonusVictoryPoints { get; set; }
        public int PlayerKnightsPlayed { get; set; }
        public int AiKnightsPlayed { get; set; }
        public PlayerId? LargestArmyOwner { get; set; }
        public PlayerId? Winner { get; set; }
        public string StatusMessage { get; set; }
        public CardId? PendingCard { get; set; }
        public bool HarborCharterPending { get; set; }
        public ResourceType? AiShopEmbargo { get; set; }
        public int AiEmbargoDaysLeft { get; set; }
        public ResourceType? PlayerShopEmbargo { get; set; }
        public int PlayerEmbargoDaysLeft { get; set; }
        public LeaderId Leader { get; set; }
        public List<UniqueBuildingId> DraftedUniques { get; set; } = new();
        public bool RunSetupComplete { get; set; }
        public List<LevelUpPerkId> AcquiredPerks { get; set; } = new();
        public List<LevelUpPerkId> PendingLevelUpChoices { get; set; } = new();
        public int LevelUpsTaken { get; set; }
        public int LastLevelUpDay { get; set; }
        public bool PioneerFreeRoadAvailable { get; set; }
        public int FreeRoadCharges { get; set; }
        public bool FirstCityBuiltThisRun { get; set; }
        public bool MonasteryUsed { get; set; }
        public EventId ActiveEvent { get; set; }
        public string EventMessage { get; set; }
        public HexCoordSaveData EventStormTile { get; set; }
        public bool EventStoneDouble { get; set; }
        public int EventShopBonus { get; set; }
        public BoardSaveData Board { get; set; }
    }
}
