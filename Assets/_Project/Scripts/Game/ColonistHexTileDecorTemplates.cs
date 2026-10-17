using System;
using System.Collections.Generic;
using CatanRoguelike.Core;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Hand-authored decor slot layouts on two hex rings (edge-mid / corner).</summary>
    public static class ColonistHexTileDecorTemplates
    {
        public const float RingARadiusHex = 0.58f;
        public const float RingBRadiusHex = 0.76f;
        public const float FootprintScale = 0.42f;
        public const float FootprintGapHex = 0.05f;
        public const float MaxJitterHex = 0.02f;
        private const float HexHalfWidth = 0.8660254f;

        public enum SlotKind
        {
            TopDownSprite,
            NatureModel,
            SheepFlock
        }

        public readonly struct Slot
        {
            public readonly float AngleDeg;
            public readonly float RadiusHex;
            public readonly SlotKind Kind;
            public readonly string Asset;
            public readonly float DiameterHex;
            public readonly float YawDeg;
            public readonly float ModelScale;

            public Slot(
                float angleDeg,
                float radiusHex,
                SlotKind kind,
                string asset,
                float diameterHex = 0.26f,
                float yawDeg = 0f,
                float modelScale = 0.20f)
            {
                AngleDeg = angleDeg;
                RadiusHex = radiusHex;
                Kind = kind;
                Asset = asset;
                DiameterHex = diameterHex;
                YawDeg = yawDeg;
                ModelScale = modelScale;
            }
        }

        public static IReadOnlyList<Slot> Wheat => WheatSlots;
        public static IReadOnlyList<Slot> Brick => BrickSlots;
        public static IReadOnlyList<Slot> Stone => StoneSlots;
        public static IReadOnlyList<Slot> Sheep => SheepSlots;
        public static IReadOnlyList<Slot> Desert => DesertSlots;
        public static IReadOnlyList<Slot> DesertFishPond => DesertFishSlots;

        private static readonly Slot[] WheatSlots =
        {
            // v111 review: 0.27/0.22 fields left big empty yellow gaps and the 0.34 mill read as bare
            // sails. Fields grow to the keep-out/ring-gap limit; the two ring-A slots beside the mill
            // (A4 at 270°, A5 at 330°) stay smaller so the 0.38 mill keeps its own corner.
            RingASlot(0, SlotKind.TopDownSprite, "wheat_field_a", 0.32f),
            RingASlot(1, SlotKind.TopDownSprite, "wheat_field_b", 0.32f),
            RingASlot(2, SlotKind.TopDownSprite, "wheat_field_c", 0.32f),
            RingASlot(3, SlotKind.TopDownSprite, "wheat_rows", 0.32f),
            RingASlot(4, SlotKind.TopDownSprite, "wheat_field_a", 0.22f),
            RingASlot(5, SlotKind.TopDownSprite, "wheat_sheaf", 0.24f),
            RingBSlot(0, SlotKind.TopDownSprite, "wheat_rows", 0.30f),
            RingBSlot(1, SlotKind.TopDownSprite, "wheat_field_b", 0.30f),
            RingBSlot(2, SlotKind.TopDownSprite, "wheat_sheaf", 0.28f),
            RingBSlot(3, SlotKind.TopDownSprite, "wheat_field_c", 0.30f),
            RingBSlot(4, SlotKind.TopDownSprite, "wheat_field_a", 0.28f),
            RingBSlot(5, SlotKind.TopDownSprite, "windmill", 0.38f, -8f)
        };

        private static readonly Slot[] BrickSlots =
        {
            RingASlot(0, SlotKind.TopDownSprite, "clay_pit", 0.27f),
            RingASlot(1, SlotKind.TopDownSprite, "brick_stack", 0.26f, -6f),
            RingASlot(2, SlotKind.TopDownSprite, "clay_pit", 0.27f),
            RingASlot(3, SlotKind.TopDownSprite, "brick_stack", 0.26f, 6f),
            RingASlot(4, SlotKind.TopDownSprite, "clay_pit", 0.27f),
            RingASlot(5, SlotKind.TopDownSprite, "brick_stack", 0.26f, -4f),
            RingBSlot(0, SlotKind.TopDownSprite, "brick_stack", 0.22f, 8f),
            RingBSlot(1, SlotKind.TopDownSprite, "clay_pit", 0.24f),
            RingBSlot(2, SlotKind.TopDownSprite, "brick_stack", 0.22f, -8f),
            RingBSlot(3, SlotKind.TopDownSprite, "clay_pit", 0.24f),
            RingBSlot(4, SlotKind.TopDownSprite, "brick_stack", 0.22f, 4f),
            RingBSlot(5, SlotKind.TopDownSprite, "clay_pit", 0.24f)
        };

        private static readonly Slot[] StoneSlots =
        {
            RingASlot(0, SlotKind.TopDownSprite, "rock_cluster_a", 0.27f),
            RingASlot(1, SlotKind.TopDownSprite, "rock_cluster_b", 0.26f),
            RingASlot(2, SlotKind.TopDownSprite, "rock_cluster_c", 0.27f),
            RingASlot(3, SlotKind.TopDownSprite, "rock_cluster_a", 0.26f),
            RingASlot(4, SlotKind.TopDownSprite, "rock_cluster_b", 0.27f),
            RingASlot(5, SlotKind.TopDownSprite, "rock_cluster_c", 0.26f),
            RingBSlot(0, SlotKind.TopDownSprite, "rock_cluster_b", 0.22f),
            RingBSlot(1, SlotKind.TopDownSprite, "rock_cluster_c", 0.22f),
            RingBSlot(2, SlotKind.TopDownSprite, "rock_cluster_a", 0.22f),
            RingBSlot(3, SlotKind.TopDownSprite, "rock_cluster_c", 0.22f),
            RingBSlot(4, SlotKind.TopDownSprite, "rock_cluster_a", 0.22f),
            RingBSlot(5, SlotKind.TopDownSprite, "rock_cluster_b", 0.22f)
        };

        private static readonly Slot[] SheepSlots =
        {
            RingASlot(0, SlotKind.SheepFlock, "sheep", modelScale: 0.22f),
            RingASlot(1, SlotKind.SheepFlock, "sheep", modelScale: 0.21f),
            RingASlot(2, SlotKind.SheepFlock, "sheep", modelScale: 0.22f),
            RingASlot(3, SlotKind.SheepFlock, "sheep", modelScale: 0.21f),
            RingASlot(4, SlotKind.SheepFlock, "sheep", modelScale: 0.22f),
            RingASlot(5, SlotKind.SheepFlock, "sheep", modelScale: 0.21f),
            // Corner bushes were ~0.03 hex dots at 0.18-0.21 (v111 review); 0.30 still clears ring A.
            RingBSlot(0, SlotKind.NatureModel, "Bush_Flowers", modelScale: 0.32f),
            RingBSlot(1, SlotKind.NatureModel, "Bush", modelScale: 0.30f),
            RingBSlot(2, SlotKind.NatureModel, "Bush_Small_Flowers", modelScale: 0.30f),
            RingBSlot(3, SlotKind.NatureModel, "Bush_Large", modelScale: 0.30f),
            RingBSlot(4, SlotKind.TopDownSprite, "fence", 0.34f, -10f),
            RingBSlot(5, SlotKind.TopDownSprite, "fence", 0.32f, 10f)
        };

        private static readonly Slot[] DesertSlots =
        {
            RingASlot(0, SlotKind.TopDownSprite, "cactus_tall", 0.26f, -8f),
            RingASlot(1, SlotKind.TopDownSprite, "cactus_short", 0.24f, 6f),
            RingASlot(2, SlotKind.TopDownSprite, "desert_rock", 0.24f),
            RingASlot(3, SlotKind.TopDownSprite, "cactus_tall", 0.26f, 8f),
            RingASlot(4, SlotKind.TopDownSprite, "cactus_short", 0.24f, -6f),
            RingASlot(5, SlotKind.TopDownSprite, "desert_rock", 0.24f),
            RingBSlot(0, SlotKind.NatureModel, "Bush", modelScale: 0.26f),
            RingBSlot(1, SlotKind.TopDownSprite, "desert_rock", 0.22f),
            RingBSlot(2, SlotKind.NatureModel, "Bush_Small_Flowers", modelScale: 0.26f),
            RingBSlot(3, SlotKind.TopDownSprite, "cactus_short", 0.22f, 4f),
            RingBSlot(4, SlotKind.TopDownSprite, "cactus_tall", 0.24f, -4f),
            RingBSlot(5, SlotKind.NatureModel, "Bush", modelScale: 0.26f)
        };

        private static readonly Slot[] DesertFishSlots =
        {
            RingBSlot(0, SlotKind.TopDownSprite, "cactus_tall", 0.24f, -8f),
            RingBSlot(1, SlotKind.TopDownSprite, "cactus_short", 0.22f, 6f),
            RingBSlot(2, SlotKind.TopDownSprite, "desert_rock", 0.22f),
            RingBSlot(3, SlotKind.TopDownSprite, "cactus_tall", 0.24f, 8f),
            RingBSlot(4, SlotKind.TopDownSprite, "cactus_short", 0.22f, -6f),
            RingBSlot(5, SlotKind.TopDownSprite, "desert_rock", 0.22f),
            RingASlot(0, SlotKind.NatureModel, "Bush", modelScale: 0.15f),
            RingASlot(2, SlotKind.NatureModel, "Bush_Small_Flowers", modelScale: 0.14f),
            RingASlot(4, SlotKind.TopDownSprite, "desert_rock", 0.22f),
            RingASlot(5, SlotKind.TopDownSprite, "cactus_short", 0.22f, 4f)
        };

        private static Slot RingASlot(int index, SlotKind kind, string asset, float diameterHex = 0.26f, float yawDeg = 0f,
            float modelScale = 0.20f) =>
            new(30f + 60f * index, RingARadiusHex, kind, asset, diameterHex, yawDeg, modelScale);

        private static Slot RingBSlot(int index, SlotKind kind, string asset, float diameterHex = 0.22f, float yawDeg = 0f,
            float modelScale = 0.20f) =>
            new(60f * index, RingBRadiusHex, kind, asset, diameterHex, yawDeg, modelScale);

        public static float VisibleFootprintRadiusHex(Slot slot) =>
            slot.Kind == SlotKind.TopDownSprite
                ? slot.DiameterHex * FootprintScale
                : slot.ModelScale * FootprintScale;

        public static Vector2 LocalPositionHex(Slot slot, float templateRotationDeg, int jitterSeed)
        {
            float angle = (slot.AngleDeg + templateRotationDeg) * Mathf.Deg2Rad;
            var basePos = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * slot.RadiusHex;
            float jx = (Hash01(jitterSeed) - 0.5f) * 2f * MaxJitterHex;
            float jz = (Hash01(jitterSeed + 31) - 0.5f) * 2f * MaxJitterHex;
            return basePos + new Vector2(jx, jz);
        }

        public static bool CanPlaceSlot(Vector2 centerHex, float footprintRadiusHex, float tokenKeepOutHex)
        {
            if (centerHex.magnitude < tokenKeepOutHex + footprintRadiusHex - 0.001f)
                return false;
            return DistanceToHexBoundary(centerHex) >= footprintRadiusHex - 0.001f;
        }

        public static void ValidateTemplate(
            IReadOnlyList<Slot> template,
            float tokenKeepOutHex,
            string label)
        {
            float jitterPad = MaxJitterHex * 1.5f;
            for (int i = 0; i < template.Count; i++)
            {
                var pos = LocalPositionHex(template[i], 0f, i);
                float foot = VisibleFootprintRadiusHex(template[i]) + jitterPad;
                if (pos.magnitude < tokenKeepOutHex + foot - 0.001f)
                    throw new InvalidOperationException(
                        $"{label} slot {i} overlaps token keep-out (inner={pos.magnitude - foot:F3}).");
                if (DistanceToHexBoundary(pos) < foot - 0.001f)
                    throw new InvalidOperationException(
                        $"{label} slot {i} exceeds hex outline (margin={DistanceToHexBoundary(pos):F3} need>={foot:F3}).");
            }

            for (int i = 0; i < template.Count; i++)
            {
                var pi = LocalPositionHex(template[i], 0f, i);
                float ri = VisibleFootprintRadiusHex(template[i]) + jitterPad;
                for (int j = i + 1; j < template.Count; j++)
                {
                    var pj = LocalPositionHex(template[j], 0f, j);
                    float rj = VisibleFootprintRadiusHex(template[j]) + jitterPad;
                    float need = ri + rj + FootprintGapHex;
                    if ((pi - pj).magnitude < need - 0.001f)
                        throw new InvalidOperationException(
                            $"{label} slots {i} and {j} overlap (dist={(pi - pj).magnitude:F3} need>={need:F3}).");
                }
            }
        }

        /// <summary>Distance from a point inside the unit flat-top hex to the nearest edge.</summary>
        public static float DistanceToHexBoundary(Vector2 p)
        {
            if (!PointInsideUnitHex(p))
                return -1f;

            float min = float.MaxValue;
            for (int i = 0; i < 6; i++)
            {
                Vector2 a = HexVertex(i);
                Vector2 b = HexVertex((i + 1) % 6);
                min = Mathf.Min(min, DistancePointToSegment(p, a, b));
            }

            return min;
        }

        private static Vector2 HexVertex(int i)
        {
            float angle = (90f - 60f * i) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static bool PointInsideUnitHex(Vector2 p)
        {
            if (Mathf.Abs(p.y) > 1f + 1e-4f)
                return false;
            if (Mathf.Abs(p.x) > HexHalfWidth + 1e-4f)
                return false;
            return HexHalfWidth * 2f - Mathf.Abs(p.x) - Mathf.Abs(p.y) * HexHalfWidth >= -1e-4f;
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-8f)
                return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return (p - (a + ab * t)).magnitude;
        }

        internal static int ExpectedTopLevelPropCount(IReadOnlyList<Slot> template) => template.Count;

        private static float Hash01(int seed)
        {
            uint x = (uint)seed;
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return (x & 0xffff) / 65535f;
        }
    }
}
