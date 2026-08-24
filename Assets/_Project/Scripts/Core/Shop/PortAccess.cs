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
        public static List<PortDefinition> DiscoverPorts(BoardState board)
        {
            var ports = new List<PortDefinition>();
            var seen = new HashSet<HexMath.Vertex>();

            foreach (var hex in board.Tiles.Values)
            {
                if (!hex.IsCoastal) continue;

                for (int c = 0; c < 6; c++)
                {
                    var vertex = VertexGraph.Canonicalize(new HexMath.Vertex(hex.Coord, c));
                    if (!seen.Add(vertex)) continue;
                    if (!IsBoardEdgeVertex(board, vertex)) continue;

                    ports.Add(new PortDefinition(vertex, hex.Resource));
                }
            }

            return ports;
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
            IReadOnlyList<PortDefinition> ports)
        {
            return ports.Any(p => p.SpecificResource == resource
                && PlayerControlsVertex(board, player, p.Vertex));
        }

        public static bool HasGenericPort(BoardState board, PlayerId player, IReadOnlyList<PortDefinition> ports)
        {
            return ports.Any(p => p.IsGeneric && PlayerControlsVertex(board, player, p.Vertex));
        }

        public static int GetEffectiveGiveAmount(BoardState board, PlayerId player, ShopDeal deal,
            IReadOnlyList<PortDefinition> ports)
        {
            int amount = deal.GiveAmount;

            if (HasSpecificPort(board, player, deal.Give, ports))
                return System.Math.Min(amount, 2);

            if (HasGenericPort(board, player, ports))
                return System.Math.Min(amount, 3);

            return amount;
        }
    }
}
