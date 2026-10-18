using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Kenney Mini Strategy / Medieval building meshes for board pieces (top-down readable).</summary>
    internal static class ColonistKenneyPlayerPieceMeshes
    {
        private const string ModelRoot = "Art/Models/Kenney";
        public const string ModelChildName = PlayerPieceVisuals.KenneyModelChildName;

        private const float SettlementFootprint = 0.34f;
        private const float CityFootprint = 0.42f;
        private const float RoadWidth = 0.16f;
        private const float BaseGapAboveHex = 0.02f;

        public static bool ModelsAvailable =>
            Resources.Load<GameObject>($"{ModelRoot}/building-farm") != null;

        public static GameObject TryCreateSettlement(
            Transform parent,
            Vector3 worldPosition,
            Color playerColor,
            float hexTopY)
        {
            return TryPlaceModel(
                parent,
                worldPosition,
                "building-farm",
                SettlementFootprint,
                playerColor,
                hexTopY,
                yawOffsetDeg: 0f);
        }

        public static GameObject TryCreateCity(
            Transform parent,
            Vector3 worldPosition,
            Color playerColor,
            float hexTopY)
        {
            return TryPlaceModel(
                parent,
                worldPosition,
                "building-smelter",
                CityFootprint,
                playerColor,
                hexTopY,
                yawOffsetDeg: 18f);
        }

        public static GameObject TryCreateRoad(
            Transform parent,
            Vector3 midpoint,
            float angleY,
            float length,
            Color playerColor,
            float hexTopY)
        {
            var prefab = Resources.Load<GameObject>($"{ModelRoot}/fence_planksDouble");
            if (prefab == null)
                return null;

            var root = new GameObject(PlayerPieceVisuals.RoadName);
            root.transform.SetParent(parent, false);
            root.transform.position = midpoint;
            root.transform.rotation = Quaternion.Euler(0f, angleY, 0f);

            var model = Object.Instantiate(prefab, root.transform, false);
            model.name = ModelChildName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            float targetLen = Mathf.Max(length * 0.92f, hexTopY * 0.5f);
            FitModelToRoad(model.transform, targetLen, RoadWidth, hexTopY);
            TintRenderers(model, playerColor, roofDarken: 0.72f);
            StripColliders(model);
            return root;
        }

        private static GameObject TryPlaceModel(
            Transform parent,
            Vector3 worldPosition,
            string modelName,
            float targetFootprint,
            Color playerColor,
            float hexTopY,
            float yawOffsetDeg)
        {
            var prefab = Resources.Load<GameObject>($"{ModelRoot}/{modelName}");
            if (prefab == null)
                return null;

            var root = new GameObject(
                modelName.Contains("smelter") ? PlayerPieceVisuals.CityName : PlayerPieceVisuals.SettlementName);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;
            root.transform.rotation = Quaternion.Euler(0f, yawOffsetDeg, 0f);

            var model = Object.Instantiate(prefab, root.transform, false);
            model.name = ModelChildName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            FitModelToFootprint(model.transform, targetFootprint, hexTopY);
            TintRenderers(model, playerColor, roofDarken: 0.62f);
            StripColliders(model);
            return root;
        }

        private static void FitModelToFootprint(Transform model, float targetFootprint, float hexTopY)
        {
            var bounds = ComputeLocalBounds(model);
            if (bounds.size.sqrMagnitude < 1e-6f)
                return;

            float xz = Mathf.Max(bounds.size.x, bounds.size.z);
            float scale = xz > 1e-4f ? targetFootprint / xz : 1f;
            model.localScale = Vector3.one * scale;

            bounds = ComputeLocalBounds(model);
            float lift = hexTopY + BaseGapAboveHex - bounds.min.y;
            model.localPosition += new Vector3(0f, lift, 0f);
        }

        private static void FitModelToRoad(Transform model, float targetLength, float targetWidth, float hexTopY)
        {
            var bounds = ComputeLocalBounds(model);
            if (bounds.size.sqrMagnitude < 1e-6f)
                return;

            float scaleZ = bounds.size.z > 1e-4f ? targetLength / bounds.size.z : 1f;
            float scaleX = bounds.size.x > 1e-4f ? targetWidth / bounds.size.x : scaleZ;
            float uniform = Mathf.Min(scaleX, scaleZ);
            model.localScale = new Vector3(uniform * 1.05f, uniform * 0.55f, uniform);

            bounds = ComputeLocalBounds(model);
            float lift = hexTopY + 0.01f - bounds.min.y;
            model.localPosition += new Vector3(0f, lift, 0f);
        }

        private static Bounds ComputeLocalBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            var bounds = new Bounds(root.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (var r in renderers)
            {
                var world = r.bounds;
                bounds.Encapsulate(root.InverseTransformPoint(world.min));
                bounds.Encapsulate(root.InverseTransformPoint(world.max));
            }

            return bounds;
        }

        private static void TintRenderers(GameObject model, Color playerColor, float roofDarken)
        {
            var body = BuiltInMaterials.Create(playerColor);
            var roof = BuiltInMaterials.Create(
                new Color(playerColor.r * roofDarken, playerColor.g * roofDarken, playerColor.b * roofDarken, 1f));

            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                string lower = renderer.gameObject.name.ToLowerInvariant();
                bool roofLike = lower.Contains("roof") || lower.Contains("chimney") || lower.Contains("tower");
                renderer.sharedMaterial = roofLike ? roof : body;
            }
        }

        private static void StripColliders(GameObject model)
        {
            foreach (var collider in model.GetComponentsInChildren<Collider>())
                ColonistSceneDestroy.SafeDestroy(collider);
        }
    }
}
