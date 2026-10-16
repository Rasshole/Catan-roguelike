using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>Unlit transparent quads lying in the board XZ plane (world rotation 90,0,0).</summary>
    public static class ColonistFlatBoardQuad
    {
        public const float WorldPitchDegrees = 90f;
        public const float DefaultClearanceAboveReference = 0.01f;

        public static GameObject Create(
            Transform parent,
            string name,
            Vector3 localPosition,
            float worldWidth,
            float worldHeight,
            Texture2D texture,
            int renderQueue,
            Renderer placeAbove = null,
            float clearanceAbove = DefaultClearanceAboveReference)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            ApplyWorldFacing(quad.transform);
            ApplyWorldSize(quad.transform, parent, worldWidth, worldHeight);

            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlitTransparent(texture);
            renderer.sharedMaterial.renderQueue = renderQueue;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var collider = quad.GetComponent<Collider>();
            if (collider != null)
                ColonistSceneDestroy.SafeDestroy(collider);

            if (placeAbove != null)
                PlaceWorldYAbove(quad.transform, placeAbove, clearanceAbove);

            return quad;
        }

        public static void AlignTextureUToWorldPositiveX(Transform quadTransform)
        {
            if (quadTransform == null)
                return;

            ApplyWorldFacing(quadTransform);
            Vector3 textureU = GetTextureUWorldAxis(quadTransform);
            textureU.y = 0f;
            if (textureU.sqrMagnitude < 1e-6f)
                return;

            textureU.Normalize();
            float yaw = Vector3.SignedAngle(textureU, Vector3.right, Vector3.up);
            quadTransform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * quadTransform.rotation;
        }

        public static void ApplyWorldFacing(Transform quadTransform)
        {
            if (quadTransform == null)
                return;
            quadTransform.rotation = Quaternion.Euler(WorldPitchDegrees, 0f, 0f);
        }

        public static void ApplyWorldSize(
            Transform quadTransform,
            Transform parent,
            float worldWidth,
            float worldHeight)
        {
            if (quadTransform == null || parent == null)
                return;

            Vector3 lossy = parent.lossyScale;
            float sx = Mathf.Max(Mathf.Abs(lossy.x), 1e-4f);
            float sz = Mathf.Max(Mathf.Abs(lossy.z), 1e-4f);
            quadTransform.localScale = new Vector3(worldWidth / sx, worldHeight / sz, 1f);
        }

        public static void PlaceWorldYAbove(Transform quadTransform, Renderer referenceBelow, float clearance)
        {
            if (quadTransform == null || referenceBelow == null)
                return;

            float targetY = referenceBelow.bounds.max.y + clearance;
            Vector3 world = quadTransform.position;
            quadTransform.position = new Vector3(world.x, targetY, world.z);
        }

        /// <summary>Visible board normal for a flat quad (faces +Y).</summary>
        public static Vector3 GetBoardFaceNormal(Transform quadTransform) =>
            quadTransform != null ? (-quadTransform.forward).normalized : Vector3.zero;

        /// <summary>World direction corresponding to texture +U (left-to-right in glyph art).</summary>
        public static Vector3 GetTextureUWorldAxis(Transform quadTransform) =>
            quadTransform != null
                ? quadTransform.TransformDirection(Vector3.right).normalized
                : Vector3.zero;

        /// <summary>World direction corresponding to texture +V on a board-flat quad.</summary>
        public static Vector3 GetTextureVWorldAxis(Transform quadTransform) =>
            quadTransform != null
                ? quadTransform.TransformDirection(Vector3.up).normalized
                : Vector3.zero;

        public static void LogDiagnostics(string tag, Transform quadTransform, Renderer referenceBelow = null)
        {
            if (quadTransform == null)
                return;

            var renderer = quadTransform.GetComponent<Renderer>();
            var mat = renderer != null ? renderer.sharedMaterial : null;
            var tex = mat != null ? mat.mainTexture as Texture2D : null;
            int opaque = tex != null ? CountOpaquePixels(tex) : 0;
            float refTop = referenceBelow != null ? referenceBelow.bounds.max.y : float.NaN;
            Debug.Log(
                $"{tag} worldPos={quadTransform.position} lossyScale={quadTransform.lossyScale} " +
                $"faceNormal={GetBoardFaceNormal(quadTransform)} queue={mat?.renderQueue} refTopY={refTop:F4} " +
                $"enabled={renderer != null && renderer.enabled} tex={(tex != null ? $"{tex.width}x{tex.height}" : "null")} " +
                $"opaquePixels={opaque}");
        }

        public static int CountOpaquePixels(Texture2D texture)
        {
            if (texture == null)
                return 0;

            var pixels = texture.GetPixels32();
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 16)
                    count++;
            }

            return count;
        }
    }
}
