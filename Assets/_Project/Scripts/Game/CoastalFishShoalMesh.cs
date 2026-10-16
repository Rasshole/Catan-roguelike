using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Small coastal triangle (Fishermen fishing ground) sitting on a land–sea edge.</summary>
    public static class CoastalFishShoalMesh
    {
        public const float TopY = 0.018f;

        public static Mesh Create(FishShoalPlacement placement, float topWorldY, MapPlaneXz localOrigin)
        {
            var mesh = new Mesh { name = "CoastalFishShoal" };
            float y = topWorldY + TopY;
            float ox = localOrigin.X;
            float oz = localOrigin.Z;
            var v0 = new Vector3(placement.CoastVertexA.X - ox, y, placement.CoastVertexA.Z - oz);
            var v1 = new Vector3(placement.CoastVertexB.X - ox, y, placement.CoastVertexB.Z - oz);
            var v2 = new Vector3(placement.SeaTip.X - ox, y, placement.SeaTip.Z - oz);
            mesh.vertices = new[] { v0, v1, v2 };
            mesh.triangles = new[] { 0, 2, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
