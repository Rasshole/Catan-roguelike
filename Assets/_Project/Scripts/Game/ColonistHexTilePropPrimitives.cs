using CatanRoguelike.Core;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Readable primitive props scaled in world units (~0.25–0.35 hex).</summary>
    internal static class ColonistHexTilePropPrimitives
    {
        public static GameObject CreateSheep(Transform parent, Vector3 localPos, float scale, int seed)
        {
            // Pivot so body + head turn together; a dark face and ears make it read as a sheep from
            // above (the all-white head hid inside the body and the sheep looked like cotton blobs).
            var pivot = new GameObject("Sheep");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = localPos;
            pivot.transform.localRotation = Quaternion.Euler(0f, seed * 37f, 0f);
            var body = CreatePart(pivot.transform, "SheepBody", Vector3.zero, PrimitiveType.Sphere,
                new Vector3(scale * 1.1f, scale * 0.85f, scale * 1.35f),
                new Color(0.95f, 0.95f, 0.93f));
            var face = new Color(0.20f, 0.18f, 0.18f);
            CreatePart(pivot.transform, "SheepHead", new Vector3(0f, scale * 0.25f, scale * 0.72f),
                PrimitiveType.Sphere, new Vector3(scale * 0.46f, scale * 0.46f, scale * 0.56f), face);
            for (int side = -1; side <= 1; side += 2)
                CreatePart(pivot.transform, "SheepEar", new Vector3(side * scale * 0.28f, scale * 0.32f, scale * 0.66f),
                    PrimitiveType.Sphere, new Vector3(scale * 0.26f, scale * 0.10f, scale * 0.14f), face);
            return body;
        }

        public static void CreateWheatRow(Transform parent, Vector3 localPos, float hexScale, float yawDeg)
        {
            var row = new GameObject("WheatRow");
            row.transform.SetParent(parent, false);
            row.transform.localPosition = localPos;
            row.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            var stalkColor = new Color(0.48f, 0.32f, 0.12f);
            var headColor = new Color(0.96f, 0.58f, 0.14f);
            float w = hexScale * 0.05f;
            float h = hexScale * 0.46f;
            for (int i = 0; i < 7; i++)
            {
                float t = (i - 3f) * hexScale * 0.10f;
                CreatePart(row.transform, $"Stalk_{i}", new Vector3(t, h * 0.42f, 0f), PrimitiveType.Cube,
                    new Vector3(w, h * 0.85f, w), stalkColor);
                CreatePart(row.transform, $"Head_{i}", new Vector3(t, h * 0.88f, 0f), PrimitiveType.Sphere,
                    new Vector3(w * 2.4f, w * 2.2f, w * 2.4f), headColor);
            }
        }

        public static void CreateWheatSheaf(Transform parent, Vector3 localPos, float hexScale)
        {
            var sheaf = new GameObject("WheatSheaf");
            sheaf.transform.SetParent(parent, false);
            sheaf.transform.localPosition = localPos;
            var gold = new Color(0.95f, 0.52f, 0.12f);
            var straw = new Color(0.42f, 0.28f, 0.12f);
            float s = hexScale * 0.26f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 12f;
                var off = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * s * 0.22f, s * 0.55f,
                    Mathf.Sin(a * Mathf.Deg2Rad) * s * 0.22f);
                CreatePart(sheaf.transform, $"Bundle_{i}", off, PrimitiveType.Cylinder,
                    new Vector3(s * 0.22f, s * 1.35f, s * 0.22f), gold);
            }
            CreatePart(sheaf.transform, "Tie", Vector3.up * s * 0.35f, PrimitiveType.Cylinder,
                new Vector3(s * 0.55f, s * 0.18f, s * 0.55f), straw);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f;
                var off = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * s * 0.38f, s * 0.95f,
                    Mathf.Sin(a * Mathf.Deg2Rad) * s * 0.38f);
                CreatePart(sheaf.transform, $"Head_{i}", off, PrimitiveType.Sphere,
                    Vector3.one * s * 0.32f, gold);
            }
        }

        public static void CreateBrickStack(Transform parent, Vector3 localPos, float hexScale)
        {
            var stack = new GameObject("BrickStack");
            stack.transform.SetParent(parent, false);
            stack.transform.localPosition = localPos;
            var brick = new Color(0.62f, 0.26f, 0.16f);
            var mortar = new Color(0.48f, 0.22f, 0.14f);
            float bw = hexScale * 0.16f;
            float bh = hexScale * 0.07f;
            float bd = hexScale * 0.11f;
            for (int layer = 0; layer < 4; layer++)
            {
                int count = layer % 2 == 0 ? 3 : 2;
                float y = layer * bh;
                float xShift = layer % 2 == 0 ? 0f : bw * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    float x = (i - (count - 1) * 0.5f) * bw * 0.95f + xShift;
                    CreatePart(stack.transform, $"Brick_{layer}_{i}", new Vector3(x, y + bh * 0.5f, 0f),
                        PrimitiveType.Cube, new Vector3(bw, bh, bd), brick);
                }
                if (layer > 0)
                {
                    CreatePart(stack.transform, $"Mortar_{layer}", new Vector3(0f, y, 0f), PrimitiveType.Cube,
                        new Vector3(bw * 2.1f, bh * 0.12f, bd * 1.05f), mortar);
                }
            }
        }

        public static void CreateKiln(Transform parent, Vector3 localPos, float hexScale)
        {
            float s = hexScale * 0.30f;
            CreatePart(parent, "Kiln", localPos + Vector3.up * s * 0.45f, PrimitiveType.Cylinder,
                new Vector3(s, s * 0.9f, s),
                new Color(0.48f, 0.26f, 0.18f));
            CreatePart(parent, "Chimney", localPos + Vector3.up * (s * 1.35f), PrimitiveType.Cylinder,
                new Vector3(s * 0.28f, s * 0.85f, s * 0.28f),
                new Color(0.18f, 0.18f, 0.20f));
        }

        public static void CreateRockPile(Transform parent, Vector3 localPos, float hexScale)
        {
            var pile = new GameObject("RockPile");
            pile.transform.SetParent(parent, false);
            pile.transform.localPosition = localPos;
            float r = hexScale * 0.30f;
            // Dark slate so boulders contrast with the light-grey mountain tile.
            var greyA = new Color(0.32f, 0.34f, 0.38f);
            var greyB = new Color(0.22f, 0.24f, 0.28f);
            CreatePart(pile.transform, "BoulderA", new Vector3(-r * 0.22f, r * 0.22f, r * 0.08f),
                PrimitiveType.Sphere, Vector3.one * r * 0.55f, greyA);
            CreatePart(pile.transform, "BoulderB", new Vector3(r * 0.18f, r * 0.18f, -r * 0.12f),
                PrimitiveType.Sphere, new Vector3(r * 0.48f, r * 0.38f, r * 0.52f), greyB);
            CreatePart(pile.transform, "BoulderC", new Vector3(0f, r * 0.32f, r * 0.2f),
                PrimitiveType.Sphere, Vector3.one * r * 0.42f, greyA);
            CreatePart(pile.transform, "BoulderD", new Vector3(r * 0.05f, r * 0.12f, -r * 0.28f),
                PrimitiveType.Cube, new Vector3(r * 0.5f, r * 0.28f, r * 0.45f), greyB);
        }

        public static void CreateMineEntrance(Transform parent, Vector3 localPos, float hexScale)
        {
            float w = hexScale * 0.26f;
            var timber = new Color(0.46f, 0.34f, 0.22f);
            CreatePart(parent, "MineFrameL", localPos + new Vector3(-w * 0.55f, w * 0.42f, 0f), PrimitiveType.Cube,
                new Vector3(w * 0.18f, w * 0.85f, w * 0.22f), timber);
            CreatePart(parent, "MineFrameR", localPos + new Vector3(w * 0.55f, w * 0.42f, 0f), PrimitiveType.Cube,
                new Vector3(w * 0.18f, w * 0.85f, w * 0.22f), timber);
            CreatePart(parent, "MineLint", localPos + new Vector3(0f, w * 0.78f, 0f), PrimitiveType.Cube,
                new Vector3(w * 1.35f, w * 0.16f, w * 0.24f), timber);
            CreatePart(parent, "MineArch", localPos + new Vector3(0f, w * 0.32f, -w * 0.06f), PrimitiveType.Cube,
                new Vector3(w * 0.95f, w * 0.55f, w * 0.32f), new Color(0.14f, 0.14f, 0.16f));
        }

        public static void CreateFenceSegment(Transform parent, Vector3 localPos, float length, float yawDeg)
        {
            var fence = new GameObject("Fence");
            fence.transform.SetParent(parent, false);
            fence.transform.localPosition = localPos;
            fence.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            var wood = new Color(0.52f, 0.38f, 0.22f);
            CreatePart(fence.transform, "Rail", Vector3.zero, PrimitiveType.Cube,
                new Vector3(length, 0.06f, 0.05f), wood);
            CreatePart(fence.transform, "PostL", new Vector3(-length * 0.5f, 0.05f, 0f), PrimitiveType.Cube,
                new Vector3(0.06f, 0.14f, 0.06f), wood);
            CreatePart(fence.transform, "PostR", new Vector3(length * 0.5f, 0.05f, 0f), PrimitiveType.Cube,
                new Vector3(0.06f, 0.14f, 0.06f), wood);
        }

        public static void CreateReedClump(Transform parent, Vector3 localPos, float hexScale)
        {
            var green = new Color(0.22f, 0.62f, 0.26f);
            var greenDark = new Color(0.18f, 0.48f, 0.20f);
            float h = hexScale * 0.42f;
            for (int i = 0; i < 7; i++)
            {
                float a = i * 42f;
                var offset = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * hexScale * 0.07f, 0f,
                    Mathf.Sin(a * Mathf.Deg2Rad) * hexScale * 0.07f);
                var color = i % 2 == 0 ? green : greenDark;
                CreatePart(parent, $"Reed_{i}", localPos + offset, PrimitiveType.Cube,
                    new Vector3(hexScale * 0.045f, h * (0.85f + i * 0.03f), hexScale * 0.045f), color);
            }
        }

        public static void CreateTreeShadowDisc(Transform parent, Vector3 localPos, float hexScale)
        {
            float y = HexPrismMesh.TopY + 0.012f;
            CreatePart(parent, "TreeShadow", new Vector3(localPos.x, y, localPos.z), PrimitiveType.Cylinder,
                new Vector3(hexScale * 0.15f, 0.01f, hexScale * 0.15f), new Color(0.10f, 0.14f, 0.10f));
        }

        public static void CreateSpriteShadowDisc(Transform parent, Vector3 localPos, float hexScale, float radius)
        {
            float y = -0.02f;
            var shadow = CreatePart(parent, "SpriteShadow", localPos + new Vector3(0f, y, 0f), PrimitiveType.Cylinder,
                new Vector3(radius, 0.008f, radius), new Color(0.08f, 0.10f, 0.08f));
            var renderer = shadow.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = BuiltInMaterials.CreateMatteOpaque(new Color(0.08f, 0.10f, 0.08f));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public static void CreateClayPit(Transform parent, Vector3 localPos, float hexScale)
        {
            float s = hexScale * 0.28f;
            CreatePart(parent, "ClayBowl", localPos + Vector3.up * s * 0.12f, PrimitiveType.Cylinder,
                new Vector3(s, s * 0.22f, s), new Color(0.58f, 0.30f, 0.20f));
            CreatePart(parent, "ClayRim", localPos + Vector3.up * s * 0.22f, PrimitiveType.Cylinder,
                new Vector3(s * 1.08f, s * 0.06f, s * 1.08f), new Color(0.48f, 0.24f, 0.16f));
        }

        public static void CreateMountainPeak(Transform parent, Vector3 localPos, float hexScale)
        {
            float s = hexScale * 0.32f;
            CreatePart(parent, "PeakBase", localPos + Vector3.up * s * 0.22f, PrimitiveType.Cylinder,
                new Vector3(s * 0.9f, s * 0.35f, s * 0.9f), new Color(0.28f, 0.30f, 0.34f));
            var top = CreatePart(parent, "PeakTop", localPos + Vector3.up * s * 0.62f, PrimitiveType.Cube,
                new Vector3(s * 0.55f, s * 0.55f, s * 0.55f), new Color(0.94f, 0.95f, 0.97f)); // snow cap
            top.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
        }

        public static void CreateHayBale(Transform parent, Vector3 localPos, float hexScale)
        {
            float s = hexScale * 0.24f;
            CreatePart(parent, "HayBale", localPos + Vector3.up * s * 0.35f, PrimitiveType.Cylinder,
                new Vector3(s * 1.1f, s * 0.7f, s * 1.1f), new Color(0.94f, 0.62f, 0.16f));
            CreatePart(parent, "HayBand", localPos + Vector3.up * s * 0.35f, PrimitiveType.Cylinder,
                new Vector3(s * 1.15f, s * 0.12f, s * 1.15f), new Color(0.44f, 0.28f, 0.12f));
        }

        public static void CreateWindmill(Transform parent, Vector3 localPos, float hexScale)
        {
            float s = hexScale * 0.26f;
            var wood = new Color(0.52f, 0.38f, 0.22f);
            var white = new Color(0.92f, 0.90f, 0.84f);
            CreatePart(parent, "MillTower", localPos + Vector3.up * s * 0.55f, PrimitiveType.Cylinder,
                new Vector3(s * 0.35f, s * 1.1f, s * 0.35f), white);
            CreatePart(parent, "MillBlades", localPos + Vector3.up * s * 1.05f, PrimitiveType.Cube,
                new Vector3(s * 1.4f, s * 0.08f, s * 0.22f), wood);
            CreatePart(parent, "MillBladesB", localPos + Vector3.up * s * 1.05f, PrimitiveType.Cube,
                new Vector3(s * 0.22f, s * 0.08f, s * 1.4f), wood);
            CreatePart(parent, "MillRoof", localPos + Vector3.up * (s * 1.22f), PrimitiveType.Cylinder,
                new Vector3(s * 0.55f, s * 0.22f, s * 0.55f), new Color(0.62f, 0.26f, 0.16f));
        }

        public static void CreateCactus(Transform parent, Vector3 localPos, float hexScale)
        {
            // Readable from the top-down camera: a thick stem plus two arms lying sideways
            // (a vertical thin cylinder only showed as a green dot).
            var green = new Color(0.16f, 0.50f, 0.24f);
            var greenLight = new Color(0.30f, 0.66f, 0.34f);
            float h = hexScale * 0.26f;
            float w = hexScale * 0.13f;
            CreatePart(parent, "CactusStem", localPos + Vector3.up * h * 0.5f, PrimitiveType.Capsule,
                new Vector3(w, h * 0.5f, w), green);
            var armL = CreatePart(parent, "CactusArmL", localPos + new Vector3(-w * 0.95f, h * 0.55f, 0f),
                PrimitiveType.Capsule, new Vector3(w * 0.62f, w * 0.75f, w * 0.62f), greenLight);
            armL.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var armR = CreatePart(parent, "CactusArmR", localPos + new Vector3(w * 0.95f, h * 0.70f, w * 0.2f),
                PrimitiveType.Capsule, new Vector3(w * 0.62f, w * 0.75f, w * 0.62f), greenLight);
            armR.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        public static void CreateDesertRock(Transform parent, Vector3 localPos, float hexScale)
        {
            float s = hexScale * 0.16f;
            CreatePart(parent, "DesertRock", localPos + Vector3.up * s * 0.35f, PrimitiveType.Sphere,
                new Vector3(s * 1.2f, s * 0.7f, s), new Color(0.62f, 0.56f, 0.48f));
        }

        public static void CreateBoat(Transform parent, Vector3 localPos, float hexScale)
        {
            float len = hexScale * 0.22f;
            var hull = CreatePart(parent, "BoatHull", localPos, PrimitiveType.Cube,
                new Vector3(len * 1.4f, len * 0.35f, len * 0.55f),
                new Color(0.48f, 0.30f, 0.16f));
            hull.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
        }

        private static GameObject CreatePart(
            Transform parent,
            string name,
            Vector3 localPos,
            PrimitiveType type,
            Vector3 scale,
            Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = BuiltInMaterials.CreateMatteOpaque(color);
            var col = go.GetComponent<Collider>();
            if (col != null)
                ColonistSceneDestroy.SafeDestroy(col);
            return go;
        }
    }
}
