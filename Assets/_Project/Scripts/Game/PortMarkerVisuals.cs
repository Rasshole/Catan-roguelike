using CatanRoguelike.Core;
using CatanRoguelike.Core.Hex;
using UnityEngine;
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

            CreatePart("PierPlank", root.transform,
                new Vector3(0f, PierHeight * 0.5f, PierLength * 0.5f),
                new Vector3(PierWidth, PierHeight, PierLength),
                PierWoodColor);

            CreatePart("PierPost", root.transform,
                new Vector3(0f, PostHeight * 0.5f, PierLength + PostWidth * 0.35f),
                new Vector3(PostWidth, PostHeight, PostWidth),
                isGeneric ? GenericPostColor : resourceColor);

            if (!isGeneric)
            {
                CreatePart("ResourceFlag", root.transform,
                    new Vector3(0f, PostHeight + FlagHeight * 0.5f, PierLength + PostWidth * 0.35f),
                    new Vector3(FlagWidth, FlagHeight, FlagWidth * 0.5f),
                    resourceColor);
            }

            CreatePortRateBadge(
                root.transform,
                isGeneric,
                specificResource,
                PostHeight + 0.02f,
                PierLength + PostWidth * 0.35f);

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
            disc.transform.localScale = new Vector3(0.16f, 0.01f, 0.16f);
            disc.GetComponent<Renderer>().sharedMaterial = BuiltInMaterials.CreateUnlit(new Color(0.95f, 0.95f, 0.93f, 1f));
            Object.Destroy(disc.GetComponent<Collider>());

            var labelGo = new GameObject("RateLabel");
            labelGo.transform.SetParent(badgeRoot.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.06f;
            var text = labelGo.AddComponent<TextMesh>();
            text.text = isGeneric ? "3:1" : "2:1";
            text.fontSize = 48;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.12f, 0.14f, 0.18f, 1f);
            text.characterSize = 0.12f;

            if (!isGeneric && specificResource.HasValue)
            {
                var iconGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                iconGo.name = "RateIcon";
                iconGo.transform.SetParent(badgeRoot.transform, false);
                iconGo.transform.localPosition = new Vector3(0.05f, 0.021f, 0f);
                iconGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                iconGo.transform.localScale = Vector3.one * 0.05f;
                var renderer = iconGo.GetComponent<Renderer>();
                renderer.sharedMaterial = BuiltInMaterials.CreateUnlitTransparent(
                    ColonistResourceIconTextures.Get(specificResource.Value));
                Object.Destroy(iconGo.GetComponent<Collider>());
            }
        }

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

            Object.DestroyImmediate(part.GetComponent<Collider>());
        }
    }
}
