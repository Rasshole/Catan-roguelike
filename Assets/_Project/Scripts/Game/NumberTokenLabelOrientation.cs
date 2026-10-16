using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// Keeps a flat TextMesh number token readable from the table camera by yawing glyph-up
    /// toward the camera's XZ position while preserving a 90° pitch (lying on the chip).
    /// </summary>
    public static class NumberTokenLabelOrientation
    {
        public const float FlatPitchDegrees = 90f;
        private const float MinPlanarDistanceSq = 1e-6f;
        private const float MinReadableAlignment = 0.99f;

        /// <summary>Upright digits on a flat colonist.io top-down board (chip lies in XZ).</summary>
        public static Quaternion ComputeColonistTopDownRotation() =>
            Quaternion.Euler(FlatPitchDegrees, 0f, 0f);

        public static Quaternion ComputeWorldRotation(Vector3 labelWorldPosition, Vector3 cameraWorldPosition)
        {
            if (ColonistPresentation.IsActive)
                return ComputeColonistTopDownRotation();

            var toCamera = cameraWorldPosition - labelWorldPosition;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < MinPlanarDistanceSq)
                return Quaternion.Euler(FlatPitchDegrees, 180f, 0f);

            float yaw = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
            // TextMesh glyphs read correctly when yaw faces the camera from the chip's top side.
            return Quaternion.Euler(FlatPitchDegrees, yaw + 180f, 0f);
        }

        /// <summary>Best planar alignment between label axes and the camera (0–1).</summary>
        public static float GetReadableAlignmentScore(
            Quaternion rotation,
            Vector3 labelWorldPosition,
            Vector3 cameraWorldPosition)
        {
            var toCamera = cameraWorldPosition - labelWorldPosition;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < MinPlanarDistanceSq)
                return 1f;
            toCamera.Normalize();

            float best = 0f;
            best = Mathf.Max(best, PlanarDot(rotation * Vector3.forward, toCamera));
            best = Mathf.Max(best, PlanarDot(rotation * Vector3.back, toCamera));
            best = Mathf.Max(best, PlanarDot(rotation * Vector3.up, toCamera));
            best = Mathf.Max(best, PlanarDot(rotation * Vector3.down, toCamera));
            best = Mathf.Max(best, PlanarDot(rotation * Vector3.right, toCamera));
            best = Mathf.Max(best, PlanarDot(rotation * Vector3.left, toCamera));
            return best;
        }

        public static bool IsReadableFromCamera(
            Quaternion rotation,
            Vector3 labelWorldPosition,
            Vector3 cameraWorldPosition) =>
            GetReadableAlignmentScore(rotation, labelWorldPosition, cameraWorldPosition) >= MinReadableAlignment;

        public static void RefreshAllLabelsForCamera(Camera camera)
        {
            if (camera == null)
                return;

            var facings = Object.FindObjectsByType<FlatTokenLabelFacing>(FindObjectsSortMode.None);
            for (int i = 0; i < facings.Length; i++)
                facings[i].ApplyFacing(camera);
        }

        private static float PlanarDot(Vector3 axis, Vector3 planarToCamera)
        {
            axis.y = 0f;
            if (axis.sqrMagnitude < MinPlanarDistanceSq)
                return 0f;
            return Mathf.Abs(Vector3.Dot(axis.normalized, planarToCamera));
        }
    }

    public sealed class FlatTokenLabelFacing : MonoBehaviour
    {
        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
                return;
            ApplyFacing(camera);
        }

        public void ApplyFacing(Camera camera)
        {
            if (camera == null)
                return;

            transform.rotation = NumberTokenLabelOrientation.ComputeWorldRotation(
                transform.position,
                camera.transform.position);
        }
    }
}
