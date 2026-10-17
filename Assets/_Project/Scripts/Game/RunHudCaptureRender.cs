using System;
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
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
            if (target == null)
                throw new InvalidOperationException(
                    $"{GameSceneCapture.HudCompositePipelineMarker} RenderHudCaptureUiCamera: target RT is null.");
            if (!restore.Active || restore.UiCamera == null)
            {
                throw new InvalidOperationException(
                    $"{GameSceneCapture.HudCompositePipelineMarker} RenderHudCaptureUiCamera: " +
                    $"inactive session (active={restore.Active} uiCamera={(restore.UiCamera != null)}).");
            }

            Debug.Log($"{GameSceneCapture.HudCompositePipelineMarker} RenderHudCaptureUiCamera enter");

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
            Debug.Log($"{GameSceneCapture.HudCompositePipelineMarker} RenderHudCaptureUiCamera exit");
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
                    ColonistSceneDestroy.SafeDestroy(scratch);
                else
                    Object.DestroyImmediate(scratch);
            }
        }

        internal static void DestroyCaptureObject(GameObject go)
        {
            if (go == null)
                return;

            if (Application.isPlaying)
                ColonistSceneDestroy.SafeDestroy(go);
            else
                Object.DestroyImmediate(go);
        }

        internal static void DestroyCaptureComponent(Component component)
        {
            if (component == null)
                return;

            if (Application.isPlaying)
                ColonistSceneDestroy.SafeDestroy(component);
            else
                Object.DestroyImmediate(component);
        }
    }
}
