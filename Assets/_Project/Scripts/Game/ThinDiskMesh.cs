using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// High-segment cylindrical disk matching Unity's default cylinder mesh bounds (radius 0.5, height 2)
    /// so <see cref="TableCameraFraming"/> scale helpers stay unchanged.
    /// </summary>
    public static class ThinDiskMesh
    {
        public const float MeshRadius = 0.5f;
        public const float MeshHeight = 2f;
        public const float TopY = 1f;
        public const float BottomY = -1f;
        public const int SegmentCount = 64;

        private static Mesh _shared;

        public static Mesh Shared => _shared ??= Create();

        public static Mesh Create(string name = "ThinDisk")
        {
            var mesh = new Mesh { name = name };

            int ringVerts = SegmentCount;
            int vertCount = ringVerts * 2 + 2;
            var vertices = new Vector3[vertCount];
            var triangles = new int[ringVerts * 4 * 3];

            int topCenter = ringVerts * 2;
            int bottomCenter = topCenter + 1;
            vertices[topCenter] = new Vector3(0f, TopY, 0f);
            vertices[bottomCenter] = new Vector3(0f, BottomY, 0f);

            for (int i = 0; i < ringVerts; i++)
            {
                float angle = i * Mathf.PI * 2f / ringVerts;
                float x = Mathf.Cos(angle) * MeshRadius;
                float z = Mathf.Sin(angle) * MeshRadius;

                vertices[i] = new Vector3(x, TopY, z);
                vertices[i + ringVerts] = new Vector3(x, BottomY, z);
            }

            int tri = 0;
            for (int i = 0; i < ringVerts; i++)
            {
                int next = (i + 1) % ringVerts;

                triangles[tri++] = topCenter;
                triangles[tri++] = i;
                triangles[tri++] = next;

                triangles[tri++] = bottomCenter;
                triangles[tri++] = next + ringVerts;
                triangles[tri++] = i + ringVerts;

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
