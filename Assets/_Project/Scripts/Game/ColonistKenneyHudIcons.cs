using System.Collections.Generic;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>Kenney Board Game Icons (CC0) for colonist HUD stat rows and build buttons.</summary>
    internal static class ColonistKenneyHudIcons
    {
        private const string Root = "Art/HudIcons";
        private static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Victory => Get("StatVictory");
        public static Sprite Resources => Get("StatResources");
        public static Sprite DevCard => Get("StatDevCard");
        public static Sprite Road => Get("StatRoad");
        public static Sprite Knight => Get("StatKnight");
        public static Sprite Army => Get("StatArmy");
        public static Sprite BuildRoad => Get("BuildRoad");
        public static Sprite BuildSettlement => Get("BuildSettlement");
        public static Sprite BuildCity => Get("BuildCity");

        public static Sprite BuildIconFor(string shortTitle)
        {
            if (shortTitle == ColonistHudCopy.Road)
                return BuildRoad;
            if (shortTitle == ColonistHudCopy.Settlement || shortTitle == ColonistHudCopy.SettlementShort)
                return BuildSettlement;
            if (shortTitle == ColonistHudCopy.City)
                return BuildCity;
            if (shortTitle == ColonistHudCopy.DevCard || shortTitle == "Dev")
                return DevCard;
            return null;
        }

        private static Sprite Get(string file)
        {
            if (Cache.TryGetValue(file, out var sprite) && sprite != null)
                return sprite;

            var tex = UnityEngine.Resources.Load<Texture2D>($"{Root}/{file}"); // property "Resources" shadows the class
            if (tex == null)
                return null;

            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            Cache[file] = sprite;
            return sprite;
        }
    }
}
