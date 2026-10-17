using CatanRoguelike.Core;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Readable primitive props scaled in world units (~0.25–0.35 hex).</summary>
    internal static class ColonistHexTilePropPrimitives
    {
        public static GameObject CreateSheep(Transform parent, Vector3 localPos, float scale, int seed)
        {
            var body = CreatePart(parent, "SheepBody", localPos, PrimitiveType.Sphere,
                new Vector3(scale * 1.2f, scale * 0.85f, scale * 1.4f),
                new Color(0.94f, 0.94f, 0.96f));
            CreatePart(parent, "SheepHead", localPos + new Vector3(0f, scale * 0.2f, scale * 0.65f),
                PrimitiveType.Sphere, Vector3.one * scale * 0.55f, new Color(0.92f, 0.92f, 0.94f));
            body.transform.localRotation = Quaternion.Euler(0f, seed * 37f, 0f);
            return body;
        }

        public static void CreateWheatRow(Transform parent, Vector3 localPos, float hexScale, float yawDeg)
        {
            var row = new GameObject("WheatRow");
            row.transform.SetParent(parent, false);
            row.transform.localPosition = localPos;
            row.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            var stalkColor = new Color(0.90f, 0.76f, 0.16f);
            float w = hexScale * 0.06f;
            float h = hexScale * 0.32f;
            for (int i = 0; i < 6; i++)
            {
                float t = (i - 2.5f) * hexScale * 0.11f;
                CreatePart(row.transform, $"Stalk_{i}", new Vector3(t, h * 0.5f, 0f), PrimitiveType.Cube,
                    new Vector3(w, h, w), stalkColor);
            }
        }

        public static void CreateWheatSheaf(Transform parent, Vector3 localPos, float hexScale)
        {
            var sheaf = new GameObject("WheatSheaf");
            sheaf.transform.SetParent(parent, false);
            sheaf.transform.localPosition = localPos;
            var gold = new Color(0.92f, 0.78f, 0.14f);
            float s = hexScale * 0.22f;
            CreatePart(sheaf.transform, "SheafCore", Vector3.up * s * 0.4f, PrimitiveType.Cylinder,
                new Vector3(s, s * 1.1f, s), gold);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f;
                var off = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * s * 0.5f, s * 0.35f,
                    Mathf.Sin(a * Mathf.Deg2Rad) * s * 0.5f);
                CreatePart(sheaf.transform, $"Head_{i}", off, PrimitiveType.Sphere,
                    Vector3.one * s * 0.35f, gold);
            }
        }

        public static void CreateBrickStack(Transform parent, Vector3 localPos, float hexScale)
        {
            var stack = new GameObject("BrickStack");
            stack.transform.SetParent(parent, false);
            stack.transform.localPosition = localPos;
            var brick = new Color(0.78f, 0.34f, 0.20f);
            float bw = hexScale * 0.18f;
            float bh = hexScale * 0.08f;
            float bd = hexScale * 0.12f;
            for (int i = 0; i < 3; i++)
            {
                CreatePart(stack.transform, $"Brick_{i}", new Vector3(i * bw * 0.45f - bw * 0.45f, i * bh, 0f),
                    PrimitiveType.Cube, new Vector3(bw, bh, bd), brick);
            }
        }

        public static void CreateKiln(Transform parent, Vector3 localPos, float hexScale)
        {
            float s = hexScale * 0.28f;
            CreatePart(parent, "Kiln", localPos, PrimitiveType.Cylinder,
                new Vector3(s, s * 0.9f, s),
                new Color(0.52f, 0.30f, 0.22f));
        }

        public static void CreateMineEntrance(Transform parent, Vector3 localPos, float hexScale)
        {
            float w = hexScale * 0.22f;
            CreatePart(parent, "MineArch", localPos + Vector3.up * w * 0.35f, PrimitiveType.Cube,
                new Vector3(w * 1.2f, w * 0.7f, w * 0.35f), new Color(0.22f, 0.22f, 0.26f));
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
            var green = new Color(0.28f, 0.58f, 0.24f);
            float h = hexScale * 0.32f;
            for (int i = 0; i < 5; i++)
            {
                float a = i * 48f;
                var offset = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * hexScale * 0.06f, 0f,
                    Mathf.Sin(a * Mathf.Deg2Rad) * hexScale * 0.06f);
                CreatePart(parent, $"Reed_{i}", localPos + offset, PrimitiveType.Cube,
                    new Vector3(hexScale * 0.04f, h, hexScale * 0.04f), green);
            }
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
            go.GetComponent<Renderer>().sharedMaterial = BuiltInMaterials.CreateOpaque(color);
            var col = go.GetComponent<Collider>();
            if (col != null)
                ColonistSceneDestroy.SafeDestroy(col);
            return go;
        }
    }
}
