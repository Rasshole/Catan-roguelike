using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;

namespace CatanRoguelike.Core.Map
{
    /// <summary>
    /// Stable listing and index selection for opponent roads (Bandit Raid UI / tests).
    /// </summary>
    public static class OpponentRoadSelector
    {
        public static IReadOnlyList<HexMath.Edge> ListOpponentRoads(BoardState board, PlayerId player)
        {
            var opponent = player == PlayerId.Human ? PlayerId.Ai : PlayerId.Human;
            return board.Roads
                .Where(kv => kv.Value == opponent)
                .Select(kv => kv.Key)
                .OrderBy(e => e.ToString(), StringComparer.Ordinal)
                .ToList();
        }

        public static int ClampIndex(IReadOnlyList<HexMath.Edge> roads, int index)
        {
            if (roads == null || roads.Count == 0) return 0;
            return Math.Clamp(index, 0, roads.Count - 1);
        }

        public static HexMath.Edge? SelectRoad(IReadOnlyList<HexMath.Edge> roads, int index)
        {
            if (roads == null || roads.Count == 0) return null;
            return roads[ClampIndex(roads, index)];
        }
    }
}
