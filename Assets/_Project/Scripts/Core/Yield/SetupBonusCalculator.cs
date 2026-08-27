using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Yield
{
    /// <summary>
    /// Classic Catan setup bonus: the second settlement grants one of each adjacent resource.
    /// Desert tiles and off-board hexes are skipped.
    /// </summary>
    public static class SetupBonusCalculator
    {
        public static ResourceBundle CalculateForVertex(BoardState board, HexMath.Vertex vertex)
        {
            var bonus = ResourceBundle.Zero;
            vertex = VertexGraph.Canonicalize(vertex);

            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (!board.TryGetTile(hex, out var tile)) continue;
                if (tile.IsDesert) continue;
                bonus.Add(tile.Resource, 1);
            }

            return bonus;
        }
    }
}
