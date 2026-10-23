using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatanRoguelike.Game
{
    /// <summary>Shows a one-line cost tooltip while hovering a build button.</summary>
    public sealed class RunHudCostTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private static RunHudCostTooltip _active;
        private static Text _sharedLabel;
        private static RectTransform _sharedPanel;

        private string _tooltipText;

        public static void EnsureShared(Transform canvasRoot)
        {
            if (_sharedPanel != null)
                return;

            var panel = RunHudUiFactory.CreatePanel(
                canvasRoot,
                "BuildCostTooltip",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-160f, 120f),
                new Vector2(160f, 156f));
            panel.gameObject.SetActive(false);
            _sharedPanel = panel;
            _sharedLabel = RunHudUiFactory.CreateText(
                panel,
                "Text",
                string.Empty,
                14,
                TextAnchor.MiddleCenter,
                ColonistPresentation.IsActive ? ColonistUiKit.Ink : RunHudUiFactory.TextPrimary);
        }

        public void SetTooltip(string text) => _tooltipText = text ?? string.Empty;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(_tooltipText) || _sharedPanel == null)
                return;

            _active = this;
            _sharedLabel.text = _tooltipText;
            _sharedPanel.gameObject.SetActive(true);

            if (eventData != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _sharedPanel.parent as RectTransform,
                    eventData.position,
                    null,
                    out var local))
            {
                local.y += 56f;
                _sharedPanel.anchoredPosition = local;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_active != this || _sharedPanel == null)
                return;

            _active = null;
            _sharedPanel.gameObject.SetActive(false);
        }
    }
}
