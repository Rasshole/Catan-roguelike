using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Victory;
using CatanRoguelike.Core.Yield;

namespace CatanRoguelike.Core.Cards
{
    public sealed class CardEngine
    {
        private readonly Random _random;
        private readonly RollEngine _rollEngine;
        private readonly DiceRollEngine _diceEngine;

        public CardEngine(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _rollEngine = new RollEngine(seed);
            _diceEngine = new DiceRollEngine(seed);
        }

        public CardId DrawCard(bool forAi = false, int aiAct = 1, IReadOnlyList<CardId> humanPool = null)
        {
            var pool = forAi
                ? GetAiDrawPool(aiAct)
                : (humanPool ?? CardLibrary.AllCards)
                    .Where(id => CardLibrary.Get(id).AiCanUse || !forAi)
                    .ToList();

            if (pool.Count == 0) return CardId.Knight;

            float totalWeight = pool.Sum(id => CardLibrary.Get(id).AiWeight);
            float roll = (float)(_random.NextDouble() * totalWeight);
            float acc = 0f;
            foreach (var id in pool)
            {
                acc += CardLibrary.Get(id).AiWeight;
                if (roll <= acc) return id;
            }
            return pool[^1];
        }

        private static List<CardId> GetAiDrawPool(int aiAct)
        {
            var pool = new List<CardId>(CardLibrary.AiPool);
            if (aiAct >= 3)
            {
                foreach (var id in CardLibrary.Act3AiPoolExtras)
                {
                    if (!pool.Contains(id))
                        pool.Add(id);
                }
            }
            return pool;
        }

        public void DrawToHand(GameState state, PlayerId player, int count = 1, int aiAct = 1,
            IReadOnlyList<CardId> humanPool = null)
        {
            var hand = player == PlayerId.Human ? state.PlayerHand : state.AiHand;
            for (int i = 0; i < count; i++)
            {
                if (hand.Count >= BalanceConfig.MaxHandSize) break;
                hand.Add(DrawCard(player == PlayerId.Ai, aiAct, humanPool));
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
                CardId.RoadBuilder => ApplyRoadBuilder(state),
                CardId.MasterBuilder => ApplyMasterBuilder(state),
                CardId.Forecast => ApplyForecast(state, targetResource),
                CardId.Embargo => ApplyEmbargo(state, player, targetResource),
                CardId.HarborCharter => ApplyHarborCharter(state),
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
            int maxRoll = ActProgression.GetMaxRollForDay(state.Board.DayNumber);
            int current = state.TomorrowRolls[resource.Value];
            state.TomorrowRolls[resource.Value] = Math.Min(current + 1, maxRoll);
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
            if (amount <= 0) return true;

            int steal = player == PlayerId.Human && state.HasPerk(Leaders.LevelUpPerkId.MonopolyFull)
                ? amount
                : (amount + 1) / 2;

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

            int steals = ModifierService.GetKnightStealAmount(state, player);
            RobberSteal.StealFromHex(state, target.Value, player, _random, steals);

            if (player == PlayerId.Human && state.HasPerk(Leaders.LevelUpPerkId.KnightMovesRobberTwice))
            {
                var humanRoads = state.Board.Roads.Where(kv => kv.Value == PlayerId.Ai).Select(kv => kv.Key).ToList();
                if (humanRoads.Count > 0)
                    state.Board.DisabledRoads.Add(humanRoads[_random.Next(humanRoads.Count)]);
            }

            ArmyCalculator.RecordKnightPlayed(state, player);
            VictoryCalculator.RefreshVictoryPoints(state);
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

        private static bool ApplyRoadBuilder(GameState state)
        {
            state.PendingCard = CardId.RoadBuilder;
            state.FreeRoadCharges = state.HasPerk(Leaders.LevelUpPerkId.DoubleRoadBuilder) ? 2 : 1;
            return true;
        }

        private static bool ApplyMasterBuilder(GameState state)
        {
            state.PendingCard = CardId.MasterBuilder;
            return true;
        }

        private bool ApplyForecast(GameState state, ResourceType? resource)
        {
            var (passes, maxRoll) = ActProgression.GetYieldConfig(state.Board.DayNumber);
            state.TomorrowRolls = passes > 1
                ? _rollEngine.RollNightlyCombined(passes, maxRoll)
                : _rollEngine.RollNightly(maxRoll);
            state.TomorrowDiceRolls = _diceEngine.RollNightly(passes);
            return true;
        }

        private static bool ApplyEmbargo(GameState state, PlayerId player, ResourceType? resource)
        {
            if (!resource.HasValue) return false;

            int days = player == PlayerId.Human && state.HasPerk(Leaders.LevelUpPerkId.EmbargoExtended)
                ? 2
                : 1;

            if (player == PlayerId.Human)
            {
                state.AiShopEmbargo = resource.Value;
                state.AiEmbargoDaysLeft = days;
            }
            else
            {
                state.PlayerShopEmbargo = resource.Value;
                state.PlayerEmbargoDaysLeft = days;
            }

            return true;
        }

        private static bool ApplyHarborCharter(GameState state)
        {
            state.HarborCharterPending = true;
            return true;
        }
    }
}
