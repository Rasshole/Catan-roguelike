using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Translucent hover silhouettes for valid settlement, city, and road placement.</summary>
    public static class PlacementMarkerVisuals
    {
        public const string SettlementGhostName = "SettlementGhost";
        public const string CityGhostName = "CityGhost";
        public const string RoadGhostName = "RoadGhost";
        public const string SpotCircleName = "SpotCircle";
        public const string RoadDashName = "RoadDash";

        // v143: one shared mesh/material cache per colour so a board full of spots stays cheap.
        private static readonly System.Collections.Generic.Dictionary<Color, Material> SpotMats = new();

        private static Material SpotMaterial(Color c)
        {
            if (!SpotMats.TryGetValue(c, out var m) || m == null)
            {
                m = BuiltInMaterials.CreateUnlit(c);
                SpotMats[c] = m;
            }
            return m;
        }

        /// <summary>
        /// v143: a small flat circle (dark rim, seat-colour fill, light core) marking a valid building spot. Replaces
        /// the ghost houses that cluttered setup. Pulses gently via <see cref="PlacementSpotPulse"/>.
        /// </summary>
        public static GameObject CreateSpotCircle(Transform parent, Vector3 worldPosition, Color seatColor,
            float hexTopY, float hexScale = ColonistBoardVisualScale.ReferenceHexScale, float sizeMul = 1f, bool pulse = true)
        {
            var root = new GameObject(SpotCircleName);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(worldPosition.x, hexTopY + 0.035f, worldPosition.z);
            float r = PlayerPieceVisuals.FlatHexWidth(hexScale) * 0.085f * sizeMul;
            seatColor.a = 1f;
            AddDisc(root.transform, "Rim", r * 1.3f, 0f, new Color(0.10f, 0.07f, 0.04f, 1f));
            AddDisc(root.transform, "Fill", r, 0.004f, seatColor);
            AddDisc(root.transform, "Core", r * 0.38f, 0.008f, Color.Lerp(seatColor, Color.white, 0.75f));
            if (pulse)
                root.AddComponent<PlacementSpotPulse>();
            return root;
        }

        /// <summary>v143: a short flat dash along an edge marking a valid road (matches the spot circles).</summary>
        public static GameObject CreateRoadDash(Transform parent, Vector3 midpoint, float angleY, float length,
            Color seatColor, float hexTopY, float hexScale = ColonistBoardVisualScale.ReferenceHexScale,
            float sizeMul = 1f, bool pulse = true)
        {
            var root = new GameObject(RoadDashName);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(midpoint.x, hexTopY + 0.03f, midpoint.z);
            root.transform.rotation = Quaternion.Euler(0f, angleY, 0f);
            float w = PlayerPieceVisuals.FlatHexWidth(hexScale) * 0.06f * sizeMul;
            float len = length * 0.42f * Mathf.Max(0.8f, sizeMul);
            seatColor.a = 1f;
            AddBar(root.transform, "Rim", w * 1.6f, len + w * 0.6f, 0f, new Color(0.10f, 0.07f, 0.04f, 1f));
            AddBar(root.transform, "Fill", w, len, 0.004f, seatColor);
            AddDisc(root.transform, "Core", w * 0.55f, 0.008f, Color.Lerp(seatColor, Color.white, 0.75f));
            if (pulse)
                root.AddComponent<PlacementSpotPulse>();
            return root;
        }

        private static void AddDisc(Transform parent, string name, float radius, float lift, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, lift, 0f);
            go.transform.localScale = new Vector3(radius * 2f, 0.002f, radius * 2f);
            go.AddComponent<MeshFilter>().sharedMesh = ThinDiskMesh.Shared;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SpotMaterial(color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static void AddBar(Transform parent, string name, float width, float length, float lift, Color color)
        {
            // Two discs + a box make a rounded bar (pill) along local Z.
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            var col = box.GetComponent<Collider>();
            if (col != null)
                ColonistSceneDestroy.SafeDestroy(col);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = new Vector3(0f, lift, 0f);
            box.transform.localScale = new Vector3(width, 0.004f, Mathf.Max(0.01f, length - width));
            var mr = box.GetComponent<MeshRenderer>();
            mr.sharedMaterial = SpotMaterial(color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            float half = Mathf.Max(0.005f, (length - width) * 0.5f);
            foreach (float z in new[] { -half, half })
            {
                var cap = new GameObject(name + "Cap");
                cap.transform.SetParent(parent, false);
                cap.transform.localPosition = new Vector3(0f, lift, z);
                cap.transform.localScale = new Vector3(width, 0.002f, width);
                cap.AddComponent<MeshFilter>().sharedMesh = ThinDiskMesh.Shared;
                var cr = cap.AddComponent<MeshRenderer>();
                cr.sharedMaterial = SpotMaterial(color);
                cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cr.receiveShadows = false;
            }
        }

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
