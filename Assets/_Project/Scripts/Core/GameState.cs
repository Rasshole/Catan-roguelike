using System.Collections.Generic;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Events;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.Hex;

namespace CatanRoguelike.Core
{
    public sealed class GameState
    {
        public BoardState Board { get; }
        public MapSize MapSize { get; set; } = MapSize.Small;
        public GamePhase Phase { get; set; } = GamePhase.RunSelectLeader;

        public ResourceBundle PlayerInventory { get; set; }
        public ResourceBundle AiInventory { get; set; }

        public Dictionary<ResourceType, int> TomorrowRolls { get; set; } = new();
        public Dictionary<ResourceType, int> TodayRolls { get; set; } = new();

        public List<CardId> PlayerHand { get; } = new();
        public List<CardId> AiHand { get; } = new();
        public List<ShopDeal> ShopDeals { get; set; } = new();
        public List<PortDefinition> Ports { get; set; } = new();

        public int PlayerVictoryPoints { get; set; }
        public int AiVictoryPoints { get; set; }

        public PlayerId? Winner { get; set; }
        public string StatusMessage { get; set; } = "Choose your leader.";

        public CardId? PendingCard { get; set; }
        public bool HarborCharterPending { get; set; }
        public ResourceType? AiShopEmbargo { get; set; }
        public int AiEmbargoDaysLeft { get; set; }

        // Run setup
        public LeaderId Leader { get; set; } = LeaderId.Merchant;
        public List<UniqueBuildingId> DraftedUniques { get; } = new();
        public bool RunSetupComplete { get; set; }

        // Level-ups (every 5 days, max 3)
        public List<LevelUpPerkId> AcquiredPerks { get; } = new();
        public List<LevelUpPerkId> PendingLevelUpChoices { get; } = new();
        public int LevelUpsTaken { get; set; }
        public int LastLevelUpDay { get; set; } = -1;

        // Daily leader flags
        public bool PioneerFreeRoadAvailable { get; set; }
        public int FreeRoadCharges { get; set; }
        public bool FirstCityBuiltThisRun { get; set; }

        // Unique building flags
        public bool MonasteryUsed { get; set; }

        // Events
        public EventId ActiveEvent { get; set; } = EventId.None;
        public string EventMessage { get; set; } = "";
        public HexCoord? EventStormTile { get; set; }
        public bool EventStoneDouble { get; set; }
        public int EventShopBonus { get; set; }

        public bool IsSetupPhase => Phase <= GamePhase.SetupPlayerRoad2;

        public bool IsNightPhase => Phase == GamePhase.NightPlayCard
            || Phase == GamePhase.NightRoll
            || Phase == GamePhase.NightAiPlan;

        public bool HasPerk(LevelUpPerkId perk) => AcquiredPerks.Contains(perk);
        public bool HasUnique(UniqueBuildingId id) => DraftedUniques.Contains(id);

        public GameState(BoardState board)
        {
            Board = board;
            PlayerInventory = ResourceBundle.Zero;
            AiInventory = ResourceBundle.Zero;
        }

        public ResourceBundle GetInventory(PlayerId player) =>
            player == PlayerId.Human ? PlayerInventory : AiInventory;

        public void SetInventory(PlayerId player, ResourceBundle bundle)
        {
            if (player == PlayerId.Human) PlayerInventory = bundle;
            else AiInventory = bundle;
        }

        public void AddVictoryPoints(PlayerId player, int points)
        {
            if (player == PlayerId.Human) PlayerVictoryPoints += points;
            else AiVictoryPoints += points;
        }

        public int GetVictoryPoints(PlayerId player) =>
            player == PlayerId.Human ? PlayerVictoryPoints : AiVictoryPoints;
    }
}
