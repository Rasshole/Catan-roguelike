using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Yield
{
    /// <summary>Pick a representative hex per resource type for production fly animations.</summary>
    public static class ProductionFlySources
    {
        public static Dictionary<ResourceType, HexCoord> MapGainedResourcesToHexes(
            GameState state,
            PlayerId player,
            Dictionary<ResourceType, int> resourceRolls,
            ResourceBundle gained)
        {
            var result = new Dictionary<ResourceType, HexCoord>();
            var producing = ProductionHexResolver.GetProducingHexesForPlayer(state, player, resourceRolls);
            if (producing.Count == 0 || gained.Total == 0)
                return result;

            foreach (var pair in gained.EnumerateNonZero())
            {
                if (result.ContainsKey(pair.type))
                    continue;

                foreach (var hex in producing)
                {
                    if (!state.Board.TryGetTile(hex, out var tile))
                        continue;
                    if (tile.Resource != pair.type)
                        continue;
                    result[pair.type] = hex;
                    break;
                }
            }

            return result;
        }
    }
}
