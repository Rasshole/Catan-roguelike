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
        public const float BaseGapAboveHex = 0.02f;

        private static readonly Color PierWoodColor = new(0.45f, 0.32f, 0.18f);
        private static readonly Color GenericPostColor = new(0.38f, 0.28f, 0.16f);

        private const float PierLength = 0.18f;
        private const float PierWidth = 0.09f;
        private const float PierHeight = 0.035f;
        private const float PostHeight = 0.1f;
        private const float PostWidth = 0.04f;
        private const float FlagHeight = 0.05f;
        private const float FlagWidth = 0.03f;

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
                new Vector3(0f, PostHeight * 0.5f, PierLength + PostWidth * 0.5f),
                new Vector3(PostWidth, PostHeight, PostWidth),
                isGeneric ? GenericPostColor : resourceColor);

            if (!isGeneric)
            {
                CreatePart("ResourceFlag", root.transform,
                    new Vector3(0f, PostHeight + FlagHeight * 0.5f, PierLength + PostWidth * 0.5f),
                    new Vector3(FlagWidth, FlagHeight, FlagWidth * 0.5f),
                    resourceColor);
            }

            return root;
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
