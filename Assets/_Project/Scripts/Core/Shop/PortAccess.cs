using System.Collections.Generic;
using System.Linq;
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
        /// Classic Catan scales specific 2:1 + generic 3:1 ports to map size.
        /// Not every coastal vertex is a port — sparse harbor layout.
        /// </summary>
        public static List<PortDefinition> DiscoverPorts(BoardState board)
        {
            var edgeVertices = CollectBoardEdgeVertices(board);
            var (specificTarget, genericTarget) = GetPortTargets(board.Tiles.Count);

            var ports = new List<PortDefinition>();
            var used = new HashSet<HexMath.Vertex>();

            foreach (var resource in System.Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>())
            {
                if (ports.Count(p => !p.IsGeneric) >= specificTarget)
                    break;

                foreach (var vertex in edgeVertices)
                {
                    if (used.Contains(vertex)) continue;
                    if (!VertexTouchesCoastalResource(board, vertex, resource)) continue;

                    ports.Add(new PortDefinition(vertex, resource));
                    used.Add(vertex);
                    break;
                }
            }

            var remaining = edgeVertices.Where(v => !used.Contains(v)).ToList();
            int genericCount = System.Math.Min(genericTarget, remaining.Count);
            for (int i = 0; i < genericCount; i++)
            {
                int idx = (i * remaining.Count) / genericCount;
                var vertex = remaining[idx];
                ports.Add(new PortDefinition(vertex, null));
                used.Add(vertex);
            }

            return ports;
        }

        private static (int specific, int generic) GetPortTargets(int tileCount) => tileCount switch
        {
            7 => (3, 2),
            13 => (4, 3),
            19 => (5, 4),
            _ => (System.Math.Min(5, tileCount / 2), System.Math.Max(1, tileCount / 6))
        };

        private static List<HexMath.Vertex> CollectBoardEdgeVertices(BoardState board)
        {
            var seen = new HashSet<HexMath.Vertex>();
            var edgeVertices = new List<HexMath.Vertex>();

            foreach (var hex in board.Tiles.Values)
            {
                if (!hex.IsCoastal) continue;

                for (int c = 0; c < 6; c++)
                {
                    var vertex = VertexGraph.Canonicalize(new HexMath.Vertex(hex.Coord, c));
                    if (!seen.Add(vertex)) continue;
                    if (!IsBoardEdgeVertex(board, vertex)) continue;
                    edgeVertices.Add(vertex);
                }
            }

            edgeVertices.Sort(CompareVertices);
            return edgeVertices;
        }

        private static int CompareVertices(HexMath.Vertex a, HexMath.Vertex b)
        {
            int cmp = a.Hex.Q.CompareTo(b.Hex.Q);
            if (cmp != 0) return cmp;
            cmp = a.Hex.R.CompareTo(b.Hex.R);
            if (cmp != 0) return cmp;
            return a.CornerIndex.CompareTo(b.CornerIndex);
        }

        private static bool VertexTouchesCoastalResource(BoardState board, HexMath.Vertex vertex,
            ResourceType resource)
        {
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (!board.Tiles.TryGetValue(hex, out var tile)) continue;
                if (tile.IsCoastal && tile.Resource == resource)
                    return true;
            }
            return false;
        }

        private static bool IsBoardEdgeVertex(BoardState board, HexMath.Vertex vertex)
        {
            int onBoard = 0;
            foreach (var hex in VertexGraph.GetHexesForVertex(vertex))
            {
                if (board.Tiles.ContainsKey(hex)) onBoard++;
            }
            return onBoard <= 2;
        }

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
