using System.Collections.Generic;
using CatanRoguelike.Core.Data;
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
        public BoardState Board { get; private set; }
        public MapSize MapSize { get; set; } = MapSize.Small;
        public GamePhase Phase { get; set; } = GamePhase.RunSelectMap;

        public ResourceBundle PlayerInventory { get; set; }
        public ResourceBundle AiInventory { get; set; }

        public Dictionary<ResourceType, int> TomorrowRolls { get; set; } = new();
        public Dictionary<ResourceType, int> TodayRolls { get; set; } = new();

        /// <summary>2d6 sums previewed at night; applied to production when day starts.</summary>
        public List<int> TomorrowDiceRolls { get; set; } = new();
        public List<int> TodayDiceRolls { get; set; } = new();

        public List<CardId> PlayerHand { get; } = new();
        public List<CardId> AiHand { get; } = new();
        public List<ShopDeal> ShopDeals { get; set; } = new();
        public List<PortDefinition> Ports { get; set; } = new();

        public int PlayerVictoryPoints { get; set; }
        public int AiVictoryPoints { get; set; }

        /// <summary>
        /// VP not derived from the board (Harbor Charter, FirstCityVp, etc.).
        /// RefreshVictoryPoints adds this on top of buildings + longest road.
        /// </summary>
        public int PlayerBonusVictoryPoints { get; set; }
        public int AiBonusVictoryPoints { get; set; }

        /// <summary>Successful Knight card plays (not day-phase robber moves).</summary>
        public int PlayerKnightsPlayed { get; set; }
        public int AiKnightsPlayed { get; set; }

        /// <summary>Classic Catan: incumbent keeps on ties until strictly surpassed.</summary>
        public PlayerId? LargestArmyOwner { get; set; }

        public PlayerId? Winner { get; set; }
        public string StatusMessage { get; set; } = "Choose your leader.";
        public string ActUnlockMessage { get; set; } = "";

        public CardId? PendingCard { get; set; }
        public bool HarborCharterPending { get; set; }
        public ResourceType? AiShopEmbargo { get; set; }
        public int AiEmbargoDaysLeft { get; set; }
        public ResourceType? PlayerShopEmbargo { get; set; }
        public int PlayerEmbargoDaysLeft { get; set; }

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
        public HexMath.Vertex? EventBlockedPortVertex { get; set; }
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

        public void ResetForNewMap(BoardState board, MapSize mapSize)
        {
            Board = board;
            MapSize = mapSize;
            Phase = GamePhase.RunSelectLeader;

            PlayerInventory = ResourceBundle.Zero;
            AiInventory = ResourceBundle.Zero;
            TomorrowRolls.Clear();
            TodayRolls.Clear();
            TomorrowDiceRolls.Clear();
            TodayDiceRolls.Clear();
            PlayerHand.Clear();
            AiHand.Clear();
            ShopDeals.Clear();
            Ports.Clear();

            PlayerVictoryPoints = 0;
            AiVictoryPoints = 0;
            PlayerBonusVictoryPoints = 0;
            AiBonusVictoryPoints = 0;
            PlayerKnightsPlayed = 0;
            AiKnightsPlayed = 0;
            LargestArmyOwner = null;
            Winner = null;
            StatusMessage = "Choose your leader.";
            ActUnlockMessage = "";
            PendingCard = null;
            HarborCharterPending = false;
            AiShopEmbargo = null;
            AiEmbargoDaysLeft = 0;
            PlayerShopEmbargo = null;
            PlayerEmbargoDaysLeft = 0;

            Leader = LeaderId.Merchant;
            DraftedUniques.Clear();
            RunSetupComplete = false;

            AcquiredPerks.Clear();
            PendingLevelUpChoices.Clear();
            LevelUpsTaken = 0;
            LastLevelUpDay = -1;

            PioneerFreeRoadAvailable = false;
            FreeRoadCharges = 0;
            FirstCityBuiltThisRun = false;
            MonasteryUsed = false;

            ActiveEvent = EventId.None;
            EventMessage = "";
            EventStormTile = null;
            EventBlockedPortVertex = null;
            EventStoneDouble = false;
            EventShopBonus = 0;
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
            if (player == PlayerId.Human)
            {
                PlayerBonusVictoryPoints += points;
                PlayerVictoryPoints += points;
            }
            else
            {
                AiBonusVictoryPoints += points;
                AiVictoryPoints += points;
            }
        }

        public int GetBonusVictoryPoints(PlayerId player) =>
            player == PlayerId.Human ? PlayerBonusVictoryPoints : AiBonusVictoryPoints;

        public int GetVictoryPoints(PlayerId player) =>
            player == PlayerId.Human ? PlayerVictoryPoints : AiVictoryPoints;
    }
}
