using System;
using System.Collections.Generic;
using CatanRoguelike.Core;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Hand-authored decor slot layouts (one prop per slot, no overlap).</summary>
    public static class ColonistHexTileDecorTemplates
    {
        public const float MaxCenterRadiusFactor = 0.70f;
        public const float FootprintGapHex = 0.05f;
        public const float MaxJitterHex = 0.02f;

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
                float diameterHex = 0.30f,
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

            public float FootprintRadiusHex => Kind == SlotKind.NatureModel || Kind == SlotKind.SheepFlock
                ? ModelScale * 0.80f
                : DiameterHex * 0.5f;
        }

        public static IReadOnlyList<Slot> Wheat => WheatSlots;
        public static IReadOnlyList<Slot> Brick => BrickSlots;
        public static IReadOnlyList<Slot> Stone => StoneSlots;
        public static IReadOnlyList<Slot> Sheep => SheepSlots;
        public static IReadOnlyList<Slot> Desert => DesertSlots;
        public static IReadOnlyList<Slot> DesertFishPond => DesertFishSlots;

        private static readonly Slot[] WheatSlots =
        {
            new(0f, 0.54f, SlotKind.TopDownSprite, "wheat_field_a", 0.30f),
            new(90f, 0.54f, SlotKind.TopDownSprite, "wheat_field_b", 0.30f),
            new(180f, 0.54f, SlotKind.TopDownSprite, "wheat_field_c", 0.30f),
            new(270f, 0.54f, SlotKind.TopDownSprite, "wheat_field_a", 0.30f),
            new(50f, 0.44f, SlotKind.TopDownSprite, "wheat_sheaf", 0.28f),
            new(230f, 0.44f, SlotKind.TopDownSprite, "wheat_sheaf", 0.28f),
            new(305f, 0.48f, SlotKind.TopDownSprite, "windmill", 0.42f, -8f),
            new(155f, 0.50f, SlotKind.TopDownSprite, "wheat_rows", 0.28f),
            new(335f, 0.50f, SlotKind.TopDownSprite, "wheat_rows", 0.28f)
        };

        private static readonly Slot[] BrickSlots =
        {
            new(0f, 0.52f, SlotKind.TopDownSprite, "clay_pit", 0.36f),
            new(60f, 0.52f, SlotKind.TopDownSprite, "brick_stack", 0.28f, -6f),
            new(120f, 0.52f, SlotKind.TopDownSprite, "clay_pit", 0.34f),
            new(180f, 0.52f, SlotKind.TopDownSprite, "brick_stack", 0.28f, 6f),
            new(240f, 0.52f, SlotKind.TopDownSprite, "clay_pit", 0.36f),
            new(300f, 0.52f, SlotKind.TopDownSprite, "brick_stack", 0.28f, -4f),
            new(30f, 0.42f, SlotKind.TopDownSprite, "brick_stack", 0.26f, 8f),
            new(90f, 0.42f, SlotKind.TopDownSprite, "clay_pit", 0.32f),
            new(210f, 0.42f, SlotKind.TopDownSprite, "clay_pit", 0.32f),
            new(270f, 0.42f, SlotKind.TopDownSprite, "brick_stack", 0.26f, -8f)
        };

        private static readonly Slot[] StoneSlots =
        {
            new(0f, 0.52f, SlotKind.TopDownSprite, "rock_cluster_a", 0.30f),
            new(60f, 0.52f, SlotKind.TopDownSprite, "rock_cluster_b", 0.28f),
            new(120f, 0.52f, SlotKind.TopDownSprite, "rock_cluster_c", 0.30f),
            new(180f, 0.52f, SlotKind.TopDownSprite, "rock_cluster_a", 0.28f),
            new(240f, 0.52f, SlotKind.TopDownSprite, "rock_cluster_b", 0.30f),
            new(300f, 0.52f, SlotKind.TopDownSprite, "rock_cluster_c", 0.28f),
            new(30f, 0.42f, SlotKind.TopDownSprite, "rock_cluster_b", 0.26f),
            new(90f, 0.42f, SlotKind.TopDownSprite, "rock_cluster_c", 0.28f),
            new(210f, 0.42f, SlotKind.TopDownSprite, "rock_cluster_a", 0.28f),
            new(270f, 0.42f, SlotKind.TopDownSprite, "rock_cluster_c", 0.26f)
        };

        private static readonly Slot[] SheepSlots =
        {
            new(0f, 0.52f, SlotKind.NatureModel, "Bush_Flowers", modelScale: 0.20f),
            new(60f, 0.52f, SlotKind.NatureModel, "Bush", modelScale: 0.18f),
            new(120f, 0.52f, SlotKind.NatureModel, "Bush_Small_Flowers", modelScale: 0.17f),
            new(180f, 0.52f, SlotKind.NatureModel, "Bush_Large", modelScale: 0.21f),
            new(240f, 0.52f, SlotKind.NatureModel, "Bush", modelScale: 0.18f),
            new(300f, 0.52f, SlotKind.NatureModel, "Bush_Flowers", modelScale: 0.20f),
            new(40f, 0.44f, SlotKind.TopDownSprite, "fence", 0.38f, -10f),
            new(220f, 0.44f, SlotKind.TopDownSprite, "fence", 0.36f, 10f),
            new(100f, 0.40f, SlotKind.SheepFlock, "sheep", modelScale: 0.20f),
            new(160f, 0.40f, SlotKind.SheepFlock, "sheep", modelScale: 0.19f),
            new(280f, 0.40f, SlotKind.SheepFlock, "sheep", modelScale: 0.20f),
            new(340f, 0.40f, SlotKind.SheepFlock, "sheep", modelScale: 0.19f)
        };

        private static readonly Slot[] DesertSlots =
        {
            new(0f, 0.54f, SlotKind.TopDownSprite, "cactus_tall", 0.28f, -8f),
            new(60f, 0.54f, SlotKind.TopDownSprite, "cactus_short", 0.26f, 6f),
            new(120f, 0.54f, SlotKind.TopDownSprite, "desert_rock", 0.24f),
            new(180f, 0.54f, SlotKind.TopDownSprite, "cactus_tall", 0.28f, 8f),
            new(240f, 0.54f, SlotKind.TopDownSprite, "cactus_short", 0.26f, -6f),
            new(300f, 0.54f, SlotKind.TopDownSprite, "desert_rock", 0.24f),
            new(30f, 0.44f, SlotKind.NatureModel, "Bush", modelScale: 0.15f),
            new(90f, 0.44f, SlotKind.TopDownSprite, "desert_rock", 0.22f),
            new(210f, 0.44f, SlotKind.NatureModel, "Bush_Small_Flowers", modelScale: 0.14f),
            new(270f, 0.44f, SlotKind.TopDownSprite, "cactus_short", 0.24f, 4f),
            new(150f, 0.44f, SlotKind.TopDownSprite, "cactus_tall", 0.26f, -4f),
            new(330f, 0.44f, SlotKind.NatureModel, "Bush", modelScale: 0.15f)
        };

        private static readonly Slot[] DesertFishSlots =
        {
            new(0f, 0.54f, SlotKind.TopDownSprite, "cactus_tall", 0.28f, -8f),
            new(72f, 0.54f, SlotKind.TopDownSprite, "cactus_short", 0.26f, 6f),
            new(144f, 0.54f, SlotKind.TopDownSprite, "desert_rock", 0.24f),
            new(216f, 0.54f, SlotKind.TopDownSprite, "cactus_tall", 0.28f, 8f),
            new(288f, 0.54f, SlotKind.TopDownSprite, "cactus_short", 0.26f, -6f),
            new(36f, 0.46f, SlotKind.TopDownSprite, "desert_rock", 0.24f),
            new(108f, 0.46f, SlotKind.NatureModel, "Bush", modelScale: 0.15f),
            new(180f, 0.46f, SlotKind.TopDownSprite, "desert_rock", 0.22f),
            new(252f, 0.46f, SlotKind.NatureModel, "Bush_Small_Flowers", modelScale: 0.14f),
            new(324f, 0.46f, SlotKind.TopDownSprite, "cactus_short", 0.24f, 4f)
        };

        public static Vector2 LocalPositionHex(Slot slot, float templateRotationDeg, int jitterSeed)
        {
            float angle = (slot.AngleDeg + templateRotationDeg) * Mathf.Deg2Rad;
            var basePos = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * slot.RadiusHex;
            float jx = (Hash01(jitterSeed) - 0.5f) * 2f * MaxJitterHex;
            float jz = (Hash01(jitterSeed + 31) - 0.5f) * 2f * MaxJitterHex;
            return basePos + new Vector2(jx, jz);
        }

        public static void ValidateTemplate(
            IReadOnlyList<Slot> template,
            float minRadiusHex,
            string label)
        {
            for (int i = 0; i < template.Count; i++)
            {
                var pos = LocalPositionHex(template[i], 0f, i);
                float inner = pos.magnitude - template[i].FootprintRadiusHex;
                float outer = pos.magnitude + template[i].FootprintRadiusHex;
                if (inner < minRadiusHex - 0.001f)
                    throw new InvalidOperationException(
                        $"{label} slot {i} overlaps token keep-out (inner={inner:F3}).");
                if (outer > MaxCenterRadiusFactor + 0.001f)
                    throw new InvalidOperationException(
                        $"{label} slot {i} exceeds decor cap (outer={outer:F3}).");
            }

            for (int i = 0; i < template.Count; i++)
            {
                var pi = LocalPositionHex(template[i], 0f, i);
                float ri = template[i].FootprintRadiusHex;
                for (int j = i + 1; j < template.Count; j++)
                {
                    var pj = LocalPositionHex(template[j], 0f, j);
                    float rj = template[j].FootprintRadiusHex;
                    float need = ri + rj + FootprintGapHex;
                    if ((pi - pj).magnitude < need - 0.001f)
                        throw new InvalidOperationException(
                            $"{label} slots {i} and {j} overlap (dist={(pi - pj).magnitude:F3} need>={need:F3}).");
                }
            }
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
