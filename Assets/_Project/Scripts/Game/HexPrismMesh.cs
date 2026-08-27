using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Pointy-top hexagonal prism matching <see cref="Core.Hex.HexMath"/> vertex orientation
    /// (corner 0 at +Z). Top at local y=+1, bottom at y=-1, circumradius 0.5 on XZ — same
    /// vertical span and horizontal scale convention as Unity's unit cylinder.
    /// </summary>
    public static class HexPrismMesh
    {
        public const float DefaultRadius = 0.5f;
        public const float TopY = 1f;
        public const float BottomY = -1f;
        public const int SideCount = 6;

        /// <summary>XZ scale multiplier so mesh circumradius matches HexMath OuterRadius at BoardView hexScale.</summary>
        public const float BoardScaleMultiplier = 2f;

        public static Mesh Create(float radius = DefaultRadius)
        {
            var mesh = new Mesh { name = "HexPrism" };

            int ringVerts = SideCount;
            int vertCount = ringVerts * 2 + 2;
            var vertices = new Vector3[vertCount];
            var triangles = new int[SideCount * 2 * 3 + SideCount * 2 * 3];

            int topCenter = ringVerts * 2;
            int bottomCenter = topCenter + 1;
            vertices[topCenter] = new Vector3(0f, TopY, 0f);
            vertices[bottomCenter] = new Vector3(0f, BottomY, 0f);

            for (int i = 0; i < SideCount; i++)
            {
                float angleRad = i * 60f * Mathf.Deg2Rad;
                float x = radius * Mathf.Sin(angleRad);
                float z = radius * Mathf.Cos(angleRad);

                vertices[i] = new Vector3(x, TopY, z);
                vertices[i + ringVerts] = new Vector3(x, BottomY, z);
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
