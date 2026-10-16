using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Fishermen fishing ground chevron (flat sea edge, V-notch into the coast).</summary>
    public static class CoastalFishShoalMesh
    {
        public const float TopLocalY = 1.06f;

        public static Mesh CreateLocal(FishShoalPlacement placement, MapPlaneXz origin)
        {
            var mesh = new Mesh { name = "CoastalFishChevron" };
            float ox = origin.X;
            float oz = origin.Z;

            Vector3 inland = ToLocal(placement.InlandTip, ox, oz);
            Vector3 armL = ToLocal(placement.CoastArmLeft, ox, oz);
            Vector3 armR = ToLocal(placement.CoastArmRight, ox, oz);
            Vector3 seaL = ToLocal(placement.SeaFlatLeft, ox, oz);
            Vector3 seaR = ToLocal(placement.SeaFlatRight, ox, oz);

            var vertices = new[] { inland, armL, armR, seaL, seaR };
            if (SignedAreaXZ(inland, armL, armR, seaL, seaR) < 0f)
            {
                vertices = new[] { inland, armR, armL, seaR, seaL };
            }

            mesh.vertices = vertices;
            mesh.triangles = new[] { 0, 1, 2, 1, 3, 2, 2, 3, 4 };
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
