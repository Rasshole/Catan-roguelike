using System.Collections.Generic;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Derives table-camera orbit distance from the hex cluster footprint (not the decorative table surface).
    /// </summary>
    public static class TableCameraFraming
    {
        public const float DistanceToRadiusRatio = 2.0f;
        public const float HeightToDistanceRatio = 7f / 8f;
        public const float DefaultMarginFactor = 1.08f;
        public const float MinOrbitDistance = 2.4f;

        /// <summary>Thin Y scale for the decorative table cube under the hex cluster.</summary>
        public const float TableSurfaceThinY = 0.06f;

        /// <summary>
        /// Pad beyond <see cref="ComputeBoardBoundingRadius"/> on each XZ half-axis (1.0 = flush with farthest hex edge).
        /// </summary>
        public const float TableSurfaceRadiusFactor = 1.15f;

        public static float ComputeBoardBoundingRadius(BoardState board, float hexScale) =>
            ComputeBoardBoundingRadius(board.Tiles.Keys, hexScale);

        public static float ComputeBoardBoundingRadius(IEnumerable<HexCoord> coords, float hexScale)
        {
            float hexOuter = hexScale * HexMath.OuterRadius;
            float maxRadius = 0f;

            foreach (var coord in coords)
            {
                var (x, z) = HexMath.ToWorldPosition(coord, hexScale);
                float centerDistance = Mathf.Sqrt(x * x + z * z);
                maxRadius = Mathf.Max(maxRadius, centerDistance + hexOuter);
            }

            return maxRadius;
        }

        public static float ComputeOrbitDistance(float boardRadius, float marginFactor = DefaultMarginFactor)
        {
            if (boardRadius <= 0f)
                return MinOrbitDistance;

            return Mathf.Max(MinOrbitDistance, boardRadius * DistanceToRadiusRatio * marginFactor);
        }

        public static float ComputeOrbitHeight(float orbitDistance) =>
            orbitDistance * HeightToDistanceRatio;

        public static Vector3 ComputeTableSurfaceScale(float boardBoundingRadius)
        {
            float halfExtent = boardBoundingRadius * TableSurfaceRadiusFactor;
            return new Vector3(2f * halfExtent, TableSurfaceThinY, 2f * halfExtent);
        }
    }
}
