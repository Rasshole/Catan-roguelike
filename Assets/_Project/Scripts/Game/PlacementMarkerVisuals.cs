using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Translucent hover silhouettes for valid settlement, city, and road placement.</summary>
    public static class PlacementMarkerVisuals
    {
        public const string SettlementGhostName = "SettlementGhost";
        public const string CityGhostName = "CityGhost";
        public const string RoadGhostName = "RoadGhost";

        public static GameObject CreateSettlementGhost(
            Transform parent,
            Vector3 worldPosition,
            Color ghostColor,
            float hexTopY)
        {
            var root = PlayerPieceVisuals.CreateSettlement(parent, worldPosition, ghostColor, hexTopY);
            root.name = SettlementGhostName;
            ApplyGhostMaterials(root, ghostColor);
            return root;
        }

        public static GameObject CreateCityGhost(
            Transform parent,
            Vector3 worldPosition,
            Color ghostColor,
            float hexTopY)
        {
            var root = PlayerPieceVisuals.CreateCity(parent, worldPosition, ghostColor, hexTopY);
            root.name = CityGhostName;
            ApplyGhostMaterials(root, ghostColor);
            return root;
        }

        public static GameObject CreateRoadGhost(
            Transform parent,
            Vector3 midpoint,
            float angleY,
            float length,
            Color ghostColor,
            float hexTopY)
        {
            var root = PlayerPieceVisuals.CreateRoad(parent, midpoint, angleY, length, ghostColor, hexTopY);
            root.name = RoadGhostName;
            ApplyGhostMaterials(root, ghostColor);
            return root;
        }

        private static void ApplyGhostMaterials(GameObject root, Color ghostColor)
        {
            var bodyMaterial = BuiltInMaterials.CreateGhost(ghostColor);
            var roofMaterial = BuiltInMaterials.CreateGhost(PlayerPieceVisuals.DarkenForRoof(ghostColor));

            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                bool isRoof = renderer.gameObject.name.Contains("Roof");
                renderer.sharedMaterial = isRoof ? roofMaterial : bodyMaterial;
            }
        }
    }
}
