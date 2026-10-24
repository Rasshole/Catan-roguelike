using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        public const int HudCaptureUiLayer = 5;

        public int CountHudGraphics()
        {
            if (_canvas == null)
                return 0;
            var graphics = _canvas.GetComponentsInChildren<Graphic>(true);
            return graphics != null ? graphics.Length : 0;
        }

        public bool IsHudCaptureReady(int minGraphics = 8) =>
            _canvas != null
            && _canvas.isActiveAndEnabled
            && _canvas.gameObject.activeInHierarchy
            && CountHudGraphics() >= minGraphics;

        public Canvas HudCanvas => _canvas;

        public struct HudCaptureDiagnostics
        {
            public bool HasCanvas;
            public bool CanvasActiveSelf;
            public bool CanvasActiveInHierarchy;
            public bool CanvasEnabled;
            public RenderMode CanvasRenderMode;
            public int CanvasSortingOrder;
            public float CanvasPlaneDistance;
            public Camera CanvasWorldCamera;
            public float CanvasGroupAlpha;
            public int GraphicCount;
            public int MainCameraCullingMask;
            public bool MainCameraRendersUiLayer;
            public float MainNearClip;
            public float MainFarClip;
            public RenderTexture MainTargetTexture;

            public static HudCaptureDiagnostics Gather(RunHudController hud, Camera mainCamera)
            {
                var d = new HudCaptureDiagnostics();
                if (hud == null || hud._canvas == null)
                    return d;

                var canvas = hud._canvas;
                d.HasCanvas = true;
                d.CanvasActiveSelf = canvas.gameObject.activeSelf;
                d.CanvasActiveInHierarchy = canvas.gameObject.activeInHierarchy;
                d.CanvasEnabled = canvas.enabled;
                d.CanvasRenderMode = canvas.renderMode;
                d.CanvasSortingOrder = canvas.sortingOrder;
                d.CanvasPlaneDistance = canvas.planeDistance;
                d.CanvasWorldCamera = canvas.worldCamera;
                d.GraphicCount = hud.CountHudGraphics();

                var group = canvas.GetComponent<CanvasGroup>();
                d.CanvasGroupAlpha = group != null ? group.alpha : 1f;

                if (mainCamera != null)
                {
                    d.MainCameraCullingMask = mainCamera.cullingMask;
                    d.MainCameraRendersUiLayer = (mainCamera.cullingMask & (1 << HudCaptureUiLayer)) != 0;
                    d.MainNearClip = mainCamera.nearClipPlane;
                    d.MainFarClip = mainCamera.farClipPlane;
                    d.MainTargetTexture = mainCamera.targetTexture;
                }

                return d;
            }

            public void LogToConsole(string prefix = "HudCapture")
            {
                Debug.Log(
                    $"{prefix}: canvas activeSelf={CanvasActiveSelf} activeInHierarchy={CanvasActiveInHierarchy} " +
                    $"enabled={CanvasEnabled} renderMode={CanvasRenderMode} sortingOrder={CanvasSortingOrder} " +
                    $"planeDistance={CanvasPlaneDistance:F3} worldCamera={(CanvasWorldCamera != null ? CanvasWorldCamera.name : "null")} " +
                    $"canvasGroupAlpha={CanvasGroupAlpha:F3} graphics={GraphicCount} " +
                    $"mainCullingMask=0x{MainCameraCullingMask:X8} mainRendersUiLayer={MainCameraRendersUiLayer} " +
                    $"mainNear={MainNearClip:F3} mainFar={MainFarClip:F1} mainTarget={(MainTargetTexture != null ? MainTargetTexture.name : "null")}");
            }
        }

        public HudCaptureDiagnostics GetCaptureDiagnostics(Camera mainCamera) =>
            HudCaptureDiagnostics.Gather(this, mainCamera);

        internal static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            var t = root.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }
}
