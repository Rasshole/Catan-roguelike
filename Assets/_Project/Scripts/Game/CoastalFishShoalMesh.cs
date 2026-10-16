using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Fishermen fishing ground chevron (flat sea edge, V-notch into the coast).</summary>
    public static class CoastalFishShoalMesh
    {
        public const float TopLocalY = 1.06f;
        private const float SeawardVisualInsetFactor = 0.04f;
        private const float OutlineExpandFactor = 0.038f;

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
            float push = SeawardVisualInsetFactor * hexScale;
            Vector3 outward = new Vector3(placement.Outward.X, 0f, placement.Outward.Z);
            if (outward.sqrMagnitude > 1e-8f)
                outward.Normalize();

            Vector3 inland = PushSeaward(ToLocal(placement.InlandTip, ox, oz), outward, push * 0.12f);
            Vector3 armL = PushSeaward(ToLocal(placement.CoastArmLeft, ox, oz), outward, push * 1.15f);
            Vector3 armR = PushSeaward(ToLocal(placement.CoastArmRight, ox, oz), outward, push * 1.15f);
            Vector3 seaL = PushSeaward(ToLocal(placement.SeaFlatLeft, ox, oz), outward, push * 1.35f);
            Vector3 seaR = PushSeaward(ToLocal(placement.SeaFlatRight, ox, oz), outward, push * 1.35f);

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

        private static Vector3 PushSeaward(Vector3 v, Vector3 outward, float distance) =>
            outward.sqrMagnitude < 1e-8f ? v : v + outward * distance;

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
