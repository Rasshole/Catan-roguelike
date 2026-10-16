using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
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
        private const float PierHeight = 0.06f;
        private const float PostHeight = 0.11f;
        private const float PostWidth = 0.07f;
        private const float FlagHeight = 0.1f;
        private const float FlagWidth = 0.06f;
        private const float BadgeDiscRadius = 0.22f;
        private const float BadgeDiscHeight = 0.012f;
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
            bool isGeneric,
            ResourceType? specificResource,
            Color resourceColor)
        {
            var (vertexX, vertexZ) = HexMath.VertexToWorldPosition(vertex, hexScale);
            var (hexX, hexZ) = HexMath.ToWorldPosition(vertex.Hex, hexScale);
            var outward = new Vector3(vertexX - hexX, 0f, vertexZ - hexZ);
            if (outward.sqrMagnitude < 1e-6f)
                outward = new Vector3(vertexX, 0f, vertexZ);
            outward.Normalize();

            var root = new GameObject(isGeneric ? "Port_Generic" : $"Port_{specificResource}");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(vertexX, hexTopY + BaseGapAboveHex, vertexZ);
            root.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);

            float coastOffset = hexScale * 0.50f;
            float badgeLocalZ = PierLength + PostWidth * 0.35f + coastOffset;
            float pierLen = Mathf.Max(PierLength, badgeLocalZ * 0.9f);

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

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "BadgeDisc";
            disc.transform.SetParent(badgeRoot.transform, false);
            disc.transform.localPosition = Vector3.zero;
            float discDiameter = BadgeDiscRadius * 2f;
            disc.transform.localScale = new Vector3(discDiameter, BadgeDiscHeight, discDiameter);
            var discRenderer = disc.GetComponent<Renderer>();
            discRenderer.sharedMaterial = BuiltInMaterials.CreateUnlit(new Color(0.95f, 0.95f, 0.93f, 1f));
            discRenderer.sharedMaterial.renderQueue = 3100;
            DestroyCollider(disc.GetComponent<Collider>());

            float labelLocalY = BadgeDiscHeight + 0.006f;
            var labelTexture = ColonistPortRateLabelTextures.Get(isGeneric);
            float labelWorldWidth = discDiameter * 0.65f;
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

            if (!isGeneric && specificResource.HasValue)
            {
                float iconWorldSize = labelWorldHeight;
                float stackGap = discDiameter * 0.06f;
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
                    ColonistResourceIconTextures.Get(specificResource.Value),
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
            Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.Create(color);

            DestroyCollider(part.GetComponent<Collider>());
        }
    }
}
