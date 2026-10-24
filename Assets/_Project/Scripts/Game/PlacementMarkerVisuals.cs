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
            // Pieces are single meshes with Wall / Roof / Edge slots: keep the roof and bevel
            // shading in the ghost so the hover silhouette still reads as a house.
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                var ghosts = new Material[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    string kind = PlayerPieceVisuals.SlotKind(slots[i] != null ? slots[i].name : null);
                    var tint = kind == PlayerPieceVisuals.RoofMaterialName
                        ? PlayerPieceVisuals.DarkenForRoof(ghostColor)
                        : kind == PlayerPieceVisuals.EdgeMaterialName
                            ? PlayerPieceVisuals.DarkenForEdge(ghostColor)
                            : ghostColor;
                    ghosts[i] = BuiltInMaterials.CreateGhost(tint);
                    ghosts[i].name = kind;
                }

                renderer.sharedMaterials = ghosts;
            }
        }
    }
}
