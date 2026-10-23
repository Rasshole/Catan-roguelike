using System.Collections.Generic;
using CatanRoguelike.Core.Civilizations;
using CatanRoguelike.Core.Events;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>
    /// v131 painted roguelike art: civilization emblems (Resources/Art/Civilizations), event category emblems
    /// (Resources/Art/Events) and parchment card frames (Resources/Art/Cards). Built by tools/art/make_emblems.py.
    /// </summary>
    public static class ColonistRoguelikeArt
    {
        public const string CivilizationFolder = "Art/Civilizations";
        public const string EventFolder = "Art/Events";
        public const string CardFolder = "Art/Cards";
        public const string PortraitFrame = "CardFramePortrait";
        public const string WideFrame = "CardFrameWide";

        private static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite GetCivilizationEmblem(CivilizationId id) =>
            CivilizationLibrary.TryGet(id, out var def) ? Load(CivilizationFolder + "/" + def.EmblemKey) : null;

        public static Sprite GetEventEmblem(EventId id) =>
            id == EventId.None ? null : Load(EventFolder + "/" + EventLibrary.Get(id).Category);

        public static Sprite GetEventCategoryEmblem(EventCategory category) => Load(EventFolder + "/" + category);

        public static Sprite GetCardFrame(bool wide) => Load(CardFolder + "/" + (wide ? WideFrame : PortraitFrame));

        /// <summary>Texture-pixel slice border of the painted card frames (wood moulding + gold corner piece).</summary>
        public const float CardFrameSliceBorder = 180f;

        /// <summary>
        /// v132: the wide painted frame as a 9-slice so the event card can be any height (it hugs its text)
        /// and any width. Set Image.pixelsPerUnitMultiplier to texWidth / cardWidth to keep proportions.
        /// </summary>
        public static Sprite GetCardFrameSliced(bool wide)
        {
            string path = CardFolder + "/" + (wide ? WideFrame : PortraitFrame) + "#sliced";
            if (Cache.TryGetValue(path, out var cached) && cached != null)
                return cached;
            var tex = Resources.Load<Texture2D>(CardFolder + "/" + (wide ? WideFrame : PortraitFrame));
            if (tex == null)
                return null;
            float b = CardFrameSliceBorder;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprite.name = path;
            Cache[path] = sprite;
            return sprite;
        }

        private static Sprite Load(string path)
        {
            if (Cache.TryGetValue(path, out var cached) && cached != null)
                return cached;
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
                return null;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = path;
            Cache[path] = sprite;
            return sprite;
        }
    }
}
