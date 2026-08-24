using System;
using System.Collections.Generic;

namespace CatanRoguelike.Core.Hex
{
    public static class HexMath
    {
        public const float OuterRadius = 1f;
        public const float InnerRadius = OuterRadius * 0.8660254f;

        public static HexCoord GetNeighbor(HexCoord hex, int direction)
        {
            direction = ((direction % 6) + 6) % 6;
            return hex + HexCoord.Directions[direction];
        }

        public static int Distance(HexCoord a, HexCoord b)
        {
            return (Math.Abs(a.Q - b.Q)
                + Math.Abs(a.Q + a.R - b.Q - b.R)
                + Math.Abs(a.R - b.R)) / 2;
        }

        /// <summary>World position for pointy-top hex center (XZ plane, Y up).</summary>
        public static (float x, float z) ToWorldPosition(HexCoord hex, float scale = 1f)
        {
            float x = scale * InnerRadius * (hex.Q + hex.R * 0.5f) * 2f / 1.7320508f;
            float z = scale * OuterRadius * 1.5f * hex.R;
            return (x, z);
        }

        /// <summary>World position of a hex corner (pointy-top, corner 0 at top).</summary>
        public static (float x, float z) VertexToWorldPosition(HexCoord hex, int cornerIndex, float scale = 1f)
        {
            var (cx, cz) = ToWorldPosition(hex, scale);
            float angleRad = (60f * cornerIndex) * (float)(Math.PI / 180f);
            float r = scale * OuterRadius;
            float x = cx + r * (float)Math.Sin(angleRad);
            float z = cz + r * (float)Math.Cos(angleRad);
            return (x, z);
        }

        public static (float x, float z) VertexToWorldPosition(Vertex vertex, float scale = 1f) =>
            VertexToWorldPosition(vertex.Hex, vertex.CornerIndex, scale);

        public static (float x, float z) EdgeMidpoint(Edge edge, float scale = 1f)
        {
            var (ax, az) = VertexToWorldPosition(edge.A, scale);
            var (bx, bz) = VertexToWorldPosition(edge.B, scale);
            return ((ax + bx) * 0.5f, (az + bz) * 0.5f);
        }

        public static float EdgeAngle(Edge edge)
        {
            var (ax, az) = VertexToWorldPosition(edge.A, 1f);
            var (bx, bz) = VertexToWorldPosition(edge.B, 1f);
            return (float)(Math.Atan2(bx - ax, bz - az) * 180f / Math.PI);
        }

        public static IEnumerable<HexCoord> Ring(HexCoord center, int radius)
        {
            if (radius == 0)
            {
                yield return center;
                yield break;
            }

            var hex = center + new HexCoord(-radius, radius);
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < radius; j++)
                {
                    yield return hex;
                    hex = GetNeighbor(hex, i);
                }
            }
        }

        public static IEnumerable<HexCoord> Spiral(HexCoord center, int maxRadius)
        {
            yield return center;
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                foreach (var hex in Ring(center, radius))
                    yield return hex;
            }
        }

        public readonly struct Vertex : IEquatable<Vertex>
        {
            public HexCoord Hex { get; }
            public int CornerIndex { get; }

            public Vertex(HexCoord hex, int cornerIndex)
            {
                Hex = hex;
                CornerIndex = ((cornerIndex % 6) + 6) % 6;
            }

            public bool Equals(Vertex other) => Hex.Equals(other.Hex) && CornerIndex == other.CornerIndex;
            public override bool Equals(object obj) => obj is Vertex other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Hex, CornerIndex);
            public override string ToString() => $"V({Hex},{CornerIndex})";

            public static bool operator ==(Vertex a, Vertex b) => a.Equals(b);
            public static bool operator !=(Vertex a, Vertex b) => !a.Equals(b);
        }

        public readonly struct Edge : IEquatable<Edge>
        {
            public Vertex A { get; }
            public Vertex B { get; }

            public Edge(Vertex a, Vertex b)
            {
                if (Compare(a, b) > 0)
                    (a, b) = (b, a);
                A = a;
                B = b;
            }

            private static int Compare(Vertex a, Vertex b)
            {
                int cmp = a.Hex.Q.CompareTo(b.Hex.Q);
                if (cmp != 0) return cmp;
                cmp = a.Hex.R.CompareTo(b.Hex.R);
                if (cmp != 0) return cmp;
                return a.CornerIndex.CompareTo(b.CornerIndex);
            }

            public bool Equals(Edge other) => A.Equals(other.A) && B.Equals(other.B);
            public override bool Equals(object obj) => obj is Edge other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(A, B);
            public override string ToString() => $"E({A}-{B})";

            public static bool operator ==(Edge a, Edge b) => a.Equals(b);
            public static bool operator !=(Edge a, Edge b) => !a.Equals(b);
        }

        public static Vertex GetVertex(HexCoord hex, int corner) => new(hex, corner);

        public static Edge GetEdge(HexCoord hex, int edgeIndex)
        {
            int c0 = edgeIndex % 6;
            int c1 = (edgeIndex + 1) % 6;
            return new Edge(new Vertex(hex, c0), new Vertex(hex, c1));
        }
    }
}
