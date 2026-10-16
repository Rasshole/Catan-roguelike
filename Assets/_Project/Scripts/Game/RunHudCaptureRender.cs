using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        /// <summary>Temporary pipeline check; remove when HUD composites reliably.</summary>
        internal const bool HudCaptureSanityProbeEnabled = true;

        internal const float SanityProbeScreenX = 0.88f;
        internal const float SanityProbeScreenY = 0.88f;

        public void LogCanvasWorldBoundsVsUiCamera(Camera uiCamera)
        {
            if (_canvas == null || uiCamera == null)
                return;

            var rect = _canvas.GetComponent<RectTransform>();
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            float halfHeight = uiCamera.orthographicSize;
            float halfWidth = halfHeight * uiCamera.aspect;
            var camPos = uiCamera.transform.position;
            var right = uiCamera.transform.right;
            var up = uiCamera.transform.up;
            var forward = uiCamera.transform.forward;

            Debug.Log(
                "HudCaptureCanvasBounds: " +
                $"corners[bl]={corners[0]} [tl]={corners[1]} [tr]={corners[2]} [br]={corners[3]} " +
                $"orthoHalfW={halfWidth:F4} orthoHalfH={halfHeight:F4} " +
                $"camPos={camPos} camForward={forward} " +
                $"canvasLayer={_canvas.gameObject.layer} renderMode={_canvas.renderMode}");
        }

        internal void RenderHudCaptureUiCamera(CanvasCaptureRestore restore, RenderTexture target)
        {
            if (!restore.Active || restore.UiCamera == null || target == null)
                return;

            var uiCamera = restore.UiCamera;
            uiCamera.enabled = true;
            uiCamera.targetTexture = target;
            uiCamera.pixelRect = new Rect(0f, 0f, target.width, target.height);

            Graphics.SetRenderTarget(target);
            RenderTexture.active = target;
            LogUiCaptureCamera(uiCamera);
            LogCanvasWorldBoundsVsUiCamera(uiCamera);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_canvas.GetComponent<RectTransform>());
            Canvas.ForceUpdateCanvases();
            uiCamera.Render();
        }

        internal static bool TrySampleRenderTargetPixel(
            RenderTexture target,
            int x,
            int y,
            out Color color)
        {
            color = default;
            if (target == null)
                return false;

            RenderTexture.active = target;
            var scratch = new Texture2D(1, 1, TextureFormat.RGB24, false);
            try
            {
                scratch.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
                scratch.Apply();
                color = scratch.GetPixel(0, 0);
                return true;
            }
            finally
            {
                if (Application.isPlaying)
                    Object.Destroy(scratch);
                else
                    Object.DestroyImmediate(scratch);
            }
        }

        internal static bool ColorLooksMagenta(Color c) =>
            c.r > 0.75f && c.g < 0.35f && c.b > 0.75f;

        internal GameObject CreateHudCaptureSanityProbe(Transform uiCameraRoot, int captureWidth, int captureHeight)
        {
            if (!HudCaptureSanityProbeEnabled || uiCameraRoot == null)
                return null;

            var probeGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            probeGo.name = "HudCaptureSanityProbe";
            probeGo.layer = HudCaptureUiLayer;
            Object.Destroy(probeGo.GetComponent<Collider>());

            var renderer = probeGo.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = Color.magenta;
                renderer.sharedMaterial = mat;
            }

            float halfHeight = captureHeight * WorldCaptureCanvasUnitScale * 0.5f;
            float halfWidth = halfHeight * (captureWidth / (float)captureHeight);
            float probeWorldSize = Mathf.Min(halfWidth, halfHeight) * 0.18f;

            probeGo.transform.SetParent(uiCameraRoot, false);
            probeGo.transform.localPosition = new Vector3(
                halfWidth * 0.72f,
                halfHeight * 0.72f,
                WorldCaptureCanvasDistance - 0.02f);
            probeGo.transform.localRotation = Quaternion.identity;
            probeGo.transform.localScale = new Vector3(probeWorldSize, probeWorldSize, 1f);
            return probeGo;
        }

        internal static void DestroyCaptureObject(GameObject go)
        {
            if (go == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
    }
}
