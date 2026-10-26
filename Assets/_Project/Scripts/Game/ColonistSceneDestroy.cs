using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Destroy/DestroyImmediate safe for EditMode tests and PlayMode.</summary>
    public static class ColonistSceneDestroy
    {
        public static void SafeDestroy(Object obj)
        {
            if (obj == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(obj);
                return;
            }
#endif
            Object.Destroy(obj);
        }
    }
}
