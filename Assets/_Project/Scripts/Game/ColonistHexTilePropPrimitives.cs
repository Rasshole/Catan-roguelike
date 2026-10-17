using CatanRoguelike.Core;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Simple primitive props when FBX variety is not needed.</summary>
    internal static class ColonistHexTilePropPrimitives
    {
        public static GameObject CreateSheep(Transform parent, Vector3 localPos, float scale, int seed)
        {
            var body = CreatePart(parent, "SheepBody", localPos, PrimitiveType.Sphere,
                new Vector3(scale * 1.1f, scale * 0.75f, scale * 1.3f),
                new Color(0.94f, 0.94f, 0.96f));
            CreatePart(parent, "SheepHead", localPos + new Vector3(0f, scale * 0.15f, scale * 0.55f),
                PrimitiveType.Sphere, Vector3.one * scale * 0.45f, new Color(0.92f, 0.92f, 0.94f));
            body.transform.localRotation = Quaternion.Euler(0f, seed * 37f, 0f);
            return body;
        }

        public static void CreateWheatRow(Transform parent, Vector3 localPos, float length, float yawDeg)
        {
            var row = new GameObject("WheatRow");
            row.transform.SetParent(parent, false);
            row.transform.localPosition = localPos;
            row.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            var stalkColor = new Color(0.86f, 0.72f, 0.18f);
            for (int i = 0; i < 5; i++)
            {
                float t = (i - 2) * 0.12f;
                CreatePart(row.transform, $"Stalk_{i}", new Vector3(t, 0f, 0f), PrimitiveType.Cube,
                    new Vector3(0.04f, 0.22f, 0.04f), stalkColor);
            }
        }

        public static void CreateBrickStack(Transform parent, Vector3 localPos, float scale)
        {
            var stack = new GameObject("BrickStack");
            stack.transform.SetParent(parent, false);
            stack.transform.localPosition = localPos;
            var brick = new Color(0.78f, 0.36f, 0.22f);
            for (int i = 0; i < 3; i++)
            {
                CreatePart(stack.transform, $"Brick_{i}", new Vector3(i * 0.08f - 0.08f, i * 0.06f, 0f),
                    PrimitiveType.Cube, new Vector3(0.14f, 0.06f, 0.08f) * scale, brick);
            }
        }

        public static void CreateKiln(Transform parent, Vector3 localPos, float scale)
        {
            CreatePart(parent, "Kiln", localPos, PrimitiveType.Cylinder,
                new Vector3(scale * 0.35f, scale * 0.28f, scale * 0.35f),
                new Color(0.55f, 0.32f, 0.24f));
        }

        public static void CreateFenceSegment(Transform parent, Vector3 localPos, float length, float yawDeg)
        {
            var fence = new GameObject("Fence");
            fence.transform.SetParent(parent, false);
            fence.transform.localPosition = localPos;
            fence.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            var wood = new Color(0.52f, 0.38f, 0.22f);
            CreatePart(fence.transform, "Rail", Vector3.zero, PrimitiveType.Cube,
                new Vector3(length, 0.05f, 0.04f), wood);
            CreatePart(fence.transform, "PostL", new Vector3(-length * 0.5f, 0.04f, 0f), PrimitiveType.Cube,
                new Vector3(0.05f, 0.12f, 0.05f), wood);
            CreatePart(fence.transform, "PostR", new Vector3(length * 0.5f, 0.04f, 0f), PrimitiveType.Cube,
                new Vector3(0.05f, 0.12f, 0.05f), wood);
        }

        public static void CreateReedClump(Transform parent, Vector3 localPos, float scale)
        {
            var green = new Color(0.32f, 0.62f, 0.28f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 55f;
                var offset = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.08f, 0f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.08f);
                CreatePart(parent, $"Reed_{i}", localPos + offset, PrimitiveType.Cube,
                    new Vector3(0.03f, 0.28f * scale, 0.03f), green);
            }
        }

        public static void CreateBoat(Transform parent, Vector3 localPos, float scale)
        {
            var hull = CreatePart(parent, "BoatHull", localPos, PrimitiveType.Cube,
                new Vector3(scale * 0.55f, scale * 0.12f, scale * 0.22f),
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
