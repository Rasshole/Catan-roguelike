using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Destroys a per-instance mesh (e.g. a piece's two-tone roof copy) with its object.</summary>
    public sealed class OwnedMeshCleanup : MonoBehaviour
    {
        public Mesh Owned;

        private void OnDestroy()
        {
            if (Owned != null)
                ColonistSceneDestroy.SafeDestroy(Owned);
        }
    }
}
