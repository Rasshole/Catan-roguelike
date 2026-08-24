using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;

namespace CatanRoguelike.Core.Cards
{
    public sealed class CardEngine
    {
        private readonly Random _random;

        public CardEngine(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public CardId DrawCard(bool forAi = false)
        {
            var pool = forAi
                ? CardLibrary.AiPool.ToList()
                : CardLibrary.AllCards.ToList();

            return pool[_random.Next(pool.Count)];
        }

        public void DrawToHand(GameState state, PlayerId player, int count = 1)
        {
            var hand = player == PlayerId.Human ? state.PlayerHand : state.AiHand;
            for (int i = 0; i < count; i++)
            {
                if (hand.Count >= BalanceConfig.MaxHandSize) break;
                hand.Add(DrawCard(player == PlayerId.Ai));
            }
        }

        public bool PlayCard(GameState state, PlayerId player, CardId card,
            ResourceType? targetResource = null,
            Hex.HexCoord? robberTarget = null,
            Hex.HexMath.Edge? roadTarget = null)
        {
            var hand = player == PlayerId.Human ? state.PlayerHand : state.AiHand;
            if (!hand.Contains(card)) return false;

            bool success = card switch
            {
                CardId.MerchantsLedger => ApplyLedger(state, targetResource),
                CardId.Drought => ApplyDrought(state, targetResource),
                CardId.FertileSeason => ApplyFertile(state, targetResource),
                CardId.YearOfPlenty => ApplyPlenty(state, player, targetResource),
                CardId.Monopoly => ApplyMonopoly(state, player, targetResource),
                CardId.Knight => ApplyKnight(state, player, robberTarget),
                CardId.BanditRaid => ApplyBanditRaid(state, player, roadTarget),
                CardId.RoadBuilder => ApplyRoadBuilder(state, player),
                CardId.MasterBuilder => ApplyMasterBuilder(state, player),
                CardId.Forecast => ApplyForecast(state, targetResource),
                _ => false
            };

            if (success)
                hand.Remove(card);

            return success;
        }

        private bool ApplyLedger(GameState state, ResourceType? resource)
        {
            if (!resource.HasValue) return false;
            if (state.TomorrowRolls[resource.Value] == 0)
                state.TomorrowRolls[resource.Value] = 1;
            return true;
        }

        private bool ApplyDrought(GameState state, ResourceType? resource)
        {
            if (!resource.HasValue) return false;
            if (state.TomorrowRolls[resource.Value] > 1)
                state.TomorrowRolls[resource.Value] = 1;
            return true;
        }

        private bool ApplyFertile(GameState state, ResourceType? resource)
        {
            if (!resource.HasValue) return false;
            int current = state.TomorrowRolls[resource.Value];
            state.TomorrowRolls[resource.Value] = Math.Min(current + 1, 2);
            return true;
        }

        private bool ApplyPlenty(GameState state, PlayerId player, ResourceType? resource)
        {
            if (!resource.HasValue) return false;
            var inv = state.GetInventory(player);
            inv.Add(resource.Value, 2);
            state.SetInventory(player, inv);
            return true;
        }

        private bool ApplyMonopoly(GameState state, PlayerId player, ResourceType? resource)
        {
            if (!resource.HasValue) return false;
            var opponent = player == PlayerId.Human ? PlayerId.Ai : PlayerId.Human;
            var oppInv = state.GetInventory(opponent);
            int amount = oppInv[resource.Value];
            int steal = (amount + 1) / 2;
            if (steal <= 0) return true;

            oppInv.Add(resource.Value, -steal);
            var inv = state.GetInventory(player);
            inv.Add(resource.Value, steal);
            state.SetInventory(opponent, oppInv);
            state.SetInventory(player, inv);
            return true;
        }

        private bool ApplyKnight(GameState state, PlayerId player, Hex.HexCoord? target)
        {
            if (!target.HasValue || !state.Board.TryGetTile(target.Value, out _)) return false;
            state.Board.PlaceRobber(target.Value);

            var opponent = player == PlayerId.Human ? PlayerId.Ai : PlayerId.Human;
            var oppInv = state.GetInventory(opponent);
            var available = oppInv.EnumerateNonZero().ToList();
            if (available.Count == 0) return true;

            var pick = available[_random.Next(available.Count)];
            oppInv.Add(pick.type, -1);
            var inv = state.GetInventory(player);
            inv.Add(pick.type, 1);
            state.SetInventory(opponent, oppInv);
            state.SetInventory(player, inv);
            return true;
        }

        private bool ApplyBanditRaid(GameState state, PlayerId player, Hex.HexMath.Edge? edge)
        {
            if (!edge.HasValue) return false;
            var opponent = player == PlayerId.Human ? PlayerId.Ai : PlayerId.Human;
            if (!state.Board.Roads.TryGetValue(edge.Value, out var owner) || owner != opponent)
                return false;

            state.Board.DisabledRoads.Add(edge.Value);
            return true;
        }

        private bool ApplyRoadBuilder(GameState state, PlayerId player)
        {
            state.PendingCard = CardId.RoadBuilder;
            return true;
        }

        private bool ApplyMasterBuilder(GameState state, PlayerId player)
        {
            state.PendingCard = CardId.MasterBuilder;
            return true;
        }

        private bool ApplyForecast(GameState state, ResourceType? resource)
        {
            if (!resource.HasValue) return false;
            var engine = new RollEngine();
            state.TomorrowRolls[resource.Value] = engine.RollResource(resource.Value, 2);
            return true;
        }
    }
}
