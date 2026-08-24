using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;
using Edge = CatanRoguelike.Core.Hex.HexMath.Edge;

namespace CatanRoguelike.Core.Map
{
    public static class VertexGraph
    {
        /// <summary>Returns the 3 hex tiles that may touch a canonical vertex.</summary>
        public static IEnumerable<HexCoord> GetHexesForVertex(Vertex vertex)
        {
            yield return vertex.Hex;

            int c = vertex.CornerIndex;
            yield return HexMath.GetNeighbor(vertex.Hex, (c + 2) % 6);
            yield return HexMath.GetNeighbor(vertex.Hex, (c + 3) % 6);
        }

        public static Vertex Canonicalize(Vertex vertex)
        {
            var candidates = new List<Vertex> { vertex };
            int c = vertex.CornerIndex;

            var h1 = HexMath.GetNeighbor(vertex.Hex, (c + 2) % 6);
            var h2 = HexMath.GetNeighbor(vertex.Hex, (c + 3) % 6);

            // Re-map corner indices for neighbor hexes (inverse mapping)
            candidates.Add(new Vertex(h1, (c + 4) % 6));
            candidates.Add(new Vertex(h2, (c + 1) % 6));

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
            int c = vertex.CornerIndex;
            yield return Canonicalize(new Vertex(vertex.Hex, (c + 5) % 6));
            yield return Canonicalize(new Vertex(vertex.Hex, (c + 1) % 6));

            var neighborHex = HexMath.GetNeighbor(vertex.Hex, c);
            yield return Canonicalize(new Vertex(neighborHex, (c + 3) % 6));
        }

        public static int VertexDistance(Vertex a, Vertex b)
        {
            if (a.Equals(b)) return 0;

            var visited = new HashSet<Vertex>();
            var queue = new Queue<(Vertex v, int dist)>();
            queue.Enqueue((Canonicalize(a), 0));
            visited.Add(Canonicalize(a));

            while (queue.Count > 0)
            {
                var (current, dist) = queue.Dequeue();
                if (current.Equals(Canonicalize(b)))
                    return dist;

                foreach (var neighbor in GetAdjacentVertices(current))
                {
                    var canon = Canonicalize(neighbor);
                    if (visited.Add(canon))
                        queue.Enqueue((canon, dist + 1));
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
