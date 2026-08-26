using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Buildings;
using CatanRoguelike.Core.Data;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Leaders;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Shop;
using CatanRoguelike.Core.Progression;
using CatanRoguelike.Core.Yield;

namespace CatanRoguelike.Core
{
    public static class ModifierService
    {
        public static int GetSettlementThreshold(GameState state) =>
            state.HasUnique(UniqueBuildingId.GuildHall) || state.HasPerk(LevelUpPerkId.ThresholdDelay)
                ? 6 : BalanceConfig.SettlementThresholdCount;

        public static ResourceBundle ApplyLeaderCostModifiers(GameState state, PlayerId player, ResourceBundle cost,
            bool isSettlement, bool isCity, bool isRoad)
        {
            if (player != PlayerId.Human) return cost;

            var result = cost;
            if (state.Leader == LeaderId.Architect)
            {
                result = DiscountPercent(result, 0.1f);
            }
            if (isSettlement && state.HasPerk(LevelUpPerkId.CheapSettlements))
                result.Sheep = Math.Max(0, result.Sheep - 1);
            if (isCity && state.HasPerk(LevelUpPerkId.CheapCities))
                result.Stone = Math.Max(0, result.Stone - 1);
            return result;
        }

        public static int GetShopGiveAmount(GameState state, PlayerId player, int baseGive, ResourceType giveResource)
        {
            int give = baseGive - state.EventShopBonus;
            if (player == PlayerId.Human && state.Leader == LeaderId.Merchant)
                give = Math.Max(2, give - 1);
            if (player == PlayerId.Human && state.HasPerk(LevelUpPerkId.PortDiscount)
                && state.Ports.Any(p => PortAccess.PlayerControlsVertex(state.Board, player, p.Vertex)))
                give = Math.Max(2, give - 1);
            return Math.Max(2, give);
        }

        public static int GetKnightStealAmount(GameState state, PlayerId player) =>
            player == PlayerId.Human && state.Leader == LeaderId.Warlord ? 2 : 1;

        public static bool IsRobberBlocking(GameState state, HexCoord hex, PlayerId forPlayer)
        {
            if (!state.Board.TryGetTile(hex, out var tile) || !tile.HasRobber) return false;
            if (forPlayer != PlayerId.Human) return true;
            if (!state.HasUnique(UniqueBuildingId.FortressOutpost)) return true;
            return !IsAdjacentToPlayerCoastalSettlement(state, hex, forPlayer);
        }

        private static bool IsAdjacentToPlayerCoastalSettlement(GameState state, HexCoord hex, PlayerId player)
        {
            foreach (var kvp in state.Board.VertexBuildings)
            {
                if (kvp.Value.owner != player) continue;
                foreach (var h in VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (!h.Equals(hex)) continue;
                    if (state.Board.TryGetTile(h, out var t) && t.IsCoastal) return true;
                }
            }
            return false;
        }

        public static void ApplyNightUniques(GameState state)
        {
            if (state.HasUnique(UniqueBuildingId.Monastery) && !state.MonasteryUsed)
            {
                if (TryPickMonasteryTarget(state.TomorrowRolls, out var pick))
                {
                    state.TomorrowRolls[pick] = 1;
                    state.MonasteryUsed = true;
                    state.StatusMessage += " Monastery saved a 0 roll.";
                }
            }

            if (state.HasPerk(LevelUpPerkId.RollInsurance))
            {
                if (TryPickRollInsuranceTarget(state, out var pick))
                    state.TomorrowRolls[pick] = 1;
            }
        }

        /// <summary>
        /// Monastery: among tomorrow rolls at the nightly minimum, bump a 0 (once per run).
        /// Tie-break when multiple resources share that minimum roll: ResourceType descending.
        /// </summary>
        internal static bool TryPickMonasteryTarget(
            IReadOnlyDictionary<ResourceType, int> tomorrowRolls,
            out ResourceType pick)
        {
            pick = default;
            if (tomorrowRolls == null || tomorrowRolls.Count == 0)
                return false;

            int minRoll = tomorrowRolls.Values.Min();
            if (minRoll != 0)
                return false;

            pick = tomorrowRolls
                .Where(kv => kv.Value == minRoll)
                .Select(kv => kv.Key)
                .OrderByDescending(r => r)
                .First();
            return true;
        }

        /// <summary>
        /// Roll insurance: among 0 rolls, bump the scarcest player inventory.
        /// Tie-break when inventory counts tie: ResourceType ascending.
        /// </summary>
        internal static bool TryPickRollInsuranceTarget(GameState state, out ResourceType pick)
        {
            pick = default;
            var zeros = state.TomorrowRolls.Where(kv => kv.Value == 0).ToList();
            if (zeros.Count == 0)
                return false;

            var inv = state.PlayerInventory;
            int minInventory = zeros.Min(kv => inv[kv.Key]);
            pick = zeros
                .Where(kv => inv[kv.Key] == minInventory)
                .OrderBy(kv => kv.Key)
                .First()
                .Key;
            return true;
        }

        private static ResourceBundle DiscountPercent(ResourceBundle cost, float percent)
        {
            return new ResourceBundle
            {
                Wood = CeilDiscount(cost.Wood, percent),
                Brick = CeilDiscount(cost.Brick, percent),
                Wheat = CeilDiscount(cost.Wheat, percent),
                Sheep = CeilDiscount(cost.Sheep, percent),
                Stone = CeilDiscount(cost.Stone, percent)
            };
        }

        private static int CeilDiscount(int v, float pct) =>
            v == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(v * (1f - pct)));
    }
}
