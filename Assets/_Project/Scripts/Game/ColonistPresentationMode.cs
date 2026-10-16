using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// When enabled on a scene (e.g. ColonistStylePrototype), drives classic colonist.io presentation:
    /// top-down board, English HUD, roguelike chrome hidden.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class ColonistPresentationMode : MonoBehaviour
    {
        [SerializeField] private bool enableClassicPresentation = true;

        private void Awake() => ColonistPresentation.SetActive(enableClassicPresentation);

        private void OnDestroy() => ColonistPresentation.SetActive(false);
    }
}
