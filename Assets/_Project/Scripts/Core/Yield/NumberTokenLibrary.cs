using System;
using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Yield
{
    /// <summary>Classic Catan number tokens (2–12, no 7) and pip weights for AI / events.</summary>
    public static class NumberTokenLibrary
    {
        /// <summary>Full 18-token multiset for a 19-hex board with one desert.</summary>
        public static readonly int[] ClassicPool =
        {
            2, 3, 3, 4, 4, 5, 5, 6, 6, 8, 8, 9, 9, 10, 10, 11, 11, 12
        };

        public static bool IsRedNumber(int token) => token == 6 || token == 8;

        /// <summary>Out of 36 two-dice outcomes — used for AI placement and robber targeting.</summary>
        public static int GetPipWeight(int token) => token switch
        {
            2 or 12 => 1,
            3 or 11 => 2,
            4 or 10 => 3,
            5 or 9 => 4,
            6 or 8 => 5,
            7 => 0,
            _ => 0
        };

        /// <summary>Assign tokens to all non-desert tiles that lack one.</summary>
        public static void AssignMissingTokens(BoardState board, int? seed = null)
        {
            var needing = board.Tiles.Values
                .Where(t => !t.IsDesert && !t.NumberToken.HasValue)
                .OrderBy(t => t.Coord.Q)
                .ThenBy(t => t.Coord.R)
                .ToList();

            if (needing.Count == 0)
                return;

            var used = new List<int>();
            foreach (var tile in board.Tiles.Values)
            {
                if (tile.NumberToken.HasValue)
                    used.Add(tile.NumberToken.Value);
            }

            var available = BuildAvailablePool(used);
            if (available.Count < needing.Count)
                throw new InvalidOperationException(
                    $"Not enough number tokens ({available.Count}) for {needing.Count} hexes.");

            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var coords = needing.Select(t => t.Coord).ToList();
            Shuffle(coords, rng);

            foreach (var coord in coords)
            {
                var tile = board.Tiles[coord];
                int? pick = PickTokenForTile(board, coord, available);
                if (!pick.HasValue)
                    pick = available[0];

                tile.NumberToken = pick;
                available.Remove(pick.Value);
            }
        }

        private static List<int> BuildAvailablePool(IReadOnlyList<int> used)
        {
            var available = new List<int>(ClassicPool);
            foreach (var token in used)
                available.Remove(token);
            return available;
        }

        private static int? PickTokenForTile(BoardState board, HexCoord coord, List<int> available)
        {
            int? best = null;
            int bestPip = -1;

            foreach (var token in available.OrderBy(t => t))
            {
                if (!CanPlaceToken(board, coord, token))
                    continue;

                int pip = GetPipWeight(token);
                if (pip > bestPip)
                {
                    bestPip = pip;
                    best = token;
                }
            }

            if (best.HasValue)
                return best;

            foreach (var token in available.OrderBy(t => t))
            {
                if (CanPlaceToken(board, coord, token))
                    return token;
            }

            return available.Count > 0 ? available[0] : (int?)null;
        }

        private static bool CanPlaceToken(BoardState board, HexCoord coord, int token)
        {
            if (!IsRedNumber(token))
                return true;

            foreach (var dir in HexCoord.Directions)
            {
                var neighbor = coord + dir;
                if (!board.TryGetTile(neighbor, out var other)) continue;
                if (!other.NumberToken.HasValue) continue;
                if (IsRedNumber(other.NumberToken.Value))
                    return false;
            }

            return true;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
