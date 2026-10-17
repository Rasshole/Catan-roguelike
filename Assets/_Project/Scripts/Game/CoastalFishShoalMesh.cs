using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Fishermen fishing ground chevron (flat sea edge, V-notch into the coast).</summary>
    public static class CoastalFishShoalMesh
    {
        public const float TopLocalY = 1.06f;
        private const float InlandSeawardNudgeFactor = 0.018f;
        private const float OutlineExpandFactor = 0.032f;

        public static Mesh CreateLocal(FishShoalPlacement placement, MapPlaneXz origin, float hexScale)
        {
            var vertices = BuildVertices(placement, origin, hexScale, outlineExpand: 0f);
            return BuildChevronMesh(vertices);
        }

        public static Mesh CreateOutlineLocal(FishShoalPlacement placement, MapPlaneXz origin, float hexScale)
        {
            float expand = OutlineExpandFactor * hexScale;
            var vertices = BuildVertices(placement, origin, hexScale, outlineExpand: expand);
            return BuildChevronMesh(vertices);
        }

        private static Vector3[] BuildVertices(
            FishShoalPlacement placement,
            MapPlaneXz origin,
            float hexScale,
            float outlineExpand)
        {
            float ox = origin.X;
            float oz = origin.Z;
            Vector3 outward = new Vector3(placement.Outward.X, 0f, placement.Outward.Z);
            if (outward.sqrMagnitude > 1e-8f)
                outward.Normalize();

            float nudge = InlandSeawardNudgeFactor * hexScale;
            Vector3 inland = ToLocal(placement.InlandTip, ox, oz) + outward * nudge;
            Vector3 armL = ToLocal(placement.CoastArmLeft, ox, oz);
            Vector3 armR = ToLocal(placement.CoastArmRight, ox, oz);
            Vector3 seaL = ToLocal(placement.SeaFlatLeft, ox, oz);
            Vector3 seaR = ToLocal(placement.SeaFlatRight, ox, oz);

            var vertices = new[] { inland, armL, armR, seaL, seaR };
            if (SignedAreaXZ(inland, armL, armR, seaL, seaR) < 0f)
                vertices = new[] { inland, armR, armL, seaR, seaL };

            if (outlineExpand > 1e-5f)
                vertices = ExpandOutlineVertices(vertices, outlineExpand);

            return vertices;
        }

        private static Mesh BuildChevronMesh(Vector3[] vertices)
        {
            var mesh = new Mesh { name = "CoastalFishChevron" };
            mesh.vertices = vertices;
            mesh.triangles = new[]
            {
                0, 1, 2, 2, 1, 0,
                1, 3, 2, 2, 3, 1,
                2, 3, 4, 4, 3, 2
            };
            mesh.uv = new[]
            {
                new Vector2(0.5f, 0.08f),
                new Vector2(0.12f, 0.42f),
                new Vector2(0.88f, 0.42f),
                new Vector2(0.08f, 0.95f),
                new Vector2(0.92f, 0.95f)
            };
            mesh.normals = new[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up
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
                if (delta.sqrMagnitude < 1e-8f)
                {
                    expanded[i] = vertices[i];
                    continue;
                }

                expanded[i] = vertices[i] + delta.normalized * expand;
            }

            return expanded;
        }

        private static Vector3 ToLocal(MapPlaneXz p, float ox, float oz) =>
            new Vector3(p.X - ox, TopLocalY, p.Z - oz);

        private static float SignedAreaXZ(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e)
        {
            float area = 0f;
            area += a.x * b.z - b.x * a.z;
            area += b.x * c.z - c.x * b.z;
            area += c.x * d.z - d.x * c.z;
            area += d.x * e.z - e.x * d.z;
            area += e.x * a.z - a.x * e.z;
            return area * 0.5f;
        }
    }
}
