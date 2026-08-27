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

        public static Quaternion ComputeWorldRotation(Vector3 labelWorldPosition, Vector3 cameraWorldPosition)
        {
            var toCamera = cameraWorldPosition - labelWorldPosition;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < MinPlanarDistanceSq)
                return Quaternion.Euler(FlatPitchDegrees, 180f, 0f);

            float yaw = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
            return Quaternion.Euler(FlatPitchDegrees, yaw, 0f);
        }
    }

    public sealed class FlatTokenLabelFacing : MonoBehaviour
    {
        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            transform.rotation = NumberTokenLabelOrientation.ComputeWorldRotation(
                transform.position,
                camera.transform.position);
        }
    }
}
