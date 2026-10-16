using System.Text;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using CatanRoguelike.Core.Turn;
using CatanRoguelike.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private Text _gameLogText;
        private RectTransform _bankStripPanel;
        private Text _bankStripText;
        private Text _opponentPanelText;
        private void BuildColonistChrome(Transform canvasRoot)
        {
            var logPanel = RunHudUiFactory.CreatePanel(
                canvasRoot,
                "GameLog",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-(RunHudLayout.RightChromePanelWidth + RunHudLayout.Margin), -RunHudLayout.Margin),
                new Vector2(-RunHudLayout.Margin, -RunHudLayout.Margin));
            _gameLogText = RunHudUiFactory.CreateText(
                logPanel,
                "Log",
                string.Empty,
                13,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextMuted);

            _bankStripPanel = RunHudUiFactory.CreatePanel(
                canvasRoot,
                "BankStrip",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-(RunHudLayout.RightChromePanelWidth + RunHudLayout.Margin), -(RunHudLayout.Margin + 200f)),
                new Vector2(-RunHudLayout.Margin, -(RunHudLayout.Margin + 172f)));
            SetBankFlyTarget(_bankStripPanel);
            _bankStripText = RunHudUiFactory.CreateText(
                _bankStripPanel,
                "Bank",
                string.Empty,
                12,
                TextAnchor.MiddleLeft,
                RunHudUiFactory.TextPrimary);

            var oppPanel = RunHudUiFactory.CreatePanel(
                canvasRoot,
                "OpponentPanel",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-(RunHudLayout.RightChromePanelWidth + RunHudLayout.Margin), -(RunHudLayout.Margin + 320f)),
                new Vector2(-RunHudLayout.Margin, -(RunHudLayout.Margin + 232f)));
            _opponentPanelText = RunHudUiFactory.CreateText(
                oppPanel,
                "Opp",
                string.Empty,
                13,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextPrimary);

        }

        private void RefreshColonistChrome(GameState state)
        {
            if (_gameLogText != null)
            {
                var sb = new StringBuilder();
                int start = Mathf.Max(0, state.Log.Count - 8);
                for (int i = start; i < state.Log.Count; i++)
                    sb.AppendLine(GameLog.FormatEntry(state.Log[i]));
                _gameLogText.text = sb.ToString().TrimEnd();
            }

            if (_bankStripText != null)
            {
                var bank = state.Bank;
                _bankStripText.text = ColonistBankDisplay.FormatStrip(bank);
            }

            if (_opponentPanelText != null)
            {
                int aiRoads = state.Board.CountRoads(PlayerId.Ai);
                _opponentPanelText.text =
                    $"AI · VP {state.AiVictoryPoints}\n" +
                    $"Cards {state.AiHand.Count} · Knights {state.AiKnightsPlayed}\n" +
                    $"Roads {aiRoads}";
            }
        }

        /// <summary>World-space HUD plane scale so a {width}x{height} canvas fills the capture ortho frustum.</summary>
        public const float WorldCaptureCanvasUnitScale = 0.001f;

        public const float WorldCaptureCanvasDistance = 1f;

        public struct CanvasCaptureRestore
        {
            internal bool Active;
            internal RenderMode RenderMode;
            internal Camera WorldCamera;
            internal float PlaneDistance;
            internal bool CanvasWasActiveSelf;
            internal Camera UiCamera;
            internal GameObject UiCameraObject;
            internal Transform CanvasParent;
            internal int CanvasSiblingIndex;
            internal Vector3 CanvasLocalPosition;
            internal Quaternion CanvasLocalRotation;
            internal Vector3 CanvasLocalScale;
            internal Vector2 CanvasSizeDelta;
        }

        /// <summary>
        /// Capture-only: world-space HUD + stacked orthographic UI camera (manual Render works in batchmode).
        /// </summary>
        public CanvasCaptureRestore BeginCanvasCameraCapture(Camera mainCamera) =>
            BeginCanvasCameraCapture(mainCamera, GameSceneCapture.CaptureWidth, GameSceneCapture.CaptureHeight);

        public CanvasCaptureRestore BeginCanvasCameraCapture(Camera mainCamera, int captureWidth, int captureHeight)
        {
            var restore = new CanvasCaptureRestore();
            if (_canvas == null || mainCamera == null)
                return restore;

            restore.Active = true;
            restore.RenderMode = _canvas.renderMode;
            restore.WorldCamera = _canvas.worldCamera;
            restore.PlaneDistance = _canvas.planeDistance;
            restore.CanvasWasActiveSelf = _canvas.gameObject.activeSelf;
            if (!_canvas.gameObject.activeSelf)
                _canvas.gameObject.SetActive(true);

            var canvasRect = _canvas.GetComponent<RectTransform>();
            restore.CanvasParent = canvasRect.parent;
            restore.CanvasSiblingIndex = canvasRect.GetSiblingIndex();
            restore.CanvasLocalPosition = canvasRect.localPosition;
            restore.CanvasLocalRotation = canvasRect.localRotation;
            restore.CanvasLocalScale = canvasRect.localScale;
            restore.CanvasSizeDelta = canvasRect.sizeDelta;

            SetLayerRecursively(_canvas.gameObject, HudCaptureUiLayer);

            var uiGo = new GameObject("HudCaptureUiCamera");
            var uiCamera = uiGo.AddComponent<Camera>();
            ConfigureWorldCaptureUiCamera(uiCamera, mainCamera, captureWidth, captureHeight);

            restore.UiCamera = uiCamera;
            restore.UiCameraObject = uiGo;

            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = uiCamera;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 100;

            canvasRect.SetParent(uiGo.transform, false);
            canvasRect.localRotation = Quaternion.identity;
            // Camera sits at −Z and looks toward the canvas plane at world origin.
            canvasRect.localPosition = new Vector3(0f, 0f, WorldCaptureCanvasDistance);
            canvasRect.sizeDelta = new Vector2(captureWidth, captureHeight);
            canvasRect.localScale = Vector3.one * WorldCaptureCanvasUnitScale;

            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
            Canvas.ForceUpdateCanvases();
            return restore;
        }

        public void RestoreCanvasAfterCapture(CanvasCaptureRestore restore)
        {
            if (!restore.Active || _canvas == null)
                return;

            var canvasRect = _canvas.GetComponent<RectTransform>();
            if (restore.CanvasParent != null)
            {
                canvasRect.SetParent(restore.CanvasParent, false);
                canvasRect.SetSiblingIndex(restore.CanvasSiblingIndex);
            }

            canvasRect.localPosition = restore.CanvasLocalPosition;
            canvasRect.localRotation = restore.CanvasLocalRotation;
            canvasRect.localScale = restore.CanvasLocalScale;
            canvasRect.sizeDelta = restore.CanvasSizeDelta;

            _canvas.renderMode = restore.RenderMode;
            _canvas.worldCamera = restore.WorldCamera;
            _canvas.planeDistance = restore.PlaneDistance;

            if (!restore.CanvasWasActiveSelf)
                _canvas.gameObject.SetActive(false);

            if (restore.UiCameraObject != null)
                DestroyCaptureObject(restore.UiCameraObject);

            Canvas.ForceUpdateCanvases();
        }

        private static void ConfigureWorldCaptureUiCamera(
            Camera uiCamera,
            Camera mainCamera,
            int captureWidth,
            int captureHeight)
        {
            uiCamera.transform.SetPositionAndRotation(
                new Vector3(0f, 0f, -WorldCaptureCanvasDistance),
                Quaternion.identity);
            uiCamera.orthographic = true;
            uiCamera.orthographicSize = captureHeight * WorldCaptureCanvasUnitScale * 0.5f;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 10f;
            uiCamera.aspect = captureWidth / (float)captureHeight;
            uiCamera.depth = mainCamera.depth + 1f;
            uiCamera.cullingMask = 1 << HudCaptureUiLayer;
            uiCamera.clearFlags = CameraClearFlags.Depth;
            uiCamera.enabled = true;
            uiCamera.pixelRect = mainCamera.pixelRect;
        }

        internal static void LogUiCaptureCamera(Camera uiCamera)
        {
            if (uiCamera == null)
                return;

            Debug.Log(
                "HudCaptureUiCamera: " +
                $"ortho={uiCamera.orthographic} orthoSize={uiCamera.orthographicSize:F4} " +
                $"near={uiCamera.nearClipPlane:F3} far={uiCamera.farClipPlane:F1} " +
                $"cullingMask=0x{uiCamera.cullingMask:X8} clearFlags={uiCamera.clearFlags} " +
                $"enabled={uiCamera.enabled} " +
                $"targetTexture={(uiCamera.targetTexture != null ? $"{uiCamera.targetTexture.width}x{uiCamera.targetTexture.height}" : "null")}");
        }

        public void PrepareCanvasForCameraCapture(Camera camera) =>
            BeginCanvasCameraCapture(camera);
    }
}
