using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Flat annulus in XZ (Y = thickness), for colonist sand coast ring.</summary>
    internal static class ColonistAnnulusMesh
    {
        public const int SegmentCount = 64;
        public const float TopY = 1f;
        public const float BottomY = -1f;

        public static Mesh Create(float innerRadius, float outerRadius, string name = "ColonistAnnulus")
        {
            innerRadius = Mathf.Max(0.01f, innerRadius);
            outerRadius = Mathf.Max(innerRadius + 0.01f, outerRadius);

            var mesh = new Mesh { name = name };
            int segments = SegmentCount;
            int vertCount = segments * 4;
            var vertices = new Vector3[vertCount];
            var triangles = new int[segments * 24];

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                int innerTop = i;
                int outerTop = i + segments;
                int innerBottom = i + segments * 2;
                int outerBottom = i + segments * 3;

                vertices[innerTop] = new Vector3(cos * innerRadius, TopY, sin * innerRadius);
                vertices[outerTop] = new Vector3(cos * outerRadius, TopY, sin * outerRadius);
                vertices[innerBottom] = new Vector3(cos * innerRadius, BottomY, sin * innerRadius);
                vertices[outerBottom] = new Vector3(cos * outerRadius, BottomY, sin * outerRadius);
            }

            int tri = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int innerTopA = i;
                int innerTopB = next;
                int outerTopA = i + segments;
                int outerTopB = next + segments;
                int innerBottomA = i + segments * 2;
                int innerBottomB = next + segments * 2;
                int outerBottomA = i + segments * 3;
                int outerBottomB = next + segments * 3;

                triangles[tri++] = innerTopA;
                triangles[tri++] = outerTopA;
                triangles[tri++] = outerTopB;
                triangles[tri++] = innerTopA;
                triangles[tri++] = outerTopB;
                triangles[tri++] = innerTopB;

                triangles[tri++] = innerBottomA;
                triangles[tri++] = innerBottomB;
                triangles[tri++] = outerBottomB;
                triangles[tri++] = innerBottomA;
                triangles[tri++] = outerBottomB;
                triangles[tri++] = outerBottomA;

                triangles[tri++] = innerTopA;
                triangles[tri++] = innerTopB;
                triangles[tri++] = innerBottomB;
                triangles[tri++] = innerTopA;
                triangles[tri++] = innerBottomB;
                triangles[tri++] = innerBottomA;

                triangles[tri++] = outerTopA;
                triangles[tri++] = outerBottomA;
                triangles[tri++] = outerBottomB;
                triangles[tri++] = outerTopA;
                triangles[tri++] = outerBottomB;
                triangles[tri++] = outerTopB;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
