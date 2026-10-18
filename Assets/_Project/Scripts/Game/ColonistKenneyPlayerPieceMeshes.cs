using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>Blender-authored Catan-style player pieces (settlements, cities, roads).</summary>
    internal static class ColonistKenneyPlayerPieceMeshes
    {
        private const string ModelRoot = "Art/Models/ColonistPieces";
        public const string ModelChildName = PlayerPieceVisuals.KenneyModelChildName;

        private const float SettlementFootprint = 0.62f;
        private const float CityFootprint = 0.72f;
        private const float RoadWidth = 0.16f;
        private const float BaseGapAboveHex = 0.02f;

        public static bool ModelsAvailable =>
            !ColonistPresentation.IsActive
            && Resources.Load<GameObject>($"{ModelRoot}/settlement") != null;

        /// <summary>Colonist pieces use Body/Roof part names at the piece root (not Kenney wrappers).</summary>
        public static bool UsesKenneyModels => false;

        public static GameObject TryCreateSettlement(
            Transform parent,
            Vector3 worldPosition,
            Color playerColor,
            float hexTopY)
        {
            return TryPlaceModel(
                parent,
                worldPosition,
                "settlement",
                PlayerPieceVisuals.SettlementName,
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
                "city",
                PlayerPieceVisuals.CityName,
                CityFootprint,
                playerColor,
                hexTopY,
                yawOffsetDeg: 14f);
        }

        public static GameObject TryCreateRoad(
            Transform parent,
            Vector3 midpoint,
            float angleY,
            float length,
            Color playerColor,
            float hexTopY)
        {
            var prefab = Resources.Load<GameObject>($"{ModelRoot}/road");
            if (prefab == null)
                return null;

            var root = Object.Instantiate(prefab, parent, false);
            root.name = PlayerPieceVisuals.RoadName;
            root.transform.position = midpoint;
            // Fit while axis-aligned: ComputeLocalBounds maps only the world AABB min/max corners back
            // to local space, so fitting after a 60°/120° yaw mis-measured width/length and some roads
            // rendered as 1 px hairlines (v117 capture). Rotate once the scale is set.
            root.transform.rotation = Quaternion.identity;

            float targetLen = Mathf.Max(length * 0.92f, 0.35f);
            FitModelToRoad(root.transform, targetLen, RoadWidth, hexTopY);
            root.transform.rotation = Quaternion.Euler(0f, angleY, 0f);
            TintRenderers(root, playerColor, roofDarken: 0.72f);
            FinishPiece(root, hexTopY);
            return root;
        }

        private static GameObject TryPlaceModel(
            Transform parent,
            Vector3 worldPosition,
            string resourceName,
            string instanceName,
            float targetFootprint,
            Color playerColor,
            float hexTopY,
            float yawOffsetDeg)
        {
            var prefab = Resources.Load<GameObject>($"{ModelRoot}/{resourceName}");
            if (prefab == null)
                return null;

            var root = Object.Instantiate(prefab, parent, false);
            root.name = instanceName;
            root.transform.position = worldPosition;
            root.transform.rotation = Quaternion.identity;

            FitModelToFootprint(root.transform, targetFootprint, hexTopY);
            root.transform.rotation = Quaternion.Euler(0f, yawOffsetDeg, 0f);
            TintRenderers(root, playerColor, roofDarken: 0.62f);
            FinishPiece(root, hexTopY);
            return root;
        }

        private static void FinishPiece(GameObject root, float hexTopY)
        {
            StripColliders(root);
            DisableRealtimeShadows(root);
        }

        private static void DisableRealtimeShadows(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Transform FindPart(Transform root, string partName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root)
                    continue;
                if (t.name.StartsWith(partName, System.StringComparison.Ordinal)
                    || t.name.Contains(partName, System.StringComparison.Ordinal))
                    return t;
            }

            return null;
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
            float heightScale = Mathf.Min(scaleX, scaleZ) * 0.5f;
            model.localScale = new Vector3(scaleX, heightScale, scaleZ);

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
            body.SetFloat("_Glossiness", 0.04f);
            var roofLit = BuiltInMaterials.Create(playerColor);
            roofLit.SetFloat("_Glossiness", 0.03f);
            var roofShade = BuiltInMaterials.Create(
                new Color(playerColor.r * 0.65f, playerColor.g * 0.65f, playerColor.b * 0.65f, 1f));
            roofShade.SetFloat("_Glossiness", 0.02f);
            var roofRidge = BuiltInMaterials.Create(
                new Color(playerColor.r * roofDarken * 0.72f, playerColor.g * roofDarken * 0.72f, playerColor.b * roofDarken * 0.72f, 1f));
            roofRidge.SetFloat("_Glossiness", 0.01f);
            var upper = BuiltInMaterials.Create(
                new Color(playerColor.r * 0.92f, playerColor.g * 0.92f, playerColor.b * 0.92f, 1f));
            upper.SetFloat("_Glossiness", 0.04f);

            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                string name = renderer.gameObject.name;
                if (name.Contains("Ridge", System.StringComparison.Ordinal))
                    renderer.sharedMaterial = roofRidge;
                else if (name.Contains("RoofLeft", System.StringComparison.Ordinal)
                         || name.Contains("TowerRoofLeft", System.StringComparison.Ordinal))
                    renderer.sharedMaterial = roofLit;
                else if (name.Contains("RoofRight", System.StringComparison.Ordinal)
                         || name.Contains("TowerRoofRight", System.StringComparison.Ordinal))
                    renderer.sharedMaterial = roofShade;
                else if (name.Contains(PlayerPieceVisuals.UpperStoreyPartName, System.StringComparison.Ordinal))
                    renderer.sharedMaterial = upper;
                else if (name.Contains("Roof", System.StringComparison.Ordinal)
                         || name.Contains("Tower", System.StringComparison.Ordinal))
                    renderer.sharedMaterial = roofShade;
                else
                    renderer.sharedMaterial = body;
            }
        }

        private static void StripColliders(GameObject model)
        {
            foreach (var collider in model.GetComponentsInChildren<Collider>())
                ColonistSceneDestroy.SafeDestroy(collider);
        }
    }
}
