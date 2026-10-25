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
        /// Classic Catan scales specific 2:1 + generic 3:1 ports to map size.
        /// Not every coastal vertex is a port — sparse harbor layout.
        /// </summary>
        public static List<PortDefinition> DiscoverPorts(BoardState board)
        {
            int landTiles = board.Tiles.Values.Count(t => !t.IsFishTile || t.IsDesert);
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

        /// <summary>
        /// v143: every corner a harbour serves. A harbour that sits in a sea slot is drawn as two piers onto both ends
        /// of the coast edge facing that slot (classic Catan), so a building on EITHER pier corner uses it — before
        /// v143 only the single discovery vertex counted, so a settlement on the other pier silently got nothing.
        /// Always includes <see cref="PortDefinition.Vertex"/>.
        /// </summary>
        public static IReadOnlyList<HexMath.Vertex> ServedVertices(BoardState board, PortDefinition port)
        {
            var v = VertexGraph.Canonicalize(port.Vertex);
            var list = new List<HexMath.Vertex> { v };
            if (board == null || !board.TryGetSeaSlotForPort(v, out var sea))
                return list;
            // Same edge choice as SeaFrameSlotLayout.TryGetHarborCoastEdge (what PortMarkerVisuals draws piers onto).
            foreach (var hex in VertexGraph.GetHexesForVertex(v))
            {
                if (!board.Tiles.ContainsKey(hex))
                    continue;
                for (int d = 0; d < 6; d++)
                {
                    if (HexMath.GetNeighbor(hex, d) != sea)
                        continue;
                    var edge = HexMath.GetEdgeFacing(hex, d);
                    foreach (var end in new[] { VertexGraph.Canonicalize(edge.A), VertexGraph.Canonicalize(edge.B) })
                        if (!list.Contains(end))
                            list.Add(end);
                    return list;
                }
            }

            return list;
        }

        /// <summary>v143: true when <paramref name="player"/> has a building on any corner the harbour serves.</summary>
        public static bool PlayerControlsPort(BoardState board, PlayerId player, PortDefinition port)
        {
            foreach (var v in ServedVertices(board, port))
                if (PlayerControlsVertex(board, player, v))
                    return true;
            return false;
        }

        /// <summary>v143: true when <paramref name="vertex"/> is one of the harbour's pier corners.</summary>
        public static bool Serves(BoardState board, PortDefinition port, HexMath.Vertex vertex)
        {
            vertex = VertexGraph.Canonicalize(vertex);
            foreach (var v in ServedVertices(board, port))
                if (v.Equals(vertex))
                    return true;
            return false;
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
                && PlayerControlsPort(board, player, p)
                && !IsPortVertexBlocked(p.Vertex, blockedPortVertex));
        }

        public static bool HasGenericPort(BoardState board, PlayerId player, IReadOnlyList<PortDefinition> ports,
            HexMath.Vertex? blockedPortVertex = null)
        {
            return ports.Any(p => p.IsGeneric
                && PlayerControlsPort(board, player, p)
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
