using UnityEngine;
using UnityEngine.Rendering;

namespace CatanRoguelike.Game
{
    /// <summary>Unlit transparent quads on the colonist board, aligned to the table camera.</summary>
    public static class ColonistFlatBoardQuad
    {
        public const float DefaultClearanceAboveReference = 0.01f;
        public const float BoardPitchDegrees = 90f;
        private const float DefaultMeshExtent = 1f;

        public enum AlignmentMode
        {
            /// <summary>Flat on board XZ without camera yaw (hex resource icons).</summary>
            BoardPitchOnly,
            /// <summary>Readable text/icons under the yawed colonist table camera.</summary>
            ColonistCamera
        }

        public static GameObject Create(
            Transform parent,
            string name,
            Vector3 localPosition,
            float worldWidth,
            float worldHeight,
            Texture2D texture,
            int renderQueue,
            Renderer placeAbove = null,
            float clearanceAbove = DefaultClearanceAboveReference,
            bool zWrite = false,
            AlignmentMode alignment = AlignmentMode.ColonistCamera)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            ApplyAlignment(quad.transform, alignment);
            ApplyWorldSize(quad.transform, parent, worldWidth, worldHeight);

            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = BuiltInMaterials.CreateUnlitTransparent(texture, zWrite);
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

        public static void ApplyAlignment(Transform quadTransform, AlignmentMode alignment, Camera camera = null)
        {
            if (quadTransform == null)
                return;

            quadTransform.rotation = alignment == AlignmentMode.BoardPitchOnly
                ? Quaternion.Euler(BoardPitchDegrees, 0f, 0f)
                : NumberTokenLabelOrientation.ComputeColonistTopDownRotation(camera);
        }

        public static void AlignToColonistCamera(Transform quadTransform, Camera camera = null) =>
            ApplyAlignment(quadTransform, AlignmentMode.ColonistCamera, camera);

        public static Vector3 ProjectOnBoardXZ(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-8f)
                return Vector3.forward;
            return direction.normalized;
        }

        public static Vector3 GetColonistCameraRightOnBoard(Camera camera = null)
        {
            camera ??= Camera.main;
            if (camera == null)
                return Vector3.right;
            return ProjectOnBoardXZ(camera.transform.right);
        }

        public static Vector3 GetColonistCameraUpOnBoard(Camera camera = null)
        {
            camera ??= Camera.main;
            if (camera == null)
                return Vector3.forward;
            return ProjectOnBoardXZ(camera.transform.up);
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

        public static float MeasureWorldWidthAlongTextureU(Transform quadTransform) =>
            quadTransform != null
                ? quadTransform.lossyScale.x * DefaultMeshExtent
                : 0f;

        public static void PlaceWorldYAbove(Transform quadTransform, Renderer referenceBelow, float clearance)
        {
            if (quadTransform == null || referenceBelow == null)
                return;

            float targetY = referenceBelow.bounds.max.y + clearance;
            Vector3 world = quadTransform.position;
            quadTransform.position = new Vector3(world.x, targetY, world.z);
        }

        public static void PlaceWorldYAboveAll(Transform quadTransform, Transform root, float clearance)
        {
            if (quadTransform == null || root == null)
                return;

            float maxY = float.NegativeInfinity;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer.transform == quadTransform)
                    continue;
                maxY = Mathf.Max(maxY, renderer.bounds.max.y);
            }

            if (maxY > float.NegativeInfinity)
            {
                Vector3 world = quadTransform.position;
                quadTransform.position = new Vector3(world.x, maxY + clearance, world.z);
            }
        }

        /// <summary>Visible board normal for a flat quad (faces +Y).</summary>
        public static Vector3 GetBoardFaceNormal(Transform quadTransform) =>
            quadTransform != null ? (-quadTransform.forward).normalized : Vector3.zero;

        public static Vector3 GetTextureUWorldAxis(Transform quadTransform) =>
            quadTransform != null
                ? quadTransform.TransformDirection(Vector3.right).normalized
                : Vector3.zero;

        public static Vector3 GetTextureVWorldAxis(Transform quadTransform) =>
            quadTransform != null
                ? quadTransform.TransformDirection(Vector3.up).normalized
                : Vector3.zero;

        /// <summary>Texture +V projected onto the board XZ plane (for camera-up alignment tests).</summary>
        public static Vector3 GetTextureVOnBoard(Transform quadTransform) =>
            ProjectOnBoardXZ(GetTextureVWorldAxis(quadTransform));

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
