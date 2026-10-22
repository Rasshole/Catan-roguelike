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
