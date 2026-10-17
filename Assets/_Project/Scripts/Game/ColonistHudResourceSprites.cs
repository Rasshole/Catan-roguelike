using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistHudResourceSprites
    {
        private static readonly Dictionary<ResourceType, Sprite> Cache = new();
        private static Sprite _fish;

        /// <summary>Fishermen hand-card glyph (Art/Icons/Fish); falls back to Wheat if missing.</summary>
        public static Sprite GetFish()
        {
            if (_fish != null)
                return _fish;
            var texture = UnityEngine.Resources.Load<Texture2D>("Art/Icons/Fish");
            if (texture == null)
                return Get(ResourceType.Wheat);
            _fish = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            return _fish;
        }

        public static Sprite Get(ResourceType resource)
        {
            if (Cache.TryGetValue(resource, out var sprite) && sprite != null)
                return sprite;

            var texture = ColonistKenneyResourceIcons.TryGet(resource)
                ?? ColonistResourceIconTextures.GetForHud(resource);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            Cache[resource] = sprite;
            return sprite;
        }
    }
}
