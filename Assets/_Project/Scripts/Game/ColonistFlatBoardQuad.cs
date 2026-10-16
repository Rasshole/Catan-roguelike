using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>Unlit transparent quads lying in the board XZ plane (world rotation 90,0,0).</summary>
    public static class ColonistFlatBoardQuad
    {
        public const float WorldPitchDegrees = 90f;

        public static GameObject Create(
            Transform parent,
            string name,
            Vector3 localPosition,
            float worldWidth,
            float worldHeight,
            Texture2D texture,
            int renderQueue)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            ApplyWorldFacing(quad.transform);
            quad.transform.localScale = new Vector3(worldWidth, worldHeight, 1f);

            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlitTransparent(texture);
            renderer.sharedMaterial.renderQueue = renderQueue;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var collider = quad.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            return quad;
        }

        public static void ApplyWorldFacing(Transform quadTransform)
        {
            if (quadTransform == null)
                return;
            quadTransform.rotation = Quaternion.Euler(WorldPitchDegrees, 0f, 0f);
        }

        /// <summary>World direction corresponding to texture +V on a board-flat quad.</summary>
        public static Vector3 GetTextureVWorldAxis(Transform quadTransform) =>
            quadTransform != null
                ? quadTransform.TransformDirection(Vector3.up).normalized
                : Vector3.zero;
    }
}
