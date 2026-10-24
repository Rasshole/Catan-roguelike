using CatanRoguelike.Core;
using CatanRoguelike.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    public sealed partial class RunHudController
    {
        private const float ToastSeconds = 3.2f;

        private RectTransform _toastPanel;
        private Text _toastText;
        private string _toastVisibleMessage;
        private float _toastHideAt;

        private void BuildToastWidget(Transform canvasRoot)
        {
            _toastPanel = RunHudUiFactory.CreatePanel(
                canvasRoot,
                "HudToast",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-220f, -72f),
                new Vector2(220f, -24f));
            if (ColonistPresentation.IsActive)
                ColonistKenneyHudArt.ApplyWoodBarImage(_toastPanel.GetComponent<Image>());
            else
                _toastPanel.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.92f);
            _toastText = RunHudUiFactory.CreateText(
                _toastPanel,
                "Message",
                string.Empty,
                16,
                TextAnchor.MiddleCenter,
                ColonistPresentation.IsActive ? ColonistUiKit.Ink : RunHudUiFactory.TextPrimary,
                FontStyle.Bold);
            _toastPanel.gameObject.SetActive(false);
        }

        private void RefreshToastWidget(GameState state)
        {
            if (_toastPanel == null)
                return;

            if (HudToastBuffer.TryConsume(state, out var incoming))
            {
                _toastVisibleMessage = incoming;
                _toastHideAt = Time.unscaledTime + ToastSeconds;
            }

            bool show = !string.IsNullOrEmpty(_toastVisibleMessage) && Time.unscaledTime < _toastHideAt;
            _toastPanel.gameObject.SetActive(show);
            if (show && _toastText != null)
                _toastText.text = _toastVisibleMessage;
            else if (!show)
                _toastVisibleMessage = null;
        }
    }
}
