using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Pointy-top hexagonal prism whose top ring matches settlement vertex positions from
    /// <see cref="HexMath.VertexToWorldPosition"/> on the canonical graph vertex for each
    /// corner. Top at local y=+1, bottom at y=-1; XZ vertices are already in world units at
    /// <paramref name="hexScale"/> so tile transforms use XZ scale 1.
    /// </summary>
    public static class HexPrismMesh
    {
        public const float TopY = 1f;
        public const float BottomY = -1f;
        public const int SideCount = 6;

        public static Mesh Create(HexCoord hex, float hexScale)
        {
            var mesh = new Mesh { name = $"HexPrism_{hex.Q}_{hex.R}" };

            int ringVerts = SideCount;
            int vertCount = ringVerts * 2 + 2;
            var vertices = new Vector3[vertCount];
            var triangles = new int[SideCount * 2 * 3 + SideCount * 2 * 3];

            int topCenter = ringVerts * 2;
            int bottomCenter = topCenter + 1;
            vertices[topCenter] = new Vector3(0f, TopY, 0f);
            vertices[bottomCenter] = new Vector3(0f, BottomY, 0f);

            var (centerX, centerZ) = HexMath.ToWorldPosition(hex, hexScale);
            for (int i = 0; i < SideCount; i++)
            {
                var canonical = VertexGraph.Canonicalize(new Vertex(hex, i));
                var (worldX, worldZ) = HexMath.VertexToWorldPosition(canonical, hexScale);
                float localX = worldX - centerX;
                float localZ = worldZ - centerZ;

                vertices[i] = new Vector3(localX, TopY, localZ);
                vertices[i + ringVerts] = new Vector3(localX, BottomY, localZ);
            }

            int tri = 0;
            for (int i = 0; i < SideCount; i++)
            {
                int next = (i + 1) % SideCount;
                triangles[tri++] = topCenter;
                triangles[tri++] = i;
                triangles[tri++] = next;

                triangles[tri++] = bottomCenter;
                triangles[tri++] = next + ringVerts;
                triangles[tri++] = i + ringVerts;
            }

            for (int i = 0; i < SideCount; i++)
            {
                int next = (i + 1) % SideCount;
                int topA = i;
                int topB = next;
                int bottomA = i + ringVerts;
                int bottomB = next + ringVerts;

                triangles[tri++] = topA;
                triangles[tri++] = topB;
                triangles[tri++] = bottomB;

                triangles[tri++] = topA;
                triangles[tri++] = bottomB;
                triangles[tri++] = bottomA;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
