using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Fishermen chevron band mesh: P1, C, P2, P2', C', P1'.</summary>
    public static class CoastalFishShoalMesh
    {
        public const float TopLocalY = 1.06f;
        private const float OutlineExpandFactor = 0.028f;

        public static Mesh CreateLocal(FishShoalPlacement placement, MapPlaneXz origin, float hexScale)
        {
            var vertices = BuildVertices(placement, origin, outlineExpand: 0f);
            return BuildBandMesh(vertices);
        }

        public static Mesh CreateOutlineLocal(FishShoalPlacement placement, MapPlaneXz origin, float hexScale)
        {
            float expand = OutlineExpandFactor * hexScale;
            var vertices = BuildVertices(placement, origin, expand);
            return BuildBandMesh(vertices);
        }

        private static Vector3[] BuildVertices(FishShoalPlacement placement, MapPlaneXz origin, float outlineExpand)
        {
            float ox = origin.X;
            float oz = origin.Z;
            var vertices = new[]
            {
                ToLocal(placement.CoastArmLeft, ox, oz),
                ToLocal(placement.InlandTip, ox, oz),
                ToLocal(placement.CoastArmRight, ox, oz),
                ToLocal(placement.SeaFlatRight, ox, oz),
                ToLocal(placement.SeaBandApex, ox, oz),
                ToLocal(placement.SeaFlatLeft, ox, oz)
            };

            if (outlineExpand > 1e-5f)
                vertices = ExpandOutlineVertices(vertices, outlineExpand);

            return vertices;
        }

        private static Mesh BuildBandMesh(Vector3[] vertices)
        {
            var mesh = new Mesh { name = "CoastalFishChevron" };
            mesh.vertices = vertices;
            // Convex hex fan from inland tip C (index 1).
            // Both windings: Unlit/Color has no _Cull property, so a single winding is culled
            // whenever the placement's left/right order faces away from the top-down camera.
            mesh.triangles = new[]
            {
                1, 0, 5, 1, 5, 0,
                1, 5, 4, 1, 4, 5,
                1, 4, 3, 1, 3, 4,
                1, 3, 2, 1, 2, 3
            };
            mesh.uv = new[]
            {
                new Vector2(0.1f, 0.35f),
                new Vector2(0.5f, 0.05f),
                new Vector2(0.9f, 0.35f),
                new Vector2(0.95f, 0.9f),
                new Vector2(0.5f, 1f),
                new Vector2(0.05f, 0.9f)
            };
            mesh.normals = new[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3[] ExpandOutlineVertices(Vector3[] vertices, float expand)
        {
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < vertices.Length; i++)
                centroid += vertices[i];
            centroid /= vertices.Length;

            var expanded = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 delta = vertices[i] - centroid;
                delta.y = 0f;
                expanded[i] = delta.sqrMagnitude < 1e-8f
                    ? vertices[i]
                    : vertices[i] + delta.normalized * expand;
            }

            return expanded;
        }

        private static Vector3 ToLocal(MapPlaneXz p, float ox, float oz) =>
            new Vector3(p.X - ox, TopLocalY, p.Z - oz);

        public static float ComputePolygonAreaXZ(Vector3[] verts)
        {
            if (verts == null || verts.Length < 3)
                return 0f;
            float area = 0f;
            for (int i = 0; i < verts.Length; i++)
            {
                int j = (i + 1) % verts.Length;
                area += verts[i].x * verts[j].z - verts[j].x * verts[i].z;
            }

            return Mathf.Abs(area) * 0.5f;
        }
    }
}
