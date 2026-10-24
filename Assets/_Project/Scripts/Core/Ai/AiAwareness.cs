using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Yield;

namespace CatanRoguelike.Core.Ai
{
    /// <summary>
    /// v132: basic AI awareness of the human civilization and the active event. Pure scoring helpers the
    /// <see cref="AiController"/> folds into robber and settlement choices.
    /// </summary>
    public static class AiAwareness
    {
        /// <summary>The resource the human civilization's passive leans on (robbing it hurts most), or null.</summary>
        public static ResourceType? CivFavouredResource(GameState state)
        {
            if (state == null)
                return null;
            return state.Civilization switch
            {
                CivilizationId.HighlandClans => ResourceType.Stone,
                CivilizationId.ShepherdKingdom => ResourceType.Sheep,
                CivilizationId.ForestFolk => ResourceType.Wood,
                CivilizationId.Brickwrights => ResourceType.Brick,
                _ => (ResourceType?)null
            };
        }

        /// <summary>True when parking the robber on a human hex actually blocks their production.</summary>
        public static bool RobberBlocksHuman(GameState state) =>
            !CivilizationRules.IgnoresRobber(state, PlayerId.Human);

        /// <summary>
        /// Value of putting the robber on <paramref name="tile"/>. Against the Sun Kingdom only the steal matters
        /// (no pip weight); otherwise civ-favoured and event-boosted hexes are worth more and an already-blighted
        /// resource is worth little. Hexes the AI itself builds on are penalised.
        /// </summary>
        public static float RobberHexScore(GameState state, HexTileData tile, int humanBuildings, int aiBuildings)
        {
            if (state == null || tile == null || humanBuildings <= 0 || tile.IsFishTile)
                return 0f;
            int pip = tile.NumberToken.HasValue && !tile.IsDesert
                ? NumberTokenLibrary.GetPipWeight(tile.NumberToken.Value)
                : 0;

            if (!RobberBlocksHuman(state))
                return humanBuildings - aiBuildings * pip;

            float score = humanBuildings * System.Math.Max(1, pip);
            if (!tile.IsDesert)
            {
                if (CivFavouredResource(state) == tile.Resource)
                    score *= 1.5f;
                if (state.EventBonusResource == tile.Resource)
                    score *= 1.5f;
                if (state.EventBlockedResource == tile.Resource)
                    score *= 0.25f;
            }

            return score - aiBuildings * pip;
        }

        /// <summary>Settlement-spot adjustment for one adjacent hex from today's event (bounty, blight, storm).</summary>
        public static float SettlementHexEventBonus(GameState state, HexTileData tile)
        {
            if (state == null || tile == null || tile.IsDesert || tile.IsFishTile)
                return 0f;
            float bonus = 0f;
            if (state.EventBonusResource == tile.Resource)
                bonus += 1.5f;
            if (state.EventBlockedResource == tile.Resource)
                bonus -= 1f;
            if (state.EventStormTile.HasValue && state.EventStormTile.Value.Equals(tile.Coord))
                bonus -= 1f;
            return bonus;
        }
    }
}
