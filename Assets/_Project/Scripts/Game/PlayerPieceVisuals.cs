using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Primitive meshes for settlements, cities, and roads on the board.
    /// </summary>
    public static class PlayerPieceVisuals
    {
        public const string SettlementName = "Settlement";
        public const string CityName = "City";
        public const string RoadName = "Road";
        public const string BodyPartName = "Body";
        public const string UpperStoreyPartName = "UpperStorey";
        public const string RoofLeftPartName = "RoofLeft";
        public const string RoofRightPartName = "RoofRight";
        public const string PlankPartName = "Plank";

        public static readonly Color HumanColor = new(0.2f, 0.4f, 0.9f);
        public static readonly Color AiColor = new(0.9f, 0.25f, 0.2f);

        private const float RoofPitchDegrees = 38f;
        private const float BaseGapAboveHex = 0.02f;
        public const float RoofColorMultiply = 0.55f;

        public static Color ColorForPlayer(PlayerId owner) =>
            owner == PlayerId.Human ? HumanColor : AiColor;

        public static Color DarkenForRoof(Color color) =>
            new(color.r * RoofColorMultiply, color.g * RoofColorMultiply, color.b * RoofColorMultiply, color.a);

        public static GameObject CreateSettlement(Transform parent, Vector3 worldPosition, Color color, float hexTopY)
        {
            var root = CreateRoot(parent, SettlementName, worldPosition);
            var bodyMaterial = BuiltInMaterials.Create(color);
            var roofMaterial = BuiltInMaterials.Create(DarkenForRoof(color));

            const float bodyWidth = 0.24f;
            const float bodyHeight = 0.19f;
            const float bodyDepth = 0.24f;

            AddBody(root.transform, bodyWidth, bodyHeight, bodyDepth, hexTopY, bodyMaterial);
            AddPitchedRoof(
                root.transform,
                bodyWidth,
                bodyDepth,
                hexTopY + BaseGapAboveHex + bodyHeight,
                roofMaterial);

            return root;
        }

        public static GameObject CreateCity(Transform parent, Vector3 worldPosition, Color color, float hexTopY)
        {
            var root = CreateRoot(parent, CityName, worldPosition);
            var bodyMaterial = BuiltInMaterials.Create(color);
            var roofMaterial = BuiltInMaterials.Create(DarkenForRoof(color));

            const float bodyWidth = 0.30f;
            const float bodyHeight = 0.22f;
            const float bodyDepth = 0.30f;
            const float upperWidth = 0.22f;
            const float upperHeight = 0.19f;
            const float upperDepth = 0.22f;

            AddBody(root.transform, bodyWidth, bodyHeight, bodyDepth, hexTopY, bodyMaterial);

            float buildingBaseY = hexTopY + BaseGapAboveHex;
            float upperCenterY = buildingBaseY + bodyHeight + upperHeight * 0.5f;
            CreatePrimitivePart(
                root.transform,
                PrimitiveType.Cube,
                UpperStoreyPartName,
                new Vector3(0f, upperCenterY, 0f),
                new Vector3(upperWidth, upperHeight, upperDepth),
                Quaternion.identity,
                bodyMaterial);

            AddPitchedRoof(
                root.transform,
                upperWidth,
                upperDepth,
                buildingBaseY + bodyHeight + upperHeight,
                roofMaterial);

            return root;
        }

        public static GameObject CreateRoad(
            Transform parent,
            Vector3 midpoint,
            float angleY,
            float length,
            Color color,
            float hexTopY)
        {
            var root = CreateRoot(parent, RoadName, midpoint);
            root.transform.rotation = Quaternion.Euler(0f, angleY, 0f);

            const float roadWidth = 0.14f;
            const float roadThickness = 0.045f;
            float centerY = hexTopY + roadThickness * 0.5f + 0.008f;

            var material = BuiltInMaterials.Create(color);
            CreatePrimitivePart(
                root.transform,
                PrimitiveType.Cube,
                PlankPartName,
                new Vector3(0f, centerY, 0f),
                new Vector3(roadWidth, roadThickness, length * 0.92f),
                Quaternion.identity,
                material);

            return root;
        }

        private static GameObject CreateRoot(Transform parent, string name, Vector3 worldPosition)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;
            return root;
        }

        private static void AddBody(
            Transform parent,
            float width,
            float height,
            float depth,
            float hexTopY,
            Material material)
        {
            float centerY = hexTopY + BaseGapAboveHex + height * 0.5f;
            CreatePrimitivePart(
                parent,
                PrimitiveType.Cube,
                BodyPartName,
                new Vector3(0f, centerY, 0f),
                new Vector3(width, height, depth),
                Quaternion.identity,
                material);
        }

        private static void AddPitchedRoof(
            Transform parent,
            float bodyWidth,
            float bodyDepth,
            float roofBaseY,
            Material material)
        {
            const float panelThickness = 0.046f;
            float panelWidth = bodyWidth * 1.18f;
            float panelDepth = bodyDepth * 0.58f;
            float panelLift = panelDepth * Mathf.Sin(RoofPitchDegrees * Mathf.Deg2Rad) * 0.5f;

            CreatePrimitivePart(
                parent,
                PrimitiveType.Cube,
                RoofLeftPartName,
                new Vector3(0f, roofBaseY + panelLift, -bodyDepth * 0.24f),
                new Vector3(panelWidth, panelThickness, panelDepth),
                Quaternion.Euler(-RoofPitchDegrees, 0f, 0f),
                material);

            CreatePrimitivePart(
                parent,
                PrimitiveType.Cube,
                RoofRightPartName,
                new Vector3(0f, roofBaseY + panelLift, bodyDepth * 0.24f),
                new Vector3(panelWidth, panelThickness, panelDepth),
                Quaternion.Euler(RoofPitchDegrees, 0f, 0f),
                material);
        }

        private static void CreatePrimitivePart(
            Transform parent,
            PrimitiveType primitive,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.transform.localRotation = localRotation;

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
