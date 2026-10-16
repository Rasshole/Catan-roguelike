using System.Collections.Generic;
using System.Linq;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;

namespace CatanRoguelike.Core.Shop
{
    /// <summary>
    /// Port on a board-edge vertex. Specific = 2:1 for that resource; generic = 3:1 any.
    /// </summary>
    public readonly struct PortDefinition
    {
        public HexMath.Vertex Vertex { get; }
        public ResourceType? SpecificResource { get; }

        public bool IsGeneric => !SpecificResource.HasValue;

        public PortDefinition(HexMath.Vertex vertex, ResourceType? specificResource = null)
        {
            Vertex = VertexGraph.Canonicalize(vertex);
            SpecificResource = specificResource;
        }
    }

    public static class PortAccess
    {
        /// <summary>
        /// Classic Catan harbor counts (5×2:1 + 4×3:1 on the 19-hex frame) scaled for other sizes.
        /// Harbors are spaced so no two share a coastal vertex or adjacent coast vertices.
        /// </summary>
        public static List<PortDefinition> DiscoverPorts(BoardState board)
        {
            int landTiles = board.Tiles.Values.Count(t => !t.IsFishTile);
            var (specificTarget, genericTarget) = GetPortTargets(landTiles);
            return PortFrameLayout.DiscoverFramePorts(board, specificTarget, genericTarget);
        }

        private static (int specific, int generic) GetPortTargets(int tileCount) => tileCount switch
        {
            7 => (3, 2),
            13 => (4, 3),
            19 => (5, 4),
            30 => (7, 4),
            37 => (7, 5),
            _ => (System.Math.Min(5, tileCount / 2), System.Math.Max(1, tileCount / 6))
        };

        public static bool PlayerControlsVertex(BoardState board, PlayerId player, HexMath.Vertex vertex)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            return board.VertexBuildings.TryGetValue(vertex, out var b) && b.owner == player;
        }

        public static bool HasSpecificPort(BoardState board, PlayerId player, ResourceType resource,
            IReadOnlyList<PortDefinition> ports, HexMath.Vertex? blockedPortVertex = null)
        {
            return ports.Any(p => p.SpecificResource == resource
                && PlayerControlsVertex(board, player, p.Vertex)
                && !IsPortVertexBlocked(p.Vertex, blockedPortVertex));
        }

        public static bool HasGenericPort(BoardState board, PlayerId player, IReadOnlyList<PortDefinition> ports,
            HexMath.Vertex? blockedPortVertex = null)
        {
            return ports.Any(p => p.IsGeneric
                && PlayerControlsVertex(board, player, p.Vertex)
                && !IsPortVertexBlocked(p.Vertex, blockedPortVertex));
        }

        public static int GetEffectiveGiveAmount(BoardState board, PlayerId player, ShopDeal deal,
            IReadOnlyList<PortDefinition> ports, HexMath.Vertex? blockedPortVertex = null)
        {
            int amount = deal.GiveAmount;

            if (HasSpecificPort(board, player, deal.Give, ports, blockedPortVertex))
                return System.Math.Min(amount, 2);

            if (HasGenericPort(board, player, ports, blockedPortVertex))
                return System.Math.Min(amount, 3);

            return amount;
        }

        private static bool IsPortVertexBlocked(HexMath.Vertex portVertex, HexMath.Vertex? blockedPortVertex) =>
            blockedPortVertex.HasValue && portVertex.Equals(blockedPortVertex.Value);
    }
}
