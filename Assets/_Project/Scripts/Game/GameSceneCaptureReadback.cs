using System;
using System.Security.Cryptography;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatanRoguelike.Game
{
    public static partial class GameSceneCapture
    {
        public static Texture2D ReadbackRgbFromRenderTexture(RenderTexture source, int width, int height)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var previousActive = RenderTexture.active;
            RenderTexture.active = source;
            GL.Flush();

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            texture.Apply(false, false);
            RenderTexture.active = previousActive;
            return texture;
        }

        public static void DetachCamerasFromRenderTexture(RenderTexture target, Camera mainCamera, Camera uiCamera)
        {
            if (mainCamera != null && mainCamera.targetTexture == target)
                mainCamera.targetTexture = null;
            if (uiCamera != null && uiCamera.targetTexture == target)
                uiCamera.targetTexture = null;
        }

        public static void LogCaptureTextureFingerprint(Texture2D texture, string stage)
        {
            if (texture == null)
            {
                Debug.Log($"{HudCompositePipelineMarker} fingerprint {stage}: texture=null");
                return;
            }

            int probeX = Mathf.Clamp(
                (int)(texture.width * RunHudController.SanityProbeScreenX),
                0,
                texture.width - 1);
            int probeY = Mathf.Clamp(
                (int)(texture.height * RunHudController.SanityProbeScreenY),
                0,
                texture.height - 1);
            var probePixel = texture.GetPixel(probeX, probeY);

            int bandY = Mathf.Clamp((int)(texture.height * 0.05f), 0, texture.height - 1);
            var bandPixel = texture.GetPixel(texture.width / 2, bandY);

            string md5 = ComputePngMd5Hex(texture);
            double sampleSum = SampleLuminanceSum(texture, 24);

            Debug.Log(
                $"{HudCompositePipelineMarker} fingerprint {stage}: " +
                $"id={texture.GetInstanceID()} {texture.width}x{texture.height} " +
                $"md5={md5} sampleSum={sampleSum:F2} " +
                $"probe@({probeX},{probeY})={FormatColor(probePixel)} " +
                $"band@({texture.width / 2},{bandY})={FormatColor(bandPixel)}");
        }

        public static string ComputePngMd5Hex(Texture2D texture)
        {
            if (texture == null)
                return "null";

            var png = texture.EncodeToPNG();
            return Convert.ToHexString(MD5.HashData(png)).ToLowerInvariant();
        }

        private static double SampleLuminanceSum(Texture2D texture, int step)
        {
            double sum = 0d;
            int samples = 0;
            for (int y = 0; y < texture.height; y += step)
            {
                for (int x = 0; x < texture.width; x += step)
                {
                    var c = texture.GetPixel(x, y);
                    sum += c.r + c.g + c.b;
                    samples++;
                }
            }

            return samples > 0 ? sum : 0d;
        }

        private static string FormatColor(Color c) =>
            $"({c.r:F3},{c.g:F3},{c.b:F3})";

        private static void DestroyCaptureTexture(Texture texture)
        {
            if (texture == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(texture);
            else
                Object.DestroyImmediate(texture);
        }
    }
}
