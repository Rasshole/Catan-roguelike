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

        private const int MaxAssignmentAttempts = 96;

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

            int tokenSlots = board.Tiles.Values.Count(t => !t.IsDesert);
            var available = BuildAvailablePool(used, tokenSlots);
            if (available.Count < needing.Count)
                throw new InvalidOperationException(
                    $"Not enough number tokens ({available.Count}) for {needing.Count} hexes.");

            var coords = needing.Select(t => t.Coord).ToList();
            for (int attempt = 0; attempt < MaxAssignmentAttempts; attempt++)
            {
                int attemptSeed = ComposeAttemptSeed(seed, attempt, needing.Count);
                var rng = new Random(attemptSeed);
                var order = coords.ToList();
                Shuffle(order, rng);
                var pool = available.ToList();

                foreach (var coord in coords)
                    board.Tiles[coord].NumberToken = null;

                if (TryAssignInOrder(board, order, 0, pool) && !HasAdjacentRedTokens(board))
                    return;
            }

            if (TryRepairBySwapping(board, seed))
                return;

            throw new InvalidOperationException(
                "Could not assign number tokens without adjacent 6/8 pairs.");
        }

        /// <summary>Build token multiset large enough for <paramref name="tokenSlots"/> assignments.</summary>
        public static List<int> BuildAvailablePool(IReadOnlyList<int> alreadyAssigned, int tokenSlots)
        {
            var pool = new List<int>();
            while (pool.Count < tokenSlots)
                pool.AddRange(ClassicPool);

            if (pool.Count > tokenSlots)
                pool.RemoveRange(tokenSlots, pool.Count - tokenSlots);

            foreach (var token in alreadyAssigned)
                pool.Remove(token);

            return pool;
        }

        private static int ComposeAttemptSeed(int? seed, int attempt, int tileCount) =>
            unchecked((seed ?? 0x4B1D) ^ (attempt * 0x9E3779B9) ^ (tileCount * 0x85EBCA6B));

        private static bool TryAssignInOrder(BoardState board, IList<HexCoord> coords, int index, List<int> available)
        {
            if (index >= coords.Count)
                return true;

            var coord = coords[index];
            foreach (var token in available
                         .OrderByDescending(GetPipWeight)
                         .ThenBy(t => t))
            {
                if (!CanPlaceToken(board, coord, token))
                    continue;

                board.Tiles[coord].NumberToken = token;
                available.Remove(token);
                if (TryAssignInOrder(board, coords, index + 1, available))
                    return true;

                available.Add(token);
                board.Tiles[coord].NumberToken = null;
            }

            return false;
        }

        private static bool HasAdjacentRedTokens(BoardState board)
        {
            foreach (var tile in board.Tiles.Values)
            {
                if (!tile.NumberToken.HasValue || !IsRedNumber(tile.NumberToken.Value))
                    continue;

                foreach (var dir in HexCoord.Directions)
                {
                    var neighbor = tile.Coord + dir;
                    if (!board.TryGetTile(neighbor, out var other))
                        continue;
                    if (!other.NumberToken.HasValue)
                        continue;
                    if (IsRedNumber(other.NumberToken.Value))
                        return true;
                }
            }

            return false;
        }

        private static bool TryRepairBySwapping(BoardState board, int? seed)
        {
            var rng = new Random(ComposeAttemptSeed(seed, MaxAssignmentAttempts, board.Tiles.Count));
            var assignable = board.Tiles.Values
                .Where(t => !t.IsDesert && t.NumberToken.HasValue)
                .Select(t => t.Coord)
                .ToList();

            for (int pass = 0; pass < assignable.Count * 8; pass++)
            {
                if (!HasAdjacentRedTokens(board))
                    return true;

                int i = rng.Next(assignable.Count);
                int j = rng.Next(assignable.Count);
                if (i == j)
                    continue;

                var a = assignable[i];
                var b = assignable[j];
                if (board.Tiles[a].Coord == board.Tiles[b].Coord)
                    continue;

                int tokenA = board.Tiles[a].NumberToken!.Value;
                int tokenB = board.Tiles[b].NumberToken!.Value;
                if (tokenA == tokenB)
                    continue;

                if (!WouldSwapBeValid(board, a, b, tokenA, tokenB))
                    continue;

                board.Tiles[a].NumberToken = tokenB;
                board.Tiles[b].NumberToken = tokenA;
            }

            return !HasAdjacentRedTokens(board);
        }

        private static bool WouldSwapBeValid(
            BoardState board,
            HexCoord a,
            HexCoord b,
            int tokenAtA,
            int tokenAtB)
        {
            board.Tiles[a].NumberToken = null;
            board.Tiles[b].NumberToken = null;
            bool ok = CanPlaceToken(board, a, tokenAtB) && CanPlaceToken(board, b, tokenAtA);
            board.Tiles[a].NumberToken = tokenAtA;
            board.Tiles[b].NumberToken = tokenAtB;
            return ok;
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
