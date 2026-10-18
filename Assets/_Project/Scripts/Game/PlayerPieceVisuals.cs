using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Board pieces: colonist mode uses top-down-readable primitive houses (ridge + two roof tones).
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
        public const string RoofRidgePartName = "RoofRidge";
        public const string PlankPartName = "Plank";
        public const string TowerRoofLeftPartName = "TowerRoofLeft";
        public const string TowerRoofRightPartName = "TowerRoofRight";
        public const string TowerRoofRidgePartName = "TowerRoofRidge";
        public const string KenneyModelChildName = "KenneyModel";

        public static readonly Color HumanColor = new(0.2f, 0.4f, 0.9f);
        public static readonly Color AiColor = new(0.9f, 0.25f, 0.2f);

        private const float RoofPitchDegrees = 38f;
        private const float BaseGapAboveHex = 0.02f;
        private const float BodyTopBelowRoofEave = 0.06f;
        private const float RidgeLift = 0.02f;
        private const float RidgeThickness = 0.10f;
        public const float RoofColorMultiply = 0.55f;
        public const float RoofGlossiness = 0.02f;

        public static Color ColorForPlayer(PlayerId owner) =>
            owner == PlayerId.Human ? HumanColor : AiColor;

        public static Color DarkenForRoof(Color color) =>
            new(color.r * RoofColorMultiply, color.g * RoofColorMultiply, color.b * RoofColorMultiply, color.a);

        public static Material CreateRoofMaterial(Color playerColor)
        {
            var mat = BuiltInMaterials.Create(DarkenForRoof(playerColor));
            mat.SetFloat("_Glossiness", RoofGlossiness);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        public static bool UsesKenneyModels => ColonistKenneyPlayerPieceMeshes.UsesKenneyModels;

        public static GameObject CreateSettlement(Transform parent, Vector3 worldPosition, Color color, float hexTopY)
        {
            if (!ColonistPresentation.IsActive)
            {
                var imported = ColonistKenneyPlayerPieceMeshes.TryCreateSettlement(
                    parent, worldPosition, color, hexTopY);
                if (imported != null)
                    return imported;
            }

            var root = CreateRoot(parent, SettlementName, worldPosition);
            var bodyMaterial = BuiltInMaterials.Create(color);

            const float bodyWidth = 0.32f;
            const float bodyNominalHeight = 0.22f;
            const float bodyDepth = 0.30f;
            float bodyHeight = bodyNominalHeight - BodyTopBelowRoofEave;
            float roofY = hexTopY + BaseGapAboveHex + bodyNominalHeight;

            AddBody(root.transform, bodyWidth, bodyHeight, bodyDepth, hexTopY, bodyMaterial);
            AddColonistTopDownRoof(root.transform, bodyWidth, bodyDepth, roofY, color);
            DisableShadows(root);

            return root;
        }

        public static GameObject CreateCity(Transform parent, Vector3 worldPosition, Color color, float hexTopY)
        {
            if (!ColonistPresentation.IsActive)
            {
                var imported = ColonistKenneyPlayerPieceMeshes.TryCreateCity(
                    parent, worldPosition, color, hexTopY);
                if (imported != null)
                    return imported;
            }

            var root = CreateRoot(parent, CityName, worldPosition);
            var bodyMaterial = BuiltInMaterials.Create(color);

            const float wingWidth = 0.36f;
            const float wingDepth = 0.30f;
            const float wingNominalHeight = 0.24f;
            const float towerWidth = 0.22f;
            const float towerDepth = 0.20f;
            const float towerNominalHeight = 0.44f;

            float wingHeight = wingNominalHeight - BodyTopBelowRoofEave;
            float towerHeight = towerNominalHeight - BodyTopBelowRoofEave;
            float baseY = hexTopY + BaseGapAboveHex;

            CreatePrimitivePart(
                root.transform,
                PrimitiveType.Cube,
                BodyPartName,
                new Vector3(-0.06f, baseY + wingHeight * 0.5f, -0.04f),
                new Vector3(wingWidth, wingHeight, wingDepth),
                Quaternion.identity,
                bodyMaterial);

            CreatePrimitivePart(
                root.transform,
                PrimitiveType.Cube,
                UpperStoreyPartName,
                new Vector3(0.14f, baseY + wingHeight + towerHeight * 0.5f, 0.12f),
                new Vector3(towerWidth, towerHeight, towerDepth),
                Quaternion.identity,
                bodyMaterial);

            float wingRoofY = baseY + wingNominalHeight;
            AddColonistTopDownRoof(root.transform, wingWidth, wingDepth, wingRoofY, color, -0.06f, -0.04f);

            float towerRoofY = baseY + wingNominalHeight + towerNominalHeight;
            AddColonistTopDownRoof(
                root.transform,
                towerWidth,
                towerDepth,
                towerRoofY,
                color,
                0.14f,
                0.12f,
                ridgeName: "TowerRoofRidge",
                leftName: "TowerRoofLeft",
                rightName: "TowerRoofRight");

            DisableShadows(root);
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
            if (!ColonistPresentation.IsActive)
            {
                var imported = ColonistKenneyPlayerPieceMeshes.TryCreateRoad(
                    parent, midpoint, angleY, length, color, hexTopY);
                if (imported != null)
                    return imported;
            }

            var root = CreateRoot(parent, RoadName, midpoint);
            root.transform.rotation = Quaternion.Euler(0f, angleY, 0f);

            const float roadWidth = 0.14f;
            const float roadThickness = 0.048f;
            float centerY = hexTopY + roadThickness * 0.5f + 0.028f;

            var material = BuiltInMaterials.Create(color);
            CreatePrimitivePart(
                root.transform,
                PrimitiveType.Cube,
                PlankPartName,
                new Vector3(0f, centerY, 0f),
                new Vector3(roadWidth, roadThickness, length * 0.92f),
                Quaternion.identity,
                material);

            DisableShadows(root);
            return root;
        }

        private static void DisableShadows(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
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

        private static void AddColonistTopDownRoof(
            Transform parent,
            float bodyWidth,
            float bodyDepth,
            float roofY,
            Color playerColor,
            float offsetX = 0f,
            float offsetZ = 0f,
            string leftName = RoofLeftPartName,
            string rightName = RoofRightPartName,
            string ridgeName = RoofRidgePartName)
        {
            var roofLit = BuiltInMaterials.Create(playerColor);
            roofLit.SetFloat("_Glossiness", 0.03f);
            var roofShade = BuiltInMaterials.Create(
                new Color(playerColor.r * RoofColorMultiply, playerColor.g * RoofColorMultiply, playerColor.b * RoofColorMultiply, 1f));
            roofShade.SetFloat("_Glossiness", 0.02f);
            var roofRidge = BuiltInMaterials.Create(
                new Color(playerColor.r * 0.38f, playerColor.g * 0.38f, playerColor.b * 0.38f, 1f));
            roofRidge.SetFloat("_Glossiness", 0.01f);
            var outline = BuiltInMaterials.Create(
                new Color(playerColor.r * 0.22f, playerColor.g * 0.22f, playerColor.b * 0.22f, 1f));

            float slab = 0.048f;
            float halfSpan = bodyWidth * 0.54f;
            float depth = bodyDepth * 1.22f;

            CreatePrimitivePart(
                parent,
                PrimitiveType.Cube,
                leftName,
                new Vector3(offsetX - halfSpan * 0.5f, roofY, offsetZ),
                new Vector3(halfSpan, slab, depth),
                Quaternion.identity,
                roofLit);

            CreatePrimitivePart(
                parent,
                PrimitiveType.Cube,
                rightName,
                new Vector3(offsetX + halfSpan * 0.5f, roofY + slab * 0.2f, offsetZ),
                new Vector3(halfSpan, slab, depth),
                Quaternion.identity,
                roofShade);

            CreatePrimitivePart(
                parent,
                PrimitiveType.Cube,
                ridgeName,
                new Vector3(offsetX, roofY + slab * 0.65f, offsetZ),
                new Vector3(bodyWidth * 0.12f, slab * 1.4f, depth * 1.04f),
                Quaternion.identity,
                roofRidge);

            float edge = 0.018f;
            float edgeY = roofY - slab * 0.35f;
            CreatePrimitivePart(
                parent,
                PrimitiveType.Cube,
                "RoofOutline",
                new Vector3(offsetX, edgeY, offsetZ + depth * 0.5f),
                new Vector3(bodyWidth * 1.08f, edge, edge),
                Quaternion.identity,
                outline);
            CreatePrimitivePart(
                parent,
                PrimitiveType.Cube,
                "RoofOutline",
                new Vector3(offsetX, edgeY, offsetZ - depth * 0.5f),
                new Vector3(bodyWidth * 1.08f, edge, edge),
                Quaternion.identity,
                outline);
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
            float panelDepth = bodyDepth * 0.62f;
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

            float ridgeDepth = bodyDepth * 1.10f;
            float ridgeCenterY = roofBaseY + RidgeLift + RidgeThickness * 0.5f;

            CreatePrimitivePart(
                parent,
                PrimitiveType.Cube,
                RoofRidgePartName,
                new Vector3(0f, ridgeCenterY, 0f),
                new Vector3(panelWidth, RidgeThickness, ridgeDepth),
                Quaternion.identity,
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
