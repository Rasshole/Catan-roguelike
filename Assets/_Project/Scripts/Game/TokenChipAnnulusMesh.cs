using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Flat annulus cap (inner/outer radius in mesh XZ, top at y=1) for token rim rings.</summary>
    public static class TokenChipAnnulusMesh
    {
        /// <summary>Slightly inside the face disc edge so the rim band underlaps the face (no hex gap).</summary>
        public const float InnerRadius = ThinDiskMesh.MeshRadius * 0.76f;
        /// <summary>Matches <see cref="HexTileView.RimOuterRadiusFactor"/> (15% rim band past face edge).</summary>
        public const float OuterRadius = ThinDiskMesh.MeshRadius * HexTileView.RimOuterRadiusFactor;
        public const float TopY = ThinDiskMesh.TopY;
        public const int SegmentCount = ThinDiskMesh.SegmentCount;

        private static Mesh _shared;

        public static Mesh Shared => _shared ??= Create();

        public static Mesh Create(string name = "TokenChipAnnulus")
        {
            var mesh = new Mesh { name = name };
            int segments = SegmentCount;
            int vertCount = segments * 2 + 2;
            var vertices = new Vector3[vertCount];
            var triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[i] = new Vector3(cos * InnerRadius, TopY, sin * InnerRadius);
                vertices[i + segments] = new Vector3(cos * OuterRadius, TopY, sin * OuterRadius);
            }

            int tri = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int innerA = i;
                int innerB = next;
                int outerA = i + segments;
                int outerB = next + segments;

                triangles[tri++] = innerA;
                triangles[tri++] = outerA;
                triangles[tri++] = outerB;

                triangles[tri++] = innerA;
                triangles[tri++] = outerB;
                triangles[tri++] = innerB;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
