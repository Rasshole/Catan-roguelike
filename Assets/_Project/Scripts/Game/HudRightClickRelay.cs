using UnityEngine;
using UnityEngine.EventSystems;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v138: right-click on a HUD graphic (the trade board's resource icons remove one card on right-click while a
    /// left-click adds one through the usual <see cref="UnityEngine.UI.Button"/>).
    /// </summary>
    public sealed class HudRightClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public System.Action OnRightClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
                OnRightClick?.Invoke();
        }

        /// <summary>Tests: same path as a right mouse click.</summary>
        public void RightClickForTests() => OnRightClick?.Invoke();
    }
}
