using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Scales board props (robber, port badges) when the land mass is larger than the tutorial 7-hex reference.</summary>
    public static class ColonistBoardVisualScale
    {
        public const float ReferenceHexScale = 1.2f;
        /// <summary>Approximate land bounding radius for the 7-hex flower at <see cref="ReferenceHexScale"/>.</summary>
        public const float ReferenceLandBoundingRadius = 2.35f;

        public static float CurrentMassScale { get; private set; } = 1f;

        public static void UpdateFromBoard(BoardState board, float hexScale)
        {
            if (board == null)
            {
                CurrentMassScale = 1f;
                return;
            }

            float radius = ComputeLandBoundingRadius(board, hexScale);
            float refRadius = ReferenceLandBoundingRadius * (hexScale / ReferenceHexScale);
            CurrentMassScale = Mathf.Max(1f, radius / Mathf.Max(0.01f, refRadius));
        }

        public static float ComputeLandBoundingRadius(BoardState board, float hexScale)
        {
            var land = new System.Collections.Generic.HashSet<HexCoord>();
            foreach (var kvp in board.Tiles)
            {
                if (!kvp.Value.IsFishTile)
                    land.Add(kvp.Key);
            }

            if (land.Count == 0)
                return 0f;

            float max = 0f;
            foreach (var coord in land)
            {
                var (x, z) = HexMath.ToWorldPosition(coord, hexScale);
                float d = new Vector2(x, z).magnitude + hexScale * HexMath.OuterRadius;
                max = Mathf.Max(max, d);
            }

            return max;
        }

        public static float PortBadgeRadiusScale => CurrentMassScale;
        public static float RobberScale => CurrentMassScale;
    }
}
