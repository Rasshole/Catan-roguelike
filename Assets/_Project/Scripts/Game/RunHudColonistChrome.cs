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
        private Text _rollPromptText;
        private Image _rollPromptGlow;

        private void BuildColonistChrome(Transform canvasRoot)
        {
            var logPanel = RunHudUiFactory.CreatePanel(
                canvasRoot,
                "GameLog",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-320f, -220f),
                new Vector2(-12f, -12f));
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
                new Vector2(-320f, -250f),
                new Vector2(-12f, -222f));
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
                new Vector2(-320f, -340f),
                new Vector2(-12f, -252f));
            _opponentPanelText = RunHudUiFactory.CreateText(
                oppPanel,
                "Opp",
                string.Empty,
                13,
                TextAnchor.UpperLeft,
                RunHudUiFactory.TextPrimary);

            var promptGo = new GameObject("RollPrompt");
            promptGo.transform.SetParent(canvasRoot, false);
            var rect = promptGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(360f, 48f);
            rect.anchoredPosition = new Vector2(120f, 40f);
            _rollPromptGlow = promptGo.AddComponent<Image>();
            _rollPromptGlow.color = new Color(1f, 0.85f, 0.2f, 0f);
            _rollPromptText = RunHudUiFactory.CreateText(
                promptGo.transform,
                "Prompt",
                "Tryk Rul (Space)",
                18,
                TextAnchor.MiddleCenter,
                RunHudUiFactory.TextPrimary,
                FontStyle.Bold);
        }

        public void SetRollPromptActive(bool active)
        {
            if (_rollPromptText != null)
                _rollPromptText.gameObject.SetActive(active);
            if (_rollPromptGlow != null)
                _rollPromptGlow.color = active
                    ? new Color(1f, 0.85f, 0.2f, 0.35f)
                    : new Color(1f, 0.85f, 0.2f, 0f);
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
                _bankStripText.text =
                    $"Bank  T{bank.Wood} M{bank.Brick} K{bank.Wheat} U{bank.Sheep} S{bank.Stone}";
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

        public void PrepareCanvasForCameraCapture(Camera camera)
        {
            if (_canvas == null || camera == null)
                return;
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = camera;
            _canvas.planeDistance = 1.2f;
            Canvas.ForceUpdateCanvases();
        }
    }
}
