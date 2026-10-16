using System.Collections.Generic;
using CatanRoguelike.Core.Hex;

namespace CatanRoguelike.Core.Shop
{
    /// <summary>Classic Catan harbor spacing: no two ports on adjacent coastal vertices.</summary>
    public static class PortSpacingRules
    {
        public static bool AreTooClose(HexMath.Vertex a, HexMath.Vertex b)
        {
            a = VertexGraph.Canonicalize(a);
            b = VertexGraph.Canonicalize(b);
            if (a.Equals(b))
                return true;

            foreach (var neighbor in VertexGraph.GetAdjacentVertices(a))
            {
                if (neighbor.Equals(b))
                    return true;
            }

            return false;
        }

        public static bool ConflictsWithAny(HexMath.Vertex candidate, IReadOnlyCollection<HexMath.Vertex> used)
        {
            foreach (var placed in used)
            {
                if (AreTooClose(candidate, placed))
                    return true;
            }

            return false;
        }
    }
}
