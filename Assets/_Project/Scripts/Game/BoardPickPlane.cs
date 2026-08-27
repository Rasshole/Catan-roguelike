using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Horizontal pick plane for board hover/click raycasts.</summary>
    public static class BoardPickPlane
    {
        public static float GetPickPlaneY(float tileHeight) =>
            tileHeight > 0f ? tileHeight : 0f;

        public static Plane CreateHorizontalPlane(float planeY) =>
            new Plane(Vector3.up, new Vector3(0f, planeY, 0f));

        public static bool TryRaycast(Ray ray, float planeY, out Vector3 hitPoint)
        {
            var plane = CreateHorizontalPlane(planeY);
            if (!plane.Raycast(ray, out float enter))
            {
                hitPoint = default;
                return false;
            }

            hitPoint = ray.GetPoint(enter);
            return true;
        }
    }
}
