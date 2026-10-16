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

                HexCoord? best = null;
                foreach (var hex in producing)
                {
                    if (!state.Board.TryGetTile(hex, out var tile))
                        continue;
                    if (tile.Resource != pair.type)
                        continue;
                    if (!best.HasValue || CompareHexCoords(hex, best.Value) < 0)
                        best = hex;
                }

                if (best.HasValue)
                    result[pair.type] = best.Value;
            }

            return result;
        }

        private static int CompareHexCoords(HexCoord a, HexCoord b)
        {
            int cmp = a.Q.CompareTo(b.Q);
            return cmp != 0 ? cmp : a.R.CompareTo(b.R);
        }
    }
}
