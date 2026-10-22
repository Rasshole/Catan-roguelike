using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;

namespace CatanRoguelike.Core.Civilizations
{
    /// <summary>
    /// Every civilization rule hook (human player only; the AI has no civilization). Each method is a
    /// pure function of the state so the rules are unit-testable without the controller.
    /// </summary>
    public static class CivilizationRules
    {
        public const int MerchantBankRate = 3;
        public const int RaiderBankRate = 5;
        public const int ShepherdStartWool = 2;

        public static bool Is(GameState state, PlayerId player, CivilizationId id) =>
            state != null && player == PlayerId.Human && state.Civilization == id;

        /// <summary>Per-hit yield change on a producing hex of <paramref name="resource"/>.</summary>
        public static int HexYieldDeltaPerHit(GameState state, PlayerId player, ResourceType resource)
        {
            if (player != PlayerId.Human || state == null)
                return 0;
            switch (state.Civilization)
            {
                case CivilizationId.HighlandClans:
                    if (resource == ResourceType.Stone) return 1;
                    if (resource == ResourceType.Sheep) return -1;
                    return 0;
                case CivilizationId.ShepherdKingdom:
                    return resource == ResourceType.Sheep ? 1 : 0;
                default:
                    return 0;
            }
        }

        /// <summary>Production that comes from the dice sums themselves (Forest Folk on 2 and 12).</summary>
        public static ResourceBundle DiceBonus(GameState state, PlayerId player, IReadOnlyList<int> diceRolls)
        {
            var bonus = ResourceBundle.Zero;
            if (!Is(state, player, CivilizationId.ForestFolk) || diceRolls == null)
                return bonus;

            int forests = CountBorderedHexes(state, player, ResourceType.Wood);
            foreach (int roll in diceRolls)
            {
                if (roll == 2 || roll == 12)
                    bonus.Add(ResourceType.Wood, Math.Max(1, forests));
            }

            return bonus;
        }

        /// <summary>Applies the 7-roll civilization effects; returns a log line (or null).</summary>
        public static string OnSevenRolled(GameState state, Random random)
        {
            if (state == null)
                return null;
            if (state.Civilization == CivilizationId.Brickwrights)
            {
                var inv = state.PlayerInventory;
                inv.Add(ResourceType.Brick, 1);
                state.PlayerInventory = inv;
                return "Brickwrights: +1 Brick from the kilns.";
            }

            if (state.Civilization == CivilizationId.NorthernRaiders)
            {
                var ai = state.AiInventory;
                var owned = ResourceTypes.Productive.Where(r => ai[r] > 0).ToList();
                if (owned.Count == 0)
                    return "Raiders: the opponent had nothing to plunder.";
                var pick = owned[random != null ? random.Next(owned.Count) : 0];
                ai.Set(pick, ai[pick] - 1);
                state.AiInventory = ai;
                var me = state.PlayerInventory;
                me.Add(pick, 1);
                state.PlayerInventory = me;
                return $"Raiders plundered 1 {Events.EventText.ResourceName(pick)}.";
            }

            return null;
        }

        public static ResourceBundle ApplyCostModifiers(GameState state, PlayerId player, ResourceBundle cost,
            bool isSettlement, bool isCity, bool isRoad)
        {
            if (player != PlayerId.Human || state == null)
                return cost;
            switch (state.Civilization)
            {
                case CivilizationId.SteppeNomads:
                    if (isSettlement) cost.Brick = 0;
                    if (isCity) cost.Stone += 1;
                    break;
                case CivilizationId.ShepherdKingdom:
                    if (isCity) cost.Wheat += 1;
                    break;
                case CivilizationId.Brickwrights:
                    if (isRoad) cost.Wood = 0;
                    break;
                case CivilizationId.Academy:
                    if (isSettlement) cost.Wheat += 1;
                    break;
            }

            return cost;
        }

        /// <summary>The no-port bank rate for the player (4:1 unless the civilization changes it).</summary>
        public static int BaseBankRate(GameState state, PlayerId player, int defaultRate)
        {
            if (player != PlayerId.Human || state == null)
                return defaultRate;
            return state.Civilization switch
            {
                CivilizationId.MerchantRepublic => Math.Min(defaultRate, MerchantBankRate),
                CivilizationId.NorthernRaiders => Math.Max(defaultRate, RaiderBankRate),
                _ => defaultRate
            };
        }

        public static int ExtraNightDraws(GameState state) =>
            state != null && state.Civilization == CivilizationId.Academy ? 1 : 0;

        public static int FishCost(GameState state, PlayerId player, FishermenSpendAction action)
        {
            int cost = FishermenRules.GetFishCost(action);
            return Is(state, player, CivilizationId.SeaFolk) ? Math.Max(1, cost - 1) : cost;
        }

        /// <summary>Sea Folk: +1 fish on every production in which their grounds caught anything.</summary>
        public static int FishCatchBonus(GameState state, PlayerId player, int caught) =>
            caught > 0 && Is(state, player, CivilizationId.SeaFolk) ? 1 : 0;

        public static bool IgnoresRobber(GameState state, PlayerId player) =>
            Is(state, player, CivilizationId.SunKingdom);

        /// <summary>One-time bonus when the civilization is chosen.</summary>
        public static void ApplyStartBonus(GameState state)
        {
            if (state.Civilization == CivilizationId.ShepherdKingdom)
            {
                var inv = state.PlayerInventory;
                inv.Add(ResourceType.Sheep, ShepherdStartWool);
                state.PlayerInventory = inv;
            }
        }

        internal static int CountBorderedHexes(GameState state, PlayerId player, ResourceType resource)
        {
            var hexes = new HashSet<Hex.HexCoord>();
            foreach (var kvp in state.Board.VertexBuildings)
            {
                if (kvp.Value.owner != player || kvp.Value.type == BuildingType.None)
                    continue;
                foreach (var hex in VertexGraph.GetHexesForVertex(kvp.Key))
                {
                    if (state.Board.TryGetTile(hex, out var tile) && !tile.IsDesert && !tile.IsFishTile
                        && tile.Resource == resource)
                        hexes.Add(hex);
                }
            }

            return hexes.Count;
        }
    }
}
