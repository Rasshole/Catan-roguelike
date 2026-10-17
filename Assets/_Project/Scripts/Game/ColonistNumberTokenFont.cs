using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Roboto Slab Bold (Apache-2.0) — closest free bold-serif to classic Catan number chips.</summary>
    public static class ColonistNumberTokenFont
    {
        private const string ResourcePath = "Fonts/RobotoSlab-Bold";
        private static Font _cached;

        public static Font Get()
        {
            if (_cached != null)
                return _cached;

            _cached = Resources.Load<Font>(ResourcePath);
            return _cached;
        }
    }
}
