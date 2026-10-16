using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using CatanRoguelike.Core.Map;
using UnityEngine;
using UnityEngine.Rendering;
using Vertex = CatanRoguelike.Core.Hex.HexMath.Vertex;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Primitive pier/dock placeholders for board-edge port vertices.
    /// </summary>
    public static class PortMarkerVisuals
    {
        public const float BaseGapAboveHex = 0.045f;

        private static readonly Color PierWoodColor = new(0.45f, 0.32f, 0.18f);
        private static readonly Color GenericPostColor = new(0.38f, 0.28f, 0.16f);

        private const float PierLength = 0.36f;
        private const float PierWidth = 0.16f;
        private const float SeaSlotPlankWidth = 0.11f;
        private const float SeaSlotPlankHeight = 0.10f;
        private const float PierHeight = 0.06f;
        private const float PostHeight = 0.11f;
        private const float PostWidth = 0.07f;
        private const float FlagHeight = 0.1f;
        private const float FlagWidth = 0.06f;
        public const float BadgeDiscSizeScale = 1.4f;
        public static float BadgeDiscWorldRadius => ColonistPortBadgeSizing.ActiveDiscWorldRadius;
        private const float BadgeDiscHeight = 0.012f * BadgeDiscSizeScale;
        public const float BadgeLabelWidthFractionGeneric = 0.78f;
        public const float BadgeLabelWidthFractionSpecific = 0.58f;
        /// <summary>World quad edge length; reads ~40% of disc diameter on screen under the colonist camera.</summary>
        public const float BadgeRateIconDiameterFraction = 0.62f;
        private const float BadgeStackGapFraction = 0.05f;
        private static bool _loggedPortMarkerChildren;

        public static void LogChildRenderersOnce(GameObject portRoot)
        {
            if (_loggedPortMarkerChildren || portRoot == null)
                return;

            _loggedPortMarkerChildren = true;
            var sb = new StringBuilder();
            sb.AppendLine($"PortMarkerVisuals children for '{portRoot.name}':");
            foreach (var renderer in portRoot.GetComponentsInChildren<Renderer>(true))
            {
                sb.AppendLine(
                    $"  {BuildPath(renderer.transform)} bounds={renderer.bounds.size} " +
                    $"enabled={renderer.enabled} mat={renderer.sharedMaterial?.name}");
            }

            Debug.Log(sb.ToString());
        }

        private static string BuildPath(Transform t)
        {
            var parts = new System.Collections.Generic.List<string>(8);
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        public static GameObject Create(
            Transform parent,
            Vertex vertex,
            float hexScale,
            float hexTopY,
            BoardState board,
            bool isGeneric,
            ResourceType? specificResource,
            Color resourceColor)
        {
            if (ColonistPresentation.IsActive
                && board != null
                && board.TryGetSeaSlotForPort(vertex, out HexCoord seaCoord))
            {
                return CreateSeaSlotPort(
                    parent,
                    board,
                    vertex,
                    seaCoord,
                    hexScale,
                    hexTopY,
                    isGeneric,
                    specificResource,
                    resourceColor);
            }

            var (vertexX, vertexZ) = HexMath.VertexToWorldPosition(vertex, hexScale);
            var (hexX, hexZ) = HexMath.ToWorldPosition(vertex.Hex, hexScale);
            Vector3 vertexPos = new Vector3(vertexX, hexTopY + BaseGapAboveHex, vertexZ);

            Vector3 seaDir3;
            if (board != null
                && PortBadgePlacement.TryComputeBadgeCenter(
                    board,
                    vertex,
                    hexScale,
                    BadgeDiscWorldRadius,
                    ColonistPresentation.IsActive,
                    out Vector2 badgeCenter,
                    out Vector2 seaOut))
            {
                seaDir3 = new Vector3(seaOut.x, 0f, seaOut.y);
                if (seaDir3.sqrMagnitude < 1e-6f)
                    seaDir3 = new Vector3(vertexX - hexX, 0f, vertexZ - hexZ).normalized;
            }
            else
            {
                seaDir3 = new Vector3(vertexX - hexX, 0f, vertexZ - hexZ);
                if (seaDir3.sqrMagnitude < 1e-6f)
                    seaDir3 = new Vector3(vertexX, 0f, vertexZ);
                seaDir3.Normalize();
                badgeCenter = new Vector2(
                    vertexX + seaDir3.x * (BadgeDiscWorldRadius + PortBadgePlacement.SeaClearanceBeyondDisc),
                    vertexZ + seaDir3.z * (BadgeDiscWorldRadius + PortBadgePlacement.SeaClearanceBeyondDisc));
            }

            var root = new GameObject(isGeneric ? "Port_Generic" : $"Port_{specificResource}");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(vertexPos, Quaternion.LookRotation(seaDir3, Vector3.up));

            Vector3 badgeWorld = new Vector3(badgeCenter.x, vertexPos.y, badgeCenter.y);
            Vector3 badgeLocal = root.transform.InverseTransformPoint(badgeWorld);
            float badgeLocalZ = Mathf.Max(
                badgeLocal.z,
                BadgeDiscWorldRadius + PortBadgePlacement.SeaClearanceBeyondDisc);
            if (ColonistPresentation.IsActive)
            {
                float maxBadgeZ = PierLength + BadgeDiscWorldRadius + PortBadgePlacement.SeaClearanceBeyondDisc + 0.05f;
                badgeLocalZ = Mathf.Min(badgeLocalZ, maxBadgeZ);
            }

            float pierLen = ColonistPresentation.IsActive
                ? PierLength
                : Mathf.Max(PierWidth * 2f, badgeLocalZ);

            CreatePart("PierPlank", root.transform,
                new Vector3(0f, PierHeight * 0.5f, pierLen * 0.5f),
                new Vector3(PierWidth, PierHeight, pierLen),
                PierWoodColor);

            CreatePart("PierPost", root.transform,
                new Vector3(0f, PostHeight * 0.5f, badgeLocalZ - PostWidth * 0.25f),
                new Vector3(PostWidth, PostHeight, PostWidth),
                isGeneric ? GenericPostColor : resourceColor);

            CreatePortRateBadge(
                root.transform,
                isGeneric,
                specificResource,
                PostHeight + 0.02f,
                badgeLocalZ);

            LogChildRenderersOnce(root);
            return root;
        }

        private static GameObject CreateSeaSlotPort(
            Transform parent,
            BoardState board,
            Vertex vertex,
            HexCoord seaCoord,
            float hexScale,
            float hexTopY,
            bool isGeneric,
            ResourceType? specificResource,
            Color resourceColor)
        {
            float y = hexTopY + BaseGapAboveHex;
            if (!SeaFrameSlotLayout.TryGetBadgeCenter(
                    board, vertex, hexScale, BadgeDiscWorldRadius, out MapPlaneXz badgeXZ))
            {
                var (seaX, seaZ) = HexMath.ToWorldPosition(seaCoord, hexScale);
                badgeXZ = new MapPlaneXz(seaX, seaZ);
            }

            Vector3 badgeWorld = new Vector3(badgeXZ.X, y, badgeXZ.Z);
            var root = new GameObject(isGeneric ? "Port_Generic" : $"Port_{specificResource}");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(badgeWorld, Quaternion.identity);

            if (SeaFrameSlotLayout.TryGetHarborCoastEdge(
                    board, vertex, seaCoord, hexScale, out _, out MapPlaneXz coastA, out MapPlaneXz coastB))
            {
                float rim = BadgeDiscWorldRadius;
                var coastAWorld = new Vector3(coastA.X, y, coastA.Z);
                var coastBWorld = new Vector3(coastB.X, y, coastB.Z);
                CreateWorldPlank(
                    root.transform,
                    coastAWorld,
                    RimPointToward(badgeWorld, coastAWorld, rim),
                    PierWoodColor,
                    SeaSlotPlankWidth,
                    SeaSlotPlankHeight);
                CreateWorldPlank(
                    root.transform,
                    coastBWorld,
                    RimPointToward(badgeWorld, coastBWorld, rim),
                    PierWoodColor,
                    SeaSlotPlankWidth,
                    SeaSlotPlankHeight);
            }

            CreatePart("PierPost", root.transform,
                new Vector3(0f, PostHeight * 0.5f, 0f),
                new Vector3(PostWidth, PostHeight, PostWidth),
                isGeneric ? GenericPostColor : resourceColor);

            CreatePortRateBadge(
                root.transform,
                isGeneric,
                specificResource,
                PostHeight + 0.02f,
                0f);

            LogChildRenderersOnce(root);
            return root;
        }

        private static Vector3 RimPointToward(Vector3 badgeCenter, Vector3 toward, float radius)
        {
            Vector3 delta = toward - badgeCenter;
            if (delta.sqrMagnitude < 1e-8f)
                return badgeCenter;
            return badgeCenter + delta.normalized * radius;
        }

        private static void CreateWorldPlank(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Color color,
            float width,
            float height)
        {
            Vector3 delta = to - from;
            float len = delta.magnitude;
            if (len < 0.02f)
                return;

            Vector3 dir = delta.normalized;
            Vector3 mid = (from + to) * 0.5f;

            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "PierPlank";
            part.transform.SetParent(parent, true);
            part.transform.position = mid;
            part.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            part.transform.localScale = new Vector3(width, height, len);

            var mat = BuiltInMaterials.Create(color);
            mat.renderQueue = 3075;
            part.GetComponent<Renderer>().sharedMaterial = mat;
            DestroyCollider(part.GetComponent<Collider>());
        }

        private static void CreatePortRateBadge(
            Transform parent,
            bool isGeneric,
            ResourceType? specificResource,
            float localY,
            float localZ)
        {
            var badgeRoot = new GameObject("PortRateBadge");
            badgeRoot.transform.SetParent(parent, false);
            badgeRoot.transform.localPosition = new Vector3(0f, localY, localZ);

            var disc = new GameObject("BadgeDisc");
            disc.transform.SetParent(badgeRoot.transform, false);
            disc.transform.localPosition = Vector3.zero;
            float discDiameter = BadgeDiscWorldRadius * 2f;
            float discYScale = BadgeDiscHeight / ThinDiskMesh.MeshHeight;
            disc.transform.localScale = new Vector3(discDiameter, discYScale, discDiameter);
            var meshFilter = disc.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = ThinDiskMesh.Shared;
            var discRenderer = disc.AddComponent<MeshRenderer>();
            discRenderer.sharedMaterial = BuiltInMaterials.CreateUnlit(new Color(0.98f, 0.96f, 0.90f, 1f));
            discRenderer.sharedMaterial.renderQueue = 3100;
            discRenderer.shadowCastingMode = ShadowCastingMode.Off;

            float discTopLocalY = discYScale * ThinDiskMesh.TopY;
            float labelLocalY = discTopLocalY + 0.006f;
            var labelTexture = ColonistPortRateLabelTextures.Get(isGeneric);
            bool hasRateIcon = !isGeneric && specificResource.HasValue;
            float labelWorldWidth = discDiameter * (hasRateIcon
                ? BadgeLabelWidthFractionSpecific
                : BadgeLabelWidthFractionGeneric);
            float aspect = labelTexture.height / (float)labelTexture.width;
            float labelWorldHeight = labelWorldWidth * aspect;

            Vector3 baseLabelLocal = new Vector3(0f, labelLocalY, 0f);
            var labelGo = ColonistFlatBoardQuad.Create(
                badgeRoot.transform,
                "RateLabel",
                baseLabelLocal,
                labelWorldWidth,
                labelWorldHeight,
                labelTexture,
                3210,
                placeAbove: null,
                alignment: ColonistFlatBoardQuad.AlignmentMode.ColonistCamera);

            if (hasRateIcon)
            {
                float iconWorldSize = discDiameter * BadgeRateIconDiameterFraction;
                float stackGap = discDiameter * BadgeStackGapFraction;
                Vector3 upBoard = ColonistFlatBoardQuad.GetColonistCameraUpOnBoard();
                Vector3 upLocal = badgeRoot.transform.InverseTransformDirection(upBoard);
                if (upLocal.sqrMagnitude > 1e-6f)
                    upLocal.Normalize();

                Vector3 labelLocal = baseLabelLocal - upLocal * (iconWorldSize * 0.5f + stackGap * 0.5f);
                labelGo.transform.localPosition = labelLocal;
                ColonistFlatBoardQuad.ApplyWorldSize(
                    labelGo.transform,
                    badgeRoot.transform,
                    labelWorldWidth,
                    labelWorldHeight);
                ColonistFlatBoardQuad.PlaceWorldYAbove(labelGo.transform, discRenderer);

                Vector3 iconLocal = labelLocal + upLocal * (labelWorldHeight * 0.5f + stackGap + iconWorldSize * 0.5f);
                var iconGo = ColonistFlatBoardQuad.Create(
                    badgeRoot.transform,
                    "RateIcon",
                    iconLocal,
                    iconWorldSize,
                    iconWorldSize,
                    ColonistResourceIconTextures.GetForPortBadge(specificResource.Value),
                    3220,
                    placeAbove: null,
                    alignment: ColonistFlatBoardQuad.AlignmentMode.ColonistCamera);
                ColonistFlatBoardQuad.ApplyWorldSize(
                    iconGo.transform,
                    badgeRoot.transform,
                    iconWorldSize,
                    iconWorldSize);
                ColonistFlatBoardQuad.PlaceWorldYAbove(iconGo.transform, discRenderer);
            }
            else
            {
                ColonistFlatBoardQuad.PlaceWorldYAbove(labelGo.transform, discRenderer);
            }
        }

        private static void DestroyCollider(Collider collider) =>
            ColonistSceneDestroy.SafeDestroy(collider);

        private static void CreatePart(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            Quaternion localRotation = default)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(color);

            DestroyCollider(part.GetComponent<Collider>());
        }
    }
}
