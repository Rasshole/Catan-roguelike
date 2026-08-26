using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Map
{
    public static class VertexGraph
    {
        /// <summary>
        /// Three hexes that meet at this vertex.
        /// Corners run clockwise from north; axial directions run CCW from east,
        /// so the other two hexes are neighbors (c+4) and (c+5).
        /// </summary>
        public static IEnumerable<HexCoord> GetHexesForVertex(Vertex vertex)
        {
            int c = vertex.CornerIndex;
            yield return vertex.Hex;
            yield return HexMath.GetNeighbor(vertex.Hex, (c + 4) % 6);
            yield return HexMath.GetNeighbor(vertex.Hex, (c + 5) % 6);
        }

        /// <summary>
        /// Unique representative of a geometric vertex. Idempotent: the three
        /// (hex, corner) labels of the same point map to one lex-smallest vertex.
        /// </summary>
        public static Vertex Canonicalize(Vertex vertex)
        {
            int c = vertex.CornerIndex;
            var candidates = new[]
            {
                vertex,
                new Vertex(HexMath.GetNeighbor(vertex.Hex, (c + 5) % 6), (c + 4) % 6),
                new Vertex(HexMath.GetNeighbor(vertex.Hex, (c + 4) % 6), (c + 2) % 6)
            };

            Vertex best = candidates[0];
            foreach (var v in candidates)
            {
                if (Compare(v, best) < 0)
                    best = v;
            }
            return best;
        }

        private static int Compare(Vertex a, Vertex b)
        {
            int cmp = a.Hex.Q.CompareTo(b.Hex.Q);
            if (cmp != 0) return cmp;
            cmp = a.Hex.R.CompareTo(b.Hex.R);
            if (cmp != 0) return cmp;
            return a.CornerIndex.CompareTo(b.CornerIndex);
        }

        public static IEnumerable<Vertex> GetAdjacentVertices(Vertex vertex)
        {
            vertex = Canonicalize(vertex);
            int c = vertex.CornerIndex;
            yield return Canonicalize(new Vertex(vertex.Hex, (c + 5) % 6));
            yield return Canonicalize(new Vertex(vertex.Hex, (c + 1) % 6));
            yield return Canonicalize(new Vertex(
                HexMath.GetNeighbor(vertex.Hex, (c + 5) % 6), (c + 5) % 6));
        }

        public static int VertexDistance(Vertex a, Vertex b)
        {
            a = Canonicalize(a);
            b = Canonicalize(b);
            if (a.Equals(b)) return 0;

            var visited = new HashSet<Vertex> { a };
            var queue = new Queue<(Vertex v, int dist)>();
            queue.Enqueue((a, 0));

            while (queue.Count > 0)
            {
                var (current, dist) = queue.Dequeue();
                foreach (var neighbor in GetAdjacentVertices(current))
                {
                    if (neighbor.Equals(b))
                        return dist + 1;
                    if (visited.Add(neighbor))
                        queue.Enqueue((neighbor, dist + 1));
                }
            }

            return int.MaxValue;
        }

        public static Edge GetEdgeBetween(Vertex a, Vertex b)
        {
            return new Edge(Canonicalize(a), Canonicalize(b));
        }
    }
}
