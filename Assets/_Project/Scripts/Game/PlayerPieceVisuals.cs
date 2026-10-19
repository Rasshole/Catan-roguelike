using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Classic Catan wooden board pieces built from the Blender meshes in
    /// <c>Resources/Art/Models/ColonistPieces</c> (tools/blender/pieces.py): a pentagon-prism house,
    /// a city (long house joined with a tower house, ~1.5x the settlement footprint) and a bevelled
    /// plank road. Each piece is one closed mesh with three material slots: Wall (player colour),
    /// Roof (darker) and Edge (bevel faces, darkest).
    /// <para>
    /// The colonist table camera looks straight down, which would show only a roof rectangle, so in
    /// colonist mode settlements and cities lean back toward the top of the screen by
    /// <see cref="ColonistLeanDegrees"/>: the camera then sees them in 3/4 view (gable, side wall
    /// and both roof slopes), like the printed pieces on colonist.io.
    /// </para>
    /// </summary>
    public static class PlayerPieceVisuals
    {
        public const string SettlementName = "Settlement";
        public const string CityName = "City";
        public const string RoadName = "Road";
        /// <summary>Child holding the single house/city mesh.</summary>
        public const string BodyPartName = "Body";
        /// <summary>Child holding the single road plank mesh.</summary>
        public const string PlankPartName = "Plank";

        public const string WallMaterialName = "PieceWall";
        public const string RoofMaterialName = "PieceRoof";
        public const string EdgeMaterialName = "PieceEdge";

        public static readonly Color HumanColor = new(0.2f, 0.4f, 0.9f);
        public static readonly Color AiColor = new(0.9f, 0.25f, 0.2f);

        public const float RoofColorMultiply = 0.55f;
        public const float EdgeColorMultiply = 0.32f;

        /// <summary>Settlement wall width as a fraction of the flat-to-flat hex width (city is 1.5x).</summary>
        public const float SettlementFootprintOfHex = 0.27f;
        /// <summary>Road plank width as a fraction of the flat-to-flat hex width.</summary>
        public const float RoadWidthOfHex = 0.09f;
        public const float RoadLengthOfEdge = 0.80f;
        /// <summary>Lean toward screen-top so the top-down colonist camera sees a 3/4 view.</summary>
        public const float ColonistLeanDegrees = 50f;
        /// <summary>Yaw of the gable end away from facing the viewer straight on.</summary>
        public const float ColonistYawDegrees = 32f;

        /// <summary>City turn relative to the settlement so its long wall and tower face the viewer.</summary>
        public const float CityExtraYawDegrees = 90f;

        private const string ModelRoot = "Art/Models/ColonistPieces";
        private const float BaseGapAboveHex = 0.02f;
        private const float RoadGapAboveHex = 0.012f;

        public static float FlatHexWidth(float hexScale) =>
            hexScale * Mathf.Sqrt(3f) * HexMath.OuterRadius;

        public static Color ColorForPlayer(PlayerId owner) =>
            owner == PlayerId.Human ? HumanColor : AiColor;

        public static Color DarkenForRoof(Color color) => Shade(color, RoofColorMultiply);

        public static Color DarkenForEdge(Color color) => Shade(color, EdgeColorMultiply);

        private static Color Shade(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);

        public static GameObject CreateSettlement(
            Transform parent,
            Vector3 worldPosition,
            Color color,
            float hexTopY,
            float hexScale = ColonistBoardVisualScale.ReferenceHexScale) =>
            CreateBuilding(parent, SettlementName, "settlement", worldPosition, color, hexTopY, hexScale, 0f);

        public static GameObject CreateCity(
            Transform parent,
            Vector3 worldPosition,
            Color color,
            float hexTopY,
            float hexScale = ColonistBoardVisualScale.ReferenceHexScale) =>
            // Turned side-on: the long house faces the viewer with the tower rising at one end.
            CreateBuilding(parent, CityName, "city", worldPosition, color, hexTopY, hexScale, CityExtraYawDegrees);

        public static GameObject CreateRoad(
            Transform parent,
            Vector3 midpoint,
            float angleY,
            float length,
            Color color,
            float hexTopY,
            float hexScale = ColonistBoardVisualScale.ReferenceHexScale)
        {
            var root = new GameObject(RoadName);
            root.transform.SetParent(parent, false);
            root.transform.position = midpoint;
            root.transform.rotation = Quaternion.Euler(0f, angleY, 0f);

            var mesh = LoadMesh("road", out var slotNames);
            var plank = CreateMeshChild(root.transform, PlankPartName, mesh, slotNames, color);
            var b = mesh.bounds;
            // Blender plank runs along model X; the road root's local Z runs along the edge.
            plank.localRotation = Quaternion.Euler(0f, 90f, 0f);
            float targetLength = Mathf.Max(length * RoadLengthOfEdge, 0.05f);
            float targetWidth = FlatHexWidth(hexScale) * RoadWidthOfHex;
            float sx = targetLength / Mathf.Max(b.size.x, 1e-4f);
            float sz = targetWidth / Mathf.Max(b.size.z, 1e-4f);
            float sy = sz; // keep the bevel profile square-ish
            plank.localScale = new Vector3(sx, sy, sz);
            plank.localPosition = new Vector3(0f, hexTopY + RoadGapAboveHex - b.min.y * sy - midpoint.y, 0f);
            DisableShadows(root);
            return root;
        }

        private static GameObject CreateBuilding(
            Transform parent,
            string rootName,
            string modelName,
            Vector3 worldPosition,
            Color color,
            float hexTopY,
            float hexScale,
            float extraYaw)
        {
            var root = new GameObject(rootName);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;

            var mesh = LoadMesh(modelName, out var slotNames);
            var body = CreateMeshChild(root.transform, BodyPartName, mesh, slotNames, color);

            // One scale for every building (from the settlement width) keeps the city at 1.5x.
            var settlementMesh = modelName == "settlement" ? mesh : LoadMesh("settlement", out _);
            float scale = FlatHexWidth(hexScale) * SettlementFootprintOfHex
                / Mathf.Max(settlementMesh.bounds.size.x, 1e-4f);
            body.localScale = Vector3.one * scale;
            body.localRotation = ComputeBuildingRotation(extraYaw);

            // Pivot on the footprint centre, then drop the lowest (leaned) point onto the hex top.
            var c = mesh.bounds.center;
            var footprintCentre = new Vector3(c.x, mesh.bounds.min.y, c.z);
            body.localPosition = -(body.localRotation * (footprintCentre * scale));
            float minY = LowestWorldY(body, mesh);
            body.position += Vector3.up * (hexTopY + BaseGapAboveHex - minY);

            DisableShadows(root);
            return root;
        }

        /// <summary>Gable toward the viewer (yawed), leaned back toward screen-top in colonist mode.</summary>
        internal static Quaternion ComputeBuildingRotation(float extraYaw)
        {
            if (!ColonistPresentation.IsActive)
                return Quaternion.Euler(0f, extraYaw + ColonistYawDegrees, 0f);

            Vector3 up = ColonistFlatBoardQuad.GetColonistCameraUpOnBoard();
            // Model +X is the settlement gable end: face it toward the viewer (screen-bottom).
            var face = Quaternion.FromToRotation(Vector3.right, -up);
            if (Vector3.Dot(face * Vector3.up, Vector3.up) < 0.5f)
                face = Quaternion.LookRotation(Vector3.Cross(-up, Vector3.up), Vector3.up);
            var yaw = Quaternion.AngleAxis(ColonistYawDegrees + extraYaw, Vector3.up);
            var leanTarget = Vector3.up * Mathf.Cos(ColonistLeanDegrees * Mathf.Deg2Rad)
                + up * Mathf.Sin(ColonistLeanDegrees * Mathf.Deg2Rad);
            var lean = Quaternion.FromToRotation(Vector3.up, leanTarget);
            return lean * yaw * face;
        }

        private static float LowestWorldY(Transform body, Mesh mesh)
        {
            float min = float.MaxValue;
            foreach (var v in mesh.vertices)
                min = Mathf.Min(min, body.TransformPoint(v).y);
            return min;
        }

        private static Transform CreateMeshChild(
            Transform parent,
            string name,
            Mesh mesh,
            string[] slotNames,
            Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = CreateSlotMaterials(slotNames, color);
            return go.transform;
        }

        /// <summary>Wall = player colour, Roof darker, Edge (bevels) darkest, in the mesh's slot order.</summary>
        internal static Material[] CreateSlotMaterials(string[] slotNames, Color color)
        {
            var mats = new Material[slotNames.Length];
            for (int i = 0; i < slotNames.Length; i++)
            {
                var kind = SlotKind(slotNames[i]);
                var tint = kind == RoofMaterialName ? DarkenForRoof(color)
                    : kind == EdgeMaterialName ? DarkenForEdge(color)
                    : color;
                var mat = BuiltInMaterials.CreateMatteOpaque(tint);
                mat.name = kind;
                mats[i] = mat;
            }

            return mats;
        }

        internal static string SlotKind(string importedName)
        {
            if (importedName != null && importedName.Contains("Roof"))
                return RoofMaterialName;
            if (importedName != null && importedName.Contains("Edge"))
                return EdgeMaterialName;
            return WallMaterialName;
        }

        private static Mesh LoadMesh(string modelName, out string[] slotNames)
        {
            var prefab = Resources.Load<GameObject>($"{ModelRoot}/{modelName}");
            var filter = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
            var renderer = prefab != null ? prefab.GetComponentInChildren<MeshRenderer>() : null;
            if (filter == null || filter.sharedMesh == null || renderer == null)
                throw new System.InvalidOperationException(
                    $"Player piece model '{ModelRoot}/{modelName}' is missing (run tools/blender/run-pieces.sh).");

            var shared = renderer.sharedMaterials;
            slotNames = new string[shared.Length];
            for (int i = 0; i < shared.Length; i++)
                slotNames[i] = shared[i] != null ? shared[i].name : WallMaterialName;
            return filter.sharedMesh;
        }

        private static void DisableShadows(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
