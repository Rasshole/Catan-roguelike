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

        /// <summary>World thickness of the decorative table disk under the hex cluster.</summary>
        public const float TableSurfaceThinY = 0.06f;

        /// <summary>
        /// Pad beyond <see cref="ComputeBoardBoundingRadius"/> for the table disk (1.0 = flush with farthest hex corner).
        /// </summary>
        public const float TableSurfacePadFactor = 1.08f;

        /// <summary>Local Y for the wood table disk under the hex cluster.</summary>
        public const float TableSurfaceLocalY = -0.08f;

        /// <summary>Multiplier on the wood disk world radius for the sea ring (not camera framing).</summary>
        public const float WaterSurfacePadFactor = 1.55f;

        /// <summary>Maximum sea ring radius as a multiple of the wood disk world radius.</summary>
        public const float MaxWaterToWoodRadiusRatio = 1.75f;

        /// <summary>World thickness of the decorative sea disk under the wood table.</summary>
        public const float WaterSurfaceThinY = 0.07f;

        /// <summary>Local Y for the sea disk — slightly below the wood table.</summary>
        public const float WaterSurfaceLocalY = -0.09f;

        /// <summary>Multiplier on the sea disk world radius for the felt tablecloth (not camera framing).</summary>
        public const float FeltSurfacePadFactor = 1.45f;

        /// <summary>Maximum felt disk radius as a multiple of the sea disk world radius.</summary>
        public const float MaxFeltToWaterRadiusRatio = 1.6f;

        /// <summary>World thickness of the decorative felt disk under the sea ring.</summary>
        public const float FeltSurfaceThinY = 0.05f;

        /// <summary>Local Y for the felt disk — below the sea ring.</summary>
        public const float FeltSurfaceLocalY = -0.11f;

        private const float CylinderMeshRadius = 0.5f;
        private const float CylinderMeshHeight = 2f;
        private const float MinWaterWoodRadiusGap = 0.001f;
        private const float MinFeltWaterRadiusGap = 0.001f;

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

        public static float ComputeTableDiskWorldRadius(float boardBoundingRadius) =>
            boardBoundingRadius * TableSurfacePadFactor;

        /// <summary>
        /// Local scale for a Unity cylinder primitive (mesh radius 0.5, height 2) sized to the hex cluster.
        /// </summary>
        public static Vector3 ComputeTableDiskScale(float boardBoundingRadius)
        {
            float worldRadius = ComputeTableDiskWorldRadius(boardBoundingRadius);
            return ComputeCylinderDiskScale(worldRadius, TableSurfaceThinY);
        }

        public static float ComputeWaterDiskWorldRadius(float boardBoundingRadius)
        {
            float woodRadius = ComputeTableDiskWorldRadius(boardBoundingRadius);
            float waterRadius = woodRadius * WaterSurfacePadFactor;
            waterRadius = Mathf.Max(waterRadius, woodRadius + MinWaterWoodRadiusGap);
            waterRadius = Mathf.Min(waterRadius, woodRadius * MaxWaterToWoodRadiusRatio);
            return waterRadius;
        }

        /// <summary>
        /// Local scale for a Unity cylinder primitive sized to the sea ring around the wood table disk.
        /// </summary>
        public static Vector3 ComputeWaterDiskScale(float boardBoundingRadius) =>
            ComputeCylinderDiskScale(
                ComputeWaterDiskWorldRadius(boardBoundingRadius),
                WaterSurfaceThinY);

        public static float ComputeFeltDiskWorldRadius(float boardBoundingRadius)
        {
            float waterRadius = ComputeWaterDiskWorldRadius(boardBoundingRadius);
            float feltRadius = waterRadius * FeltSurfacePadFactor;
            feltRadius = Mathf.Max(feltRadius, waterRadius + MinFeltWaterRadiusGap);
            feltRadius = Mathf.Min(feltRadius, waterRadius * MaxFeltToWaterRadiusRatio);
            return feltRadius;
        }

        /// <summary>
        /// Local scale for a Unity cylinder primitive sized to the felt tablecloth around the sea ring.
        /// </summary>
        public static Vector3 ComputeFeltDiskScale(float boardBoundingRadius) =>
            ComputeCylinderDiskScale(
                ComputeFeltDiskWorldRadius(boardBoundingRadius),
                FeltSurfaceThinY);

        private static Vector3 ComputeCylinderDiskScale(float worldRadius, float thinY)
        {
            float xz = worldRadius / CylinderMeshRadius;
            float y = thinY / CylinderMeshHeight;
            return new Vector3(xz, y, xz);
        }
    }
}
