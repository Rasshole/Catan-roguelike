using System;
using System.Collections.Generic;
using CatanRoguelike.Core.Cards;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Turn;

namespace CatanRoguelike.Core
{
    public sealed class GameState
    {
        public BoardState Board { get; }
        public GamePhase Phase { get; set; } = GamePhase.SetupAiSettlement1;

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
        public string StatusMessage { get; set; } = "";

        /// <summary>Active card effect waiting to be consumed on next build.</summary>
        public CardId? PendingCard { get; set; }

        /// <summary>Next coastal settlement grants +1 VP (Harbor Charter).</summary>
        public bool HarborCharterPending { get; set; }

        /// <summary>AI cannot use shop deals that cost this resource today.</summary>
        public ResourceType? AiShopEmbargo { get; set; }

        public bool IsSetupPhase => Phase <= GamePhase.SetupPlayerRoad2;

        public bool IsNightPhase => Phase == GamePhase.NightPlayCard
            || Phase == GamePhase.NightRoll
            || Phase == GamePhase.NightAiPlan;

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
