using System.Collections.Generic;
using CatanRoguelike.Core;
using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    internal static class ColonistHudResourceSprites
    {
        private static readonly Dictionary<ResourceType, Sprite> Cache = new();

        public static Sprite Get(ResourceType resource)
        {
            if (Cache.TryGetValue(resource, out var sprite) && sprite != null)
                return sprite;

            var texture = ColonistResourceIconTextures.Get(resource);
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
