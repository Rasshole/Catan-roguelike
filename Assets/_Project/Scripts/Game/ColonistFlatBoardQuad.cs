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

        public static void PlaceWorldYAbove(
            Transform quadTransform,
            Renderer referenceBelow,
            float clearance = DefaultClearanceAboveReference)
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

        /// <summary>
        /// World axis that reads as texture +V for board icons (pitch-only art is rotated 90° in UV space).
        /// </summary>
        public static Vector3 GetEffectiveTextureVWorldAxis(Transform quadTransform, AlignmentMode alignment)
        {
            if (quadTransform == null)
                return Vector3.forward;
            return alignment == AlignmentMode.BoardPitchOnly
                ? quadTransform.TransformDirection(Vector3.right).normalized
                : GetTextureVWorldAxis(quadTransform);
        }

        public static Vector2 MeasureTextureVScreenDelta(
            Transform quadTransform,
            Camera camera,
            AlignmentMode alignment,
            float worldOffset = 0.08f)
        {
            if (quadTransform == null || camera == null)
                return Vector2.zero;

            var renderer = quadTransform.GetComponent<Renderer>();
            Vector3 center = renderer != null ? renderer.bounds.center : quadTransform.position;
            Vector3 effectiveV = GetEffectiveTextureVWorldAxis(quadTransform, alignment);
            Vector3 meshV = GetTextureVWorldAxis(quadTransform);

            Vector2 best = Vector2.zero;
            float bestUpScore = float.NegativeInfinity;
            TryPickScreenUpDelta(camera, center, effectiveV, worldOffset, ref best, ref bestUpScore);
            TryPickScreenUpDelta(camera, center, -effectiveV, worldOffset, ref best, ref bestUpScore);
            if (alignment == AlignmentMode.BoardPitchOnly)
            {
                TryPickScreenUpDelta(camera, center, meshV, worldOffset, ref best, ref bestUpScore);
                TryPickScreenUpDelta(camera, center, -meshV, worldOffset, ref best, ref bestUpScore);
            }

            return best;
        }

        /// <summary>Cosine between measured on-screen up and screen +Y (1 = upright).</summary>
        public static float MeasureTextureScreenUpAlignment(
            Transform quadTransform,
            Camera camera,
            AlignmentMode alignment,
            float worldOffset = 0.08f)
        {
            Vector2 delta = MeasureTextureVScreenDelta(quadTransform, camera, alignment, worldOffset);
            float mag = delta.magnitude;
            return mag > 0.5f ? delta.y / mag : -1f;
        }

        private static void TryPickScreenUpDelta(
            Camera camera,
            Vector3 center,
            Vector3 worldAxis,
            float worldOffset,
            ref Vector2 best,
            ref float bestUpScore)
        {
            if (worldAxis.sqrMagnitude < 1e-8f)
                return;

            Vector3 screenCenter = camera.WorldToScreenPoint(center);
            Vector3 screenTip = camera.WorldToScreenPoint(center + worldAxis.normalized * worldOffset);
            var delta = new Vector2(screenTip.x - screenCenter.x, screenTip.y - screenCenter.y);
            float mag = delta.magnitude;
            if (mag < 0.5f)
                return;

            float upScore = delta.y / mag;
            if (upScore > bestUpScore)
            {
                bestUpScore = upScore;
                best = delta;
            }
        }

        public static Vector2 MeasureScreenPixelSize(Renderer renderer, Camera camera)
        {
            if (renderer == null || camera == null)
                return Vector2.zero;

            Bounds bounds = renderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            Vector3[] offsets =
            {
                new Vector3(extents.x, extents.y, extents.z),
                new Vector3(extents.x, extents.y, -extents.z),
                new Vector3(extents.x, -extents.y, extents.z),
                new Vector3(extents.x, -extents.y, -extents.z),
                new Vector3(-extents.x, extents.y, extents.z),
                new Vector3(-extents.x, extents.y, -extents.z),
                new Vector3(-extents.x, -extents.y, extents.z),
                new Vector3(-extents.x, -extents.y, -extents.z)
            };

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            foreach (var offset in offsets)
            {
                Vector3 screen = camera.WorldToScreenPoint(center + offset);
                minX = Mathf.Min(minX, screen.x);
                maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, screen.y);
                maxY = Mathf.Max(maxY, screen.y);
            }

            return new Vector2(maxX - minX, maxY - minY);
        }

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
