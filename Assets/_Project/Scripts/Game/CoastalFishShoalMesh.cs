using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Small coastal triangle (Fishermen fishing ground) sitting in the water at a land–sea edge.</summary>
    public static class CoastalFishShoalMesh
    {
        public const float TopLocalY = 1.06f;

        public static Mesh CreateLocal(FishShoalPlacement placement, MapPlaneXz origin)
        {
            var mesh = new Mesh { name = "CoastalFishShoal" };
            float ox = origin.X;
            float oz = origin.Z;
            var v0 = new Vector3(placement.CoastVertexA.X - ox, TopLocalY, placement.CoastVertexA.Z - oz);
            var v1 = new Vector3(placement.CoastVertexB.X - ox, TopLocalY, placement.CoastVertexB.Z - oz);
            var v2 = new Vector3(placement.SeaTip.X - ox, TopLocalY, placement.SeaTip.Z - oz);
            mesh.vertices = new[] { v0, v1, v2 };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
